using System;
using TMPro;
using UnityEngine;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Interactive 3D spatial button supporting direct poke and ray interaction.
    /// Provides smooth tactile depression animation without GC allocations.
    /// </summary>
    public class SpatialNumpadButton : MonoBehaviour
    {
        [Header("Button Configuration")]
        [SerializeField] private int value = 1; // 1-9 for digits, 0 for erase
        [SerializeField] private string labelText = "1";
        [SerializeField] private TMP_Text textComponent;
        [SerializeField] private MeshRenderer buttonRenderer;

        [Header("Tactile Animation")]
        [SerializeField] private float pressDepth = 0.006f; // 6mm physical depress in VR
        [SerializeField] private float returnSpeed = 15f;

        [Header("Colors (URP)")]
        [SerializeField] private Color normalColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.35f, 0.48f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.0f, 0.7f, 1.0f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _propBlock;
        private Vector3 _initialLocalPos;
        private Vector3 _targetLocalPos;
        private bool _isHovered;
        private bool _isPressed;

        public int Value => value;
        public event Action<int> OnButtonPressed;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            _initialLocalPos = transform.localPosition;
            _targetLocalPos = _initialLocalPos;

            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponent<MeshRenderer>();
            }

            if (textComponent == null)
            {
                textComponent = GetComponentInChildren<TMP_Text>();
            }

            UpdateVisualColor(normalColor);
        }

        private void Update()
        {
            // Smooth tactile spring return
            if (transform.localPosition != _targetLocalPos)
            {
                transform.localPosition = Vector3.MoveTowards(
                    transform.localPosition,
                    _targetLocalPos,
                    returnSpeed * Time.deltaTime
                );

                if (transform.localPosition == _targetLocalPos && _isPressed)
                {
                    _isPressed = false;
                    _targetLocalPos = _initialLocalPos;
                    UpdateVisualColor(_isHovered ? hoverColor : normalColor);
                }
            }
        }

        public void Setup(int val, string label)
        {
            value = val;
            labelText = label;
            if (textComponent != null)
            {
                textComponent.text = label;
            }
        }

        public void TriggerPress()
        {
            _isPressed = true;
            _targetLocalPos = _initialLocalPos - transform.up * pressDepth;
            UpdateVisualColor(pressedColor);

            OnButtonPressed?.Invoke(value);
        }

        public void SetHover(bool hovered)
        {
            _isHovered = hovered;
            if (!_isPressed)
            {
                UpdateVisualColor(hovered ? hoverColor : normalColor);
            }
        }

        private void UpdateVisualColor(Color c)
        {
            if (buttonRenderer == null) return;
            buttonRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, c);
            _propBlock.SetColor(ColorId, c);
            buttonRenderer.SetPropertyBlock(_propBlock);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Direct poke trigger with VR hand or controller tip
            TriggerPress();
        }
    }
}
