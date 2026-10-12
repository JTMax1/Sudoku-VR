using System;
using UnityEngine;
using SudokuVR.Interaction;

namespace SudokuVR.Gameplay
{
    public enum BoardTheme
    {
        RoyalPorcelain = 0,
        CyberpunkNeon = 1,
        VolcanicEmber = 2,
        RoseQuartz = 3,
        ZenWoodcraft = 4
    }

    [Serializable]
    public struct ThemePalette
    {
        public string themeName;
        public Color defaultCellBg;
        public Color prefilledCellBg;
        public Color selectedCellBg;
        public Color relatedCellBg;
        public Color conflictCellBg;
        public Color defaultText;
        public Color prefilledText;
        public Color frameColor;
        public Color backplateColor;
        public float smoothness;
        public float metallic;
    }

    /// <summary>
    /// Master visual theme engine for Sudoku Zenith MR.
    /// Supports dynamic switching between 5 luxury aesthetics:
    /// Royal Porcelain, Cyberpunk Neon, Volcanic Ember, Rose Quartz, and Zen Woodcraft.
    /// </summary>
    public class BoardThemeManager : MonoBehaviour
    {
        public static BoardThemeManager Instance { get; private set; }

        [SerializeField] private BoardTheme currentTheme = BoardTheme.RoyalPorcelain;

        private readonly ThemePalette[] _palettes = new ThemePalette[5];

        public BoardTheme CurrentTheme => currentTheme;
        public event Action<BoardTheme> OnThemeChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            InitializePalettes();
        }

        private void InitializePalettes()
        {
            // 0. Royal Porcelain & Champagne Gold
            _palettes[0] = new ThemePalette
            {
                themeName = "Royal Porcelain",
                defaultCellBg = new Color(0.96f, 0.97f, 0.98f, 1.0f),
                prefilledCellBg = new Color(0.93f, 0.94f, 0.96f, 1.0f),
                selectedCellBg = new Color(0.96f, 0.82f, 0.30f, 1.0f),
                relatedCellBg = new Color(0.88f, 0.92f, 0.96f, 1.0f),
                conflictCellBg = new Color(0.95f, 0.42f, 0.42f, 1.0f),
                defaultText = new Color(0.12f, 0.35f, 0.85f, 1.0f),
                prefilledText = new Color(0.10f, 0.12f, 0.15f, 1.0f),
                frameColor = new Color(0.92f, 0.79f, 0.35f, 1.0f),
                backplateColor = new Color(0.08f, 0.10f, 0.13f, 1.0f),
                smoothness = 0.88f,
                metallic = 0.82f
            };

            // 1. Cyberpunk Neon Synthwave
            _palettes[1] = new ThemePalette
            {
                themeName = "Cyberpunk Neon",
                defaultCellBg = new Color(0.06f, 0.07f, 0.11f, 1.0f),
                prefilledCellBg = new Color(0.04f, 0.05f, 0.08f, 1.0f),
                selectedCellBg = new Color(0.95f, 0.15f, 0.65f, 1.0f), // Neon Magenta
                relatedCellBg = new Color(0.10f, 0.15f, 0.25f, 1.0f),
                conflictCellBg = new Color(1.0f, 0.20f, 0.20f, 1.0f),
                defaultText = new Color(0.0f, 0.95f, 1.0f, 1.0f),    // Electric Cyan
                prefilledText = new Color(0.95f, 0.95f, 1.0f, 1.0f),
                frameColor = new Color(0.0f, 0.90f, 1.0f, 1.0f),
                backplateColor = new Color(0.02f, 0.03f, 0.05f, 1.0f),
                smoothness = 0.95f,
                metallic = 0.90f
            };

            // 2. Volcanic Ember
            _palettes[2] = new ThemePalette
            {
                themeName = "Volcanic Ember",
                defaultCellBg = new Color(0.14f, 0.14f, 0.16f, 1.0f),
                prefilledCellBg = new Color(0.09f, 0.09f, 0.11f, 1.0f),
                selectedCellBg = new Color(1.0f, 0.45f, 0.10f, 1.0f), // Molten Orange
                relatedCellBg = new Color(0.22f, 0.18f, 0.16f, 1.0f),
                conflictCellBg = new Color(0.95f, 0.20f, 0.20f, 1.0f),
                defaultText = new Color(1.0f, 0.65f, 0.15f, 1.0f),
                prefilledText = new Color(0.95f, 0.90f, 0.85f, 1.0f),
                frameColor = new Color(0.85f, 0.45f, 0.12f, 1.0f),
                backplateColor = new Color(0.06f, 0.06f, 0.07f, 1.0f),
                smoothness = 0.70f,
                metallic = 0.65f
            };

            // 3. Rose Quartz
            _palettes[3] = new ThemePalette
            {
                themeName = "Rose Quartz",
                defaultCellBg = new Color(0.98f, 0.92f, 0.94f, 1.0f),
                prefilledCellBg = new Color(0.94f, 0.88f, 0.90f, 1.0f),
                selectedCellBg = new Color(0.95f, 0.55f, 0.70f, 1.0f),
                relatedCellBg = new Color(0.92f, 0.85f, 0.88f, 1.0f),
                conflictCellBg = new Color(0.95f, 0.35f, 0.45f, 1.0f),
                defaultText = new Color(0.75f, 0.25f, 0.45f, 1.0f),
                prefilledText = new Color(0.25f, 0.15f, 0.20f, 1.0f),
                frameColor = new Color(0.90f, 0.65f, 0.75f, 1.0f),
                backplateColor = new Color(0.18f, 0.12f, 0.15f, 1.0f),
                smoothness = 0.92f,
                metallic = 0.45f
            };

            // 4. Zen Woodcraft
            _palettes[4] = new ThemePalette
            {
                themeName = "Zen Woodcraft",
                defaultCellBg = new Color(0.85f, 0.78f, 0.68f, 1.0f), // Warm Bamboo
                prefilledCellBg = new Color(0.78f, 0.70f, 0.60f, 1.0f),
                selectedCellBg = new Color(0.82f, 0.62f, 0.32f, 1.0f),
                relatedCellBg = new Color(0.80f, 0.74f, 0.65f, 1.0f),
                conflictCellBg = new Color(0.85f, 0.30f, 0.25f, 1.0f),
                defaultText = new Color(0.35f, 0.22f, 0.12f, 1.0f),
                prefilledText = new Color(0.18f, 0.12f, 0.08f, 1.0f),
                frameColor = new Color(0.65f, 0.48f, 0.28f, 1.0f),
                backplateColor = new Color(0.15f, 0.10f, 0.06f, 1.0f),
                smoothness = 0.55f,
                metallic = 0.25f
            };
        }

        public void SetTheme(BoardTheme theme)
        {
            currentTheme = theme;
            ApplyThemeToActiveBoard();
            OnThemeChanged?.Invoke(theme);
        }

        public void CycleNextTheme()
        {
            int next = ((int)currentTheme + 1) % 5;
            SetTheme((BoardTheme)next);
        }

        public void ApplyThemeToActiveBoard()
        {
            int idx = (int)currentTheme;
            ThemePalette p = _palettes[idx];

            var monolith = GameObject.Find("SpatialBoard_Monolith");
            if (monolith == null) return;

            // Apply backplate and frame colors
            var backplate = monolith.transform.Find("Monolith_Backplate");
            if (backplate != null)
            {
                var mr = backplate.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = p.backplateColor;
            }

            var frame = monolith.transform.Find("BoundingFrame");
            if (frame != null)
            {
                var mr = frame.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = p.frameColor;
            }

            var handleL = monolith.transform.Find("GrabHandle_Left");
            if (handleL != null)
            {
                var mr = handleL.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = p.frameColor;
            }

            var handleR = monolith.transform.Find("GrabHandle_Right");
            if (handleR != null)
            {
                var mr = handleR.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = p.frameColor;
            }

            // Refresh tiles
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var tile = monolith.transform.Find($"Tile_{r}_{c}")?.GetComponent<Tile3DView>();
                    if (tile != null)
                    {
                        tile.SetVisualState(tile.VisualState);
                    }
                }
            }

            Debug.Log($"<color=#FFD700><b>[Theme Manager] Activated aesthetic theme: {p.themeName}</b></color>");
        }

        public void CreateThemeSwitcherPedestal(Transform parent)
        {
            if (parent == null) return;
            var dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dial.name = "Theme_Switcher_Dial";
            dial.transform.SetParent(parent, false);
            dial.transform.localPosition = new Vector3(-0.38f, 0.95f, 0f);
            dial.transform.localScale = new Vector3(0.09f, 0.015f, 0.09f);
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", new Color(0.2f, 0.75f, 1.0f, 1f));
            mat.SetFloat("_Metallic", 0.9f);
            mat.SetFloat("_Smoothness", 0.95f);
            dial.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = dial.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var trigger = dial.AddComponent<ThemeSwitcherTrigger>();
            trigger.BindManager(this);
        }
    }

    /// <summary>
    /// Tactile trigger for cycling themes via direct finger poke or ray click.
    /// </summary>
    public class ThemeSwitcherTrigger : MonoBehaviour
    {
        private BoardThemeManager _manager;

        public void BindManager(BoardThemeManager manager)
        {
            _manager = manager;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_manager != null)
            {
                _manager.CycleNextTheme();
            }
        }

        public void OnPointerClick()
        {
            if (_manager != null)
            {
                _manager.CycleNextTheme();
            }
        }
    }
}

