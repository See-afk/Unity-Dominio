using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Dominio.Managers
{
    /// <summary>
    /// NetworkBehaviour instanciado por cada jugador al conectarse.
    /// Almacena nombre y color via NetworkVariables para que todos los
    /// clientes puedan leerlos y mostrarlos en el lobby.
    ///
    /// Este prefab NO tiene representación visual en la escena del lobby;
    /// es un objeto de datos puro que usa DontDestroyOnLoad para persistir
    /// hasta que el jugador se desconecte.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class LobbyPlayerData : NetworkBehaviour
    {
        // ── NetworkVariables (solo el Owner escribe, todos leen) ─────────
        public NetworkVariable<FixedString64Bytes> PlayerName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public NetworkVariable<int> ColorIndex = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public NetworkVariable<bool> IsReady = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public NetworkVariable<bool> IsBot = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        // ────────────────────────────────────────────────────────────────
        public override void OnNetworkSpawn()
        {
            // Persistir entre escenas mientras el jugador esté conectado
            DontDestroyOnLoad(gameObject);

            // Registrar en el manager local
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.RegisterPlayer(this);

            // Si somos el Owner y no somos un bot, asignamos nuestros datos de GameData directamente
            // (Ya que los NetworkVariables tienen WritePermission = Owner)
            if (IsOwner && !IsBot.Value)
            {
                PlayerName.Value = GameData.PlayerName;
                ColorIndex.Value = GameData.PlayerColorIndex;
            }

            // Suscribirse a cambios para actualizar UI
            PlayerName.OnValueChanged += (_, _) => LobbyManager.Instance?.OnLobbyUpdated?.Invoke();
            ColorIndex.OnValueChanged += (_, _) => LobbyManager.Instance?.OnLobbyUpdated?.Invoke();
            IsReady.OnValueChanged    += (_, _) => LobbyManager.Instance?.OnLobbyUpdated?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.UnregisterPlayer(this);
        }

        // ── Helpers públicos para el Owner ───────────────────────────────
        public void SetReady(bool ready)
        {
            if (IsOwner) IsReady.Value = ready;
        }

        public void ChangeColor(int colorIndex)
        {
            if (IsOwner) ColorIndex.Value = colorIndex;
        }

        // ── Acceso conveniente ───────────────────────────────────────────
        public string  DisplayName  => PlayerName.Value.ToString();
        public Color32 DisplayColor => GameData.PlayerColors[ColorIndex.Value % GameData.PlayerColors.Length];
        public bool    IsHostPlayer => IsOwnedByServer;
    }
}
