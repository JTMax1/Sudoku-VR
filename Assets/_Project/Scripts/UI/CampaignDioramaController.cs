using System;
using System.Collections.Generic;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Gameplay;

namespace SudokuVR.UI
{
    /// <summary>
    /// Master controller for the Living Alpine Campaign Diorama.
    /// Assembles authentic stylized 3D low-poly models (snowy pine trees, alpine chalets,
    /// rock formations, festive string lights, interactive level pedestals, and summit treasure chest).
    /// </summary>
    public class CampaignDioramaController : MonoBehaviour
    {
        [Header("Diorama Root Settings")]
        [SerializeField] private Transform terrainRoot;
        [SerializeField] private Transform nodesRoot;
        [SerializeField] private Transform cableCarRoot;
        [SerializeField] private Transform cloudsRoot;
        [SerializeField] private TreasureChestController summitChest;

        [Header("Ambient Motion")]
        [SerializeField] private float cableCarSpeed = 0.08f;
        [SerializeField] private float cloudDriftSpeed = 0.02f;
        [SerializeField] private bool reducedMotion = false;

        private readonly List<DioramaLevelNode> _nodes = new List<DioramaLevelNode>();
        private Vector3 _cableCarStartPos;
        private Vector3 _cableCarEndPos;
        private float _cableCarProgress;

        private void Awake()
        {
            if (terrainRoot == null)
            {
                BuildAuthenticAlpineDiorama();
            }

            _cableCarStartPos = new Vector3(-0.35f, 0.08f, -0.05f);
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

            // Ambient miniature cable car motion between peaks
            if (cableCarRoot != null)
            {
                _cableCarProgress = Mathf.PingPong(Time.time * cableCarSpeed, 1.0f);
                cableCarRoot.localPosition = Vector3.Lerp(_cableCarStartPos, _cableCarEndPos, _cableCarProgress);
            }

            // Subtle drifting clouds
            if (cloudsRoot != null)
            {
                float driftX = Mathf.Sin(Time.time * cloudDriftSpeed) * 0.05f;
                cloudsRoot.localPosition = new Vector3(driftX, 0.38f, 0.05f);
            }
        }

        public void RefreshAllNodes()
        {
            var profile = ProgressionManager.Instance?.Profile;
            if (profile == null) return;

            Difficulty[] diffs = { Difficulty.Beginner, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert, Difficulty.Extreme };

            for (int i = 0; i < _nodes.Count && i < diffs.Length; i++)
            {
                var node = _nodes[i];
                var tierData = (i < profile.tierProgress.Length) ? profile.tierProgress[i] : null;

                bool unlocked = tierData != null && tierData.isUnlocked;
                int stars = (tierData != null && tierData.completedCount > 0) ? Mathf.Min(3, tierData.completedCount) : 0;

                node.Setup(diffs[i], i + 1, unlocked, stars);
            }
        }

        public void SetReducedMotion(bool enable)
        {
            reducedMotion = enable;
        }

        #region Authentic 3D Alpine Diorama Assembly (Using Real 3D FBX Models)

        public void BuildAuthenticAlpineDiorama()
        {
            // Clear legacy children if any
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            // 1. Terrain Root
            var islandObj = new GameObject("Island_Terrain");
            islandObj.transform.SetParent(transform, false);
            islandObj.transform.localPosition = Vector3.zero;
            terrainRoot = islandObj.transform;

            // Base Basalt Plinth ($1.1m x 0.7m)
            GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = "Basalt_Foundation";
            plinth.transform.SetParent(terrainRoot, false);
            plinth.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            plinth.transform.localScale = new Vector3(1.1f, 0.04f, 0.75f);
            SafeDestroy(plinth.GetComponent<Collider>());

            Material rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            rockMat.color = new Color(0.18f, 0.20f, 0.24f, 1f); // Dark basalt rock
            rockMat.SetFloat("_Smoothness", 0.35f);
            plinth.GetComponent<MeshRenderer>().sharedMaterial = rockMat;

            // 2. Instantiate Real Snowy Pine Trees from Assets/_Project/Art/Models/Diorama/
            SpawnDecorModel("tree-snow-a", new Vector3(-0.35f, 0.05f, -0.15f), new Vector3(0.25f, 0.25f, 0.25f), Quaternion.identity, terrainRoot);
            SpawnDecorModel("tree-snow-b", new Vector3(-0.28f, 0.08f, 0.12f), new Vector3(0.30f, 0.30f, 0.30f), Quaternion.Euler(0, 45, 0), terrainRoot);
            SpawnDecorModel("tree-snow-c", new Vector3(-0.12f, 0.12f, -0.18f), new Vector3(0.22f, 0.22f, 0.22f), Quaternion.identity, terrainRoot);
            SpawnDecorModel("tree-snow-a", new Vector3(0.08f, 0.18f, 0.15f), new Vector3(0.28f, 0.28f, 0.28f), Quaternion.Euler(0, 90, 0), terrainRoot);
            SpawnDecorModel("tree-snow-b", new Vector3(0.22f, 0.24f, -0.12f), new Vector3(0.26f, 0.26f, 0.26f), Quaternion.identity, terrainRoot);

            // 3. Modular Alpine Chalet (Alpine Village)
            SpawnDecorModel("cabin-wall", new Vector3(-0.25f, 0.04f, 0f), new Vector3(0.15f, 0.15f, 0.15f), Quaternion.identity, terrainRoot);
            SpawnDecorModel("cabin-roof-snow-chimney", new Vector3(-0.25f, 0.11f, 0f), new Vector3(0.15f, 0.15f, 0.15f), Quaternion.identity, terrainRoot);

            // 4. Real Rocks and Snow Banks
            SpawnDecorModel("rocks-large", new Vector3(0.15f, 0.15f, 0f), new Vector3(0.20f, 0.20f, 0.20f), Quaternion.Euler(0, 30, 0), terrainRoot);
            SpawnDecorModel("rocks-medium", new Vector3(-0.05f, 0.09f, 0.14f), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.identity, terrainRoot);
            SpawnDecorModel("snow-pile", new Vector3(-0.18f, 0.06f, -0.08f), new Vector3(0.25f, 0.25f, 0.25f), Quaternion.identity, terrainRoot);
            SpawnDecorModel("lights-colored", new Vector3(-0.25f, 0.14f, -0.06f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, terrainRoot);

            // 5. Authentic Summit Treasure Chest (The Grand Reward Prop)
            GameObject chestObj = SpawnDecorModel("chest", new Vector3(0.38f, 0.32f, 0f), new Vector3(0.25f, 0.25f, 0.25f), Quaternion.Euler(0, -30, 0), terrainRoot);
            if (chestObj != null)
            {
                summitChest = chestObj.AddComponent<TreasureChestController>();
                var col = chestObj.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0.6f, 0.5f, 0.5f);
            }

            // 6. Level Pedestals (5 Zones)
            var nodesObj = new GameObject("Level_Nodes");
            nodesObj.transform.SetParent(transform, false);
            nodesRoot = nodesObj.transform;

            Vector3[] nodePositions = new Vector3[]
            {
                new Vector3(-0.35f, 0.075f, -0.05f), // Zone 1: Alpine Village (Beginner)
                new Vector3(-0.18f, 0.115f, 0.05f),  // Zone 2: Pine Forest (Easy)
                new Vector3(0.0f, 0.185f, -0.04f),   // Zone 3: Snowy Peak (Medium)
                new Vector3(0.18f, 0.235f, 0.05f),   // Zone 4: Glacier Ridge (Hard)
                new Vector3(0.34f, 0.305f, -0.08f)   // Zone 5: Zenith Summit (Extreme)
            };

            Difficulty[] diffs = { Difficulty.Beginner, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert, Difficulty.Extreme };

            _nodes.Clear();
            for (int i = 0; i < 5; i++)
            {
                GameObject nodeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                nodeObj.name = $"Node_Level_{i + 1}";
                nodeObj.transform.SetParent(nodesRoot, false);
                nodeObj.transform.localPosition = nodePositions[i];
                nodeObj.transform.localScale = new Vector3(0.065f, 0.015f, 0.065f);

                var levelNode = nodeObj.AddComponent<DioramaLevelNode>();
                _nodes.Add(levelNode);

                // Add TextMeshPro level number on top
                GameObject txtObj = new GameObject("LevelNum");
                txtObj.transform.SetParent(nodeObj.transform, false);
                txtObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                txtObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                txtObj.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
                var tmp = txtObj.AddComponent<TMPro.TextMeshPro>();
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontSize = 24f;
                tmp.text = (i + 1).ToString();
                tmp.color = Color.white;
            }

            // 7. Miniature Cable Car
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
            carMat.color = new Color(0.92f, 0.25f, 0.25f, 1f); // Alpine red
            carBody.GetComponent<MeshRenderer>().sharedMaterial = carMat;

            // 8. Drifting Clouds
            var clouds = new GameObject("Floating_Clouds");
            clouds.transform.SetParent(transform, false);
            clouds.transform.localPosition = new Vector3(0f, 0.38f, 0.05f);
            cloudsRoot = clouds.transform;

            GameObject cloudMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloudMesh.name = "Cloud_Puff";
            cloudMesh.transform.SetParent(cloudsRoot, false);
            cloudMesh.transform.localScale = new Vector3(0.16f, 0.055f, 0.09f);
            SafeDestroy(cloudMesh.GetComponent<Collider>());
            Material cloudMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            cloudMat.color = new Color(1f, 1f, 1f, 0.9f);
            cloudMat.SetFloat("_Smoothness", 0.9f);
            cloudMesh.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
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
                GameObject instance = Instantiate(prefab, parent);
                instance.name = modelName;
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
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(obj);
            else Destroy(obj);
#else
            Destroy(obj);
#endif
        }

        #endregion
    }
}
