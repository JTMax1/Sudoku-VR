using UnityEngine;
using UnityEngine.InputSystem;
using SudokuVR.Gameplay;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Multi-modal XR interactor supporting:
    /// 1. Direct VR Poke (colliders / fingertip triggers)
    /// 2. VR Controller Pointer / Ray interaction
    /// 3. Editor mouse fallback for zero-friction debugging
    /// GC-optimized: pre-allocated raycast hit buffers, zero tick allocations.
    /// </summary>
    public class CellInteractor : MonoBehaviour
    {
        [Header("Raycast Origins (Optional VR Controller Transforms)")]
        [SerializeField] private Transform rightControllerRayOrigin;
        [SerializeField] private Transform leftControllerRayOrigin;

        [Header("Raycast Configuration")]
        [SerializeField] private float maxRayDistance = 3.0f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("Line Visual (Optional)")]
        [SerializeField] private LineRenderer rayVisual;

        // Pre-allocated hit buffer to guarantee zero GC allocations per frame
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        private Camera _mainCamera;
        private SudokuCellView _currentHoveredCell;
        private SpatialNumpadButton _currentHoveredButton;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                _mainCamera = FindAnyObjectByType<Camera>();
            }
        }

        private void Update()
        {
            // Evaluate interactions without tick allocations
            PerformInteractionTick();
        }

        private void PerformInteractionTick()
        {
            Ray interactionRay = default;
            bool hasRay = false;
            bool triggerPressed = false;

            // 1. Check VR Controller Ray Origin if assigned
            if (rightControllerRayOrigin != null)
            {
                interactionRay = new Ray(rightControllerRayOrigin.position, rightControllerRayOrigin.forward);
                hasRay = true;

                // Check Meta XR / OpenXR trigger state
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
                    OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
                {
                    triggerPressed = true;
                }
            }

            // 2. Fallback to Mouse Ray in Unity Editor / Desktop
            if (!hasRay || Application.isEditor)
            {
                if (Mouse.current != null && _mainCamera != null)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    interactionRay = _mainCamera.ScreenPointToRay(mousePos);
                    hasRay = true;

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        triggerPressed = true;
                    }
                }
            }

            if (!hasRay) return;

            // Perform non-allocating raycast
            int hitCount = Physics.RaycastNonAlloc(interactionRay, _hitBuffer, maxRayDistance, interactableLayer);

            SudokuCellView hitCell = null;
            SpatialNumpadButton hitButton = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (hit.distance < closestDist)
                {
                    SudokuCellView cell = hit.collider.GetComponentInParent<SudokuCellView>();
                    SpatialNumpadButton button = hit.collider.GetComponentInParent<SpatialNumpadButton>();

                    if (cell != null || button != null)
                    {
                        closestDist = hit.distance;
                        hitCell = cell;
                        hitButton = button;
                    }
                }
            }

            // Update cell hover states
            if (hitCell != _currentHoveredCell)
            {
                if (_currentHoveredCell != null)
                {
                    _currentHoveredCell.TriggerUnhover();
                }
                _currentHoveredCell = hitCell;
                if (_currentHoveredCell != null)
                {
                    _currentHoveredCell.TriggerHover();
                }
            }

            // Update button hover states
            if (hitButton != _currentHoveredButton)
            {
                if (_currentHoveredButton != null)
                {
                    _currentHoveredButton.SetHover(false);
                }
                _currentHoveredButton = hitButton;
                if (_currentHoveredButton != null)
                {
                    _currentHoveredButton.SetHover(true);
                }
            }

            // Trigger click
            if (triggerPressed)
            {
                if (_currentHoveredCell != null)
                {
                    _currentHoveredCell.TriggerClick();
                }
                else if (_currentHoveredButton != null)
                {
                    _currentHoveredButton.TriggerPress();
                }
            }

            // Update optional ray visual
            if (rayVisual != null)
            {
                rayVisual.enabled = true;
                rayVisual.SetPosition(0, interactionRay.origin);
                Vector3 endPos = (closestDist < float.MaxValue)
                    ? interactionRay.origin + interactionRay.direction * closestDist
                    : interactionRay.origin + interactionRay.direction * maxRayDistance;
                rayVisual.SetPosition(1, endPos);
            }
        }
    }
}
