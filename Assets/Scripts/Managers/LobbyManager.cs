using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dominio.Managers
{
    /// <summary>
    /// Gestor central del lobby. Controla la conexión Host/Cliente
    /// y la lista de jugadores conectados.
    /// Debe estar en la escena MainMenu_Scene junto al NetworkManager.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────
        public static LobbyManager Instance { get; private set; }

        [Header("Configuración de escena")]
        [Tooltip("Nombre de la escena de juego a cargar cuando el host inicia.")]
        [SerializeField] private string gameplaySceneName = "Gameplay_Scene";

        [Header("Lobby")]
        [Tooltip("Máximo de jugadores permitidos.")]
        [SerializeField] private int maxPlayers = 8;

        /// <summary>Máximo de jugadores permitidos en el lobby.</summary>
        public int MaxPlayers => maxPlayers;

        // ── Eventos ──────────────────────────────────────────────────────
        public System.Action            OnLobbyUpdated;
        public System.Action<string>    OnConnectionFailed;
        public System.Action            OnGameStarted;

        // ── Lista en memoria de jugadores en el lobby ────────────────────
        private readonly List<LobbyPlayerData> _players = new();
        public IReadOnlyList<LobbyPlayerData> Players => _players;

        // ── Estado ───────────────────────────────────────────────────────
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // ────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback    += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted              += OnServerStarted;
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback    -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted              -= OnServerStarted;
        }

        // ── Arrancar Red ─────────────────────────────────────────────────

        private bool _isStarting = false;

        /// <summary>Inicia como Host. Hace Shutdown primero si ya estaba corriendo.</summary>
        public void StartHost()
        {
            if (_isStarting) return;
            StartCoroutine(StartAfterShutdown(isHost: true));
        }

        /// <summary>Se une como Cliente. Hace Shutdown primero si ya estaba corriendo.</summary>
        public void JoinAsClient()
        {
            if (_isStarting) return;
            StartCoroutine(StartAfterShutdown(isHost: false));
        }

        private IEnumerator StartAfterShutdown(bool isHost)
        {
            _isStarting = true;
            // Si el NetworkManager ya está corriendo, apagarlo primero
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient))
            {
                Debug.Log("[LobbyManager] NetworkManager activo, haciendo Shutdown antes de iniciar.");
                NetworkManager.Singleton.Shutdown();
                // Esperar hasta que termine el proceso de Shutdown para liberar el socket UDP
                while (NetworkManager.Singleton.ShutdownInProgress)
                {
                    yield return null;
                }
                // Unos frames extra de seguridad para asegurar la liberación del puerto
                yield return new WaitForSeconds(0.1f);
            }

            _players.Clear();

            if (isHost)
            {
                ConfigureTransport(GameData.DefaultPort);
                if (!NetworkManager.Singleton.StartHost())
                    OnConnectionFailed?.Invoke("No se pudo iniciar el servidor.");
            }
            else
            {
                ConfigureTransport(GameData.DefaultPort, GameData.JoinAddress);
                if (!NetworkManager.Singleton.StartClient())
                    OnConnectionFailed?.Invoke("No se pudo conectar al servidor.");
            }

            _isStarting = false;
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient))
                NetworkManager.Singleton.Shutdown();

            _players.Clear();
        }

        private static void ConfigureTransport(ushort port, string address = "127.0.0.1")
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null) return;
            
            // "0.0.0.0" obliga al host a escuchar en TODAS las interfaces de red (LAN/WiFi),
            // no solo en localhost.
            transport.SetConnectionData(address, port, "0.0.0.0");
        }

        // ────────────────────────────────────────────────────────────────
        #region Callbacks de Red

        private void OnServerStarted()
        {
            // El host ya está listo, se unirá al lobby como primer jugador
            Debug.Log("[LobbyManager] Servidor iniciado.");
        }

        private void OnClientConnected(ulong clientId)
        {
            OnLobbyUpdated?.Invoke();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            _players.RemoveAll(p => p != null && p.OwnerClientId == clientId);
            OnLobbyUpdated?.Invoke();
        }

        #endregion

        // ────────────────────────────────────────────────────────────────
        #region Gestión de Jugadores en Lobby

        /// <summary>Registra un LobbyPlayerData cuando se instancia en red.</summary>
        public void RegisterPlayer(LobbyPlayerData player)
        {
            if (!_players.Contains(player))
                _players.Add(player);
            OnLobbyUpdated?.Invoke();
        }

        /// <summary>Elimina un LobbyPlayerData cuando se destruye.</summary>
        public void UnregisterPlayer(LobbyPlayerData player)
        {
            _players.Remove(player);
            OnLobbyUpdated?.Invoke();
        }

        /// <summary>Agrega un jugador controlado por la IA al lobby.</summary>
        public void AddBot()
        {
            if (!IsHost) return;
            if (_players.Count >= MaxPlayers) return;
            var prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (prefab == null) return;

            var go = Instantiate(prefab);
            var netObj = go.GetComponent<NetworkObject>();
            // Instanciar como objeto de red regular, propiedad del host
            netObj.Spawn();

            if (go.TryGetComponent(out LobbyPlayerData data))
            {
                data.IsBot.Value = true;
                data.PlayerName.Value = "Bot " + UnityEngine.Random.Range(10, 999);
                data.ColorIndex.Value = UnityEngine.Random.Range(0, GameData.PlayerColors.Length);
                data.IsReady.Value = true;
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────
        #region Iniciar Partida

        /// <summary>Solo el Host puede iniciar la partida.</summary>
        public void StartGame()
        {
            if (!IsHost) return;
            if (NetworkManager.Singleton.ConnectedClients.Count < 1) return;

            OnGameStarted?.Invoke();

            // Carga la escena de gameplay para TODOS los clientes
            NetworkManager.Singleton.SceneManager.LoadScene(
                gameplaySceneName,
                LoadSceneMode.Single
            );
        }

        #endregion
    }
}
