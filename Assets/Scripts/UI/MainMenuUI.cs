using System.Net;
using System.Net.Sockets;
using Dominio.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dominio.UI
{
    /// <summary>
    /// Controlador del Canvas principal del Menú.
    /// Gestiona 3 paneles:
    ///   1. PanelMain     - Botones "Crear" y "Unirse"
    ///   2. PanelCreate   - Formulario para crear lobby (nombre)
    ///   3. PanelJoin     - Formulario para unirse (nombre + IP)
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        // ── Paneles ──────────────────────────────────────────────────────
        [Header("Paneles")]
        [SerializeField] private GameObject panelMain;
        [SerializeField] private GameObject panelCreate;
        [SerializeField] private GameObject panelJoin;

        // ── Panel Main ───────────────────────────────────────────────────
        [Header("Panel Principal")]
        [SerializeField] private Button btnCreate;
        [SerializeField] private Button btnJoin;
        [SerializeField] private Button btnQuit;

        // ── Panel Crear ──────────────────────────────────────────────────
        [Header("Panel Crear Lobby")]
        [SerializeField] private TMP_InputField inputNameCreate;
        [SerializeField] private Transform      colorSelectorCreate;   // padre con botones de color
        [SerializeField] private Button         btnConfirmCreate;
        [SerializeField] private Button         btnBackCreate;
        [SerializeField] private TMP_Text       txtLocalIP;            // muestra la IP al host

        // ── Panel Unirse ─────────────────────────────────────────────────
        [Header("Panel Unirse")]
        [SerializeField] private TMP_InputField inputNameJoin;
        [SerializeField] private TMP_InputField inputIP;
        [SerializeField] private Transform      colorSelectorJoin;
        [SerializeField] private Button         btnConfirmJoin;
        [SerializeField] private Button         btnBackJoin;

        // ── Feedback ─────────────────────────────────────────────────────
        [Header("Feedback")]
        [SerializeField] private GameObject     panelError;
        [SerializeField] private TMP_Text       txtError;

        // ── Selección de color ───────────────────────────────────────────
        private int _selectedColorCreate = 0;
        private int _selectedColorJoin   = 0;

        // ────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Panel principal
            btnCreate.onClick.AddListener(ShowCreate);
            btnJoin.onClick.AddListener(ShowJoin);
            btnQuit.onClick.AddListener(QuitGame);

            // Panel crear
            btnConfirmCreate.onClick.AddListener(OnConfirmCreate);
            btnBackCreate.onClick.AddListener(ShowMain);

            // Panel unirse
            btnConfirmJoin.onClick.AddListener(OnConfirmJoin);
            btnBackJoin.onClick.AddListener(ShowMain);

            // Configurar selección de colores
            SetupColorButtons(colorSelectorCreate, isCreate: true);
            SetupColorButtons(colorSelectorJoin,   isCreate: false);

            // Esconder error
            if (panelError != null) panelError.SetActive(false);
        }

        private void Start()
        {
            ShowMain();

            // Suscribir al error del LobbyManager
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnConnectionFailed += ShowError;
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnConnectionFailed -= ShowError;
        }

        // ── Navegación ───────────────────────────────────────────────────
        private void ShowMain()
        {
            panelMain.SetActive(true);
            panelCreate.SetActive(false);
            panelJoin.SetActive(false);
        }

        private void ShowCreate()
        {
            panelMain.SetActive(false);
            panelCreate.SetActive(true);
            panelJoin.SetActive(false);

            // Mostrar IP local para que el host se la comparta a los demás
            if (txtLocalIP != null)
                txtLocalIP.text = $"Tu IP: {GetLocalIP()}";

            // Valor por defecto
            inputNameCreate.text = GameData.PlayerName;
            SelectColor(0, isCreate: true);
        }

        private void ShowJoin()
        {
            panelMain.SetActive(false);
            panelCreate.SetActive(false);
            panelJoin.SetActive(true);

            inputNameJoin.text = GameData.PlayerName;
            inputIP.text       = GameData.JoinAddress;
            SelectColor(0, isCreate: false);
        }

        // ── Acciones ─────────────────────────────────────────────────────
        private void OnConfirmCreate()
        {
            if (!ValidateName(inputNameCreate.text)) return;

            GameData.PlayerName       = inputNameCreate.text;
            GameData.PlayerColorIndex = _selectedColorCreate;

            LobbyManager.Instance.StartHost();
        }

        private void OnConfirmJoin()
        {
            if (!ValidateName(inputNameJoin.text)) return;
            if (!ValidateIP(inputIP.text)) return;

            GameData.PlayerName       = inputNameJoin.text;
            GameData.PlayerColorIndex = _selectedColorJoin;
            GameData.JoinAddress      = inputIP.text.Trim();

            LobbyManager.Instance.JoinAsClient();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Validaciones ─────────────────────────────────────────────────
        private bool ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
            {
                ShowError("El nombre debe tener al menos 2 caracteres.");
                return false;
            }
            return true;
        }

        private bool ValidateIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip.Trim(), out _))
            {
                ShowError("IP no válida. Ejemplo: 192.168.1.100");
                return false;
            }
            return true;
        }

        // ── Color Selector ───────────────────────────────────────────────
        private void SetupColorButtons(Transform parent, bool isCreate)
        {
            if (parent == null) return;

            int count = Mathf.Min(parent.childCount, GameData.PlayerColors.Length);
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var btn = parent.GetChild(i).GetComponent<Button>();
                if (btn == null) continue;

                // Pintar el botón con el color del jugador
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = GameData.PlayerColors[idx];

                btn.onClick.AddListener(() => SelectColor(idx, isCreate));
            }
        }

        private void SelectColor(int idx, bool isCreate)
        {
            if (isCreate) _selectedColorCreate = idx;
            else          _selectedColorJoin   = idx;

            // Marcar visualmente el seleccionado (escala)
            var parent = isCreate ? colorSelectorCreate : colorSelectorJoin;
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                float scale = (i == idx) ? 1.3f : 1.0f;
                parent.GetChild(i).localScale = Vector3.one * scale;
            }
        }

        // ── Error ────────────────────────────────────────────────────────
        private void ShowError(string message)
        {
            if (panelError == null) return;
            panelError.SetActive(true);
            if (txtError != null) txtError.text = message;
            Invoke(nameof(HideError), 3f);
        }

        private void HideError() => panelError?.SetActive(false);

        // ── Utilidad IP ──────────────────────────────────────────────────
        private static string GetLocalIP()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "Desconocida";
            }
            catch { return "Desconocida"; }
        }
    }
}
