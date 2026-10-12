using System;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    public enum CompanionType
    {
        FoxClever = 0,
        DogShiba = 1,
        PenguinPippin = 2,
        DeerSylva = 3,
        OrbiStarSprite = 4
    }

    /// <summary>
    /// Multi-companion mascot system for Sudoku Zenith MR.
    /// Manages 5 unique animated 3D mascots (Foxy, Shiba Dog, Pippin Penguin, Sylva Deer, and Orbi),
    /// reacting in real time to correct placements, mistakes, and victories with animated gestures and audio.
    /// </summary>
    public class CompanionSystem : MonoBehaviour
    {
        public static CompanionSystem Instance { get; private set; }

        [Header("Active Companion Configuration")]
        [SerializeField] private CompanionType activeCompanionType = CompanionType.FoxClever;
        [SerializeField] private Transform companionSlot;

        private GameObject _activeInstance;
        private Animator _activeAnimator;

        public CompanionType ActiveType => activeCompanionType;
        public event Action<CompanionType> OnCompanionChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            if (companionSlot == null)
            {
                var found = GameObject.Find("SpatialBoard_Monolith/Companion_Orbi");
                if (found != null) companionSlot = found.transform;
            }
        }

        private void Start()
        {
            SpawnActiveCompanion();
        }

        public void SetCompanion(CompanionType type)
        {
            activeCompanionType = type;
            SpawnActiveCompanion();
            OnCompanionChanged?.Invoke(type);
        }

        public void CycleNextCompanion()
        {
            int count = Enum.GetValues(typeof(CompanionType)).Length;
            int next = ((int)activeCompanionType + 1) % count;
            SetCompanion((CompanionType)next);
        }

        public void TriggerJoyReaction()
        {
            if (_activeAnimator == null) return;

            switch (activeCompanionType)
            {
                case CompanionType.FoxClever:
                    _activeAnimator.Play("Fox_Sit_Yes");
                    break;
                case CompanionType.DogShiba:
                    _activeAnimator.Play("Dog_001_run");
                    Invoke(nameof(ResetToIdle), 1.2f);
                    break;
                case CompanionType.PenguinPippin:
                    _activeAnimator.Play("Pinguin_001_run");
                    Invoke(nameof(ResetToIdle), 1.2f);
                    break;
                case CompanionType.DeerSylva:
                    _activeAnimator.Play("Deer_001_run");
                    Invoke(nameof(ResetToIdle), 1.2f);
                    break;
            }

            TactileAudioService.Instance?.PlayDigitPlaced(6);
        }

        public void TriggerMistakeReaction()
        {
            if (_activeAnimator == null) return;

            switch (activeCompanionType)
            {
                case CompanionType.FoxClever:
                    _activeAnimator.Play("Fox_Sit_No");
                    break;
                case CompanionType.DogShiba:
                case CompanionType.PenguinPippin:
                case CompanionType.DeerSylva:
                    ResetToIdle();
                    break;
            }
        }

        public void TriggerVictoryDance()
        {
            if (_activeAnimator == null) return;

            switch (activeCompanionType)
            {
                case CompanionType.FoxClever:
                    _activeAnimator.Play("Fox_Somersault");
                    break;
                case CompanionType.DogShiba:
                    _activeAnimator.Play("Dog_001_run");
                    break;
                case CompanionType.PenguinPippin:
                    _activeAnimator.Play("Pinguin_001_run");
                    break;
                case CompanionType.DeerSylva:
                    _activeAnimator.Play("Deer_001_run");
                    break;
            }
        }

        private void ResetToIdle()
        {
            if (_activeAnimator == null) return;

            switch (activeCompanionType)
            {
                case CompanionType.FoxClever:
                    _activeAnimator.Play("Fox_Idle");
                    break;
                case CompanionType.DogShiba:
                    _activeAnimator.Play("Dog_001_idle");
                    break;
                case CompanionType.PenguinPippin:
                    _activeAnimator.Play("Pinguin_001_idle");
                    break;
                case CompanionType.DeerSylva:
                    _activeAnimator.Play("Deer_001_idle");
                    break;
            }
        }

        private void SpawnActiveCompanion()
        {
            if (companionSlot == null) return;

            // Clear previous visual children under companion slot
            while (companionSlot.childCount > 0)
            {
                DestroyImmediate(companionSlot.GetChild(0).gameObject);
            }

            switch (activeCompanionType)
            {
                case CompanionType.FoxClever:
                    _activeInstance = LoadPrefab("Assets/Fox/Prefabs/Fox.prefab", new Vector3(0, -0.06f, 0), new Vector3(0.20f, 0.20f, 0.20f), Quaternion.Euler(0, -35, 0));
                    break;
                case CompanionType.DogShiba:
                    _activeInstance = LoadPrefab("Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab", new Vector3(0, -0.06f, 0), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.Euler(0, -35, 0));
                    break;
                case CompanionType.PenguinPippin:
                    _activeInstance = LoadPrefab("Assets/ithappy/Animals_FREE/Prefabs/Pinguin_001.prefab", new Vector3(0, -0.06f, 0), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.Euler(0, -35, 0));
                    break;
                case CompanionType.DeerSylva:
                    _activeInstance = LoadPrefab("Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab", new Vector3(0, -0.06f, 0), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.Euler(0, -35, 0));
                    break;
                case CompanionType.OrbiStarSprite:
                    BuildOrbiMascot();
                    break;
            }

            if (_activeInstance != null)
            {
                _activeAnimator = _activeInstance.GetComponentInChildren<Animator>();
                ResetToIdle();
            }

            // Create tactile companion cycle pedestal button underneath
            CreateCompanionSwitcherPedestal();

            TactileAudioService.Instance?.PlayHint();
            Debug.Log($"<color=#34C759><b>[Companion System] Summoned companion: {activeCompanionType}</b></color>");
        }

        private void CreateCompanionSwitcherPedestal()
        {
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Companion_Switcher_Pedestal";
            pedestal.transform.SetParent(companionSlot, false);
            pedestal.transform.localPosition = new Vector3(0f, -0.09f, 0f);
            pedestal.transform.localScale = new Vector3(0.14f, 0.015f, 0.14f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.32f, 1f)); // Gold pedestal
            mat.SetFloat("_Metallic", 0.9f);
            mat.SetFloat("_Smoothness", 0.95f);
            pedestal.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = pedestal.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var trigger = pedestal.AddComponent<CompanionSwitcherTrigger>();
            trigger.BindSystem(this);
        }

        private GameObject LoadPrefab(string path, Vector3 localPos, Vector3 localScale, Quaternion localRot)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, companionSlot);
                inst.transform.localPosition = localPos;
                inst.transform.localScale = localScale;
                inst.transform.localRotation = localRot;
                var cc = inst.GetComponentInChildren<CharacterController>();
                if (cc != null)
                {
                    cc.stepOffset = 0f;
                    cc.enabled = false;
                }
                return inst;
            }
#endif
            return null;
        }

        private void BuildOrbiMascot()
        {
            var go = new GameObject("Orbi_Visual");
            go.transform.SetParent(companionSlot, false);
            go.transform.localPosition = Vector3.zero;

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = Vector3.one * 0.12f;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", new Color(0.95f, 0.98f, 1.0f, 1f));
            mat.SetFloat("_Smoothness", 0.96f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 1.0f, 1f) * 0.4f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;

            _activeInstance = go;
            _activeAnimator = null;
        }
    }

    /// <summary>
    /// Tactile trigger for cycling companions via direct index poke or ray click.
    /// </summary>
    public class CompanionSwitcherTrigger : MonoBehaviour
    {
        private CompanionSystem _system;

        public void BindSystem(CompanionSystem system)
        {
            _system = system;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_system != null)
            {
                _system.CycleNextCompanion();
            }
        }

        public void OnPointerClick()
        {
            if (_system != null)
            {
                _system.CycleNextCompanion();
            }
        }
    }
}
