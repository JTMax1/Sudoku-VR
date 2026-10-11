using System;
using UnityEngine;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Encapsulates the floating 3D monolith board in a spatial bounding frame.
    /// Provides:
    /// - Distance Ray Grab & Proximity Direct Grab for Left & Right Hands and Touch Controllers
    /// - Two-handed pinch-to-scale (clamped between 0.4m and 1.6m)
    /// - Free 6DOF repositioning and tilting in 3D space
    /// - Thumbstick push/pull distance manipulation while grabbing
    /// - Instant One-Tap Recentering (0.65m along player gaze vector at chest height)
    /// - Passthrough MR vs. Zen Minimalist Skybox/Dojo environment switching
    /// </summary>
    public class SpatialBoardAnchor : MonoBehaviour
    {
        [Header("Scale Boundaries (Meters)")]
        [SerializeField] private float minScale = 0.4f;
        [SerializeField] private float maxScale = 1.6f;
        [SerializeField] private float defaultScale = 0.85f;

        [Header("Recentering Geometry")]
        [SerializeField] private float defaultGazeDistance = 0.65f; // 65cm in front of chest
        [SerializeField] private float chestHeightOffset = -0.15f;

        [Header("Grab & Manipulation Distance")]
        [SerializeField] private float grabRadius = 0.60f; // Proximity grab range around handles/frame

        [Header("Passthrough & Environment")]
        [SerializeField] private OVRPassthroughLayer passthroughLayer;
        [SerializeField] private bool startInPassthrough = true;
        [SerializeField] private GameObject zenEnvironmentRoot;

        [Header("Bounding Frame & Handles")]
        [SerializeField] private Transform leftGrabTab;
        [SerializeField] private Transform rightGrabTab;
        [SerializeField] private Transform boundingFrameMesh;

        private Camera _mainCamera;
        private OVRCameraRig _cameraRig;
        private OVRHand _rightHand;
        private OVRHand _leftHand;

        private bool _isPassthroughActive;
        private Vector3 _baseScaleVector;

        // Grab states
        private bool _isGrabbedRight;
        private bool _isGrabbedLeft;
        private bool _isDistanceGrabRight;
        private bool _isDistanceGrabLeft;
        private Vector3 _grabOffsetRight;
        private Vector3 _grabOffsetLeft;
        private Quaternion _grabRotOffsetRight;
        private Quaternion _grabRotOffsetLeft;
        private float _currentGrabDistanceRight;
        private float _currentGrabDistanceLeft;

        private float _initialTwoHandDist;
        private float _initialScaleOnTwoHandGrab;

        public bool IsPassthroughActive => _isPassthroughActive;
        public float CurrentScaleMultiplier => transform.localScale.x / defaultScale;

        public event Action<bool> OnPassthroughToggled;
        public event Action OnBoardRecentered;

        private void Awake()
        {
            _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            _cameraRig = FindAnyObjectByType<OVRCameraRig>();

            _baseScaleVector = Vector3.one * defaultScale;
            transform.localScale = _baseScaleVector;

            if (passthroughLayer == null)
            {
                passthroughLayer = FindAnyObjectByType<OVRPassthroughLayer>();
            }

            EnsureZenEnvironmentVisuals(false);
            SetPassthroughState(startInPassthrough);
        }

        private void Start()
        {
            BindTrackingRig();
            RecenterToPlayerGaze();
        }

        private void BindTrackingRig()
        {
            if (_cameraRig == null) _cameraRig = FindAnyObjectByType<OVRCameraRig>();
            if (_cameraRig != null)
            {
                _rightHand = _cameraRig.rightHandAnchor?.GetComponentInChildren<OVRHand>();
                _leftHand = _cameraRig.leftHandAnchor?.GetComponentInChildren<OVRHand>();
            }
        }

        private void Update()
        {
            if (_rightHand == null || _leftHand == null) BindTrackingRig();

            HandleXRBoardManipulation();
            HandleControllerShortcuts();
        }

        private void HandleXRBoardManipulation()
        {
            Vector3 rightPos = Vector3.zero;
            Vector3 leftPos = Vector3.zero;
            Quaternion rightRot = Quaternion.identity;
            Quaternion leftRot = Quaternion.identity;
            Vector3 rightForward = Vector3.forward;
            Vector3 leftForward = Vector3.forward;

            bool rightGripPressed = false;
            bool leftGripPressed = false;

            // 1. Right Hand / Controller
            if (_rightHand != null && _rightHand.IsTracked)
            {
                Transform pointer = _rightHand.PointerPose != null ? _rightHand.PointerPose : _rightHand.transform;
                rightPos = pointer.position;
                rightRot = pointer.rotation;
                rightForward = pointer.forward;

                bool isMiddlePinch = _rightHand.GetFingerIsPinching(OVRHand.HandFinger.Middle) || _rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) > 0.35f;
                bool isRingGrip = _rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) > 0.35f;
                bool isPinkyGrip = _rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) > 0.35f;
                bool isIndexPinch = _rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index) || _rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.50f;

                rightGripPressed = isMiddlePinch || isRingGrip || isPinkyGrip;
                if (!rightGripPressed && isIndexPinch)
                {
                    // Pinch near handles, perimeter frame, or distance ray grab
                    float dHandleL = leftGrabTab != null ? Vector3.Distance(rightPos, leftGrabTab.position) : 999f;
                    float dHandleR = rightGrabTab != null ? Vector3.Distance(rightPos, rightGrabTab.position) : 999f;
                    if (dHandleL < 0.28f || dHandleR < 0.28f || _isGrabbedRight)
                    {
                        rightGripPressed = true;
                    }
                }
            }
            else if (_cameraRig?.rightControllerAnchor != null)
            {
                rightPos = _cameraRig.rightControllerAnchor.position;
                rightRot = _cameraRig.rightControllerAnchor.rotation;
                rightForward = _cameraRig.rightControllerAnchor.forward;

                rightGripPressed = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
                                   OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > 0.25f;
            }

            // 2. Left Hand / Controller
            if (_leftHand != null && _leftHand.IsTracked)
            {
                Transform pointer = _leftHand.PointerPose != null ? _leftHand.PointerPose : _leftHand.transform;
                leftPos = pointer.position;
                leftRot = pointer.rotation;
                leftForward = pointer.forward;

                bool isMiddlePinch = _leftHand.GetFingerIsPinching(OVRHand.HandFinger.Middle) || _leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) > 0.35f;
                bool isRingGrip = _leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) > 0.35f;
                bool isPinkyGrip = _leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) > 0.35f;
                bool isIndexPinch = _leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index) || _leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.50f;

                leftGripPressed = isMiddlePinch || isRingGrip || isPinkyGrip;
                if (!leftGripPressed && isIndexPinch)
                {
                    float dHandleL = leftGrabTab != null ? Vector3.Distance(leftPos, leftGrabTab.position) : 999f;
                    float dHandleR = rightGrabTab != null ? Vector3.Distance(leftPos, rightGrabTab.position) : 999f;
                    if (dHandleL < 0.28f || dHandleR < 0.28f || _isGrabbedLeft)
                    {
                        leftGripPressed = true;
                    }
                }
            }
            else if (_cameraRig?.leftControllerAnchor != null)
            {
                leftPos = _cameraRig.leftControllerAnchor.position;
                leftRot = _cameraRig.leftControllerAnchor.rotation;
                leftForward = _cameraRig.leftControllerAnchor.forward;

                leftGripPressed = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch) ||
                                  OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > 0.25f;
            }

            // 3. Proximity and Ray Checks
            float distRightToBoard = Vector3.Distance(rightPos, transform.position);
            float distLeftToBoard = Vector3.Distance(leftPos, transform.position);

            bool proximityRight = distRightToBoard <= (grabRadius * 1.5f * transform.localScale.x);
            bool proximityLeft = distLeftToBoard <= (grabRadius * 1.5f * transform.localScale.x);

            // Distance Ray check: Is the ray pointing near the board?
            Ray rightRay = new Ray(rightPos, rightForward);
            Ray leftRay = new Ray(leftPos, leftForward);
            bool rayRightAiming = IsRayAimingAtBoard(rightRay, out float hitDistRight);
            bool rayLeftAiming = IsRayAimingAtBoard(leftRay, out float hitDistLeft);

            // If index pinch with tracked hand at a distance, allow distance grab if aiming at frame/board
            if (_rightHand != null && _rightHand.IsTracked && !rightGripPressed)
            {
                if (rayRightAiming && (_rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index) || _rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.6f))
                {
                    rightGripPressed = true;
                }
            }
            if (_leftHand != null && _leftHand.IsTracked && !leftGripPressed)
            {
                if (rayLeftAiming && (_leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index) || _leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.6f))
                {
                    leftGripPressed = true;
                }
            }

            // RIGHT HAND GRAB TRIGGER
            if (rightGripPressed && !_isGrabbedRight)
            {
                if (proximityRight)
                {
                    _isGrabbedRight = true;
                    _isDistanceGrabRight = false;
                    _grabOffsetRight = Quaternion.Inverse(rightRot) * (transform.position - rightPos);
                    _grabRotOffsetRight = Quaternion.Inverse(rightRot) * transform.rotation;
                }
                else if (rayRightAiming)
                {
                    _isGrabbedRight = true;
                    _isDistanceGrabRight = true;
                    _currentGrabDistanceRight = hitDistRight;
                    _grabRotOffsetRight = Quaternion.Inverse(rightRot) * transform.rotation;
                }
            }
            else if (!rightGripPressed && _isGrabbedRight)
            {
                _isGrabbedRight = false;
                _isDistanceGrabRight = false;
            }

            // LEFT HAND GRAB TRIGGER
            if (leftGripPressed && !_isGrabbedLeft)
            {
                if (proximityLeft)
                {
                    _isGrabbedLeft = true;
                    _isDistanceGrabLeft = false;
                    _grabOffsetLeft = Quaternion.Inverse(leftRot) * (transform.position - leftPos);
                    _grabRotOffsetLeft = Quaternion.Inverse(leftRot) * transform.rotation;
                }
                else if (rayLeftAiming)
                {
                    _isGrabbedLeft = true;
                    _isDistanceGrabLeft = true;
                    _currentGrabDistanceLeft = hitDistLeft;
                    _grabRotOffsetLeft = Quaternion.Inverse(leftRot) * transform.rotation;
                }
            }
            else if (!leftGripPressed && _isGrabbedLeft)
            {
                _isGrabbedLeft = false;
                _isDistanceGrabLeft = false;
            }

            // 4. Two-Handed Manipulation & Scaling
            if (_isGrabbedRight && _isGrabbedLeft)
            {
                float currentDist = Vector3.Distance(rightPos, leftPos);

                if (_initialTwoHandDist <= 0.001f)
                {
                    _initialTwoHandDist = currentDist;
                    _initialScaleOnTwoHandGrab = transform.localScale.x;
                }
                else if (currentDist > 0.05f)
                {
                    // Pinch-to-scale smoothly
                    float scaleFactor = currentDist / _initialTwoHandDist;
                    SetBoardScale(_initialScaleOnTwoHandGrab * scaleFactor);

                    // Position at midpoint between hands
                    Vector3 midpoint = (rightPos + leftPos) * 0.5f;
                    transform.position = midpoint;

                    // Orient along hand vector
                    Vector3 handDir = (rightPos - leftPos).normalized;
                    if (handDir.sqrMagnitude > 0.01f)
                    {
                        Vector3 forward = Vector3.Cross(handDir, Vector3.up).normalized;
                        if (forward.sqrMagnitude > 0.01f)
                        {
                            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                        }
                    }
                }
            }
            // 5. One-Handed Reposition (Right Hand)
            else if (_isGrabbedRight)
            {
                _initialTwoHandDist = 0f;

                // Controller thumbstick pushes/pulls distance
                float stickY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;
                if (Mathf.Abs(stickY) > 0.1f)
                {
                    _currentGrabDistanceRight = Mathf.Clamp(_currentGrabDistanceRight + stickY * Time.deltaTime * 0.8f, 0.35f, 2.5f);
                }

                if (_isDistanceGrabRight)
                {
                    transform.position = rightPos + rightForward * _currentGrabDistanceRight;
                    transform.rotation = rightRot * _grabRotOffsetRight;
                }
                else
                {
                    transform.position = rightPos + rightRot * _grabOffsetRight;
                    transform.rotation = rightRot * _grabRotOffsetRight;
                }
            }
            // 6. One-Handed Reposition (Left Hand)
            else if (_isGrabbedLeft)
            {
                _initialTwoHandDist = 0f;

                float stickY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).y;
                if (Mathf.Abs(stickY) > 0.1f)
                {
                    _currentGrabDistanceLeft = Mathf.Clamp(_currentGrabDistanceLeft + stickY * Time.deltaTime * 0.8f, 0.35f, 2.5f);
                }

                if (_isDistanceGrabLeft)
                {
                    transform.position = leftPos + leftForward * _currentGrabDistanceLeft;
                    transform.rotation = leftRot * _grabRotOffsetLeft;
                }
                else
                {
                    transform.position = leftPos + leftRot * _grabOffsetLeft;
                    transform.rotation = leftRot * _grabRotOffsetLeft;
                }
            }
            else
            {
                _initialTwoHandDist = 0f;
            }
        }

        private bool IsRayAimingAtBoard(Ray ray, out float distance)
        {
            distance = 0f;
            Bounds boardBounds = new Bounds(transform.position, Vector3.one * (0.65f * transform.localScale.x));
            if (boardBounds.IntersectRay(ray, out float hitDist))
            {
                distance = hitDist;
                return hitDist <= 3.5f;
            }
            return false;
        }

        private void HandleControllerShortcuts()
        {
            // B button on Right Touch controller or Y button on Left Touch controller triggers Recenter
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
                OVRInput.GetDown(OVRInput.Button.Four, OVRInput.Controller.LTouch))
            {
                RecenterToPlayerGaze();
            }
        }

        /// <summary>
        /// Pins the board 0.65m directly along the player's current head gaze vector at chest height,
        /// standing upright like a TV facing the player.
        /// </summary>
        public void RecenterToPlayerGaze()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            }
            if (_mainCamera == null) return;

            Vector3 headPos = _mainCamera.transform.position;
            Vector3 forward = _mainCamera.transform.forward;
            Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z).normalized;
            if (horizontalForward.sqrMagnitude < 0.01f)
            {
                horizontalForward = Vector3.forward;
            }

            Vector3 targetPos = headPos + horizontalForward * defaultGazeDistance + Vector3.up * chestHeightOffset;
            transform.position = targetPos;

            // In our vertical TV coordinate system, the front face faces -Z towards player
            // So transform.forward points along horizontalForward (away from player)
            transform.rotation = Quaternion.LookRotation(horizontalForward, Vector3.up);

            OnBoardRecentered?.Invoke();
        }

        public void SetBoardScale(float targetScale)
        {
            float clamped = Mathf.Clamp(targetScale, minScale, maxScale);
            transform.localScale = Vector3.one * clamped;
        }

        public void ApplyScaleDelta(float delta)
        {
            SetBoardScale(transform.localScale.x + delta);
        }

        public void TogglePassthrough()
        {
            SetPassthroughState(!_isPassthroughActive);
        }

        public void SetPassthroughState(bool enablePassthrough)
        {
            _isPassthroughActive = enablePassthrough;

            if (passthroughLayer != null)
            {
                passthroughLayer.enabled = enablePassthrough;
                passthroughLayer.hidden = !enablePassthrough;
            }

            // Update all active eye cameras in rig
            Camera[] cams = Camera.allCameras;
            foreach (var cam in cams)
            {
                if (cam == null) continue;

                if (enablePassthrough)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // Alpha 0 punches through to Quest passthrough
                }
                else
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1.0f); // Opaque alpha blocks passthrough completely
                }
            }

            EnsureZenEnvironmentVisuals(!enablePassthrough);

            OnPassthroughToggled?.Invoke(_isPassthroughActive);
        }

        private void EnsureZenEnvironmentVisuals(bool showZenEnvironment)
        {
            if (zenEnvironmentRoot == null)
            {
                Transform found = transform.root.Find("ZenStudio_Environment") ?? GameObject.Find("ZenStudio_Environment")?.transform;
                if (found != null) zenEnvironmentRoot = found.gameObject;
            }

            if (zenEnvironmentRoot != null)
            {
                zenEnvironmentRoot.SetActive(showZenEnvironment);
            }
        }

        public void BuildBoundingFrame(float boardSpan)
        {
            Material frameMat = null;
            Transform backplate = transform.Find("Monolith_Backplate");
            if (backplate != null)
            {
                var bpRend = backplate.GetComponent<MeshRenderer>();
                if (bpRend != null && bpRend.sharedMaterial != null) frameMat = bpRend.sharedMaterial;
            }
            if (frameMat == null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                if (urpShader != null)
                {
                    frameMat = new Material(urpShader);
                    frameMat.SetColor("_BaseColor", new Color(0.18f, 0.22f, 0.28f, 1f));
                    frameMat.SetFloat("_Metallic", 0.6f);
                    frameMat.SetFloat("_Smoothness", 0.7f);
                }
            }

            // Sleek TV Bounding Frame standing in the X-Y plane
            if (boundingFrameMesh == null)
            {
                GameObject frameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frameObj.name = "BoundingFrame";
                frameObj.transform.SetParent(transform, false);
                frameObj.transform.localPosition = new Vector3(0f, 0f, 0.012f);
                float frameWidth = boardSpan + 0.10f;
                frameObj.transform.localScale = new Vector3(frameWidth, frameWidth, 0.018f);

                var rend = frameObj.GetComponent<MeshRenderer>();
                if (rend != null && frameMat != null) rend.sharedMaterial = frameMat;

                // Frame box collider for distance grab raycasting
                var col = frameObj.GetComponent<BoxCollider>();
                if (col == null) col = frameObj.AddComponent<BoxCollider>();
                col.isTrigger = false;

                boundingFrameMesh = frameObj.transform;
            }

            // Left Grab Handle
            if (leftGrabTab == null)
            {
                GameObject leftTab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftTab.name = "GrabHandle_Left";
                leftTab.transform.SetParent(transform, false);
                leftTab.transform.localPosition = new Vector3(-boardSpan * 0.56f, 0f, 0f);
                leftTab.transform.localScale = new Vector3(0.038f, 0.16f, 0.024f);

                var rend = leftTab.GetComponent<MeshRenderer>();
                if (rend != null && frameMat != null) rend.sharedMaterial = frameMat;

                var col = leftTab.GetComponent<BoxCollider>();
                if (col == null) col = leftTab.AddComponent<BoxCollider>();
                col.isTrigger = false;

                leftGrabTab = leftTab.transform;
            }

            // Right Grab Handle
            if (rightGrabTab == null)
            {
                GameObject rightTab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightTab.name = "GrabHandle_Right";
                rightTab.transform.SetParent(transform, false);
                rightTab.transform.localPosition = new Vector3(boardSpan * 0.56f, 0f, 0f);
                rightTab.transform.localScale = new Vector3(0.038f, 0.16f, 0.024f);

                var rend = rightTab.GetComponent<MeshRenderer>();
                if (rend != null && frameMat != null) rend.sharedMaterial = frameMat;

                var col = rightTab.GetComponent<BoxCollider>();
                if (col == null) col = rightTab.AddComponent<BoxCollider>();
                col.isTrigger = false;

                rightGrabTab = rightTab.transform;
            }
        }
    }
}
