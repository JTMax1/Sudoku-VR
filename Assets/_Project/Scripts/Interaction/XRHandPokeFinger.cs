using UnityEngine;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Attaches an ergonomic 3D trigger collider to tracked index fingertips and VR controllers.
    /// Provides direct physical 3D poking into Tile3DView and TactileKeypadButton monoliths.
    /// Supports tracked hands (OVRSkeleton) and physical Touch controllers.
    /// </summary>
    public class XRHandPokeFinger : MonoBehaviour
    {
        [Header("Poke Configuration")]
        [SerializeField] private OVRPlugin.Hand handType = OVRPlugin.Hand.HandRight;
        public OVRPlugin.Hand HandType => handType;
        [SerializeField] private float pokeRadius = 0.015f; // 1.5cm fingertip sphere
        [SerializeField] private Vector3 controllerTipOffset = new Vector3(0f, -0.02f, 0.06f);

        private OVRHand _ovrHand;
        private OVRSkeleton _ovrSkeleton;
        private Transform _indexTipBone;
        private SphereCollider _pokeCollider;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _ovrHand = GetComponentInParent<OVRHand>() ?? GetComponentInChildren<OVRHand>();
            _ovrSkeleton = GetComponentInParent<OVRSkeleton>() ?? GetComponentInChildren<OVRSkeleton>();

            SetupPokeCollider();
        }

        private void SetupPokeCollider()
        {
            _pokeCollider = GetComponent<SphereCollider>();
            if (_pokeCollider == null)
            {
                _pokeCollider = gameObject.AddComponent<SphereCollider>();
            }
            _pokeCollider.isTrigger = true;
            _pokeCollider.radius = pokeRadius;

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        private void Update()
        {
            UpdatePokePosition();
        }

        private void UpdatePokePosition()
        {
            // 1. If tracked hand skeleton is initialized, lock directly to the index tip bone
            if (_ovrSkeleton != null && _ovrSkeleton.IsInitialized)
            {
                if (_indexTipBone == null)
                {
                    foreach (var bone in _ovrSkeleton.Bones)
                    {
                        if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                        {
                            _indexTipBone = bone.Transform;
                            break;
                        }
                    }
                }

                if (_indexTipBone != null)
                {
                    transform.position = _indexTipBone.position;
                    transform.rotation = _indexTipBone.rotation;
                    return;
                }
            }

            // 2. If hand tracking is active but skeleton bones not yet ready, use hand pointer pose
            if (_ovrHand != null && _ovrHand.IsTracked && _ovrHand.PointerPose != null)
            {
                transform.position = _ovrHand.PointerPose.position;
                transform.rotation = _ovrHand.PointerPose.rotation;
                return;
            }

            // 3. Controller fallback offset
            if (transform.parent != null)
            {
                transform.localPosition = controllerTipOffset;
            }
        }
    }
}
