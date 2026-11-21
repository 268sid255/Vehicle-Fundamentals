using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>Conversions between the controller's own Vec3/Quat and Unity's types.</summary>
    public static class UnityConversions
    {
        public static Vector3 ToUnity(this Vec3 v) => new Vector3(v.x, v.y, v.z);

        public static Vec3 ToCore(this Vector3 v) => new Vec3(v.x, v.y, v.z);

        public static Quaternion ToUnity(this Quat q) => new Quaternion(q.x, q.y, q.z, q.w);

        public static Quat ToCore(this Quaternion q) => new Quat(q.x, q.y, q.z, q.w);

        public static Vector3 Forward(this Quat q) => Quat.Rotate(q, Vec3.Forward).ToUnity();
    }
}
