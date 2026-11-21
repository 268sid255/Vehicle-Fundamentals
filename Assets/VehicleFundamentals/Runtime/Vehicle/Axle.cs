using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// One axle: Ackermann steering, the per-wheel steering limit, ground contacts, anti-roll bar,
    /// traction control, differential and the two wheels.
    /// Chapters: Ackermann steering, Steering assist (wheel limit), Anti-roll bar,
    /// Differential, Traction control, The physics step in order.
    /// </summary>
    public class Axle
    {
        public const float LimitedSlipFullDifference = 2f;   // rad/s at which the clutches pass their full torque
        public const float TractionSlipStart = 0.2f;         // slip ratio where the torque cut starts
        public const float TractionSlipFull = 0.6f;          // ... and where it reaches its maximum
        public const float TractionCutRate = 8f;             // 1/s, cut fast
        public const float TractionRecoverRate = 3f;         // 1/s, give torque back slowly
        public const float TractionBrakeBuildRate = 5f;      // 1/s, like brake pressure building
        public const float TractionBrakeReleaseRate = 3f;    // 1/s
        public const float TractionBrakeReadyShare = 0.9f;   // of the weak wheel's peak
        public const float SpinStopTime = 0.2f;              // s to bring a spinnning weak wheel back to road speed
        public const float SpinOverload = 1.25f;             // a wheel breaks loose above this share of its peak torque

        private readonly IPhysicsWorld physics;
        private readonly AxleSettings settings;
        private readonly Wheel left;
        private readonly Wheel right;

        private float tractionScale = 1f;
        private float tractionBrakeLevel;
        private float tractionBrakeTorque;

        public Axle(IPhysicsWorld physics, AxleSettings settings)
        {
            this.physics = physics;
            this.settings = settings;
            left = new Wheel(physics, settings, -1);
            right = new Wheel(physics, settings, 1);
            PeakSlipAngle = MathF.Max(settings.tire.lateralPeakAngleDeg, 0.5f);
            PeakGrip = settings.tire.lateralPeakGrip;
        }

        public AxleSettings Settings => settings;
        public Wheel Left => left;
        public Wheel Right => right;
        public bool IsRear => settings.isRear;
        public float PeakSlipAngle { get; }
        public float PeakGrip { get; }
        public float TractionScale => tractionScale;
        public float TractionBrakeLevel => tractionBrakeLevel;
        public float TractionBrakeTorque => tractionBrakeTorque;

        /// <summary>
        /// Ackermann steering: every wheel points at the turn centre beside the turn centre line.
        /// Steer is curvature, so half steer drives a circle of twice the minimum radius.
        /// </summary>
        public void Steer(float steer, float minTurnRadius, float turnCenter)
        {
            float offset = settings.position - turnCenter;
            if (!settings.steering || offset == 0f || steer == 0f)
            {
                left.SteerAngle = 0f;
                right.SteerAngle = 0f;
                return;
            }

            float turnRadius = minTurnRadius / MathF.Abs(steer);
            float halfTrack = settings.trackWidth * 0.5f;
            float inner = MathF.Atan2(offset, turnRadius - halfTrack) * MathHelpers.RadToDeg;
            float outer = MathF.Atan2(offset, turnRadius + halfTrack) * MathHelpers.RadToDeg;

            if (steer > 0f)
            {
                // Turning right: the right wheel is on the inside.
                left.SteerAngle = outer;
                right.SteerAngle = inner;
            }
            else
            {
                left.SteerAngle = -inner;
                right.SteerAngle = -outer;
            }
        }

        /// <summary>
        /// The per-wheel steering limit (Steering assist): no wheel points further from the line it
        /// travels along than its tire's peak slip angle. Counter-steer stays free by the geometry.
        /// </summary>
        public void LimitSteer(Vec3 velocity, Vec3 angularVelocity, Vec3 centerOfMass,
                               float strength, float counterSteerStrength, float counterSteerClosing)
        {
            left.SteerAngle = LimitedSteerAngle(left, PeakSlipAngle, velocity, angularVelocity, centerOfMass,
                strength, counterSteerStrength, counterSteerClosing);
            right.SteerAngle = LimitedSteerAngle(right, PeakSlipAngle, velocity, angularVelocity, centerOfMass,
                strength, counterSteerStrength, counterSteerClosing);
        }

        public const float SteerLimitStartSpeed = 1f;   // m/s at the mount
        public const float SteerLimitFullSpeed = 3f;
        public const float SidewaysStartAngle = 75f;    // deg
        public const float SidewaysAngle = 90f;

        /// <summary>
        /// Holds a wheel's steer inside the window around the line its mount travels along, and only
        /// towards straight ahead: never further than the driver steered, never to the other side.
        /// </summary>
        public static float LimitedSteerAngle(Wheel wheel, float maxSlipAngle, Vec3 velocity,
            Vec3 angularVelocity, Vec3 centerOfMass, float strength, float counterSteerStrength,
            float counterSteerClosing)
        {
            float steerAngle = wheel.SteerAngle;
            if (steerAngle == 0f) return steerAngle;

            Vec3 mountVelocity = velocity + Vec3.Cross(angularVelocity, wheel.LocalMount - centerOfMass);
            float speed = MathF.Sqrt(mountVelocity.x * mountVelocity.x + mountVelocity.z * mountVelocity.z);

            // The line the mount travels along, forwards or backwards. In a slide it points out of the turn.
            float travelAngle = MathF.Atan2(mountVelocity.x * MathHelpers.SignPos(mountVelocity.z),
                MathF.Abs(mountVelocity.z)) * MathHelpers.RadToDeg;

            float f = MathF.Max(strength, counterSteerStrength);
            f *= MathHelpers.InverseLerp(SteerLimitStartSpeed, SteerLimitFullSpeed, speed)
               * MathHelpers.InverseLerp(SidewaysAngle, SidewaysStartAngle, MathF.Abs(travelAngle));
            if (f == 0f) return steerAngle;

            // Coming back, the rear swings on past straight; the window closes ahead of it.
            float closing = counterSteerClosing * MathHelpers.SignPos(steerAngle);
            float limited = MathHelpers.Clamp(steerAngle,
                travelAngle - maxSlipAngle - closing,
                travelAngle + maxSlipAngle - closing);
            limited = steerAngle > 0f
                ? MathHelpers.Clamp(limited, 0f, steerAngle)
                : MathHelpers.Clamp(limited, steerAngle, 0f);
            return MathHelpers.Lerp(steerAngle, limited, f);
        }

        /// <summary>Rays, springs, dampers and then the anti-roll bar (Raycast, Spring and damper, Anti-roll bar).</summary>
        public void UpdateContacts(object body, Vec3 position, Quat rotation)
        {
            left.UpdateContact(body, position, rotation);
            right.UpdateContact(body, position, rotation);

            float antiRoll = (left.Compression - right.Compression) * settings.antiRollRate;
            if (left.IsGrounded) left.AddSuspensionForce(antiRoll);
            if (right.IsGrounded) right.AddSuspensionForce(-antiRoll);
        }

        /// <summary>One axle step: traction cut, differential, traction brake, then the two wheels.</summary>
        public void Simulate(object body, float driveTorque, float drivelineInertia,
            float brakeTorque, float handbrakeTorque, AssistState assists, float dt)
        {
            if (assists.traction > 0f)
            {
                driveTorque *= MathHelpers.Fade(1f,
                    UpdateTractionControl(driveTorque, assists, dt), assists.traction);
            }

            // Open differential: equal torque on both wheels, a lifted wheel spins freely.
            float leftTorque = driveTorque * 0.5f;
            float rightTorque = driveTorque * 0.5f;
            bool isLeftSpinning = left.HasOwnSpin;
            bool isRightSpinning = right.HasOwnSpin;
            if (settings.limitedSlip > 0f && (isLeftSpinning || isRightSpinning))
                LimitSlip(ref leftTorque, ref rightTorque, driveTorque);

            // Behind an open differential the engine turns with the average of both wheels.
            float wheelInertia = drivelineInertia * (isLeftSpinning && isRightSpinning ? 0.5f : 0.25f);

            float leftSpinBrake = 0f;
            float rightSpinBrake = 0f;
            if (assists.traction > 0f)
            {
                ApplyTractionBrake(ref leftTorque, ref rightTorque, wheelInertia,
                    assists.tractionBrakeRelease, assists.traction, dt,
                    out leftSpinBrake, out rightSpinBrake);
            }
            else
            {
                tractionBrakeTorque = 0f;
            }

            left.Simulate(body, leftTorque, wheelInertia, brakeTorque, handbrakeTorque,
                leftSpinBrake, assists, dt);
            right.Simulate(body, rightTorque, wheelInertia, brakeTorque, handbrakeTorque,
                rightSpinBrake, assists, dt);
        }

        /// <summary>
        /// Traction control's torque cut. The share follows its target at a limited rate: it cuts fast
        /// and gives torque back slowly, so the wheel does not hunt between spinning and gripping.
        /// </summary>
        private float UpdateTractionControl(float driveTorque, AssistState assists, float dt)
        {
            float spin = MathF.Max(WheelSpin(left, driveTorque), WheelSpin(right, driveTorque));

            // A driver who asks to slide wants the wheels to spin: by the drift intent the control
            // turns from holding the wheels' slip to holding the drift angle.
            float target = MathHelpers.Lerp(
                1f - MathHelpers.InverseLerp(TractionSlipStart, TractionSlipFull, spin),
                assists.driftDrive, assists.driftIntent);
            float rate = target < tractionScale ? TractionCutRate : TractionRecoverRate;
            tractionScale = MathHelpers.MoveTowards(tractionScale, target, rate * dt);
            return tractionScale;
        }

        /// <summary>Slip ratio in the direction the drive pushes, from the previous step.</summary>
        private static float WheelSpin(Wheel wheel, float driveTorque)
        {
            return wheel.IsGrounded
                ? MathF.Max(0f, wheel.SlipRatio * MathHelpers.MathSign(driveTorque))
                : 0f;
        }

        /// <summary>
        /// Limited-slip differential: while one wheel spins, the clutches move up to a share of the
        /// axle torque from the faster wheel to the slower one.
        /// </summary>
        private void LimitSlip(ref float leftTorque, ref float rightTorque, float driveTorque)
        {
            float direction = MathHelpers.SignPos(driveTorque);
            float difference = (left.TurningAngularVelocity - right.TurningAngularVelocity) * direction;
            float transfer = settings.limitedSlip * MathF.Abs(driveTorque) * 0.5f
                * MathHelpers.Clamp(difference / LimitedSlipFullDifference, -1f, 1f);
            leftTorque -= transfer * direction;
            rightTorque += transfer * direction;
        }

        /// <summary>
        /// The traction brake: brakes the weak wheel by the torque its tire cannot pass on, and brakes
        /// a wheel that has already broken loose back towards road speed.
        /// </summary>
        private void ApplyTractionBrake(ref float leftTorque, ref float rightTorque,
            float wheelInertia, float release, float traction, float dt,
            out float leftSpinBrake, out float rightSpinBrake)
        {
            float leftPeak = left.PeakTorque();
            float rightPeak = right.PeakTorque();
            bool isLeftWeak = leftPeak < rightPeak;
            Wheel weak = isLeftWeak ? left : right;
            float weakTorque = MathF.Abs(isLeftWeak ? leftTorque : rightTorque);
            float strongTorque = MathF.Abs(isLeftWeak ? rightTorque : leftTorque);
            float weakPeak = MathF.Min(leftPeak, rightPeak);
            float strongPeak = MathF.Max(leftPeak, rightPeak);

            float excess = MathF.Max(0f, weakTorque - weakPeak);
            float strongOverload = strongPeak > 0f ? strongTorque / strongPeak : float.PositiveInfinity;

            // A strong wheel that breaks loose as well leaves nothing to gain by braking the weak one.
            float strength = (1f - MathHelpers.InverseLerp(1f, SpinOverload, strongOverload))
                * (1f - release) * traction;
            float target = (weakTorque >= TractionBrakeReadyShare * weakPeak || weak.HasOwnSpin)
                && strength > 0f ? 1f : 0f;
            float rate = target > tractionBrakeLevel ? TractionBrakeBuildRate : TractionBrakeReleaseRate;
            tractionBrakeLevel = MathHelpers.MoveTowards(tractionBrakeLevel, target, rate * dt);

            leftSpinBrake = 0f;
            rightSpinBrake = 0f;
            if (weak.HasOwnSpin && weak.IsGrounded)
            {
                float spin = MathF.Abs(weak.AngularVelocity - weak.ForwardSpeed / settings.wheelRadius);
                float spinBrake = (weak.Inertia + wheelInertia) * spin
                    / SpinStopTime * strength * tractionBrakeLevel;
                if (isLeftWeak) leftSpinBrake = spinBrake;
                else rightSpinBrake = spinBrake;
            }

            tractionBrakeTorque = excess * strength * tractionBrakeLevel;
            if (tractionBrakeTorque > 0f)
            {
                // The brake takes torque towards zero: it can take away the drive, never push.
                if (isLeftWeak) leftTorque = MathHelpers.MoveTowards(leftTorque, 0f, tractionBrakeTorque);
                else rightTorque = MathHelpers.MoveTowards(rightTorque, 0f, tractionBrakeTorque);
            }
        }

        /// <summary>Forget the assist states and the wheel spin, for a reset to standstill.</summary>
        public void ResetToStandstill()
        {
            tractionScale = 1f;
            tractionBrakeLevel = 0f;
            tractionBrakeTorque = 0f;
            left.ResetToStandstill();
            right.ResetToStandstill();
        }
    }
}
