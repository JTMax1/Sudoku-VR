using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SudokuVR.Core;
using SudokuVR.Gameplay;
using SudokuVR.Interaction;
using SudokuVR.Networking;
using SudokuVR.UI;

namespace SudokuVR.Editor
{
    public static class ZenithSceneBuilder
    {
        [MenuItem("Zenith Sudoku/Build Production Architecture")]
        public static void BuildProductionArchitecture()
        {
            Debug.Log("<color=#00E5FF><b>[Zenith XR Builder]</b></color> Starting Zenith Sudoku XR Production Build...");

            // 1. Ensure Art Materials exist
            Material matFrame = GetOrCreateMaterial("Assets/_Project/Art/Materials/M_Frame_Titanium.mat", new Color(0.25f, 0.28f, 0.34f, 1f), 0.85f, 0.75f);
            Material matMonolith = GetOrCreateMaterial("Assets/_Project/Art/Materials/M_Acrylic_Monolith.mat", new Color(0.08f, 0.10f, 0.15f, 0.95f), 0.1f, 0.85f);
            Material matTile = GetOrCreateMaterial("Assets/_Project/Art/Materials/M_Acrylic_Tile.mat", new Color(0.12f, 0.15f, 0.21f, 1f), 0.05f, 0.7f);
            Material matButton = GetOrCreateMaterial("Assets/_Project/Art/Materials/M_Keypad_Button.mat", new Color(0.16f, 0.20f, 0.28f, 1f), 0.1f, 0.65f);

            // 2. Locate or configure Camera & Passthrough
            Camera mainCam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            OVRPassthroughLayer passthrough = UnityEngine.Object.FindAnyObjectByType<OVRPassthroughLayer>();

            // 3. Assemble Core Systems Root
            GameObject systemsRoot = GameObject.Find("[Zenith_Systems]");
            if (systemsRoot == null)
            {
                systemsRoot = new GameObject("[Zenith_Systems]");
                Undo.RegisterCreatedObjectUndo(systemsRoot, "Create Zenith Systems");
            }

            ProgressionManager progression = GetOrAddComponent<ProgressionManager>(systemsRoot);
            CountryPassportTracker passportTracker = GetOrAddComponent<CountryPassportTracker>(systemsRoot);
            MultiplayerLobbyBridge lobbyBridge = GetOrAddComponent<MultiplayerLobbyBridge>(systemsRoot);
            LocalSudokuNetworkSync networkSync = GetOrAddComponent<LocalSudokuNetworkSync>(systemsRoot);
            TimerSystem timer = GetOrAddComponent<TimerSystem>(systemsRoot);
            ScoringEngine scoring = GetOrAddComponent<ScoringEngine>(systemsRoot);
            HintService hints = GetOrAddComponent<HintService>(systemsRoot);
            SudokuGameManager gameManager = GetOrAddComponent<SudokuGameManager>(systemsRoot);
            CellInteractor interactor = GetOrAddComponent<CellInteractor>(systemsRoot);

            // 4. Assemble Spatial Board Monolith
            GameObject monolithRoot = GameObject.Find("SpatialBoard_Monolith");
            if (monolithRoot == null)
            {
                monolithRoot = new GameObject("SpatialBoard_Monolith");
                monolithRoot.transform.position = new Vector3(0f, 1.15f, 0.72f);
                monolithRoot.transform.rotation = Quaternion.Euler(15f, 0f, 0f); // Ergonomic 15-degree tilt
                Undo.RegisterCreatedObjectUndo(monolithRoot, "Create SpatialBoard Monolith");
            }

            SpatialBoardAnchor boardAnchor = GetOrAddComponent<SpatialBoardAnchor>(monolithRoot);
            SudokuBoardView boardView = GetOrAddComponent<SudokuBoardView>(monolithRoot);

            // Backplate Monolith Mesh
            Transform backplate = monolithRoot.transform.Find("Monolith_Backplate");
            if (backplate == null)
            {
                GameObject bpObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bpObj.name = "Monolith_Backplate";
                bpObj.transform.SetParent(monolithRoot.transform, false);
                bpObj.transform.localPosition = new Vector3(0f, -0.012f, 0f);
                bpObj.transform.localScale = new Vector3(0.58f, 0.018f, 0.58f);
                var rend = bpObj.GetComponent<MeshRenderer>();
                if (rend != null) rend.sharedMaterial = matMonolith;
                UnityEngine.Object.DestroyImmediate(bpObj.GetComponent<Collider>());
                backplate = bpObj.transform;
            }

            // Build Bounding Frame and Handles
            boardAnchor.BuildBoundingFrame(0.56f);

            // Wire Serialized Properties on SudokuBoardView via SerializedObject
            SerializedObject soBoardView = new SerializedObject(boardView);
            soBoardView.FindProperty("boardBackplate").objectReferenceValue = backplate;
            soBoardView.ApplyModifiedPropertiesWithoutUndo();

            // Build Grid Monolith 81 Tiles
            boardView.BuildGridHierarchy();

            // 5. Assemble Floating Curved Keypad
            Transform keypadTransform = monolithRoot.transform.Find("FloatingKeypad");
            GameObject keypadObj = (keypadTransform != null) ? keypadTransform.gameObject : new GameObject("FloatingKeypad");
            keypadObj.transform.SetParent(monolithRoot.transform, false);
            keypadObj.transform.localPosition = new Vector3(0f, -0.34f, -0.04f);
            keypadObj.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            FloatingKeypadView keypadView = GetOrAddComponent<FloatingKeypadView>(keypadObj);
            keypadView.BuildProceduralKeypad();

            // 6. Assemble Spatial HUD Arc above the Monolith
            Transform hudTransform = monolithRoot.transform.Find("SpatialHUD_Arc");
            GameObject hudObj = (hudTransform != null) ? hudTransform.gameObject : new GameObject("SpatialHUD_Arc");
            hudObj.transform.SetParent(monolithRoot.transform, false);
            hudObj.transform.localPosition = new Vector3(0f, 0.34f, 0.05f);
            hudObj.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);

            SpatialHUDView hudView = GetOrAddComponent<SpatialHUDView>(hudObj);
            BuildSpatialHUDVisuals(hudObj, hudView);

            // 7. Assemble Spatial Navigation Hub (Home Environment Console)
            GameObject hubRoot = GameObject.Find("SpatialHub_Navigation");
            if (hubRoot == null)
            {
                hubRoot = new GameObject("SpatialHub_Navigation");
                hubRoot.transform.position = new Vector3(0f, 1.25f, 1.15f);
                hubRoot.transform.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(hubRoot, "Create SpatialHub Navigation");
            }

            MainMenuController menuController = GetOrAddComponent<MainMenuController>(hubRoot);
            BattlePassportController battleController = GetOrAddComponent<BattlePassportController>(hubRoot);
            PassportRoomView passportView = GetOrAddComponent<PassportRoomView>(hubRoot);

            BuildNavigationConsoleVisuals(hubRoot, menuController, battleController, passportView, monolithRoot);

            // 8. Wire SudokuGameManager Serialized References
            SerializedObject soGM = new SerializedObject(gameManager);
            soGM.FindProperty("boardView").objectReferenceValue = boardView;
            soGM.FindProperty("floatingKeypad").objectReferenceValue = keypadView;
            soGM.FindProperty("boardAnchor").objectReferenceValue = boardAnchor;
            soGM.FindProperty("timerSystem").objectReferenceValue = timer;
            soGM.FindProperty("scoringEngine").objectReferenceValue = scoring;
            soGM.FindProperty("hintService").objectReferenceValue = hints;
            soGM.FindProperty("networkSync").objectReferenceValue = networkSync;
            soGM.ApplyModifiedPropertiesWithoutUndo();

            // 9. Wire SpatialBoardAnchor References
            SerializedObject soAnchor = new SerializedObject(boardAnchor);
            soAnchor.FindProperty("passthroughLayer").objectReferenceValue = passthrough;
            soAnchor.ApplyModifiedPropertiesWithoutUndo();

            // Save Prefabs to Assets/_Project/Prefabs/
            SavePrefabSafe(monolithRoot, "Assets/_Project/Prefabs/Board/SpatialBoard_Monolith.prefab");
            SavePrefabSafe(hubRoot, "Assets/_Project/Prefabs/UI/SpatialHub_Navigation.prefab");

            // Mark Scene Dirty and Save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log("<color=#34C759><b>[Zenith XR Builder] SUCCESS!</b></color> Zenith Sudoku XR complete production architecture built and verified!");
        }

        private static void BuildSpatialHUDVisuals(GameObject hudRoot, SpatialHUDView hudView)
        {
            Canvas canvas = hudRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = hudRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
            }

            RectTransform rt = hudRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 180f);
            rt.localScale = new Vector3(0.0008f, 0.0008f, 0.0008f);

            // Metrics Bar
            GameObject barObj = new GameObject("MetricsBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            barObj.transform.SetParent(hudRoot.transform, false);
            RectTransform barRt = barObj.GetComponent<RectTransform>();
            barRt.anchoredPosition = new Vector2(0f, 25f);
            barRt.sizeDelta = new Vector2(650f, 60f);

            HorizontalLayoutGroup hlg = barObj.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 30f;
            hlg.childForceExpandWidth = false;

            TMP_Text diffText = CreateTMPLabel(barObj, "DiffBadge", "BEGINNER", 24f, new Color(0f, 0.85f, 1f));
            TMP_Text timerText = CreateTMPLabel(barObj, "Timer", "00:00", 28f, Color.white);
            TMP_Text scoreText = CreateTMPLabel(barObj, "Score", "SCORE: 0", 24f, new Color(1f, 0.85f, 0.2f));
            TMP_Text mistakesText = CreateTMPLabel(barObj, "Mistakes", "MISTAKES: 0/3", 24f, Color.white);

            // Hint Toast Text
            GameObject hintObj = new GameObject("HintToast", typeof(RectTransform));
            hintObj.transform.SetParent(hudRoot.transform, false);
            RectTransform hintRt = hintObj.GetComponent<RectTransform>();
            hintRt.anchoredPosition = new Vector2(0f, -40f);
            hintRt.sizeDelta = new Vector2(650f, 40f);
            TMP_Text hintText = CreateTMPLabel(hintObj, "HintLabel", "", 20f, new Color(1f, 0.85f, 0.3f));

            // Wire into SpatialHUDView
            SerializedObject so = new SerializedObject(hudView);
            so.FindProperty("difficultyText").objectReferenceValue = diffText;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("mistakesText").objectReferenceValue = mistakesText;
            so.FindProperty("hintNotificationText").objectReferenceValue = hintText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildNavigationConsoleVisuals(GameObject hubRoot, MainMenuController menu, BattlePassportController battle, PassportRoomView passport, GameObject inGameMonolith)
        {
            Canvas canvas = hubRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = hubRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
            }

            RectTransform rt = hubRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 650f);
            rt.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            // 1. Home Panel
            GameObject homePanel = new GameObject("Panel_Home", typeof(RectTransform));
            homePanel.transform.SetParent(hubRoot.transform, false);
            RectTransform homeRt = homePanel.GetComponent<RectTransform>();
            homeRt.sizeDelta = new Vector2(950f, 600f);

            // Title
            GameObject titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(homePanel.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0f, 220f);
            titleRt.sizeDelta = new Vector2(800f, 70f);
            CreateTMPLabel(titleObj, "TitleText", "<color=#00E5FF><b>ZENITH SUDOKU XR</b></color>", 44f, Color.white);

            // Top Stats Ribbon
            GameObject statsObj = new GameObject("StatsRibbon", typeof(RectTransform));
            statsObj.transform.SetParent(homePanel.transform, false);
            RectTransform statsRt = statsObj.GetComponent<RectTransform>();
            statsRt.anchoredPosition = new Vector2(0f, 150f);
            statsRt.sizeDelta = new Vector2(750f, 40f);
            TMP_Text coinsText = CreateTMPLabel(statsObj, "Coins", "150 COINS", 22f, new Color(1f, 0.85f, 0.2f));
            RectTransform coinsRt = coinsText.GetComponent<RectTransform>();
            coinsRt.anchoredPosition = new Vector2(-250f, 0f);

            TMP_Text streakText = CreateTMPLabel(statsObj, "Streak", "0 STREAK", 22f, new Color(0.2f, 0.85f, 1f));
            RectTransform streakRt = streakText.GetComponent<RectTransform>();
            streakRt.anchoredPosition = new Vector2(0f, 0f);

            TMP_Text winsText = CreateTMPLabel(statsObj, "Wins", "0 WINS", 22f, new Color(0.3f, 0.9f, 0.5f));
            RectTransform winsRt = winsText.GetComponent<RectTransform>();
            winsRt.anchoredPosition = new Vector2(250f, 0f);

            // Hero Play Button
            Button playHeroBtn = CreateSpatialUIButton(homePanel, "Btn_PlayHero", "PLAY PUZZLE", new Vector2(0f, 30f), new Vector2(280f, 75f), new Color(0f, 0.65f, 0.95f));
            Button battleHeroBtn = CreateSpatialUIButton(homePanel, "Btn_BattleHero", "1v1 SPEED DUEL", new Vector2(-220f, -80f), new Vector2(220f, 60f), new Color(0.85f, 0.35f, 0.15f));
            Button passportHeroBtn = CreateSpatialUIButton(homePanel, "Btn_PassportHero", "PASSPORT STAMPS", new Vector2(220f, -80f), new Vector2(220f, 60f), new Color(0.2f, 0.7f, 0.45f));

            // Bottom Dock
            GameObject dockObj = new GameObject("BottomDock", typeof(RectTransform));
            dockObj.transform.SetParent(homePanel.transform, false);
            RectTransform dockRt = dockObj.GetComponent<RectTransform>();
            dockRt.anchoredPosition = new Vector2(0f, -220f);
            dockRt.sizeDelta = new Vector2(700f, 55f);

            Button navHomeBtn = CreateSpatialUIButton(dockObj, "Nav_Home", "HOME", new Vector2(-220f, 0f), new Vector2(130f, 45f), new Color(0.2f, 0.25f, 0.35f));
            Button navBattleBtn = CreateSpatialUIButton(dockObj, "Nav_Battle", "BATTLE", new Vector2(-70f, 0f), new Vector2(130f, 45f), new Color(0.2f, 0.25f, 0.35f));
            Button navPassportBtn = CreateSpatialUIButton(dockObj, "Nav_Passport", "PASSPORT", new Vector2(80f, 0f), new Vector2(130f, 45f), new Color(0.2f, 0.25f, 0.35f));
            Button navMRBtn = CreateSpatialUIButton(dockObj, "Nav_Passthrough", "MR TOGGLE", new Vector2(230f, 0f), new Vector2(140f, 45f), new Color(0.15f, 0.55f, 0.4f));

            // 2. Difficulty Modal Panel
            GameObject diffPanel = new GameObject("Panel_DifficultySelect", typeof(RectTransform));
            diffPanel.transform.SetParent(hubRoot.transform, false);
            RectTransform diffRt = diffPanel.GetComponent<RectTransform>();
            diffRt.sizeDelta = new Vector2(950f, 600f);

            CreateTMPLabel(diffPanel, "DiffTitle", "<color=#00E5FF><b>SELECT DIFFICULTY TIER</b></color>", 36f, Color.white, new Vector2(0f, 240f));

            Button[] diffBtns = new Button[6];
            TMP_Text[] diffCriteria = new TMP_Text[6];
            GameObject[] padlocks = new GameObject[6];

            string[] tierNames = { "BEGINNER", "EASY", "MEDIUM", "HARD", "EXPERT", "EXTREME" };
            Color[] tierColors = {
                new Color(0.2f, 0.75f, 0.45f),
                new Color(0.1f, 0.65f, 0.95f),
                new Color(0.95f, 0.75f, 0.1f),
                new Color(0.95f, 0.45f, 0.1f),
                new Color(0.85f, 0.2f, 0.35f),
                new Color(0.6f, 0.15f, 0.85f)
            };

            for (int i = 0; i < 6; i++)
            {
                float y = 160f - (i * 65f);
                Button btn = CreateSpatialUIButton(diffPanel, $"Btn_Tier_{tierNames[i]}", tierNames[i], new Vector2(-150f, y), new Vector2(240f, 50f), tierColors[i]);
                diffBtns[i] = btn;

                TMP_Text info = CreateTMPLabel(diffPanel, $"Info_{tierNames[i]}", "Complete previous tiers to unlock", 16f, Color.white, new Vector2(160f, y));
                diffCriteria[i] = info;

                GameObject pad = new GameObject($"Padlock_{tierNames[i]}", typeof(RectTransform));
                pad.transform.SetParent(diffPanel.transform, false);
                CreateTMPLabel(pad, "PadText", "[LOCKED]", 16f, Color.red, new Vector2(-150f, y));
                padlocks[i] = pad;
                pad.SetActive(false);
            }

            Button closeDiffBtn = CreateSpatialUIButton(diffPanel, "Btn_CloseDiff", "BACK TO HUB", new Vector2(0f, -240f), new Vector2(220f, 48f), new Color(0.3f, 0.35f, 0.45f));
            diffPanel.SetActive(false);

            // 3. Battle Lobby Panel
            GameObject battlePanel = new GameObject("Panel_BattleLobby", typeof(RectTransform));
            battlePanel.transform.SetParent(hubRoot.transform, false);
            RectTransform battleRt = battlePanel.GetComponent<RectTransform>();
            battleRt.sizeDelta = new Vector2(950f, 600f);

            CreateTMPLabel(battlePanel, "BattleTitle", "<color=#FF6B00><b>1v1 SPEED DUEL LOBBY</b></color>", 38f, Color.white, new Vector2(0f, 220f));
            TMP_Text seasonTxt = CreateTMPLabel(battlePanel, "Season", "Season 1: Horizon Zenith", 22f, new Color(1f, 0.85f, 0.3f), new Vector2(0f, 160f));
            TMP_Text duelStats = CreateTMPLabel(battlePanel, "DuelStats", "RATING: 1250 | WINS: 0", 20f, Color.white, new Vector2(0f, 110f));
            TMP_Text duelStatus = CreateTMPLabel(battlePanel, "DuelStatus", "Ready to duel rivals worldwide", 22f, new Color(0.2f, 0.85f, 1f), new Vector2(0f, 20f));

            Button findMatchBtn = CreateSpatialUIButton(battlePanel, "Btn_FindMatch", "ENTER DUEL (25 COINS)", new Vector2(0f, -60f), new Vector2(300f, 65f), new Color(0.9f, 0.4f, 0.1f));
            Button cancelMatchBtn = CreateSpatialUIButton(battlePanel, "Btn_CancelMatch", "CANCEL MATCHMAKING", new Vector2(0f, -60f), new Vector2(300f, 65f), new Color(0.5f, 0.2f, 0.2f));
            Button backBattleBtn = CreateSpatialUIButton(battlePanel, "Btn_BackBattle", "BACK TO HUB", new Vector2(0f, -220f), new Vector2(200f, 48f), new Color(0.3f, 0.35f, 0.45f));

            battlePanel.SetActive(false);

            // 4. Passport Room Gallery Panel
            GameObject passportPanel = new GameObject("Panel_PassportGallery", typeof(RectTransform));
            passportPanel.transform.SetParent(hubRoot.transform, false);
            RectTransform passRt = passportPanel.GetComponent<RectTransform>();
            passRt.sizeDelta = new Vector2(950f, 600f);

            CreateTMPLabel(passportPanel, "PassTitle", "<color=#34C759><b>WORLD PASSPORT STAMPS</b></color>", 36f, Color.white, new Vector2(0f, 240f));
            TMP_Text stampCountTxt = CreateTMPLabel(passportPanel, "StampCount", "PASSPORT STAMPS: 2/30 UNLOCKED", 20f, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 190f));

            // Stamp Grid Container
            GameObject gridObj = new GameObject("StampGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObj.transform.SetParent(passportPanel.transform, false);
            RectTransform gridRt = gridObj.GetComponent<RectTransform>();
            gridRt.anchoredPosition = new Vector2(0f, 0f);
            gridRt.sizeDelta = new Vector2(850f, 320f);

            GridLayoutGroup glg = gridObj.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(120f, 65f);
            glg.spacing = new Vector2(15f, 15f);
            glg.childAlignment = TextAnchor.MiddleCenter;

            Button backPassBtn = CreateSpatialUIButton(passportPanel, "Btn_BackPassport", "BACK TO HUB", new Vector2(0f, -240f), new Vector2(200f, 48f), new Color(0.3f, 0.35f, 0.45f));
            passportPanel.SetActive(false);

            // Wire MainMenuController References
            SerializedObject soMenu = new SerializedObject(menu);
            soMenu.FindProperty("hubHomePanel").objectReferenceValue = homePanel;
            soMenu.FindProperty("difficultyModalPanel").objectReferenceValue = diffPanel;
            soMenu.FindProperty("battleLobbyPanel").objectReferenceValue = battlePanel;
            soMenu.FindProperty("passportGalleryPanel").objectReferenceValue = passportPanel;
            soMenu.FindProperty("inGameRoot").objectReferenceValue = inGameMonolith;

            soMenu.FindProperty("coinsText").objectReferenceValue = coinsText;
            soMenu.FindProperty("streakText").objectReferenceValue = streakText;
            soMenu.FindProperty("gamesWonText").objectReferenceValue = winsText;

            soMenu.FindProperty("playHeroButton").objectReferenceValue = playHeroBtn;
            soMenu.FindProperty("battleHeroButton").objectReferenceValue = battleHeroBtn;
            soMenu.FindProperty("navHomeButton").objectReferenceValue = navHomeBtn;
            soMenu.FindProperty("navBattleButton").objectReferenceValue = navBattleBtn;
            soMenu.FindProperty("navPassportButton").objectReferenceValue = navPassportBtn;
            soMenu.FindProperty("navPassthroughToggle").objectReferenceValue = navMRBtn;
            soMenu.FindProperty("closeDifficultyModalButton").objectReferenceValue = closeDiffBtn;

            for (int i = 0; i < 6; i++)
            {
                soMenu.FindProperty("difficultyButtons").GetArrayElementAtIndex(i).objectReferenceValue = diffBtns[i];
                soMenu.FindProperty("difficultyCriteriaTexts").GetArrayElementAtIndex(i).objectReferenceValue = diffCriteria[i];
                soMenu.FindProperty("padlockIcons").GetArrayElementAtIndex(i).objectReferenceValue = padlocks[i];
            }
            soMenu.ApplyModifiedPropertiesWithoutUndo();

            // Wire BattlePassportController References
            SerializedObject soBattle = new SerializedObject(battle);
            soBattle.FindProperty("seasonTitleText").objectReferenceValue = seasonTxt;
            soBattle.FindProperty("winRateStatsText").objectReferenceValue = duelStats;
            soBattle.FindProperty("matchStatusText").objectReferenceValue = duelStatus;
            soBattle.FindProperty("findMatchButton").objectReferenceValue = findMatchBtn;
            soBattle.FindProperty("cancelButton").objectReferenceValue = cancelMatchBtn;
            soBattle.FindProperty("backToHomeButton").objectReferenceValue = backBattleBtn;
            soBattle.ApplyModifiedPropertiesWithoutUndo();

            // Wire PassportRoomView References
            SerializedObject soPassport = new SerializedObject(passport);
            soPassport.FindProperty("stampCountText").objectReferenceValue = stampCountTxt;
            soPassport.FindProperty("backButton").objectReferenceValue = backPassBtn;
            soPassport.FindProperty("gridContainer").objectReferenceValue = gridObj.transform;
            soPassport.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text CreateTMPLabel(GameObject parent, string name, string text, float size, Color color, Vector2 pos = default)
        {
            GameObject labelObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(parent.transform, false);
            RectTransform rt = labelObj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;

            TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.text = text;
            return tmp;
        }

        private static Button CreateSpatialUIButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent.transform, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.GetComponent<Button>();

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.sizeDelta = size;

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.text = label;

            return btn;
        }

        private static Material GetOrCreateMaterial(string path, Color baseColor, float metallic, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                mat.SetColor("_BaseColor", baseColor);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void SavePrefabSafe(GameObject go, string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Zenith XR Builder] Could not save prefab at {path}: {ex.Message}");
            }
        }
    }
}
