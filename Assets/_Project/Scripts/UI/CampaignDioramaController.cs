using System;
using System.Collections.Generic;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Gameplay;

namespace SudokuVR.UI
{
    /// <summary>
    /// Master controller for the Living Sky Island Zenith Archipelago Diorama.
    /// Assembles rich stylized 3D assets (floating sky island bases, clouds, palm trees,
    /// pine forests, fantasy mushrooms, alpine chalets, animated wildlife companions,
    /// interactive tactile level pedestals 1-5, and the animated URP Epic Treasure Chest).
    /// </summary>
    public class CampaignDioramaController : MonoBehaviour
    {
        [Header("Diorama Root Settings")]
        [SerializeField] private Transform terrainRoot;
        [SerializeField] private Transform nodesRoot;
        [SerializeField] private Transform cableCarRoot;
        [SerializeField] private Transform cloudsRoot;
        [SerializeField] private Animator summitChestAnimator;

        [Header("Ambient Motion")]
        [SerializeField] private float cableCarSpeed = 0.08f;
        [SerializeField] private float cloudDriftSpeed = 0.025f;
        [SerializeField] private bool reducedMotion = false;

        private readonly List<DioramaLevelNode> _nodes = new List<DioramaLevelNode>();
        private Vector3 _cableCarStartPos;
        private Vector3 _cableCarEndPos;
        private float _cableCarProgress;
        private bool _isChestOpened = false;

        private void Awake()
        {
            if (terrainRoot == null)
            {
                BuildAuthenticAlpineDiorama();
            }

            _cableCarStartPos = new Vector3(-0.35f, 0.12f, -0.05f);
            _cableCarEndPos = new Vector3(0.35f, 0.32f, 0.05f);
        }

        private void Start()
        {
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnProgressionUpdated += RefreshAllNodes;
            }
            RefreshAllNodes();
        }

        private void OnDestroy()
        {
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnProgressionUpdated -= RefreshAllNodes;
            }
        }

        private void Update()
        {
            if (reducedMotion) return;

            // Ambient miniature cable car motion
            if (cableCarRoot != null)
            {
                _cableCarProgress = Mathf.PingPong(Time.time * cableCarSpeed, 1.0f);
                cableCarRoot.localPosition = Vector3.Lerp(_cableCarStartPos, _cableCarEndPos, _cableCarProgress);
            }

            // Floating clouds gentle bob and drift
            if (cloudsRoot != null)
            {
                float driftX = Mathf.Sin(Time.time * cloudDriftSpeed) * 0.04f;
                float bobY = Mathf.Sin(Time.time * 0.8f) * 0.015f;
                cloudsRoot.localPosition = new Vector3(driftX, 0.42f + bobY, 0.05f);
            }
        }

        public void RefreshAllNodes()
        {
            var profile = ProgressionManager.Instance?.Profile;
            if (profile == null) return;

            Difficulty[] diffs = { Difficulty.Beginner, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Extreme };

            for (int i = 0; i < _nodes.Count && i < diffs.Length; i++)
            {
                var node = _nodes[i];
                var tierData = (i < profile.tierProgress.Length) ? profile.tierProgress[i] : null;

                bool unlocked = (i == 0) || (tierData != null && tierData.isUnlocked);
                int stars = (tierData != null && tierData.completedCount > 0) ? Mathf.Min(3, tierData.completedCount) : 0;

                node.Setup(diffs[i], i + 1, unlocked, stars);
            }
        }

        public void SetReducedMotion(bool enable)
        {
            reducedMotion = enable;
        }

        #region Authentic Sky Island & Multi-Biome Assembly

        public void BuildAuthenticAlpineDiorama()
        {
            // Clear existing children
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            // 1. Terrain Root & Floating Sky Island Base
            var islandObj = new GameObject("Island_Terrain");
            islandObj.transform.SetParent(transform, false);
            islandObj.transform.localPosition = Vector3.zero;
            terrainRoot = islandObj.transform;

            // Primary Floating Island Foundation
            GameObject islandBase = LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/SkyIslandPackFree01_PirateIslandBase_A01.prefab",
                new Vector3(0f, -0.12f, 0f), new Vector3(0.045f, 0.045f, 0.045f), Quaternion.Euler(0, 180, 0), terrainRoot);

            if (islandBase == null)
            {
                // Fallback plinth
                GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plinth.name = "Basalt_Foundation";
                plinth.transform.SetParent(terrainRoot, false);
                plinth.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                plinth.transform.localScale = new Vector3(1.1f, 0.04f, 0.75f);
                SafeDestroy(plinth.GetComponent<Collider>());
            }

            // -------------------------------------------------------------
            // BIOME 1: LEVEL 1 — COASTAL SKY SHORE (Beginner)
            // -------------------------------------------------------------
            LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/SkyIslandPackFree01_WoodenPier_A01.prefab",
                new Vector3(-0.38f, 0.04f, -0.15f), new Vector3(0.04f, 0.04f, 0.04f), Quaternion.Euler(0, 45, 0), terrainRoot);

            LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/SkyIslandPackFree01_PalmTree_A01.prefab",
                new Vector3(-0.44f, 0.06f, -0.08f), new Vector3(0.045f, 0.045f, 0.045f), Quaternion.Euler(0, 20, 0), terrainRoot);

            LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/SkyIslandPackFree01_Barrel_A01.prefab",
                new Vector3(-0.32f, 0.06f, -0.18f), new Vector3(0.04f, 0.04f, 0.04f), Quaternion.identity, terrainRoot);

            // Animated Companion 1: Shiba/Doggo at coastal haven
            GameObject dogGo = LoadAndInstantiate("Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab",
                new Vector3(-0.30f, 0.065f, -0.10f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.Euler(0, 135, 0), terrainRoot);
            if (dogGo != null)
            {
                var dogAnim = dogGo.GetComponentInChildren<Animator>();
                if (dogAnim != null) dogAnim.Play("Dog_001_idle");
            }

            // -------------------------------------------------------------
            // BIOME 2: LEVEL 2 — WHISPERING PINE FOREST (Easy)
            // -------------------------------------------------------------
            SpawnDecorModel("tree-snow-a", new Vector3(-0.24f, 0.10f, 0.14f), new Vector3(0.25f, 0.25f, 0.25f), Quaternion.Euler(0, 30, 0), terrainRoot);
            SpawnDecorModel("tree-snow-b", new Vector3(-0.16f, 0.12f, -0.16f), new Vector3(0.28f, 0.28f, 0.28f), Quaternion.Euler(0, 90, 0), terrainRoot);

            LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/SkyIslandPackFree01_WoodenWatchtower_A01.prefab",
                new Vector3(-0.18f, 0.11f, 0.16f), new Vector3(0.035f, 0.035f, 0.035f), Quaternion.Euler(0, -25, 0), terrainRoot);

            // Animated Companion 2: Clever Fox patrolling the forest
            GameObject foxGo = LoadAndInstantiate("Assets/Fox/Prefabs/Fox.prefab",
                new Vector3(-0.12f, 0.11f, 0.06f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.Euler(0, -60, 0), terrainRoot);
            if (foxGo != null)
            {
                var foxAnim = foxGo.GetComponentInChildren<Animator>();
                if (foxAnim != null) foxAnim.Play("Fox_Idle");
            }

            // -------------------------------------------------------------
            // BIOME 3: LEVEL 3 — FANTASY CRYSTAL GLADE (Medium)
            // -------------------------------------------------------------
            LoadAndInstantiate("Assets/ithappy/Fantasy_FREE/Prefabs/fabulous_mushroom_001.prefab",
                new Vector3(0.02f, 0.16f, -0.16f), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.identity, terrainRoot);

            LoadAndInstantiate("Assets/ithappy/Fantasy_FREE/Prefabs/fabulous_mushroom_002.prefab",
                new Vector3(-0.04f, 0.15f, -0.12f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.Euler(0, 45, 0), terrainRoot);

            // Animated Companion 3: Forest Deer grazing in glade
            GameObject deerGo = LoadAndInstantiate("Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab",
                new Vector3(0.04f, 0.17f, -0.06f), new Vector3(0.13f, 0.13f, 0.13f), Quaternion.Euler(0, -45, 0), terrainRoot);
            if (deerGo != null)
            {
                var deerAnim = deerGo.GetComponentInChildren<Animator>();
                if (deerAnim != null) deerAnim.Play("Deer_001_idle");
            }

            // -------------------------------------------------------------
            // BIOME 4: LEVEL 4 — ALPINE FROST VILLAGE (Hard)
            // -------------------------------------------------------------
            SpawnDecorModel("cabin-wall", new Vector3(0.18f, 0.20f, 0.10f), new Vector3(0.15f, 0.15f, 0.15f), Quaternion.Euler(0, -20, 0), terrainRoot);
            SpawnDecorModel("cabin-roof-snow-chimney", new Vector3(0.18f, 0.27f, 0.10f), new Vector3(0.15f, 0.15f, 0.15f), Quaternion.Euler(0, -20, 0), terrainRoot);
            SpawnDecorModel("lights-colored", new Vector3(0.18f, 0.30f, 0.04f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, terrainRoot);
            SpawnDecorModel("tree-snow-c", new Vector3(0.24f, 0.24f, 0.16f), new Vector3(0.24f, 0.24f, 0.24f), Quaternion.identity, terrainRoot);

            // Animated Companion 4: Alpine Penguin perched by snow
            GameObject pengGo = LoadAndInstantiate("Assets/ithappy/Animals_FREE/Prefabs/Pinguin_001.prefab",
                new Vector3(0.14f, 0.21f, -0.08f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.Euler(0, 45, 0), terrainRoot);
            if (pengGo != null)
            {
                var pengAnim = pengGo.GetComponentInChildren<Animator>();
                if (pengAnim != null) pengAnim.Play("Pinguin_001_idle");
            }

            // -------------------------------------------------------------
            // BIOME 5: LEVEL 5 — ZENITH SUMMIT & ANIMATED EPIC CHEST (Extreme)
            // -------------------------------------------------------------
            LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/SkyIslandPackFree01_StoneBlock_A01.prefab",
                new Vector3(0.36f, 0.29f, 0f), new Vector3(0.04f, 0.04f, 0.04f), Quaternion.identity, terrainRoot);

            // Authentic URP Animated Epic Chest!
            GameObject chestGo = LoadAndInstantiate("Assets/Stylized 3D Animated Chests – FREE Demo/URP/Chsets/Epic Chest/EpicChest_PF_URP.prefab",
                new Vector3(0.36f, 0.33f, 0f), new Vector3(0.22f, 0.22f, 0.22f), Quaternion.Euler(0, -45, 0), terrainRoot);

            if (chestGo != null)
            {
                summitChestAnimator = chestGo.GetComponentInChildren<Animator>();
                var col = chestGo.GetComponent<BoxCollider>();
                if (col == null) col = chestGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0.65f, 0.65f, 0.65f);
                col.center = new Vector3(0f, 0.25f, 0f);

                var trigger = chestGo.AddComponent<DioramaEpicChestTrigger>();
                trigger.BindDiorama(this);
            }

            // -------------------------------------------------------------
            // 6. INTERACTIVE TACTILE LEVEL PEDESTALS (Levels 1 to 5)
            // -------------------------------------------------------------
            var nodesObj = new GameObject("Level_Nodes");
            nodesObj.transform.SetParent(transform, false);
            nodesRoot = nodesObj.transform;

            Vector3[] nodePositions = new Vector3[]
            {
                new Vector3(-0.35f, 0.075f, -0.04f), // Level 1: Coastal Haven
                new Vector3(-0.18f, 0.125f, 0.02f),  // Level 2: Pine Forest
                new Vector3(0.0f, 0.185f, -0.02f),   // Level 3: Crystal Glade
                new Vector3(0.18f, 0.245f, 0.03f),   // Level 4: Alpine Perch
                new Vector3(0.32f, 0.315f, -0.06f)   // Level 5: Zenith Summit
            };

            Difficulty[] diffs = { Difficulty.Beginner, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Extreme };
            _nodes.Clear();

            for (int i = 0; i < 5; i++)
            {
                GameObject nodeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                nodeObj.name = $"Node_Level_{i + 1}";
                nodeObj.transform.SetParent(nodesRoot, false);
                nodeObj.transform.localPosition = nodePositions[i];
                nodeObj.transform.localScale = new Vector3(0.07f, 0.016f, 0.07f);

                var nodeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                nodeMat.color = (i == 4) ? new Color(0.98f, 0.82f, 0.3f, 1f) : new Color(0.2f, 0.25f, 0.35f, 1f);
                nodeMat.SetFloat("_Metallic", (i == 4) ? 0.9f : 0.4f);
                nodeMat.SetFloat("_Smoothness", 0.92f);
                nodeObj.GetComponent<MeshRenderer>().sharedMaterial = nodeMat;

                var levelNode = nodeObj.AddComponent<DioramaLevelNode>();
                _nodes.Add(levelNode);

                // Add 3D level number
                GameObject txtObj = new GameObject("LevelNum");
                txtObj.transform.SetParent(nodeObj.transform, false);
                txtObj.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                txtObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                txtObj.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
                var tmp = txtObj.AddComponent<TMPro.TextMeshPro>();
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontSize = 24f;
                tmp.text = (i + 1).ToString();
                tmp.color = Color.white;
            }

            // -------------------------------------------------------------
            // 7. MINIATURE CABLE CAR
            // -------------------------------------------------------------
            var carObj = new GameObject("Miniature_CableCar");
            carObj.transform.SetParent(transform, false);
            carObj.transform.localPosition = _cableCarStartPos;
            cableCarRoot = carObj.transform;

            GameObject carBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carBody.name = "Cabin";
            carBody.transform.SetParent(cableCarRoot, false);
            carBody.transform.localScale = new Vector3(0.045f, 0.035f, 0.03f);
            SafeDestroy(carBody.GetComponent<Collider>());
            Material carMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            carMat.color = new Color(0.95f, 0.22f, 0.22f, 1f);
            carBody.GetComponent<MeshRenderer>().sharedMaterial = carMat;

            // -------------------------------------------------------------
            // 8. FLOATING PUFFY 3D CLOUDS
            // -------------------------------------------------------------
            var clouds = new GameObject("Floating_Clouds");
            clouds.transform.SetParent(transform, false);
            clouds.transform.localPosition = new Vector3(0f, 0.42f, 0.05f);
            cloudsRoot = clouds.transform;

            LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/Cloud_01.prefab",
                new Vector3(-0.25f, 0f, 0.08f), new Vector3(0.03f, 0.02f, 0.03f), Quaternion.identity, cloudsRoot);

            LoadAndInstantiate("Assets/SkyIsland_Pack_Free_01/Prefabs/Cloud_02.prefab",
                new Vector3(0.25f, 0.04f, -0.06f), new Vector3(0.025f, 0.02f, 0.025f), Quaternion.identity, cloudsRoot);
        }

        public void HandleChestInteracted()
        {
            if (_isChestOpened) return;
            _isChestOpened = true;

            if (summitChestAnimator != null)
            {
                summitChestAnimator.Play("EpicChest_Open_ANIM");
            }

            TactileAudioService.Instance?.PlayVictoryFanfare();

            // Grant tangible in-game booster rewards
            if (GameBoosterInventory.Instance != null)
            {
                GameBoosterInventory.Instance.GrantRewards(1, 2, 1, 100);
            }
        }

        private GameObject LoadAndInstantiate(string assetPath, Vector3 localPos, Vector3 localScale, Quaternion localRot, Transform parent)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.localPosition = localPos;
                instance.transform.localScale = localScale;
                instance.transform.localRotation = localRot;
                var cc = instance.GetComponentInChildren<CharacterController>();
                if (cc != null)
                {
                    cc.stepOffset = 0f;
                    cc.enabled = false;
                }
                return instance;
            }
#endif
            return null;
        }

        private GameObject SpawnDecorModel(string modelName, Vector3 localPos, Vector3 localScale, Quaternion localRot, Transform parent)
        {
#if UNITY_EDITOR
            string pathDiorama = $"Assets/_Project/Art/Models/Diorama/{modelName}.fbx";
            string pathProps = $"Assets/_Project/Art/Models/Props/{modelName}.fbx";

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(pathDiorama)
                             ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(pathProps);

            if (prefab != null)
            {
                GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.localPosition = localPos;
                instance.transform.localScale = localScale;
                instance.transform.localRotation = localRot;
                return instance;
            }
#endif
            return null;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        #endregion
    }

    /// <summary>
    /// Tactile trigger helper for direct finger poke and ray click on the Summit Epic Chest.
    /// </summary>
    public class DioramaEpicChestTrigger : MonoBehaviour
    {
        private CampaignDioramaController _diorama;

        public void BindDiorama(CampaignDioramaController diorama)
        {
            _diorama = diorama;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_diorama != null)
            {
                _diorama.HandleChestInteracted();
            }
        }

        public void OnPointerClick()
        {
            if (_diorama != null)
            {
                _diorama.HandleChestInteracted();
            }
        }
    }
}
