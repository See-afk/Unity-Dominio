using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingOfTheHill
{
    /// <summary>
    /// Helper de runtime para iniciar rápidamente como Host o Client durante pruebas.
    /// Muestra un menú simple en pantalla al presionar Play.
    /// NO es para producción: solo para testing en Editor.
    /// </summary>
    public class NetworkStarterHelper : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("UI (opcional — se crea automáticamente si está vacío)")]
        [SerializeField] private Canvas quickMenuCanvas;

        private bool _menuVisible = true;
        private GUIStyle _boxStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;

        // ─────────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_menuVisible) return;
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsListening) return;  // ya conectado

            // Inicializar estilos una sola vez
            InitStyles();

            float w = 280f, h = 160f;
            float x = (Screen.width  - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), "", _boxStyle);

            GUILayout.BeginArea(new Rect(x + 10, y + 10, w - 20, h - 20));

            GUILayout.Label("⚙  Rey de la Colina — LAN", _labelStyle);
            GUILayout.Space(10);

            if (GUILayout.Button("▶  Iniciar como HOST", _buttonStyle, GUILayout.Height(38)))
            {
                NetworkManager.Singleton.StartHost();
                _menuVisible = false;
            }

            GUILayout.Space(6);

            if (GUILayout.Button("🔗  Unirse como CLIENT", _buttonStyle, GUILayout.Height(38)))
            {
                NetworkManager.Singleton.StartClient();
                _menuVisible = false;
            }

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeColorTexture(new Color(0.1f, 0.1f, 0.15f, 0.95f)) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.white, background = MakeColorTexture(new Color(0.2f, 0.5f, 0.9f)) },
                hover     = { textColor = Color.white, background = MakeColorTexture(new Color(0.3f, 0.65f, 1f)) }
            };
        }

        private static Texture2D MakeColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
