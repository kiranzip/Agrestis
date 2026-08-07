using UnityEngine;
using Agrestis.Core;
using Agrestis.World;

namespace Agrestis.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Ground movement")]
        public float WalkSpeed = 3.4f;
        public float SprintSpeed = 6.8f;
        public float Acceleration = 14f;
        public float AirAcceleration = 4f;
        public float TurnSharpness = 14f;

        [Header("Jump and gravity")]
        public float JumpHeight = 1.5f;
        public float Gravity = -22f;
        [Tooltip("Grace period for jumping after leaving a ledge.")]
        public float CoyoteTime = 0.12f;

        [Header("Climbing")]
        [Tooltip("Minimum surface angle that can be climbed.")]
        public float MinClimbAngle = 46f;
        public float ClimbSpeed = 2.1f;
        public float ClimbJumpImpulse = 5.2f;
        [Tooltip("Probe distance for finding a wall.")]
        public float ClimbProbeDistance = 0.75f;
        [Tooltip("Radius of the climb probe sphere.")]
        public float ClimbProbeRadius = 0.18f;
        [Tooltip("Sideways climb speed, as a fraction of Climb Speed.")]
        [Range(0.2f, 1f)] public float ClimbSideSpeedMultiplier = 0.55f;
        [Tooltip("How far around an edge to look for the next face.")]
        public float CornerProbeDistance = 0.45f;
        [Tooltip("Time taken to swing around a corner.")]
        public float CornerWrapDuration = 0.3f;
        public float MantleHeight = 0.55f;

        [Header("Swimming")]
        public float SwimSpeed = 2.6f;
        public float SwimSprintSpeed = 4.2f;
        [Tooltip("How far below the surface the body floats.")]
        public float SwimBuoyancyOffset = 1.05f;
        public float BuoyancyStiffness = 6f;
        [Tooltip("Drowning damage per second.")]
        public float DrownDamagePerSecond = 2f;

        [Header("Gliding (Paraglider)")]
        public bool ParagliderUnlocked = true;
        public float GlideFallSpeed = -2.2f;
        public float GlideSpeed = 7.5f;
        [Tooltip("Minimum height to deploy the paraglider.")]
        public float MinGlideHeight = 2.5f;

        [Header("Interaction")]
        public float InteractRadius = 2.6f;

        [Header("Scene references")]
        [Tooltip("Camera used for movement direction.")]
        [SerializeField] private Transform _cameraTransform;

        public Transform CameraTransform
        {
            get => _cameraTransform;
            set => _cameraTransform = value;
        }

        public static PlayerController Instance { get; private set; }

        public StaminaSystem Stamina { get; private set; }
        public HealthSystem Health { get; private set; }
        public PlayerMotionState State { get; private set; } = PlayerMotionState.Idle;
        public CharacterController Controller { get; private set; }

        public float PlanarSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public Vector3 ClimbSurfaceNormal { get; private set; } = Vector3.forward;
        public bool InWater => _water != null;

        public float ClimbVertical { get; private set; }

        private PlayerInputRouter _input;
        private PlayerCombat _combat;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _timeSinceGrounded;
        private float _fallPeakY;
        private bool _trackingFall;
        private bool _sprintLatched;
        private float _climbCooldownUntil;
        private float _climbBlockedUpUntil;
        private Vector3 _lastWallPoint;

        private bool _regenLockedUntilLanding;

        private bool _wrapping;
        private Vector3 _wrapStart, _wrapControl, _wrapEnd;
        private Quaternion _wrapStartRot, _wrapEndRot;
        private float _wrapTime;
        private WaterVolume _water;
        private IInteractable _focus;
        private bool _dead;

        private readonly Collider[] _overlapBuffer = new Collider[16];

        private void Awake()
        {
            Instance = this;

            Controller = GetComponent<CharacterController>();
            Stamina = GetComponent<StaminaSystem>();
            Health = GetComponent<HealthSystem>();
            _input = GetComponent<PlayerInputRouter>();
            _combat = GetComponent<PlayerCombat>();

            if (_cameraTransform == null && UnityEngine.Camera.main != null)
                _cameraTransform = UnityEngine.Camera.main.transform;

            if (_cameraTransform == null)
                Debug.LogWarning("PlayerController has no camera assigned.", this);

            if (Health != null) Health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Died -= OnDied;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_input == null) return;

            if (_input.PausePressed && GameManager.Instance != null)
                GameManager.Instance.TogglePause();

            if (_dead || (GameManager.Instance != null && GameManager.Instance.IsPaused))
                return;

            UpdateGroundedFlag();
            UpdateInteractionFocus();

            switch (State)
            {
                case PlayerMotionState.Climbing:
                    TickClimbing();
                    break;
                case PlayerMotionState.Swimming:
                    TickSwimming();
                    break;
                case PlayerMotionState.Gliding:
                    TickGliding();
                    break;
                default:
                    TickLocomotion();
                    break;
            }

            PlanarSpeed = new Vector3(Controller.velocity.x, 0f, Controller.velocity.z).magnitude;

            if (_input.AttackPressed && _combat != null && State != PlayerMotionState.Climbing && State != PlayerMotionState.Swimming)
                _combat.TryAttack();

            if (_input.InteractPressed && _focus != null && _focus.IsAvailable)
                _focus.Interact(gameObject);
        }

        private void UpdateGroundedFlag()
        {
            bool ray = false;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
                    out RaycastHit groundHit, 0.34f, ~0, QueryTriggerInteraction.Ignore))
            {
                ray = groundHit.collider.GetComponentInParent<PlayerController>() == null;
            }

            IsGrounded = Controller.isGrounded || ray;
            _timeSinceGrounded = IsGrounded ? 0f : _timeSinceGrounded + Time.deltaTime;
        }

        private Vector3 CameraRelativeInput()
        {
            Vector2 raw = _input.Move;
            if (raw.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 fwd = CameraTransform != null ? CameraTransform.forward : Vector3.forward;
            Vector3 right = CameraTransform != null ? CameraTransform.right : Vector3.right;
            fwd.y = 0f; right.y = 0f;
            fwd.Normalize(); right.Normalize();

            return Vector3.ClampMagnitude(fwd * raw.y + right * raw.x, 1f);
        }

        private void FaceDirection(Vector3 dir, float sharpness)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        private void SetState(PlayerMotionState next)
        {
            if (State == next) return;
            State = next;
            GameEvents.RaisePlayerStateChanged(next);
        }

        private void TickLocomotion()
        {
            Vector3 wish = CameraRelativeInput();

            bool wantsSprint = _input.SprintHeld && wish.sqrMagnitude > 0.2f;
            if (wantsSprint && !_sprintLatched && Stamina.CanStartAction) _sprintLatched = true;
            if (!_input.SprintHeld || wish.sqrMagnitude <= 0.05f) _sprintLatched = false;

            bool sprinting = false;
            if (_sprintLatched && IsGrounded)
            {
                sprinting = Stamina.Drain(Stamina.SprintDrain);
                if (!sprinting) _sprintLatched = false;
            }

            float targetSpeed = sprinting ? SprintSpeed : WalkSpeed;
            Vector3 targetVelocity = wish * targetSpeed;

            float accel = IsGrounded ? Acceleration : AirAcceleration;
            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            planar = Vector3.Lerp(planar, targetVelocity, 1f - Mathf.Exp(-accel * Time.deltaTime));
            _velocity.x = planar.x;
            _velocity.z = planar.z;

            FaceDirection(wish, TurnSharpness);

            if (IsGrounded && _verticalVelocity <= 0f)
            {
                if (_trackingFall)
                {
                    Health?.ApplyFallDamage(_fallPeakY - transform.position.y);
                    _trackingFall = false;
                }

                if (_regenLockedUntilLanding)
                {
                    _regenLockedUntilLanding = false;
                    Stamina.BlockRegenFor(Stamina.LandingRegenDelay);
                }

                _verticalVelocity = -2f;
            }
            else
            {
                if (_regenLockedUntilLanding) Stamina.Hold();

                if (!_trackingFall)
                {
                    _trackingFall = true;
                    _fallPeakY = transform.position.y;
                }
                _fallPeakY = Mathf.Max(_fallPeakY, transform.position.y);
                _verticalVelocity += Gravity * Time.deltaTime;
            }

            if (_input.JumpPressed && (IsGrounded || _timeSinceGrounded < CoyoteTime))
            {
                _verticalVelocity = Mathf.Sqrt(-2f * Gravity * JumpHeight);
                _trackingFall = true;
                _fallPeakY = transform.position.y;
                GameEvents.RaiseLoudNoise(transform.position);
            }

            else if (_input.JumpPressed && ParagliderUnlocked && !IsGrounded
                     && _verticalVelocity < 0.5f && Stamina.CanStartAction
                     && HeightAboveGround() > MinGlideHeight)
            {
                SetState(PlayerMotionState.Gliding);
                _verticalVelocity = GlideFallSpeed;
                return;
            }

            if (Time.time >= _climbCooldownUntil && wish.sqrMagnitude > 0.05f && TryFindClimbSurface(out Vector3 normal, out Vector3 point))
            {
                if (Stamina.CanStartAction)
                {
                    EnterClimb(normal, point);
                    return;
                }
            }

            if (ShouldSwim())
            {
                SetState(PlayerMotionState.Swimming);
                _verticalVelocity = 0f;
                return;
            }

            Vector3 motion = new Vector3(_velocity.x, _verticalVelocity, _velocity.z);
            Controller.Move(motion * Time.deltaTime);

            if (Stamina.IsExhausted && IsGrounded && PlanarSpeed < 0.2f) SetState(PlayerMotionState.Exhausted);
            else if (!IsGrounded) SetState(PlayerMotionState.Airborne);
            else if (sprinting) SetState(PlayerMotionState.Sprinting);
            else if (PlanarSpeed > 0.25f) SetState(PlayerMotionState.Walking);
            else SetState(PlayerMotionState.Idle);
        }

        private float HeightAboveGround()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                return hit.distance;
            return 999f;
        }

        private bool TryFindClimbSurface(out Vector3 normal, out Vector3 point)
        {
            normal = Vector3.zero;
            point = Vector3.zero;

            Vector3 origin = transform.position + Vector3.up * (Controller.height * 0.6f);
            Vector3 wish = CameraRelativeInput();
            Vector3 baseDir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;

            for (int i = -1; i <= 1; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 22f, Vector3.up) * baseDir;
                if (!ClimbCast(origin, dir, ClimbProbeDistance, out RaycastHit hit)) continue;
                if (!IsClimbableCollider(hit.collider)) continue;

                float angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle < MinClimbAngle || angle > 160f) continue;

                normal = hit.normal;
                point = hit.point;
                return true;
            }

            return false;
        }

        private bool TryWrapCorner(Vector3 chest, Vector3 wallRight, float stickX, bool allowOutsideCorner)
        {
            if (Mathf.Abs(stickX) < 0.15f) return false;

            Vector3 moveDir = wallRight * Mathf.Sign(stickX);
            Vector3 forward = -ClimbSurfaceNormal;

            if (Physics.Raycast(chest, moveDir, out RaycastHit inner,
                    Controller.radius + CornerProbeDistance, ~0, QueryTriggerInteraction.Ignore)
                && IsClimbable(inner))
            {
                return AttachToWall(inner);
            }

            if (!allowOutsideCorner) return false;

            for (int i = 1; i <= 4; i++)
            {
                float step = CornerProbeDistance * 0.5f * i;
                Vector3 origin = _lastWallPoint + moveDir * step + forward * step;

                if (Physics.Raycast(origin, -moveDir, out RaycastHit outer, step * 2f, ~0, QueryTriggerInteraction.Ignore)
                    && IsClimbable(outer))
                {
                    if (AttachToWall(outer)) return true;
                }
            }

            return false;
        }

        private bool IsClimbable(RaycastHit hit)
        {
            if (!IsClimbableCollider(hit.collider)) return false;

            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle < MinClimbAngle || angle > 160f) return false;

            return Vector3.Angle(hit.normal, ClimbSurfaceNormal) > 20f;
        }

        private bool AttachToWall(RaycastHit hit)
        {
            Vector3 target = hit.point + hit.normal * (Controller.radius + 0.08f);
            target.y = transform.position.y;

            if (Physics.CheckCapsule(target + Vector3.up * Controller.radius,
                                     target + Vector3.up * (Controller.height - Controller.radius),
                                     Controller.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            _lastWallPoint = hit.point;
            BeginCornerWrap(target, hit.normal);
            return true;
        }

        private void BeginCornerWrap(Vector3 target, Vector3 newNormal)
        {
            Vector3 outward = (ClimbSurfaceNormal + newNormal).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = newNormal;

            _wrapStart = transform.position;
            _wrapEnd = target;

            float distance = Vector3.Distance(_wrapStart, _wrapEnd);
            _wrapControl = (_wrapStart + _wrapEnd) * 0.5f + outward * Mathf.Min(distance * 0.5f, 0.45f);

            _wrapStartRot = transform.rotation;
            Vector3 face = -newNormal;
            face.y = 0f;
            _wrapEndRot = face.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(face, Vector3.up)
                : transform.rotation;

            ClimbSurfaceNormal = newNormal;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            _wrapTime = 0f;
            _wrapping = true;
        }

        private void TickCornerWrap()
        {
            _wrapTime += Time.deltaTime;
            float t = Mathf.Clamp01(_wrapTime / Mathf.Max(0.01f, CornerWrapDuration));
            float eased = t * t * (3f - 2f * t);

            Vector3 a = Vector3.Lerp(_wrapStart, _wrapControl, eased);
            Vector3 b = Vector3.Lerp(_wrapControl, _wrapEnd, eased);

            Controller.enabled = false;
            transform.position = Vector3.Lerp(a, b, eased);
            Controller.enabled = true;

            transform.rotation = Quaternion.Slerp(_wrapStartRot, _wrapEndRot, eased);
            ClimbVertical = 0f;

            if (!Stamina.Drain(Stamina.ClimbDrain))
            {
                _wrapping = false;
                ExitClimb(pushOff: false);
                return;
            }

            if (t >= 1f) _wrapping = false;
        }

        private void EnterClimb(Vector3 normal, Vector3 point)
        {
            ClimbSurfaceNormal = normal;
            _lastWallPoint = point;
            _wrapping = false;
            _regenLockedUntilLanding = false;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            _trackingFall = false;
            _sprintLatched = false;
            SetState(PlayerMotionState.Climbing);
        }

        private void TickClimbing()
        {
            if (_wrapping)
            {
                TickCornerWrap();
                return;
            }

            Vector3 origin = transform.position + Vector3.up * (Controller.height * 0.6f);
            bool attached = ProbeWall(origin, out RaycastHit hit);

            if (attached)
            {
                ClimbSurfaceNormal = hit.normal;

                _lastWallPoint = hit.point;

                float gap = hit.distance - (Controller.radius + 0.08f);
                if (gap > 0f)
                    Controller.Move(-hit.normal * gap * Mathf.Clamp01(Time.deltaTime * 8f));
            }

            Vector3 faceDir = -ClimbSurfaceNormal;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir, Vector3.up), 1f - Mathf.Exp(-12f * Time.deltaTime));

            Vector3 wallRight = Vector3.Normalize(Vector3.Cross(ClimbSurfaceNormal, Vector3.up));
            Vector3 wallUp = Vector3.Normalize(Vector3.Cross(wallRight, ClimbSurfaceNormal));

            Vector2 stick = _input.Move;
            ClimbVertical = stick.y;

            float up = stick.y;
            if (Time.time < _climbBlockedUpUntil) up = Mathf.Min(up, 0f);

            Vector3 climbMove = (wallRight * stick.x * ClimbSideSpeedMultiplier + wallUp * up) * ClimbSpeed;

            if (TryWrapCorner(origin, wallRight, stick.x, allowOutsideCorner: false)) return;

            if (stick.sqrMagnitude > 0.02f)
            {
                if (!Stamina.Drain(Stamina.ClimbDrain))
                {
                    ExitClimb(pushOff: false);
                    return;
                }
            }
            else
            {
                Stamina.Hold();
            }

            if (_input.JumpPressed)
            {
                if (stick.y < -0.5f)
                {
                    ExitClimb(pushOff: true);
                    return;
                }

                if (Stamina.Spend(Stamina.ClimbJumpCost))
                {
                    Controller.Move(wallUp * ClimbJumpImpulse * 0.18f);
                }
                else
                {
                    ExitClimb(pushOff: false);
                    return;
                }
            }

            Controller.Move(climbMove * Time.deltaTime);

            if (!attached)
            {
                Vector3 chest = transform.position + Vector3.up * (Controller.height * 0.6f);
                if (TryWrapCorner(chest, wallRight, stick.x, allowOutsideCorner: true)) return;
                if (TryMantle()) return;
                if (TryClingBelow(chest)) return;

                ExitClimb(pushOff: false);
                return;
            }

            if (IsGrounded && stick.y < 0f)
            {
                ExitClimb(pushOff: false);
            }
        }

        private bool ProbeWall(Vector3 origin, out RaycastHit hit)
        {
            float distance = ClimbProbeDistance + 0.4f;
            Vector3 baseDir = -ClimbSurfaceNormal;

            float holdAngle = Mathf.Max(20f, MinClimbAngle - 10f);

            for (int i = 0; i < 3; i++)
            {
                float yaw = i == 0 ? 0f : (i == 1 ? 20f : -20f);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * baseDir;

                if (!ClimbCast(origin, dir, distance, out hit)) continue;
                if (!IsClimbableCollider(hit.collider)) continue;

                float angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle >= holdAngle && angle <= 160f) return true;
            }

            hit = default;
            return false;
        }

        private bool TryClingBelow(Vector3 chest)
        {
            for (float drop = 0.2f; drop <= 0.6f; drop += 0.2f)
            {
                if (!ProbeWall(chest + Vector3.down * drop, out RaycastHit lower)) continue;

                ClimbSurfaceNormal = lower.normal;
                _lastWallPoint = lower.point;
                Controller.Move(Vector3.down * drop);
                _climbBlockedUpUntil = Time.time + 0.4f;
                return true;
            }

            return false;
        }

        private bool IsClimbableCollider(Collider collider)
        {
            if (collider == null) return false;
            if (collider.transform.IsChildOf(transform)) return false;
            return collider.GetComponentInParent<NoClimb>() == null;
        }

        private bool ClimbCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            float radius = Mathf.Min(ClimbProbeRadius, Controller.radius * 0.6f);
            float backOff = radius + 0.05f;

            return Physics.SphereCast(origin - direction * backOff, radius, direction,
                                      out hit, distance + backOff, ~0, QueryTriggerInteraction.Ignore);
        }

        private bool TryMantle()
        {
            Vector3 forward = -ClimbSurfaceNormal;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return false;
            forward.Normalize();

            Vector3 probeStart = transform.position + Vector3.up * (Controller.height + MantleHeight) + forward * (Controller.radius + 0.35f);
            if (!Physics.Raycast(probeStart, Vector3.down, out RaycastHit ground, Controller.height + MantleHeight, ~0, QueryTriggerInteraction.Ignore))
                return false;
            if (Vector3.Angle(ground.normal, Vector3.up) > 50f) return false;

            if (ground.point.y < transform.position.y + 0.3f) return false;

            Vector3 destination = ground.point + Vector3.up * 0.1f;
            if (Physics.CheckCapsule(destination + Vector3.up * Controller.radius,
                                     destination + Vector3.up * (Controller.height - Controller.radius),
                                     Controller.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            Controller.enabled = false;
            transform.position = destination;
            Controller.enabled = true;

            _velocity = Vector3.zero;
            _verticalVelocity = -1f;
            _trackingFall = false;
            _climbCooldownUntil = Time.time + 0.35f;
            SetState(PlayerMotionState.Idle);
            return true;
        }

        private void ExitClimb(bool pushOff)
        {
            ClimbVertical = 0f;
            _wrapping = false;

            _regenLockedUntilLanding = true;
            _climbCooldownUntil = Time.time + 0.35f;
            _verticalVelocity = pushOff ? 1.5f : 0f;
            if (pushOff) _velocity = ClimbSurfaceNormal * 2.5f;
            _trackingFall = true;
            _fallPeakY = transform.position.y;
            SetState(PlayerMotionState.Airborne);
        }

        private bool ShouldSwim()
        {
            if (_water == null) return false;

            float chestY = transform.position.y + Controller.height * 0.55f;
            return chestY < _water.SurfaceY;
        }

        private void TickSwimming()
        {
            if (_water == null || !ShouldSwim())
            {
                SetState(PlayerMotionState.Idle);
                return;
            }

            Vector3 wish = CameraRelativeInput();
            bool wantsSprint = _input.SprintHeld && wish.sqrMagnitude > 0.2f;
            float speed = SwimSpeed;

            float drain = Stamina.SwimDrain * (wantsSprint ? 2.1f : 1f);
            bool hasStamina = Stamina.Drain(drain);
            if (wantsSprint && hasStamina) speed = SwimSprintSpeed;

            if (!hasStamina)
            {
                Health?.TakeDamage(DrownDamagePerSecond * Time.deltaTime, ignoreInvulnerability: true);
                speed *= 0.4f;
            }

            FaceDirection(wish, TurnSharpness * 0.6f);

            Vector3 planar = Vector3.Lerp(new Vector3(_velocity.x, 0f, _velocity.z), wish * speed, 1f - Mathf.Exp(-6f * Time.deltaTime));
            _velocity.x = planar.x;
            _velocity.z = planar.z;

            float targetY = _water.SurfaceY - SwimBuoyancyOffset;
            float error = targetY - transform.position.y;
            _verticalVelocity = Mathf.Clamp(error * BuoyancyStiffness, -3f, 3f);

            Controller.Move(new Vector3(_velocity.x, _verticalVelocity, _velocity.z) * Time.deltaTime);
            SetState(PlayerMotionState.Swimming);
        }

        private void TickGliding()
        {
            _regenLockedUntilLanding = true;

            if (IsGrounded || ShouldSwim())
            {
                _verticalVelocity = -2f;
                SetState(PlayerMotionState.Idle);
                return;
            }

            if (!Stamina.Drain(Stamina.GlideDrain) || _input.JumpPressed)
            {
                _trackingFall = true;
                _fallPeakY = transform.position.y;
                SetState(PlayerMotionState.Airborne);
                return;
            }

            Vector3 wish = CameraRelativeInput();
            FaceDirection(wish.sqrMagnitude > 0.01f ? wish : transform.forward, 6f);

            Vector3 target = (wish.sqrMagnitude > 0.01f ? wish : transform.forward) * GlideSpeed;
            Vector3 planar = Vector3.Lerp(new Vector3(_velocity.x, 0f, _velocity.z), target, 1f - Mathf.Exp(-3f * Time.deltaTime));
            _velocity.x = planar.x;
            _velocity.z = planar.z;

            _verticalVelocity = Mathf.Lerp(_verticalVelocity, GlideFallSpeed, 1f - Mathf.Exp(-8f * Time.deltaTime));
            Controller.Move(new Vector3(_velocity.x, _verticalVelocity, _velocity.z) * Time.deltaTime);

            _trackingFall = false;
            SetState(PlayerMotionState.Gliding);
        }

        private void UpdateInteractionFocus()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, InteractRadius, _overlapBuffer, ~0, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider c = _overlapBuffer[i];
                if (c == null) continue;
                IInteractable candidate = c.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.IsAvailable) continue;

                float d = (candidate.InteractPoint - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }

            if (!ReferenceEquals(best, _focus))
            {
                _focus = best;
                GameEvents.RaiseInteractPrompt(best != null ? best.Prompt : null);
            }
        }

        public void SetWater(WaterVolume water) => _water = water;

        public void ClearWater(WaterVolume water)
        {
            if (_water == water) _water = null;
        }

        public void Teleport(Vector3 position)
        {
            Controller.enabled = false;
            transform.position = position;
            Controller.enabled = true;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            _trackingFall = false;
            _wrapping = false;
            _regenLockedUntilLanding = false;
            _water = null;
            SetState(PlayerMotionState.Idle);
        }

        public void Knockback(Vector3 direction, float force)
        {
            direction.y = 0f;
            _velocity += direction.normalized * force;
            _verticalVelocity = Mathf.Max(_verticalVelocity, force * 0.35f);
            if (State == PlayerMotionState.Climbing) ExitClimb(pushOff: true);
        }

        private void OnDied()
        {
            _dead = true;
            SetState(PlayerMotionState.Dead);
        }

        public void Revive()
        {
            _dead = false;
            SetState(PlayerMotionState.Idle);
        }
    }
}
