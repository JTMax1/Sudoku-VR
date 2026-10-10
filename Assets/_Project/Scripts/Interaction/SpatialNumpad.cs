using System;
using TMPro;
using UnityEngine;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Curved or angled spatial 3D number pad.
    /// Provides comfortable ergonomic access to digits 1-9, Erase, and Clear in VR.
    /// </summary>
    public class SpatialNumpad : MonoBehaviour
    {
        [Header("Button References")]
        [SerializeField] private SpatialNumpadButton[] digitButtons = new SpatialNumpadButton[9];
        [SerializeField] private SpatialNumpadButton eraseButton;
        [SerializeField] private SpatialNumpadButton clearButton;

        [Header("Ergonomic Layout Settings")]
        [SerializeField] private float buttonSize = 0.048f;
        [SerializeField] private float buttonSpacing = 0.008f;

        public event Action<int> OnNumberSelected;
        public event Action OnEraseRequested;
        public event Action OnClearRequested;

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

            if (eraseButton != null)
            {
                eraseButton.OnButtonPressed -= HandleErasePressed;
                eraseButton.OnButtonPressed += HandleErasePressed;
            }

            if (clearButton != null)
            {
                clearButton.OnButtonPressed -= HandleClearPressed;
                clearButton.OnButtonPressed += HandleClearPressed;
            }
        }

        private void HandleDigitPressed(int digit)
        {
            OnNumberSelected?.Invoke(digit);
        }

        private void HandleErasePressed(int val)
        {
            OnEraseRequested?.Invoke();
        }

        private void HandleClearPressed(int val)
        {
            OnClearRequested?.Invoke();
        }

        /// <summary>
        /// Procedurally builds or arranges the spatial numpad layout if not already pre-wired.
        /// </summary>
        public void BuildProceduralLayout()
        {
            float pitch = buttonSize + buttonSpacing;

            // 3x3 layout for digits 1-9:
            // Row 0: 1 2 3
            // Row 1: 4 5 6
            // Row 2: 7 8 9
            // Row 3: Clear (X) | Erase (Bksp)
            for (int r = 0; r < 3; r++)
            {
                float z = (1 - r) * pitch;

                for (int c = 0; c < 3; c++)
                {
                    int idx = r * 3 + c;
                    int digit = idx + 1;
                    float x = (c - 1) * pitch;

                    if (digitButtons[idx] == null)
                    {
                        Transform existing = transform.Find($"Btn_{digit}");
                        if (existing != null)
                        {
                            digitButtons[idx] = existing.GetComponent<SpatialNumpadButton>();
                        }
                        if (digitButtons[idx] == null)
                        {
                            digitButtons[idx] = CreateProceduralButton($"Btn_{digit}", digit, digit.ToString());
                        }
                    }

                    digitButtons[idx].transform.localPosition = new Vector3(x, 0.005f, z);
                    digitButtons[idx].transform.localRotation = Quaternion.identity;
                    digitButtons[idx].transform.localScale = new Vector3(buttonSize, 0.012f, buttonSize);
                }
            }

            // Action row below 7,8,9
            float actionZ = -2 * pitch;
            if (clearButton == null)
            {
                Transform existing = transform.Find("Btn_Clear");
                if (existing != null) clearButton = existing.GetComponent<SpatialNumpadButton>();
                if (clearButton == null) clearButton = CreateProceduralButton("Btn_Clear", -1, "CLR");
            }
            clearButton.transform.localPosition = new Vector3(-pitch * 0.75f, 0.005f, actionZ);
            clearButton.transform.localRotation = Quaternion.identity;
            clearButton.transform.localScale = new Vector3(buttonSize * 1.3f, 0.012f, buttonSize);

            if (eraseButton == null)
            {
                Transform existing = transform.Find("Btn_Erase");
                if (existing != null) eraseButton = existing.GetComponent<SpatialNumpadButton>();
                if (eraseButton == null) eraseButton = CreateProceduralButton("Btn_Erase", 0, "DEL");
            }
            eraseButton.transform.localPosition = new Vector3(pitch * 0.75f, 0.005f, actionZ);
            eraseButton.transform.localRotation = Quaternion.identity;
            eraseButton.transform.localScale = new Vector3(buttonSize * 1.3f, 0.012f, buttonSize);

            RegisterButtonCallbacks();
        }

        private SpatialNumpadButton CreateProceduralButton(string name, int val, string label)
        {
            GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnObj.name = name;
            btnObj.transform.SetParent(transform, false);

            SpatialNumpadButton btn = btnObj.AddComponent<SpatialNumpadButton>();

            // Label
            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, 0.51f, 0f);
            txtObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            txtObj.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

            TextMeshPro tmp = txtObj.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 32f;
            tmp.text = label;
            tmp.color = Color.white;

            btn.Setup(val, label);
            return btn;
        }
    }
}
