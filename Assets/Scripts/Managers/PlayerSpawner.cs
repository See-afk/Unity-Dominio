using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using KingOfTheHill.Players;

namespace KingOfTheHill.Managers
{
    /// <summary>
    /// Gestiona los puntos de spawn y hace respawn de jugadores.
    /// Solo corre en el servidor.
    /// Optimización: usa un array de SpawnPoints en vez de GameObject.FindWithTag
    /// (Find es muy costoso en runtime).
    /// </summary>
    public class PlayerSpawner : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Prefab")]
        [SerializeField] private NetworkObject playerPrefab;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;


        // ─── Estado ───────────────────────────────────────────────────────────────
        private int _spawnIndex;

        // ─────────────────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // Suscribirse a conexiones/desconexiones
            NetworkManager.Singleton.OnClientConnectedCallback    += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   += OnClientDisconnected;

            // Hacer spawn del host (cliente 0 ya está conectado)
            Debug.Log("[PlayerSpawner] Intentando hacer spawn del Host (LocalClientId: " + NetworkManager.Singleton.LocalClientId + ")");
            SpawnPlayer(NetworkManager.Singleton.LocalClientId);

            // Spawnea a los bots usando los datos persistentes de LobbyPlayerData
            var lobbyPlayers = FindObjectsByType<Dominio.Managers.LobbyPlayerData>(FindObjectsInactive.Exclude);
            if (lobbyPlayers != null)
            {
                int botIndex = 1;
                foreach (var p in lobbyPlayers)
                {
                    if (p != null && p.IsBot.Value)
                    {
                        SpawnBot(botIndex, p.PlayerName.Value.ToString());
                        botIndex++;
                    }
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        // ─── Callbacks de red ─────────────────────────────────────────────────────

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            if (clientId == NetworkManager.Singleton.LocalClientId) return; // host ya spawneado

            SpawnPlayer(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // El NetworkObject se destruye automáticamente; no necesitamos hacer nada extra.
        }

        // ─── Spawn ────────────────────────────────────────────────────────────────

        private void SpawnPlayer(ulong clientId)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] playerPrefab no asignado.");
                return;
            }

            Transform spawnPoint = GetNextSpawnPoint();

            NetworkObject netObj = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation);

            netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            Debug.Log($"[PlayerSpawner] Jugador {clientId} instanciado y Spawneado con éxito en {spawnPoint.position}");


        }

        private void SpawnBot(int botIndex, string botName)
        {
            if (playerPrefab == null) return;

            Transform spawnPoint = GetNextSpawnPoint();

            // Usamos un padre inactivo para evitar que PlayerInput mande el warning en OnEnable
            GameObject tempParent = new GameObject("BotTempParent");
            tempParent.SetActive(false);

            NetworkObject netObj = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                tempParent.transform);

            // Destruir Input antes de que el objeto despierte
            if (netObj.TryGetComponent(out UnityEngine.InputSystem.PlayerInput pInput))
            {
                DestroyImmediate(pInput);
            }

            // Sacarlo del padre y destruirlo
            netObj.transform.SetParent(null);
            Destroy(tempParent);

            netObj.Spawn(destroyWithScene: true); // Objeto del servidor
            Debug.Log($"[PlayerSpawner] Bot {botIndex} ({botName}) instanciado en {spawnPoint.position}");


            // Añadir IA
            netObj.gameObject.AddComponent<KingOfTheHill.AI.BotRandomMovement>();
            
            // Asignar nombre
            if (netObj.TryGetComponent(out PlayerStats stats))
            {
                stats.PlayerName.Value = botName;
            }
        }

        private Transform GetNextSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform;   // fallback: posición del spawner

            Transform point = spawnPoints[_spawnIndex % spawnPoints.Length];
            _spawnIndex++;
            return point;
        }

        // ─── Respawn público (llamado desde PlayerStats cuando muere) ─────────────

        /// <summary>Teletransporta al jugador a un nuevo spawn point.</summary>
        public void RespawnPlayer(PlayerController player)
        {
            if (!IsServer) return;

            Transform point = GetNextSpawnPoint();
            player.Teleport(point.position, point.rotation);
        }
    }
}
