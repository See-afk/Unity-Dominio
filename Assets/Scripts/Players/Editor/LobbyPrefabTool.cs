#if UNITY_EDITOR
using Dominio.Managers;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dominio.Editor
{
    /// <summary>
    /// Crea los prefabs necesarios para el lobby:
    ///   1. LobbyPlayerData_Prefab  (NetworkObject invisible, datos de red)
    ///   2. PlayerEntry_Prefab      (fila visual en el ScrollView del lobby)
    /// </summary>
    public static class LobbyPrefabTool
    {
        private const string kPrefabsDir = "Assets/Prefabs";

        [MenuItem("Tools/Dominio/4 - Create Lobby Prefabs")]
        public static void CreateLobbyPrefabs()
        {
            System.IO.Directory.CreateDirectory(kPrefabsDir);
            CreateLobbyPlayerDataPrefab();
            CreatePlayerEntryPrefab();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Listo",
                "Prefabs creados en Assets/Prefabs/:\n• LobbyPlayerData_Prefab\n• PlayerEntry_Prefab\n\n" +
                "Arrastra LobbyPlayerData_Prefab al campo 'Player Prefab' del NetworkManager.", "OK");
        }

        // ── LobbyPlayerData (objeto de red, sin gráficos) ────────────────
        private static void CreateLobbyPlayerDataPrefab()
        {
            var path = $"{kPrefabsDir}/LobbyPlayerData_Prefab.prefab";
            var go   = new GameObject("LobbyPlayerData_Prefab");

            go.AddComponent<NetworkObject>();
            go.AddComponent<LobbyPlayerData>();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[LobbyPrefabTool] Creado: {path}");
        }

        // ── PlayerEntry (fila visual en el lobby) ────────────────────────
        private static void CreatePlayerEntryPrefab()
        {
            var path = $"{kPrefabsDir}/PlayerEntry_Prefab.prefab";
            var root = new GameObject("PlayerEntry_Prefab");

            // Fondo de la fila
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.2f, 0.95f);
            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 100;
            le.minHeight = 90;

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 16;
            hlg.padding = new RectOffset(16, 16, 10, 10);
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
            hlg.childControlHeight = true;
            hlg.childControlWidth  = true;

            // ── Strip de color lateral ────────────────────────────────────
            var strip = new GameObject("ColorStrip");
            strip.transform.SetParent(root.transform, false);
            var stripImg = strip.AddComponent<Image>();
            stripImg.color = Color.white;
            var sle = strip.AddComponent<LayoutElement>();
            sle.preferredWidth = 8;
            sle.minWidth = 8;

            // ── Avatar (círculo con inicial) ──────────────────────────────
            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(root.transform, false);
            var avImg = avatar.AddComponent<Image>();
            avImg.color = new Color(0.2f, 0.2f, 0.3f);
            var ale = avatar.AddComponent<LayoutElement>();
            ale.preferredWidth  = 72;
            ale.preferredHeight = 72;

            var avTxtGO = new GameObject("TxtAvatar");
            avTxtGO.transform.SetParent(avatar.transform, false);
            var avTxt = avTxtGO.AddComponent<TextMeshProUGUI>();
            avTxt.text = "A";
            avTxt.fontSize = 36;
            avTxt.fontStyle = FontStyles.Bold;
            avTxt.alignment = TextAlignmentOptions.Center;
            avTxt.color = Color.white;
            var avRT = avTxtGO.GetComponent<RectTransform>();
            avRT.anchorMin = Vector2.zero; avRT.anchorMax = Vector2.one;
            avRT.offsetMin = avRT.offsetMax = Vector2.zero;

            // ── Bloque nombre + host badge ────────────────────────────────
            var nameBlock = new GameObject("NameBlock");
            nameBlock.transform.SetParent(root.transform, false);
            var nbVlg = nameBlock.AddComponent<VerticalLayoutGroup>();
            nbVlg.childAlignment = TextAnchor.MiddleLeft;
            nbVlg.childForceExpandWidth  = true;
            nbVlg.childForceExpandHeight = false;
            nbVlg.childControlWidth  = true;
            nbVlg.childControlHeight = true;
            var nble = nameBlock.AddComponent<LayoutElement>();
            nble.flexibleWidth = 1;

            var txtNameGO = new GameObject("TxtName");
            txtNameGO.transform.SetParent(nameBlock.transform, false);
            var txtName = txtNameGO.AddComponent<TextMeshProUGUI>();
            txtName.text = "NombreJugador";
            txtName.fontSize = 30;
            txtName.fontStyle = FontStyles.Bold;
            txtName.color = Color.white;

            var hostBadgeGO = new GameObject("HostBadge");
            hostBadgeGO.transform.SetParent(nameBlock.transform, false);
            var hostTxt = hostBadgeGO.AddComponent<TextMeshProUGUI>();
            hostTxt.text = "[HOST]";
            hostTxt.fontSize = 20;
            hostTxt.color = new Color(1f, 0.8f, 0.2f);
            hostBadgeGO.SetActive(false);

            // ── Ícono de listo ────────────────────────────────────────────
            var readyGO = new GameObject("ReadyIcon");
            readyGO.transform.SetParent(root.transform, false);
            var readyTxt = readyGO.AddComponent<TextMeshProUGUI>();
            readyTxt.text = "[OK]";
            readyTxt.fontSize = 26;
            readyTxt.color = new Color(0.2f, 0.9f, 0.4f);
            readyTxt.alignment = TextAlignmentOptions.Center;
            var rle = readyGO.AddComponent<LayoutElement>();
            rle.preferredWidth = 50;
            readyGO.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[LobbyPrefabTool] Creado: {path}");
        }
    }
}
#endif
