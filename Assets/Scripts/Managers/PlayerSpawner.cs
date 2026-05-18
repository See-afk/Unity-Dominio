using Dominio.Managers;
using KingOfTheHill.Players;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace KingOfTheHill.Managers
{
    /// <summary>
    /// Gestiona los puntos de spawn y crea jugadores usando los datos persistentes del lobby.
    /// </summary>
    public class PlayerSpawner : NetworkBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private NetworkObject playerPrefab;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        private int _spawnIndex;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Esperar a que la escena termine de cargar para TODOS antes de spawnear nada.
            // Si spawneamos aquí, los clientes que aún están en la pantalla de carga (MainMenu)
            // instanciarán los objetos en su escena actual y se destruirán al cambiar de escena.
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            }
        }

        private void HandleLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            if (!IsServer) return;

            // La escena ya cargó para todos, ahora sí podemos spawnear a los jugadores
            foreach (ulong clientId in clientsCompleted)
            {
                SpawnPlayer(clientId);
            }

            SpawnLobbyBots();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                if (NetworkManager.Singleton.SceneManager != null)
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            if (clientId == NetworkManager.Singleton.LocalClientId) return;

            SpawnPlayer(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
        }

        private void SpawnPlayer(ulong clientId)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] playerPrefab no asignado.");
                return;
            }

            Transform spawnPoint = GetNextSpawnPoint();
            NetworkObject netObj = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

            netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            ApplyLobbyDataToPlayer(clientId, netObj);

            Debug.Log($"[PlayerSpawner] Jugador {clientId} instanciado en {spawnPoint.position}");
        }

        private void SpawnLobbyBots()
        {
            LobbyPlayerData[] lobbyPlayers = FindObjectsByType<LobbyPlayerData>(FindObjectsInactive.Exclude);
            int botIndex = 1;

            for (int i = 0; i < lobbyPlayers.Length; i++)
            {
                LobbyPlayerData data = lobbyPlayers[i];
                if (data == null || !data.IsBot.Value) continue;

                SpawnBot(botIndex, data.PlayerName.Value.ToString(), data.ColorIndex.Value);
                botIndex++;
            }
        }

        private void SpawnBot(int botIndex, string botName, int colorIndex)
        {
            if (playerPrefab == null) return;

            Transform spawnPoint = GetNextSpawnPoint();

            GameObject tempParent = new GameObject("BotTempParent");
            tempParent.SetActive(false);

            NetworkObject netObj = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation, tempParent.transform);

            if (netObj.TryGetComponent(out UnityEngine.InputSystem.PlayerInput playerInput))
                DestroyImmediate(playerInput);

            netObj.transform.SetParent(null);
            Destroy(tempParent);

            netObj.Spawn(destroyWithScene: true);
            netObj.gameObject.AddComponent<KingOfTheHill.AI.BotRandomMovement>();

            if (netObj.TryGetComponent(out PlayerStats stats))
            {
                stats.PlayerName.Value = new FixedString32Bytes(botName);
                stats.TeamIndex.Value = colorIndex;
            }

            Debug.Log($"[PlayerSpawner] Bot {botIndex} ({botName}) instanciado en {spawnPoint.position}");
        }

        private void ApplyLobbyDataToPlayer(ulong clientId, NetworkObject playerObject)
        {
            if (playerObject == null || !playerObject.TryGetComponent(out PlayerStats stats))
                return;

            LobbyPlayerData lobbyData = FindLobbyData(clientId);
            if (lobbyData == null)
            {
                stats.PlayerName.Value = new FixedString32Bytes($"Player {clientId}");
                stats.TeamIndex.Value = (int)clientId;
                return;
            }

            stats.PlayerName.Value = new FixedString32Bytes(lobbyData.PlayerName.Value.ToString());
            stats.TeamIndex.Value = lobbyData.ColorIndex.Value;
        }

        private LobbyPlayerData FindLobbyData(ulong clientId)
        {
            LobbyPlayerData[] lobbyPlayers = FindObjectsByType<LobbyPlayerData>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lobbyPlayers.Length; i++)
            {
                if (lobbyPlayers[i] != null && lobbyPlayers[i].OwnerClientId == clientId && !lobbyPlayers[i].IsBot.Value)
                    return lobbyPlayers[i];
            }

            return null;
        }

        private Transform GetNextSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform;

            Transform point = spawnPoints[_spawnIndex % spawnPoints.Length];
            _spawnIndex++;
            return point;
        }

        public void RespawnPlayer(PlayerController player)
        {
            if (!IsServer) return;

            Transform point = GetNextSpawnPoint();
            player.Teleport(point.position, point.rotation);
        }
    }
}
