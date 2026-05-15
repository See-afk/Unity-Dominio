using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.InputSystem;
using System.IO;
using KingOfTheHill.Players;
using KingOfTheHill.Managers;

namespace KingOfTheHill.Editor
{
    /// <summary>
    /// Herramienta de editor que crea el prefab del jugador y
    /// configura la escena de prueba con un solo clic.
    /// </summary>
    public static class PlayerSetupTool
    {
        private const string PrefabPath   = "Assets/Prefabs/Players/Player.prefab";
        private const string ScenePath    = "Assets/Scenes/Dev/SampleScene.unity";

        // ─── Menú principal ───────────────────────────────────────────────────────

        [MenuItem("KingOfTheHill/1 - Crear Prefab Jugador", priority = 1)]
        public static void CreatePlayerPrefab()
        {
            // ── 1. Crear GameObject raíz ──────────────────────────────────────────
            var root = new GameObject("Player");

            // ── 2. Componentes de red ─────────────────────────────────────────────
            root.AddComponent<NetworkObject>();

            // ── 3. Físicas ────────────────────────────────────────────────────────
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // ── 4. Input ──────────────────────────────────────────────────────────
            var pi = root.AddComponent<PlayerInput>();
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");
            if (inputAsset != null)
            {
                pi.actions         = inputAsset;
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = PlayerNotifications.SendMessages;
            }
            else
            {
                Debug.LogWarning("[PlayerSetupTool] No se encontró InputSystem_Actions.inputactions");
            }

            // ── 5. Scripts del jugador ────────────────────────────────────────────
            root.AddComponent<PlayerStats>();
            root.AddComponent<PlayerNetworkSync>();
            root.AddComponent<PlayerHUD>();
            root.AddComponent<PlayerMovement>();
            root.AddComponent<PlayerCombat>();
            root.AddComponent<PlayerController>();

            // ── 6. Animator (placeholder) ─────────────────────────────────────────
            root.AddComponent<Animator>();

            // ── 7. Cuerpo visual ──────────────────────────────────────────────────
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            // Quitar colider del cuerpo (el CC ya maneja colisiones)
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            // ── 8. CameraRoot ─────────────────────────────────────────────────────
            var camRoot = new GameObject("CameraRoot");
            camRoot.transform.SetParent(root.transform, false);
            camRoot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var camGO = new GameObject("PlayerCamera");
            camGO.transform.SetParent(camRoot.transform, false);
            camGO.transform.localPosition = new Vector3(0f, 0f, 0f);
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();

            // Asignar cameraRoot al PlayerMovement
            var movement = root.GetComponent<PlayerMovement>();
            var so = new SerializedObject(movement);
            so.FindProperty("cameraRoot").objectReferenceValue = camRoot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Asignar camera al PlayerController
            var controller = root.GetComponent<PlayerController>();
            var soc = new SerializedObject(controller);
            soc.FindProperty("playerCamera").objectReferenceValue = camGO;
            // Asignar bodyRenderer
            var smr = body.GetComponent<MeshRenderer>();
            soc.FindProperty("bodyRenderer").objectReferenceValue = smr;
            soc.ApplyModifiedPropertiesWithoutUndo();

            // ── 9. Guardar como Prefab ────────────────────────────────────────────
            Directory.CreateDirectory(Path.GetDirectoryName(Application.dataPath + "/../" + PrefabPath));
            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out success);
            Object.DestroyImmediate(root);

            if (success)
            {
                Debug.Log($"[PlayerSetupTool] Prefab creado en: {PrefabPath}");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError("[PlayerSetupTool] Error al guardar el prefab.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────

        [MenuItem("KingOfTheHill/2 - Configurar Escena de Prueba", priority = 2)]
        public static void SetupTestScene()
        {
            // Abrir escena
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // ── NetworkManager ─────────────────────────────────────────────────────
            var nmGO = new GameObject("NetworkManager");
            var nm   = nmGO.AddComponent<NetworkManager>();
            var transport = nmGO.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;

            // ── PlayerSpawner ─────────────────────────────────────────────────────
            var spawnerGO = new GameObject("PlayerSpawner");
            spawnerGO.AddComponent<NetworkObject>();
            var spawner   = spawnerGO.AddComponent<PlayerSpawner>();

            // Cargar prefab y asignarlo al spawner
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab != null)
            {
                var netObj = playerPrefab.GetComponent<NetworkObject>();

                // Registrar en NetworkManager
                var prefabList = nm.NetworkConfig.Prefabs;
                var entry = new NetworkPrefab { Prefab = playerPrefab };
                prefabList.Add(entry);

                // Asignar al spawner
                var sSpawner = new SerializedObject(spawner);
                sSpawner.FindProperty("playerPrefab").objectReferenceValue = netObj;
                sSpawner.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[PlayerSetupTool] Ejecuta primero '1 - Crear Prefab Jugador'");
            }

            // ── Spawn Points ───────────────────────────────────────────────────────
            var spawnParent = new GameObject("SpawnPoints");
            var points = new Transform[4];
            Vector3[] positions = {
                new Vector3( 3f, 0.1f,  3f),
                new Vector3(-3f, 0.1f,  3f),
                new Vector3( 3f, 0.1f, -3f),
                new Vector3(-3f, 0.1f, -3f),
            };
            for (int i = 0; i < 4; i++)
            {
                var sp = new GameObject($"SpawnPoint_{i + 1}");
                sp.transform.SetParent(spawnParent.transform, false);
                sp.transform.localPosition = positions[i];
                // Icono visual
                var iconContent = EditorGUIUtility.IconContent("sv_icon_dot3_pix16_gizmo");
                EditorGUIUtility.SetIconForObject(sp, iconContent.image as Texture2D);
                points[i] = sp.transform;
            }

            // Asignar spawn points al spawner
            var sSpawner2 = new SerializedObject(spawner);
            var spArr = sSpawner2.FindProperty("spawnPoints");
            spArr.arraySize = 4;
            for (int i = 0; i < 4; i++)
                spArr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            sSpawner2.ApplyModifiedPropertiesWithoutUndo();

            // ── Suelo ──────────────────────────────────────────────────────────────
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(3f, 1f, 3f);
                ground.transform.position   = Vector3.zero;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.3f, 0.55f, 0.3f);
                ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            // ── Luz direccional ────────────────────────────────────────────────────
            if (GameObject.Find("Directional Light") == null)
            {
                var lightGO = new GameObject("Directional Light");
                var light   = lightGO.AddComponent<Light>();
                light.type      = LightType.Directional;
                light.intensity = 1.2f;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // ── Guardar escena ─────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PlayerSetupTool] Escena configurada. Presiona Play y luego 'Start Host'.");
        }

        // ─────────────────────────────────────────────────────────────────────────

        [MenuItem("KingOfTheHill/3 - Agregar StartHost al Play (Runtime Helper)", priority = 3)]
        public static void CreateNetworkStarter()
        {
            var go = new GameObject("NetworkStarter");
            go.AddComponent<NetworkStarterHelper>();
            EditorUtility.SetDirty(go);
            Debug.Log("[PlayerSetupTool] NetworkStarter creado. Verifica que esté en la escena.");
        }

        [MenuItem("KingOfTheHill/4 - Crear UI Táctil (Android)", priority = 4)]
        public static void CreateMobileUI()
        {
            if (GameObject.Find("MobileUI_Canvas") != null)
            {
                Debug.LogWarning("La UI táctil ya existe en la escena.");
                return;
            }

            var canvasGO = new GameObject("MobileUI_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Bridge
            var bridge = canvasGO.AddComponent<UI.MobileInputBridge>();

            // Funciones helper
            UnityEngine.UI.Button CreateButton(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, string text)
            {
                var btnGO = new GameObject(name);
                btnGO.transform.SetParent(canvasGO.transform, false);
                var rect = btnGO.AddComponent<RectTransform>();
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
                rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(100, 100);
                var img = btnGO.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(1, 1, 1, 0.3f);
                var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
                
                var txtGO = new GameObject("Text");
                txtGO.transform.SetParent(btnGO.transform, false);
                var txtRect = txtGO.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;
                var tmp = txtGO.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.text = text; tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = Color.black;
                
                return btn;
            }

            // Botones
            var jumpBtn = CreateButton("JumpBtn", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 150), "SALTO");
            var attackBtn = CreateButton("AttackBtn", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-280, 150), "ATAQUE");
            
            // Joysticks
            UI.VirtualJoystick CreateJoystick(string name, Vector2 anchor, Vector2 pos)
            {
                var joyGO = new GameObject(name);
                joyGO.transform.SetParent(canvasGO.transform, false);
                var rect = joyGO.AddComponent<RectTransform>();
                rect.anchorMin = anchor; rect.anchorMax = anchor;
                rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(250, 250);
                var img = joyGO.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                
                var handleGO = new GameObject("Handle");
                handleGO.transform.SetParent(joyGO.transform, false);
                var handleRect = handleGO.AddComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(100, 100);
                var handleImg = handleGO.AddComponent<UnityEngine.UI.Image>();
                handleImg.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                
                var vj = joyGO.AddComponent<UI.VirtualJoystick>();
                var so = new SerializedObject(vj);
                so.FindProperty("background").objectReferenceValue = rect;
                so.FindProperty("handle").objectReferenceValue = handleRect;
                so.ApplyModifiedPropertiesWithoutUndo();
                
                return vj;
            }

            var moveJoy = CreateJoystick("MoveJoystick", new Vector2(0, 0), new Vector2(200, 200));
            var lookJoy = CreateJoystick("LookJoystick", new Vector2(1, 1), new Vector2(-200, -200));

            // Setup Bridge
            var bridgeSO = new SerializedObject(bridge);
            bridgeSO.FindProperty("moveJoystick").objectReferenceValue = moveJoy;
            bridgeSO.FindProperty("lookJoystick").objectReferenceValue = lookJoy;
            bridgeSO.FindProperty("jumpButton").objectReferenceValue = jumpBtn;
            bridgeSO.FindProperty("attackButton").objectReferenceValue = attackBtn;
            bridgeSO.ApplyModifiedPropertiesWithoutUndo();

            // Event System
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            Debug.Log("[PlayerSetupTool] UI Táctil creada en la escena.");
        }

        [MenuItem("KingOfTheHill/5 - Agregar Fase de Inicio (Cinemática)", priority = 5)]
        public static void CreateGamePhaseManager()
        {
            if (Object.FindAnyObjectByType<GamePhaseManager>() != null)
            {
                Debug.LogWarning("El GamePhaseManager ya existe en la escena.");
                return;
            }

            // 1. Crear Canvas y Texto de la Cuenta Regresiva
            var canvasGO = new GameObject("Countdown_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.sortingOrder = 100; // Por encima de todo

            var textGO = new GameObject("Countdown_Text");
            textGO.transform.SetParent(canvasGO.transform, false);
            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 150;
            tmp.color = Color.yellow;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            // Borde y sombra
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;

            // 2. Crear Cámara Cinemática
            var cinematicCamGO = new GameObject("CinematicCamera");
            var cam = cinematicCamGO.AddComponent<Camera>();
            cam.depth = 0;
            cinematicCamGO.AddComponent<AudioListener>();
            var rotator = cinematicCamGO.AddComponent<Gameplay.CinematicCameraRotator>();
            
            // Buscar un objetivo (el suelo o el centro del mapa)
            var target = GameObject.Find("Ground");
            if (target != null)
            {
                var sRotator = new SerializedObject(rotator);
                sRotator.FindProperty("target").objectReferenceValue = target.transform;
                sRotator.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3. Crear GamePhaseManager
            var pmGO = new GameObject("GamePhaseManager");
            pmGO.AddComponent<NetworkObject>();
            var pm = pmGO.AddComponent<GamePhaseManager>();

            // Asignar referencias
            var sPM = new SerializedObject(pm);
            sPM.FindProperty("countdownText").objectReferenceValue = tmp;
            sPM.FindProperty("cinematicCamera").objectReferenceValue = cinematicCamGO;
            
            var mobileUI = GameObject.Find("MobileUI_Canvas");
            if (mobileUI != null)
                sPM.FindProperty("mobileUIRoot").objectReferenceValue = mobileUI;

            sPM.ApplyModifiedPropertiesWithoutUndo();

            // Registrar en el NetworkManager (solo necesario si fuera un prefab que se instancia en runtime)
            // Como este objeto ya está en la escena, Netcode lo sincronizará automáticamente al iniciar.
            // Opcionalmente podemos quitar el bloque nm.AddNetworkPrefab.
            Debug.Log("[PlayerSetupTool] Fase de Inicio (Cuenta regresiva y Cinemática) añadida a la escena.");
        }
    }
}
