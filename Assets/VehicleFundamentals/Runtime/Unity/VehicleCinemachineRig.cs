using Unity.Cinemachine;
using UnityEngine;

namespace VehicleFundamentals
{
    public enum CameraMode
    {
        Chase,
        Hood,
    }

    /// <summary>
    /// Cinemachine 3 camera rig for the sandbox. Two CinemachineCameras look at the car: a chase
    /// camera that stays level with the world and a hood camera locked to the body. Cycling the
    /// mode swaps their priorities, so the CinemachineBrain blends between the two shots instead of
    /// snapping. The lens widens with speed, the chase camera looks further ahead, and a teleport
    /// (reset) warps both cameras so they do not fly across the map.
    /// </summary>
    public sealed class VehicleCinemachineRig : MonoBehaviour
    {
        public RaycastVehicleController controller;
        public CinemachineCamera chaseCamera;
        public CinemachineCamera hoodCamera;
        public CameraMode mode = CameraMode.Chase;

        [Header("Lens")]
        public float chaseFov = 58f;
        public float hoodFov = 66f;
        [Tooltip("Extra field of view at top speed, degrees.")]
        public float fovPerSpeed = 12f;
        public float topSpeedKmh = 180f;

        [Header("Look ahead")]
        [Tooltip("Lookahead time at a standstill, seconds.")]
        public float lookaheadIdle = 0.08f;
        [Tooltip("Lookahead time at top speed, seconds.")]
        public float lookaheadFast = 0.22f;

        [Header("Priorities")]
        public int livePriority = 20;
        public int idlePriority = 5;

        private CinemachineRotationComposer chaseComposer;
        private Vector3 lastTargetPosition;
        private bool hasTargetPosition;
        private Vehicle framedVehicle;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<RaycastVehicleController>();
            if (chaseCamera != null)
                chaseComposer = chaseCamera.GetComponent<CinemachineRotationComposer>();
            ApplyMode();
        }

        private void OnEnable()
        {
            ApplyMode();
            hasTargetPosition = false;
        }

        /// <summary>Switches between the chase and the hood camera; the brain blends the two.</summary>
        public void CycleMode()
        {
            mode = mode == CameraMode.Chase ? CameraMode.Hood : CameraMode.Chase;
            ApplyMode();
        }

        private void ApplyMode()
        {
            if (chaseCamera != null)
                chaseCamera.Priority = mode == CameraMode.Chase ? livePriority : idlePriority;
            if (hoodCamera != null)
                hoodCamera.Priority = mode == CameraMode.Hood ? livePriority : idlePriority;
        }

        private void LateUpdate()
        {
            if (controller == null) return;

            Transform target = controller.transform;
            ApplyVehicleSize();
            WarpIfTeleported(target);
            ApplySpeed(target);
        }

        private void ApplyVehicleSize()
        {
            Vehicle vehicle = controller.Vehicle;
            if (vehicle == null || vehicle == framedVehicle) return;
            framedVehicle = vehicle;
            BoxCollider shape = controller.GetComponent<BoxCollider>();
            if (shape == null) return;
            float roof = shape.center.y + shape.size.y * 0.5f;
            float front = shape.center.z + shape.size.z * 0.5f;
            if (chaseCamera != null)
            {
                CinemachineFollow follow = chaseCamera.GetComponent<CinemachineFollow>();
                if (follow != null) follow.FollowOffset = new Vector3(0, Mathf.Max(2.35f, roof + 1.1f),
                    shape.center.z - Mathf.Max(6.4f, shape.size.z * 1.5f));
                if (chaseComposer != null) chaseComposer.TargetOffset = new Vector3(0, roof * 0.65f, shape.center.z);
            }
            if (hoodCamera != null)
            {
                CinemachineFollow follow = hoodCamera.GetComponent<CinemachineFollow>();
                if (follow != null) follow.FollowOffset = new Vector3(0, roof + 0.12f, front - 0.25f);
                var composer = hoodCamera.GetComponent<CinemachineRotationComposer>();
                if (composer != null) composer.TargetOffset = new Vector3(0, roof + 0.05f, front + 18);
            }
        }

        private void WarpIfTeleported(Transform target)
        {
            Vector3 position = target.position;
            if (hasTargetPosition)
            {
                Vector3 delta = position - lastTargetPosition;
                // A reset moves the car tens of metres in one frame; damping alone would fly there.
                if (delta.sqrMagnitude > 9f) Warp(target, delta);
            }
            lastTargetPosition = position;
            hasTargetPosition = true;
        }

        private void Warp(Transform target, Vector3 delta)
        {
            if (chaseCamera != null) chaseCamera.OnTargetObjectWarped(target, delta);
            if (hoodCamera != null) hoodCamera.OnTargetObjectWarped(target, delta);
        }

        private void ApplySpeed(Transform target)
        {
            Vehicle vehicle = controller.Vehicle;
            float speed = vehicle != null ? Mathf.Abs(vehicle.SpeedForward) * 3.6f : 0f;
            float norm = Mathf.Clamp01(speed / Mathf.Max(1f, topSpeedKmh));

            if (chaseCamera != null)
            {
                LensSettings lens = chaseCamera.Lens;
                lens.FieldOfView = chaseFov + fovPerSpeed * norm;
                chaseCamera.Lens = lens;
            }

            if (hoodCamera != null)
            {
                LensSettings lens = hoodCamera.Lens;
                lens.FieldOfView = hoodFov + fovPerSpeed * 0.6f * norm;
                hoodCamera.Lens = lens;
            }

            if (chaseComposer != null)
            {
                LookaheadSettings lookahead = chaseComposer.Lookahead;
                lookahead.Enabled = true;
                lookahead.Time = Mathf.Lerp(lookaheadIdle, lookaheadFast, norm);
                chaseComposer.Lookahead = lookahead;
            }
        }
    }
}
