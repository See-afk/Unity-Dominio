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

        [Header("Teams")]
        [SerializeField] private int teamsCount = 2;

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

            // Asignar equipo en round-robin
            if (netObj.TryGetComponent(out PlayerController controller))
            {
                int team = (int)(clientId % (ulong)teamsCount);
                controller.SetTeam(team);
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
