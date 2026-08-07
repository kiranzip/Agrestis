using UnityEngine;
using Agrestis.Core;
using Agrestis.Player;

namespace Agrestis.CameraRig
{
    public class OrbitCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to follow. Found automatically if empty.")]
        public Transform Target;
        [Tooltip("Offset from the target to the orbit point.")]
        public Vector3 PivotOffset = new Vector3(0f, 1.55f, 0f);

        [Header("Orbit")]
        public float Distance = 5.2f;
        public float MinPitch = -35f;
        public float MaxPitch = 72f;
        public float FollowSharpness = 12f;
        public float RotationSharpness = 24f;

        [Header("Collision")]
        public float CollisionRadius = 0.28f;
        public float MinDistance = 0.9f;
        [Tooltip("How fast the camera returns after an obstruction.")]
        public float ReturnSpeed = 4f;

        [Header("Field of view")]
        public float BaseFov = 62f;
        public float SprintFov = 72f;
        public float FovSharpness = 5f;

        private PlayerInputRouter _input;
        private PlayerController _player;
        private UnityEngine.Camera _camera;

        private float _yaw;
        private float _pitch = 14f;
        private float _currentDistance;
        private float _targetDistance;
        private Vector3 _smoothPivot;

        private void Awake()
        {
            _camera = GetComponentInChildren<UnityEngine.Camera>();
            _currentDistance = _targetDistance = Distance;
        }

        private void Start()
        {
            if (Target == null && PlayerController.Instance != null)
                Target = PlayerController.Instance.transform;

            if (Target != null)
            {
                _player = Target.GetComponentInParent<PlayerController>();
                _input = Target.GetComponentInParent<PlayerInputRouter>();
                SnapToTarget();
            }
            else
            {
                Debug.LogWarning("OrbitCameraRig has no target.", this);
            }
        }

        public void Bind(Transform target, PlayerInputRouter input, PlayerController player)
        {
            Target = target;
            _input = input;
            _player = player;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            if (_input != null)
            {
                _yaw += _input.Look.x;
                _pitch = Mathf.Clamp(_pitch - _input.Look.y, MinPitch, MaxPitch);
            }

            Quaternion desiredRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-RotationSharpness * Time.deltaTime));

            Vector3 pivot = Target.position + PivotOffset;
            _smoothPivot = Vector3.Lerp(_smoothPivot, pivot, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));

            float wantDistance = Distance;
            float wantFov = BaseFov;
            if (_player != null)
            {
                switch (_player.State)
                {
                    case PlayerMotionState.Climbing:
                        wantDistance = Distance * 1.15f;
                        break;
                    case PlayerMotionState.Sprinting:
                        wantFov = SprintFov;
                        break;
                    case PlayerMotionState.Gliding:
                        wantDistance = Distance * 1.35f;
                        wantFov = SprintFov + 4f;
                        break;
                    case PlayerMotionState.Swimming:
                        wantDistance = Distance * 0.9f;
                        break;
                }
            }
            _targetDistance = wantDistance;

            Vector3 back = transform.rotation * Vector3.back;
            float allowed = _targetDistance;
            if (Physics.SphereCast(_smoothPivot, CollisionRadius, back, out RaycastHit hit, _targetDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerController>() == null)
                    allowed = Mathf.Max(MinDistance, hit.distance - 0.1f);
            }

            _currentDistance = allowed < _currentDistance
                ? allowed
                : Mathf.MoveTowards(_currentDistance, allowed, ReturnSpeed * Time.deltaTime);

            transform.position = _smoothPivot + back * _currentDistance;

            if (_camera != null)
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, wantFov, 1f - Mathf.Exp(-FovSharpness * Time.deltaTime));
        }

        public void SnapToTarget()
        {
            if (Target == null) return;
            _smoothPivot = Target.position + PivotOffset;
            _currentDistance = _targetDistance;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _smoothPivot + transform.rotation * Vector3.back * _currentDistance;
        }
    }
}
