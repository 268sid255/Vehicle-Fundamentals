using System;

namespace VehicleFundamentals
{
    /// <summary>The ground under one wheel: what the ray found (Raycast and wheel contact).</summary>
    public struct GroundHit
    {
        public float distance;
        public Vec3 point;
        public Vec3 normal;
        public object body;

        public GroundHit(float distance, Vec3 point, Vec3 normal, object body)
        {
            this.distance = distance;
            this.point = point;
            this.normal = normal;
            this.body = body;
        }
    }

    /// <summary>
    /// The only four things the controller asks of the physics engine (Porting to your game engine):
    /// cast a ray down, push the body at a point, read the body's state / velocity at a point, and gravity.
    /// The Unity adapter lives in Runtime/Unity/UnityVehiclePhysics.cs.
    /// </summary>
    public interface IPhysicsWorld
    {
        GroundHit? Raycast(Vec3 origin, Vec3 direction, float maxDistance, object ignoreBody);
        Vec3 Position(object body);
        Quat Rotation(object body);
        Vec3 Velocity(object body);
        Vec3 AngularVelocity(object body);
        Vec3 CenterOfMass(object body); // local
        Vec3 PointVelocity(object body, Vec3 worldPoint);
        void AddForceAtPosition(object body, Vec3 force, Vec3 worldPoint);
        Vec3 Gravity { get; }
        bool IsKinematic(object body);
    }

    /// <summary>What the driving assists ask of the axles in one step (What an assist does).</summary>
    public struct AssistState
    {
        public float driftIntent;          // 0..1, the driver asks to slide
        public float tractionBrakeRelease; // 0..1, how far the traction brake lets go for a slide
        public float traction;             // 0..1, strength of traction control and traction brake
        public float antiLock;             // 0..1, strength of the anti-lock brakes
        public float corneringBrakes;      // 0..1, how far the brakes give way to cornering (rear only)
        public float driftDrive;           // 0..1, share of drive torque the drift allows
        public float driftGrip;            // 0..1, how much side grip a spinning wheel keeps (rear only)
    }
}
