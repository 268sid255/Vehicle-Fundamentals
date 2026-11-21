using System;
using System.Collections.Generic;

namespace VehicleFundamentals
{
    /// <summary>
    /// One key of a cubic Hermite curve: a time, a value and a slope on each side.
    /// (Curves chapter.)
    /// </summary>
    [Serializable]
    public struct CurveKey
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;

        public CurveKey(float time, float value, float inTangent, float outTangent)
        {
            this.time = time;
            this.value = value;
            this.inTangent = inTangent;
            this.outTangent = outTangent;
        }
    }

    /// <summary>Shorthand for a key with the same slope on both sides.</summary>
    public static class Keys
    {
        public static CurveKey Key(float time, float value, float slope = 0f) =>
            new CurveKey(time, value, slope, slope);
    }

    /// <summary>
    /// Cubic Hermite curve, the same as Unity's AnimationCurve and the Curves chapter:
    /// flat before the first key and after the last, smooth in between.
    /// </summary>
    [Serializable]
    public class Curve
    {
        public List<CurveKey> keys = new List<CurveKey>();

        public Curve() { }

        public Curve(IEnumerable<CurveKey> keys)
        {
            this.keys = new List<CurveKey>(keys);
            Sort();
        }

        public void Sort() => keys.Sort((a, b) => a.time.CompareTo(b.time));

        public float Evaluate(float t)
        {
            if (keys == null || keys.Count == 0) return 0f;
            if (keys.Count == 1) return keys[0].value;
            if (t <= keys[0].time) return keys[0].value;
            CurveKey last = keys[keys.Count - 1];
            if (t >= last.time) return last.value;

            int i = 0;
            while (i < keys.Count - 2 && t > keys[i + 1].time) i++;

            CurveKey a = keys[i];
            CurveKey b = keys[i + 1];
            float dt = b.time - a.time;
            if (dt <= 1e-9f) return b.value;

            float s = (t - a.time) / dt;
            float s2 = s * s;
            float s3 = s2 * s;

            // Cubic Hermite: values at both ends, slopes scaled to the interval.
            return (2f * s3 - 3f * s2 + 1f) * a.value
                 + (s3 - 2f * s2 + s) * dt * a.outTangent
                 + (-2f * s3 + 3f * s2) * b.value
                 + (s3 - s2) * dt * b.inTangent;
        }

        /// <summary>The highest key. Every curve here has flat tangents at its peak, so the key is the peak.</summary>
        public float PeakValue()
        {
            if (keys == null || keys.Count == 0) return 0f;
            float best = keys[0].value;
            for (int i = 1; i < keys.Count; i++)
                if (keys[i].value > best) best = keys[i].value;
            return best;
        }

        /// <summary>Where the highest key is.</summary>
        public float PeakTime()
        {
            if (keys == null || keys.Count == 0) return 0f;
            CurveKey best = keys[0];
            for (int i = 1; i < keys.Count; i++)
                if (keys[i].value > best.value) best = keys[i];
            return best.time;
        }

        public Curve Clone()
        {
            var copy = new Curve();
            if (keys == null) return copy;
            for (int i = 0; i < keys.Count; i++) copy.keys.Add(keys[i]);
            copy.Sort();
            return copy;
        }

        /// <summary>Scales values and slopes, e.g. to build the shipped tire from the default curves.</summary>
        public Curve Scaled(float factor)
        {
            var copy = new Curve();
            if (keys == null) return copy;
            for (int i = 0; i < keys.Count; i++)
            {
                CurveKey k = keys[i];
                copy.keys.Add(new CurveKey(k.time, k.value * factor, k.inTangent * factor, k.outTangent * factor));
            }
            copy.Sort();
            return copy;
        }
    }
}
