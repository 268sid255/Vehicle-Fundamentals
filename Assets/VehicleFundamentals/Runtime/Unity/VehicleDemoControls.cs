using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>
    /// Demo shortcuts: R resets the car, C switches the camera.
    /// With the new Input System the shortcuts come from VehiclePlayerInput events; the legacy
    /// path stays as a fallback for projects without the package.
    /// </summary>
    public sealed class VehicleDemoControls : MonoBehaviour
    {
        public RaycastVehicleController controller;
        public VehiclePlayerInput playerInput;
        public VehicleCinemachineRig cameraRig;

        [Header("Legacy fallback")]
        public KeyCode resetKey = KeyCode.R;
        public KeyCode cameraKey = KeyCode.C;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<RaycastVehicleController>();
            if (playerInput == null) playerInput = GetComponentInParent<VehiclePlayerInput>();

            if (controller != null)
            {
                startPosition = controller.transform.position;
                startRotation = controller.transform.rotation;
            }
        }

        private void OnEnable()
        {
            if (playerInput == null) return;
            playerInput.ResetPressed += ResetVehicle;
            playerInput.CameraPressed += CycleCamera;
        }

        private void OnDisable()
        {
            if (playerInput == null) return;
            playerInput.ResetPressed -= ResetVehicle;
            playerInput.CameraPressed -= CycleCamera;
        }

        private void Update()
        {
#if !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            if (playerInput != null) return;
            if (Input.GetKeyDown(resetKey)) ResetVehicle();
            if (Input.GetKeyDown(cameraKey)) CycleCamera();
#endif
        }

        public void ResetVehicle()
        {
            if (controller != null) controller.Teleport(startPosition, startRotation);
        }

        public void CycleCamera()
        {
            if (cameraRig != null) cameraRig.CycleMode();
        }
    }
}
