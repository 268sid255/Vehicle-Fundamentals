using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>
    /// Runs the pure C# controller in FixedUpdate on a Rigidbody (Fixed time step, Unity).
    /// The vehicle is built from a VehicleDefinition asset, or from a preset when none is assigned;
    /// the wheel rays use Physics.RaycastNonAlloc.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RaycastVehicleController : MonoBehaviour
    {
        [Header("Vehicle")]
        [Tooltip("Reusable vehicle asset. When empty, the preset below is used as a fallback.")]
        [SerializeField] private VehicleDefinition definition;
        [Tooltip("Fallback when no Vehicle Definition is assigned.")]
        [SerializeField] private VehiclePreset preset = VehiclePreset.SportsCoupe;

        [Header("Assists (0 = off, 1 = full)")]
        [SerializeField] private AssistSettings assists = new AssistSettings();

        [Header("Ground")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Rigidbody")]
        [SerializeField] private bool configureRigidbody = true;
        [SerializeField] private bool autoFindInputSource = true;

        [SerializeField] private Rigidbody body;

        private readonly UnityVehiclePhysics physicsWorld = new UnityVehiclePhysics();
        private IVehicleInputSource inputSource;
        private Vehicle vehicle;

        public Vehicle Vehicle => vehicle;
        public Rigidbody Body => body;
        public VehicleDefinition Definition => definition;
        public VehiclePreset Preset => preset;
        public AssistSettings Assists => assists;
        public event System.Action VehicleRebuilt;

        /// <summary>Assigns a reusable vehicle asset; rebuilds immediately in play mode.</summary>
        public void SetDefinition(VehicleDefinition newDefinition, bool rebuild = true)
        {
            definition = newDefinition;
            if (rebuild && Application.isPlaying) Rebuild();
        }

        /// <summary>Builds a fresh vehicle from the definition, or from the preset fallback.</summary>
        [ContextMenu("Rebuild Vehicle")]
        public void Rebuild()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body == null) return;

            physicsWorld.groundMask = groundMask;
            vehicle = definition != null
                ? definition.Build(physicsWorld, assists)
                : VehiclePresets.Create(preset, physicsWorld);
            vehicle.Body = body;
            SyncAssists();

            if (configureRigidbody) ConfigureBody();
            VehicleRebuilt?.Invoke();
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body == null)
            {
                Debug.LogError("RaycastVehicleController needs a Rigidbody.", this);
                enabled = false;
                return;
            }

            physicsWorld.groundMask = groundMask;
            if (autoFindInputSource) FindInputSource();
            Rebuild();
        }

        private void FixedUpdate()
        {
            if (vehicle == null) return;
            SyncAssists();

            float dt = Time.fixedDeltaTime;
            VehicleInput input = inputSource != null
                ? inputSource.ReadInput(vehicle, dt)
                : vehicle.Input;
            vehicle.Simulate(input, dt);
        }

        private void ConfigureBody()
        {
            body.mass = vehicle.Settings.mass;
            body.centerOfMass = vehicle.Settings.centerOfMass.ToUnity();
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (body.maxAngularVelocity < 20f) body.maxAngularVelocity = 20f;

            // Air drag is the controller's own force, not Unity's linear damping.
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = 0f;
            body.angularDamping = 0f;
#else
            body.drag = 0f;
            body.angularDrag = 0f;
#endif
        }

        /// <summary>Updates automatic inertia after a body collider changes while preserving manual Rigidbody settings.</summary>
        internal void RefreshInertiaTensor()
        {
            if (configureRigidbody && body != null) body.ResetInertiaTensor();
        }

        private void SyncAssists()
        {
            if (vehicle == null) return;
            AssistSettings target = vehicle.Assists;
            target.steering = assists.steering;
            target.traction = assists.traction;
            target.drift = assists.drift;
            target.brakes = assists.brakes;
            target.Validate();
        }

        private void FindInputSource()
        {
            inputSource = null;
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IVehicleInputSource source)
                {
                    inputSource = source;
                    return;
                }
            }
        }

        public void SetPreset(VehiclePreset newPreset, bool rebuild = true)
        {
            definition = null;
            preset = newPreset;
            if (rebuild && Application.isPlaying) Rebuild();
        }

        /// <summary>Put the body at a pose with zero velocity and forget every assist state and gear.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body == null) return;

            body.position = position;
            body.rotation = rotation;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);

            if (vehicle != null) vehicle.ResetToStandstill();
            if (inputSource != null) inputSource.ResetToStandstill();
        }

        public void ResetToStandstill()
        {
            if (vehicle != null) vehicle.ResetToStandstill();
            if (inputSource != null) inputSource.ResetToStandstill();
        }

        private void Reset()
        {
            if (body == null) body = GetComponent<Rigidbody>();
        }

        private void OnValidate()
        {
            if (assists == null) assists = new AssistSettings();
            assists.Validate();
            if (Application.isPlaying) physicsWorld.groundMask = groundMask;
        }

        private void OnDrawGizmosSelected()
        {
            if (vehicle == null) return;
            for (int i = 0; i < vehicle.Wheels.Count; i++)
            {
                Wheel wheel = vehicle.Wheels[i];
                Vector3 mount = transform.TransformPoint(wheel.LocalMount.ToUnity());
                Gizmos.color = wheel.IsGrounded ? Color.green : Color.red;
                if (wheel.IsGrounded)
                {
                    Gizmos.DrawSphere(wheel.ContactPoint.ToUnity(), 0.06f);
                    Gizmos.DrawLine(mount, wheel.ContactPoint.ToUnity());
                }
                else
                {
                    Gizmos.DrawLine(mount, mount + Vector3.down * wheel.Radius);
                }
            }
        }
    }
}
