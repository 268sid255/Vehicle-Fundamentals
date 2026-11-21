using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>
    /// Places the visible wheels at their mounts minus the spring length, steers them smoothly at
    /// 200 deg/s and turns them by their shown spin (Visual wheels, Wheel spin and inertia).
    /// Runs in the rendered frame, never in the physics step.
    /// </summary>
    public sealed class VehicleWheelVisuals : MonoBehaviour
    {
        public RaycastVehicleController controller;

        [Tooltip("Axle order, left then right for every axle. Leave empty to find the controller's children.")]
        public Transform[] wheels;
        public Transform[] axleVisuals;

        public bool updateVisuals = true;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<RaycastVehicleController>();
            TryAutoAssign();
        }

        private void LateUpdate()
        {
            if (controller == null) controller = GetComponentInParent<RaycastVehicleController>();
            Vehicle vehicle = controller != null ? controller.Vehicle : null;
            if (vehicle == null) return;
            TryAutoAssign();
            if (wheels == null || wheels.Length == 0) return;

            float frameDt = Time.deltaTime;
            int count = Mathf.Min(wheels.Length, vehicle.Wheels.Count);
            for (int i = 0; i < count; i++)
            {
                Wheel wheel = vehicle.Wheels[i];
                Transform visual = wheels[i];
                if (visual == null) continue;

                if (updateVisuals) wheel.UpdateVisual(frameDt);
                visual.localPosition = wheel.LocalMount.ToUnity() + Vector3.down * wheel.SpringLength;
                visual.localRotation = Quaternion.Euler(0f, wheel.VisualSteerAngleDeg, 0f)
                    * Quaternion.Euler(wheel.SpinAngleDeg, 0f, 0f);
            }
            if (axleVisuals == null) return;
            for (int i = 0; i < Mathf.Min(axleVisuals.Length, vehicle.Axles.Count); i++)
            {
                Transform visual = axleVisuals[i];
                if (visual == null) continue;
                Axle axle = vehicle.Axles[i];
                Vector3 left = axle.Left.LocalMount.ToUnity() + Vector3.down * axle.Left.SpringLength;
                Vector3 right = axle.Right.LocalMount.ToUnity() + Vector3.down * axle.Right.SpringLength;
                visual.localPosition = (left + right) * 0.5f;
                float roll = Mathf.Atan2(right.y - left.y, right.x - left.x) * Mathf.Rad2Deg;
                visual.localRotation = Quaternion.Euler(0f, 0f, roll);
            }
        }

        private void TryAutoAssign()
        {
            if (wheels != null && wheels.Length > 0) return;
            if (controller == null || controller.Vehicle == null) return;

            int count = controller.Vehicle.Wheels.Count;
            wheels = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                Transform child = controller.transform.Find("Wheel " + i);
                if (child != null) wheels[i] = child;
            }
        }
    }
}
