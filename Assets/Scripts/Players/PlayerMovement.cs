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

        [Header("Física")]
        [SerializeField] private float pushDecay = 5f;

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
        private Vector3 _pushVelocity;
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
            // Si no somos el dueño, o si somos el dueño pero no es nuestro objeto de jugador principal (ej: bots)
            if (!IsOwner || !NetworkObject.IsPlayerObject)
            {
                // Deshabilitar input si no se destruyó
                if (_playerInput != null)
                    _playerInput.enabled = false;

                // Deshabilitar cámara y audio
                if (cameraRoot != null)
                {
                    var cam = cameraRoot.GetComponentInChildren<Camera>(true);
                    if (cam != null) cam.enabled = false;

                    var listener = cameraRoot.GetComponentInChildren<AudioListener>(true);
                    if (listener != null) listener.enabled = false;
                }
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
            if (!IsOwner || !NetworkObject.IsPlayerObject) return;
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

        public void OnJump() => Jump();
        private void OnJump(InputAction.CallbackContext ctx) => Jump();

        private void Jump()
        {
            if (!IsOwner || !NetworkObject.IsPlayerObject || !_stats.IsAlive.Value) return;
            if (_cc.isGrounded)
            {
                _verticalVelocity = jumpForce;
                Managers.GamePhaseManager.Singleton?.PlayJumpSound();
            }
        }

        public void OnCrouch() => Crouch();
        private void OnCrouch(InputAction.CallbackContext ctx) => Crouch();

        private void Crouch()
        {
            if (!IsOwner || !NetworkObject.IsPlayerObject) return;
            _isCrouching  = !_isCrouching;
            _cc.height    = _isCrouching ? _crouchHeight : _standHeight;
            _cc.center    = Vector3.up * (_cc.height * 0.5f);

            if (TryGetComponent(out PlayerNetworkSync sync))
                sync.SetCrouching(_isCrouching);
        }

        // ─── Movimiento ───────────────────────────────────────────────────────────

        private void HandleLook()
        {
            // Para el modo Top-Down / MOBA en móvil, la rotación manual con ratón se desactiva.
            // El personaje girará automáticamente hacia donde camina (ver HandleMove).
            
            /*
            transform.Rotate(Vector3.up, _lookInput.x * lookSensitivity);

            _cameraPitch -= _lookInput.y * lookSensitivity;
            _cameraPitch  = Mathf.Clamp(_cameraPitch, -verticalClamp, verticalClamp);

            if (cameraRoot != null)
                cameraRoot.localEulerAngles = new Vector3(_cameraPitch, 0f, 0f);
            */
        }

        private void HandleMove()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += gravity * Time.deltaTime;

            float speed = _isCrouching ? crouchSpeed
                        : _isSprinting ? sprintSpeed
                        : walkSpeed;

            // En lugar de usar la rotación del jugador, usamos la rotación de la cámara
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            if (Camera.main != null)
            {
                forward = Camera.main.transform.forward;
                forward.y = 0;
                forward.Normalize();

                right = Camera.main.transform.right;
                right.y = 0;
                right.Normalize();
            }

            Vector3 move = right * _moveInput.x + forward * _moveInput.y;

            // --- Auto-rotación hacia donde caminamos (Estilo MOBA/Brawler) ---
            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 15f * Time.deltaTime);
            }

            move *= speed;
            move.y = _verticalVelocity;

            move += _pushVelocity;
            _pushVelocity = Vector3.Lerp(_pushVelocity, Vector3.zero, pushDecay * Time.deltaTime);

            _cc.Move(move * Time.deltaTime);
        }

        private void HandleGravityOnly()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += gravity * Time.deltaTime;
            
            Vector3 move = new Vector3(0, _verticalVelocity, 0);
            move += _pushVelocity;
            _pushVelocity = Vector3.Lerp(_pushVelocity, Vector3.zero, pushDecay * Time.deltaTime);
            
            _cc.Move(move * Time.deltaTime);
        }

        [ClientRpc]
        public void ApplyPushClientRpc(Vector3 pushVector)
        {
            if (!IsOwner) return;
            _pushVelocity += pushVector;

            if (TryGetComponent(out KingOfTheHill.AI.BotRandomMovement botMove))
            {
                botMove.ApplyPush(pushVector);
            }
        }
    }
}
