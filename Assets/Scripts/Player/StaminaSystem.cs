using UnityEngine;
using Agrestis.Core;

namespace Agrestis.Player
{
    public class StaminaSystem : MonoBehaviour
    {
        [Header("Capacity")]
        [Tooltip("Number of stamina wheels.")]
        [SerializeField] private float _maxWheels = 1f;

        [Header("Drain rates (wheels per second)")]
        public float SprintDrain = 0.19f;
        public float ClimbDrain = 0.14f;
        public float ClimbJumpCost = 0.10f;
        public float SwimDrain = 0.11f;
        public float GlideDrain = 0.08f;
        public float AttackCost = 0.06f;

        [Header("Regeneration")]
        [Tooltip("Delay before the wheel starts refilling.")]
        public float RegenDelay = 0.55f;
        [Tooltip("Refill rate.")]
        public float RegenRate = 0.40f;
        [Tooltip("Refill rate while exhausted.")]
        public float ExhaustedRegenRate = 0.22f;
        [Tooltip("Amount needed to clear exhaustion.")]
        [Range(0.1f, 1f)] public float RecoveryThreshold = 0.34f;
        [Tooltip("Delay after landing before stamina refills.")]
        public float LandingRegenDelay = 0.8f;

        public float Current { get; private set; }
        public float Max => _maxWheels;
        public bool IsExhausted { get; private set; }
        public float Normalised => _maxWheels <= 0f ? 0f : Current / _maxWheels;

        public bool CanStartAction => !IsExhausted && Current > 0.02f;

        private float _timeSinceDrain;
        private float _regenBlockedUntil;

        private void Awake()
        {
            Current = _maxWheels;
        }

        private void Start()
        {
            Broadcast();
        }

        private void Update()
        {
            _timeSinceDrain += Time.deltaTime;

            if (Time.time < _regenBlockedUntil) return;
            if (_timeSinceDrain < RegenDelay || Current >= _maxWheels) return;

            float rate = IsExhausted ? ExhaustedRegenRate : RegenRate;
            Current = Mathf.Min(_maxWheels, Current + rate * Time.deltaTime);

            if (IsExhausted && Current >= Mathf.Min(RecoveryThreshold, _maxWheels))
                IsExhausted = false;

            Broadcast();
        }

        public void Hold()
        {
            _timeSinceDrain = 0f;
        }

        public void BlockRegenFor(float seconds)
        {
            _regenBlockedUntil = Mathf.Max(_regenBlockedUntil, Time.time + seconds);
        }

        public bool Drain(float perSecond)
        {
            return Spend(perSecond * Time.deltaTime);
        }

        public bool Spend(float amount)
        {
            if (amount <= 0f) return true;
            if (IsExhausted) return false;

            Current -= amount;
            _timeSinceDrain = 0f;

            if (Current <= 0f)
            {
                Current = 0f;
                IsExhausted = true;
                Broadcast();
                return false;
            }

            Broadcast();
            return true;
        }

        public void AddWheelSegment(float amount = 0.4f)
        {
            _maxWheels += amount;
            Current = _maxWheels;
            IsExhausted = false;
            Broadcast();
        }

        public void Refill()
        {
            Current = _maxWheels;
            IsExhausted = false;
            _timeSinceDrain = RegenDelay;
            _regenBlockedUntil = 0f;
            Broadcast();
        }

        private void Broadcast() => GameEvents.RaiseStaminaChanged(Current, _maxWheels, IsExhausted);
    }
}
