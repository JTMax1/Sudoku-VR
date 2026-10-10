using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Networking;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Visual and interactive representation of an individual Sudoku cell in 3D VR space.
    /// Uses MaterialPropertyBlock to avoid runtime material allocations and maintain SRP batching.
    /// Zero tick-loop allocations with pre-cached digit strings.
    /// </summary>
    public class SudokuCellView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private MeshRenderer backgroundRenderer;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private BoxCollider interactionCollider;

        [Header("Visual Colors (URP)")]
        [SerializeField] private Color defaultBgColor = new Color(0.12f, 0.14f, 0.18f, 1f);
        [SerializeField] private Color prefilledBgColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        [SerializeField] private Color selectedBgColor = new Color(0.0f, 0.55f, 0.95f, 1f);
        [SerializeField] private Color relatedBgColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color conflictBgColor = new Color(0.85f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color correctBgColor = new Color(0.15f, 0.75f, 0.35f, 1f);

        [SerializeField] private Color defaultTextColor = new Color(0.6f, 0.75f, 1.0f, 1f);
        [SerializeField] private Color prefilledTextColor = new Color(0.95f, 0.98f, 1.0f, 1f);
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color conflictTextColor = Color.white;

        // Pre-cached digit strings to eliminate boxing and string allocations completely
        private static readonly string[] DigitStrings = { "", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _propBlock;
        private int _row;
        private int _col;
        private int _currentValue;
        private bool _isPrefilled;
        private bool _isRelated;
        private CellVisualState _visualState = CellVisualState.Default;

        public int Row => _row;
        public int Col => _col;
        public int Value => _currentValue;
        public bool IsPrefilled => _isPrefilled;
        public CellVisualState VisualState => _visualState;

        public event Action<int, int> OnCellClicked;
        public event Action<int, int> OnCellHovered;
        public event Action<int, int> OnCellUnhovered;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (backgroundRenderer == null)
            {
                backgroundRenderer = GetComponent<MeshRenderer>();
            }

            if (valueText == null)
            {
                valueText = GetComponentInChildren<TMP_Text>();
            }

            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<BoxCollider>();
            }
        }

        public void Setup(int row, int col)
        {
            _row = row;
            _col = col;
            name = $"Cell_{row}_{col}";
        }

        public void SetPrefilled(bool prefilled)
        {
            _isPrefilled = prefilled;
        }

        public void SetValue(int value)
        {
            _currentValue = Mathf.Clamp(value, 0, 9);
            if (valueText != null)
            {
                valueText.text = DigitStrings[_currentValue];
                valueText.fontStyle = _isPrefilled ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        public void SetVisualState(CellVisualState state, PlayerId peerId = default)
        {
            _visualState = state;
            ApplyVisuals(peerId);
        }

        public void SetRelatedHighlight(bool isRelated)
        {
            _isRelated = isRelated;
            if (_visualState == CellVisualState.Default)
            {
                ApplyVisuals(default);
            }
        }

        private void ApplyVisuals(PlayerId peerId)
        {
            if (backgroundRenderer == null) return;

            Color targetBg;
            Color targetText = _isPrefilled ? prefilledTextColor : defaultTextColor;

            switch (_visualState)
            {
                case CellVisualState.Prefilled:
                    targetBg = prefilledBgColor;
                    targetText = prefilledTextColor;
                    break;
                case CellVisualState.Selected:
                    targetBg = selectedBgColor;
                    targetText = selectedTextColor;
                    break;
                case CellVisualState.Conflict:
                    targetBg = conflictBgColor;
                    targetText = conflictTextColor;
                    break;
                case CellVisualState.Correct:
                    targetBg = correctBgColor;
                    targetText = Color.white;
                    break;
                case CellVisualState.PeerSelected:
                    targetBg = PlayerId.GetPlayerColor(peerId);
                    targetText = Color.white;
                    break;
                case CellVisualState.Default:
                default:
                    targetBg = _isPrefilled ? prefilledBgColor : (_isRelated ? relatedBgColor : defaultBgColor);
                    break;
            }

            backgroundRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, targetBg);
            _propBlock.SetColor(ColorId, targetBg);
            backgroundRenderer.SetPropertyBlock(_propBlock);

            if (valueText != null)
            {
                valueText.color = targetText;
            }
        }

        public void TriggerClick()
        {
            OnCellClicked?.Invoke(_row, _col);
        }

        public void TriggerHover()
        {
            OnCellHovered?.Invoke(_row, _col);
        }

        public void TriggerUnhover()
        {
            OnCellUnhovered?.Invoke(_row, _col);
        }
    }
}
