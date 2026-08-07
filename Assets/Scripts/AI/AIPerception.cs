using UnityEngine;
using Agrestis.Core;

namespace Agrestis.AI
{
    public class AIPerception : MonoBehaviour
    {
        [Header("Sight")]
        public float SightRange = 22f;
        [Range(10f, 180f)] public float SightHalfAngle = 62f;
        public float EyeHeight = 1.6f;
        [Tooltip("Noticed within this range regardless of facing.")]
        public float ProximityRange = 3.5f;

        [Header("Memory")]
        [Tooltip("How long the target is remembered.")]
        public float MemoryDuration = 6f;

        [Header("Hearing")]
        public float HearingRange = 16f;

        [Header("Budget")]
        [Tooltip("Seconds between sight checks.")]
        public float Interval = 0.15f;

        public Transform Target { get; private set; }
        public bool HasLineOfSight { get; private set; }

        public Vector3 LastKnownPosition { get; private set; }
        public bool IsAlert => Time.time - _lastPerceivedTime < MemoryDuration;
        public float DistanceToTarget { get; private set; } = float.MaxValue;

        private float _nextCheck;
        private float _lastPerceivedTime = -999f;

        private void OnEnable()
        {
            _nextCheck = Time.time + Random.value * Interval;
            GameEvents.LoudNoiseMade += OnNoise;
        }

        private void OnDisable()
        {
            GameEvents.LoudNoiseMade -= OnNoise;
        }

        public void SetTarget(Transform target) => Target = target;

        private void Update()
        {
            if (Target == null || Time.time < _nextCheck) return;
            _nextCheck = Time.time + Interval;

            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            Vector3 targetPoint = Target.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPoint - eye;
            DistanceToTarget = toTarget.magnitude;

            HasLineOfSight = false;

            if (DistanceToTarget > SightRange) return;

            bool withinCone = Vector3.Angle(transform.forward, toTarget) <= SightHalfAngle;
            bool withinProximity = DistanceToTarget <= ProximityRange;
            if (!withinCone && !withinProximity) return;

            if (Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, DistanceToTarget, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(Target)) return;
            }

            HasLineOfSight = true;
            LastKnownPosition = Target.position;
            _lastPerceivedTime = Time.time;
        }

        private void OnNoise(Vector3 position)
        {
            if ((position - transform.position).sqrMagnitude > HearingRange * HearingRange) return;
            LastKnownPosition = position;
            _lastPerceivedTime = Time.time;
        }

        public void ForceAlert(Vector3 position)
        {
            LastKnownPosition = position;
            _lastPerceivedTime = Time.time;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            Gizmos.color = HasLineOfSight ? Color.red : new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawRay(eye, Quaternion.AngleAxis(SightHalfAngle, Vector3.up) * transform.forward * SightRange);
            Gizmos.DrawRay(eye, Quaternion.AngleAxis(-SightHalfAngle, Vector3.up) * transform.forward * SightRange);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, HearingRange);
        }
#endif
    }
}
