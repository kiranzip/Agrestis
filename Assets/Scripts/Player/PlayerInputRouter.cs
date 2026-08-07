using UnityEngine;
using UnityEngine.InputSystem;

namespace Agrestis.Player
{
    public class PlayerInputRouter : MonoBehaviour
    {
        [Header("Look sensitivity")]
        public float MouseSensitivity = 0.12f;
        public float GamepadSensitivity = 220f;
        public bool InvertY = false;

        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private InputAction _sprint;
        private InputAction _interact;
        private InputAction _attack;
        private InputAction _pause;

        public Vector2 Move { get; private set; }

        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool PausePressed { get; private set; }

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector")
                 .With("Up", "<Keyboard>/w")
                 .With("Down", "<Keyboard>/s")
                 .With("Left", "<Keyboard>/a")
                 .With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                 .With("Up", "<Keyboard>/upArrow")
                 .With("Down", "<Keyboard>/downArrow")
                 .With("Left", "<Keyboard>/leftArrow")
                 .With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _look = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            _look.AddBinding("<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick");

            _jump = new InputAction("Jump", InputActionType.Button);
            _jump.AddBinding("<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _sprint = new InputAction("Sprint", InputActionType.Button);
            _sprint.AddBinding("<Keyboard>/leftShift");
            _sprint.AddBinding("<Gamepad>/leftStickPress");

            _interact = new InputAction("Interact", InputActionType.Button);
            _interact.AddBinding("<Keyboard>/e");
            _interact.AddBinding("<Gamepad>/buttonWest");

            _attack = new InputAction("Attack", InputActionType.Button);
            _attack.AddBinding("<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/rightTrigger");

            _pause = new InputAction("Pause", InputActionType.Button);
            _pause.AddBinding("<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");
        }

        private void OnEnable()
        {
            _move.Enable();
            _look.Enable();
            _jump.Enable();
            _sprint.Enable();
            _interact.Enable();
            _attack.Enable();
            _pause.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            _move.Disable();
            _look.Disable();
            _jump.Disable();
            _sprint.Disable();
            _interact.Disable();
            _attack.Disable();
            _pause.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);

            Vector2 raw = _look.ReadValue<Vector2>();
            bool fromGamepad = _look.activeControl != null && _look.activeControl.device is Gamepad;
            Vector2 look = fromGamepad
                ? raw * GamepadSensitivity * Time.unscaledDeltaTime
                : raw * MouseSensitivity;
            if (InvertY) look.y = -look.y;
            Look = look;

            SprintHeld = _sprint.IsPressed();
            JumpHeld = _jump.IsPressed();
            JumpPressed = _jump.WasPressedThisFrame();
            InteractPressed = _interact.WasPressedThisFrame();
            AttackPressed = _attack.WasPressedThisFrame();
            PausePressed = _pause.WasPressedThisFrame();
        }

        private void OnDestroy()
        {
            _move?.Dispose();
            _look?.Dispose();
            _jump?.Dispose();
            _sprint?.Dispose();
            _interact?.Dispose();
            _attack?.Dispose();
            _pause?.Dispose();
        }
    }
}
