using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace KingOfTheHill.Players
{
    /// <summary>
    /// Punto de entrada del jugador: coordina todos los subsistemas.
    /// Desactiva componentes de control en clientes remotos para no gastar CPU.
    /// Aplicando principio del PDF: "evita cálculos innecesarios" por jugador.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerNetworkSync))]
    [RequireComponent(typeof(PlayerHUD))]
    public class PlayerController : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Cámara")]
        [SerializeField] private GameObject playerCamera;   // cámara solo activa para Owner

        [Header("Referencias")]
        [SerializeField] private SkinnedMeshRenderer bodyRenderer;

        // ─── Subsistemas ──────────────────────────────────────────────────────────
        private PlayerStats       _stats;
        private PlayerMovement    _movement;
        private PlayerCombat      _combat;
        private PlayerNetworkSync _netSync;
        private PlayerHUD         _hud;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _stats    = GetComponent<PlayerStats>();
            _movement = GetComponent<PlayerMovement>();
            _combat   = GetComponent<PlayerCombat>();
            _netSync  = GetComponent<PlayerNetworkSync>();
            _hud      = GetComponent<PlayerHUD>();
        }

        public override void OnNetworkSpawn()
        {
            // ── Cámara: se activa solo si es LocalPlayer y la fase es Playing ───────
            if (playerCamera != null)
                playerCamera.SetActive(IsLocalPlayer && IsPlayingPhase());

            // ── Subsistemas de control: solo activos para dueño ───────────────────
            // Los clientes remotos no necesitan input, solo visualización y sync.
            _movement.enabled = IsOwner;
            _combat.enabled   = IsOwner;

            // ── Sincronización: activa para todos ─────────────────────────────────
            _netSync.enabled = true;
            _hud.enabled     = true;

            // ── Suscribirse a muerte/respawn ──────────────────────────────────────
            _stats.OnDied      += HandleDied;
            _stats.OnRespawned += HandleRespawned;

            // ── Poner nombre por defecto en el servidor ────────────────────────────
            if (IsServer)
                _stats.PlayerName.Value = new Unity.Collections.FixedString32Bytes(
                    $"Player {OwnerClientId}");
        }

        public override void OnNetworkDespawn()
        {
            _stats.OnDied      -= HandleDied;
            _stats.OnRespawned -= HandleRespawned;
        }

        private bool IsPlayingPhase()
        {
            if (Managers.GamePhaseManager.Singleton == null) return true; // Fallback
            return Managers.GamePhaseManager.Singleton.CurrentPhase.Value == Managers.GamePhase.Playing;
        }

        private void Update()
        {
            if (!IsLocalPlayer) return;
            
            // Verificamos robustamente si debemos tener la cámara activa.
            // Esto previene problemas de orden de inicialización entre el Manager y el Player.
            if (playerCamera != null)
            {
                bool shouldBeActive = IsPlayingPhase();
                if (playerCamera.activeSelf != shouldBeActive)
                {
                    playerCamera.SetActive(shouldBeActive);
                    Debug.Log($"[PlayerController] Cambiando estado de playerCamera a: {shouldBeActive}");
                }
            }
        }

        // ─── Muerte y Respawn ─────────────────────────────────────────────────────

        private void HandleDied()
        {
            // Desactivar colisiones localmente (el servidor ya gestionó el estado)
            GetComponent<CharacterController>().enabled = false;

            // Ocultar mesh en todos los clientes via RPC
            if (IsOwner)
                SetVisibleServerRpc(false);
        }

        private void HandleRespawned()
        {
            GetComponent<CharacterController>().enabled = true;

            if (IsOwner)
                SetVisibleServerRpc(true);
        }

        // ─── RPCs ─────────────────────────────────────────────────────────────────

        [ServerRpc]
        private void SetVisibleServerRpc(bool visible)
        {
            SetVisibleClientRpc(visible);
        }

        [ClientRpc]
        private void SetVisibleClientRpc(bool visible)
        {
            if (bodyRenderer != null)
                bodyRenderer.enabled = visible;
        }

        // ─── API pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Asigna nombre del jugador. Llamar desde el servidor o con ServerRpc.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerNameServerRpc(string name)
        {
            _stats.PlayerName.Value = new Unity.Collections.FixedString32Bytes(name);
        }

        /// <summary>
        /// Asigna equipo. Llamar desde el servidor.
        /// </summary>
        public void SetTeam(int teamIndex)
        {
            if (!IsServer) return;
            _stats.TeamIndex.Value = teamIndex;
        }

        /// <summary>
        /// Teletransporta al jugador a una posición (solo servidor).
        /// </summary>
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
