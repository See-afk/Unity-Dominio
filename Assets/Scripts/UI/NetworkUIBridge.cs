using Dominio.Managers;
using Unity.Netcode;
using UnityEngine;

namespace Dominio.UI
{
    /// <summary>
    /// Puente entre los eventos de red (LobbyManager / NetworkManager)
    /// y los Canvas de la UI en la escena MainMenu_Scene.
    ///
    /// Controla cuál panel está visible:
    ///   - CanvasMainMenu  →  mientras el jugador no está conectado
    ///   - CanvasLobby     →  mientras está en el lobby esperando
    ///
    /// Coloca este componente en un GameObject vacío en MainMenu_Scene.
    /// </summary>
    public class NetworkUIBridge : MonoBehaviour
    {
        [Header("Canvas raíz")]
        [SerializeField] private GameObject canvasMainMenu;
        [SerializeField] private GameObject canvasLobby;

        // ────────────────────────────────────────────────────────────────
        private void Awake()
        {
            ShowMainMenu();
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback  += OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
                NetworkManager.Singleton.OnServerStarted            += OnServerStarted;
            }
            else
            {
                Debug.LogError("[NetworkUIBridge] NetworkManager.Singleton es null en Start.");
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback  -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
                NetworkManager.Singleton.OnServerStarted            -= OnServerStarted;
            }
        }

        // ── Cuando el servidor arranca (Host) ────────────────────────────
        private void OnServerStarted()
        {
            ShowLobby();
        }

        // ── Cuando un cliente se conecta ────────────────────────────────
        private void OnConnected(ulong clientId)
        {
            // Solo actuamos sobre el cliente local
            if (NetworkManager.Singleton.LocalClientId == clientId)
                ShowLobby();
        }

        // ── Cuando un cliente se desconecta ─────────────────────────────
        private void OnDisconnected(ulong clientId)
        {
            // Si el jugador local se desconecta (o pierde conexión), volver al menú
            if (NetworkManager.Singleton.LocalClientId == clientId ||
                !NetworkManager.Singleton.IsListening)
            {
                ShowMainMenu();
            }
        }

        // ── Navegación ───────────────────────────────────────────────────
        private void ShowMainMenu()
        {
            canvasMainMenu?.SetActive(true);
            canvasLobby?.SetActive(false);
        }

        private void ShowLobby()
        {
            canvasMainMenu?.SetActive(false);
            canvasLobby?.SetActive(true);
        }
    }
}
