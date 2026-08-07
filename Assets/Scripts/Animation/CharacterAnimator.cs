using UnityEngine;
using Agrestis.Core;
using Agrestis.Player;

namespace Agrestis.Anim
{
    public enum RigPose
    {
        Idle,
        Walk,
        Run,
        Fall,
        Climb,
        Swim,
        Glide,
        Attack,
        Dead
    }

    public class CharacterAnimator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator on the character model.")]
        public Animator Animator;

        [Header("Calibration")]
        [Tooltip("Speed that maps to Speed = 1.")]
        public float ReferenceSpeed = 6.8f;
        [Tooltip("Damping on the Speed parameter.")]
        public float SpeedDamping = 0.12f;

        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashClimbSpeed = Animator.StringToHash("ClimbSpeed");
        private static readonly int HashGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int HashClimbing = Animator.StringToHash("IsClimbing");
        private static readonly int HashSwimming = Animator.StringToHash("IsSwimming");
        private static readonly int HashGliding = Animator.StringToHash("IsGliding");
        private static readonly int HashMoveState = Animator.StringToHash("MoveState");
        private static readonly int HashAttack = Animator.StringToHash("Attack");
        private static readonly int HashHit = Animator.StringToHash("Hit");
        private static readonly int HashDie = Animator.StringToHash("Die");

        private PlayerController _player;
        private HealthSystem _health;
        private bool _ready;

        public RigPose CurrentPose { get; private set; } = RigPose.Idle;

        private void Awake()
        {
            if (Animator == null) Animator = GetComponentInChildren<Animator>();

            _player = GetComponent<PlayerController>();
            _health = GetComponent<HealthSystem>();
            _ready = Animator != null && Animator.runtimeAnimatorController != null;

            if (_ready)
            {
                Animator.applyRootMotion = false;
            }
            else
            {
                Debug.LogWarning($"{name} has no Animator Controller.", this);
            }
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void Update()
        {
            if (_player == null) return;

            float speed01 = ReferenceSpeed <= 0f ? 0f : Mathf.Clamp01(_player.PlanarSpeed / ReferenceSpeed);
            Report(MapPlayerState(_player.State), speed01, _player.IsGrounded, _player.ClimbVertical);
        }

        public void Report(RigPose pose, float speed01, bool grounded, float climbVertical = 0f)
        {
            CurrentPose = pose;
            if (!_ready) return;

            Animator.SetFloat(HashSpeed, speed01, SpeedDamping, Time.deltaTime);
            Animator.SetFloat(HashClimbSpeed, climbVertical, 0.08f, Time.deltaTime);
            Animator.SetBool(HashGrounded, grounded);
            Animator.SetBool(HashClimbing, pose == RigPose.Climb);
            Animator.SetBool(HashSwimming, pose == RigPose.Swim);
            Animator.SetBool(HashGliding, pose == RigPose.Glide);
            Animator.SetInteger(HashMoveState, (int)pose);
        }

        public void PlayAttack()
        {
            if (_ready) Animator.SetTrigger(HashAttack);
        }

        public void PlayHitReaction()
        {
            if (_ready) Animator.SetTrigger(HashHit);
        }

        public void PlayDeath()
        {
            if (_ready) Animator.SetTrigger(HashDie);
        }

        private void OnDamaged(int quarters) => PlayHitReaction();
        private void OnDied() => PlayDeath();

        private static RigPose MapPlayerState(PlayerMotionState state)
        {
            switch (state)
            {
                case PlayerMotionState.Walking: return RigPose.Walk;
                case PlayerMotionState.Sprinting: return RigPose.Run;
                case PlayerMotionState.Airborne: return RigPose.Fall;
                case PlayerMotionState.Climbing: return RigPose.Climb;
                case PlayerMotionState.Swimming: return RigPose.Swim;
                case PlayerMotionState.Gliding: return RigPose.Glide;
                case PlayerMotionState.Dead: return RigPose.Dead;
                default: return RigPose.Idle;
            }
        }
    }
}
