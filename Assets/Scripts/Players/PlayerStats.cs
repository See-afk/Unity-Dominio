using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace KingOfTheHill.Players
{
    /// <summary>
    /// Contiene y sincroniza las estadísticas del jugador por red.
    /// Optimización: solo NetworkVariables para evitar RPCs innecesarios.
    /// </summary>
    public class PlayerStats : NetworkBehaviour
    {
        // ─── Constantes ───────────────────────────────────────────────────────────
        public const float MaxHealth = 100f;

        // ─── NetworkVariables (sincronizadas automáticamente) ─────────────────────
        public NetworkVariable<float> Health = new NetworkVariable<float>(
            MaxHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> TeamIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
            new FixedString32Bytes("Player"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ─── Eventos locales (no de red) ──────────────────────────────────────────
        public event Action<float, float> OnHealthChanged;   // (newHP, maxHP)
        public event Action                OnDied;
        public event Action                OnRespawned;

        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Respawn")]
        [SerializeField] private float respawnDelay = 5f;

        private float _respawnTimer;

        // ─────────────────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            Health.OnValueChanged     += HandleHealthChanged;
            IsAlive.OnValueChanged    += HandleAliveChanged;
        }

        public override void OnNetworkDespawn()
        {
            Health.OnValueChanged     -= HandleHealthChanged;
            IsAlive.OnValueChanged    -= HandleAliveChanged;
        }

        private void HandleHealthChanged(float oldVal, float newVal)
        {
            OnHealthChanged?.Invoke(newVal, MaxHealth);
        }

        private void HandleAliveChanged(bool oldVal, bool newVal)
        {
            if (!newVal) OnDied?.Invoke();
            else         OnRespawned?.Invoke();
        }

        // ─── API (solo llamar desde Server) ───────────────────────────────────────

        /// <summary>Aplica daño. Llamar solo en el servidor.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage)
        {
            if (!IsAlive.Value) return;

            Health.Value = Mathf.Max(0f, Health.Value - damage);

            if (Health.Value <= 0f)
            {
                IsAlive.Value = false;
                ScheduleRespawn();
            }
        }

        /// <summary>Cura al jugador. Llamar solo en el servidor.</summary>
        public void Heal(float amount)
        {
            if (!IsSpawned) return;
            Health.Value = Mathf.Min(MaxHealth, Health.Value + amount);
        }

        private void ScheduleRespawn()
        {
            _respawnTimer = respawnDelay;
        }

        private void Update()
        {
            if (!IsServer) return;
            if (IsAlive.Value) return;

            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
                Respawn();
        }

        private void Respawn()
        {
            Health.Value  = MaxHealth;
            IsAlive.Value = true;
        }
    }
}
