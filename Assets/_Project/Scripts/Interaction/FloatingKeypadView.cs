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

            // Optional Acrylic Backplate for keypad
            Transform backplate = transform.Find("Keypad_Backplate");
            if (backplate == null)
            {
                GameObject bpObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bpObj.name = "Keypad_Backplate";
                bpObj.transform.SetParent(transform, false);
                var col = bpObj.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
                var rend = bpObj.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                    if (s != null)
                    {
                        var mat = new Material(s);
                        mat.SetColor("_BaseColor", new Color(0.08f, 0.10f, 0.15f, 0.95f));
                        rend.sharedMaterial = mat;
                    }
                }
                backplate = bpObj.transform;
            }
            backplate.localPosition = new Vector3(0f, 0.012f, 0.008f);
            backplate.localRotation = Quaternion.identity;
            backplate.localScale = new Vector3(0.52f, 0.13f, 0.012f);

            // 1. Action Ribbon above digits (Undo, Erase, Pencil, Hint)
            float ribbonPitch = buttonSize * 1.25f + spacing;
            float ribbonStartX = -1.5f * ribbonPitch;
            float ribbonY = 0.040f;

            if (undoButton == null) undoButton = transform.Find("Btn_Undo")?.GetComponent<TactileKeypadButton>();
            if (undoButton == null) undoButton = CreateProceduralButton("Btn_Undo", -1, "UNDO", null);
            undoButton.transform.localPosition = new Vector3(ribbonStartX, ribbonY, -0.006f);
            undoButton.transform.localRotation = Quaternion.identity;
            undoButton.transform.localScale = new Vector3(buttonSize * 1.15f, 0.034f, 0.014f);
            RealignButtonVisuals(undoButton, false);

            if (eraseButton == null) eraseButton = transform.Find("Btn_Erase")?.GetComponent<TactileKeypadButton>();
            if (eraseButton == null) eraseButton = CreateProceduralButton("Btn_Erase", 0, "ERASE", null);
            eraseButton.transform.localPosition = new Vector3(ribbonStartX + ribbonPitch, ribbonY, -0.006f);
            eraseButton.transform.localRotation = Quaternion.identity;
            eraseButton.transform.localScale = new Vector3(buttonSize * 1.15f, 0.034f, 0.014f);
            RealignButtonVisuals(eraseButton, false);

            if (pencilButton == null) pencilButton = transform.Find("Btn_Pencil")?.GetComponent<TactileKeypadButton>();
            if (pencilButton == null) pencilButton = CreateProceduralButton("Btn_Pencil", -2, "NOTE", null);
            pencilButton.transform.localPosition = new Vector3(ribbonStartX + 2 * ribbonPitch, ribbonY, -0.006f);
            pencilButton.transform.localRotation = Quaternion.identity;
            pencilButton.transform.localScale = new Vector3(buttonSize * 1.15f, 0.034f, 0.014f);
            RealignButtonVisuals(pencilButton, false);

            if (hintButton == null) hintButton = transform.Find("Btn_Hint")?.GetComponent<TactileKeypadButton>();
            if (hintButton == null) hintButton = CreateProceduralButton("Btn_Hint", -3, "HINT", null);
            hintButton.transform.localPosition = new Vector3(ribbonStartX + 3 * ribbonPitch, ribbonY, -0.006f);
            hintButton.transform.localRotation = Quaternion.identity;
            hintButton.transform.localScale = new Vector3(buttonSize * 1.15f, 0.034f, 0.014f);
            RealignButtonVisuals(hintButton, false);

            // 2. Numeric Digits 1-9 in horizontal strip directly facing player
            float startX = -4f * pitch;
            float digitsY = -0.020f;
            for (int i = 0; i < 9; i++)
            {
                int digit = i + 1;
                float x = startX + i * pitch;

                if (digitButtons[i] == null)
                {
                    digitButtons[i] = transform.Find($"Btn_Digit_{digit}")?.GetComponent<TactileKeypadButton>();
                }
                if (digitButtons[i] == null)
                {
                    digitButtons[i] = CreateProceduralButton($"Btn_Digit_{digit}", digit, digit.ToString(), "9");
                }

                digitButtons[i].transform.localPosition = new Vector3(x, digitsY, -0.006f);
                digitButtons[i].transform.localRotation = Quaternion.identity;
                digitButtons[i].transform.localScale = new Vector3(buttonSize, buttonSize, 0.014f);
                RealignButtonVisuals(digitButtons[i], true);
            }

            RegisterButtonCallbacks();
        }

        private void RealignButtonVisuals(TactileKeypadButton btn, bool hasBadge)
        {
            if (btn == null) return;
            Transform lbl = btn.transform.Find("Label");
            if (lbl != null)
            {
                lbl.localPosition = new Vector3(0f, hasBadge ? 0.12f : 0f, -0.52f);
                lbl.localRotation = Quaternion.identity;
            }
            Transform bdg = btn.transform.Find("Badge");
            if (bdg != null)
            {
                bdg.localPosition = new Vector3(0f, -0.28f, -0.52f);
                bdg.localRotation = Quaternion.identity;
            }
        }

        private TactileKeypadButton CreateProceduralButton(string name, int val, string lbl, string badge)
        {
            GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnObj.name = name;
            btnObj.transform.SetParent(transform, false);

            var rend = btnObj.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s != null)
                {
                    var mat = new Material(s);
                    mat.SetColor("_BaseColor", new Color(0.16f, 0.20f, 0.28f, 1f));
                    rend.sharedMaterial = mat;
                }
            }

            TactileKeypadButton btn = btnObj.AddComponent<TactileKeypadButton>();

            // Main Label on front face facing player
            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, (badge != null) ? 0.12f : 0f, -0.52f);
            txtObj.transform.localRotation = Quaternion.identity;
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
                badgeObj.transform.localPosition = new Vector3(0f, -0.28f, -0.52f);
                badgeObj.transform.localRotation = Quaternion.identity;
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
