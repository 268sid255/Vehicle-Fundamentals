using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// Inverts the rising side of a tire grip curve: the slip at which the curve gives a grip.
    /// The curve is evaluated once ahead of time into a binary tree, so a step only compares numbers
    /// (Parked car jitter and sliding on slopes, rule 2).
    /// </summary>
    public sealed class SlipSearch
    {
        private const int Steps = 10; // halvings: the peak slip / 1024 is close enough
        private readonly float peakSlip;
        // The curve at the middle of every reachable interval. Node i halves its interval into
        // child 2i + 1 below the middle and 2i + 2 above it.
        private readonly float[] grips;

        public SlipSearch(Curve curve)
        {
            peakSlip = MathF.Max(curve.PeakTime(), 1e-4f);
            grips = new float[(1 << Steps) - 1];
            Fill(curve, 0, 0f, peakSlip);
        }

        public float PeakSlip => peakSlip;

        /// <summary>The slip at which a curve gives a grip on its rise to the peak; above the peak grip, the peak slip.</summary>
        public float SlipAtGrip(float grip)
        {
            float low = 0f;
            float high = peakSlip;
            int node = 0;

            for (int i = 0; i < Steps; i++)
            {
                float middle = (low + high) * 0.5f;
                if (grips[node] < grip)
                {
                    low = middle;
                    node = 2 * node + 2;
                }
                else
                {
                    high = middle;
                    node = 2 * node + 1;
                }
            }

            return high;
        }

        private void Fill(Curve curve, int node, float low, float high)
        {
            if (node >= grips.Length) return;
            float middle = (low + high) * 0.5f;
            grips[node] = curve.Evaluate(middle);
            Fill(curve, 2 * node + 1, low, middle);
            Fill(curve, 2 * node + 2, middle, high);
        }
    }
}
