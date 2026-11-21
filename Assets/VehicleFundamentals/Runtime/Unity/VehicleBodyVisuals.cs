using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>Instantiates imported vehicle prefabs and binds their wheel rig to the simulation.</summary>
    [RequireComponent(typeof(RaycastVehicleController), typeof(VehicleWheelVisuals))]
    public sealed class VehicleBodyVisuals : MonoBehaviour
    {
        public GameObject[] models;
        public GameObject[] driftModels;
        private RaycastVehicleController controller;
        private Vehicle builtVehicle;

        private void Awake() => controller = GetComponent<RaycastVehicleController>();
        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<RaycastVehicleController>();
            controller.VehicleRebuilt += Rebuild;
        }
        private void Start() { if (controller.Vehicle != builtVehicle) Rebuild(); }
        private void OnDisable() { if (controller != null) controller.VehicleRebuilt -= Rebuild; }

        public void Rebuild()
        {
            if (controller == null) controller = GetComponent<RaycastVehicleController>();
            Vehicle vehicle = controller.Vehicle;
            if (vehicle == null) return;
            Build(vehicle.Settings, controller.Definition != null ? controller.Definition.preset : controller.Preset,
                controller.Definition != null ? controller.Definition.visualVariant : 0);
            builtVehicle = vehicle;
        }

        public void Build(VehicleSettings settings, VehiclePreset preset, int variant = 0)
        {
            if (models == null || (int)preset >= models.Length || models[(int)preset] == null)
            {
                Debug.LogError("No imported visual prefab for " + preset, this);
                return;
            }
            Transform old = transform.Find("Vehicle Model");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            GameObject template = preset == VehiclePreset.Drift && driftModels != null && driftModels.Length > 0
                ? driftModels[Mathf.Clamp(variant, 0, driftModels.Length - 1)] : models[(int)preset];
            GameObject instance = Instantiate(template, transform, false);
            instance.name = "Vehicle Model";
            var rig = instance.GetComponent<VehicleVisualPrefab>();
            AxleSettings front = settings.axles[0], rear = settings.axles[settings.axles.Count - 1];
            float x = front.trackWidth / rig.trackWidth;
            float y = front.wheelRadius / rig.wheelRadii[0];
            float z = (front.position - rear.position) / (rig.frontAxle - rig.rearAxle);
            float restHeight = front.mountHeight - front.suspensionTravel + front.staticWheelLoad / front.springRate;
            Vector3 offset = new Vector3(0, restHeight - rig.frontRestHeight * y, rear.position - rig.rearAxle * z);
            rig.body.localScale = new Vector3(x, y, z);
            rig.body.localPosition = offset;
            var visuals = GetComponent<VehicleWheelVisuals>();
            visuals.wheels = new Transform[settings.axles.Count * 2];
            for (int i = 0; i < visuals.wheels.Length; i++)
            {
                int source = Mathf.Min(i / 2, rig.wheels.Length / 2 - 1) * 2 + i % 2;
                Transform wheel = i < rig.wheels.Length ? rig.wheels[i] : Instantiate(rig.wheels[source], instance.transform, false);
                wheel.name = "Wheel " + i;
                AxleSettings axle = settings.axles[i / 2];
                wheel.localScale = Vector3.one * (axle.wheelRadius / rig.wheelRadii[source]);
                wheel.localPosition = new Vector3((i % 2 == 0 ? -0.5f : 0.5f) * axle.trackWidth,
                    axle.mountHeight - axle.suspensionTravel + axle.staticWheelLoad / axle.springRate, axle.position);
                visuals.wheels[i] = wheel;
            }
            for (int i = visuals.wheels.Length; i < rig.wheels.Length; i++) rig.wheels[i].gameObject.SetActive(false);
            visuals.axleVisuals = rig.axleVisuals;
            if (rig.axleVisuals != null)
                for (int i = 0; i < Mathf.Min(rig.axleVisuals.Length, settings.axles.Count); i++)
                {
                    Transform visual = rig.axleVisuals[i];
                    if (visual == null) continue;
                    AxleSettings axle = settings.axles[i];
                    visual.localPosition = new Vector3(0, axle.mountHeight - axle.suspensionTravel + axle.staticWheelLoad / axle.springRate, axle.position);
                    visual.localScale = new Vector3(x, y, z);
                }
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.center = Vector3.Scale(rig.colliderCenter, new Vector3(x, y, z)) + offset;
                collider.size = Vector3.Scale(rig.colliderSize, new Vector3(x, y, z));
            }
            if (controller == null) controller = GetComponent<RaycastVehicleController>();
            controller.RefreshInertiaTensor();
        }
    }
}
