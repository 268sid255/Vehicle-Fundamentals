using System;
using System.Collections.Generic;

namespace VehicleFundamentals
{
    /// <summary>
    /// The whole vehicle and the one function that runs every fixed physics step.
    /// Chapters: The physics step in order, Drift assist, Burnout, Handbrake drift,
    /// Counter-steer detection, Steering assist, Air drag and speed limit, What an assist does.
    /// </summary>
    public class Vehicle
    {
        // Drift intent and control (Drift assist)
        public const float DriftIntentRise = 3.3f;          // per second: full after 0.3 s, a tap is a third
        public const float DriftIntentFall = 1f;            // per second, once the vehicle has stopped sliding
        public const float MinSlideSpeedSquared = 1f;       // m2/s2; slower, the direction of travel is mostly noise
        public const float ControlMinSlide = 20f;           // deg; below it the slide is being caught, not held
        public const float HoldMinSlide = 8f;               // deg, after the handbrake
        public const float ControlRiseTriggered = 3.3f;     // per second, after a handbrake pull or burnout
        public const float ControlRiseUntriggered = 1f;     // per second, without
        public const float ControlDrop = 4f;                // per second
        public const float ControlThrottle = 0.3f;          // on the throttle from here
        public const float LiftThrottle = 0.15f;            // ... off it below here

        // Drift drive (Drift assist)
        public const float DriftHoldAngle = 45f;            // deg, without drift control
        public const float DriftMaxAngle = 60f;
        public const float DriftDriveFloor = 0.2f;
        public const float ControlledHoldAngle = 50f;       // ... and with it
        public const float ControlledMaxAngle = 70f;
        public const float ControlledDriveFloor = 0.35f;
        public const float DriftLookAhead = 0.15f;          // s

        // Drift hold (Drift assist)
        public const float CounterSteerLead = 0.1f;         // s
        public const float HoldSlideAngle = 40f;            // deg
        public const float HoldGain = 1.5f;                 // deg of closing per deg below the hold angle
        public const float HoldLookAhead = 0.1f;            // s

        // Counter-steer detection
        public const float CounterSteerFadeAngle = 1f;      // deg of rear slide over which it fades in
        public const float CounterSteerMemory = 1.5f;       // s it lasts after the rear stops sliding
        public const float SpinningAngle = 75f;             // deg between heading and travel where it fades out
        public const float BackwardsAngle = 90f;            // ... gone here: the car goes backwards

        // Handbrake drift
        public const float HandbrakeLimitStartSpeed = 50f / 3.6f; // m/s
        public const float HandbrakeLimitFullSpeed = 80f / 3.6f;
        public const float HandbrakeCatchAngle = 30f;       // deg
        public const float HandbrakeLetGoBand = 10f;        // deg below the catch angle
        public const float HandbrakeLookAhead = 0.2f;       // s
        public const float HandbrakeLimitHoldStart = 0.8f;  // s
        public const float HandbrakeLimitHoldEnd = 1.5f;    // s
        public const float RollHandbrakeFrom = 0.6f;        // share of the roll grip

        // Burnout
        public const float BurnoutSlideSpeed = 10f / 3.6f;      // m/s
        public const float BurnoutSlideEndSpeed = 20f / 3.6f;
        public const float BurnoutRearShare = 0.7f;

        // Steering assist
        public const float ReverseSteeringHelp = 0f;   // share of the assist that stays going backwards
        public const float ReverseFadeStart = 1f;      // m/s of travelSpeed
        public const float ReverseFadeEnd = 3f;

        // Air drag and speed limit
        public const float SpeedLimitBand = 2f;        // km/h below the limit where the drive starts to fade
        public const float SlideSpeed = 2f;            // m/s: slower, it is a reverse or a roll
        public const float BackwardsTolerance = 0.577f; // tan 30 deg: further off the axis is a slide

        private readonly IPhysicsWorld physics;
        private readonly VehicleSettings settings;
        private readonly AssistSettings assists;
        private readonly List<Axle> axles = new List<Axle>();
        private readonly List<Wheel> wheels = new List<Wheel>();
        private readonly SteerLimit steerLimit;

        private float driveShareTotal;
        private float rearDriveShareTotal;
        private float rearAxlePosition;
        private float peakSlipAngle = 6f;
        private float peakGrip = 1.05f;

        private float slideAngle;
        private float previousSlideAngle;
        private float slideRate;
        private float driftIntent;
        private float driftControl;
        private float counterSteerSide = 1f;
        private float rearCounterSteered;
        private float handbrakeHeld;
        private float handbrakeSpeed;
        private float burnoutToRear;

        private Vec3 groundVelocity;
        private float speedForward;
        private float travelSpeed;

        public Vehicle(IPhysicsWorld physics, VehicleSettings settings, AssistSettings assists,
            IVehicleDrive drive)
        {
            this.physics = physics;
            this.settings = settings.Clone();
            this.assists = assists != null ? assists.Clone() : new AssistSettings();
            Drive = drive;

            this.settings.Prepare(physics.Gravity.Length);
            BuildAxles();
            steerLimit = new SteerLimit(physics, this.settings, peakGrip, peakSlipAngle);
        }

        public VehicleSettings Settings => settings;
        public AssistSettings Assists => assists;
        public IVehicleDrive Drive { get; set; }
        public object Body { get; set; }
        public VehicleInput Input { get; private set; }

        public IReadOnlyList<Axle> Axles => axles;
        public IReadOnlyList<Wheel> Wheels => wheels;

        public float DriftIntent => driftIntent;
        public float DriftControl => driftControl;
        public float SlideAngle => slideAngle;
        public float SlideRate => slideRate;
        public float GroundVelocityMagnitude => groundVelocity.Length;
        public float SpeedForward => speedForward;
        public float TravelSpeed => travelSpeed;
        public float BurnoutToRear => burnoutToRear;
        public float PeakSlipAngle => peakSlipAngle;
        public float PeakGrip => peakGrip;
        public float RollGrip => steerLimit.RollGrip;
        public float MinTurnRadius => settings.MinTurnRadius;
        public float RearAxlePosition => rearAxlePosition;
        public bool HasDrivenFrontAxle { get; private set; }
        public bool HasDrivenRearAxle { get; private set; }
        public float DriveShareTotal => driveShareTotal;

        /// <summary>How fast the driven wheels actually turn, weighted by axle drive share. Gearbox speed.</summary>
        public float DrivenWheelSpeed => DrivenAverage(WheelValue.ActualSpin);

        /// <summary>Road speed while the wheels grip, their own spin while they spin or fly. Engine speed.</summary>
        public float DrivenTurningSpeed => DrivenAverage(WheelValue.Turning);

        /// <summary>How fast they would turn without slip, from the ground speed. Shifting.</summary>
        public float DrivenRollingSpeed => DrivenAverage(WheelValue.Rolling);

        public bool HasDrivenGroundContact
        {
            get
            {
                for (int i = 0; i < axles.Count; i++)
                {
                    Axle axle = axles[i];
                    if (axle.Settings.driveShare > 0f && (axle.Left.IsGrounded || axle.Right.IsGrounded))
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// The one function that runs every physics step. The order is the whole point:
        /// input and snapshot, slide states, steering, contacts, handbrake, drive, assists, axles, drag, speeds.
        /// </summary>
        public void Simulate(VehicleInput input, float dt)
        {
            if (dt <= 0f || Body == null) return;
            input = input.Clamped();
            Input = input;

            // 1. A snapshot of the body. Forces added below act only after this step.
            Vec3 position = physics.Position(Body);
            Quat rotation = physics.Rotation(Body);
            Vec3 velocity = physics.Velocity(Body);
            Vec3 centerOfMass = physics.CenterOfMass(Body);

            // 2. Velocities in the vehicle's frame, relative to the ground of the previous step.
            Quat toLocal = Quat.Inverse(rotation);
            Vec3 localVelocity = Quat.Rotate(toLocal, velocity - groundVelocity);
            Vec3 localAngularVelocity = Quat.Rotate(toLocal, physics.AngularVelocity(Body));
            Vec3 rearVelocity = localVelocity + Vec3.Cross(localAngularVelocity,
                new Vec3(0f, 0f, rearAxlePosition) - centerOfMass);

            slideAngle = MathHelpers.SlideAngleOf(localVelocity);
            float rearSlideAngle = MathHelpers.SlideAngleOf(rearVelocity);
            slideRate = (slideAngle - previousSlideAngle) / dt;
            previousSlideAngle = slideAngle;

            // 3. What the driver wants.
            float speed = MathF.Sqrt(localVelocity.x * localVelocity.x
                                   + localVelocity.z * localVelocity.z);
            float burnoutSlide = input.burnout * MathF.Max(input.brake,
                MathHelpers.InverseLerp(BurnoutSlideEndSpeed, BurnoutSlideSpeed, speed));
            UpdateDriftIntent(MathF.Max(input.handbrake, burnoutSlide), localVelocity, rearSlideAngle, dt);
            UpdateDriftControl(input, localVelocity, rearVelocity, rearSlideAngle, slideRate, dt);
            float rearCounterSlide = input.steer * rearVelocity.x * rearVelocity.z > 0f
                ? rearSlideAngle
                : 0f;
            float counterSteer = CounterSteerShare(input.steer, rearCounterSlide, localVelocity, dt);

            // 4a. Steering assist on the input.
            float steering = assists.steering * ForwardShare(travelSpeed);
            float driftAssist = assists.drift;
            float steer = input.steer;
            if (steering > 0f)
            {
                float freeCounterSteer = counterSteer
                    * MathHelpers.InverseLerp(0.5f * peakSlipAngle, peakSlipAngle,
                        MathF.Max(rearCounterSlide, slideAngle));
                float limited = steerLimit.Limit(steer, localVelocity, localAngularVelocity.y,
                    RearSlipAngle(), driftIntent, freeCounterSteer, dt);
                steer = MathHelpers.Fade(steer, limited, steering);
            }

            for (int i = 0; i < axles.Count; i++)
            {
                Axle axle = axles[i];

                // 4b. Ackermann angles, then the per-wheel limit.
                axle.Steer(steer, settings.MinTurnRadius, settings.turnCenter);
                if (steering > 0f)
                {
                    float closing = counterSteer * MathF.Max(0f, -slideRate) * CounterSteerLead;
                    if (counterSteer > 0f && driftAssist > 0f)
                    {
                        float hold = driftControl
                            * MathHelpers.InverseLerp(LiftThrottle, ControlThrottle, input.throttle)
                            * driftAssist
                            * MathHelpers.InverseLerp(0f, peakSlipAngle, rearSlideAngle);
                        float slideAhead = slideAngle + HoldLookAhead * MathF.Max(0f, slideRate);
                        closing += counterSteer * hold * HoldGain
                            * MathF.Max(0f, HoldSlideAngle - slideAhead);
                    }

                    axle.LimitSteer(localVelocity, localAngularVelocity, centerOfMass,
                        (1f - driftIntent) * steering, counterSteer * steering, closing);
                }

                // 4c. Rays, springs, dampers, anti-roll bar - with the steer angles just set.
                axle.UpdateContacts(Body, position, rotation);
            }

            // 5. Handbrake limit.
            float handbrake = LimitHandbrake(input.handbrake, localVelocity,
                localAngularVelocity.y, slideRate, dt);

            // 6. Drive torque. The drive sees the wheel speeds of the previous step.
            burnoutToRear = HasDrivenFrontAxle && HasDrivenRearAxle
                ? input.burnout * driftIntent
                : 0f;
            float driveTorque = Drive != null ? Drive.Simulate(this, input, dt) : 0f;
            if (settings.speedLimit > 0f && driveTorque > 0f)
            {
                // Only the push is cut, engine braking stays.
                float kmh = speedForward * 3.6f;
                driveTorque *= MathHelpers.InverseLerp(settings.speedLimit,
                    settings.speedLimit - SpeedLimitBand, kmh);
            }
            float drivelineInertia = Drive != null ? Drive.Inertia : 0f;

            // 7. What the assists ask of the axles in this step.
            float corneringBrakes = (1f - driftIntent) * assists.brakes;
            float driftDrive = driftAssist > 0f && !HasDrivenFrontAxle && input.throttle != 0f
                ? MathHelpers.Fade(1f,
                    DriftDrive(MathF.Abs(input.steer) * counterSteer, localVelocity, slideRate),
                    driftAssist)
                : 1f;
            float driftGrip = driftControl * driftAssist;
            float frontRelease = driftIntent * (1f - burnoutToRear);

            var rearAssists = new AssistState
            {
                driftIntent = driftIntent,
                tractionBrakeRelease = driftIntent,
                traction = assists.traction,
                antiLock = assists.brakes,
                corneringBrakes = corneringBrakes,
                driftDrive = driftDrive,
                driftGrip = driftGrip,
            };
            var frontAssists = new AssistState
            {
                driftIntent = driftIntent,
                tractionBrakeRelease = frontRelease,
                traction = assists.traction,
                antiLock = assists.brakes,
                corneringBrakes = 0f,
                driftDrive = driftDrive,
                driftGrip = 0f,
            };

            // 8. Per axle: torque split, brakes, traction control, differential and every wheel's tire.
            for (int i = 0; i < axles.Count; i++)
            {
                Axle axle = axles[i];
                AxleSettings s = axle.Settings;
                float driveShare = driveShareTotal > 0f ? s.driveShare / driveShareTotal : 0f;
                if (burnoutToRear > 0f) driveShare = DriveWeight(axle) / driveShareTotal;
                float axleTorque = driveTorque * driveShare;

                bool spinsInBurnout = s.driveShare > 0f && (axle.IsRear || !HasDrivenRearAxle);
                float brake = spinsInBurnout ? input.brake * (1f - input.burnout) : input.brake;
                float brakeTorque = settings.maxBrakeTorque * brake * s.brakeShare;
                float handbrakeTorque = settings.maxHandbrakeTorque * handbrake * s.handbrakeShare;

                axle.Simulate(Body, axleTorque, drivelineInertia * driveShare,
                    brakeTorque, handbrakeTorque, axle.IsRear ? rearAssists : frontAssists, dt);
            }

            // 9. Air drag on the body, at the centre of mass.
            ApplyAirDrag(velocity, position + Quat.Rotate(rotation, centerOfMass));

            // 10. For the next step: the ground's velocity and the speed over it.
            UpdateSpeed(rotation, velocity);
        }

        private void BuildAxles()
        {
            axles.Clear();
            wheels.Clear();
            driveShareTotal = 0f;
            rearDriveShareTotal = 0f;
            HasDrivenFrontAxle = false;
            HasDrivenRearAxle = false;

            // Every axle behind the centre of mass is a rear axle. If all are ahead of it, the
            // rearmost one counts as the rear (What an assist does).
            bool anyRear = false;
            for (int i = 0; i < settings.axles.Count; i++)
                if (settings.axles[i].position < settings.centerOfMass.z) anyRear = true;
            float rearmost = float.PositiveInfinity;
            for (int i = 0; i < settings.axles.Count; i++)
                rearmost = MathF.Min(rearmost, settings.axles[i].position);

            rearAxlePosition = float.IsInfinity(rearmost) ? 0f : rearmost;
            peakGrip = float.PositiveInfinity;
            peakSlipAngle = float.PositiveInfinity;

            for (int i = 0; i < settings.axles.Count; i++)
            {
                AxleSettings s = settings.axles[i];
                s.isRear = anyRear ? s.position < settings.centerOfMass.z : s.position == rearmost;
                Axle axle = new Axle(physics, s);
                axles.Add(axle);
                wheels.Add(axle.Left);
                wheels.Add(axle.Right);

                float share = MathF.Max(0f, s.driveShare);
                driveShareTotal += share;
                if (s.isRear) rearDriveShareTotal += share;
                if (share > 0f)
                {
                    if (s.isRear) HasDrivenRearAxle = true;
                    else HasDrivenFrontAxle = true;
                }

                peakGrip = MathF.Min(peakGrip, s.tire.lateralPeakGrip);
                // The drift states are read at the rear axle; its tire is the reference.
                if (s.isRear) peakSlipAngle = MathF.Min(peakSlipAngle, s.tire.lateralPeakAngleDeg);
            }

            if (float.IsInfinity(peakGrip)) peakGrip = 1.05f;
            if (float.IsInfinity(peakSlipAngle))
            {
                for (int i = 0; i < settings.axles.Count; i++)
                    peakSlipAngle = MathF.Min(peakSlipAngle, settings.axles[i].tire.lateralPeakAngleDeg);
            }
            if (float.IsInfinity(peakSlipAngle)) peakSlipAngle = 6f;
            peakSlipAngle = MathF.Max(peakSlipAngle, 0.5f);
        }

        /// <summary>Drift intent: rises while the handbrake is held or a burnout is asked for, stays through the slide.</summary>
        private void UpdateDriftIntent(float slideRequest, Vec3 localVelocity, float rearSlideAngle, float dt)
        {
            if (slideRequest > 0f)
            {
                driftIntent = MathHelpers.MoveTowards(driftIntent, 1f,
                    DriftIntentRise * slideRequest * dt);
                return;
            }

            bool isSliding = localVelocity.x * localVelocity.x
                           + localVelocity.z * localVelocity.z > MinSlideSpeedSquared
                && rearSlideAngle >= peakSlipAngle;
            if (!isSliding)
                driftIntent = MathHelpers.MoveTowards(driftIntent, 0f, DriftIntentFall * dt);
        }

        /// <summary>Drift control: builds while the driver counter-steers and holds the throttle, drops at once on a lift or brake.</summary>
        private void UpdateDriftControl(VehicleInput input, Vec3 localVelocity, Vec3 rearVelocity,
            float rearSlideAngle, float slideRate, float dt)
        {
            float minSlide = MathHelpers.Lerp(ControlMinSlide, HoldMinSlide,
                driftIntent * assists.drift);
            bool isSliding = localVelocity.x * localVelocity.x
                           + localVelocity.z * localVelocity.z > MinSlideSpeedSquared
                && rearSlideAngle >= minSlide;

            // > 0: steering the way the rear slides (counter-steer), forwards or backwards.
            float towardsSlide = input.steer * MathHelpers.SignPos(rearVelocity.x)
                * MathHelpers.SignPos(rearVelocity.z);
            bool backsOff = input.brake > 0.1f
                || input.throttle < LiftThrottle
                || (towardsSlide < -0.5f && slideRate > 0f);
            if (!isSliding || backsOff)
            {
                driftControl = MathHelpers.MoveTowards(driftControl, 0f, ControlDrop * dt);
                return;
            }

            if (towardsSlide > 0.5f && input.throttle >= ControlThrottle)
            {
                float rise = driftIntent > 0.5f ? ControlRiseTriggered : ControlRiseUntriggered;
                driftControl = MathHelpers.MoveTowards(driftControl, 1f, rise * dt);
            }
        }

        /// <summary>
        /// The driver counter-steers: steers towards the side the rear slides to. Read at the rear
        /// axle, with a 1.5 s memory; the side changes only with the driver.
        /// </summary>
        private float CounterSteerShare(float steer, float rearCounterSlide, Vec3 localVelocity, float dt)
        {
            float rearShare = MathHelpers.InverseLerp(0f, CounterSteerFadeAngle, rearCounterSlide);
            if (rearShare > 0f) counterSteerSide = MathHelpers.SignPos(steer);

            rearCounterSteered = MathF.Max(rearShare,
                MathHelpers.MoveTowards(rearCounterSteered, 0f, dt / CounterSteerMemory));
            float heading = MathF.Atan2(MathF.Abs(localVelocity.x), localVelocity.z)
                * MathHelpers.RadToDeg; // 0 forwards, 180 backwards
            float share = steer * counterSteerSide > 0f ? rearCounterSteered : 0f;
            return share * MathHelpers.InverseLerp(BackwardsAngle, SpinningAngle, heading);
        }

        /// <summary>The larger rear wheel's slip angle from the previous step.</summary>
        private float RearSlipAngle()
        {
            float result = 0f;
            bool any = false;
            for (int i = 0; i < axles.Count; i++)
            {
                Axle axle = axles[i];
                if (!axle.IsRear) continue;
                result = MathF.Max(result, MathF.Max(axle.Left.SlipAngleDeg, axle.Right.SlipAngleDeg));
                any = true;
            }
            return any ? result : slideAngle;
        }

        /// <summary>The handbrake lets go as the slide nears what counter-steering can still catch.</summary>
        private float LimitHandbrake(float handbrake, Vec3 localVelocity, float yawRate,
            float slideRate, float dt)
        {
            if (handbrake <= 0f)
            {
                handbrakeHeld = 0f;
                return handbrake;
            }

            float speed = MathF.Sqrt(localVelocity.x * localVelocity.x
                                   + localVelocity.z * localVelocity.z);
            if (handbrakeHeld == 0f) handbrakeSpeed = speed; // the speed of the pull
            handbrakeHeld += dt;

            if (assists.drift > 0f)
            {
                float strength = MathHelpers.InverseLerp(HandbrakeLimitStartSpeed,
                        HandbrakeLimitFullSpeed, handbrakeSpeed)
                    * (1f - MathHelpers.InverseLerp(HandbrakeLimitHoldStart,
                        HandbrakeLimitHoldEnd, handbrakeHeld))
                    * assists.drift;
                float slide = slideAngle + HandbrakeLookAhead * MathF.Max(0f, slideRate);
                float share = 1f - MathHelpers.InverseLerp(
                    HandbrakeCatchAngle - HandbrakeLetGoBand, HandbrakeCatchAngle, slide);
                handbrake *= MathHelpers.Lerp(1f, share, strength);
            }

            if (assists.steering > 0f && steerLimit.RollGrip < float.PositiveInfinity)
            {
                float g = physics.Gravity.Length;
                float lateral = MathF.Abs(yawRate) * speed / (steerLimit.RollGrip * g);
                handbrake *= MathHelpers.Lerp(1f,
                    1f - MathHelpers.InverseLerp(RollHandbrakeFrom, 1f, lateral),
                    assists.steering);
            }

            return handbrake;
        }

        /// <summary>The share of drive torque a drift leaves, by the slide angle and how far the driver counter-steers.</summary>
        private float DriftDrive(float counterSteer, Vec3 localVelocity, float slideRate)
        {
            if (localVelocity.x * localVelocity.x
              + localVelocity.z * localVelocity.z < MinSlideSpeedSquared) return 1f;

            float slide = slideAngle + DriftLookAhead * MathF.Max(0f, slideRate);
            float control = driftControl * assists.drift;
            float holdAngle = MathHelpers.Lerp(DriftHoldAngle, ControlledHoldAngle, control);
            float maxAngle = MathHelpers.Lerp(DriftMaxAngle, ControlledMaxAngle, control);
            float floor = MathHelpers.Lerp(DriftDriveFloor, ControlledDriveFloor, control);
            float share = MathHelpers.Lerp(1f, floor,
                MathHelpers.InverseLerp(holdAngle, maxAngle, slide));
            return MathHelpers.Lerp(1f, share, counterSteer);
        }

        /// <summary>An axle's drive share; in an all-wheel-drive burnout shifted towards 70 % rear, keeping the total.</summary>
        private float DriveWeight(Axle axle)
        {
            float share = axle.Settings.driveShare;
            if (burnoutToRear <= 0f) return share;

            float axlesShare = axle.IsRear
                ? rearDriveShareTotal
                : driveShareTotal - rearDriveShareTotal;
            if (axlesShare <= 0f) return share;
            float burnoutShare = share / axlesShare * driveShareTotal
                * (axle.IsRear ? BurnoutRearShare : 1f - BurnoutRearShare);
            return MathHelpers.Lerp(share, burnoutShare, burnoutToRear);
        }

        /// <summary>F = -1/2 rho CdA |v| v, against the direction of travel, at the centre of mass.</summary>
        private void ApplyAirDrag(Vec3 velocity, Vec3 worldCentreOfMass)
        {
            if (settings.dragArea <= 0f) return;
            Vec3 force = velocity * (-0.5f * MathHelpers.AirDensity * settings.dragArea * velocity.Length);
            physics.AddForceAtPosition(Body, force, worldCentreOfMass);
        }

        /// <summary>Ground velocity and travel speed for the next step.</summary>
        private void UpdateSpeed(Quat rotation, Vec3 velocity)
        {
            Vec3 sum = Vec3.Zero;
            int grounded = 0;
            for (int i = 0; i < wheels.Count; i++)
            {
                Wheel wheel = wheels[i];
                if (!wheel.IsGrounded) continue;
                sum += wheel.GroundVelocity;
                grounded++;
            }

            groundVelocity = grounded > 0 ? sum / grounded : Vec3.Zero;
            Vec3 relative = velocity - groundVelocity;
            speedForward = Vec3.Dot(relative, Quat.Rotate(rotation, Vec3.Forward));
            travelSpeed = speedForward;

            if (speedForward < 0f)
            {
                float sideways = Vec3.Dot(relative, Quat.Rotate(rotation, Vec3.Right));
                float planar = MathF.Sqrt(speedForward * speedForward + sideways * sideways);
                // Backwards fast and well off the axis: a slide with the tail swung round, not a reverse.
                if (planar >= SlideSpeed
                 && MathF.Abs(sideways) > -speedForward * BackwardsTolerance)
                {
                    travelSpeed = planar;
                }
            }
        }

        private enum WheelValue
        {
            ActualSpin,
            Turning,
            Rolling,
        }

        /// <summary>Average of the driven wheels, weighted by each axle's drive share.</summary>
        private float DrivenAverage(WheelValue value)
        {
            float sum = 0f;
            float total = 0f;
            for (int i = 0; i < axles.Count; i++)
            {
                Axle axle = axles[i];
                float share = axle.Settings.driveShare;
                if (share <= 0f) continue;

                float left;
                float right;
                switch (value)
                {
                    case WheelValue.ActualSpin:
                        left = axle.Left.AngularVelocity;
                        right = axle.Right.AngularVelocity;
                        break;
                    case WheelValue.Turning:
                        left = axle.Left.TurningAngularVelocity;
                        right = axle.Right.TurningAngularVelocity;
                        break;
                    default:
                        left = axle.Left.ForwardSpeed / axle.Left.Radius;
                        right = axle.Right.ForwardSpeed / axle.Right.Radius;
                        break;
                }

                sum += (left + right) * 0.5f * share;
                total += share;
            }
            return total > 0f ? sum / total : 0f;
        }

        /// <summary>The steering assist fades out going backwards between 1 and 3 m/s of travel speed.</summary>
        private static float ForwardShare(float travelSpeed)
        {
            return MathHelpers.Lerp(1f, ReverseSteeringHelp,
                MathHelpers.InverseLerp(-ReverseFadeStart, -ReverseFadeEnd, travelSpeed));
        }

        /// <summary>Forget the assist states, ground velocity and the drive's gear and rpm.</summary>
        public void ResetToStandstill()
        {
            slideAngle = 0f;
            previousSlideAngle = 0f;
            slideRate = 0f;
            driftIntent = 0f;
            driftControl = 0f;
            counterSteerSide = 1f;
            rearCounterSteered = 0f;
            handbrakeHeld = 0f;
            handbrakeSpeed = 0f;
            burnoutToRear = 0f;
            groundVelocity = Vec3.Zero;
            speedForward = 0f;
            travelSpeed = 0f;
            Input = VehicleInput.None;

            for (int i = 0; i < axles.Count; i++) axles[i].ResetToStandstill();
            steerLimit.ResetToStandstill();
            if (Drive != null) Drive.ResetToStandstill();
        }
    }
}
