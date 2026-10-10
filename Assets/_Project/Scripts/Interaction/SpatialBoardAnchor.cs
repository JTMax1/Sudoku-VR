using System;
using UnityEngine;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Encapsulates the floating 3D monolith board in a spatial bounding frame.
    /// Provides:
    /// - Two-handed pinch-to-scale (clamped between 0.4m and 1.5m)
    /// - Free 6DOF repositioning and tilting via controller grip or hand grab
    /// - Instant One-Tap Recentering (0.65m along player gaze vector at chest height)
    /// - Passthrough MR vs. Zen Skybox environment switching
    /// </summary>
    public class SpatialBoardAnchor : MonoBehaviour
    {
        [Header("Scale Boundaries (Meters)")]
        [SerializeField] private float minScale = 0.4f;
        [SerializeField] private float maxScale = 1.5f;
        [SerializeField] private float defaultScale = 0.85f;

        [Header("Recentering Geometry")]
        [SerializeField] private float defaultGazeDistance = 0.65f; // 65cm in front of chest
        [SerializeField] private float chestHeightOffset = -0.15f;

        [Header("Grab & Manipulation Distance")]
        [SerializeField] private float grabRadius = 0.55f; // Grab range around board center or handles

        [Header("Passthrough & Environment")]
        [SerializeField] private OVRPassthroughLayer passthroughLayer;
        [SerializeField] private bool startInPassthrough = true;

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

        // Grab state
        private bool _isGrabbedRight;
        private bool _isGrabbedLeft;
        private Vector3 _grabOffsetRight;
        private Vector3 _grabOffsetLeft;
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

            SetPassthroughState(startInPassthrough);
        }

        private void Start()
        {
            if (_cameraRig != null)
            {
                _rightHand = _cameraRig.rightHandAnchor?.GetComponentInChildren<OVRHand>();
                _leftHand = _cameraRig.leftHandAnchor?.GetComponentInChildren<OVRHand>();
            }

            RecenterToPlayerGaze();
        }

        private void Update()
        {
            HandleXRBoardManipulation();
            HandleControllerShortcuts();
        }

        private void HandleXRBoardManipulation()
        {
            Vector3 rightPos = Vector3.zero;
            Vector3 leftPos = Vector3.zero;
            bool rightGrabActive = false;
            bool leftGrabActive = false;

            // 1. Right Hand / Controller Grab Check
            if (_rightHand != null && _rightHand.IsTracked)
            {
                rightPos = _rightHand.transform.position;
                // Middle finger pinch or palm grab
                rightGrabActive = _rightHand.GetFingerIsPinching(OVRHand.HandFinger.Middle) ||
                                  _rightHand.GetFingerIsPinching(OVRHand.HandFinger.Pinky);
            }
            else if (_cameraRig?.rightControllerAnchor != null)
            {
                rightPos = _cameraRig.rightControllerAnchor.position;
                rightGrabActive = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
                                  OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > 0.45f;
            }

            // 2. Left Hand / Controller Grab Check
            if (_leftHand != null && _leftHand.IsTracked)
            {
                leftPos = _leftHand.transform.position;
                leftGrabActive = _leftHand.GetFingerIsPinching(OVRHand.HandFinger.Middle) ||
                                 _leftHand.GetFingerIsPinching(OVRHand.HandFinger.Pinky);
            }
            else if (_cameraRig?.leftControllerAnchor != null)
            {
                leftPos = _cameraRig.leftControllerAnchor.position;
                leftGrabActive = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch) ||
                                 OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > 0.45f;
            }

            // Evaluate Proximity to Board
            float distRightToBoard = Vector3.Distance(rightPos, transform.position);
            float distLeftToBoard = Vector3.Distance(leftPos, transform.position);

            bool canGrabRight = distRightToBoard <= (grabRadius * transform.localScale.x);
            bool canGrabLeft = distLeftToBoard <= (grabRadius * transform.localScale.x);

            // Handle Right Grab Transition
            if (rightGrabActive && canGrabRight && !_isGrabbedRight)
            {
                _isGrabbedRight = true;
                _grabOffsetRight = transform.position - rightPos;
            }
            else if (!rightGrabActive && _isGrabbedRight)
            {
                _isGrabbedRight = false;
            }

            // Handle Left Grab Transition
            if (leftGrabActive && canGrabLeft && !_isGrabbedLeft)
            {
                _isGrabbedLeft = true;
                _grabOffsetLeft = transform.position - leftPos;
            }
            else if (!leftGrabActive && _isGrabbedLeft)
            {
                _isGrabbedLeft = false;
            }

            // 3. Two-Handed Manipulation & Scaling
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
                    // Scale smoothly proportional to hand distance
                    float scaleFactor = currentDist / _initialTwoHandDist;
                    SetBoardScale(_initialScaleOnTwoHandGrab * scaleFactor);

                    // Move to midpoint between hands
                    Vector3 midpoint = (rightPos + leftPos) * 0.5f;
                    transform.position = midpoint;

                    // Rotate board along hand direction
                    Vector3 handDir = (rightPos - leftPos).normalized;
                    if (handDir.sqrMagnitude > 0.01f)
                    {
                        Vector3 forward = Vector3.Cross(handDir, Vector3.up).normalized;
                        if (forward.sqrMagnitude > 0.01f)
                        {
                            transform.rotation = Quaternion.LookRotation(forward);
                        }
                    }
                }
            }
            // 4. One-Handed Reposition (Right Hand)
            else if (_isGrabbedRight)
            {
                _initialTwoHandDist = 0f;
                transform.position = rightPos + _grabOffsetRight;
            }
            // 5. One-Handed Reposition (Left Hand)
            else if (_isGrabbedLeft)
            {
                _initialTwoHandDist = 0f;
                transform.position = leftPos + _grabOffsetLeft;
            }
            else
            {
                _initialTwoHandDist = 0f;
            }
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
        /// Pins the board 0.65m directly along the player's current head gaze vector at chest height.
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

            Vector3 lookDirection = headPos - targetPos;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-lookDirection);
            }

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

            if (_mainCamera != null)
            {
                if (enablePassthrough)
                {
                    _mainCamera.clearFlags = CameraClearFlags.SolidColor;
                    _mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                }
                else
                {
                    _mainCamera.clearFlags = CameraClearFlags.Skybox;
                }
            }

            OnPassthroughToggled?.Invoke(_isPassthroughActive);
        }

        public void BuildBoundingFrame(float boardSpan)
        {
            if (boundingFrameMesh == null)
            {
                GameObject frameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frameObj.name = "BoundingFrame";
                frameObj.transform.SetParent(transform, false);
                frameObj.transform.localPosition = new Vector3(0f, -0.015f, 0f);
                float frameWidth = boardSpan + 0.12f;
                frameObj.transform.localScale = new Vector3(frameWidth, 0.015f, frameWidth);

                var col = frameObj.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                boundingFrameMesh = frameObj.transform;
            }

            if (leftGrabTab == null)
            {
                GameObject leftTab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftTab.name = "GrabHandle_Left";
                leftTab.transform.SetParent(transform, false);
                leftTab.transform.localPosition = new Vector3(-boardSpan * 0.58f, 0f, 0f);
                leftTab.transform.localScale = new Vector3(0.04f, 0.02f, 0.16f);
                leftGrabTab = leftTab.transform;
            }

            if (rightGrabTab == null)
            {
                GameObject rightTab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightTab.name = "GrabHandle_Right";
                rightTab.transform.SetParent(transform, false);
                rightTab.transform.localPosition = new Vector3(boardSpan * 0.58f, 0f, 0f);
                rightTab.transform.localScale = new Vector3(0.04f, 0.02f, 0.16f);
                rightGrabTab = rightTab.transform;
            }
        }
    }
}
