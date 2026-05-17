using System.Collections.Generic;
using KingOfTheHill.Managers;
using KingOfTheHill.Players;
using Unity.Netcode;
using UnityEngine;

namespace KingOfTheHill.Gameplay
{
    /// <summary>
    /// Zona de captura autoritativa en servidor.
    /// Los jugadores vivos dentro de la zona ganan puntos pasivos por segundo.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SphereCollider))]
    public class CaptureZone : NetworkBehaviour
    {
        public static CaptureZone ActiveZone { get; private set; }
        public Vector3 Center => transform.position;
        public float Radius => radius;

        [Header("Captura")]
        [SerializeField] private float radius = 4f;
        [SerializeField] private float pointsPerSecond = 3f;
        [SerializeField] private float scoringTickRate = 0.25f;
        [SerializeField] private bool requirePlayingPhase = true;

        [Header("Reubicacion")]
        [SerializeField] private bool relocateOnSpawn = true;
        [SerializeField] private float relocationInterval = 30f;
        [SerializeField] private Transform[] fixedSpawnPoints;
        [SerializeField] private Transform areaCenter;
        [SerializeField] private Vector2 areaSize = new Vector2(28f, 28f);
        [SerializeField] private float groundOffset = 0.05f;
        [SerializeField] private float groundProbeHeight = 30f;
        [SerializeField] private float groundProbeDistance = 80f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Deteccion")]
        [SerializeField] private LayerMask playerMask = ~0;
        [SerializeField] private int overlapBufferSize = 32;

        [Header("Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Light zoneLight;
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float pulseAmount = 0.08f;

        private readonly Dictionary<PlayerStats, float> _scoreBanks = new Dictionary<PlayerStats, float>();
        private readonly HashSet<PlayerStats> _playersInside = new HashSet<PlayerStats>();
        private readonly List<PlayerStats> _playersToRemove = new List<PlayerStats>();

        private NetworkVariable<Vector3> _syncedPosition = new NetworkVariable<Vector3>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<float> _syncedRadius = new NetworkVariable<float>(
            4f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private SphereCollider _trigger;
        private Collider[] _overlapResults;
        private Vector3 _baseVisualScale;
        private float _relocationTimer;
        private float _scoreTimer;

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.center = Vector3.up;

            _overlapResults = new Collider[Mathf.Max(4, overlapBufferSize)];

            if (visualRoot != null)
                _baseVisualScale = visualRoot.localScale;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.5f, radius);
            pointsPerSecond = Mathf.Max(0f, pointsPerSecond);
            scoringTickRate = Mathf.Max(0.05f, scoringTickRate);
            relocationInterval = Mathf.Max(0f, relocationInterval);
            overlapBufferSize = Mathf.Max(4, overlapBufferSize);

            var sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.isTrigger = true;
                sphere.center = Vector3.up;
                sphere.radius = radius;
            }
        }

        public override void OnNetworkSpawn()
        {
            ActiveZone = this;

            _syncedPosition.OnValueChanged += HandlePositionChanged;
            _syncedRadius.OnValueChanged += HandleRadiusChanged;

            if (IsServer)
            {
                ApplyRadius(radius);
                _syncedRadius.Value = radius;

                if (relocateOnSpawn)
                    Relocate();
                else
                    _syncedPosition.Value = transform.position;

                _relocationTimer = relocationInterval;
            }
            else
            {
                ApplyRadius(_syncedRadius.Value);
                ApplyPosition(_syncedPosition.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ActiveZone == this)
                ActiveZone = null;

            _syncedPosition.OnValueChanged -= HandlePositionChanged;
            _syncedRadius.OnValueChanged -= HandleRadiusChanged;
            _playersInside.Clear();
            _scoreBanks.Clear();
        }

        private void Update()
        {
            PulseVisual();

            if (!IsServer) return;
            if (requirePlayingPhase && !IsGamePlaying()) return;

            TickRelocation();
            TickScoring();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            PlayerStats stats = FindPlayerStats(other);
            if (IsValidPlayerInZone(stats))
            {
                _playersInside.Add(stats);
                stats.SetCaptureZoneState(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer) return;

            PlayerStats stats = FindPlayerStats(other);
            if (stats == null) return;

            _playersInside.Remove(stats);
            _scoreBanks.Remove(stats);
            stats.SetCaptureZoneState(false);
        }

        private void TickRelocation()
        {
            if (relocationInterval <= 0f) return;

            _relocationTimer -= Time.deltaTime;
            if (_relocationTimer > 0f) return;

            Relocate();
            _relocationTimer = relocationInterval;
        }

        private void TickScoring()
        {
            _scoreTimer += Time.deltaTime;
            if (_scoreTimer < scoringTickRate) return;

            RefreshPlayersInside();
            AwardScore(_scoreTimer);
            _scoreTimer = 0f;
        }

        private void AwardScore(float deltaTime)
        {
            float pointsThisTick = pointsPerSecond * deltaTime;
            if (pointsThisTick <= 0f) return;

            _playersToRemove.Clear();

            foreach (PlayerStats stats in _playersInside)
            {
                if (!IsValidPlayerInZone(stats))
                {
                    _playersToRemove.Add(stats);
                    continue;
                }

                stats.SetCaptureZoneState(true);
                _scoreBanks.TryGetValue(stats, out float bank);
                bank += pointsThisTick;

                int wholePoints = Mathf.FloorToInt(bank);
                if (wholePoints > 0)
                {
                    stats.AddScore(wholePoints);
                    bank -= wholePoints;
                }

                _scoreBanks[stats] = bank;
            }

            for (int i = 0; i < _playersToRemove.Count; i++)
            {
                _playersToRemove[i].SetCaptureZoneState(false);
                _playersInside.Remove(_playersToRemove[i]);
                _scoreBanks.Remove(_playersToRemove[i]);
            }
        }

        private void RefreshPlayersInside()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up,
                radius,
                _overlapResults,
                playerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                PlayerStats stats = FindPlayerStats(_overlapResults[i]);
                if (IsValidPlayerInZone(stats))
                {
                    _playersInside.Add(stats);
                    stats.SetCaptureZoneState(true);
                }
            }
        }

        [ContextMenu("Relocate Now")]
        public void Relocate()
        {
            if (!IsServer) return;

            Vector3 nextPosition = PickRandomPosition();
            ApplyPosition(nextPosition);
            _syncedPosition.Value = nextPosition;

            ClearCaptureStates();
            _playersInside.Clear();
            _scoreBanks.Clear();
            RefreshPlayersInside();
        }

        private void ClearCaptureStates()
        {
            foreach (PlayerStats stats in _playersInside)
            {
                if (stats != null)
                    stats.SetCaptureZoneState(false);
            }
        }

        private Vector3 PickRandomPosition()
        {
            if (fixedSpawnPoints != null && fixedSpawnPoints.Length > 0)
            {
                Transform point = fixedSpawnPoints[Random.Range(0, fixedSpawnPoints.Length)];
                if (point != null)
                    return ProjectToGround(point.position);
            }

            Vector3 center = areaCenter != null ? areaCenter.position : Vector3.zero;
            Vector3 random = new Vector3(
                center.x + Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                center.y,
                center.z + Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f));

            return ProjectToGround(random);
        }

        private Vector3 ProjectToGround(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * groundProbeHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + groundOffset;
            else
                position.y += groundOffset;

            return position;
        }

        private bool IsValidPlayerInZone(PlayerStats stats)
        {
            if (stats == null || !stats.IsSpawned || !stats.IsAlive.Value) return false;

            Vector3 delta = stats.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= radius * radius;
        }

        private PlayerStats FindPlayerStats(Collider other)
        {
            if (other == null) return null;
            return other.GetComponentInParent<PlayerStats>();
        }

        private bool IsGamePlaying()
        {
            if (GamePhaseManager.Singleton == null) return true;
            return GamePhaseManager.Singleton.CurrentPhase.Value == GamePhase.Playing;
        }

        private void HandlePositionChanged(Vector3 oldPosition, Vector3 newPosition)
        {
            ApplyPosition(newPosition);
        }

        private void HandleRadiusChanged(float oldRadius, float newRadius)
        {
            ApplyRadius(newRadius);
        }

        private void ApplyPosition(Vector3 position)
        {
            transform.position = position;
        }

        private void ApplyRadius(float newRadius)
        {
            radius = Mathf.Max(0.5f, newRadius);

            if (_trigger == null)
                _trigger = GetComponent<SphereCollider>();

            if (_trigger != null)
                _trigger.radius = radius;
        }

        private void PulseVisual()
        {
            if (visualRoot == null) return;

            if (_baseVisualScale == Vector3.zero)
                _baseVisualScale = visualRoot.localScale;

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            visualRoot.localScale = _baseVisualScale * pulse;

            if (zoneLight != null)
                zoneLight.intensity = 5f + Mathf.Sin(Time.time * pulseSpeed) * 1.25f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, radius);

            Vector3 center = areaCenter != null ? areaCenter.position : Vector3.zero;
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireCube(center, new Vector3(areaSize.x, 0.1f, areaSize.y));
        }
    }
}
