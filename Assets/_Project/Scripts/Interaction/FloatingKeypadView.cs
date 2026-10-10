using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Ergonomic curved tactile keypad floating below/alongside the spatial monolith board.
    /// Provides 1-9 numeric tokens with live supply counters and action ribbon (Undo, Erase, Pencil, Hint).
    /// </summary>
    public class FloatingKeypadView : MonoBehaviour
    {
        [Header("Digit Keys (1-9)")]
        [SerializeField] private TactileKeypadButton[] digitButtons = new TactileKeypadButton[9];

        [Header("Action Ribbon")]
        [SerializeField] private TactileKeypadButton undoButton;
        [SerializeField] private TactileKeypadButton eraseButton;
        [SerializeField] private TactileKeypadButton pencilButton;
        [SerializeField] private TactileKeypadButton hintButton;

        [Header("Ergonomics & Layout")]
        [SerializeField] private float buttonSize = 0.046f;
        [SerializeField] private float spacing = 0.007f;
        [SerializeField] private float arcCurvature = 0.03f; // Slight curve for ergonomic reach

        public event Action<int> OnNumberSelected;
        public event Action OnEraseRequested;
        public event Action OnUndoRequested;
        public event Action<bool> OnPencilToggled;
        public event Action OnHintRequested;

        private bool _isPencilModeActive;
        public bool IsPencilModeActive => _isPencilModeActive;

        private void Awake()
        {
            RegisterButtonCallbacks();
        }

        private void RegisterButtonCallbacks()
        {
            for (int i = 0; i < digitButtons.Length; i++)
            {
                if (digitButtons[i] != null)
                {
                    int digit = i + 1;
                    digitButtons[i].OnButtonPressed -= HandleDigitPressed;
                    digitButtons[i].OnButtonPressed += HandleDigitPressed;
                }
            }

            if (undoButton != null)
            {
                undoButton.OnButtonPressed -= HandleUndoPressed;
                undoButton.OnButtonPressed += HandleUndoPressed;
            }

            if (eraseButton != null)
            {
                eraseButton.OnButtonPressed -= HandleErasePressed;
                eraseButton.OnButtonPressed += HandleErasePressed;
            }

            if (pencilButton != null)
            {
                pencilButton.OnButtonPressed -= HandlePencilPressed;
                pencilButton.OnButtonPressed += HandlePencilPressed;
            }

            if (hintButton != null)
            {
                hintButton.OnButtonPressed -= HandleHintPressed;
                hintButton.OnButtonPressed += HandleHintPressed;
            }
        }

        private void HandleDigitPressed(int digit)
        {
            OnNumberSelected?.Invoke(digit);
        }

        private void HandleUndoPressed(int val)
        {
            OnUndoRequested?.Invoke();
        }

        private void HandleErasePressed(int val)
        {
            OnEraseRequested?.Invoke();
        }

        private void HandlePencilPressed(int val)
        {
            _isPencilModeActive = !_isPencilModeActive;
            pencilButton?.SetToggleState(_isPencilModeActive);
            OnPencilToggled?.Invoke(_isPencilModeActive);
        }

        private void HandleHintPressed(int val)
        {
            OnHintRequested?.Invoke();
        }

        public void SetPencilMode(bool active)
        {
            _isPencilModeActive = active;
            pencilButton?.SetToggleState(active);
        }

        /// <summary>
        /// Updates the remaining count badges below each keypad token (0 to 9 remaining).
        /// Extinguishes tokens when all 9 are placed on the board.
        /// </summary>
        public void UpdateRemainingCounts(SudokuBoardState boardState)
        {
            if (boardState == null || !boardState.IsInitialized) return;

            for (int d = 1; d <= 9; d++)
            {
                int placed = boardState.CountDigitPlaced(d);
                int remaining = Mathf.Max(0, 9 - placed);

                if (digitButtons[d - 1] != null)
                {
                    digitButtons[d - 1].SetRemainingCount(remaining);
                }
            }
        }

        /// <summary>
        /// Procedurally constructs or aligns the floating ergonomic layout.
        /// </summary>
        public void BuildProceduralKeypad()
        {
            float pitch = buttonSize + spacing;

            // 1. Action Ribbon above digits (Undo, Erase, Pencil, Hint)
            float ribbonZ = pitch * 1.5f;
            float ribbonStartX = -1.5f * (buttonSize * 1.2f + spacing);

            if (undoButton == null) undoButton = CreateProceduralButton("Btn_Undo", -1, "UNDO", null);
            undoButton.transform.localPosition = new Vector3(ribbonStartX, 0.005f, ribbonZ);
            undoButton.transform.localScale = new Vector3(buttonSize * 1.1f, 0.012f, buttonSize * 0.85f);

            if (eraseButton == null) eraseButton = CreateProceduralButton("Btn_Erase", 0, "ERASE", null);
            eraseButton.transform.localPosition = new Vector3(ribbonStartX + (buttonSize * 1.2f + spacing), 0.005f, ribbonZ);
            eraseButton.transform.localScale = new Vector3(buttonSize * 1.1f, 0.012f, buttonSize * 0.85f);

            if (pencilButton == null) pencilButton = CreateProceduralButton("Btn_Pencil", -2, "NOTE", null);
            pencilButton.transform.localPosition = new Vector3(ribbonStartX + 2 * (buttonSize * 1.2f + spacing), 0.005f, ribbonZ);
            pencilButton.transform.localScale = new Vector3(buttonSize * 1.1f, 0.012f, buttonSize * 0.85f);

            if (hintButton == null) hintButton = CreateProceduralButton("Btn_Hint", -3, "HINT", null);
            hintButton.transform.localPosition = new Vector3(ribbonStartX + 3 * (buttonSize * 1.2f + spacing), 0.005f, ribbonZ);
            hintButton.transform.localScale = new Vector3(buttonSize * 1.1f, 0.012f, buttonSize * 0.85f);

            // 2. Numeric Digits 1-9 in horizontal curved strip or 3x3
            // Horizontal ergonomic arc (9 tokens in an accessible concave curve)
            float startX = -4f * pitch;
            for (int i = 0; i < 9; i++)
            {
                int digit = i + 1;
                float x = startX + i * pitch;
                // Parabolic curve: outer tokens slightly closer to player
                float z = -Mathf.Pow(i - 4, 2) * arcCurvature * 0.15f;

                if (digitButtons[i] == null)
                {
                    digitButtons[i] = CreateProceduralButton($"Btn_Digit_{digit}", digit, digit.ToString(), "9");
                }

                digitButtons[i].transform.localPosition = new Vector3(x, 0.005f, z);
                digitButtons[i].transform.localRotation = Quaternion.Euler(0f, (i - 4) * -3f, 0f);
                digitButtons[i].transform.localScale = new Vector3(buttonSize, 0.012f, buttonSize);
            }

            RegisterButtonCallbacks();
        }

        private TactileKeypadButton CreateProceduralButton(string name, int val, string lbl, string badge)
        {
            GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnObj.name = name;
            btnObj.transform.SetParent(transform, false);

            TactileKeypadButton btn = btnObj.AddComponent<TactileKeypadButton>();

            // Main Label
            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, 0.51f, (badge != null) ? 0.12f : 0f);
            txtObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            txtObj.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

            TextMeshPro tmp = txtObj.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = (lbl.Length > 2) ? 22f : 32f;
            tmp.text = lbl;
            tmp.color = Color.white;

            // Optional Badge below label
            if (badge != null)
            {
                GameObject badgeObj = new GameObject("Badge");
                badgeObj.transform.SetParent(btnObj.transform, false);
                badgeObj.transform.localPosition = new Vector3(0f, 0.51f, -0.28f);
                badgeObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                badgeObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

                TextMeshPro badgeTmp = badgeObj.AddComponent<TextMeshPro>();
                badgeTmp.alignment = TextAlignmentOptions.Center;
                badgeTmp.fontSize = 18f;
                badgeTmp.text = badge;
                badgeTmp.color = new Color(0.6f, 0.8f, 1.0f, 0.9f);
            }

            btn.Setup(val, lbl);
            return btn;
        }
    }
}
