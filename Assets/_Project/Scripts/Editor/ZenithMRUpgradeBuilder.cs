using UnityEngine;
using UnityEditor;
using TMPro;
using SudokuVR.Gameplay;
using SudokuVR.Interaction;
using SudokuVR.UI;

namespace SudokuVR.Editor
{
    public static class ZenithMRUpgradeBuilder
    {
        public static void ExecuteUpgrade()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            Debug.Log("<color=#00E5FF><b>[Zenith MR Upgrade] Starting full 3D visual and asset overhaul...</b></color>");

            // 1. Load All Essential Prefabs
            GameObject foxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fox/Prefabs/Fox.prefab");
            GameObject dogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab");
            GameObject penguinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Animals_FREE/Prefabs/Pinguin_001.prefab");
            GameObject deerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab");
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized 3D Animated Chests – FREE Demo/URP/Chsets/Epic Chest/EpicChest_PF_URP.prefab");
            GameObject islandBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SkyIsland_Pack_Free_01/Prefabs/SkyIslandPackFree01_PirateIslandBase_A01.prefab");

            // 2. Locate Roots
            GameObject boardGo = null;
            GameObject hubGo = null;
            GameObject systemsGo = GameObject.Find("[Zenith_Systems]");

            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r.name == "SpatialBoard_Monolith") boardGo = r;
                else if (r.name == "SpatialHub_Navigation") hubGo = r;
            }

            if (boardGo == null)
            {
                Debug.LogError("SpatialBoard_Monolith root not found!");
                return;
            }

            // -----------------------------------------------------------------
            // STEP A: UPGRADE COMPANION ON BOARD (KILL PRIMITIVE ORBI, SPAWN FOX)
            // -----------------------------------------------------------------
            var oldOrbi = boardGo.transform.Find("Companion_Orbi");
            if (oldOrbi != null)
            {
                Object.DestroyImmediate(oldOrbi.gameObject);
                Debug.Log("Destroyed legacy primitive Companion_Orbi.");
            }

            var oldSlot = boardGo.transform.Find("Companion_Slot");
            if (oldSlot != null)
            {
                Object.DestroyImmediate(oldSlot.gameObject);
            }

            GameObject companionSlotGo = new GameObject("Companion_Slot");
            companionSlotGo.transform.SetParent(boardGo.transform, false);
            // Position floating right beside the board at eye/chest level
            companionSlotGo.transform.localPosition = new Vector3(0.38f, 0.02f, -0.03f);
            companionSlotGo.transform.localRotation = Quaternion.Euler(0f, -25f, 0f);

            // Spawn Golden Pedestal Plinth
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal_Plinth";
            pedestal.transform.SetParent(companionSlotGo.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, -0.09f, 0f);
            pedestal.transform.localScale = new Vector3(0.18f, 0.02f, 0.18f);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material goldPedMat = new Material(litShader);
            goldPedMat.name = "Mat_Gold_Pedestal";
            goldPedMat.SetColor("_BaseColor", new Color(0.96f, 0.80f, 0.30f, 1f));
            goldPedMat.SetFloat("_Metallic", 0.95f);
            goldPedMat.SetFloat("_Smoothness", 0.92f);
            pedestal.GetComponent<MeshRenderer>().sharedMaterial = goldPedMat;

            var pedCol = pedestal.GetComponent<Collider>();
            if (pedCol != null) pedCol.isTrigger = true;

            var companionSwitcher = pedestal.AddComponent<CompanionSwitcherTrigger>();

            // Spawn the real 3D Fox as default in-scene mascot
            if (foxPrefab != null)
            {
                GameObject foxInstance = (GameObject)PrefabUtility.InstantiatePrefab(foxPrefab, companionSlotGo.transform);
                foxInstance.name = "Active_Companion_Model";
                foxInstance.transform.localPosition = new Vector3(0f, -0.06f, 0f);
                foxInstance.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);
                foxInstance.transform.localRotation = Quaternion.identity;

                var cc = foxInstance.GetComponentInChildren<CharacterController>();
                if (cc != null) { cc.stepOffset = 0f; cc.enabled = false; }
            }

            // Wire up CompanionSystem on [Zenith_Systems]
            if (systemsGo != null)
            {
                var cs = systemsGo.GetComponent<CompanionSystem>();
                if (cs != null)
                {
                    var serializedCS = new SerializedObject(cs);
                    serializedCS.FindProperty("companionSlot").objectReferenceValue = companionSlotGo.transform;
                    serializedCS.FindProperty("foxPrefab").objectReferenceValue = foxPrefab;
                    serializedCS.FindProperty("dogPrefab").objectReferenceValue = dogPrefab;
                    serializedCS.FindProperty("penguinPrefab").objectReferenceValue = penguinPrefab;
                    serializedCS.FindProperty("deerPrefab").objectReferenceValue = deerPrefab;
                    serializedCS.ApplyModifiedProperties();
                    companionSwitcher.BindSystem(cs);
                    Debug.Log("Bound CompanionSystem with 4 real animal prefabs and companionSlot.");
                }
            }

            // -----------------------------------------------------------------
            // STEP B: UPGRADE BOARD MONOLITH BACKPLATE & FRAME
            // -----------------------------------------------------------------
            var backplate = boardGo.transform.Find("Monolith_Backplate");
            if (backplate != null)
            {
                Material boardMat = new Material(litShader);
                boardMat.name = "Mat_Board_ObsidianCeramic";
                boardMat.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.18f, 1f)); // Sleek dark slate ceramic
                boardMat.SetFloat("_Metallic", 0.35f);
                boardMat.SetFloat("_Smoothness", 0.88f);
                backplate.GetComponent<MeshRenderer>().sharedMaterial = boardMat;
            }

            // Outer Metallic Gold Beveled Frame
            var oldFrame = boardGo.transform.Find("Monolith_BeveledFrame");
            if (oldFrame != null) Object.DestroyImmediate(oldFrame.gameObject);

            GameObject frameGo = new GameObject("Monolith_BeveledFrame");
            frameGo.transform.SetParent(boardGo.transform, false);
            frameGo.transform.localPosition = new Vector3(0f, 0f, 0.005f);

            Material goldFrameMat = new Material(litShader);
            goldFrameMat.name = "Mat_Frame_ChampagneGold";
            goldFrameMat.SetColor("_BaseColor", new Color(0.92f, 0.78f, 0.36f, 1f));
            goldFrameMat.SetFloat("_Metallic", 0.95f);
            goldFrameMat.SetFloat("_Smoothness", 0.94f);

            // 4 Frame Borders
            CreateFrameBar("Bar_Top", new Vector3(0f, 0.285f, 0f), new Vector3(0.59f, 0.022f, 0.025f), goldFrameMat, frameGo.transform);
            CreateFrameBar("Bar_Bottom", new Vector3(0f, -0.285f, 0f), new Vector3(0.59f, 0.022f, 0.025f), goldFrameMat, frameGo.transform);
            CreateFrameBar("Bar_Left", new Vector3(-0.285f, 0f, 0f), new Vector3(0.022f, 0.59f, 0.025f), goldFrameMat, frameGo.transform);
            CreateFrameBar("Bar_Right", new Vector3(0.285f, 0f, 0f), new Vector3(0.022f, 0.59f, 0.025f), goldFrameMat, frameGo.transform);

            // -----------------------------------------------------------------
            // STEP C: UPGRADE HEART TOKENS (NO MORE PINK CYLINDERS)
            // -----------------------------------------------------------------
            var heartsRoot = boardGo.transform.Find("HeartTokens_Header");
            if (heartsRoot != null)
            {
                // Clear old cylinders
                for (int i = heartsRoot.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(heartsRoot.GetChild(i).gameObject);
                }

                // Heart Plinth Tray
                GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tray.name = "Tray_Backing";
                tray.transform.SetParent(heartsRoot, false);
                tray.transform.localPosition = Vector3.zero;
                tray.transform.localScale = new Vector3(0.20f, 0.035f, 0.012f);
                tray.GetComponent<MeshRenderer>().sharedMaterial = goldFrameMat;
                var trayCol = tray.GetComponent<Collider>();
                if (trayCol != null) Object.DestroyImmediate(trayCol);

                Material rubyMat = new Material(litShader);
                rubyMat.name = "Mat_Ruby_Heart";
                rubyMat.SetColor("_BaseColor", new Color(1.0f, 0.15f, 0.30f, 1f));
                rubyMat.SetFloat("_Metallic", 0.2f);
                rubyMat.SetFloat("_Smoothness", 0.95f);
                rubyMat.EnableKeyword("_EMISSION");
                rubyMat.SetColor("_EmissionColor", new Color(0.9f, 0.08f, 0.20f, 1f) * 0.4f);

                for (int i = 0; i < 3; i++)
                {
                    float x = -0.06f + (i * 0.06f);
                    GameObject heartObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    heartObj.name = $"Heart_Token_{i + 1}";
                    heartObj.transform.SetParent(heartsRoot, false);
                    heartObj.transform.localPosition = new Vector3(x, 0f, -0.012f);
                    heartObj.transform.localScale = new Vector3(0.032f, 0.032f, 0.022f);
                    heartObj.GetComponent<MeshRenderer>().sharedMaterial = rubyMat;
                    var c = heartObj.GetComponent<Collider>();
                    if (c != null) Object.DestroyImmediate(c);
                }
            }

            // -----------------------------------------------------------------
            // STEP D: UPGRADE FLOATING KEYPAD VIEW
            // -----------------------------------------------------------------
            var keypad = boardGo.transform.Find("FloatingKeypad");
            if (keypad != null)
            {
                // Give keypad base a rich chamfered ceramic chassis
                var baseMesh = keypad.Find("Keypad_Chassis") ?? keypad.Find("Base_Chassis");
                if (baseMesh == null)
                {
                    GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    chassis.name = "Keypad_Chassis";
                    chassis.transform.SetParent(keypad, false);
                    chassis.transform.localPosition = new Vector3(0f, 0f, 0.005f);
                    chassis.transform.localScale = new Vector3(0.52f, 0.16f, 0.016f);
                    chassis.GetComponent<MeshRenderer>().sharedMaterial = backplate.GetComponent<MeshRenderer>().sharedMaterial;
                    var col = chassis.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);
                }
            }

            // -----------------------------------------------------------------
            // STEP E: DETACH DIORAMA AND MAKE IT A PERSISTENT 3D WORLD ARCHIPELAGO
            // -----------------------------------------------------------------
            Transform dioramaTransform = null;
            if (hubGo != null)
            {
                // Fix hub UI canvas scale so it doesn't squash everything
                var rt = hubGo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localScale = new Vector3(0.001f, 0.001f, 0.001f);
                }

                dioramaTransform = hubGo.transform.Find("Diorama_AlpineCampaign");
            }

            GameObject worldDiorama = GameObject.Find("Diorama_ArchipelagoWorld");
            if (worldDiorama == null)
            {
                if (dioramaTransform != null)
                {
                    // Reparent out of UI Canvas into root scene space!
                    dioramaTransform.SetParent(null, true);
                    worldDiorama = dioramaTransform.gameObject;
                    worldDiorama.name = "Diorama_ArchipelagoWorld";
                }
                else
                {
                    worldDiorama = new GameObject("Diorama_ArchipelagoWorld");
                }
            }

            // Position gracefully directly in front of the player beneath the board
            worldDiorama.transform.position = new Vector3(0f, 0.45f, 1.05f);
            worldDiorama.transform.rotation = Quaternion.identity;
            worldDiorama.transform.localScale = Vector3.one * 0.50f;
            worldDiorama.SetActive(true);

            // Ensure Summit Epic Chest is mounted and visible
            var islandTerrain = worldDiorama.transform.Find("Island_Terrain");
            if (islandTerrain != null)
            {
                var chestInDiorama = islandTerrain.Find("EpicChest_PF_URP");
                if (chestInDiorama == null && chestPrefab != null)
                {
                    GameObject chestInst = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, islandTerrain);
                    chestInst.name = "EpicChest_PF_URP";
                    chestInst.transform.localPosition = new Vector3(0.35f, 0.28f, 0.15f);
                    chestInst.transform.localScale = Vector3.one * 0.15f;
                    chestInst.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
                }
            }

            // Activate SpatialBoard_Monolith so player sees it
            boardGo.SetActive(true);

            // Save Scene Cleanly
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=#34C759><b>[Zenith MR Upgrade] Full 3D upgrade successfully applied and baked into SampleScene.unity!</b></color>");
        }

        private static GameObject CreateFrameBar(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = pos;
            bar.transform.localScale = scale;
            bar.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var c = bar.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            return bar;
        }
    }
}
