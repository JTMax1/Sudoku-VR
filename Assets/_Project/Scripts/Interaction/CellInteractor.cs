using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SudokuVR.Gameplay;
using SudokuVR.UI;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Multi-modal XR interactor for Meta Quest 3 (Horizon OS).
    /// Enforces clean, auto-detected mutually exclusive interaction modes:
    /// 1. Controllers: Dual laser rays with controller triggers when controllers are held.
    /// 2. Direct Index Fingertip Poke: Automatically activates in close proximity (<0.32m).
    ///    Mutes laser pointers and enables tactile 3D touch using the real INDEX finger (never ring).
    /// 3. Ray Point & Click: Automatically activates at distance (>=0.35m).
    ///    Displays laser line + round reticle and uses index pinch to click.
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

        [Header("Proximity Poke Thresholds")]
        [SerializeField] private float pokeEnterDistance = 0.32f; // Distance to board to enter poke mode
        [SerializeField] private float pokeExitDistance = 0.38f;  // Distance to board to exit poke mode

        [Header("Laser Colors")]
        [SerializeField] private Color rayNormalColor = new Color(0f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color rayHoverColor = new Color(0f, 1f, 0.75f, 0.85f);
        [SerializeField] private Color rayClickColor = new Color(1f, 0.9f, 0.2f, 1f);

        [Header("Pointers (Auto-created if null)")]
        [SerializeField] private LineRenderer rightRayVisual;
        [SerializeField] private GameObject rightPointerReticle;
        [SerializeField] private LineRenderer leftRayVisual;
        [SerializeField] private GameObject leftPointerReticle;

        [Header("Poke Tip Visuals (Index Fingertips)")]
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
        private Transform _boardMonolithTransform;

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

        // Modality State per hand (Hysteresis state tracking)
        private bool _isRightInPokeMode;
        private bool _isLeftInPokeMode;

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
            FindBoardMonolith();
        }

        private void FindBoardMonolith()
        {
            if (_boardMonolithTransform == null)
            {
                var board = GameObject.Find("SpatialBoard_Monolith");
                if (board != null) _boardMonolithTransform = board.transform;
            }
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
            if (_rightHand == null || _leftHand == null)
            {
                AutoBindRigTransforms();
            }
            if (_boardMonolithTransform == null)
            {
                FindBoardMonolith();
            }

            // Process Right Hand/Controller
            ProcessHandInteraction(true);

            // Process Left Hand/Controller
            ProcessHandInteraction(false);

            // Editor Mouse Fallback
            if (Application.isEditor)
            {
                ProcessEditorMouseFallback();
            }
        }

        private void ProcessHandInteraction(bool isRight)
        {
            OVRHand hand = isRight ? _rightHand : _leftHand;
            Transform ctrlOrigin = isRight ? rightControllerRayOrigin : leftControllerRayOrigin;
            OVRInput.Controller ctrl = isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;

            bool isControllerActive = OVRInput.IsControllerConnected(ctrl) &&
                                     (hand == null || !hand.IsTracked);

            // -------------------------------------------------------------
            // MODE 1: CONTROLLERS
            // -------------------------------------------------------------
            if (isControllerActive)
            {
                // In controller mode: Poking is disabled; Ray pointer is active
                GameObject orb = isRight ? rightPokeOrb : leftPokeOrb;
                if (orb != null && orb.activeSelf) orb.SetActive(false);

                ProcessControllerPointer(isRight);
                return;
            }

            // -------------------------------------------------------------
            // TRACKED HANDS: AUTO-DETECT DIRECT POKE VS RAY POINT & CLICK
            // -------------------------------------------------------------
            if (hand != null && hand.IsTracked)
            {
                bool hasIndexTip = TryGetIndexTipPosition(isRight, out Vector3 indexTipPos);

                // Compute distance to interactable board/keypad
                float distToBoard = float.MaxValue;
                if (hasIndexTip && _boardMonolithTransform != null)
                {
                    distToBoard = Vector3.Distance(indexTipPos, _boardMonolithTransform.position);
                }

                // Hysteresis for smooth mode switching
                ref bool inPokeMode = ref (isRight ? ref _isRightInPokeMode : ref _isLeftInPokeMode);
                if (inPokeMode)
                {
                    if (distToBoard > pokeExitDistance) inPokeMode = false;
                }
                else
                {
                    if (distToBoard < pokeEnterDistance) inPokeMode = true;
                }

                if (inPokeMode && hasIndexTip)
                {
                    // ---------------------------------------------------------
                    // MODE 2: DIRECT INDEX FINGERTIP POKE (Close proximity)
                    // ---------------------------------------------------------
                    // 1. Hide laser ray and reticle
                    LineRenderer line = isRight ? rightRayVisual : leftRayVisual;
                    GameObject reticle = isRight ? rightPointerReticle : leftPointerReticle;
                    if (line != null && line.enabled) line.enabled = false;
                    if (reticle != null && reticle.activeSelf) reticle.SetActive(false);
                    ClearHovers(isRight);

                    // 2. Show index fingertip poke orb
                    GameObject orb = isRight ? rightPokeOrb : leftPokeOrb;
                    if (orb != null)
                    {
                        orb.SetActive(true);
                        orb.transform.position = indexTipPos;
                    }

                    // 3. Process direct physical touch
                    ExecuteIndexTipOverlapPoke(isRight, indexTipPos);
                }
                else
                {
                    // ---------------------------------------------------------
                    // MODE 3: RAY POINT & CLICK (Distance)
                    // ---------------------------------------------------------
                    // 1. Hide poke orb
                    GameObject orb = isRight ? rightPokeOrb : leftPokeOrb;
                    if (orb != null && orb.activeSelf) orb.SetActive(false);

                    // 2. Process laser ray pointing and pinch click
                    ProcessHandRayPointer(isRight, hand);
                }
                return;
            }

            // Hand not tracked & no controller: hide visuals
            HideAllVisualsForHand(isRight);
        }

        #region Direct Index Fingertip Poke

        private void ExecuteIndexTipOverlapPoke(bool isRight, Vector3 tipPos)
        {
            float lastPokeTime = isRight ? _lastPokeTimeRight : _lastPokeTimeLeft;
            if (Time.time - lastPokeTime < 0.22f) return;

            int hitCount = Physics.OverlapSphereNonAlloc(tipPos, 0.024f, _pokeOverlapBuffer, interactableLayer);
            if (hitCount == 0) return;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _pokeOverlapBuffer[i];
                if (col == null) continue;

                // 1. 3D Sudoku Tile
                Tile3DView tile = col.GetComponentInParent<Tile3DView>();
                if (tile != null)
                {
                    tile.TriggerClick();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.45f, 0.12f);
                    return;
                }

                // 2. Tactile Keypad Button
                TactileKeypadButton key = col.GetComponentInParent<TactileKeypadButton>();
                if (key != null)
                {
                    key.TriggerPress();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.45f, 0.12f);
                    return;
                }

                // 3. Spatial UI Canvas Button (HUD, Pause, Modals)
                Button uiBtn = col.GetComponentInParent<Button>();
                if (uiBtn != null && uiBtn.interactable)
                {
                    uiBtn.onClick.Invoke();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.5f, 0.14f);
                    return;
                }

                // 4. Diorama Level Node (Levels 1 to 5)
                DioramaLevelNode dNode = col.GetComponentInParent<DioramaLevelNode>();
                if (dNode != null)
                {
                    dNode.TriggerNodeClick();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.45f, 0.12f);
                    return;
                }

                // 5. Summit Epic Chest
                DioramaEpicChestTrigger chestTrig = col.GetComponentInParent<DioramaEpicChestTrigger>();
                if (chestTrig != null)
                {
                    chestTrig.OnPointerClick();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.5f, 0.15f);
                    return;
                }

                // 6. Companion Switcher Pedestal
                CompanionSwitcherTrigger compTrig = col.GetComponentInParent<CompanionSwitcherTrigger>();
                if (compTrig != null)
                {
                    compTrig.OnPointerClick();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.4f, 0.1f);
                    return;
                }

                // 7. Board Theme Switcher Dial
                ThemeSwitcherTrigger themeTrig = col.GetComponentInParent<ThemeSwitcherTrigger>();
                if (themeTrig != null)
                {
                    themeTrig.OnPointerClick();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.4f, 0.1f);
                    return;
                }

                // 8. Title Screen Enter Button
                TitleEnterTrigger titleTrig = col.GetComponentInParent<TitleEnterTrigger>();
                if (titleTrig != null)
                {
                    titleTrig.GetComponentInParent<TitleScreen3DController>()?.HandleEnterPressed();
                    if (isRight) _lastPokeTimeRight = Time.time;
                    else _lastPokeTimeLeft = Time.time;
                    TriggerHapticPulse(isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch, 0.5f, 0.15f);
                    return;
                }

                // 9. Legacy Button / Cell
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

            // 1. Strictly identify the INDEX FINGERTIP bone from skeleton
            if (hand != null && hand.IsTracked && skeleton != null && skeleton.IsInitialized)
            {
                if (tipBone == null)
                {
                    foreach (var bone in skeleton.Bones)
                    {
                        if (bone.Transform == null) continue;
                        string bName = bone.Transform.name;

                        // Check name contains index and tip/distal/3
                        bool nameHasIndex = bName.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool nameHasTip = bName.IndexOf("tip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          bName.IndexOf("distal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          bName.IndexOf("3", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (nameHasIndex && nameHasTip)
                        {
                            tipBone = bone.Transform;
                            break;
                        }

                        // In Meta XR SDK (XRHandLeft / XRHandRight), XRHand_IndexTip == 10
                        // (Crucial: Bone 20 is XRHand_RingTip, so we NEVER match 20!)
                        if ((int)bone.Id == 10)
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

            // 2. Hand tracking fallback (project forward from pointer pose)
            if (hand != null && hand.IsTracked)
            {
                Transform pointer = hand.PointerPose != null ? hand.PointerPose : (isRight ? rightHandRayOrigin : leftHandRayOrigin);
                if (pointer != null)
                {
                    tipPos = pointer.position + pointer.forward * 0.04f;
                    return true;
                }
            }

            tipPos = Vector3.zero;
            return false;
        }

        #endregion

        #region Ray Pointers

        private void ProcessHandRayPointer(bool isRight, OVRHand hand)
        {
            Transform originTransform = hand.PointerPose != null ? hand.PointerPose : (isRight ? rightHandRayOrigin : leftHandRayOrigin);
            if (originTransform == null)
            {
                HideAllVisualsForHand(isRight);
                return;
            }

            Ray ray = new Ray(originTransform.position, originTransform.forward);

            bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index) ||
                             hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.60f;

            bool triggerClicked = false;
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

            CastAndResolveRay(isRight, ray, triggerClicked, isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);
        }

        private void ProcessControllerPointer(bool isRight)
        {
            Transform ctrlOrigin = isRight ? rightControllerRayOrigin : leftControllerRayOrigin;
            if (ctrlOrigin == null)
            {
                HideAllVisualsForHand(isRight);
                return;
            }

            Ray ray = new Ray(ctrlOrigin.position, ctrlOrigin.forward);
            OVRInput.Controller ctrl = isRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
            OVRInput.Button btnPrimary = isRight ? OVRInput.Button.One : OVRInput.Button.Three;

            bool btnDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ctrl) ||
                           OVRInput.GetDown(btnPrimary, ctrl);
            float triggerAxis = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ctrl);

            bool triggerClicked = false;
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

            CastAndResolveRay(isRight, ray, triggerClicked, ctrl);
        }

        private void CastAndResolveRay(bool isRight, Ray ray, bool triggerClicked, OVRInput.Controller ctrl)
        {
            RaycastHit[] hitBuffer = isRight ? _rightHitBuffer : _leftHitBuffer;
            int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, maxRayDistance, interactableLayer);

            Tile3DView hitTile = null;
            TactileKeypadButton hitKey = null;
            Button hitUIButton = null;
            SpatialNumpadButton hitLegacyBtn = null;
            SudokuCellView hitLegacyCell = null;
            float closestDist = float.MaxValue;

            DioramaLevelNode hitDioramaNode = null;
            DioramaEpicChestTrigger hitChest = null;
            CompanionSwitcherTrigger hitCompSwitcher = null;
            ThemeSwitcherTrigger hitThemeSwitcher = null;
            TitleEnterTrigger hitTitleBtn = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];
                if (hit.distance < closestDist)
                {
                    Tile3DView t = hit.collider.GetComponentInParent<Tile3DView>();
                    TactileKeypadButton k = hit.collider.GetComponentInParent<TactileKeypadButton>();
                    Button u = hit.collider.GetComponentInParent<Button>();
                    DioramaLevelNode dn = hit.collider.GetComponentInParent<DioramaLevelNode>();
                    DioramaEpicChestTrigger ct = hit.collider.GetComponentInParent<DioramaEpicChestTrigger>();
                    CompanionSwitcherTrigger cs = hit.collider.GetComponentInParent<CompanionSwitcherTrigger>();
                    ThemeSwitcherTrigger ts = hit.collider.GetComponentInParent<ThemeSwitcherTrigger>();
                    TitleEnterTrigger te = hit.collider.GetComponentInParent<TitleEnterTrigger>();
                    SpatialNumpadButton lb = hit.collider.GetComponentInParent<SpatialNumpadButton>();
                    SudokuCellView lc = hit.collider.GetComponentInParent<SudokuCellView>();

                    if (t != null || k != null || u != null || dn != null || ct != null || cs != null || ts != null || te != null || lb != null || lc != null)
                    {
                        closestDist = hit.distance;
                        hitTile = t;
                        hitKey = k;
                        hitUIButton = u;
                        hitDioramaNode = dn;
                        hitChest = ct;
                        hitCompSwitcher = cs;
                        hitThemeSwitcher = ts;
                        hitTitleBtn = te;
                        hitLegacyBtn = lb;
                        hitLegacyCell = lc;
                    }
                }
            }

            UpdateHandHovers(isRight, hitTile, hitKey, hitUIButton, hitLegacyBtn, hitLegacyCell);

            if (triggerClicked)
            {
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
                else if (hitDioramaNode != null)
                {
                    hitDioramaNode.TriggerNodeClick();
                    TriggerHapticPulse(ctrl, 0.45f, 0.12f);
                }
                else if (hitChest != null)
                {
                    hitChest.OnPointerClick();
                    TriggerHapticPulse(ctrl, 0.5f, 0.15f);
                }
                else if (hitCompSwitcher != null)
                {
                    hitCompSwitcher.OnPointerClick();
                    TriggerHapticPulse(ctrl, 0.4f, 0.1f);
                }
                else if (hitThemeSwitcher != null)
                {
                    hitThemeSwitcher.OnPointerClick();
                    TriggerHapticPulse(ctrl, 0.4f, 0.1f);
                }
                else if (hitTitleBtn != null)
                {
                    hitTitleBtn.GetComponentInParent<TitleScreen3DController>()?.HandleEnterPressed();
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

            LineRenderer line = isRight ? rightRayVisual : leftRayVisual;
            GameObject reticle = isRight ? rightPointerReticle : leftPointerReticle;
            Material reticleMat = isRight ? _rightReticleMat : _leftReticleMat;

            bool hasHover = (hitTile != null || hitKey != null || hitUIButton != null || hitDioramaNode != null || hitChest != null || hitCompSwitcher != null || hitThemeSwitcher != null || hitTitleBtn != null || hitLegacyBtn != null || hitLegacyCell != null);
            UpdatePointerVisuals(line, reticle, reticleMat, ray, closestDist, hasHover, triggerClicked);
        }

        private void HideAllVisualsForHand(bool isRight)
        {
            LineRenderer line = isRight ? rightRayVisual : leftRayVisual;
            GameObject reticle = isRight ? rightPointerReticle : leftPointerReticle;
            GameObject orb = isRight ? rightPokeOrb : leftPokeOrb;

            if (line != null) line.enabled = false;
            if (reticle != null) reticle.SetActive(false);
            if (orb != null) orb.SetActive(false);
            ClearHovers(isRight);
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
