using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KingOfTheHill.Players
{
    /// <summary>
    /// Controla el movimiento 3D del jugador.
    /// Usa PlayerInput (componente Inspector) en lugar de la clase generada,
    /// para no depender de la generación de código del .inputactions.
    /// Optimización: sin allocs en Update, CharacterController en vez de Rigidbody.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerMovement : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Velocidades")]
        [SerializeField] private float walkSpeed   = 5f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float jumpForce   = 7f;
        [SerializeField] private float gravity     = -20f;

        [Header("Cámara")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private float lookSensitivity = 0.15f;
        [SerializeField] private float verticalClamp   = 80f;

        // ─── Privados ─────────────────────────────────────────────────────────────
        private CharacterController _cc;
        private PlayerInput         _playerInput;
        private PlayerStats         _stats;

        // Acciones cacheadas (sin string lookup por frame)
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;

        // Estado (sin allocs)
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private float   _verticalVelocity;
        private float   _cameraPitch;
        private bool    _isSprinting;
        private bool    _isCrouching;

        private float _standHeight;
        private float _crouchHeight;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _cc          = GetComponent<CharacterController>();
            _playerInput = GetComponent<PlayerInput>();
            _stats       = GetComponent<PlayerStats>();

            _standHeight  = _cc.height;
            _crouchHeight = _cc.height * 0.55f;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                // Clientes remotos: deshabilitar PlayerInput para no procesar input
                _playerInput.enabled = false;
                return;
            }

            // Cachear acciones una sola vez
            var actions = _playerInput.actions;
            _moveAction   = actions["Move"];
            _lookAction   = actions["Look"];
            _jumpAction   = actions["Jump"];
            _sprintAction = actions["Sprint"];
            _crouchAction = actions["Crouch"];

            // Suscribir botones (solo eventos, sin polling)
            _jumpAction.performed   += OnJump;
            _sprintAction.started   += ctx => _isSprinting = true;
            _sprintAction.canceled  += ctx => _isSprinting = false;
            _crouchAction.performed += OnCrouch;

#if !UNITY_ANDROID
            Cursor.lockState = CursorLockMode.Locked;
#endif
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner || _jumpAction == null) return;

            _jumpAction.performed   -= OnJump;
            _sprintAction.started   -= ctx => _isSprinting = true;
            _sprintAction.canceled  -= ctx => _isSprinting = false;
            _crouchAction.performed -= OnCrouch;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (!_stats.IsAlive.Value) return;

            // No permitir movimiento si no estamos en fase de juego
            if (Managers.GamePhaseManager.Singleton != null && 
                Managers.GamePhaseManager.Singleton.CurrentPhase.Value != Managers.GamePhase.Playing)
            {
                if (Managers.GamePhaseManager.Singleton.CurrentPhase.Value == Managers.GamePhase.Finished)
                    return;

                // Asegurarnos de que el jugador caiga por gravedad aunque no se pueda mover
                HandleGravityOnly();
                return;
            }

            _moveInput = _moveAction.ReadValue<Vector2>();
            _lookInput = _lookAction.ReadValue<Vector2>();

            // Fallback a los joysticks virtuales si no hay input del teclado/gamepad
            if (KingOfTheHill.UI.MobileInputBridge.Instance != null)
            {
                Vector2 touchMove = KingOfTheHill.UI.MobileInputBridge.Instance.GetMoveInput();
                Vector2 touchLook = KingOfTheHill.UI.MobileInputBridge.Instance.GetLookInput();
                
                if (touchMove.sqrMagnitude > 0.01f) _moveInput = touchMove;
                if (touchLook.sqrMagnitude > 0.01f) _lookInput = touchLook;
            }

            HandleLook();
            HandleMove();
        }

        // ─── Callbacks ────────────────────────────────────────────────────────────

        private void OnJump(InputAction.CallbackContext ctx)
        {
            if (!IsOwner || !_stats.IsAlive.Value) return;
            if (_cc.isGrounded)
                _verticalVelocity = jumpForce;
        }

        private void OnCrouch(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            _isCrouching  = !_isCrouching;
            _cc.height    = _isCrouching ? _crouchHeight : _standHeight;
            _cc.center    = Vector3.up * (_cc.height * 0.5f);

            if (TryGetComponent(out PlayerNetworkSync sync))
                sync.SetCrouching(_isCrouching);
        }

        // ─── Movimiento ───────────────────────────────────────────────────────────

        private void HandleLook()
        {
            transform.Rotate(Vector3.up, _lookInput.x * lookSensitivity);

            _cameraPitch -= _lookInput.y * lookSensitivity;
            _cameraPitch  = Mathf.Clamp(_cameraPitch, -verticalClamp, verticalClamp);

            if (cameraRoot != null)
                cameraRoot.localEulerAngles = new Vector3(_cameraPitch, 0f, 0f);
        }

        private void HandleMove()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += gravity * Time.deltaTime;

            float speed = _isCrouching ? crouchSpeed
                        : _isSprinting ? sprintSpeed
                        : walkSpeed;

            Vector3 move = transform.right   * _moveInput.x
                         + transform.forward * _moveInput.y;
            move   *= speed;
            move.y  = _verticalVelocity;

            _cc.Move(move * Time.deltaTime);
        }

        private void HandleGravityOnly()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += gravity * Time.deltaTime;
            _cc.Move(new Vector3(0, _verticalVelocity * Time.deltaTime, 0));
        }
    }
}
