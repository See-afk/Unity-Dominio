using Dominio.Managers;
using Unity.Netcode;
using UnityEngine;

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

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<PlayerCombat>();
            _netSync = GetComponent<PlayerNetworkSync>();
            _hud = GetComponent<PlayerHUD>();
            _bodyRendererFallback = bodyRenderer != null ? bodyRenderer : GetComponentInChildren<Renderer>();
        }

        public override void OnNetworkSpawn()
        {
            if (playerCamera != null)
                playerCamera.SetActive(IsLocalPlayer && IsPlayingPhase());

            _movement.enabled = IsOwner;
            _combat.enabled = IsOwner;
            _netSync.enabled = true;
            _hud.enabled = true;

            _stats.OnDied += HandleDied;
            _stats.OnRespawned += HandleRespawned;
            _stats.TeamIndex.OnValueChanged += HandleTeamChanged;

            if (IsServer && _stats.PlayerName.Value.ToString() == "Player")
                _stats.PlayerName.Value = new Unity.Collections.FixedString32Bytes($"Player {OwnerClientId}");

            ApplyPlayerColor(_stats.TeamIndex.Value);
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
        }

        private void HandleRespawned()
        {
            GetComponent<CharacterController>().enabled = true;

            if (IsOwner)
                SetVisibleServerRpc(true);
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
        }
    }
}
