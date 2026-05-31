using Dominio.Managers;
using Unity.Netcode;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace KingOfTheHill.Players
{
    /// <summary>
    /// Punto de entrada del jugador: coordina movimiento, combate, sync, HUD y datos visuales.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerNetworkSync))]
    [RequireComponent(typeof(PlayerHUD))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Camara")]
        [SerializeField] private GameObject playerCamera;

        [Header("Referencias")]
        [SerializeField] private SkinnedMeshRenderer bodyRenderer;

        private PlayerStats _stats;
        private PlayerMovement _movement;
        private PlayerCombat _combat;
        private PlayerNetworkSync _netSync;
        private PlayerHUD _hud;
        private Renderer _bodyRendererFallback;

        private Vector3 _originalCamPos;
        private Quaternion _originalCamRot;

        public Transform PlayerCameraTransform => playerCamera != null ? playerCamera.transform : null;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<PlayerCombat>();
            _netSync = GetComponent<PlayerNetworkSync>();
            _hud = GetComponent<PlayerHUD>();
            _bodyRendererFallback = bodyRenderer != null ? bodyRenderer : GetComponentInChildren<Renderer>();

            // Deshabilitar PlayerInput inmediatamente para evitar que intente vincular controles
            // a jugadores remotos durante el Instantiate (evita el error de "Cannot find matching control scheme").
            if (TryGetComponent(out UnityEngine.InputSystem.PlayerInput pi))
            {
                pi.enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (playerCamera != null)
                playerCamera.SetActive(IsLocalPlayer && IsPlayingPhase());

            _movement.enabled = IsOwner;
            _combat.enabled = IsOwner;
            _netSync.enabled = true;
            _hud.enabled = true;

            if (TryGetComponent(out UnityEngine.InputSystem.PlayerInput pi))
            {
                pi.enabled = IsOwner;
            }

            _stats.OnDied += HandleDied;
            _stats.OnRespawned += HandleRespawned;
            _stats.TeamIndex.OnValueChanged += HandleTeamChanged;

            if (IsServer && _stats.PlayerName.Value.ToString() == "Player")
                _stats.PlayerName.Value = new Unity.Collections.FixedString32Bytes($"Player {OwnerClientId}");

            ApplyPlayerColor(_stats.TeamIndex.Value);

            if (playerCamera != null)
            {
                _originalCamPos = playerCamera.transform.localPosition;
                _originalCamRot = playerCamera.transform.localRotation;
            }
        }

        public override void OnNetworkDespawn()
        {
            _stats.OnDied -= HandleDied;
            _stats.OnRespawned -= HandleRespawned;
            _stats.TeamIndex.OnValueChanged -= HandleTeamChanged;
        }

        private bool IsPlayingPhase()
        {
            if (Managers.GamePhaseManager.Singleton == null) return true;
            return Managers.GamePhaseManager.Singleton.CurrentPhase.Value == Managers.GamePhase.Playing;
        }

        private void Update()
        {
            if (!IsLocalPlayer) return;

            if (playerCamera != null)
            {
                bool shouldBeActive = IsPlayingPhase();
                if (playerCamera.activeSelf != shouldBeActive)
                    playerCamera.SetActive(shouldBeActive);
            }
        }

        private void HandleDied()
        {
            GetComponent<CharacterController>().enabled = false;

            if (IsOwner)
                SetVisibleServerRpc(false);
                
            if (IsLocalPlayer)
            {
                _localRespawnTimer = _stats.GetRespawnDelay();
            }
        }

        private void HandleRespawned()
        {
            GetComponent<CharacterController>().enabled = true;

            if (IsOwner)
                SetVisibleServerRpc(true);

            if (IsLocalPlayer)
            {
                RestoreCinemachineTarget();
                _spectatedTarget = null;
            }
        }

        [ServerRpc]
        private void SetVisibleServerRpc(bool visible)
        {
            SetVisibleClientRpc(visible);
        }

        [ClientRpc]
        private void SetVisibleClientRpc(bool visible)
        {
            Renderer rendererToUse = GetBodyRenderer();
            if (rendererToUse != null)
                rendererToUse.enabled = visible;
        }

        private void HandleTeamChanged(int oldValue, int newValue)
        {
            ApplyPlayerColor(newValue);
        }

        private void ApplyPlayerColor(int colorIndex)
        {
            Renderer rendererToUse = GetBodyRenderer();
            if (rendererToUse == null || GameData.PlayerColors.Length == 0) return;

            Color32 color = GameData.PlayerColors[Mathf.Abs(colorIndex) % GameData.PlayerColors.Length];
            rendererToUse.material.color = color;
        }

        private Renderer GetBodyRenderer()
        {
            if (_bodyRendererFallback == null)
                _bodyRendererFallback = bodyRenderer != null ? bodyRenderer : GetComponentInChildren<Renderer>();

            return _bodyRendererFallback;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerNameServerRpc(string name)
        {
            _stats.PlayerName.Value = new Unity.Collections.FixedString32Bytes(name);
        }

        public void SetTeam(int teamIndex)
        {
            if (!IsServer) return;
            _stats.TeamIndex.Value = teamIndex;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            TeleportClientRpc(position, rotation);
        }

        [ClientRpc]
        private void TeleportClientRpc(Vector3 position, Quaternion rotation)
        {
            var cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = true;

            if (IsLocalPlayer)
            {
                SnapCamera();
            }
        }

        private void SnapCamera()
        {
            var topDownTarget = Object.FindAnyObjectByType<KingOfTheHill.Gameplay.TopDownCameraTarget>();
            if (topDownTarget != null)
            {
                topDownTarget.SnapToPlayer();
            }

#if UNITY_6000_0_OR_NEWER
            var cmCam = Object.FindAnyObjectByType<CinemachineCamera>();
            if (cmCam != null)
            {
                cmCam.enabled = false;
                cmCam.enabled = true;
            }
#else
            var cmCam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
            if (cmCam != null)
            {
                cmCam.PreviousStateIsValid = false;
            }
#endif
        }

        private void LateUpdate()
        {
            if (!IsLocalPlayer) return;

            if (!_stats.IsAlive.Value && IsPlayingPhase())
            {
                SpectateBestPlayer();
            }
        }

        private PlayerController _spectatedTarget;
        private float _spectateCheckTimer;
        private float _localRespawnTimer;

        private void SpectateBestPlayer()
        {
            _localRespawnTimer -= Time.deltaTime;
            
            _spectateCheckTimer -= Time.deltaTime;
            if (_spectatedTarget == null || !_spectatedTarget.GetComponent<PlayerStats>().IsAlive.Value || _spectateCheckTimer <= 0f)
            {
                _spectateCheckTimer = 1f;
                var newTarget = FindBestPlayerToSpectate();

                if (newTarget != null && newTarget != _spectatedTarget)
                {
                    _spectatedTarget = newTarget;
                    ChangeCinemachineTarget(_spectatedTarget.transform);
                }
            }
            
            UpdateRespawnHUD();
        }

        private void UpdateRespawnHUD()
        {
            if (_hud == null) return;
            
            string timeStr = Mathf.Max(0, Mathf.CeilToInt(_localRespawnTimer)).ToString();
            string nameStr = "Nadie";
            
            if (_spectatedTarget != null && _spectatedTarget.TryGetComponent<PlayerStats>(out var stats))
            {
                nameStr = stats.PlayerName.Value.ToString();
            }
            _hud.SetRespawnText($"{nameStr}\n{timeStr}");
        }

        private Transform _originalCinemachineFollow;
        private Transform _originalCinemachineLookAt;
        private bool _cinemachineTargetSaved = false;

        private void ChangeCinemachineTarget(Transform newTarget)
        {
#if UNITY_6000_0_OR_NEWER
            var cmCam = Object.FindAnyObjectByType<CinemachineCamera>();
            if (cmCam != null)
            {
                var targets = cmCam.Target;
                if (!_cinemachineTargetSaved)
                {
                    _originalCinemachineFollow = targets.TrackingTarget;
                    _originalCinemachineLookAt = targets.LookAtTarget;
                    _cinemachineTargetSaved = true;
                }
                
                targets.TrackingTarget = newTarget;
                if (targets.LookAtTarget != null) targets.LookAtTarget = newTarget;
                cmCam.Target = targets;
            }
#else
            var cmCam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
            if (cmCam != null)
            {
                if (!_cinemachineTargetSaved)
                {
                    _originalCinemachineFollow = cmCam.Follow;
                    _originalCinemachineLookAt = cmCam.LookAt;
                    _cinemachineTargetSaved = true;
                }
                
                cmCam.Follow = newTarget;
                if (cmCam.LookAt != null) cmCam.LookAt = newTarget;
            }
#endif
            SnapCamera();
        }

        private void RestoreCinemachineTarget()
        {
            if (!_cinemachineTargetSaved) return;

#if UNITY_6000_0_OR_NEWER
            var cmCam = Object.FindAnyObjectByType<CinemachineCamera>();
            if (cmCam != null)
            {
                var targets = cmCam.Target;
                targets.TrackingTarget = _originalCinemachineFollow;
                targets.LookAtTarget = _originalCinemachineLookAt;
                cmCam.Target = targets;
            }
#else
            var cmCam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
            if (cmCam != null)
            {
                cmCam.Follow = _originalCinemachineFollow;
                cmCam.LookAt = _originalCinemachineLookAt;
            }
#endif
            _cinemachineTargetSaved = false;
            SnapCamera();
        }

        private PlayerController FindBestPlayerToSpectate()
        {
            PlayerStats[] players = FindObjectsByType<PlayerStats>();
            System.Collections.Generic.List<PlayerStats> candidates = new System.Collections.Generic.List<PlayerStats>();
            int highestScore = -1;

            foreach (var p in players)
            {
                if (!p.IsSpawned || !p.IsAlive.Value || p == _stats) continue;

                int score = p.Score.Value;
                if (score > highestScore)
                {
                    highestScore = score;
                    candidates.Clear();
                    candidates.Add(p);
                }
                else if (score == highestScore)
                {
                    candidates.Add(p);
                }
            }

            if (candidates.Count > 0)
            {
                int rnd = Random.Range(0, candidates.Count);
                return candidates[rnd].GetComponent<PlayerController>();
            }

            return null;
        }
    }
}
