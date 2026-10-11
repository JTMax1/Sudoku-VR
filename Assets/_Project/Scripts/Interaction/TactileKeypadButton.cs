using System;
using TMPro;
using UnityEngine;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Tactile 3D physical button with mechanical depression, remaining count badge,
    /// and Meta Quest haptic pulse feedback on contact.
    /// </summary>
    public class TactileKeypadButton : MonoBehaviour
    {
        [Header("Button Identification")]
        [SerializeField] private int value = 1; // 1-9 for digits, negative for action keys
        [SerializeField] private string label = "1";
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text remainingCountBadge;
        [SerializeField] private MeshRenderer buttonRenderer;

        [Header("Tactile Ergonomics")]
        [SerializeField] private float pressDepth = 0.007f; // 7mm tactile depression
        [SerializeField] private float returnSpeed = 16f;

        [Header("Colors (URP)")]
        [SerializeField] private Color normalColor = new Color(0.14f, 0.17f, 0.23f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.22f, 0.32f, 0.45f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.0f, 0.75f, 1.0f, 1f);
        [SerializeField] private Color activeToggleColor = new Color(0.12f, 0.82f, 0.42f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.08f, 0.09f, 0.11f, 0.4f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _propBlock;
        private Vector3 _initialLocalPos;
        private Vector3 _targetLocalPos;
        private bool _isHovered;
        private bool _isPressed;
        private bool _isToggledOn;
        private bool _isExtinguished;

        public int Value => value;
        public bool IsExtinguished => _isExtinguished;
        public event Action<int> OnButtonPressed;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            _initialLocalPos = transform.localPosition;
            _targetLocalPos = _initialLocalPos;

            if (buttonRenderer == null) buttonRenderer = GetComponent<MeshRenderer>();
            if (labelText == null) labelText = GetComponentInChildren<TMP_Text>();

            UpdateVisuals();
        }

        private void Update()
        {
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
                    UpdateVisuals();
                }
            }
        }

        public void Setup(int val, string lbl)
        {
            value = val;
            label = lbl;
            if (labelText != null) labelText.text = lbl;
        }

        public void SetRemainingCount(int remaining)
        {
            if (remainingCountBadge != null)
            {
                remainingCountBadge.gameObject.SetActive(remaining > 0);
                remainingCountBadge.text = remaining.ToString();
            }

            SetExtinguished(remaining <= 0);
        }

        public void SetExtinguished(bool extinguished)
        {
            _isExtinguished = extinguished;
            UpdateVisuals();
        }

        public void SetToggleState(bool toggled)
        {
            _isToggledOn = toggled;
            UpdateVisuals();
        }

        private float _lastPokeTime;

        public void TriggerPress()
        {
            if (_isExtinguished) return;

            _isPressed = true;
            _targetLocalPos = _initialLocalPos + transform.forward * pressDepth;
            UpdateVisuals();

            // Trigger Meta Quest controller haptic pulse
            TriggerHaptics();

            OnButtonPressed?.Invoke(value);
        }

        public void SetHover(bool hovered)
        {
            if (_isExtinguished) return;
            _isHovered = hovered;
            if (!_isPressed)
            {
                UpdateVisuals();
            }
        }

        private void TriggerHaptics()
        {
            try
            {
                OVRInput.SetControllerVibration(0.4f, 0.15f, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(0.4f, 0.15f, OVRInput.Controller.LTouch);
            }
            catch
            {
                // Standalone fallback
            }
        }

        private void UpdateVisuals()
        {
            if (buttonRenderer == null) return;

            Color targetColor;
            Color targetEmission = Color.black;

            if (_isExtinguished)
            {
                targetColor = disabledColor;
            }
            else if (_isPressed)
            {
                targetColor = pressedColor;
                targetEmission = pressedColor * 0.4f;
            }
            else if (_isToggledOn)
            {
                targetColor = activeToggleColor;
                targetEmission = activeToggleColor * 0.3f;
            }
            else if (_isHovered)
            {
                targetColor = hoverColor;
            }
            else
            {
                targetColor = normalColor;
            }

            buttonRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, targetColor);
            _propBlock.SetColor(ColorId, targetColor);
            _propBlock.SetColor(EmissionColorId, targetEmission);
            buttonRenderer.SetPropertyBlock(_propBlock);

            if (labelText != null)
            {
                labelText.color = _isExtinguished ? new Color(0.4f, 0.4f, 0.4f, 0.4f) : Color.white;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time - _lastPokeTime < 0.25f) return;
            _lastPokeTime = Time.time;
            TriggerPress();
        }
    }
}
