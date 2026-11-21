using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// The small vector library of Vehicle Fundamentals (Math toolbox, Units, axes and signs).
    /// Local frame: x right, y up, z forward. Everything is SI.
    /// </summary>
    [Serializable]
    public struct Vec3
    {
        public float x;
        public float y;
        public float z;

        public Vec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);
        public static readonly Vec3 Forward = new Vec3(0f, 0f, 1f);
        public static readonly Vec3 Right = new Vec3(1f, 0f, 0f);

        public float SqrLength => x * x + y * y + z * z;
        public float Length => MathF.Sqrt(SqrLength);

        public Vec3 Normalized
        {
            get
            {
                float length = Length;
                return length > 1e-9f ? new Vec3(x / length, y / length, z / length) : Zero;
            }
        }

        public static float Dot(Vec3 a, Vec3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x);

        public static Vec3 Scale(Vec3 a, float s) => new Vec3(a.x * s, a.y * s, a.z * s);

        public static Vec3 Add(Vec3 a, Vec3 b) => new Vec3(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Vec3 Sub(Vec3 a, Vec3 b) => new Vec3(a.x - b.x, a.y - b.y, a.z - b.z);

        /// <summary>The part of v that lies in the plane with normal n (n has length 1).</summary>
        public static Vec3 ProjectOnPlane(Vec3 v, Vec3 n) => Sub(v, Scale(n, Dot(v, n)));

        public static Vec3 operator +(Vec3 a, Vec3 b) => Add(a, b);
        public static Vec3 operator -(Vec3 a, Vec3 b) => Sub(a, b);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.x, -a.y, -a.z);
        public static Vec3 operator *(Vec3 a, float s) => Scale(a, s);
        public static Vec3 operator *(float s, Vec3 a) => Scale(a, s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.x / s, a.y / s, a.z / s);

        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###})";
    }

    /// <summary>
    /// Unit quaternion. The formulas are the ones in the Math toolbox chapter and work in left- and
    /// right-handed game engines alike.
    /// </summary>
    [Serializable]
    public struct Quat
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quat(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static readonly Quat Identity = new Quat(0f, 0f, 0f, 1f);

        public static Quat Inverse(Quat q) => new Quat(-q.x, -q.y, -q.z, q.w);

        /// <summary>Rotates v by the unit quaternion q.</summary>
        public static Vec3 Rotate(Quat q, Vec3 v)
        {
            var u = new Vec3(q.x, q.y, q.z);
            Vec3 t = Vec3.Cross(u, v) * 2f;
            return v + t * q.w + Vec3.Cross(u, t);
        }

        /// <summary>Rotation by angleDeg about the vehicle's local up axis, then by q.</summary>
        public static Vec3 RotateAboutUp(Quat q, float angleDeg, Vec3 v)
        {
            float h = 0.5f * angleDeg * MathHelpers.DegToRad;
            var yaw = new Quat(0f, MathF.Sin(h), 0f, MathF.Cos(h));
            return Rotate(q, Rotate(yaw, v));
        }

        public static Quat Multiply(Quat a, Quat b) => new Quat(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Quat FromAxisAngle(Vec3 axis, float angleRad)
        {
            Vec3 n = axis.Normalized;
            float h = 0.5f * angleRad;
            float s = MathF.Sin(h);
            return new Quat(n.x * s, n.y * s, n.z * s, MathF.Cos(h));
        }

        public Quat Normalized
        {
            get
            {
                float length = MathF.Sqrt(x * x + y * y + z * z + w * w);
                return length > 1e-9f
                    ? new Quat(x / length, y / length, z / length, w / length)
                    : Identity;
            }
        }

        public static Quat operator *(Quat a, Quat b) => Multiply(a, b);

        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###}, {w:0.###})";
    }

    /// <summary>
    /// Scalar helpers with the exact behaviour of the documentation: Lerp and InverseLerp clamp,
    /// MathSign is 0 at zero, SignPos is +1 at zero (Math toolbox).
    /// </summary>
    public static class MathHelpers
    {
        public const float RadPerSecToRpm = 60f / (2f * MathF.PI);
        public const float DegToRad = MathF.PI / 180f;
        public const float RadToDeg = 180f / MathF.PI;
        public const float AirDensity = 1.225f; // kg/m3

        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        /// <summary>a at t = 0, b at t = 1; t is clamped to 0..1, like Unity's Mathf.Lerp.</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        /// <summary>Where v lies between a and b, 0..1, clamped. Works with a &gt; b too.</summary>
        public static float InverseLerp(float a, float b, float v) =>
            MathF.Abs(a - b) > 1e-9f ? Clamp01((v - a) / (b - a)) : 0f;

        /// <summary>Moves current towards target by at most maxDelta, never past it.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + MathSign(target - current) * maxDelta;
        }

        /// <summary>Sign that is 0 at zero, like System.Math.Sign. Used where a sign multiplies a force.</summary>
        public static float MathSign(float v) => v > 0f ? 1f : v < 0f ? -1f : 0f;

        /// <summary>Sign that is +1 at zero, like Unity's Mathf.Sign. Used where zero must still pick a side.</summary>
        public static float SignPos(float v) => v >= 0f ? 1f : -1f;

        /// <summary>From the value without an assist to the one with it; exactly withAssist at strength 1.</summary>
        public static float Fade(float without, float withAssist, float strength) =>
            strength >= 1f ? withAssist : Lerp(without, withAssist, strength);

        /// <summary>deg, 0..90, between the vehicle's axis and a local velocity, forwards or backwards.</summary>
        public static float SlideAngleOf(Vec3 v) =>
            MathF.Atan2(MathF.Abs(v.x), MathF.Abs(v.z)) * RadToDeg;
    }
}
