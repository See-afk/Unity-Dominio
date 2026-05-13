
#if UNITY_EDITOR
using Dominio.Managers;
using Dominio.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Dominio.Editor
{
    /// <summary>
    /// Menú de Editor: Tools > Dominio > Setup Scenes
    /// Crea Bootstrap_Scene y MainMenu_Scene con toda la jerarquía de Canvas.
    /// </summary>
    public static class SceneSetupTool
    {
        private const string kBootstrap = "Assets/Scenes/Bootstrap_Scene.unity";
        private const string kMainMenu  = "Assets/Scenes/MainMenu_Scene.unity";

        // ── Menú principal ────────────────────────────────────────────────
        [MenuItem("Tools/Dominio/1 - Setup Bootstrap Scene")]
        public static void SetupBootstrap()
        {
            var scene = CreateOrOpenScene(kBootstrap);

            // Limpiar objetos existentes
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            // ── Loader ────────────────────────────────────────────────────
            var loader = new GameObject("BootstrapLoader");
            loader.AddComponent<BootstrapLoader>();
            SceneManager.MoveGameObjectToScene(loader, scene);

            // ── Splash (texto simple) ─────────────────────────────────────
            var cam = new GameObject("Camera");
            var camComp = cam.AddComponent<Camera>();
            camComp.clearFlags = CameraClearFlags.SolidColor;
            camComp.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            SceneManager.MoveGameObjectToScene(cam, scene);

            SaveAndClose(scene, kBootstrap);
            Debug.Log("[SceneSetupTool] Bootstrap_Scene creada correctamente.");
            EditorUtility.DisplayDialog("Listo", "Bootstrap_Scene creada.\nAhora ejecuta Setup MainMenu Scene.", "OK");
        }

        [MenuItem("Tools/Dominio/2 - Setup MainMenu Scene")]
        public static void SetupMainMenu()
        {
            var scene = CreateOrOpenScene(kMainMenu);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            // ── Cámara ────────────────────────────────────────────────────
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var camComp = cam.AddComponent<Camera>();
            camComp.clearFlags = CameraClearFlags.SolidColor;
            camComp.backgroundColor = new Color(0.05f, 0.05f, 0.12f);
            cam.AddComponent<AudioListener>();
            SceneManager.MoveGameObjectToScene(cam, scene);

            // ── NetworkManager ────────────────────────────────────────────
            var nmGO = new GameObject("NetworkManager");
            var nm = nmGO.AddComponent<NetworkManager>();
            nmGO.AddComponent<UnityTransport>();
            // Asignar transport al NetworkManager via serialized property
            var so = new SerializedObject(nm);
            var tp = so.FindProperty("NetworkConfig.NetworkTransport");
            if (tp != null) { tp.objectReferenceValue = nmGO.GetComponent<UnityTransport>(); so.ApplyModifiedProperties(); }
            SceneManager.MoveGameObjectToScene(nmGO, scene);

            // ── Managers ──────────────────────────────────────────────────
            var mgr = new GameObject("LobbyManager");
            mgr.AddComponent<LobbyManager>();
            SceneManager.MoveGameObjectToScene(mgr, scene);

            // ── EventSystem (New Input System) ────────────────────────────
            var evSys = new GameObject("EventSystem");
            evSys.AddComponent<EventSystem>();
            evSys.AddComponent<InputSystemUIInputModule>();
            SceneManager.MoveGameObjectToScene(evSys, scene);

            // ── UIBridge ──────────────────────────────────────────────────
            var bridge = new GameObject("NetworkUIBridge");
            var bridgeComp = bridge.AddComponent<NetworkUIBridge>();
            SceneManager.MoveGameObjectToScene(bridge, scene);

            // ── Canvas MainMenu ───────────────────────────────────────────
            var canvasMenu = CreateCanvas("Canvas_MainMenu", scene);
            BuildMainMenuCanvas(canvasMenu);

            // ── Canvas Lobby ──────────────────────────────────────────────
            var canvasLobby = CreateCanvas("Canvas_Lobby", scene);
            BuildLobbyCanvas(canvasLobby);
            canvasLobby.SetActive(false);

            // ── Asignar referencias al Bridge ─────────────────────────────
            var bridgeSO = new SerializedObject(bridgeComp);
            bridgeSO.FindProperty("canvasMainMenu").objectReferenceValue = canvasMenu;
            bridgeSO.FindProperty("canvasLobby").objectReferenceValue    = canvasLobby;
            bridgeSO.ApplyModifiedProperties();

            SaveAndClose(scene, kMainMenu);
            Debug.Log("[SceneSetupTool] MainMenu_Scene creada correctamente.");
            EditorUtility.DisplayDialog("Listo",
                "MainMenu_Scene creada.\n\nPor favor conecta manualmente los campos de los componentes\nMainMenuUI y LobbyUI en el Inspector.", "OK");
        }

        [MenuItem("Tools/Dominio/3 - Add Scenes to Build Settings")]
        public static void AddScenesToBuild()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(kBootstrap, true),
                new EditorBuildSettingsScene(kMainMenu,  true),
                new EditorBuildSettingsScene("Assets/Scenes/Dev/Gameplay_Scene.unity", true),
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("[SceneSetupTool] Build Settings actualizados.");
            EditorUtility.DisplayDialog("Listo", "Escenas añadidas a Build Settings:\n0: Bootstrap\n1: MainMenu\n2: Gameplay", "OK");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static Scene CreateOrOpenScene(string path)
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "../", path)));
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void SaveAndClose(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
        }

        private static GameObject CreateCanvas(string name, Scene scene)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // Portrait Android
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        // ─────────────────────────────────────────────────────────────────
        #region Construir Canvas MainMenu

        private static void BuildMainMenuCanvas(GameObject canvas)
        {
            // Fondo
            var bg = CreateImage(canvas.transform, "Background",
                new Color(0.05f, 0.05f, 0.12f), Vector2.zero, Vector2.one);
            SetAnchors(bg, Vector2.zero, Vector2.one);

            // ── Panel Main ────────────────────────────────────────────────
            var panelMain = CreatePanel(canvas.transform, "PanelMain");
            CreateTitleText(panelMain.transform, "REY DE LA COLINA", 64);
            CreateSubtitleText(panelMain.transform, "Multijugador LAN - Android", 28);
            var btnCreate = CreateButton(panelMain.transform, "BtnCreate", "CREAR PARTIDA", new Color(0.9f, 0.3f, 0.2f));
            var btnJoin   = CreateButton(panelMain.transform, "BtnJoin",   "UNIRSE",        new Color(0.2f, 0.5f, 0.9f));
            var btnQuit   = CreateButton(panelMain.transform, "BtnQuit",   "SALIR",         new Color(0.25f, 0.25f, 0.3f));

            // ── Panel Crear ───────────────────────────────────────────────
            var panelCreate = CreatePanel(canvas.transform, "PanelCreate");
            panelCreate.SetActive(false);
            CreateTitleText(panelCreate.transform, "Crear Lobby", 48);
            CreateLabel(panelCreate.transform, "Tu Nombre:");
            var inputNameCreate     = CreateInputField(panelCreate.transform, "InputNameCreate", "Ej: GuerreroDragon");
            CreateLabel(panelCreate.transform, "Tu Color:");
            var colorSelCreate      = CreateColorSelector(panelCreate.transform, "ColorSelectorCreate");
            var txtLocalIP          = CreateIPDisplay(panelCreate.transform, "TxtLocalIP");
            var btnConfirmCreate    = CreateButton(panelCreate.transform, "BtnConfirmCreate", "CREAR",   new Color(0.2f, 0.7f, 0.3f));
            var btnBackCreate       = CreateButton(panelCreate.transform, "BtnBackCreate",    "< Volver", new Color(0.3f, 0.3f, 0.35f));

            // ── Panel Unirse ──────────────────────────────────────────────
            var panelJoin = CreatePanel(canvas.transform, "PanelJoin");
            panelJoin.SetActive(false);
            CreateTitleText(panelJoin.transform, "Unirse a Partida", 48);
            CreateLabel(panelJoin.transform, "Tu Nombre:");
            var inputNameJoin    = CreateInputField(panelJoin.transform, "InputNameJoin", "Ej: GuerreroDragon");
            CreateLabel(panelJoin.transform, "IP del Host:");
            var inputIP          = CreateInputField(panelJoin.transform, "InputIP", "192.168.1.100");
            CreateLabel(panelJoin.transform, "Tu Color:");
            var colorSelJoin     = CreateColorSelector(panelJoin.transform, "ColorSelectorJoin");
            var btnConfirmJoin   = CreateButton(panelJoin.transform, "BtnConfirmJoin", "UNIRSE",   new Color(0.2f, 0.5f, 0.9f));
            var btnBackJoin      = CreateButton(panelJoin.transform, "BtnBackJoin",    "< Volver", new Color(0.3f, 0.3f, 0.35f));

            // ── Panel Error ───────────────────────────────────────────────
            var panelError = CreateErrorPanel(canvas.transform);

            // ── Añadir MainMenuUI y cablear TODAS las referencias ─────────
            var menuUI = canvas.AddComponent<MainMenuUI>();
            var so = new SerializedObject(menuUI);

            so.FindProperty("panelMain").objectReferenceValue          = panelMain;
            so.FindProperty("panelCreate").objectReferenceValue        = panelCreate;
            so.FindProperty("panelJoin").objectReferenceValue          = panelJoin;

            so.FindProperty("btnCreate").objectReferenceValue          = btnCreate.GetComponent<Button>();
            so.FindProperty("btnJoin").objectReferenceValue            = btnJoin.GetComponent<Button>();
            so.FindProperty("btnQuit").objectReferenceValue            = btnQuit.GetComponent<Button>();

            so.FindProperty("inputNameCreate").objectReferenceValue    = inputNameCreate.GetComponent<TMP_InputField>();
            so.FindProperty("colorSelectorCreate").objectReferenceValue= colorSelCreate.transform;
            so.FindProperty("btnConfirmCreate").objectReferenceValue   = btnConfirmCreate.GetComponent<Button>();
            so.FindProperty("btnBackCreate").objectReferenceValue      = btnBackCreate.GetComponent<Button>();
            so.FindProperty("txtLocalIP").objectReferenceValue         = txtLocalIP.GetComponent<TMP_Text>();

            so.FindProperty("inputNameJoin").objectReferenceValue      = inputNameJoin.GetComponent<TMP_InputField>();
            so.FindProperty("inputIP").objectReferenceValue            = inputIP.GetComponent<TMP_InputField>();
            so.FindProperty("colorSelectorJoin").objectReferenceValue  = colorSelJoin.transform;
            so.FindProperty("btnConfirmJoin").objectReferenceValue     = btnConfirmJoin.GetComponent<Button>();
            so.FindProperty("btnBackJoin").objectReferenceValue        = btnBackJoin.GetComponent<Button>();

            so.FindProperty("panelError").objectReferenceValue         = panelError;
            so.FindProperty("txtError").objectReferenceValue           =
                panelError.transform.Find("TxtError")?.GetComponent<TMP_Text>();

            so.ApplyModifiedProperties();
            Debug.Log("[SceneSetupTool] MainMenuUI conectado correctamente.");
        }


        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Construir Canvas Lobby

        private static void BuildLobbyCanvas(GameObject canvas)
        {
            var bg = CreateImage(canvas.transform, "Background",
                new Color(0.05f, 0.05f, 0.12f), Vector2.zero, Vector2.one);
            SetAnchors(bg, Vector2.zero, Vector2.one);

            var root = CreateVerticalLayout(canvas.transform, "LobbyRoot");
            SetAnchors(root, Vector2.zero, Vector2.one, new Vector2(40, 40), new Vector2(-40, -80));

            CreateTitleText(root.transform, "LOBBY", 52);
            var txtPlayerCount  = CreateBodyText(root.transform, "TxtPlayerCount", "0 / 8 jugadores", 26);
            var scrollView      = CreateScrollViewAndGetContent(root.transform, "PlayerList");
            var txtWaiting      = CreateBodyText(root.transform, "TxtWaiting", "Esperando mas jugadores...", 24, new Color(0.8f, 0.8f, 0.5f));
            var txtStatusMsg    = CreateBodyText(root.transform, "TxtStatusMsg", "", 22, new Color(0.7f, 0.7f, 0.7f));
            var btnStartGame    = CreateButton(root.transform, "BtnStartGame", "INICIAR PARTIDA", new Color(0.2f, 0.7f, 0.3f));
            var btnLeave        = CreateButton(root.transform, "BtnLeave",     "SALIR DEL LOBBY", new Color(0.5f, 0.2f, 0.2f));

            // ── Añadir LobbyUI y cablear referencias ──────────────────────
            var lobbyUI = canvas.AddComponent<LobbyUI>();
            var so      = new SerializedObject(lobbyUI);

            // Buscar TxtTitle dentro del LobbyRoot (primer hijo TxtTitle)
            var txtTitle = root.transform.Find("TxtTitle");

            so.FindProperty("txtLobbyTitle").objectReferenceValue      = txtTitle?.GetComponent<TMP_Text>();
            so.FindProperty("txtPlayerCount").objectReferenceValue     = txtPlayerCount.GetComponent<TMP_Text>();
            so.FindProperty("playerListContainer").objectReferenceValue= scrollView;   // es el Content del ScrollView
            so.FindProperty("btnStartGame").objectReferenceValue       = btnStartGame.GetComponent<Button>();
            so.FindProperty("btnLeave").objectReferenceValue           = btnLeave.GetComponent<Button>();
            so.FindProperty("txtWaiting").objectReferenceValue         = txtWaiting.GetComponent<TMP_Text>();
            so.FindProperty("txtStatusMsg").objectReferenceValue       = txtStatusMsg.GetComponent<TMP_Text>();

            // Cargar el prefab de entrada de jugador si ya existe
            var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerEntry_Prefab.prefab");
            if (entryPrefab != null)
                so.FindProperty("playerEntryPrefab").objectReferenceValue = entryPrefab;
            else
                Debug.LogWarning("[SceneSetupTool] PlayerEntry_Prefab no encontrado. Ejecuta primero 'Tools > Dominio > 4 - Create Lobby Prefabs'.");

            so.ApplyModifiedProperties();
            Debug.Log("[SceneSetupTool] LobbyUI conectado correctamente.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Helpers de UI

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 24;
            vlg.padding = new RectOffset(60, 60, 120, 60);
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;  // false = el hijo controla su propia altura via LayoutElement
            return go;
        }

        private static GameObject CreateVerticalLayout(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 20;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;  // false = el hijo controla su propia altura
            return go;
        }

        private static GameObject CreateTitleText(Transform parent, string text, int size)
        {
            var go = new GameObject("TxtTitle");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size * 1.4f;
            return go;
        }

        private static GameObject CreateSubtitleText(Transform parent, string text, int size)
        {
            var go = new GameObject("TxtSubtitle");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.7f, 0.7f, 0.8f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size * 1.6f;
            return go;
        }

        private static GameObject CreateBodyText(Transform parent, string goName, string text, int size,
            Color? color = null)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? Color.white;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size * 1.6f;
            return go;
        }

        private static GameObject CreateLabel(Transform parent, string text)
        {
            var go = new GameObject("Label_" + text.Replace(" ", ""));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = new Color(0.7f, 0.8f, 1f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36;
            return go;
        }

        private static GameObject CreateButton(Transform parent, string goName, string label, Color color)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Color.white * 1.2f;
            colors.pressedColor = color * 0.7f;
            btn.colors = colors;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight       = 90;
            le.preferredHeight = 110;
            le.flexibleHeight  = 0;   // evita que el grupo lo estire

            // Texto del botón
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 30;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var rt = txtGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            return go;
        }

        private static GameObject CreateInputField(Transform parent, string goName, string placeholder)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.22f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 90;
            le.minHeight = 70;

            var field = go.AddComponent<TMP_InputField>();

            // Placeholder
            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            var phTxt = phGO.AddComponent<TextMeshProUGUI>();
            phTxt.text = placeholder;
            phTxt.fontSize = 28;
            phTxt.color = new Color(0.5f, 0.5f, 0.55f);
            phTxt.fontStyle = FontStyles.Italic;
            phTxt.margin = new Vector4(20, 0, 20, 0);
            SetStretch(phGO);

            // Text
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 28;
            txt.color = Color.white;
            txt.margin = new Vector4(20, 0, 20, 0);
            SetStretch(txtGO);

            field.textComponent = txt;
            field.placeholder = phTxt;
            field.characterLimit = 20;

            return go;
        }

        private static GameObject CreateColorSelector(Transform parent, string goName)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 12;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 70;

            // Crear un botón de color por cada color disponible
            Color32[] colors = {
                new Color32(255,80,80,255), new Color32(80,160,255,255),
                new Color32(80,255,120,255), new Color32(255,200,60,255),
                new Color32(200,80,255,255), new Color32(255,140,60,255),
                new Color32(60,220,220,255), new Color32(255,100,180,255),
            };

            foreach (var c in colors)
            {
                var btn = new GameObject("ColorBtn");
                btn.transform.SetParent(go.transform, false);
                var img = btn.AddComponent<Image>();
                img.color = c;
                btn.AddComponent<Button>();
                var ble = btn.AddComponent<LayoutElement>();
                ble.preferredWidth  = 60;
                ble.preferredHeight = 60;
            }
            return go;
        }

        private static GameObject CreateIPDisplay(Transform parent, string goName)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "Tu IP: ...";
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.5f, 1f, 0.7f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36;
            return go;
        }

        private static GameObject CreateErrorPanel(Transform parent)
        {
            var go = new GameObject("PanelError");
            go.transform.SetParent(parent, false);
            go.SetActive(false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.05f);
            rt.anchorMax = new Vector2(0.9f, 0.15f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.7f, 0.1f, 0.1f, 0.92f);

            var txtGO = new GameObject("TxtError");
            txtGO.transform.SetParent(go.transform, false);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "Error";
            tmp.fontSize = 26;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            SetStretch(txtGO);

            return go;
        }

        // Versión que retorna el Content (para wiring de LobbyUI.playerListContainer)
        private static Transform CreateScrollViewAndGetContent(Transform parent, string goName)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var sv = go.AddComponent<ScrollRect>();
            var le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = 1;

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(go.transform, false);
            var vmask = viewportGO.AddComponent<Mask>();
            vmask.showMaskGraphic = false;
            viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            SetStretch(viewportGO);

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth  = true;
            vlg.childControlHeight = false;
            contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot     = new Vector2(0.5f, 1);
            contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;

            sv.content    = contentRT;
            sv.viewport   = viewportGO.GetComponent<RectTransform>();
            sv.horizontal = false;

            return contentGO.transform;
        }

        private static GameObject CreateImage(Transform parent, string name,
            Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static void SetAnchors(GameObject go,
            Vector2 min, Vector2 max,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void SetStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
#endif
