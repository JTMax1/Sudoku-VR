using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SudokuVR.Gameplay;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Comprehensive multi-modal XR interactor for Meta Quest 3 (Horizon OS).
    /// Supports:
    /// 1. Meta Quest Touch Controllers (Ray pointing + Trigger click + Haptic pulses)
    /// 2. Tracked Hands (Pinch-to-click + Hand Pointer Pose)
    /// 3. Direct 3D tile and tactile keypad button interactions
    /// 4. World-space Canvas UI button interactions (MainMenu, Modals, Battle, Passport)
    /// 5. Editor Mouse & Keyboard fallback
    /// Zero garbage collection allocations in tick loops.
    /// </summary>
    public class CellInteractor : MonoBehaviour
    {
        [Header("Raycast Origins (Auto-detected if null)")]
        [SerializeField] private Transform rightControllerRayOrigin;
        [SerializeField] private Transform leftControllerRayOrigin;
        [SerializeField] private Transform rightHandRayOrigin;
        [SerializeField] private Transform leftHandRayOrigin;

        [Header("Raycast Configuration")]
        [SerializeField] private float maxRayDistance = 4.0f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("Laser Visuals")]
        [SerializeField] private LineRenderer rayVisual;
        [SerializeField] private Color rayNormalColor = new Color(0f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color rayHoverColor = new Color(0f, 1f, 0.75f, 0.85f);
        [SerializeField] private Color rayClickColor = new Color(1f, 0.9f, 0.2f, 1f);

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        private Camera _mainCamera;
        private OVRCameraRig _cameraRig;
        private OVRHand _rightHand;
        private OVRHand _leftHand;

        private Tile3DView _currentHoveredTile;
        private TactileKeypadButton _currentHoveredKey;
        private Button _currentHoveredUIButton;
        private SpatialNumpadButton _currentHoveredLegacyBtn;
        private SudokuCellView _currentHoveredLegacyCell;

        private bool _wasPinchingRight;
        private bool _wasPinchingLeft;

        private void Awake()
        {
            _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            _cameraRig = FindAnyObjectByType<OVRCameraRig>();

            AutoBindRigTransforms();
            EnsureLaserVisual();
        }

        private void Start()
        {
            AutoBindRigTransforms();
        }

        private void AutoBindRigTransforms()
        {
            if (_cameraRig == null) _cameraRig = FindAnyObjectByType<OVRCameraRig>();
            if (_cameraRig != null)
            {
                if (rightControllerRayOrigin == null && _cameraRig.rightControllerAnchor != null)
                {
                    rightControllerRayOrigin = _cameraRig.rightControllerAnchor;
                }
                if (leftControllerRayOrigin == null && _cameraRig.leftControllerAnchor != null)
                {
                    leftControllerRayOrigin = _cameraRig.leftControllerAnchor;
                }
                if (rightHandRayOrigin == null && _cameraRig.rightHandAnchor != null)
                {
                    rightHandRayOrigin = _cameraRig.rightHandAnchor;
                }
                if (leftHandRayOrigin == null && _cameraRig.leftHandAnchor != null)
                {
                    leftHandRayOrigin = _cameraRig.leftHandAnchor;
                }

                _rightHand = _cameraRig.rightHandAnchor?.GetComponentInChildren<OVRHand>();
                _leftHand = _cameraRig.leftHandAnchor?.GetComponentInChildren<OVRHand>();
            }
        }

        private void Update()
        {
            PerformInteractionTick();
        }

        private void PerformInteractionTick()
        {
            Ray interactionRay = default;
            bool hasRay = false;
            bool triggerPressed = false;
            Transform activeOrigin = null;

            // 1. Check Hand Tracking Pinch & Pointer
            if (_rightHand != null && _rightHand.IsTracked)
            {
                Transform pointer = _rightHand.PointerPose != null ? _rightHand.PointerPose : rightHandRayOrigin;
                if (pointer != null)
                {
                    interactionRay = new Ray(pointer.position, pointer.forward);
                    hasRay = true;
                    activeOrigin = pointer;

                    bool isPinching = _rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                    if (isPinching && !_wasPinchingRight)
                    {
                        triggerPressed = true;
                    }
                    _wasPinchingRight = isPinching;
                }
            }
            else if (_leftHand != null && _leftHand.IsTracked)
            {
                Transform pointer = _leftHand.PointerPose != null ? _leftHand.PointerPose : leftHandRayOrigin;
                if (pointer != null)
                {
                    interactionRay = new Ray(pointer.position, pointer.forward);
                    hasRay = true;
                    activeOrigin = pointer;

                    bool isPinching = _leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                    if (isPinching && !_wasPinchingLeft)
                    {
                        triggerPressed = true;
                    }
                    _wasPinchingLeft = isPinching;
                }
            }

            // 2. Check Meta Quest Touch Controller
            if (!hasRay)
            {
                if (rightControllerRayOrigin != null)
                {
                    interactionRay = new Ray(rightControllerRayOrigin.position, rightControllerRayOrigin.forward);
                    hasRay = true;
                    activeOrigin = rightControllerRayOrigin;

                    // Trigger detection: index trigger click or Button One (A button)
                    if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
                        OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                        OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > 0.65f)
                    {
                        triggerPressed = true;
                    }
                }
                else if (leftControllerRayOrigin != null)
                {
                    interactionRay = new Ray(leftControllerRayOrigin.position, leftControllerRayOrigin.forward);
                    hasRay = true;
                    activeOrigin = leftControllerRayOrigin;

                    if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) ||
                        OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch))
                    {
                        triggerPressed = true;
                    }
                }
            }

            // 3. Fallback to Screen / Mouse in Unity Editor
            if (!hasRay || Application.isEditor)
            {
                if (Mouse.current != null)
                {
                    if (_mainCamera == null) _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
                    if (_mainCamera != null)
                    {
                        Vector2 mousePos = Mouse.current.position.ReadValue();
                        interactionRay = _mainCamera.ScreenPointToRay(mousePos);
                        hasRay = true;
                        activeOrigin = _mainCamera.transform;

                        if (Mouse.current.leftButton.wasPressedThisFrame)
                        {
                            triggerPressed = true;
                        }
                    }
                }
            }

            if (!hasRay)
            {
                if (rayVisual != null) rayVisual.enabled = false;
                return;
            }

            // Perform Raycast
            int hitCount = Physics.RaycastNonAlloc(interactionRay, _hitBuffer, maxRayDistance, interactableLayer);

            Tile3DView hitTile = null;
            TactileKeypadButton hitKey = null;
            Button hitUIButton = null;
            SpatialNumpadButton hitLegacyBtn = null;
            SudokuCellView hitLegacyCell = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (hit.distance < closestDist)
                {
                    Tile3DView t = hit.collider.GetComponentInParent<Tile3DView>();
                    TactileKeypadButton k = hit.collider.GetComponentInParent<TactileKeypadButton>();
                    Button u = hit.collider.GetComponentInParent<Button>();
                    SpatialNumpadButton lb = hit.collider.GetComponentInParent<SpatialNumpadButton>();
                    SudokuCellView lc = hit.collider.GetComponentInParent<SudokuCellView>();

                    if (t != null || k != null || u != null || lb != null || lc != null)
                    {
                        closestDist = hit.distance;
                        hitTile = t;
                        hitKey = k;
                        hitUIButton = u;
                        hitLegacyBtn = lb;
                        hitLegacyCell = lc;
                    }
                }
            }

            // Update Tile Hover
            if (hitTile != _currentHoveredTile)
            {
                _currentHoveredTile?.TriggerUnhover();
                _currentHoveredTile = hitTile;
                _currentHoveredTile?.TriggerHover();
            }

            // Update Key Hover
            if (hitKey != _currentHoveredKey)
            {
                _currentHoveredKey?.SetHover(false);
                _currentHoveredKey = hitKey;
                _currentHoveredKey?.SetHover(true);
            }

            // Update UI Button Hover
            if (hitUIButton != _currentHoveredUIButton)
            {
                _currentHoveredUIButton = hitUIButton;
            }

            // Update Legacy Hovers
            if (hitLegacyBtn != _currentHoveredLegacyBtn)
            {
                _currentHoveredLegacyBtn?.SetHover(false);
                _currentHoveredLegacyBtn = hitLegacyBtn;
                _currentHoveredLegacyBtn?.SetHover(true);
            }
            if (hitLegacyCell != _currentHoveredLegacyCell)
            {
                _currentHoveredLegacyCell?.TriggerUnhover();
                _currentHoveredLegacyCell = hitLegacyCell;
                _currentHoveredLegacyCell?.TriggerHover();
            }

            // Process Trigger Click
            if (triggerPressed)
            {
                if (_currentHoveredTile != null)
                {
                    _currentHoveredTile.TriggerClick();
                    TriggerHapticPulse(0.35f, 0.12f);
                }
                else if (_currentHoveredKey != null)
                {
                    _currentHoveredKey.TriggerPress();
                }
                else if (_currentHoveredUIButton != null && _currentHoveredUIButton.interactable)
                {
                    _currentHoveredUIButton.onClick.Invoke();
                    TriggerHapticPulse(0.5f, 0.15f);
                }
                else if (_currentHoveredLegacyBtn != null)
                {
                    _currentHoveredLegacyBtn.TriggerPress();
                }
                else if (_currentHoveredLegacyCell != null)
                {
                    _currentHoveredLegacyCell.TriggerClick();
                }
            }

            // Update Laser Visual
            UpdateLaserVisual(interactionRay, closestDist, activeOrigin, hitTile != null || hitKey != null || hitUIButton != null, triggerPressed);
        }

        private void UpdateLaserVisual(Ray ray, float hitDist, Transform origin, bool hasHover, bool isClicking)
        {
            if (rayVisual == null) return;

            // Don't show ray in head-cam fallback unless clicking/hovering
            if (origin == _mainCamera?.transform && !hasHover)
            {
                rayVisual.enabled = false;
                return;
            }

            rayVisual.enabled = true;
            rayVisual.SetPosition(0, ray.origin);

            float length = (hitDist < float.MaxValue) ? hitDist : 2.5f;
            Vector3 end = ray.origin + ray.direction * length;
            rayVisual.SetPosition(1, end);

            Color color = isClicking ? rayClickColor : (hasHover ? rayHoverColor : rayNormalColor);
            rayVisual.startColor = color;
            rayVisual.endColor = new Color(color.r, color.g, color.b, 0.05f);
        }

        private void TriggerHapticPulse(float amplitude, float duration)
        {
            try
            {
                OVRInput.SetControllerVibration(amplitude, duration, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(amplitude, duration, OVRInput.Controller.LTouch);
            }
            catch
            {
                // Fallback in Editor
            }
        }

        private void EnsureLaserVisual()
        {
            if (rayVisual == null)
            {
                GameObject laserObj = new GameObject("VR_LaserPointer");
                laserObj.transform.SetParent(transform, false);

                rayVisual = laserObj.AddComponent<LineRenderer>();
                rayVisual.useWorldSpace = true;
                rayVisual.positionCount = 2;
                rayVisual.startWidth = 0.005f;
                rayVisual.endWidth = 0.002f;

                Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                mat.color = rayNormalColor;
                rayVisual.material = mat;
                rayVisual.enabled = false;
            }
        }
    }
}
