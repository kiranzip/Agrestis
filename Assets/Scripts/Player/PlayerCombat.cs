using UnityEngine;
using Agrestis.Core;
using Agrestis.Anim;

namespace Agrestis.Player
{
    [RequireComponent(typeof(StaminaSystem))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Swing timing")]
        [Tooltip("Length of one swing.")]
        public float SwingDuration = 0.45f;
        [Tooltip("Part of the swing that deals damage.")]
        public Vector2 DamageWindow = new Vector2(0.25f, 0.6f);
        [Tooltip("Time allowed to chain the next hit.")]
        public float ComboWindow = 0.65f;

        [Header("Hit volume")]
        public float Reach = 2.1f;
        public float SwingHalfAngle = 70f;

        [Header("Damage (quarter-hearts)")]
        public float BaseDamage = 3f;
        [Tooltip("Damage multiplier on the last hit.")]
        public float FinisherMultiplier = 1.8f;
        public float Knockback = 5f;

        public int ComboStep { get; private set; }
        public bool IsSwinging => _swingTimer > 0f;

        public float SwingProgress => _swingTimer <= 0f ? 0f : 1f - (_swingTimer / SwingDuration);

        private StaminaSystem _stamina;
        private CharacterAnimator _animation;
        private float _swingTimer;
        private float _comboExpiry;
        private bool _damageApplied;

        private readonly Collider[] _hits = new Collider[24];

        private void Awake()
        {
            _stamina = GetComponent<StaminaSystem>();
            _animation = GetComponent<CharacterAnimator>();
        }

        public bool TryAttack()
        {
            if (IsSwinging) return false;
            if (_stamina != null && !_stamina.Spend(_stamina.AttackCost)) return false;

            ComboStep = Time.time <= _comboExpiry ? Mathf.Min(3, ComboStep + 1) : 1;
            _swingTimer = SwingDuration;
            _damageApplied = false;
            _animation?.PlayAttack();
            GameEvents.RaiseLoudNoise(transform.position);
            return true;
        }

        private void Update()
        {
            if (_swingTimer <= 0f)
            {
                if (Time.time > _comboExpiry) ComboStep = 0;
                return;
            }

            _swingTimer -= Time.deltaTime;
            float t = SwingProgress;

            if (!_damageApplied && t >= DamageWindow.x && t <= DamageWindow.y)
            {
                _damageApplied = true;
                ApplyHit();
            }

            if (_swingTimer <= 0f)
            {
                _swingTimer = 0f;
                _comboExpiry = Time.time + ComboWindow;
            }
        }

        private void ApplyHit()
        {
            Vector3 origin = transform.position + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(origin, Reach, _hits, ~0, QueryTriggerInteraction.Ignore);

            float damage = BaseDamage * (ComboStep >= 3 ? FinisherMultiplier : 1f);
            bool connected = false;

            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i];
                if (c == null) continue;

                IDamageable target = c.GetComponentInParent<IDamageable>();
                if (target == null) continue;
                if (c.transform.IsChildOf(transform)) continue;

                Vector3 toTarget = c.bounds.center - origin;
                toTarget.y = 0f;
                if (Vector3.Angle(transform.forward, toTarget) > SwingHalfAngle) continue;

                target.ApplyDamage(damage, transform.position);
                connected = true;
            }

            if (connected) GameEvents.RaiseLoudNoise(transform.position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Vector3 origin = transform.position + Vector3.up;
            Gizmos.DrawWireSphere(origin, Reach);
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(SwingHalfAngle, Vector3.up) * transform.forward * Reach);
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(-SwingHalfAngle, Vector3.up) * transform.forward * Reach);
        }
#endif
    }
}
