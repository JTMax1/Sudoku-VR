using System;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    public enum CompanionState
    {
        Idle,
        Thinking,
        Celebrating,
        Hinting,
        Encouraging,
        Victory
    }

    /// <summary>
    /// Tactile 3D animated companion mascot ("Orbi the Star Sprite") for Sudoku Zenith MR.
    /// Provides emotional anchoring, guidance, and celebratory joy inspired by Jolly Match 3 MR.
    /// Features procedural levitation, procedural eye blinks, wand waving, and streak celebrations.
    /// Fully zero-allocation runtime.
    /// </summary>
    public class CompanionMascotView : MonoBehaviour
    {
        [Header("Procedural Visual Roots")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform wandRoot;
        [SerializeField] private Transform leftEye;
        [SerializeField] private Transform rightEye;

        [Header("Floating Offset")]
        [SerializeField] private Vector3 offsetFromBoard = new Vector3(0.38f, 0.05f, -0.05f); // 38cm to the right of board

        [Header("Motion Dynamics")]
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float bobHeight = 0.015f;
        [SerializeField] private bool reducedMotion = false;

        private CompanionState _currentState = CompanionState.Idle;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private float _stateTimer;
        private float _nextBlinkTime;
        private int _consecutiveCorrectStreak;

        private Camera _playerCamera;
        private Transform _boardTransform;

        private void Awake()
        {
            _playerCamera = Camera.main;
            _boardTransform = transform.parent;

            if (bodyRoot == null)
            {
                BuildProceduralMascotGeometry();
            }

            _baseLocalPosition = offsetFromBoard;
            transform.localPosition = _baseLocalPosition;
            _baseLocalRotation = Quaternion.Euler(0f, -15f, 0f); // Slight inward turn towards board
            transform.localRotation = _baseLocalRotation;

            _nextBlinkTime = Time.time + UnityEngine.Random.Range(2f, 5f);
        }

        private void Start()
        {
            var gm = SudokuGameManager.Instance;
            if (gm != null)
            {
                gm.OnHintReceived += HandleHintReceived;
                gm.OnMistakesUpdated += HandleMistakeRegistered;
                gm.OnGameFinished += HandleGameFinished;
            }
        }

        private void OnDestroy()
        {
            var gm = SudokuGameManager.Instance;
            if (gm != null)
            {
                gm.OnHintReceived -= HandleHintReceived;
                gm.OnMistakesUpdated -= HandleMistakeRegistered;
                gm.OnGameFinished -= HandleGameFinished;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _stateTimer -= dt;

            // Handle procedural eye blinks
            if (Time.time >= _nextBlinkTime)
            {
                TriggerBlink();
                _nextBlinkTime = Time.time + UnityEngine.Random.Range(2.5f, 6f);
            }

            // Procedural animation state machine
            switch (_currentState)
            {
                case CompanionState.Idle:
                    AnimateIdle(dt);
                    break;
                case CompanionState.Celebrating:
                    AnimateCelebration(dt);
                    if (_stateTimer <= 0f) SetState(CompanionState.Idle);
                    break;
                case CompanionState.Hinting:
                    AnimateHinting(dt);
                    if (_stateTimer <= 0f) SetState(CompanionState.Idle);
                    break;
                case CompanionState.Encouraging:
                    AnimateEncouragement(dt);
                    if (_stateTimer <= 0f) SetState(CompanionState.Idle);
                    break;
                case CompanionState.Victory:
                    AnimateVictory(dt);
                    break;
            }
        }

        #region Procedural Animation Routines

        private void AnimateIdle(float dt)
        {
            if (reducedMotion) return;

            float hover = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, hover, 0f);

            // Gentle gentle breathing roll
            float tilt = Mathf.Sin(Time.time * 1.5f) * 2.5f;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, tilt);

            // Floating hands gentle levitation
            if (leftHand != null)
            {
                leftHand.localPosition = new Vector3(-0.065f, Mathf.Sin(Time.time * 2.5f) * 0.005f, 0.02f);
            }
            if (rightHand != null)
            {
                rightHand.localPosition = new Vector3(0.065f, Mathf.Sin(Time.time * 2.5f + 1f) * 0.005f, 0.02f);
            }
        }

        private void AnimateCelebration(float dt)
        {
            // Upward joyful bounce and small spin
            float bounce = Mathf.Abs(Mathf.Sin((3f - _stateTimer) * 8f)) * 0.04f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, bounce, 0f);

            float spin = Mathf.Sin((3f - _stateTimer) * 6f) * 25f;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, spin, 0f);

            if (rightHand != null)
            {
                rightHand.localPosition = new Vector3(0.065f, 0.04f + Mathf.Sin(Time.time * 12f) * 0.015f, 0.03f);
            }
        }

        private void AnimateHinting(float dt)
        {
            // Lean forward and wave wand towards board
            transform.localPosition = _baseLocalPosition + new Vector3(-0.04f, 0.01f, -0.04f);
            transform.localRotation = Quaternion.Euler(5f, -35f, -5f);

            if (wandRoot != null)
            {
                wandRoot.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 8f) * 20f, 0f, 0f);
            }
        }

        private void AnimateEncouragement(float dt)
        {
            // Sympathetic curious nod
            float nod = Mathf.Sin(Time.time * 4f) * 6f;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(nod, 0f, 0f);
        }

        private void AnimateVictory(float dt)
        {
            // Full triumphant spin and loop
            float loopProgress = (Time.time * 2f) % (Mathf.PI * 2f);
            float yOffset = Mathf.Sin(loopProgress) * 0.06f;
            float xOffset = Mathf.Cos(loopProgress) * 0.03f;

            transform.localPosition = _baseLocalPosition + new Vector3(xOffset, yOffset, 0f);
            transform.Rotate(Vector3.up, 180f * dt, Space.Self);
        }

        private void TriggerBlink()
        {
            if (leftEye != null && rightEye != null)
            {
                // Quick scale flatten on Y to simulate eyelid blink
                leftEye.localScale = new Vector3(0.018f, 0.002f, 0.005f);
                rightEye.localScale = new Vector3(0.018f, 0.002f, 0.005f);
                CancelInvoke(nameof(RestoreEyes));
                Invoke(nameof(RestoreEyes), 0.12f);
            }
        }

        private void RestoreEyes()
        {
            if (leftEye != null && rightEye != null)
            {
                leftEye.localScale = new Vector3(0.018f, 0.024f, 0.005f);
                rightEye.localScale = new Vector3(0.018f, 0.024f, 0.005f);
            }
        }

        #endregion

        #region Event Reactions

        public void NotifyCorrectMove()
        {
            _consecutiveCorrectStreak++;
            if (_consecutiveCorrectStreak >= 3)
            {
                SetState(CompanionState.Celebrating, 2.2f);
            }
            else
            {
                // Short happy nod
                SetState(CompanionState.Celebrating, 1.2f);
            }
        }

        private void HandleHintReceived(HintResult hint)
        {
            SetState(CompanionState.Hinting, 3.5f);
        }

        private void HandleMistakeRegistered(int current, int max)
        {
            _consecutiveCorrectStreak = 0;
            SetState(CompanionState.Encouraging, 2.0f);
        }

        private void HandleGameFinished(bool victory)
        {
            if (victory)
            {
                SetState(CompanionState.Victory, 999f);
            }
        }

        private void SetState(CompanionState state, float duration = 2.0f)
        {
            _currentState = state;
            _stateTimer = duration;
        }

        #endregion

        #region Procedural 3D Mesh Generation (Pearlescent Mascot Geometry)

        private void BuildProceduralMascotGeometry()
        {
            bodyRoot = new GameObject("Orbi_Body").transform;
            bodyRoot.SetParent(transform, false);

            // 1. Head/Body (Pearlescent Soft Sphere)
            GameObject headObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headObj.name = "Head_Sphere";
            headObj.transform.SetParent(bodyRoot, false);
            headObj.transform.localScale = new Vector3(0.13f, 0.125f, 0.12f);
            Destroy(headObj.GetComponent<Collider>());

            // Soft pearlescent ceramic material
            var headRenderer = headObj.GetComponent<MeshRenderer>();
            Material headMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            headMat.color = new Color(0.97f, 0.96f, 0.99f, 1f); // Milk porcelain
            headMat.SetFloat("_Smoothness", 0.88f);
            headRenderer.sharedMaterial = headMat;

            // 2. Eyes
            Material eyeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            eyeMat.color = new Color(0.12f, 0.14f, 0.20f, 1f); // Deep glossy slate

            GameObject leftEyeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftEyeObj.name = "Left_Eye";
            leftEyeObj.transform.SetParent(bodyRoot, false);
            leftEyeObj.transform.localPosition = new Vector3(-0.026f, 0.015f, 0.055f);
            leftEyeObj.transform.localScale = new Vector3(0.018f, 0.024f, 0.005f);
            Destroy(leftEyeObj.GetComponent<Collider>());
            leftEyeObj.GetComponent<MeshRenderer>().sharedMaterial = eyeMat;
            leftEye = leftEyeObj.transform;

            GameObject rightEyeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightEyeObj.name = "Right_Eye";
            rightEyeObj.transform.SetParent(bodyRoot, false);
            rightEyeObj.transform.localPosition = new Vector3(0.026f, 0.015f, 0.055f);
            rightEyeObj.transform.localScale = new Vector3(0.018f, 0.024f, 0.005f);
            Destroy(rightEyeObj.GetComponent<Collider>());
            rightEyeObj.GetComponent<MeshRenderer>().sharedMaterial = eyeMat;
            rightEye = rightEyeObj.transform;

            // 3. Floating Mitten Hands
            Material goldTrimMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            goldTrimMat.color = new Color(0.95f, 0.78f, 0.25f, 1f); // Champagne gold
            goldTrimMat.SetFloat("_Metallic", 0.85f);
            goldTrimMat.SetFloat("_Smoothness", 0.82f);

            GameObject leftHandObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftHandObj.name = "Left_Hand";
            leftHandObj.transform.SetParent(transform, false);
            leftHandObj.transform.localPosition = new Vector3(-0.065f, -0.01f, 0.02f);
            leftHandObj.transform.localScale = new Vector3(0.038f, 0.035f, 0.035f);
            Destroy(leftHandObj.GetComponent<Collider>());
            leftHandObj.GetComponent<MeshRenderer>().sharedMaterial = headMat;
            leftHand = leftHandObj.transform;

            GameObject rightHandObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightHandObj.name = "Right_Hand";
            rightHandObj.transform.SetParent(transform, false);
            rightHandObj.transform.localPosition = new Vector3(0.065f, -0.01f, 0.02f);
            rightHandObj.transform.localScale = new Vector3(0.038f, 0.035f, 0.035f);
            Destroy(rightHandObj.GetComponent<Collider>());
            rightHandObj.GetComponent<MeshRenderer>().sharedMaterial = headMat;
            rightHand = rightHandObj.transform;

            // 4. Star Wand in Right Hand
            GameObject wandObj = new GameObject("Star_Wand");
            wandObj.transform.SetParent(rightHand, false);
            wandObj.transform.localPosition = new Vector3(0.01f, 0.025f, 0.02f);
            wandObj.transform.localRotation = Quaternion.Euler(-25f, 15f, 0f);

            GameObject wandShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wandShaft.transform.SetParent(wandObj.transform, false);
            wandShaft.transform.localScale = new Vector3(0.006f, 0.045f, 0.006f);
            Destroy(wandShaft.GetComponent<Collider>());
            wandShaft.GetComponent<MeshRenderer>().sharedMaterial = goldTrimMat;

            GameObject wandTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wandTip.transform.SetParent(wandObj.transform, false);
            wandTip.transform.localPosition = new Vector3(0f, 0.048f, 0f);
            wandTip.transform.localScale = new Vector3(0.024f, 0.024f, 0.024f);
            Destroy(wandTip.GetComponent<Collider>());
            wandTip.GetComponent<MeshRenderer>().sharedMaterial = goldTrimMat;
            wandRoot = wandObj.transform;

            // 5. Golden Zenith Crown on Head
            GameObject crownObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crownObj.name = "Zenith_Crown";
            crownObj.transform.SetParent(bodyRoot, false);
            crownObj.transform.localPosition = new Vector3(0f, 0.065f, 0f);
            crownObj.transform.localScale = new Vector3(0.045f, 0.008f, 0.045f);
            Destroy(crownObj.GetComponent<Collider>());
            crownObj.GetComponent<MeshRenderer>().sharedMaterial = goldTrimMat;
        }

        #endregion
    }
}
