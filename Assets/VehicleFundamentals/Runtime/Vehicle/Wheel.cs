using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// One raycast wheel: ground contact, spring, damper, tire force, spin and brake.
    /// Chapters: Raycast and wheel contact, Spring and damper, Tire force, Wheel spin and inertia,
    /// Tire grip curves, Combined slip and friction circle, Anti-lock brakes, Parked car jitter and slopes.
    /// </summary>
    public class Wheel
    {
        public const float HoldSpeed = 0.4f;                 // m/s; below it a braked wheel holds with static grip
        public const float MinCorneringBrakeShare = 0.2f;    // share of the peak a wheel may still brake with while cornering
        public const float SpinOverload = 1.25f;             // share of the peak torque above which a driven wheel breaks loose
        public const float MinSpinSpeed = 1f;                // m/s of slip speed below which a loose wheel grips again
        public const float AirborneDragTorque = 5f;          // N*m, bearings and brake pads on a wheel in the air
        public const float RollingResistanceSpeed = 0.1f;    // m/s, rolling resistance fades in below this
        public const float MaxCombinedSlipAngle = 1.5f;      // rad; keeps tan finite near 90 degrees
        public const float MinStrutAlignment = 0.25f;        // least share of the ground normal along the strut
        public const float LevelGroundNormal = 0.9986f;      // up component of the normal: level up to 3 degrees
        public const float TiltedGroundNormal = 0.985f;      // tilted from 10 degrees
        public const float SteepGroundNormal = 0.5f;         // up component of the normal: 60 degrees

        private readonly IPhysicsWorld physics;
        private readonly AxleSettings settings;
        private readonly SlipSearch slipSearch;
        private readonly int side;
        private readonly float driveShare;

        private Vec3 up = Vec3.Up;
        private Vec3 forward = Vec3.Forward;
        private Vec3 right = Vec3.Right;

        public Wheel(IPhysicsWorld physics, AxleSettings settings, int side)
        {
            this.physics = physics;
            this.settings = settings;
            this.side = side;
            driveShare = settings.driveShare;
            slipSearch = settings.tire.longitudinalSlipSearch ?? new SlipSearch(settings.tire.longitudinal);
            LocalMount = new Vec3(side * settings.trackWidth * 0.5f, settings.mountHeight, settings.position);
            Radius = settings.wheelRadius;
            Inertia = settings.WheelInertia;
            SpringLength = settings.suspensionTravel;
        }

        public AxleSettings Settings => settings;
        public int Side => side;
        public Vec3 LocalMount { get; }
        public float Radius { get; }
        public float Inertia { get; } // the wheel alone, kg*m2
        public float DriveShare => driveShare;

        // Contact (Raycast and wheel contact)
        public bool IsGrounded { get; private set; }
        public float SpringLength { get; private set; }
        public Vec3 ContactPoint { get; private set; }
        public Vec3 ContactNormal { get; private set; } = Vec3.Up;
        public object GroundBody { get; private set; }
        public Vec3 GroundVelocity { get; private set; }
        public float SteerAngle { get; set; }
        public float ForwardSpeed { get; private set; }
        public float SideSpeed { get; private set; }
        public Vec3 Force { get; private set; }

        // Suspension (Spring and damper)
        public float SuspensionForce { get; private set; }
        public float Compression => MathF.Max(0f, settings.suspensionTravel - SpringLength);

        // Tire (Tire force, Tire grip curves)
        public float Load { get; private set; }
        public float LongitudinalGrip { get; private set; }
        public float LateralGrip { get; private set; }
        public float CorneringGrip { get; private set; } // sideways grip of the previous step, before the circle
        public float SlipRatio { get; private set; }
        public float SlipAngleDeg { get; private set; }
        public float LongitudinalPeakGrip => settings.tire.longitudinalPeakGrip;
        public float LateralPeakGrip => settings.tire.lateralPeakGrip;
        public float LongitudinalPeakSlip => settings.tire.longitudinalPeakSlip;
        public float LateralPeakSlip => settings.tire.lateralPeakSlip;
        public float LateralPeakAngleDeg => settings.tire.lateralPeakAngleDeg;

        public bool IsRolling { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsSpinning { get; private set; }
        public bool HasOwnSpin { get; private set; }
        public bool IsAntiLockActive { get; private set; }
        public bool IsAntiLocked { get; private set; }

        // Wheel spin (Wheel spin and inertia)
        public float AngularVelocity { get; private set; }
        public float ShownAngularVelocity { get; private set; }
        public float DriveTorque { get; private set; }
        public float BrakeTorque { get; private set; }
        public float DrivelineInertia { get; private set; }
        public float SpinBrakeTorque { get; private set; }
        public float TurningAngularVelocity => HasOwnSpin ? AngularVelocity : ForwardSpeed / Radius;

        /// <summary>Visual state, advanced in the render frame only (Wheel spin and inertia, Visual wheels).</summary>
        public float SpinAngleDeg { get; set; }
        public float VisualSteerAngleDeg { get; set; }

        /// <summary>The most torque the tire can pass on at its peak: peak grip x load x radius.</summary>
        public float PeakTorque() => settings.tire.longitudinalPeakGrip * SuspensionForce * Radius;

        /// <summary>Reads the ground under the wheel and builds its frame on it (Raycast and wheel contact).</summary>
        public void UpdateContact(object body, Vec3 position, Quat rotation)
        {
            up = Quat.Rotate(rotation, Vec3.Up);
            Vec3 mount = position + Quat.Rotate(rotation, LocalMount);
            float rayLength = settings.suspensionTravel + Radius;

            GroundHit? maybeHit = physics.Raycast(mount, -up, rayLength, body);
            IsGrounded = maybeHit.HasValue;

            if (!IsGrounded)
            {
                SpringLength = settings.suspensionTravel; // hangs fully extended
                SuspensionForce = 0f;
                GroundVelocity = Vec3.Zero;
                GroundBody = null;
                ForwardSpeed = 0f;
                SideSpeed = 0f;
                return;
            }

            GroundHit hit = maybeHit.Value;
            SpringLength = hit.distance - Radius;
            ContactPoint = hit.point;
            ContactNormal = hit.normal;
            GroundBody = hit.body;
            GroundVelocity = hit.body != null ? physics.PointVelocity(hit.body, hit.point) : Vec3.Zero;

            // Everything the tire and the damper see is relative to the ground.
            Vec3 relativeVelocity = physics.PointVelocity(body, hit.point) - GroundVelocity;

            UpdateSuspension(relativeVelocity, hit.normal);

            Vec3 heading = Quat.RotateAboutUp(rotation, SteerAngle, Vec3.Forward);
            Vec3 flatHeading = Vec3.ProjectOnPlane(heading, hit.normal);
            forward = flatHeading.SqrLength > 1e-9f ? flatHeading.Normalized
                : Vec3.Cross(Quat.Rotate(rotation, Vec3.Right), hit.normal).Normalized;
            right = Vec3.Cross(hit.normal, forward);
            ForwardSpeed = Vec3.Dot(relativeVelocity, forward);
            SideSpeed = Vec3.Dot(relativeVelocity, right);
        }

        /// <summary>
        /// Spring and damper. The damper reads the body's velocity against the ground, not the change of
        /// spring length, so teleports and moving ground do not kick it (Spring and damper).
        /// </summary>
        private void UpdateSuspension(Vec3 relativeVelocity, Vec3 normal)
        {
            float compressionSpeed = -Vec3.Dot(relativeVelocity, up);
            if (normal.y < LevelGroundNormal)
            {
                float exact = -Vec3.Dot(relativeVelocity, normal)
                    / MathF.Max(Vec3.Dot(up, normal), MinStrutAlignment);
                compressionSpeed = MathHelpers.Lerp(exact, compressionSpeed,
                    MathHelpers.InverseLerp(TiltedGroundNormal, LevelGroundNormal, normal.y));
            }

            float compression = settings.suspensionTravel - SpringLength;
            float spring = settings.springRate * compression + settings.damperRate * compressionSpeed; // F = k*x + c*v
            SuspensionForce = MathF.Max(0f, spring);
        }

        public void AddSuspensionForce(float force) => SuspensionForce = MathF.Max(0f, SuspensionForce + force);

        /// <summary>One wheel step: anti-lock, tire force, spin, brake last (Wheel spin and inertia).</summary>
        public void Simulate(object body, float driveTorque, float drivelineInertia, float brakeTorque,
                             float handbrakeTorque, float spinBrake, AssistState assists, float dt)
        {
            DrivelineInertia = drivelineInertia;
            SpinBrakeTorque = spinBrake;

            // Anti-lock brakes may fade engine braking on top of the foot brake, so the wheel
            // keeps the torque it actually got (Anti-lock brakes, Engine braking).
            ApplyAntiLock(ref brakeTorque, ref driveTorque, handbrakeTorque, assists);
            DriveTorque = driveTorque;
            BrakeTorque = MathF.Max(brakeTorque, handbrakeTorque);

            if (IsGrounded) ApplyTireForce(body, BrakeTorque > 0f, assists.driftGrip, dt);
            else ResetTire(); // no load, no force, no slip

            float inertia = Inertia + drivelineInertia;
            if (IsGrounded && !IsSpinning)
            {
                // Rolls with the ground; only this step's drive torque turns it faster.
                AngularVelocity = ForwardSpeed / Radius + (driveTorque / Inertia) * dt;
            }
            else
            {
                // Own spin, from where it was last seen turning.
                float spin = HasOwnSpin ? AngularVelocity : ShownAngularVelocity;
                spin += ((driveTorque - LongitudinalGrip * Load * Radius) / inertia) * dt;
                AngularVelocity = IsGrounded
                    ? MathHelpers.MoveTowards(spin, ForwardSpeed / Radius, (spinBrake / inertia) * dt)
                    : MathHelpers.MoveTowards(spin, 0f, (AirborneDragTorque / inertia) * dt);
            }

            HasOwnSpin = !IsGrounded || IsSpinning;

            // Brake last: it can stop the wheel but never spin it backwards.
            AngularVelocity = MathHelpers.MoveTowards(AngularVelocity, 0f,
                (BrakeTorque / (HasOwnSpin ? inertia : Inertia)) * dt);

            // On screen a rolling wheel turns with the slip it shows, not with one step's drive torque.
            ShownAngularVelocity = IsRolling
                ? (ForwardSpeed + SlipRatio * MathF.Abs(ForwardSpeed)) / Radius
                : AngularVelocity;
        }

        /// <summary>
        /// Anti-lock brakes: no more brake torque than the tire passes on at its peak. Only the rear gives
        /// way to cornering; the handbrake is not limited (Anti-lock brakes).
        /// </summary>
        private void ApplyAntiLock(ref float brakeTorque, ref float driveTorque,
            float handbrakeTorque, AssistState assists)
        {
            IsAntiLockActive = false;
            bool isWithinPeak = false;

            if (assists.antiLock > 0f && IsGrounded)
            {
                float lateral = MathF.Min(MathF.Abs(CorneringGrip) / MathF.Max(LateralPeakGrip, 1e-6f), 1f);
                float share = MathF.Max(MathF.Sqrt(1f - lateral * lateral), MinCorneringBrakeShare);
                float peak = PeakTorque() * MathHelpers.Lerp(1f, share, assists.corneringBrakes);
                float limited = MathHelpers.Fade(brakeTorque, MathF.Min(brakeTorque, peak), assists.antiLock);

                // Holding a vehicle that stands is not anti-lock braking, even where the limit applies.
                IsAntiLockActive = limited < brakeTorque && MathF.Abs(ForwardSpeed) >= HoldSpeed;
                isWithinPeak = limited <= peak;
                brakeTorque = limited;

                if (IsAntiLockActive && driveTorque * ForwardSpeed < 0f)
                {
                    // Engine braking on top would take the wheel past its peak again.
                    driveTorque = MathHelpers.Fade(driveTorque, 0f, assists.antiLock);
                }
            }

            // The tire reads this in this step only. The handbrake is not limited, so it still locks.
            IsAntiLocked = isWithinPeak && brakeTorque > 0f && brakeTorque >= handbrakeTorque;
        }

        private void ResetTire()
        {
            Load = 0f;
            LongitudinalGrip = 0f;
            LateralGrip = 0f;
            CorneringGrip = 0f;
            SlipRatio = 0f;
            SlipAngleDeg = 0f;
            Force = Vec3.Zero;
            IsRolling = false;
            IsLocked = false;
            IsAntiLockActive = false;
            IsAntiLocked = false;
        }

        /// <summary>Grip x load, onto the body at the contact point (Tire force).</summary>
        private void ApplyTireForce(object body, bool isBraking, float driftGrip, float dt)
        {
            Load = SuspensionForce;
            float normalForce = SuspensionForce * UprightPart();
            UpdateGrip(isBraking, driftGrip, dt);
            LimitByWheelTorque(isBraking, dt); // the torque limit
            ApplyFrictionCircle();             // corneringGrip, circle, slip read back

            float longitudinalForce = LongitudinalGrip * Load;
            float rollingResistance = -settings.tire.rollingResistance * Load
                * MathHelpers.Clamp(ForwardSpeed / RollingResistanceSpeed, -1f, 1f);

            Force = ContactNormal * normalForce
                  + forward * (longitudinalForce + rollingResistance)
                  + right * (LateralGrip * Load);

            physics.AddForceAtPosition(body, Force, ContactPoint);
            if (GroundBody != null && !physics.IsKinematic(GroundBody))
                physics.AddForceAtPosition(GroundBody, -Force, ContactPoint);
        }

        /// <summary>Four states first, then the curves: holding, locked, spinning or rolling (Tire grip curves).</summary>
        private void UpdateGrip(bool isBraking, float driftGrip, float dt)
        {
            TireSettings tire = settings.tire;
            float slipSpeed = AngularVelocity * Radius - ForwardSpeed;
            float forwardSpeed = MathF.Abs(ForwardSpeed);

            // At an exact standstill a wheel that turns slips fully: zero speed must not mean zero force.
            SlipRatio = forwardSpeed > 0f ? MathHelpers.Clamp(slipSpeed / forwardSpeed, -1f, 1f) : MathHelpers.MathSign(slipSpeed);
            float slipAngle = MathF.Atan2(MathF.Abs(SideSpeed), forwardSpeed); // rad
            SlipAngleDeg = slipAngle * MathHelpers.RadToDeg;

            bool isHolding = isBraking && forwardSpeed < HoldSpeed;
            // Only a wheel the brake holds still really slides - unless the anti-lock brakes keep it at the peak.
            IsLocked = isBraking && AngularVelocity == 0f && !IsAntiLocked;
            UpdateSpinning(slipSpeed, forwardSpeed);
            IsRolling = !isHolding && !IsLocked && !IsSpinning;

            // A rolling wheel is never past its peak; the anti-lock brakes hold theirs right at it.
            float gripSlip = IsRolling
                ? (IsAntiLockActive ? tire.longitudinalPeakSlip : MathF.Min(MathF.Abs(SlipRatio), tire.longitudinalPeakSlip))
                : MathF.Abs(SlipRatio);
            float longitudinalTarget = tire.longitudinal.Evaluate(gripSlip);
            float lateralTarget = tire.lateral.Evaluate(slipAngle / (0.5f * MathF.PI));

            if (!isHolding && (IsLocked || IsSpinning))
            {
                // A spinning wheel's slip is not cut off at 1: rubbing far faster than it rolls, it has
                // little side grip.
                float longitudinalSlip = IsSpinning && forwardSpeed > 0f
                    ? MathF.Abs(slipSpeed) / forwardSpeed
                    : MathF.Abs(SlipRatio);
                float rollingLateral = lateralTarget;
                CombineSlip(slipAngle, longitudinalSlip, longitudinalTarget, lateralTarget,
                    out longitudinalTarget, out lateralTarget);
                if (IsSpinning && !IsLocked)
                    lateralTarget = MathHelpers.Lerp(lateralTarget, rollingLateral, driftGrip); // drift assist
            }

            longitudinalTarget *= MathHelpers.MathSign(SlipRatio); // pushes the way the wheel slips
            lateralTarget *= -MathHelpers.MathSign(SideSpeed);     // pushes against the sideways motion
            if (isHolding) longitudinalTarget = -MathHelpers.MathSign(ForwardSpeed); // static friction

            // Relaxation: the force follows its target over the distance the tire slips.
            float longitudinalShare = MathF.Abs(slipSpeed) * dt
                / MathF.Max(tire.longitudinalRelaxationLength, 1e-6f);
            float lateralShare = MathF.Abs(SideSpeed) * dt
                / MathF.Max(tire.lateralRelaxationLength, 1e-6f);
            LongitudinalGrip = MathHelpers.Lerp(LongitudinalGrip, longitudinalTarget, longitudinalShare);
            LateralGrip = MathHelpers.Lerp(LateralGrip, lateralTarget, lateralShare);
        }

        /// <summary>
        /// Combined slip for locked and spinning wheels: the force points along the slip velocity of the
        /// contact patch (Combined slip and friction circle).
        /// </summary>
        private void CombineSlip(float slipAngle, float longitudinalSlip,
                                 float longitudinalGrip, float lateralGrip,
                                 out float combinedLongitudinal, out float combinedLateral)
        {
            float lateralSlip = MathF.Tan(MathF.Min(slipAngle, MaxCombinedSlipAngle));
            float slip = MathF.Sqrt(longitudinalSlip * longitudinalSlip + lateralSlip * lateralSlip);
            if (slip <= 1e-9f)
            {
                combinedLongitudinal = longitudinalGrip;
                combinedLateral = lateralGrip;
                return;
            }

            float longitudinal = longitudinalSlip / settings.tire.longitudinalPeakSlip;
            float lateral = lateralSlip / settings.tire.lateralPeakSlip;
            float combined = MathF.Sqrt(longitudinal * longitudinal + lateral * lateral);

            float combinedSlipAngle = MathF.Atan(combined * settings.tire.lateralPeakSlip); // rad
            combinedLongitudinal = settings.tire.longitudinal.Evaluate(combined * settings.tire.longitudinalPeakSlip)
                * (longitudinalSlip / slip);
            combinedLateral = settings.tire.lateral.Evaluate(combinedSlipAngle / (0.5f * MathF.PI))
                * (lateralSlip / slip);
        }

        /// <summary>
        /// The tire cannot pass on more torque than the wheel actually got in this step: the brake torque,
        /// or the torque that turned the wheel away from road speed (Tire force, torque limit).
        /// </summary>
        private void LimitByWheelTorque(bool isBraking, float dt)
        {
            if (isBraking && MathF.Abs(ForwardSpeed) < HoldSpeed) return; // holding wheels keep their static friction

            float inertia = IsSpinning ? Inertia + DrivelineInertia : Inertia;
            float wheelTorque = MathF.Max(
                inertia * MathF.Abs(AngularVelocity - ForwardSpeed / Radius) / dt,
                BrakeTorque);
            float maxGrip = Load > 0f ? wheelTorque / Radius / Load : 0f;
            LongitudinalGrip = MathHelpers.Clamp(LongitudinalGrip, -maxGrip, maxGrip);
        }

        /// <summary>
        /// The friction circle for a rolling wheel under the anti-lock brakes: braking and cornering
        /// both ask for grip, so both are scaled back onto the circle (Combined slip and friction circle).
        /// </summary>
        private void ApplyFrictionCircle()
        {
            CorneringGrip = LateralGrip; // before the circle: the anti-lock brakes read it in the next step

            if (IsRolling && IsAntiLocked)
            {
                float longitudinal = LongitudinalGrip / MathF.Max(settings.tire.longitudinalPeakGrip, 1e-6f);
                float lateral = LateralGrip / MathF.Max(settings.tire.lateralPeakGrip, 1e-6f);
                float combined = MathF.Sqrt(longitudinal * longitudinal + lateral * lateral);
                if (combined > 1f)
                {
                    LongitudinalGrip /= combined;
                    LateralGrip /= combined;
                }
            }

            if (IsRolling)
            {
                // The slip a real tire would have for this force.
                SlipRatio = slipSearch.SlipAtGrip(MathF.Abs(LongitudinalGrip))
                    * MathHelpers.MathSign(LongitudinalGrip);
            }
        }

        /// <summary>
        /// A driven wheel that gets clearly more than its peak breaks loose and keeps spinning until the
        /// tire slows it back (Wheel spin and inertia, When a wheel breaks loose).
        /// </summary>
        private void UpdateSpinning(float slipSpeed, float forwardSpeed)
        {
            float torque = MathF.Abs(DriveTorque);
            float peakTorque = settings.tire.longitudinalPeakGrip * SuspensionForce * Radius;
            bool isOverloaded = torque > SpinOverload * peakTorque;
            bool isLoose = HasOwnSpin && driveShare > 0f
                && (torque > peakTorque
                    || MathF.Abs(slipSpeed) > MathF.Max(settings.tire.longitudinalPeakSlip * forwardSpeed, MinSpinSpeed));
            IsSpinning = isOverloaded || isLoose;
        }

        /// <summary>How much of the strut force holds the body up along the ground normal (Wheel load).</summary>
        private float UprightPart()
        {
            float upright = up.y;
            if (ContactNormal.y < SteepGroundNormal)
            {
                upright = MathHelpers.Lerp(Vec3.Dot(up, ContactNormal), up.y,
                    MathF.Max(ContactNormal.y, 0f) / SteepGroundNormal);
            }
            return upright;
        }

        /// <summary>Forget spin and grip, for a reset to standstill (The physics step in order, outside the step).</summary>
        public void ResetToStandstill()
        {
            ResetTire();
            IsGrounded = false;
            GroundBody = null;
            GroundVelocity = Vec3.Zero;
            ForwardSpeed = 0f;
            SideSpeed = 0f;
            SpringLength = settings.suspensionTravel;
            SuspensionForce = 0f;
            ContactPoint = Vec3.Zero;
            ContactNormal = Vec3.Up;
            AngularVelocity = 0f;
            ShownAngularVelocity = 0f;
            DriveTorque = 0f;
            BrakeTorque = 0f;
            SpinBrakeTorque = 0f;
            DrivelineInertia = 0f;
            HasOwnSpin = false;
            IsSpinning = false;
            IsLocked = false;
            IsRolling = false;
            IsAntiLockActive = false;
            IsAntiLocked = false;
            SteerAngle = 0f;
            SpinAngleDeg = 0f;
            VisualSteerAngleDeg = 0f;
        }

        /// <summary>Display only: 200 deg/s steering and the shown spin (Steering by turn radius, Visual wheels).</summary>
        public void UpdateVisual(float frameDt)
        {
            const float visualSteerSpeed = 200f; // deg/s, display only
            SpinAngleDeg = (SpinAngleDeg + ShownAngularVelocity * MathHelpers.RadToDeg * frameDt) % 360f;
            VisualSteerAngleDeg = MathHelpers.MoveTowards(VisualSteerAngleDeg, SteerAngle, visualSteerSpeed * frameDt);
        }
    }
}
