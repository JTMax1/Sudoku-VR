using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SudokuVR.Core;

namespace SudokuVR.UI
{
    /// <summary>
    /// Monumental 3D Floating Title & Splash Experience for Sudoku Zenith MR.
    /// Builds true volumetric 3D polygonal letter meshes for "S U D O K U" and "Z E N I T H   M R",
    /// with harmonic floating wave dynamics, luxury PBR materials, and a tactile "ENTER ZENITH" 3D button.
    /// </summary>
    public class TitleScreen3DController : MonoBehaviour
    {
        [Header("Root & Layout Config")]
        [SerializeField] private Transform titleRoot;
        [SerializeField] private float letterScale = 0.11f;
        [SerializeField] private float bobFrequency = 1.6f;
        [SerializeField] private float bobAmplitude = 0.025f;

        [Header("Tactile Portal Button")]
        [SerializeField] private Button enterButton;

        private Vector3 _initialLocalPos;
        private bool _isDismissed = false;
        private readonly List<Transform> _letterTransforms = new List<Transform>();
        private readonly List<float> _letterPhaseOffsets = new List<float>();

        public event Action OnEnterZenithPressed;

        private void Awake()
        {
            if (titleRoot == null) titleRoot = transform;
            _initialLocalPos = titleRoot.localPosition;

            if (enterButton != null)
            {
                enterButton.onClick.AddListener(HandleEnterPressed);
            }

            BuildVolumetric3DLetterVisuals();
        }

        private void Start()
        {
            // Ensure SpatialHub_Navigation is hidden while TitleScreen_3D is showing
            var mm = FindAnyObjectByType<MainMenuController>();
            if (mm != null && mm.gameObject.activeSelf)
            {
                mm.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isDismissed) return;

            // Global gentle breathing levitation
            float hoverY = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            float tiltZ = Mathf.Sin(Time.time * 0.9f) * 1.0f;

            titleRoot.localPosition = _initialLocalPos + new Vector3(0f, hoverY, 0f);
            titleRoot.localRotation = Quaternion.Euler(0f, 0f, tiltZ);

            // Individual letter harmonic wave ripple
            for (int i = 0; i < _letterTransforms.Count; i++)
            {
                if (_letterTransforms[i] != null)
                {
                    float letterWave = Mathf.Sin(Time.time * 2.4f + _letterPhaseOffsets[i]) * 0.012f;
                    Vector3 basePos = _letterTransforms[i].localPosition;
                    _letterTransforms[i].localPosition = new Vector3(basePos.x, basePos.y + letterWave * 0.05f, basePos.z);
                }
            }
        }

        public void HandleEnterPressed()
        {
            if (_isDismissed) return;
            _isDismissed = true;

            TactileAudioService.Instance?.PlayVictoryFanfare();

            CancelInvoke(nameof(DismissTitleComplete));
            Invoke(nameof(DismissTitleComplete), 0.5f);
        }

        private void DismissTitleComplete()
        {
            gameObject.SetActive(false);
            OnEnterZenithPressed?.Invoke();

            var mm = FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (mm != null)
            {
                mm.gameObject.SetActive(true);
                mm.ShowHubView();
            }
        }

        public void ShowTitleScreen()
        {
            _isDismissed = false;
            gameObject.SetActive(true);
            titleRoot.localPosition = _initialLocalPos;
        }

        #region Volumetric 3D Letter Construction

        public void BuildVolumetric3DLetterVisuals()
        {
            if (titleRoot == null) titleRoot = transform;

            // If already built with 3D letters, cache transforms and return
            var existingContainer = titleRoot.Find("Container_3DLetters");
            if (existingContainer != null)
            {
                CacheLetterTransforms(existingContainer);
                return;
            }

            // Remove any legacy 2D text elements
            for (int i = titleRoot.childCount - 1; i >= 0; i--)
            {
                var child = titleRoot.GetChild(i);
                if (child.name.StartsWith("Text_") || child.name.StartsWith("Button_"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            GameObject container = new GameObject("Container_3DLetters");
            container.transform.SetParent(titleRoot, false);
            container.transform.localPosition = Vector3.zero;

            // Materials
            Material goldMat = CreateTitleMaterial("Mat_Title_Gold", new Color(0.98f, 0.82f, 0.32f, 1f), 0.95f, 0.92f, new Color(0.98f, 0.82f, 0.32f, 1f) * 0.25f);
            Material cyanMat = CreateTitleMaterial("Mat_Title_Cyan", new Color(0.05f, 0.88f, 1.0f, 1f), 0.40f, 0.95f, new Color(0.05f, 0.88f, 1.0f, 1f) * 0.55f);
            Material magentaMat = CreateTitleMaterial("Mat_Title_Magenta", new Color(1.0f, 0.25f, 0.65f, 1f), 0.35f, 0.95f, new Color(1.0f, 0.25f, 0.65f, 1f) * 0.50f);

            // 1. Build "S U D O K U" in Polished Champagne Gold (Top Row)
            string topWord = "SUDOKU";
            float topY = 0.18f;
            float topSpacing = 0.115f;
            float topStartX = -((topWord.Length - 1) * topSpacing) * 0.5f;

            for (int i = 0; i < topWord.Length; i++)
            {
                char ch = topWord[i];
                GameObject letterGo = CreateLetterObject($"Letter_{ch}", ch, goldMat, container.transform);
                float x = topStartX + (i * topSpacing);
                // Subtle curve arc
                float z = -Mathf.Cos((x / 0.6f) * 0.5f) * 0.04f + 0.04f;
                letterGo.transform.localPosition = new Vector3(x, topY, z);
                letterGo.transform.localScale = Vector3.one * letterScale;

                _letterTransforms.Add(letterGo.transform);
                _letterPhaseOffsets.Add(i * 0.45f);
            }

            // 2. Build "ZENITH MR" in Electric Cyan & Magenta Glaze (Bottom Row)
            string bottomWord = "ZENITH";
            float botY = 0.04f;
            float botSpacing = 0.105f;
            float botStartX = -((bottomWord.Length + 2.5f) * botSpacing) * 0.5f;

            for (int i = 0; i < bottomWord.Length; i++)
            {
                char ch = bottomWord[i];
                GameObject letterGo = CreateLetterObject($"Letter_{ch}", ch, cyanMat, container.transform);
                float x = botStartX + (i * botSpacing);
                float z = -Mathf.Cos((x / 0.8f) * 0.5f) * 0.04f + 0.04f;
                letterGo.transform.localPosition = new Vector3(x, botY, z);
                letterGo.transform.localScale = Vector3.one * (letterScale * 0.92f);

                _letterTransforms.Add(letterGo.transform);
                _letterPhaseOffsets.Add(i * 0.45f + 1.2f);
            }

            // "MR" in Magenta
            string mrWord = "MR";
            float mrStartX = botStartX + ((bottomWord.Length + 0.8f) * botSpacing);
            for (int i = 0; i < mrWord.Length; i++)
            {
                char ch = mrWord[i];
                GameObject letterGo = CreateLetterObject($"Letter_{ch}", ch, magentaMat, container.transform);
                float x = mrStartX + (i * botSpacing);
                float z = -Mathf.Cos((x / 0.8f) * 0.5f) * 0.04f + 0.04f;
                letterGo.transform.localPosition = new Vector3(x, botY, z);
                letterGo.transform.localScale = Vector3.one * (letterScale * 0.92f);

                _letterTransforms.Add(letterGo.transform);
                _letterPhaseOffsets.Add(i * 0.45f + 2.5f);
            }

            // 3. Subtitle Banner Plaque (Crisp, proportional typography)
            GameObject subObj = new GameObject("Text_Subtitle");
            subObj.transform.SetParent(container.transform, false);
            subObj.transform.localPosition = new Vector3(0f, -0.065f, -0.015f);
            subObj.transform.localScale = new Vector3(0.016f, 0.016f, 0.016f);
            var subTmp = subObj.AddComponent<TextMeshPro>();
            subTmp.text = "THE TACTILE SPATIAL PUZZLE EXPERIENCE";
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.fontSize = 20f;
            subTmp.characterSpacing = 6f;
            subTmp.color = new Color(0.85f, 0.92f, 1.0f, 0.90f);

            // 4. Chunky 3D Tactile "ENTER ZENITH" Portal Button
            GameObject btnRoot = new GameObject("Button_EnterZenith");
            btnRoot.transform.SetParent(container.transform, false);
            btnRoot.transform.localPosition = new Vector3(0f, -0.155f, -0.02f);
            btnRoot.transform.localScale = new Vector3(0.32f, 0.07f, 0.045f);

            GameObject pillMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillMesh.name = "Pill_Mesh";
            pillMesh.transform.SetParent(btnRoot.transform, false);
            pillMesh.transform.localScale = Vector3.one;

            Material btnMat = CreateTitleMaterial("Mat_Title_Button", new Color(0.12f, 0.55f, 0.92f, 1f), 0.2f, 0.95f, new Color(0.12f, 0.55f, 0.92f, 1f) * 0.4f);
            pillMesh.GetComponent<MeshRenderer>().sharedMaterial = btnMat;

            GameObject btnLbl = new GameObject("Label");
            btnLbl.transform.SetParent(btnRoot.transform, false);
            btnLbl.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            btnLbl.transform.localScale = new Vector3(0.24f, 0.95f, 1f);

            var lblTmp = btnLbl.AddComponent<TextMeshPro>();
            lblTmp.text = "ENTER ZENITH";
            lblTmp.alignment = TextAlignmentOptions.Center;
            lblTmp.fontSize = 22f;
            lblTmp.fontStyle = FontStyles.Bold;
            lblTmp.color = Color.white;

            var col = pillMesh.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;

            var trigger = pillMesh.AddComponent<TitleEnterTrigger>();
            trigger.BindController(this);
        }

        private void CacheLetterTransforms(Transform container)
        {
            _letterTransforms.Clear();
            _letterPhaseOffsets.Clear();
            int idx = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child.name.StartsWith("Letter_"))
                {
                    _letterTransforms.Add(child);
                    _letterPhaseOffsets.Add(idx * 0.45f);
                    idx++;
                }
            }
        }

        private GameObject CreateLetterObject(string name, char letter, Material mat, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

#if UNITY_EDITOR
            string assetPath = $"Assets/_Project/Art/Models/3DText/Letter_{letter}.obj";
            var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            Mesh mesh = null;
            if (subAssets != null)
            {
                foreach (var obj in subAssets)
                {
                    if (obj is Mesh m) { mesh = m; break; }
                }
            }
            if (mesh != null)
            {
                mf.sharedMesh = mesh;
            }
            else
            {
                // Fallback to primitive cube
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }
#else
            Mesh mesh = Resources.Load<Mesh>($"3DText/Letter_{letter}");
            if (mesh != null)
            {
                mf.sharedMesh = mesh;
            }
            else
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(temp);
            }
#endif
            return go;
        }

        private Material CreateTitleMaterial(string matName, Color baseColor, float metallic, float smoothness, Color emission)
        {
#if UNITY_EDITOR
            string assetPath = $"Assets/_Project/Art/Materials/Title/{matName}.mat";
            Material existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material newMat = new Material(shader);
            newMat.name = matName;
            newMat.SetColor("_BaseColor", baseColor);
            newMat.SetFloat("_Metallic", metallic);
            newMat.SetFloat("_Smoothness", smoothness);
            newMat.EnableKeyword("_EMISSION");
            newMat.SetColor("_EmissionColor", emission);

            UnityEditor.AssetDatabase.CreateAsset(newMat, assetPath);
            return newMat;
#else
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            return mat;
#endif
        }

        #endregion
    }

    /// <summary>
    /// Trigger helper for physical poke contact on the Enter Zenith button.
    /// </summary>
    public class TitleEnterTrigger : MonoBehaviour
    {
        private TitleScreen3DController _controller;

        public void BindController(TitleScreen3DController controller)
        {
            _controller = controller;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_controller != null)
            {
                _controller.HandleEnterPressed();
            }
        }
    }
}
