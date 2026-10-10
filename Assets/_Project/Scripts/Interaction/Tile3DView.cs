using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Networking;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Tactile 3D acrylic cell monolith for Meta Quest 3.
    /// Features:
    /// - Multi-state dynamic lighting (Selected, Related, Identical number match, Error pulse)
    /// - Mini 3x3 candidate pencil/notes grid
    /// - Direct finger poke and controller raycast support
    /// - MaterialPropertyBlock for zero-allocation SRP batching
    /// </summary>
    public class Tile3DView : MonoBehaviour
    {
        [Header("Mesh & Text Components")]
        [SerializeField] private MeshRenderer cellRenderer;
        [SerializeField] private TMP_Text mainDigitText;
        [SerializeField] private GameObject pencilGridRoot;
        [SerializeField] private TMP_Text[] pencilNoteTexts = new TMP_Text[9];
        [SerializeField] private BoxCollider interactionCollider;

        [Header("Colors (URP Acrylic & Emissive Palette)")]
        [SerializeField] private Color defaultBgColor = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        [SerializeField] private Color prefilledBgColor = new Color(0.06f, 0.08f, 0.11f, 1.0f);
        [SerializeField] private Color selectedBgColor = new Color(0.0f, 0.72f, 0.98f, 1.0f);
        [SerializeField] private Color relatedBgColor = new Color(0.16f, 0.22f, 0.30f, 0.95f);
        [SerializeField] private Color identicalBgColor = new Color(0.25f, 0.45f, 0.65f, 1.0f);
        [SerializeField] private Color conflictBgColor = new Color(0.92f, 0.20f, 0.20f, 1.0f);
        [SerializeField] private Color correctBgColor = new Color(0.12f, 0.82f, 0.42f, 1.0f);

        [SerializeField] private Color defaultTextColor = new Color(0.70f, 0.85f, 1.0f, 1f);
        [SerializeField] private Color prefilledTextColor = new Color(0.98f, 0.98f, 1.0f, 1f);
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color conflictTextColor = Color.white;
        [SerializeField] private Color pencilNoteColor = new Color(0.55f, 0.75f, 0.95f, 0.85f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly string[] DigitStrings = { "", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private MaterialPropertyBlock _propBlock;
        private int _row;
        private int _col;
        private int _currentValue;
        private ushort _currentNotesMask;
        private bool _isPrefilled;
        private bool _isRelated;
        private bool _isIdenticalNumber;
        private CellVisualState _visualState = CellVisualState.Default;

        public int Row => _row;
        public int Col => _col;
        public int Value => _currentValue;
        public ushort NotesMask => _currentNotesMask;
        public bool IsPrefilled => _isPrefilled;
        public CellVisualState VisualState => _visualState;

        public event Action<int, int> OnTileClicked;
        public event Action<int, int> OnTileHovered;
        public event Action<int, int> OnTileUnhovered;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (cellRenderer == null)
            {
                cellRenderer = GetComponent<MeshRenderer>();
            }

            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<BoxCollider>();
            }

            EnsurePencilGridInitialized();
        }

        public void Setup(int row, int col)
        {
            _row = row;
            _col = col;
            name = $"Tile_{row}_{col}";
        }

        public void SetPrefilled(bool prefilled)
        {
            _isPrefilled = prefilled;
        }

        public void SetValue(int value)
        {
            _currentValue = Mathf.Clamp(value, 0, 9);
            UpdateDisplay();
        }

        public void SetNotes(ushort mask)
        {
            _currentNotesMask = mask;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_currentValue > 0)
            {
                if (mainDigitText != null)
                {
                    mainDigitText.gameObject.SetActive(true);
                    mainDigitText.text = DigitStrings[_currentValue];
                    mainDigitText.fontStyle = _isPrefilled ? FontStyles.Bold : FontStyles.Normal;
                }
                if (pencilGridRoot != null)
                {
                    pencilGridRoot.SetActive(false);
                }
            }
            else
            {
                if (mainDigitText != null)
                {
                    mainDigitText.gameObject.SetActive(false);
                }

                if (pencilGridRoot != null)
                {
                    bool hasAnyNotes = (_currentNotesMask != 0);
                    pencilGridRoot.SetActive(hasAnyNotes);

                    if (hasAnyNotes)
                    {
                        for (int d = 1; d <= 9; d++)
                        {
                            bool hasNote = (_currentNotesMask & (1 << d)) != 0;
                            if (pencilNoteTexts[d - 1] != null)
                            {
                                pencilNoteTexts[d - 1].gameObject.SetActive(hasNote);
                                if (hasNote)
                                {
                                    pencilNoteTexts[d - 1].text = DigitStrings[d];
                                }
                            }
                        }
                    }
                }
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

        public void SetIdenticalNumberHighlight(bool isIdentical)
        {
            _isIdenticalNumber = isIdentical;
            if (_visualState == CellVisualState.Default && !_isRelated)
            {
                ApplyVisuals(default);
            }
        }

        private void ApplyVisuals(PlayerId peerId)
        {
            if (cellRenderer == null) return;

            Color targetBg;
            Color targetEmission = Color.black;
            Color targetText = _isPrefilled ? prefilledTextColor : defaultTextColor;

            switch (_visualState)
            {
                case CellVisualState.Prefilled:
                    targetBg = prefilledBgColor;
                    targetText = prefilledTextColor;
                    break;
                case CellVisualState.Selected:
                    targetBg = selectedBgColor;
                    targetEmission = selectedBgColor * 0.35f;
                    targetText = selectedTextColor;
                    break;
                case CellVisualState.Conflict:
                    targetBg = conflictBgColor;
                    targetEmission = conflictBgColor * 0.5f;
                    targetText = conflictTextColor;
                    break;
                case CellVisualState.Correct:
                    targetBg = correctBgColor;
                    targetEmission = correctBgColor * 0.4f;
                    targetText = Color.white;
                    break;
                case CellVisualState.PeerSelected:
                    targetBg = PlayerId.GetPlayerColor(peerId);
                    targetEmission = targetBg * 0.3f;
                    targetText = Color.white;
                    break;
                case CellVisualState.Default:
                default:
                    if (_isPrefilled)
                    {
                        targetBg = prefilledBgColor;
                    }
                    else if (_isIdenticalNumber)
                    {
                        targetBg = identicalBgColor;
                        targetEmission = identicalBgColor * 0.2f;
                    }
                    else if (_isRelated)
                    {
                        targetBg = relatedBgColor;
                    }
                    else
                    {
                        targetBg = defaultBgColor;
                    }
                    break;
            }

            cellRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, targetBg);
            _propBlock.SetColor(ColorId, targetBg);
            _propBlock.SetColor(EmissionColorId, targetEmission);
            cellRenderer.SetPropertyBlock(_propBlock);

            if (mainDigitText != null)
            {
                mainDigitText.color = targetText;
            }
        }

        public void TriggerClick()
        {
            OnTileClicked?.Invoke(_row, _col);
        }

        public void TriggerHover()
        {
            OnTileHovered?.Invoke(_row, _col);
        }

        public void TriggerUnhover()
        {
            OnTileUnhovered?.Invoke(_row, _col);
        }

        private void EnsurePencilGridInitialized()
        {
            if (pencilGridRoot == null)
            {
                Transform found = transform.Find("PencilGrid");
                if (found != null)
                {
                    pencilGridRoot = found.gameObject;
                }
            }

            if (pencilGridRoot == null)
            {
                pencilGridRoot = new GameObject("PencilGrid");
                pencilGridRoot.transform.SetParent(transform, false);
                pencilGridRoot.transform.localPosition = new Vector3(0f, 0.51f, 0f);
                pencilGridRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pencilGridRoot.transform.localScale = Vector3.one;

                float step = 0.28f;
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int idx = r * 3 + c;
                        GameObject noteObj = new GameObject($"Note_{idx + 1}");
                        noteObj.transform.SetParent(pencilGridRoot.transform, false);
                        noteObj.transform.localPosition = new Vector3((c - 1) * step, (1 - r) * step, 0f);
                        noteObj.transform.localRotation = Quaternion.identity;
                        noteObj.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);

                        TextMeshPro tmp = noteObj.AddComponent<TextMeshPro>();
                        tmp.alignment = TextAlignmentOptions.Center;
                        tmp.fontSize = 24f;
                        tmp.color = pencilNoteColor;
                        tmp.text = (idx + 1).ToString();
                        noteObj.SetActive(false);

                        pencilNoteTexts[idx] = tmp;
                    }
                }
                pencilGridRoot.SetActive(false);
            }
            else
            {
                for (int i = 0; i < 9; i++)
                {
                    if (pencilNoteTexts[i] == null)
                    {
                        Transform child = pencilGridRoot.transform.Find($"Note_{i + 1}");
                        if (child != null)
                        {
                            pencilNoteTexts[i] = child.GetComponent<TMP_Text>();
                        }
                    }
                }
            }
        }
    }
}
