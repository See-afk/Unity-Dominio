using UnityEngine;

namespace Dominio.Managers
{
    /// <summary>
    /// Contenedor de datos globales del jugador que persisten entre escenas.
    /// No es un MonoBehaviour, es un singleton de datos puro.
    /// </summary>
    public static class GameData
    {
        // ── Nombre del jugador local ─────────────────────────────────────
        private static string _playerName = "Jugador";

        public static string PlayerName
        {
            get => _playerName;
            set => _playerName = string.IsNullOrWhiteSpace(value) ? "Jugador" : value.Trim();
        }

        // ── Colores disponibles para identificar jugadores ───────────────
        public static readonly Color32[] PlayerColors = new Color32[]
        {
            new Color32(255, 80,  80,  255), // Rojo
            new Color32(80,  160, 255, 255), // Azul
            new Color32(80,  255, 120, 255), // Verde
            new Color32(255, 200, 60,  255), // Amarillo
            new Color32(200, 80,  255, 255), // Púrpura
            new Color32(255, 140, 60,  255), // Naranja
            new Color32(60,  220, 220, 255), // Cian
            new Color32(255, 100, 180, 255), // Rosa
        };

        // ── Índice de color seleccionado por el jugador local ────────────
        public static int PlayerColorIndex { get; set; } = 0;

        public static Color32 PlayerColor => PlayerColors[PlayerColorIndex % PlayerColors.Length];

        // ── Dirección IP para conectarse como cliente ────────────────────
        public static string JoinAddress { get; set; } = "127.0.0.1";

        // ── Puerto ───────────────────────────────────────────────────────
        public const ushort DefaultPort = 7777;

        // ── Resetea todos los datos (para cuando se sale al menú) ────────
        public static void Reset()
        {
            _playerName      = "Jugador";
            PlayerColorIndex = 0;
            JoinAddress      = "127.0.0.1";
        }
    }
}
