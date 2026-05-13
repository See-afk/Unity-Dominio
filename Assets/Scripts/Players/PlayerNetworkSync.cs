using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace KingOfTheHill.Players
{
    /// <summary>
    /// Sincroniza la posición, rotación y estado de animación del jugador por red.
    /// Optimización: interpolación en clientes remotos para suavizar con menor frecuencia
    /// de red. Solo el dueño envía datos; el servidor los retransmite.
    /// Basado en el patrón "owner-authoritative" para LAN de baja latencia.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerNetworkSync : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Interpolación")]
        [SerializeField] private float positionLerpSpeed = 15f;
        [SerializeField] private float rotationLerpSpeed = 15f;

        [Header("Animación")]
        [SerializeField] private string speedParam   = "Speed";
        [SerializeField] private string groundParam  = "IsGrounded";
        [SerializeField] private string crouchParam  = "IsCrouching";

        // ─── NetworkVariables de transformación ───────────────────────────────────
        private NetworkVariable<Vector3>    _netPosition = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // ─── NetworkVariables de animación ────────────────────────────────────────
        private NetworkVariable<float> _netSpeed     = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool>  _netGrounded  = new NetworkVariable<bool>(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool>  _netCrouching = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // ─── Privados ─────────────────────────────────────────────────────────────
        private Animator          _animator;
        private CharacterController _cc;

        private int _speedHash;
        private int _groundHash;
        private int _crouchHash;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _cc       = GetComponent<CharacterController>();

            _speedHash  = Animator.StringToHash(speedParam);
            _groundHash = Animator.StringToHash(groundParam);
            _crouchHash = Animator.StringToHash(crouchParam);
        }

        private void Update()
        {
            if (IsOwner)
                SendState();
            else
                ApplyState();
        }

        // ─── Propietario: envía estado ────────────────────────────────────────────

        private void SendState()
        {
            _netPosition.Value = transform.position;
            _netRotation.Value = transform.rotation;

            float speed = _cc != null
                ? new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude
                : 0f;

            _netSpeed.Value    = speed;
            _netGrounded.Value = _cc != null && _cc.isGrounded;

            // Animator local
            _animator.SetFloat(_speedHash,  speed);
            _animator.SetBool(_groundHash,  _netGrounded.Value);
        }

        // ─── Clientes remotos: aplica estado con interpolación ────────────────────

        private void ApplyState()
        {
            // Interpolación suave de posición/rotación
            transform.position = Vector3.Lerp(
                transform.position, _netPosition.Value,
                positionLerpSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, _netRotation.Value,
                rotationLerpSpeed * Time.deltaTime);

            // Animación
            _animator.SetFloat(_speedHash,  _netSpeed.Value);
            _animator.SetBool(_groundHash,  _netGrounded.Value);
            _animator.SetBool(_crouchHash,  _netCrouching.Value);
        }

        /// <summary>Llamado por PlayerMovement para sincronizar crouch.</summary>
        public void SetCrouching(bool value)
        {
            if (!IsOwner) return;
            _netCrouching.Value = value;
            _animator.SetBool(_crouchHash, value);
        }
    }
}
