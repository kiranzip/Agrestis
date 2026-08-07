using UnityEngine;
using UnityEngine.AI;
using Agrestis.Core;
using Agrestis.Anim;
using Agrestis.Player;
using Agrestis.World;

namespace Agrestis.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthSystem))]
    public class EnemyAI : MonoBehaviour, IDamageable
    {
        public enum State { Patrol, Investigate, Chase, Attack, Flee, Dead }

        [Header("Movement speeds")]
        public float PatrolSpeed = 1.6f;
        public float ChaseSpeed = 4.4f;
        public float FleeSpeed = 5.2f;

        [Header("Patrol")]
        public float PatrolRadius = 14f;
        public float PatrolPauseMin = 1.2f;
        public float PatrolPauseMax = 3.5f;

        [Header("Combat")]
        public float AttackRange = 2.3f;
        public float AttackCooldown = 1.6f;
        [Tooltip("Delay between starting a swing and the damage landing.")]
        public float AttackWindup = 0.45f;
        public float AttackDamage = 3f;
        public float AttackKnockback = 6f;

        [Header("Morale")]
        [Range(0f, 1f)] public float FleeHealthThreshold = 0.25f;
        public float FleeDistance = 26f;

        [Header("Rewards")]
        [Tooltip("Chance of dropping a pickup.")]
        [Range(0f, 1f)] public float DropChance = 0.7f;
        [Tooltip("Seconds before the body is removed.")]
        public float CorpseLifetime = 6f;

        [Header("Setup")]
        [Tooltip("Transform to follow. Found automatically if empty.")]
        public Transform Target;
        [Tooltip("Difficulty multiplier. 0 uses the GameManager value.")]
        public float DifficultyOverride = 0f;
        [Tooltip("Centre of the patrol area.")]
        public Transform PatrolAnchor;

        public State Current { get; private set; } = State.Patrol;

        private NavMeshAgent _agent;
        private AIPerception _perception;
        private HealthSystem _health;
        private CharacterAnimator _animation;

        private Vector3 _homePosition;
        private Vector3 _patrolTarget;
        private float _stateTimer;
        private float _nextAttackTime;
        private float _windupEndsAt = -1f;
        private bool _pendingHit;
        private float _difficulty = 1f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _perception = GetComponent<AIPerception>();
            _health = GetComponent<HealthSystem>();
            _animation = GetComponent<CharacterAnimator>();

            _health.ReportToHud = false;
            _health.TakesFallDamage = false;
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void Start()
        {
            if (Target == null && PlayerController.Instance != null)
                Target = PlayerController.Instance.transform;

            float difficulty = DifficultyOverride > 0f
                ? DifficultyOverride
                : GameManager.Instance != null ? GameManager.Instance.DifficultyScalar : 1f;

            Configure(Target, difficulty);
        }

        public void Configure(Transform target, float difficulty)
        {
            _difficulty = Mathf.Max(0.5f, difficulty);
            _homePosition = PatrolAnchor != null ? PatrolAnchor.position : transform.position;

            Target = target;
            if (_perception != null) _perception.SetTarget(target);

            AttackDamage *= _difficulty;
            _health.SetMaxHearts(Mathf.Max(1, Mathf.RoundToInt(2f * _difficulty)));
            _health.FullHeal();
            ChaseSpeed *= Mathf.Lerp(1f, 1.25f, _difficulty - 1f);

            if (_animation != null) _animation.ReferenceSpeed = ChaseSpeed;

            PickPatrolPoint();
        }

        private void Update()
        {
            if (Current == State.Dead) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            _stateTimer += Time.deltaTime;

            if (Current != State.Flee && _health.Normalised <= FleeHealthThreshold)
                TransitionTo(State.Flee);

            switch (Current)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Investigate: TickInvestigate(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
                case State.Flee: TickFlee(); break;
            }

            ResolvePendingHit();
            DriveAnimation();
        }

        private void TransitionTo(State next)
        {
            if (Current == next) return;
            Current = next;
            _stateTimer = 0f;

            switch (next)
            {
                case State.Patrol:
                    _agent.speed = PatrolSpeed;
                    _agent.isStopped = false;
                    PickPatrolPoint();
                    break;
                case State.Investigate:
                    _agent.speed = ChaseSpeed * 0.7f;
                    _agent.isStopped = false;
                    SetDestinationSafe(_perception != null ? _perception.LastKnownPosition : transform.position);
                    break;
                case State.Chase:
                    _agent.speed = ChaseSpeed;
                    _agent.isStopped = false;
                    break;
                case State.Attack:
                    _agent.isStopped = true;
                    break;
                case State.Flee:
                    _agent.speed = FleeSpeed;
                    _agent.isStopped = false;
                    break;
                case State.Dead:
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    _agent.enabled = false;
                    break;
            }
        }

        private void TickPatrol()
        {
            if (_perception != null && _perception.IsAlert)
            {
                TransitionTo(_perception.HasLineOfSight ? State.Chase : State.Investigate);
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.4f)
            {
                if (_stateTimer > Random.Range(PatrolPauseMin, PatrolPauseMax))
                {
                    PickPatrolPoint();
                    _stateTimer = 0f;
                }
            }
        }

        private void TickInvestigate()
        {
            if (_perception == null) { TransitionTo(State.Patrol); return; }

            if (_perception.HasLineOfSight)
            {
                TransitionTo(State.Chase);
                return;
            }

            if (!_perception.IsAlert)
            {
                TransitionTo(State.Patrol);
                return;
            }

            SetDestinationSafe(_perception.LastKnownPosition);

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
            {
                transform.Rotate(Vector3.up, 70f * Time.deltaTime, Space.World);
                if (_stateTimer > 4.5f) TransitionTo(State.Patrol);
            }
        }

        private void TickChase()
        {
            if (_perception == null) { TransitionTo(State.Patrol); return; }

            if (!_perception.IsAlert)
            {
                TransitionTo(State.Patrol);
                return;
            }

            Vector3 targetPos = _perception.HasLineOfSight && _perception.Target != null
                ? _perception.Target.position
                : _perception.LastKnownPosition;

            SetDestinationSafe(targetPos);

            if (_perception.HasLineOfSight && _perception.DistanceToTarget <= AttackRange)
                TransitionTo(State.Attack);
            else if (!_perception.HasLineOfSight && _stateTimer > 1.5f)
                TransitionTo(State.Investigate);
        }

        private void TickAttack()
        {
            if (_perception == null || _perception.Target == null)
            {
                TransitionTo(State.Patrol);
                return;
            }

            Vector3 toTarget = _perception.Target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), 1f - Mathf.Exp(-9f * Time.deltaTime));

            if (_perception.DistanceToTarget > AttackRange * 1.35f)
            {
                TransitionTo(State.Chase);
                return;
            }

            if (Time.time >= _nextAttackTime && _windupEndsAt < 0f)
            {
                _windupEndsAt = Time.time + AttackWindup;
                _pendingHit = true;
                _animation?.PlayAttack();
                _nextAttackTime = Time.time + AttackCooldown;
            }
        }

        private void ResolvePendingHit()
        {
            if (!_pendingHit || Time.time < _windupEndsAt) return;

            _pendingHit = false;
            _windupEndsAt = -1f;

            if (_perception == null || _perception.Target == null) return;
            if (_perception.DistanceToTarget > AttackRange * 1.25f) return;

            PlayerController pc = _perception.Target.GetComponentInParent<PlayerController>();
            if (pc == null) return;

            if (pc.Health != null && pc.Health.TakeDamage(AttackDamage))
                pc.Knockback(pc.transform.position - transform.position, AttackKnockback);
        }

        private void TickFlee()
        {
            Vector3 away = transform.position - (_perception != null && _perception.Target != null
                ? _perception.Target.position
                : _homePosition);
            away.y = 0f;

            if (away.sqrMagnitude < 0.01f) away = transform.forward;

            Vector3 goal = transform.position + away.normalized * 12f;
            SetDestinationSafe(goal);

            bool safe = _perception == null || _perception.DistanceToTarget > FleeDistance;
            if (safe && _stateTimer > 3f)
            {
                _health.Heal(1);
                TransitionTo(State.Patrol);
            }
        }

        private void PickPatrolPoint()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * PatrolRadius;
                Vector3 candidate = _homePosition + new Vector3(offset.x, 0f, offset.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    _patrolTarget = hit.position;
                    SetDestinationSafe(_patrolTarget);
                    return;
                }
            }
            _patrolTarget = transform.position;
        }

        private void SetDestinationSafe(Vector3 position)
        {
            if (!_agent.enabled) return;

            if (!_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit self, 8f, NavMesh.AllAreas))
                    _agent.Warp(self.position);
                else
                    return;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        public void ApplyDamage(float quarterHearts, Vector3 fromPosition)
        {
            if (Current == State.Dead) return;

            if (_health.TakeDamage(quarterHearts))
            {
                _perception?.ForceAlert(fromPosition);
                if (Current == State.Patrol) TransitionTo(State.Chase);
            }
        }

        private void OnDied()
        {
            TransitionTo(State.Dead);
            _animation?.Report(RigPose.Dead, 0f, true);

            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

            if (Random.value < DropChance)
                Pickup.SpawnHeartDrop(transform.position + Vector3.up * 0.6f);

            Destroy(gameObject, CorpseLifetime);
        }

        private void DriveAnimation()
        {
            if (_animation == null) return;

            float speed01 = _agent.enabled && _agent.isOnNavMesh
                ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.01f, ChaseSpeed))
                : 0f;

            RigPose pose;
            if (Current == State.Dead) pose = RigPose.Dead;
            else if (speed01 > 0.55f) pose = RigPose.Run;
            else if (speed01 > 0.05f) pose = RigPose.Walk;
            else pose = RigPose.Idle;

            _animation.Report(pose, speed01, true);
        }
    }
}
