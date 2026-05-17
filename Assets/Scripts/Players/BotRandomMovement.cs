using Unity.Netcode;
using UnityEngine;
using KingOfTheHill.Players;

namespace KingOfTheHill.AI
{
    [RequireComponent(typeof(CharacterController))]
    public class BotRandomMovement : MonoBehaviour
    {
        private CharacterController _cc;
        private PlayerNetworkSync _sync;
        private PlayerStats _stats;

        private Vector2 _moveInput;
        private float _changeTimer;
        private float _attackTimer;

        [SerializeField] private float speed = 4f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float pushDecay = 5f;
        private float _verticalVelocity;
        private Vector3 _pushVelocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _sync = GetComponent<PlayerNetworkSync>();
            _stats = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return; // Solo el servidor controla a la IA
            if (_stats != null && !_stats.IsAlive.Value) return;

            // ==============================================================
            // DEBUG: IA desactivada temporalmente a petición del usuario.
            // Los bots funcionarán como maniquíes estacionarios para pruebas de combate.
            // ==============================================================
            
            /*
            // Cambiar dirección aleatoriamente
            _changeTimer -= Time.deltaTime;
            if (_changeTimer <= 0f)
            {
                _changeTimer = Random.Range(1f, 3f);
                _moveInput = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            }

            // Atacar aleatoriamente si hay un PlayerCombat
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                _attackTimer = Random.Range(2f, 5f);
                if (TryGetComponent(out PlayerCombat combat))
                {
                    combat.OnAttack();
                }
            }
            */
            
            _moveInput = Vector2.zero;

            // Aplicar gravedad
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            // Movimiento absoluto en el mundo para evitar que gire sobre sí mismo infinitamente
            Vector3 worldMove = new Vector3(_moveInput.x, 0, _moveInput.y) * speed;
            worldMove.y = _verticalVelocity;

            worldMove += _pushVelocity;
            _pushVelocity = Vector3.Lerp(_pushVelocity, Vector3.zero, pushDecay * Time.deltaTime);

            _cc.Move(worldMove * Time.deltaTime);

            // Rotar hacia donde intenta moverse
            Vector3 flatMove = new Vector3(_moveInput.x, 0, _moveInput.y);
            if (flatMove.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatMove), Time.deltaTime * 5f);
            }
        }

        public void ApplyPush(Vector3 pushVector)
        {
            _pushVelocity += pushVector;
        }
    }
}
