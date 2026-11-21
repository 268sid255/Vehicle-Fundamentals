using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// The vehicle-level steering limit (Steering assist): steer asks for no more turn than the tires
    /// can hold, the wheels lead into the turn, counter-steer stays free and a tall vehicle is clamped
    /// below the acceleration at which it would tip.
    /// </summary>
    public class SteerLimit
    {
        public const float BaseReach = 0.7f;      // what a quick correction asks for
        public const float MaxReach = 1.2f;
        public const float ReachGrowth = 1.5f;     // per second, while steer is held beyond the limit
        public const float ReachDecay = 0.5f;      // per second, back to the base when it is not
        public const float ReachBack = 1.5f;       // per second, following a rear that starts to slide
        public const float RearGrip = 0.5f;        // rear slip as a share of the peak: grip to spare up to here
        public const float RearSliding = 1.1f;     // ... and no room left from here
        public const float Lead = 2f;
        public const float MinSpeedSquared = 1f;   // m2/s2
        public const float MinLeadSpeed = 3f;      // m/s, forwards
        public const float RollMargin = 0.8f;      // share of the static rollover threshold a tall vehicle may ask for

        private readonly IPhysicsWorld physics;
        private readonly VehicleSettings settings;
        private readonly float peakGrip;
        private readonly float peakSlipAngle;
        private float reach = BaseReach;

        public SteerLimit(IPhysicsWorld physics, VehicleSettings settings, float peakGrip, float peakSlipAngle)
        {
            this.physics = physics;
            this.settings = settings;
            this.peakGrip = peakGrip;
            this.peakSlipAngle = MathF.Max(peakSlipAngle, 0.5f);

            // Static rollover threshold: the lateral acceleration at which the inner wheels lift.
            float narrowestTrack = float.PositiveInfinity;
            for (int i = 0; i < settings.axles.Count; i++)
                narrowestTrack = MathF.Min(narrowestTrack, settings.axles[i].trackWidth);
            if (float.IsInfinity(narrowestTrack)) narrowestTrack = 1.48f;

            float rolloverThreshold = narrowestTrack / (2f * MathF.Max(settings.centerOfMass.y, 0.01f));
            RollGrip = rolloverThreshold < this.peakGrip ? RollMargin * rolloverThreshold : float.PositiveInfinity;
        }

        public float Reach => reach;

        /// <summary>Lateral acceleration in g at which a tall vehicle may still turn; infinity for a car.</summary>
        public float RollGrip { get; }

        /// <summary>
        /// velocity: m/s, local, relative to the ground. yawRate: rad/s, positive turning right.
        /// rearSlipAngle: deg, the larger of the rear wheels in the previous step.
        /// driftIntent, counterSteer: 0..1, the assist lets go by these shares.
        /// </summary>
        public float Limit(float steer, Vec3 velocity, float yawRate, float rearSlipAngle,
            float driftIntent, float counterSteer, float dt)
        {
            float speedSquared = MathF.Max(
                velocity.x * velocity.x + velocity.z * velocity.z, MinSpeedSquared);
            float g = physics.Gravity.Length;
            float gripSteer = peakGrip * g * settings.MinTurnRadius / speedSquared;
            UpdateReach(steer, gripSteer, rearSlipAngle / peakSlipAngle, dt);

            float maxSteer = reach * gripSteer;
            float limited = MathHelpers.Clamp(steer, -maxSteer, maxSteer);

            if (velocity.z > MinLeadSpeed)
            {
                float yawingSteer = yawRate * settings.MinTurnRadius / velocity.z;
                float led = limited + Lead * (limited - yawingSteer);
                limited = steer > 0f
                    ? MathHelpers.Clamp(led, 0f, steer)
                    : MathHelpers.Clamp(led, steer, 0f);
            }

            // Counter-steering and a slide the driver asks for stay free.
            limited = MathHelpers.Lerp(limited, steer, counterSteer);
            limited = MathHelpers.Lerp(limited, steer, driftIntent);

            // A tall vehicle tips over before its tires slide: no reach or lead past that, also in a slide.
            float rollSteer = RollGrip * g * settings.MinTurnRadius / speedSquared;
            return MathHelpers.Clamp(limited, -rollSteer, rollSteer);
        }

        /// <summary>
        /// How much of the grip-limited steer the driver may ask for. The room depends on the rear
        /// tires: up to half their peak slip angle there is grip to spare, near the peak there is none.
        /// </summary>
        private void UpdateReach(float steer, float gripSteer, float rearSlip, float dt)
        {
            float room = MathHelpers.Lerp(MaxReach, BaseReach,
                MathHelpers.InverseLerp(RearGrip, RearSliding, rearSlip));
            bool asksMore = MathF.Abs(steer) > reach * gripSteer;

            float target = asksMore ? room : BaseReach;
            float rate = target > reach ? ReachGrowth : room < reach ? ReachBack : ReachDecay;
            reach = MathHelpers.Clamp(
                MathHelpers.MoveTowards(reach, target, rate * dt), BaseReach, MaxReach);
        }

        public void ResetToStandstill()
        {
            reach = BaseReach;
        }
    }
}
