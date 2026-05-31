using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;
using System.IO;

// No runtime-namespace usings — avoids cross-assembly compile errors.

namespace KingOfTheHill.Editor
{
    public static class GameplayUISetupTool
    {
        // ── Paths ─────────────────────────────────────────────────────────────────
        private const string PrefabPath  = "Assets/Prefabs/Players/Player.prefab";
        private const string ScenePath   = "Assets/Scenes/Dev/Gameplay_Scene.unity";
        private const string BackupScene = "Assets/Scenes/Dev/SampleScene.unity";
        private const string SpritesDir  = "Assets/Art/UI/HUD";

        private static readonly string[] FontPaths =
        {
            "Assets/TextMesh Pro/Fonts/ThaleahFat SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/ThaleahFat SDF.asset",
        };
        private const string FontTTF = "Assets/TextMesh Pro/Fonts/ThaleahFat.ttf";

        // ── Palette ───────────────────────────────────────────────────────────────
        private static readonly Color Gold      = new Color(1.00f, 0.92f, 0.23f);
        private static readonly Color PanelBG   = new Color(0.06f, 0.06f, 0.10f, 0.85f);
        private static readonly Color HealthRed  = new Color(0.87f, 0.13f, 0.13f);
        private static readonly Color HealthDark = new Color(0.50f, 0.05f, 0.05f);
        private static readonly Color EmptyGray  = new Color(0.22f, 0.22f, 0.27f);
        private static readonly Color EmptyDark  = new Color(0.12f, 0.12f, 0.15f);

        // ─────────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/KingOfTheHill/6 - Redisenar UI de Gameplay", priority = 6)]
        public static void RedesignGameplayUI()
        {
            EnsureFolder(SpritesDir);

            TMP_FontAsset font = LoadOrCreateFont();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "No se encontro ThaleahFat en:\n" + FontTTF, "OK");
                return;
            }

            // Save sprites as PNG assets so they survive prefab serialization
            Sprite heartSprite = SaveSprite("HeartIcon",   HeartPixels(),        11, 10);
            Sprite fillSprite  = SaveSprite("HealthFill",  SegmentPixels(true),  32, 22);
            Sprite emptySprite = SaveSprite("HealthEmpty", SegmentPixels(false), 32, 22);

            bool prefabOk = ApplyToPrefab(font, heartSprite, fillSprite, emptySprite);
            bool sceneOk  = ApplyToScene(font);

            string msg = $"Fuente: {font.name}\n";
            if (prefabOk) msg += "• Player Prefab actualizado.\n";
            if (sceneOk)  msg += "• Escena actualizada.\n";
            if (!prefabOk && !sceneOk)
                msg += "ADVERTENCIA: No se encontro el prefab ni la escena.";

            EditorUtility.DisplayDialog("UI Rediseñada", msg, "OK");
        }

        // ═════════════════════════════════════════════════════════════════════════
        // FONT
        // ═════════════════════════════════════════════════════════════════════════

        private static TMP_FontAsset LoadOrCreateFont()
        {
            string[] preferred = { "Peaberry", "m5x7", "Silkscreen", "kenney", "pico", "ThaleahFat" };
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            
            // Prioridad 1: Buscar fuentes preferidas que no esten corruptas
            foreach (string pref in preferred)
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.IndexOf(pref, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                        if (fa != null && fa.material != null && fa.atlasTexture != null)
                            return fa;
                    }
                }
            }

            // Prioridad 2: Cualquier SDF valida
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fa != null && fa.material != null && fa.atlasTexture != null && path.IndexOf("SDF", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return fa;
            }

            // Fallback a default
            return TMP_Settings.defaultFontAsset;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // SPRITE GENERATION  — saved as PNG assets so Unity serializes them
        // ═════════════════════════════════════════════════════════════════════════

        private static Sprite SaveSprite(string name, Color[] pixels, int w, int h)
        {
            string assetPath = $"{SpritesDir}/{name}.png";
            string fullPath  = Path.GetFullPath(
                Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            // Configure import settings: Sprite, Point filter, 100 ppu (UI default)
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType         = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.filterMode          = FilterMode.Point;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 100f; // Unity default; keeps tiling math simple
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        // ── Heart: 11×10 pixel art ────────────────────────────────────────────────
        private static Color[] HeartPixels()
        {
            Color T = new Color(0, 0, 0, 0);
            Color B = Color.black;
            Color R = new Color(0.88f, 0.13f, 0.13f);
            Color D = new Color(0.55f, 0.04f, 0.04f);
            Color W = new Color(1f, 1f, 1f, 0.85f);

            // Rows stored bottom→top (y=0 at bottom)
            return new[]
            {
                T,T,T,T,T,B,T,T,T,T,T,   // y=0
                T,T,T,T,B,D,B,T,T,T,T,
                T,T,T,B,D,R,D,B,T,T,T,
                T,T,B,D,R,R,R,D,B,T,T,
                T,B,D,R,R,R,R,R,D,B,T,
                B,D,R,R,R,R,R,R,R,D,B,
                B,R,W,R,R,R,R,R,R,R,B,
                B,W,W,R,R,B,R,R,R,R,B,
                T,B,B,B,B,T,B,B,B,B,T,
                T,T,T,T,T,T,T,T,T,T,T    // y=9
            };
        }

        // ── Health segment: 32×22 — red (full) or gray (empty) ───────────────────
        // Right 3 pixels = black separator between segments.
        // Top 4 rows = highlight; bottom 4 rows = shadow.
        private static Color[] SegmentPixels(bool filled)
        {
            const int W = 32, H = 22;
            Color mid   = filled ? HealthRed  : EmptyGray;
            Color dark  = filled ? HealthDark : EmptyDark;
            Color light = filled
                ? new Color(1f, 1f, 1f, 0.50f)
                : new Color(0.38f, 0.38f, 0.43f);

            var pix = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = mid;
                    if (y >= H - 4) c = light;   // top highlight
                    else if (y < 4) c = dark;    // bottom shadow
                    if (x >= W - 3) c = Color.black; // right separator
                    pix[y * W + x] = c;
                }
            return pix;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // APPLY TO PREFAB
        // ═════════════════════════════════════════════════════════════════════════

        private static bool ApplyToPrefab(TMP_FontAsset font,
            Sprite heart, Sprite fill, Sprite empty)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogWarning("[GameplayUISetupTool] Prefab no encontrado: " + PrefabPath);
                return false;
            }

            using var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath);
            var root = scope.prefabContentsRoot;

            // ── Find or create OwnerHUD_Canvas ────────────────────────────────────
            Transform existingCanvas = root.transform.Find("OwnerHUD_Canvas");
            GameObject hudCanvas;

            if (existingCanvas != null)
            {
                hudCanvas = existingCanvas.gameObject;
                // Destroy old HUD children (keep joystick / mobile controls untouched)
                for (int i = hudCanvas.transform.childCount - 1; i >= 0; i--)
                {
                    Transform ch = hudCanvas.transform.GetChild(i);
                    string lname = ch.name.ToLower();
                    if (lname.Contains("joystick") || lname.Contains("mobile") || lname.Contains("button"))
                        continue;
                    Object.DestroyImmediate(ch.gameObject);
                }
            }
            else
            {
                hudCanvas = new GameObject("OwnerHUD_Canvas");
                hudCanvas.transform.SetParent(root.transform, false);

                var cv = hudCanvas.AddComponent<Canvas>();
                cv.renderMode  = RenderMode.ScreenSpaceOverlay;
                cv.sortingOrder = 10;

                var cs = hudCanvas.AddComponent<CanvasScaler>();
                cs.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                cs.matchWidthOrHeight  = 0.5f;

                hudCanvas.AddComponent<GraphicRaycaster>();
            }

            // ── Build new HUD ─────────────────────────────────────────────────────
            BuildHUD(hudCanvas.transform, font, heart, fill, empty,
                out Slider    healthSlider,
                out TextMeshProUGUI scoreText,
                out TextMeshProUGUI respawnText);

            // ── Wire PlayerHUD via SerializedObject (no runtime-namespace import) ─
            foreach (MonoBehaviour comp in root.GetComponents<MonoBehaviour>())
            {
                if (comp == null || comp.GetType().Name != "PlayerHUD") continue;

                var so = new SerializedObject(comp);
                SetRef(so, "ownerHUDRoot", hudCanvas);
                SetRef(so, "healthSlider",  healthSlider);
                SetRef(so, "scoreText",     scoreText);
                SetRef(so, "respawnText",   respawnText);
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[GameplayUISetupTool] PlayerHUD wired successfully.");
                break;
            }

            return true;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // APPLY TO SCENE (update countdown / timer text style)
        // ═════════════════════════════════════════════════════════════════════════

        private static bool ApplyToScene(TMP_FontAsset font)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath && scene.path != BackupScene) return false;

            foreach (TextMeshProUGUI tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = tmp.gameObject.name.ToLower();
                if (!n.Contains("countdown") && !n.Contains("phase") && !n.Contains("timer")) continue;

                tmp.font         = font;
                tmp.color        = Gold;
                EditorUtility.SetDirty(tmp);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            
            SetupScoreboardCanvas(font);
            
            return true;
        }

        private static void SetupScoreboardCanvas(TMP_FontAsset font)
        {
            var gpm = Object.FindAnyObjectByType<KingOfTheHill.Managers.GamePhaseManager>();
            if (gpm == null) return;

            GameObject canvasGO = GameObject.Find("Scoreboard_Canvas");
            if (canvasGO != null) Object.DestroyImmediate(canvasGO);

            canvasGO = new GameObject("Scoreboard_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Por encima de los HUD de jugador
            
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Temporizador (Arriba al centro)
            var timerText = CreateText("MatchTimer", canvasGO.transform, font, 60, Color.white, TextAlignmentOptions.Center);
            RectTransform timerRT = timerText.GetComponent<RectTransform>();
            timerRT.anchorMin = new Vector2(0.5f, 1f);
            timerRT.anchorMax = new Vector2(0.5f, 1f);
            timerRT.pivot = new Vector2(0.5f, 1f);
            timerRT.anchoredPosition = new Vector2(0, -30);
            timerRT.sizeDelta = new Vector2(300, 80);

            // Panel de Resultados (Centro)
            var resultPanel = new GameObject("ResultPanel");
            resultPanel.transform.SetParent(canvasGO.transform, false);
            var resultImg = resultPanel.AddComponent<UnityEngine.UI.Image>();
            resultImg.color = new Color(0, 0, 0, 0.85f);
            RectTransform panelRT = resultPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(600, 560);

            // Título
            var titleText = CreateText("Title", resultPanel.transform, font, 70, Color.white, TextAlignmentOptions.Center);
            RectTransform titleRT = titleText.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0, -30);
            titleRT.sizeDelta = new Vector2(0, 80);

            // Subtítulo
            var subtitleText = CreateText("Subtitle", resultPanel.transform, font, 40, Color.white, TextAlignmentOptions.Center);
            RectTransform subtitleRT = subtitleText.GetComponent<RectTransform>();
            subtitleRT.anchorMin = new Vector2(0f, 1f);
            subtitleRT.anchorMax = new Vector2(1f, 1f);
            subtitleRT.pivot = new Vector2(0.5f, 1f);
            subtitleRT.anchoredPosition = new Vector2(0, -110);
            subtitleRT.sizeDelta = new Vector2(0, 50);

            // Lista de jugadores
            var scoreList = CreateText("ScoreList", resultPanel.transform, font, 36, Color.white, TextAlignmentOptions.TopLeft);
            RectTransform listRT = scoreList.GetComponent<RectTransform>();
            listRT.anchorMin = new Vector2(0f, 0f);
            listRT.anchorMax = new Vector2(1f, 1f);
            listRT.pivot = new Vector2(0.5f, 0.5f);
            listRT.offsetMin = new Vector2(40, 100);
            listRT.offsetMax = new Vector2(-40, -180);

            // Botón Menú
            var menuBtn = CreateButton("MenuButton", resultPanel.transform, font, "Salir al menú");
            RectTransform menuRT = menuBtn.GetComponent<RectTransform>();
            menuRT.anchorMin = new Vector2(0.5f, 0f);
            menuRT.anchorMax = new Vector2(0.5f, 0f);
            menuRT.pivot = new Vector2(1f, 0f);
            menuRT.anchoredPosition = new Vector2(-15, 30);
            menuRT.sizeDelta = new Vector2(250, 60);

            // Botón Reiniciar
            var restartBtn = CreateButton("RestartButton", resultPanel.transform, font, "Nueva partida");
            RectTransform restartRT = restartBtn.GetComponent<RectTransform>();
            restartRT.anchorMin = new Vector2(0.5f, 0f);
            restartRT.anchorMax = new Vector2(0.5f, 0f);
            restartRT.pivot = new Vector2(0f, 0f);
            restartRT.anchoredPosition = new Vector2(15, 30);
            restartRT.sizeDelta = new Vector2(250, 60);

            resultPanel.SetActive(false);

            var so = new SerializedObject(gpm);
            so.FindProperty("matchTimerText").objectReferenceValue = timerText;
            so.FindProperty("resultPanel").objectReferenceValue = resultPanel;
            so.FindProperty("resultTitleText").objectReferenceValue = titleText;
            so.FindProperty("resultSubtitleText").objectReferenceValue = subtitleText;
            so.FindProperty("scoreboardText").objectReferenceValue = scoreList;
            so.FindProperty("menuButton").objectReferenceValue = menuBtn.GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("restartButton").objectReferenceValue = restartBtn.GetComponent<UnityEngine.UI.Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gpm);
            
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            return tmp;
        }

        private static GameObject CreateButton(string name, Transform parent, TMP_FontAsset font, string textContent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var btn = go.AddComponent<UnityEngine.UI.Button>();

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = textContent;
            tmp.fontSize = 28;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            RectTransform txtRT = tmp.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            return go;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // BUILD HUD LAYOUT
        // ═════════════════════════════════════════════════════════════════════════
        //
        // Layout (top-left area, no overlap with top-center countdown timer):
        //
        //  ┌─────────────────────┐   ← ScorePanel  (y offset -20)
        //  │  0 pts              │
        //  └─────────────────────┘
        //  ❤  ████████░░░░░░░░░   ← HealthBarRoot (y offset -98)
        //
        //  Full-screen centered text (hidden by default) for respawn/spectate.
        // ═════════════════════════════════════════════════════════════════════════

        private static void BuildHUD(Transform parent, TMP_FontAsset font,
            Sprite heart, Sprite fill, Sprite empty,
            out Slider           healthSlider,
            out TextMeshProUGUI  scoreText,
            out TextMeshProUGUI  respawnText)
        {
            // ── 1. Score panel — below health bar ─────────────────────────────────────────
            var scorePanel = Rect("ScorePanel", parent,
                new Vector2(0,1), new Vector2(0,1), new Vector2(0,1),
                new Vector2(20, -100), new Vector2(250, 64));
            // No background image as requested, let the text render with outline

            var scoreLbl = Rect("ScoreText", scorePanel,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            scoreLbl.offsetMin = new Vector2(12, 5);
            scoreLbl.offsetMax = new Vector2(-12, -5);

            scoreText = scoreLbl.gameObject.AddComponent<TextMeshProUGUI>();
            scoreText.font        = font;
            scoreText.text        = "0 pts";
            scoreText.fontSize    = 38;
            scoreText.color       = Gold;
            scoreText.fontStyle   = FontStyles.Bold;
            scoreText.alignment   = TextAlignmentOptions.MidlineLeft;

            // ── 2. Health bar — top-left ─────────────────────────────
            // Root (contains heart + bar side by side)
            var hbRoot = Rect("HealthBarRoot", parent,
                new Vector2(0,1), new Vector2(0,1), new Vector2(0,1),
                new Vector2(20, -20), new Vector2(380, 52));

            // Heart icon (left side)
            var heartGO = Rect("HeartIcon", hbRoot,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0), new Vector2(46, 46));
            var heartImg = heartGO.gameObject.AddComponent<Image>();
            heartImg.sprite = heart;
            heartImg.preserveAspect = true;

            // Outer black border
            var border = Rect("BarBorder", hbRoot,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f),
                new Vector2(54, 0), new Vector2(-2, 46));
            border.gameObject.AddComponent<Image>().color = Color.black;

            // Background — empty segments (tiled sprite)
            var barBG = Rect("Background", border,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            barBG.offsetMin = new Vector2(3, 3);
            barBG.offsetMax = new Vector2(-3, -3);
            var bgImg = barBG.gameObject.AddComponent<Image>();
            bgImg.sprite = empty;
            bgImg.type   = Image.Type.Tiled;
            // pixelsPerUnitMultiplier controls segment width:
            // sprite 32px wide, ppu=100 → tile = 32px; *0.4 → ~13px tiles looks like segments
            bgImg.pixelsPerUnitMultiplier = 0.42f;

            // Fill Area (Slider fills this)
            var fillArea = Rect("Fill Area", barBG,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;

            // Fill image — red segments (tiled)
            var fillGO = Rect("Fill", fillArea,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero);
            fillGO.offsetMin = Vector2.zero;
            fillGO.offsetMax = Vector2.zero;
            var fillImg = fillGO.gameObject.AddComponent<Image>();
            fillImg.sprite = fill;
            fillImg.type   = Image.Type.Tiled;
            fillImg.pixelsPerUnitMultiplier = 0.42f;

            // Slider component (PlayerHUD drives this)
            healthSlider = hbRoot.gameObject.AddComponent<Slider>();
            healthSlider.interactable = false;
            healthSlider.transition   = Selectable.Transition.None;
            healthSlider.fillRect     = fillGO;
            healthSlider.direction    = Slider.Direction.LeftToRight;
            healthSlider.minValue     = 0f;
            healthSlider.maxValue     = 1f;
            healthSlider.value        = 1f;

            var respawnRT = Rect("RespawnOverlay", parent,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            respawnRT.offsetMin = Vector2.zero;
            respawnRT.offsetMax = Vector2.zero;

            // Transparent background (user doesn't want it to darken)
            var respawnBG = respawnRT.gameObject.AddComponent<Image>();
            respawnBG.color = new Color(0f, 0f, 0f, 0f);

            // Centered text label
            var respawnLbl = Rect("RespawnText", respawnRT,
                new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            respawnLbl.offsetMin = Vector2.zero;
            respawnLbl.offsetMax = Vector2.zero;

            respawnText = respawnLbl.gameObject.AddComponent<TextMeshProUGUI>();
            respawnText.font        = font;
            respawnText.text        = "";
            respawnText.fontSize    = 52;
            respawnText.color       = Gold;
            respawnText.fontStyle   = FontStyles.Bold;
            respawnText.alignment   = TextAlignmentOptions.Center;
            respawnText.textWrappingMode = TextWrappingModes.Normal;
            respawnRT.gameObject.SetActive(false);

        }

        // ═════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════════

        private static RectTransform Rect(string name, Transform parent,
            Vector2 anMin, Vector2 anMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = anMin;
            rt.anchorMax        = anMax;
            rt.pivot            = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            return rt;
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
                p.objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int sep = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, sep));
            AssetDatabase.CreateFolder(path.Substring(0, sep), path.Substring(sep + 1));
        }
    }
}
