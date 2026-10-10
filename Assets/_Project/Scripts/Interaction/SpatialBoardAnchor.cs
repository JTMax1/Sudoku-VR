using System;
using UnityEngine;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Encapsulates the floating 3D monolith board in a spatial bounding frame.
    /// Provides:
    /// - Two-handed pinch-to-scale (clamped between 0.4m and 1.5m)
    /// - Free 6DOF repositioning and tilting
    /// - Instant One-Tap Recentering (0.65m along player gaze vector at chest height)
    /// - Passthrough MR vs. Zen Skybox environment switching
    /// - Integration with Meta Interaction SDK Grabbable
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

        [Header("Passthrough & Environment")]
        [SerializeField] private OVRPassthroughLayer passthroughLayer;
        [SerializeField] private bool startInPassthrough = true;

        [Header("Bounding Frame & Handles")]
        [SerializeField] private Transform leftGrabTab;
        [SerializeField] private Transform rightGrabTab;
        [SerializeField] private Transform boundingFrameMesh;

        private Camera _mainCamera;
        private bool _isPassthroughActive;
        private Vector3 _baseScaleVector;

        public bool IsPassthroughActive => _isPassthroughActive;
        public float CurrentScaleMultiplier => transform.localScale.x / defaultScale;

        public event Action<bool> OnPassthroughToggled;
        public event Action OnBoardRecentered;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                _mainCamera = FindAnyObjectByType<Camera>();
            }

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
            // Initial ergonomic recenter at start
            RecenterToPlayerGaze();
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
            // Project forward onto horizontal plane to avoid weird pitch tilts unless looking down
            Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z).normalized;
            if (horizontalForward.sqrMagnitude < 0.01f)
            {
                horizontalForward = Vector3.forward;
            }

            Vector3 targetPos = headPos + horizontalForward * defaultGazeDistance + Vector3.up * chestHeightOffset;
            transform.position = targetPos;

            // Orient board to face the player's head cleanly
            Vector3 lookDirection = headPos - targetPos;
            lookDirection.y = 0f; // Keep vertical upright
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-lookDirection);
            }

            OnBoardRecentered?.Invoke();
        }

        /// <summary>
        /// Scales the floating monolith smoothly, clamped between 0.4m and 1.5m.
        /// </summary>
        public void SetBoardScale(float targetScale)
        {
            float clamped = Mathf.Clamp(targetScale, minScale, maxScale);
            transform.localScale = Vector3.one * clamped;
        }

        public void ApplyScaleDelta(float delta)
        {
            float current = transform.localScale.x;
            SetBoardScale(current + delta);
        }

        /// <summary>
        /// Toggles between Zen Studio Skybox and Passthrough Mixed Reality.
        /// </summary>
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

        /// <summary>
        /// Procedurally generates lightweight grab tabs and a perimeter acrylic frame.
        /// </summary>
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

                // Remove collider from visual backing so it doesn't block tile clicks
                var col = frameObj.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                boundingFrameMesh = frameObj.transform;
            }

            // Left Grab Tab
            if (leftGrabTab == null)
            {
                GameObject leftTab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftTab.name = "GrabHandle_Left";
                leftTab.transform.SetParent(transform, false);
                leftTab.transform.localPosition = new Vector3(-boardSpan * 0.58f, 0f, 0f);
                leftTab.transform.localScale = new Vector3(0.04f, 0.02f, 0.16f);
                leftGrabTab = leftTab.transform;
            }

            // Right Grab Tab
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
