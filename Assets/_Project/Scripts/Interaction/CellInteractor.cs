using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SudokuVR.Gameplay;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Multi-modal XR interactor for Meta Quest 3 (Horizon OS).
    /// Provides independent, simultaneous dual-hand interaction:
    /// 1. Dual Ray Point & Click (Independent Right & Left hands/controllers with custom reticles)
    /// 2. Dual Direct Index Fingertip Poking (Physical contact on 3D tiles, tactile keys, and UI buttons)
    /// 3. Haptic feedback pulses per-hand
    /// 4. Editor Mouse & Keyboard fallback
    /// Zero garbage collection allocations in update loops.
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

        [Header("Laser Colors")]
        [SerializeField] private Color rayNormalColor = new Color(0f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color rayHoverColor = new Color(0f, 1f, 0.75f, 0.85f);
        [SerializeField] private Color rayClickColor = new Color(1f, 0.9f, 0.2f, 1f);

        // Ray Visuals per hand
        [Header("Pointers (Auto-created if null)")]
        [SerializeField] private LineRenderer rightRayVisual;
        [SerializeField] private GameObject rightPointerReticle;
        [SerializeField] private LineRenderer leftRayVisual;
        [SerializeField] private GameObject leftPointerReticle;

        // Fingertip Poke visuals per hand
        [Header("Poke Tip Visuals")]
        [SerializeField] private GameObject rightPokeOrb;
        [SerializeField] private GameObject leftPokeOrb;

        private Camera _mainCamera;
        private OVRCameraRig _cameraRig;
        private OVRHand _rightHand;
        private OVRHand _leftHand;
        private OVRSkeleton _rightSkeleton;
        private OVRSkeleton _leftSkeleton;
        private Transform _rightIndexTipBone;
        private Transform _leftIndexTipBone;

        private Material _rightReticleMat;
        private Material _leftReticleMat;

        // Hit Buffers (preallocated zero GC)
        private readonly RaycastHit[] _rightHitBuffer = new RaycastHit[16];
        private readonly RaycastHit[] _leftHitBuffer = new RaycastHit[16];
        private readonly Collider[] _pokeOverlapBuffer = new Collider[16];

        // Hover States Right Hand
        private Tile3DView _rightHoveredTile;
        private TactileKeypadButton _rightHoveredKey;
        private Button _rightHoveredUIButton;
        private SpatialNumpadButton _rightHoveredLegacyBtn;
        private SudokuCellView _rightHoveredLegacyCell;

        // Hover States Left Hand
        private Tile3DView _leftHoveredTile;
        private TactileKeypadButton _leftHoveredKey;
        private Button _leftHoveredUIButton;
        private SpatialNumpadButton _leftHoveredLegacyBtn;
        private SudokuCellView _leftHoveredLegacyCell;

        // Previous Input States
        private bool _wasPinchingRight;
        private bool _wasPinchingLeft;
        private bool _wasRightTriggerDown;
        private bool _wasLeftTriggerDown;

        // Poke Cooldowns
        private float _lastPokeTimeRight;
        private float _lastPokeTimeLeft;

        private void Awake()
        {
            _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            _cameraRig = FindAnyObjectByType<OVRCameraRig>();

            AutoBindRigTransforms();
            EnsureLaserVisuals();
            EnsurePokeOrbVisuals();
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
                    rightControllerRayOrigin = _cameraRig.rightControllerAnchor;
                if (leftControllerRayOrigin == null && _cameraRig.leftControllerAnchor != null)
                    leftControllerRayOrigin = _cameraRig.leftControllerAnchor;
                if (rightHandRayOrigin == null && _cameraRig.rightHandAnchor != null)
                    rightHandRayOrigin = _cameraRig.rightHandAnchor;
                if (leftHandRayOrigin == null && _cameraRig.leftHandAnchor != null)
                    leftHandRayOrigin = _cameraRig.leftHandAnchor;

                if (_rightHand == null && _cameraRig.rightHandAnchor != null)
                {
                    _rightHand = _cameraRig.rightHandAnchor.GetComponentInChildren<OVRHand>();
                    _rightSkeleton = _cameraRig.rightHandAnchor.GetComponentInChildren<OVRSkeleton>();
                }
                if (_leftHand == null && _cameraRig.leftHandAnchor != null)
                {
                    _leftHand = _cameraRig.leftHandAnchor.GetComponentInChildren<OVRHand>();
                    _leftSkeleton = _cameraRig.leftHandAnchor.GetComponentInChildren<OVRSkeleton>();
                }
            }
        }

        private void Update()
        {
            // Re-bind if hands initialized late
            if (_rightHand == null || _leftHand == null)
            {
                AutoBindRigTransforms();
            }

            // 1. Process Direct Index Fingertip Poking (Both Hands)
            ProcessDirectFingertipPoke(true);  // Right Hand Poke
            ProcessDirectFingertipPoke(false); // Left Hand Poke

            // 2. Process Independent Raycast Pointers (Both Hands)
            ProcessHandPointer(true);  // Right Hand Pointer
            ProcessHandPointer(false); // Left Hand Pointer

            // 3. Editor Mouse Fallback
            if (Application.isEditor)
            {
                ProcessEditorMouseFallback();
            }
        }

        #region Direct Fingertip Poking

        private void ProcessDirectFingertipPoke(bool isRight)
        {
            if (!TryGetIndexTipPosition(isRight, out Vector3 tipPos))
            {
                GameObject pokeOrb = isRight ? rightPokeOrb : leftPokeOrb;
                if (pokeOrb != null && pokeOrb.activeSelf) pokeOrb.SetActive(false);
                return;
            }

            GameObject orb = isRight ? rightPokeOrb : leftPokeOrb;
            if (orb != null)
            {
                orb.SetActive(true);
                orb.transform.position = tipPos;
            }

            float lastPokeTime = isRight ? _lastPokeTimeRight : _lastPokeTimeLeft;
            if (Time.time - lastPokeTime < 0.22f) return;

            int hitCount = Physics.OverlapSphereNonAlloc(tipPos, 0.024f, _pokeOverlapBuffer, interactableLayer);
            if (hitCount == 0) return;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _pokeOverlapBuffer[i];
                if (col == null) continue;

                // 3D Tile
                Tile3DView tile = col.GetComponentInParent<Tile3DView>();
                if (tile != null)
                {
                    tile.TriggerClick();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.45f, 0.12f);
                    return;
                }

                // Tactile Keypad Button
                TactileKeypadButton key = col.GetComponentInParent<TactileKeypadButton>();
                if (key != null)
                {
                    key.TriggerPress();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.45f, 0.12f);
                    return;
                }

                // Spatial UI Canvas Button
                Button uiBtn = col.GetComponentInParent<Button>();
                if (uiBtn != null && uiBtn.interactable)
                {
                    uiBtn.onClick.Invoke();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.5f, 0.14f);
                    return;
                }

                // Legacy Button & Cell
                SpatialNumpadButton legBtn = col.GetComponentInParent<SpatialNumpadButton>();
                if (legBtn != null)
                {
                    legBtn.TriggerPress();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    return;
                }

                SudokuCellView legCell = col.GetComponentInParent<SudokuCellView>();
                if (legCell != null)
                {
                    legCell.TriggerClick();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    return;
                }
            }
        }

        private bool TryGetIndexTipPosition(bool isRight, out Vector3 tipPos)
        {
            OVRHand hand = isRight ? _rightHand : _leftHand;
            OVRSkeleton skeleton = isRight ? _rightSkeleton : _leftSkeleton;
            ref Transform tipBone = ref (isRight ? ref _rightIndexTipBone : ref _leftIndexTipBone);
            Transform ctrlOrigin = isRight ? rightControllerRayOrigin : leftControllerRayOrigin;

            // 1. Hand tracking with initialized skeleton
            if (hand != null && hand.IsTracked && skeleton != null && skeleton.IsInitialized)
            {
                if (tipBone == null)
                {
                    foreach (var bone in skeleton.Bones)
                    {
                        if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                        {
                            tipBone = bone.Transform;
                            break;
                        }
                    }
                }

                if (tipBone != null)
                {
                    tipPos = tipBone.position;
                    return true;
                }
            }

            // 2. Hand tracking fallback (pointer pose projected forward)
            if (hand != null && hand.IsTracked)
            {
                Transform pointer = hand.PointerPose != null ? hand.PointerPose : (isRight ? rightHandRayOrigin : leftHandRayOrigin);
                if (pointer != null)
                {
                    tipPos = pointer.position + pointer.forward * 0.04f;
                    return true;
                }
            }

            // 3. Controller fallback tip
            if (ctrlOrigin != null && OVRInput.IsControllerConnected(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch))
            {
                tipPos = ctrlOrigin.TransformPoint(new Vector3(isRight ? 0.012f : -0.012f, -0.015f, 0.055f));
                return true;
            }

            tipPos = Vector3.zero;
            return false;
        }

        #endregion

        #region Ray Pointers

        private void ProcessHandPointer(bool isRight)
        {
            Ray ray = default;
            bool hasRay = false;
            bool triggerClicked = false;
            Transform originTransform = null;

            OVRHand hand = isRight ? _rightHand : _leftHand;
            Transform handOrigin = isRight ? rightHandRayOrigin : leftHandRayOrigin;
            Transform ctrlOrigin = isRight ? rightControllerRayOrigin : leftControllerRayOrigin;

            // Evaluate Hand Tracking
            if (hand != null && hand.IsTracked)
            {
                originTransform = hand.PointerPose != null ? hand.PointerPose : handOrigin;
                if (originTransform != null)
                {
                    ray = new Ray(originTransform.position, originTransform.forward);
                    hasRay = true;

                    bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index) ||
                                     hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.60f;

                    if (isRight)
                    {
                        if (isPinching && !_wasPinchingRight) triggerClicked = true;
                        _wasPinchingRight = isPinching;
                    }
                    else
                    {
                        if (isPinching && !_wasPinchingLeft) triggerClicked = true;
                        _wasPinchingLeft = isPinching;
                    }
                }
            }
            // Evaluate Touch Controller
            else if (ctrlOrigin != null)
            {
                originTransform = ctrlOrigin;
                ray = new Ray(ctrlOrigin.position, ctrlOrigin.forward);
                hasRay = true;

                OVRInput.Controller ctrl = isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
                OVRInput.Button btnPrimary = isRight ? OVRInput.Button.One : OVRInput.Button.Three;

                bool btnDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ctrl) ||
                               OVRInput.GetDown(btnPrimary, ctrl);
                float triggerAxis = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ctrl);

                if (isRight)
                {
                    bool axisDown = triggerAxis > 0.60f && !_wasRightTriggerDown;
                    _wasRightTriggerDown = triggerAxis > 0.60f;
                    triggerClicked = btnDown || axisDown;
                }
                else
                {
                    bool axisDown = triggerAxis > 0.60f && !_wasLeftTriggerDown;
                    _wasLeftTriggerDown = triggerAxis > 0.60f;
                    triggerClicked = btnDown || axisDown;
                }
            }

            LineRenderer line = isRight ? rightRayVisual : leftRayVisual;
            GameObject reticle = isRight ? rightPointerReticle : leftPointerReticle;
            Material reticleMat = isRight ? _rightReticleMat : _leftReticleMat;

            if (!hasRay)
            {
                if (line != null) line.enabled = false;
                if (reticle != null) reticle.SetActive(false);
                ClearHovers(isRight);
                return;
            }

            // Raycast execution
            RaycastHit[] hitBuffer = isRight ? _rightHitBuffer : _leftHitBuffer;
            int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, maxRayDistance, interactableLayer);

            Tile3DView hitTile = null;
            TactileKeypadButton hitKey = null;
            Button hitUIButton = null;
            SpatialNumpadButton hitLegacyBtn = null;
            SudokuCellView hitLegacyCell = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];
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

            // Update Hand Hovers
            UpdateHandHovers(isRight, hitTile, hitKey, hitUIButton, hitLegacyBtn, hitLegacyCell);

            // Execute Click
            if (triggerClicked)
            {
                OVRInput.Controller ctrl = isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;

                if (hitTile != null)
                {
                    hitTile.TriggerClick();
                    TriggerHapticPulse(ctrl, 0.35f, 0.12f);
                }
                else if (hitKey != null)
                {
                    hitKey.TriggerPress();
                }
                else if (hitUIButton != null && hitUIButton.interactable)
                {
                    hitUIButton.onClick.Invoke();
                    TriggerHapticPulse(ctrl, 0.5f, 0.15f);
                }
                else if (hitLegacyBtn != null)
                {
                    hitLegacyBtn.TriggerPress();
                }
                else if (hitLegacyCell != null)
                {
                    hitLegacyCell.TriggerClick();
                }
            }

            // Update Laser Visuals & Reticle
            bool hasHover = (hitTile != null || hitKey != null || hitUIButton != null || hitLegacyBtn != null || hitLegacyCell != null);
            UpdatePointerVisuals(line, reticle, reticleMat, ray, closestDist, hasHover, triggerClicked);
        }

        private void UpdateHandHovers(bool isRight, Tile3DView tile, TactileKeypadButton key, Button uiBtn, SpatialNumpadButton legBtn, SudokuCellView legCell)
        {
            if (isRight)
            {
                if (tile != _rightHoveredTile)
                {
                    _rightHoveredTile?.TriggerUnhover();
                    _rightHoveredTile = tile;
                    _rightHoveredTile?.TriggerHover();
                }
                if (key != _rightHoveredKey)
                {
                    _rightHoveredKey?.SetHover(false);
                    _rightHoveredKey = key;
                    _rightHoveredKey?.SetHover(true);
                }
                _rightHoveredUIButton = uiBtn;
                if (legBtn != _rightHoveredLegacyBtn)
                {
                    _rightHoveredLegacyBtn?.SetHover(false);
                    _rightHoveredLegacyBtn = legBtn;
                    _rightHoveredLegacyBtn?.SetHover(true);
                }
                if (legCell != _rightHoveredLegacyCell)
                {
                    _rightHoveredLegacyCell?.TriggerUnhover();
                    _rightHoveredLegacyCell = legCell;
                    _rightHoveredLegacyCell?.TriggerHover();
                }
            }
            else
            {
                if (tile != _leftHoveredTile)
                {
                    _leftHoveredTile?.TriggerUnhover();
                    _leftHoveredTile = tile;
                    _leftHoveredTile?.TriggerHover();
                }
                if (key != _leftHoveredKey)
                {
                    _leftHoveredKey?.SetHover(false);
                    _leftHoveredKey = key;
                    _leftHoveredKey?.SetHover(true);
                }
                _leftHoveredUIButton = uiBtn;
                if (legBtn != _leftHoveredLegacyBtn)
                {
                    _leftHoveredLegacyBtn?.SetHover(false);
                    _leftHoveredLegacyBtn = legBtn;
                    _leftHoveredLegacyBtn?.SetHover(true);
                }
                if (legCell != _leftHoveredLegacyCell)
                {
                    _leftHoveredLegacyCell?.TriggerUnhover();
                    _leftHoveredLegacyCell = legCell;
                    _leftHoveredLegacyCell?.TriggerHover();
                }
            }
        }

        private void ClearHovers(bool isRight)
        {
            if (isRight)
            {
                _rightHoveredTile?.TriggerUnhover();
                _rightHoveredTile = null;
                _rightHoveredKey?.SetHover(false);
                _rightHoveredKey = null;
                _rightHoveredUIButton = null;
                _rightHoveredLegacyBtn?.SetHover(false);
                _rightHoveredLegacyBtn = null;
                _rightHoveredLegacyCell?.TriggerUnhover();
                _rightHoveredLegacyCell = null;
            }
            else
            {
                _leftHoveredTile?.TriggerUnhover();
                _leftHoveredTile = null;
                _leftHoveredKey?.SetHover(false);
                _leftHoveredKey = null;
                _leftHoveredUIButton = null;
                _leftHoveredLegacyBtn?.SetHover(false);
                _leftHoveredLegacyBtn = null;
                _leftHoveredLegacyCell?.TriggerUnhover();
                _leftHoveredLegacyCell = null;
            }
        }

        private void UpdatePointerVisuals(LineRenderer line, GameObject reticle, Material reticleMat, Ray ray, float hitDist, bool hasHover, bool isClicking)
        {
            if (line == null) return;

            line.enabled = true;
            line.SetPosition(0, ray.origin);

            float length = (hitDist < float.MaxValue) ? hitDist : 2.5f;
            Vector3 end = ray.origin + ray.direction * length;
            line.SetPosition(1, end);

            Color color = isClicking ? rayClickColor : (hasHover ? rayHoverColor : rayNormalColor);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.05f);

            // Update Round Reticle
            if (reticle != null)
            {
                reticle.SetActive(true);
                reticle.transform.position = end - ray.direction * 0.003f;
                reticle.transform.rotation = Quaternion.LookRotation(ray.direction);

                float scaleMultiplier = isClicking ? 0.75f : (hasHover ? 1.4f : 1.0f);
                reticle.transform.localScale = Vector3.one * (0.018f * scaleMultiplier);

                if (reticleMat != null)
                {
                    reticleMat.color = color;
                }
            }
        }

        #endregion

        #region Editor Fallback

        private void ProcessEditorMouseFallback()
        {
            // If VR hands/controllers are actively pointing, let them drive
            if ((_rightHand != null && _rightHand.IsTracked) || (_leftHand != null && _leftHand.IsTracked)) return;

            if (Mouse.current != null)
            {
                if (_mainCamera == null) _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
                if (_mainCamera != null)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    Ray ray = _mainCamera.ScreenPointToRay(mousePos);

                    int hitCount = Physics.RaycastNonAlloc(ray, _rightHitBuffer, maxRayDistance, interactableLayer);
                    Tile3DView hitTile = null;
                    TactileKeypadButton hitKey = null;
                    Button hitUIButton = null;

                    for (int i = 0; i < hitCount; i++)
                    {
                        var hit = _rightHitBuffer[i];
                        var t = hit.collider.GetComponentInParent<Tile3DView>();
                        var k = hit.collider.GetComponentInParent<TactileKeypadButton>();
                        var u = hit.collider.GetComponentInParent<Button>();
                        if (t != null) hitTile = t;
                        if (k != null) hitKey = k;
                        if (u != null) hitUIButton = u;
                    }

                    if (hitTile != _rightHoveredTile)
                    {
                        _rightHoveredTile?.TriggerUnhover();
                        _rightHoveredTile = hitTile;
                        _rightHoveredTile?.TriggerHover();
                    }
                    if (hitKey != _rightHoveredKey)
                    {
                        _rightHoveredKey?.SetHover(false);
                        _rightHoveredKey = hitKey;
                        _rightHoveredKey?.SetHover(true);
                    }
                    _rightHoveredUIButton = hitUIButton;

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (hitTile != null) hitTile.TriggerClick();
                        else if (hitKey != null) hitKey.TriggerPress();
                        else if (hitUIButton != null && hitUIButton.interactable) hitUIButton.onClick.Invoke();
                    }
                }
            }
        }

        #endregion

        private void TriggerHapticPulse(OVRInput.Controller ctrl, float amplitude, float duration)
        {
            try
            {
                OVRInput.SetControllerVibration(amplitude, duration, ctrl);
            }
            catch
            {
                // Ignored in editor
            }
        }

        private void EnsureLaserVisuals()
        {
            // Right Pointer
            if (rightRayVisual == null)
            {
                GameObject laserObj = new GameObject("VR_LaserPointer_Right");
                laserObj.transform.SetParent(transform, false);
                rightRayVisual = CreateLineRenderer(laserObj);
            }

            if (rightPointerReticle == null)
            {
                rightPointerReticle = CreateReticleObject("VR_PointerReticle_Right", out _rightReticleMat);
            }

            // Left Pointer
            if (leftRayVisual == null)
            {
                GameObject laserObj = new GameObject("VR_LaserPointer_Left");
                laserObj.transform.SetParent(transform, false);
                leftRayVisual = CreateLineRenderer(laserObj);
            }

            if (leftPointerReticle == null)
            {
                leftPointerReticle = CreateReticleObject("VR_PointerReticle_Left", out _leftReticleMat);
            }
        }

        private void EnsurePokeOrbVisuals()
        {
            if (rightPokeOrb == null)
            {
                rightPokeOrb = CreatePokeOrb("VR_PokeOrb_Right");
            }
            if (leftPokeOrb == null)
            {
                leftPokeOrb = CreatePokeOrb("VR_PokeOrb_Left");
            }
        }

        private LineRenderer CreateLineRenderer(GameObject go)
        {
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = 0.005f;
            lr.endWidth = 0.002f;

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = new Material(s);
            mat.color = rayNormalColor;
            lr.material = mat;
            lr.enabled = false;
            return lr;
        }

        private GameObject CreateReticleObject(string name, out Material reticleMat)
        {
            GameObject reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reticle.name = name;
            reticle.transform.SetParent(transform, false);
            reticle.transform.localScale = Vector3.one * 0.018f;

            var col = reticle.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            reticleMat = new Material(s);
            reticleMat.color = rayNormalColor;
            var mr = reticle.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = reticleMat;

            reticle.SetActive(false);
            return reticle;
        }

        private GameObject CreatePokeOrb(string name)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = name;
            orb.transform.SetParent(transform, false);
            orb.transform.localScale = Vector3.one * 0.014f;

            var col = orb.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = new Material(s);
            mat.color = new Color(0f, 0.85f, 1f, 0.6f);
            var mr = orb.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;

            orb.SetActive(false);
            return orb;
        }
    }
}
