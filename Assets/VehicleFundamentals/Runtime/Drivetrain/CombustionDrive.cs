using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// The combustion engine with its automatic clutch, automatic gearbox, engine braking and rev limiter.
    /// Chapters: Engine and torque curve, Clutch, Gearbox and shifting, Engine braking.
    /// The engine speed comes from the driven wheels; only the torque comes back.
    /// </summary>
    public class CombustionDrive : IVehicleDrive
    {
        public const float ClutchClosedThrottle = 0.25f; // throttle at which the clutch is fully closed
        public const float RevUpRate = 11f;              // rpm/s per N*m: the engine's own torque against a slipping clutch
        public const float ClutchPullRate = 900f;        // rpm/s towards gearbox speed under throttle
        public const float CoastRate = 2600f;            // rpm/s: a rolling gearbox drags the engine along
        public const float IdleReturnRate = 1800f;       // rpm/s back to idle once the clutch opens
        public const float ClutchReleaseFactor = 1.25f;  // x idle: off throttle the clutch stays closed above this
        public const float FullEngineBrakeShare = 0.9f;  // x max rpm: engine braking reaches full torque here
        public const float LimiterBand = 250f;           // rpm below max where the limiter fades the torque
        public const float ShiftDelay = 0.4f;            // s between two automatic shifts
        public const float DirectionChangeSpeed = 1f;    // m/s

        public EngineSettings engine;
        public GearboxSettings gearbox;

        public int gear = 1;       // -1 reverse, 1 first gear; there is no neutral, the clutch takes that role
        public float rpm;
        public bool isAtLimiter;
        public bool picksGear;

        /// <summary>How far the automatic clutch is closed: 0 open, 1 fully engaged (Clutch chapter).</summary>
        public float Clutch { get; private set; }

        private float shiftTimer;

        public CombustionDrive(EngineSettings engine, GearboxSettings gearbox)
        {
            this.engine = engine.Clone();
            this.gearbox = gearbox.Clone();
            rpm = engine.idleRpm;
        }

        public bool IsReverse => gear < 0;

        /// <summary>The total ratio: gear x final drive, negative in reverse (Gearbox and shifting).</summary>
        public float GearRatio()
        {
            GearboxSettings g = gearbox;
            float gearRatio = gear < 0 ? -g.reverseRatio : g.gearRatios[Math.Max(0, Math.Min(gear - 1, g.gearRatios.Length - 1))];
            return gearRatio * g.finalDriveRatio;
        }

        /// <summary>The rpm at the highest key of the torque curve, where the engine pulls hardest.</summary>
        public float LaunchRpm() =>
            MathHelpers.Lerp(engine.idleRpm, engine.maxRpm, engine.torqueCurve.PeakTime());

        public float Inertia
        {
            get
            {
                float ratio = GearRatio();
                return engine.inertia * ratio * ratio;
            }
        }

        public float Simulate(Vehicle vehicle, VehicleInput input, float dt)
        {
            SelectDirection(vehicle.TravelSpeed, input.throttle);
            if (picksGear && vehicle.HasDrivenGroundContact)
            {
                picksGear = false;
                if (gear > 0) gear = GearForSpeed(vehicle.DrivenRollingSpeed);
            }
            if (vehicle.HasDrivenGroundContact) ShiftAutomatically(vehicle, dt); // never in the air

            float ratio = GearRatio(); // gear x final drive, negative in reverse
            float gearboxRpm = vehicle.DrivenWheelSpeed * ratio * MathHelpers.RadPerSecToRpm;
            float gearboxSpeed = MathF.Abs(gearboxRpm);
            float throttle = MathHelpers.Clamp01(input.throttle * MathHelpers.MathSign(ratio));
            float idleRpm = engine.idleRpm;
            float maxRpm = engine.maxRpm;
            float clutchReleaseRpm = idleRpm * ClutchReleaseFactor;

            rpm = MathHelpers.Clamp(rpm, idleRpm, maxRpm);
            isAtLimiter = false;

            float throttleTorque = engine.torqueCurve.Evaluate(
                MathHelpers.InverseLerp(idleRpm, maxRpm, rpm)) * engine.maxTorque * throttle;
            float clutch = MathHelpers.Clamp01(throttle / ClutchClosedThrottle);
            Clutch = throttle > 0f ? clutch : gearboxSpeed > clutchReleaseRpm ? 1f : 0f;

            // Against the turning direction, so it also slows a car rolling backwards in a forward gear.
            float engineBrake = engine.engineBrakeTorque
                * MathHelpers.InverseLerp(clutchReleaseRpm, maxRpm * FullEngineBrakeShare, gearboxSpeed)
                * MathHelpers.MathSign(gearboxRpm);

            if (throttle > 0f && clutch >= 1f)
            {
                // Closed: slips at peak-torque rpm when moving off, then turns with the driven wheels.
                float wheelRpm = MathF.Abs(vehicle.DrivenTurningSpeed * ratio * MathHelpers.RadPerSecToRpm);
                rpm = MathF.Max(wheelRpm, LaunchRpm());
                isAtLimiter = wheelRpm > maxRpm - LimiterBand;
                if (isAtLimiter) throttleTorque *= MathHelpers.InverseLerp(maxRpm, maxRpm - LimiterBand, wheelRpm);
            }
            else if (throttle > 0f)
            {
                rpm += throttleTorque * RevUpRate * dt;
                rpm = MathHelpers.MoveTowards(rpm, MathF.Max(gearboxSpeed, idleRpm), clutch * ClutchPullRate * dt);
            }
            else if (gearboxSpeed > clutchReleaseRpm)
            {
                rpm = MathHelpers.MoveTowards(rpm, gearboxSpeed, CoastRate * dt);
            }
            else
            {
                rpm = MathHelpers.MoveTowards(rpm, idleRpm, IdleReturnRate * dt);
            }

            rpm = MathHelpers.Clamp(rpm, idleRpm, maxRpm);
            return (throttleTorque * clutch - engineBrake) * ratio;
        }

        /// <summary>
        /// Swaps the engine while driving. The next step on the ground picks the gear that matches
        /// the current road speed (Gearbox and shifting, Changing parts while driving).
        /// </summary>
        public void SetEngine(EngineSettings newEngine)
        {
            if (newEngine == null) return;
            engine = newEngine.Clone();
            rpm = MathHelpers.Clamp(rpm, engine.idleRpm, engine.maxRpm);
            picksGear = true;
        }

        /// <summary>
        /// Swaps the gearbox while driving. The gear stays within the new number of gears and the
        /// next step on the ground picks the gear for the current road speed (Gearbox and shifting).
        /// </summary>
        public void SetGearbox(GearboxSettings newGearbox)
        {
            if (newGearbox == null || newGearbox.gearRatios == null || newGearbox.gearRatios.Length == 0) return;
            gearbox = newGearbox.Clone();
            if (gear > gearbox.gearRatios.Length) gear = gearbox.gearRatios.Length;
            picksGear = true;
        }

        /// <summary>Forwards or reverse. Above 1 m/s the throttle only lifts off (Gearbox and shifting).</summary>
        public void SelectDirection(float travelSpeed, float throttle)
        {
            if (throttle < 0f && gear > 0 && travelSpeed < DirectionChangeSpeed)
            {
                gear = -1;
            }
            else if (throttle > 0f && gear < 0 && travelSpeed > -DirectionChangeSpeed)
            {
                gear = 1;
            }
        }

        /// <summary>
        /// Shifts by road speed, not by how fast the wheels actually turn: spinning wheels must not
        /// shift up in a burnout (Gearbox and shifting, Shift by road speed).
        /// </summary>
        public void ShiftAutomatically(Vehicle vehicle, float dt)
        {
            shiftTimer -= dt;
            if (shiftTimer > 0f || gear < 0) return; // reverse has one gear

            float roadRpm = MathF.Abs(vehicle.DrivenRollingSpeed * GearRatio()) * MathHelpers.RadPerSecToRpm;
            float maxRpm = engine.maxRpm;
            int gearCount = gearbox.gearRatios.Length;

            // Slipping at the top of a gear, the wheels take the engine into the limiter before the road
            // reaches the shift-up point. Shift up there too - unless the driver asked for a slide.
            bool isHeldByLimiter = isAtLimiter && vehicle.DriftIntent <= 0f
                && roadRpm * (1f + DrivenPeakSlip(vehicle)) > maxRpm - LimiterBand;

            if (gear < gearCount && (roadRpm > MathF.Min(gearbox.shiftUpRpm, maxRpm) || isHeldByLimiter))
            {
                gear++;
                shiftTimer = ShiftDelay;
            }
            else if (roadRpm < gearbox.shiftDownRpm && gear > 1)
            {
                gear--;
                shiftTimer = ShiftDelay;
            }
        }

        /// <summary>The smallest slip ratio at which a driven tire grips most.</summary>
        public static float DrivenPeakSlip(Vehicle vehicle)
        {
            float peakSlip = 1f;
            for (int i = 0; i < vehicle.Axles.Count; i++)
            {
                Axle axle = vehicle.Axles[i];
                if (axle.Settings.driveShare > 0f)
                    peakSlip = MathF.Min(peakSlip, axle.Settings.tire.longitudinal.PeakTime());
            }
            return peakSlip;
        }

        /// <summary>The lowest gear that stays below the shift-up point at this rolling speed.</summary>
        public int GearForSpeed(float rollingSpeed)
        {
            float shiftUpRpm = MathF.Min(gearbox.shiftUpRpm, engine.maxRpm);
            float wheelRpm = MathF.Abs(rollingSpeed) * MathHelpers.RadPerSecToRpm;
            int result = 1;
            while (result < gearbox.gearRatios.Length
                && wheelRpm * gearbox.gearRatios[result - 1] * gearbox.finalDriveRatio > shiftUpRpm)
            {
                result++;
            }
            return result;
        }

        public void ResetToStandstill()
        {
            gear = 1;
            rpm = engine.idleRpm;
            shiftTimer = 0f;
            isAtLimiter = false;
            picksGear = false;
            Clutch = 0f;
        }
    }
}
