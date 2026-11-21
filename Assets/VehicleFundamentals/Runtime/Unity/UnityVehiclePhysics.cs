using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>
    /// The Unity adapter for the four physics calls of the documentation (Porting to your game engine,
    /// Unity). The simulation never reads the interpolated transform: it reads the Rigidbody's own pose.
    /// </summary>
    public sealed class UnityVehiclePhysics : IPhysicsWorld
    {
        private RaycastHit[] hits = new RaycastHit[16];

        /// <summary>Layers the wheels stand on (RaycastVehicleController.groundMask).</summary>
        public LayerMask groundMask = ~0;

        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        public GroundHit? Raycast(Vec3 origin, Vec3 direction, float maxDistance, object ignoreBody)
        {
            Vector3 o = origin.ToUnity();
            Vector3 d = direction.ToUnity();
            int count = Physics.RaycastNonAlloc(o, d, hits, maxDistance, groundMask, triggerInteraction);
            // NonAlloc results are unordered. A full buffer may omit the nearest road surface.
            while (count == hits.Length)
            {
                System.Array.Resize(ref hits, hits.Length * 2);
                count = Physics.RaycastNonAlloc(o, d, hits, maxDistance, groundMask, triggerInteraction);
            }
            Rigidbody ignore = ignoreBody as Rigidbody;

            float nearest = float.PositiveInfinity;
            GroundHit? result = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;
                if (ignore != null && hit.rigidbody == ignore) continue; // the car's own collider
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                result = new GroundHit(hit.distance, hit.point.ToCore(), hit.normal.ToCore(),
                    hit.rigidbody);
            }
            return result;
        }

        public Vec3 Position(object body)
        {
            Rigidbody rigidbody = body as Rigidbody;
            return rigidbody != null ? rigidbody.position.ToCore() : Vec3.Zero;
        }

        public Quat Rotation(object body)
        {
            Rigidbody rigidbody = body as Rigidbody;
            return rigidbody != null ? rigidbody.rotation.ToCore() : Quat.Identity;
        }

        public Vec3 Velocity(object body)
        {
            Rigidbody rigidbody = body as Rigidbody;
            if (rigidbody == null) return Vec3.Zero;
#if UNITY_6000_0_OR_NEWER
            return rigidbody.linearVelocity.ToCore();
#else
            return rigidbody.velocity.ToCore();
#endif
        }

        public Vec3 AngularVelocity(object body)
        {
            Rigidbody rigidbody = body as Rigidbody;
            return rigidbody != null ? rigidbody.angularVelocity.ToCore() : Vec3.Zero;
        }

        public Vec3 CenterOfMass(object body)
        {
            Rigidbody rigidbody = body as Rigidbody;
            return rigidbody != null ? rigidbody.centerOfMass.ToCore() : Vec3.Zero;
        }

        public Vec3 PointVelocity(object body, Vec3 worldPoint)
        {
            Rigidbody rigidbody = body as Rigidbody;
            return rigidbody != null
                ? rigidbody.GetPointVelocity(worldPoint.ToUnity()).ToCore()
                : Vec3.Zero;
        }

        public void AddForceAtPosition(object body, Vec3 force, Vec3 worldPoint)
        {
            Rigidbody rigidbody = body as Rigidbody;
            if (rigidbody == null) return;
            // PhysX clears its forces after every fixed step, so this is one step of force.
            rigidbody.AddForceAtPosition(force.ToUnity(), worldPoint.ToUnity(), ForceMode.Force);
        }

        public Vec3 Gravity => Physics.gravity.ToCore();

        public bool IsKinematic(object body)
        {
            Rigidbody rigidbody = body as Rigidbody;
            return rigidbody != null && rigidbody.isKinematic;
        }
    }
}
