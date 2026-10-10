using UnityEngine;
using UnityEngine.InputSystem;
using SudokuVR.Gameplay;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Multi-modal XR interactor supporting:
    /// 1. Direct VR Poke (colliders / fingertip triggers)
    /// 2. VR Controller Pointer / Ray interaction (Meta Quest Touch Plus)
    /// 3. Editor mouse fallback for zero-friction debugging
    /// GC-optimized: pre-allocated raycast hit buffers, zero tick allocations.
    /// </summary>
    public class CellInteractor : MonoBehaviour
    {
        [Header("Raycast Origins (Optional VR Controller Transforms)")]
        [SerializeField] private Transform rightControllerRayOrigin;
        [SerializeField] private Transform leftControllerRayOrigin;

        [Header("Raycast Configuration")]
        [SerializeField] private float maxRayDistance = 3.5f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("Line Visual (Optional)")]
        [SerializeField] private LineRenderer rayVisual;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[12];

        private Camera _mainCamera;
        private Tile3DView _currentHoveredTile;
        private TactileKeypadButton _currentHoveredKey;
        private SudokuCellView _legacyHoveredCell;
        private SpatialNumpadButton _legacyHoveredButton;

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

                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
                    OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
                {
                    triggerPressed = true;
                }
            }

            // 2. Fallback to Mouse Ray in Unity Editor / Desktop
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

                        if (Mouse.current.leftButton.wasPressedThisFrame)
                        {
                            triggerPressed = true;
                        }
                    }
                }
            }

            if (!hasRay) return;

            int hitCount = Physics.RaycastNonAlloc(interactionRay, _hitBuffer, maxRayDistance, interactableLayer);

            Tile3DView hitTile = null;
            TactileKeypadButton hitKey = null;
            SudokuCellView legacyCell = null;
            SpatialNumpadButton legacyBtn = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (hit.distance < closestDist)
                {
                    Tile3DView tile = hit.collider.GetComponentInParent<Tile3DView>();
                    TactileKeypadButton key = hit.collider.GetComponentInParent<TactileKeypadButton>();
                    SudokuCellView c = hit.collider.GetComponentInParent<SudokuCellView>();
                    SpatialNumpadButton b = hit.collider.GetComponentInParent<SpatialNumpadButton>();

                    if (tile != null || key != null || c != null || b != null)
                    {
                        closestDist = hit.distance;
                        hitTile = tile;
                        hitKey = key;
                        legacyCell = c;
                        legacyBtn = b;
                    }
                }
            }

            // Tile Hover
            if (hitTile != _currentHoveredTile)
            {
                _currentHoveredTile?.TriggerUnhover();
                _currentHoveredTile = hitTile;
                _currentHoveredTile?.TriggerHover();
            }

            // Key Hover
            if (hitKey != _currentHoveredKey)
            {
                _currentHoveredKey?.SetHover(false);
                _currentHoveredKey = hitKey;
                _currentHoveredKey?.SetHover(true);
            }

            // Legacy Fallbacks
            if (legacyCell != _legacyHoveredCell)
            {
                _legacyHoveredCell?.TriggerUnhover();
                _legacyHoveredCell = legacyCell;
                _legacyHoveredCell?.TriggerHover();
            }
            if (legacyBtn != _legacyHoveredButton)
            {
                _legacyHoveredButton?.SetHover(false);
                _legacyHoveredButton = legacyBtn;
                _legacyHoveredButton?.SetHover(true);
            }

            // Trigger Clicks
            if (triggerPressed)
            {
                if (_currentHoveredTile != null) _currentHoveredTile.TriggerClick();
                else if (_currentHoveredKey != null) _currentHoveredKey.TriggerPress();
                else if (_legacyHoveredCell != null) _legacyHoveredCell.TriggerClick();
                else if (_legacyHoveredButton != null) _legacyHoveredButton.TriggerPress();
            }

            // Line Visual
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
