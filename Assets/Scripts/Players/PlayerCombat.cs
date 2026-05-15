using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KingOfTheHill.Players
{
    /// <summary>
    /// Maneja el combate cuerpo a cuerpo del jugador.
    /// Usa PlayerInput en vez de clase generada.
    /// Optimización: OverlapSphereNonAlloc + hashes de Animator cacheados.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(Animator))]
    public class PlayerCombat : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Ataque")]
        [SerializeField] private float     attackDamage   = 20f;
        [SerializeField] private float     attackRange    = 1.8f;
        [SerializeField] private float     attackCooldown = 0.6f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Animaciones")]
        [SerializeField] private string attackAnimParam = "Attack";
        [SerializeField] private string hitAnimParam    = "Hit";
        [SerializeField] private string dieAnimParam    = "Die";

        // ─── Privados ─────────────────────────────────────────────────────────────
        private PlayerStats  _stats;
        private Animator     _animator;
        private PlayerInput  _playerInput;

        private InputAction _attackAction;
        private float       _attackTimer;

        // Buffer sin alloc (máx 8 coliders)
        private readonly Collider[] _hitBuffer = new Collider[8];

        // Hashes cacheados para evitar string lookup
        private int _attackHash;
        private int _hitHash;
        private int _dieHash;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _stats       = GetComponent<PlayerStats>();
            _animator    = GetComponent<Animator>();
            _playerInput = GetComponent<PlayerInput>();

            _attackHash = Animator.StringToHash(attackAnimParam);
            _hitHash    = Animator.StringToHash(hitAnimParam);
            _dieHash    = Animator.StringToHash(dieAnimParam);
        }

        public override void OnNetworkSpawn()
        {
            _stats.OnDied      += HandleDied;
            _stats.OnRespawned += HandleRespawned;

            if (!IsOwner) return;

            _attackAction = _playerInput.actions["Attack"];
            _attackAction.performed += OnAttackPerformed;
        }

        public override void OnNetworkDespawn()
        {
            _stats.OnDied      -= HandleDied;
            _stats.OnRespawned -= HandleRespawned;

            if (!IsOwner || _attackAction == null) return;
            _attackAction.performed -= OnAttackPerformed;
        }

        private void Update()
        {
            if (_attackTimer > 0f)
                _attackTimer -= Time.deltaTime;
        }

        // ─── Ataque ───────────────────────────────────────────────────────────────

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsOwner || !_stats.IsAlive.Value) return;
            
            // No permitir ataque si no estamos en fase de juego
            if (Managers.GamePhaseManager.Singleton != null && 
                Managers.GamePhaseManager.Singleton.CurrentPhase.Value != Managers.GamePhase.Playing)
                return;

            if (_attackTimer > 0f) return;

            _attackTimer = attackCooldown;
            _animator.SetTrigger(_attackHash);

            PerformAttackServerRpc();
        }

        [ServerRpc]
        private void PerformAttackServerRpc()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position + transform.forward * (attackRange * 0.5f),
                attackRange,
                _hitBuffer,
                playerLayer);

            for (int i = 0; i < count; i++)
            {
                if (_hitBuffer[i] == null) continue;
                if (_hitBuffer[i].transform.root == transform.root) continue;

                if (_hitBuffer[i].TryGetComponent(out PlayerStats target))
                {
                    target.TakeDamageServerRpc(attackDamage);
                    PlayHitEffectClientRpc(target.OwnerClientId);
                }
            }
        }

        [ClientRpc]
        private void PlayHitEffectClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
            _animator.SetTrigger(_hitHash);
        }

        // ─── Muerte / Respawn ─────────────────────────────────────────────────────

        private void HandleDied()    => _animator.SetTrigger(_dieHash);
        private void HandleRespawned() => _animator.Rebind();
    }
}
