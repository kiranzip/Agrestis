using UnityEngine;
using Agrestis.Core;

namespace Agrestis.Player
{
    public class HealthSystem : MonoBehaviour
    {
        public const int QuartersPerHeart = 4;

        [Header("Capacity")]
        [SerializeField] private int _maxHearts = 3;

        [Header("Damage response")]
        [Tooltip("Invulnerability after a hit.")]
        public float InvulnerabilityTime = 0.9f;
        [Tooltip("Send changes to the HUD. Off for enemies.")]
        public bool ReportToHud = true;

        [Header("Fall damage")]
        public bool TakesFallDamage = true;
        [Tooltip("Falls shorter than this do no damage.")]
        public float SafeFallDistance = 5.5f;
        [Tooltip("Quarter hearts lost per metre beyond the safe distance.")]
        public float FallDamagePerMetre = 0.55f;

        public int CurrentQuarters { get; private set; }
        public int MaxQuarters => _maxHearts * QuartersPerHeart;
        public bool IsDead => CurrentQuarters <= 0;
        public float Normalised => MaxQuarters == 0 ? 0f : CurrentQuarters / (float)MaxQuarters;

        public System.Action<int> Damaged;
        public System.Action Died;

        private float _invulnerableUntil;

        private void Awake()
        {
            CurrentQuarters = MaxQuarters;
        }

        private void Start()
        {
            Broadcast();
        }

        public bool TakeDamage(float quarters, bool ignoreInvulnerability = false)
        {
            if (IsDead) return false;
            if (!ignoreInvulnerability && Time.time < _invulnerableUntil) return false;

            int amount = Mathf.Max(1, Mathf.RoundToInt(quarters));
            CurrentQuarters = Mathf.Max(0, CurrentQuarters - amount);
            _invulnerableUntil = Time.time + InvulnerabilityTime;

            Damaged?.Invoke(amount);
            Broadcast();

            if (CurrentQuarters == 0)
            {
                Died?.Invoke();
                if (ReportToHud) GameEvents.RaisePlayerDied();
            }

            return true;
        }

        public void ApplyFallDamage(float fallDistance)
        {
            if (!TakesFallDamage || fallDistance <= SafeFallDistance) return;
            float quarters = (fallDistance - SafeFallDistance) * FallDamagePerMetre;
            TakeDamage(quarters, ignoreInvulnerability: true);
        }

        public void Heal(int quarters)
        {
            if (IsDead) return;
            CurrentQuarters = Mathf.Min(MaxQuarters, CurrentQuarters + Mathf.Max(0, quarters));
            Broadcast();
        }

        public void FullHeal()
        {
            CurrentQuarters = MaxQuarters;
            Broadcast();
        }

        public void AddHeartContainer(int hearts = 1)
        {
            _maxHearts += hearts;
            CurrentQuarters += hearts * QuartersPerHeart;
            Broadcast();
        }

        public void SetMaxHearts(int hearts)
        {
            _maxHearts = Mathf.Max(1, hearts);
            CurrentQuarters = Mathf.Clamp(CurrentQuarters, 0, MaxQuarters);
            Broadcast();
        }

        private void Broadcast()
        {
            if (ReportToHud) GameEvents.RaiseHealthChanged(CurrentQuarters, MaxQuarters);
        }
    }
}
