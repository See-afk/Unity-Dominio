using Dominio.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dominio.UI
{
    /// <summary>
    /// Controlador del Canvas del Lobby.
    /// Se activa cuando el jugador se conecta (host o cliente).
    /// Muestra la lista de jugadores con su nombre y color.
    /// El Host tiene un botón "Iniciar Partida".
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        // ── Cabecera ─────────────────────────────────────────────────────
        [Header("Cabecera")]
        [SerializeField] private TMP_Text txtLobbyTitle;
        [SerializeField] private TMP_Text txtPlayerCount;   // "3 / 8 jugadores"

        // ── Lista de jugadores ───────────────────────────────────────────
        [Header("Lista de Jugadores")]
        [SerializeField] private Transform          playerListContainer;  // ScrollView > Content
        [SerializeField] private GameObject         playerEntryPrefab;    // Prefab de fila de jugador

        // ── Botones inferiores ───────────────────────────────────────────
        [Header("Botones")]
        [SerializeField] private Button btnStartGame;   // Solo visible para el Host
        [SerializeField] private Button btnLeave;

        [Header("Feedback")]
        [SerializeField] private TMP_Text txtWaiting;   // "Esperando jugadores..."
        [SerializeField] private TMP_Text txtStatusMsg; // Mensajes de estado

        // ────────────────────────────────────────────────────────────────
        private void Awake()
        {
            btnStartGame.onClick.AddListener(OnStartGame);
            btnLeave.onClick.AddListener(OnLeave);
        }

        private void OnEnable()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyUpdated    += RefreshPlayerList;
                LobbyManager.Instance.OnConnectionFailed += OnConnectionFailed;
            }

            RefreshPlayerList();
        }

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyUpdated    -= RefreshPlayerList;
                LobbyManager.Instance.OnConnectionFailed -= OnConnectionFailed;
            }
        }

        // ── Actualizar lista ─────────────────────────────────────────────
        private void RefreshPlayerList()
        {
            // Limpiar lista actual
            foreach (Transform child in playerListContainer)
                Destroy(child.gameObject);

            var players = LobbyManager.Instance?.Players;
            int count   = players?.Count ?? 0;

            // Contador
            if (txtPlayerCount != null)
                txtPlayerCount.text = $"{count} / 8 jugadores";

            // Generar filas
            if (players != null)
            {
                foreach (var player in players)
                {
                    if (player == null) continue;
                    var entry = Instantiate(playerEntryPrefab, playerListContainer);
                    ConfigureEntry(entry, player);
                }
            }

            // Botón iniciar solo para el host
            bool isHost = LobbyManager.Instance?.IsHost ?? false;
            btnStartGame.gameObject.SetActive(isHost);

            // Mensaje de espera
            if (txtWaiting != null)
                txtWaiting.gameObject.SetActive(count < 2);

            // Título
            if (txtLobbyTitle != null)
                txtLobbyTitle.text = isHost ? "Tu Lobby" : "Lobby";
        }

        // ── Configurar fila de jugador ───────────────────────────────────
        private static void ConfigureEntry(GameObject entry, LobbyPlayerData player)
        {
            // Color del jugador (strip lateral)
            var colorStrip = entry.transform.Find("ColorStrip")?.GetComponent<Image>();
            if (colorStrip != null)
                colorStrip.color = player.DisplayColor;

            // Avatar iniciales (está dentro de "Avatar")
            var txtAvatar = entry.transform.Find("Avatar/TxtAvatar")?.GetComponent<TMP_Text>();
            if (txtAvatar != null && player.DisplayName.Length > 0)
                txtAvatar.text = player.DisplayName[0].ToString().ToUpper();

            // Nombre del jugador (está dentro de "NameBlock")
            var txtName = entry.transform.Find("NameBlock/TxtName")?.GetComponent<TMP_Text>();
            if (txtName != null)
                txtName.text = player.DisplayName;

            // Insignia de Host (está dentro de "NameBlock")
            var hostBadge = entry.transform.Find("NameBlock/HostBadge");
            if (hostBadge != null)
                hostBadge.gameObject.SetActive(player.IsHostPlayer);

            // Estado "Listo"
            var readyIcon = entry.transform.Find("ReadyIcon");
            if (readyIcon != null)
                readyIcon.gameObject.SetActive(player.IsReady.Value);
        }

        // ── Acciones ─────────────────────────────────────────────────────
        private void OnStartGame()
        {
            if (txtStatusMsg != null) txtStatusMsg.text = "Iniciando partida...";
            btnStartGame.interactable = false;
            LobbyManager.Instance?.StartGame();
        }

        private void OnLeave()
        {
            LobbyManager.Instance?.Disconnect();
            // Volver a mostrar el menú (el LobbyUI pertenece a la misma escena)
            gameObject.SetActive(false);
        }

        private void OnConnectionFailed(string message)
        {
            if (txtStatusMsg != null) txtStatusMsg.text = $"[!] {message}";
        }
    }
}
