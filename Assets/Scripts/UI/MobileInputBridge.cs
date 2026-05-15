using KingOfTheHill.Players;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace KingOfTheHill.UI
{
    /// <summary>
    /// Puente entre los botones táctiles y el Input System en Android.
    /// Obtiene referencia al PlayerInput del jugador local y envía eventos
    /// a sus acciones usando OnScreenButton de Unity (sin código extra).
    /// Se autodesactiva en PC/Editor para no consumir recursos.
    /// </summary>
    public class MobileInputBridge : MonoBehaviour
    {
        [Header("Joysticks")]
        [SerializeField] private VirtualJoystick moveJoystick;
        [SerializeField] private VirtualJoystick lookJoystick;

        [Header("Botones")]
        [SerializeField] private UnityEngine.UI.Button jumpButton;
        [SerializeField] private UnityEngine.UI.Button attackButton;
        [SerializeField] private UnityEngine.UI.Button crouchButton;

        private PlayerInput _localPlayerInput;
        private InputAction _jumpAction;
        private InputAction _attackAction;
        private InputAction _crouchAction;

        public static MobileInputBridge Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            // Quitamos la desactivación en Editor para que puedas probarlo con el mouse
#if !UNITY_EDITOR && !UNITY_ANDROID
            gameObject.SetActive(false);
#endif
        }

        private void Start()
        {
            // Conectar botones UI
            if (jumpButton   != null) jumpButton.onClick.AddListener(OnJumpPressed);
            if (attackButton != null) attackButton.onClick.AddListener(OnAttackPressed);
            if (crouchButton != null) crouchButton.onClick.AddListener(OnCrouchPressed);
        }

        private void Update()
        {
            // Intentar encontrar el jugador local si aún no se ha encontrado
            if (_localPlayerInput == null)
                TryFindLocalPlayer();
        }

        private void TryFindLocalPlayer()
        {
            var players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude);
            foreach (var p in players)
            {
                if (!p.IsOwner) continue;

                _localPlayerInput = p.GetComponent<PlayerInput>();
                if (_localPlayerInput == null) continue;

                // Cachear acciones
                _jumpAction   = _localPlayerInput.actions["Jump"];
                _attackAction = _localPlayerInput.actions["Attack"];
                _crouchAction = _localPlayerInput.actions["Crouch"];
                break;
            }
        }

        // ─── Botones táctiles ─────────────────────────────────────────────────────
        // Unity no permite disparar InputActions manualmente desde código en la mayoría
        // de los casos, así que usamos SendMessage al PlayerInput para "simular"
        // el mensaje que el componente enviaría (behavior = Send Messages).

        private void OnJumpPressed()
        {
            if (_localPlayerInput == null) return;
            // El PlayerInput con "Send Messages" envía "OnJump", "OnAttack", etc.
            // al mismo GameObject. Lo simulamos así:
            _localPlayerInput.SendMessage("OnJump", SendMessageOptions.DontRequireReceiver);
        }

        private void OnAttackPressed()
        {
            if (_localPlayerInput == null) return;
            _localPlayerInput.SendMessage("OnAttack", SendMessageOptions.DontRequireReceiver);
        }

        private void OnCrouchPressed()
        {
            if (_localPlayerInput == null) return;
            _localPlayerInput.SendMessage("OnCrouch", SendMessageOptions.DontRequireReceiver);
        }

        public Vector2 GetMoveInput() => moveJoystick != null ? moveJoystick.Value : Vector2.zero;
        public Vector2 GetLookInput() => lookJoystick != null ? lookJoystick.Value : Vector2.zero;
    }
}
