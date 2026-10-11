using System;
using System.Collections.Generic;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.UI
{
    /// <summary>
    /// Master controller for the Living Alpine Campaign Diorama.
    /// Manages the miniature 3D island terrain, winding level paths across 5 thematic zones,
    /// animated cable cars, floating clouds, and persistent level unlocks.
    /// </summary>
    public class CampaignDioramaController : MonoBehaviour
    {
        [Header("Diorama Root Settings")]
        [SerializeField] private Transform terrainRoot;
        [SerializeField] private Transform nodesRoot;
        [SerializeField] private Transform cableCarRoot;
        [SerializeField] private Transform cloudsRoot;

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
                BuildProceduralAlpineIsland();
            }

            _cableCarStartPos = new Vector3(-0.25f, 0.05f, 0.0f);
            _cableCarEndPos = new Vector3(0.25f, 0.22f, 0.0f);
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
                float driftX = Mathf.Sin(Time.time * cloudDriftSpeed) * 0.04f;
                cloudsRoot.localPosition = new Vector3(driftX, 0.28f, 0.05f);
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

        #region Procedural 3D Alpine Terrain Assembly

        public void BuildProceduralAlpineIsland()
        {
            // 1. Terrain Root
            var islandObj = new GameObject("Island_Terrain");
            islandObj.transform.SetParent(transform, false);
            islandObj.transform.localPosition = Vector3.zero;
            terrainRoot = islandObj.transform;

            // Base Plinth / Floating Rock
            GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = "Floating_Plinth";
            plinth.transform.SetParent(terrainRoot, false);
            plinth.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            plinth.transform.localScale = new Vector3(0.85f, 0.035f, 0.55f);
            Destroy(plinth.GetComponent<Collider>());

            Material rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            rockMat.color = new Color(0.20f, 0.22f, 0.26f, 1f); // Slate basalt rock
            rockMat.SetFloat("_Smoothness", 0.35f);
            plinth.GetComponent<MeshRenderer>().sharedMaterial = rockMat;

            // Alpine Grass & Snow Terraces
            Material meadowMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            meadowMat.color = new Color(0.32f, 0.58f, 0.35f, 1f); // Alpine green

            Material snowMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            snowMat.color = new Color(0.95f, 0.96f, 0.99f, 1f); // Crisp alpine snow
            snowMat.SetFloat("_Smoothness", 0.75f);

            // Left Meadow (Village)
            GameObject meadow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meadow.name = "Terrace_Village";
            meadow.transform.SetParent(terrainRoot, false);
            meadow.transform.localPosition = new Vector3(-0.24f, 0.02f, 0f);
            meadow.transform.localScale = new Vector3(0.32f, 0.06f, 0.35f);
            Destroy(meadow.GetComponent<Collider>());
            meadow.GetComponent<MeshRenderer>().sharedMaterial = meadowMat;

            // Center Peak (Snowy Chalet)
            GameObject centerPeak = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerPeak.name = "Terrace_CenterPeak";
            centerPeak.transform.SetParent(terrainRoot, false);
            centerPeak.transform.localPosition = new Vector3(0.04f, 0.08f, 0f);
            centerPeak.transform.localScale = new Vector3(0.28f, 0.14f, 0.32f);
            Destroy(centerPeak.GetComponent<Collider>());
            centerPeak.GetComponent<MeshRenderer>().sharedMaterial = snowMat;

            // Right Summit (Glacier / Zenith)
            GameObject summit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            summit.name = "Terrace_ZenithSummit";
            summit.transform.SetParent(terrainRoot, false);
            summit.transform.localPosition = new Vector3(0.28f, 0.16f, 0f);
            summit.transform.localScale = new Vector3(0.24f, 0.22f, 0.28f);
            Destroy(summit.GetComponent<Collider>());
            summit.GetComponent<MeshRenderer>().sharedMaterial = snowMat;

            // 2. Nodes Root & 5 Zone Pedestals
            var nodesObj = new GameObject("Level_Nodes");
            nodesObj.transform.SetParent(transform, false);
            nodesRoot = nodesObj.transform;

            Vector3[] nodePositions = new Vector3[]
            {
                new Vector3(-0.32f, 0.065f, -0.05f), // Zone 1: Alpine Village (Beginner)
                new Vector3(-0.16f, 0.075f, 0.05f),  // Zone 2: Pine Forest (Easy)
                new Vector3(0.0f, 0.165f, -0.04f),   // Zone 3: Snowy Peak (Medium)
                new Vector3(0.16f, 0.195f, 0.05f),   // Zone 4: Glacier Ridge (Hard)
                new Vector3(0.30f, 0.285f, -0.02f)   // Zone 5: Zenith Summit (Extreme)
            };

            Difficulty[] diffs = { Difficulty.Beginner, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert, Difficulty.Extreme };

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

            // 3. Miniature Cable Car
            var carObj = new GameObject("Miniature_CableCar");
            carObj.transform.SetParent(transform, false);
            carObj.transform.localPosition = _cableCarStartPos;
            cableCarRoot = carObj.transform;

            GameObject carBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carBody.name = "Cabin";
            carBody.transform.SetParent(cableCarRoot, false);
            carBody.transform.localScale = new Vector3(0.035f, 0.03f, 0.025f);
            Destroy(carBody.GetComponent<Collider>());
            Material carMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            carMat.color = new Color(0.92f, 0.25f, 0.25f, 1f); // Vibrant Swiss alpine red
            carBody.GetComponent<MeshRenderer>().sharedMaterial = carMat;

            // 4. Floating Ambient Clouds
            var clouds = new GameObject("Floating_Clouds");
            clouds.transform.SetParent(transform, false);
            clouds.transform.localPosition = new Vector3(0f, 0.28f, 0.05f);
            cloudsRoot = clouds.transform;

            GameObject cloudMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloudMesh.name = "Cloud_Puff";
            cloudMesh.transform.SetParent(cloudsRoot, false);
            cloudMesh.transform.localScale = new Vector3(0.14f, 0.05f, 0.08f);
            Destroy(cloudMesh.GetComponent<Collider>());
            Material cloudMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            cloudMat.color = new Color(1f, 1f, 1f, 0.85f);
            cloudMat.SetFloat("_Smoothness", 0.9f);
            cloudMesh.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
        }

        #endregion
    }
}
