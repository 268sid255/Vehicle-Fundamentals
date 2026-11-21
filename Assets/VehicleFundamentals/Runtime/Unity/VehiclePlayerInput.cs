using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VehicleFundamentals
{
    /// <summary>
    /// The player input source for the new Input System: keyboard and gamepad through actions,
    /// with the documented pedal ramps for digital keys and direct analog values for a gamepad.
    /// It also raises the demo shortcuts (reset, camera, HUD, template selection) so the rest of
    /// the demo does not need to read devices itself. A legacy fallback keeps the component usable
    /// when the Input System package is not installed.
    /// </summary>
    public sealed class VehiclePlayerInput : MonoBehaviour, IVehicleInputSource
    {
        [Header("Manual override (replay, AI, tests)")]
        public bool useManualInput;
        public VehicleInput manualInput;

        [Header("Gamepad")]
        public bool useGamepad = true;
        [Range(0.05f, 0.5f)] public float gamepadDeadzone = 0.15f;

        [Header("Legacy fallback")]
        public KeyCode throttleKey = KeyCode.W;
        public KeyCode brakeKey = KeyCode.S;
        public KeyCode handbrakeKey = KeyCode.Space;
        public KeyCode steerLeftKey = KeyCode.A;
        public KeyCode steerRightKey = KeyCode.D;
        public KeyCode resetKey = KeyCode.R;
        public KeyCode cameraKey = KeyCode.C;
        public KeyCode hudKey = KeyCode.H;

        public event Action ResetPressed;
        public event Action CameraPressed;
        public event Action HudPressed;
        public event Action PreviousTemplatePressed;
        public event Action NextTemplatePressed;
        public event Action<int> TemplatePressed;

        public VehiclePedalRamps Ramps { get; } = new VehiclePedalRamps();

        public bool ActionsEnabled
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return throttleAction != null && throttleAction.enabled;
#else
                return false;
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        private InputAction throttleAction;
        private InputAction brakeAction;
        private InputAction handbrakeAction;
        private InputAction steerAction;
        private InputAction resetAction;
        private InputAction cameraAction;
        private InputAction hudAction;
        private InputAction previousTemplateAction;
        private InputAction nextTemplateAction;
        private InputAction[] templateActions;

        private void Awake()
        {
            CreateActions();
        }

        private void OnEnable()
        {
            EnableActions(true);
        }

        private void OnDisable()
        {
            EnableActions(false);
        }

        private void OnDestroy()
        {
            DisposeActions();
        }

        private void CreateActions()
        {
            if (throttleAction != null) return;

            throttleAction = Value("Throttle", "<Keyboard>/w", "<Gamepad>/rightTrigger");
            brakeAction = Value("Brake", "<Keyboard>/s", "<Gamepad>/leftTrigger");
            handbrakeAction = Value("Handbrake", "<Keyboard>/space", "<Gamepad>/buttonSouth");

            steerAction = new InputAction("Steer", InputActionType.Value);
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            steerAction.AddBinding("<Gamepad>/leftStick/x");

            resetAction = Button("Reset", "<Keyboard>/r", "<Gamepad>/select",
                () => ResetPressed?.Invoke());
            cameraAction = Button("Camera", "<Keyboard>/c", "<Gamepad>/buttonNorth",
                () => CameraPressed?.Invoke());
            hudAction = Button("Hud", "<Keyboard>/h", "<Gamepad>/buttonWest",
                () => HudPressed?.Invoke());
            previousTemplateAction = Button("Previous Template", "<Keyboard>/leftBracket",
                "<Gamepad>/leftShoulder", () => PreviousTemplatePressed?.Invoke());
            nextTemplateAction = Button("Next Template", "<Keyboard>/rightBracket",
                "<Gamepad>/rightShoulder", () => NextTemplatePressed?.Invoke());

            templateActions = new InputAction[9];
            for (int i = 0; i < templateActions.Length; i++)
            {
                int number = i + 1;
                templateActions[i] = Button("Template " + number, "<Keyboard>/" + number, null,
                    () => TemplatePressed?.Invoke(number));
            }
        }

        private static InputAction Value(string name, string keyboardBinding, string gamepadBinding)
        {
            var action = new InputAction(name, InputActionType.Value);
            action.AddBinding(keyboardBinding);
            action.AddBinding(gamepadBinding);
            return action;
        }

        private static InputAction Button(string name, string keyboardBinding, string gamepadBinding,
            Action callback)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(keyboardBinding);
            if (!string.IsNullOrEmpty(gamepadBinding)) action.AddBinding(gamepadBinding);
            action.performed += _ => callback();
            return action;
        }

        private void EnableActions(bool enable)
        {
            if (throttleAction == null) return;

            InputAction[] actions =
            {
                throttleAction, brakeAction, handbrakeAction, steerAction,
                resetAction, cameraAction, hudAction, previousTemplateAction, nextTemplateAction,
            };

            for (int i = 0; i < actions.Length; i++)
            {
                if (enable) actions[i].Enable();
                else actions[i].Disable();
            }

            for (int i = 0; i < templateActions.Length; i++)
            {
                if (enable) templateActions[i].Enable();
                else templateActions[i].Disable();
            }
        }

        private void DisposeActions()
        {
            if (throttleAction == null) return;

            throttleAction.Dispose();
            brakeAction.Dispose();
            handbrakeAction.Dispose();
            steerAction.Dispose();
            resetAction.Dispose();
            cameraAction.Dispose();
            hudAction.Dispose();
            previousTemplateAction.Dispose();
            nextTemplateAction.Dispose();

            for (int i = 0; i < templateActions.Length; i++) templateActions[i].Dispose();
            templateActions = null;
            throttleAction = null;
        }
#endif

        private void Update()
        {
#if !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(resetKey)) ResetPressed?.Invoke();
            if (Input.GetKeyDown(cameraKey)) CameraPressed?.Invoke();
            if (Input.GetKeyDown(hudKey)) HudPressed?.Invoke();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) PreviousTemplatePressed?.Invoke();
            if (Input.GetKeyDown(KeyCode.RightBracket)) NextTemplatePressed?.Invoke();
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) TemplatePressed?.Invoke(i + 1);
            }
#endif
        }

        public VehicleInput ReadInput(Vehicle vehicle, float dt)
        {
            if (useManualInput) return manualInput.Clamped();

#if ENABLE_INPUT_SYSTEM
            if (vehicle == null) return VehicleInput.None;

            if (useGamepad && IsGamepadDriving())
            {
                return Ramps.UpdateAnalog(
                    vehicle,
                    throttleAction.ReadValue<float>(),
                    brakeAction.ReadValue<float>(),
                    handbrakeAction.ReadValue<float>(),
                    steerAction.ReadValue<float>(),
                    dt);
            }

            float steer = steerAction.ReadValue<float>();
            return Ramps.Update(
                vehicle,
                throttleAction.IsPressed(),
                brakeAction.IsPressed(),
                handbrakeAction.IsPressed(),
                steer <= -0.5f,
                steer >= 0.5f,
                dt);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Ramps.Update(
                vehicle,
                Input.GetKey(throttleKey),
                Input.GetKey(brakeKey),
                Input.GetKey(handbrakeKey),
                Input.GetKey(steerLeftKey),
                Input.GetKey(steerRightKey),
                dt);
#else
            return VehicleInput.None;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private bool IsGamepadDriving()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return false;

            return pad.leftStick.ReadValue().magnitude > gamepadDeadzone ||
                   pad.rightTrigger.ReadValue() > gamepadDeadzone ||
                   pad.leftTrigger.ReadValue() > gamepadDeadzone;
        }
#endif

        public void ResetToStandstill()
        {
            Ramps.ResetToStandstill();
        }
    }
}
