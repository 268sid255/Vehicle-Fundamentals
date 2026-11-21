using System;
using System.Collections.Generic;

namespace VehicleFundamentals
{
    /// <summary>What the driver asks for. Clamped at the start of every vehicle step (What an assist does).</summary>
    [Serializable]
    public struct VehicleInput
    {
        public float throttle;  // -1..1, negative pushes backwards (reverse)
        public float brake;     // 0..1
        public float handbrake; // 0..1
        public float burnout;   // 0..1, asks the driven wheels to spin, as a line lock does
        public float steer;     // -1 (full left) .. 1 (full right), the curvature of the turn

        public VehicleInput(float throttle = 0f, float brake = 0f, float handbrake = 0f,
                            float burnout = 0f, float steer = 0f)
        {
            this.throttle = throttle;
            this.brake = brake;
            this.handbrake = handbrake;
            this.burnout = burnout;
            this.steer = steer;
        }

        public VehicleInput Clamped() => new VehicleInput(
            MathHelpers.Clamp(throttle, -1f, 1f),
            MathHelpers.Clamp01(brake),
            MathHelpers.Clamp01(handbrake),
            MathHelpers.Clamp01(burnout),
            MathHelpers.Clamp(steer, -1f, 1f));

        public static readonly VehicleInput None = new VehicleInput();
    }

    /// <summary>
    /// How much the driving assists help, each 0 (off) .. 1 (full).
    /// Every assist only takes away or lets go (What an assist does).
    /// </summary>
    [Serializable]
    public class AssistSettings
    {
        public float steering = 1f;
        public float traction = 1f;
        public float drift = 1f;
        public float brakes = 1f;

        public void Validate()
        {
            steering = MathHelpers.Clamp01(steering);
            traction = MathHelpers.Clamp01(traction);
            drift = MathHelpers.Clamp01(drift);
            brakes = MathHelpers.Clamp01(brakes);
        }

        public AssistSettings Clone() => new AssistSettings
        {
            steering = steering,
            traction = traction,
            drift = drift,
            brakes = brakes,
        };
    }

    /// <summary>Tire grip curves, relaxation and rolling resistance (Tire grip curves, All settings).</summary>
    [Serializable]
    public class TireSettings
    {
        public Curve longitudinal = DefaultLongitudinal();
        public Curve lateral = DefaultLateral();
        public float longitudinalRelaxationLength = 0.01f; // m
        public float lateralRelaxationLength = 0.007f;     // m
        public float rollingResistance = 0.012f;           // –

        [NonSerialized] public float longitudinalPeakSlip;
        [NonSerialized] public float longitudinalPeakGrip;
        [NonSerialized] public float lateralPeakSlip;
        [NonSerialized] public float lateralPeakGrip;
        [NonSerialized] public float lateralPeakAngleDeg;
        [NonSerialized] public SlipSearch longitudinalSlipSearch;

        /// <summary>Computes the peak values and the slip search. Call once when the vehicle is built.</summary>
        public void Prepare()
        {
            longitudinalPeakSlip = MathF.Max(longitudinal.PeakTime(), 0.01f);
            longitudinalPeakGrip = longitudinal.PeakValue();
            lateralPeakAngleDeg = MathF.Max(lateral.PeakTime() * 90f, 0.5f);
            lateralPeakSlip = MathF.Tan(lateralPeakAngleDeg * MathHelpers.DegToRad);
            lateralPeakGrip = lateral.PeakValue();
            longitudinalSlipSearch = new SlipSearch(longitudinal);
        }

        public TireSettings Clone() => new TireSettings
        {
            longitudinal = longitudinal.Clone(),
            lateral = lateral.Clone(),
            longitudinalRelaxationLength = longitudinalRelaxationLength,
            lateralRelaxationLength = lateralRelaxationLength,
            rollingResistance = rollingResistance,
        };

        /// <summary>Default sport tire: longitudinal peak 1.0 at 0.15 slip, lateral peak 1.05 at 6 degrees.</summary>
        public static Curve DefaultLongitudinal() => new Curve(new[]
        {
            Keys.Key(0f, 0f, 6.6666665f),
            Keys.Key(0.15f, 1f),
            Keys.Key(1f, 0.7f, 0.005827481f),
        });

        public static Curve DefaultLateral() => new Curve(new[]
        {
            Keys.Key(0f, 0f, 15.75f),
            Keys.Key(0.0667f, 1.05f),
            Keys.Key(0.2f, 1f),
            Keys.Key(1f, 0.75f),
        });

        /// <summary>
        /// A shipped tire: all tires share the default shape, scaled to their peak lateral grip.
        /// The longitudinal peak is lower by a factor of 1.05 (Vehicle types and tuning).
        /// </summary>
        public static TireSettings FromPeakLateralGrip(float peakLateralGrip, float rollingResistance = 0.012f)
        {
            float factor = peakLateralGrip / 1.05f;
            return new TireSettings
            {
                longitudinal = DefaultLongitudinal().Scaled(factor),
                lateral = DefaultLateral().Scaled(factor),
                longitudinalRelaxationLength = 0.01f,
                lateralRelaxationLength = 0.007f,
                rollingResistance = rollingResistance,
            };
        }
    }

    /// <summary>One axle: geometry, suspension, drive / brake shares, differential and its tire (All settings).</summary>
    [Serializable]
    public class AxleSettings
    {
        public string label = "Axle";
        public float position = 0f;        // m, local z
        public float trackWidth = 1.48f;   // m
        public float mountHeight = 0.67f;  // m, local y
        public float wheelRadius = 0.35f;  // m
        public float wheelMass = 20f;      // kg
        public float suspensionTravel = 0.4f; // m
        public float springRate = 30672f;  // N/m
        public float damperRate = 3067f;   // N*s/m
        public float antiRollRate = 0f;    // N/m, 0 = off
        public float driveShare = 0f;      // 0..1, normalised over all axles
        public float limitedSlip = 0f;     // 0 = open, 1 = can move all of one side's torque
        public float brakeShare = 1f;      // 0..1
        public float handbrakeShare = 0f;  // 0..1
        public bool steering = true;      // false for fixed tandem axles (tire scrub is physical)
        public TireSettings tire = TireSettings.FromPeakLateralGrip(1.05f);

        [NonSerialized] public bool isRear;
        [NonSerialized] public float staticWheelLoad;

        public float WheelInertia => 0.5f * wheelMass * wheelRadius * wheelRadius; // kg*m2

        public AxleSettings Clone() => new AxleSettings
        {
            label = label,
            position = position,
            trackWidth = trackWidth,
            mountHeight = mountHeight,
            wheelRadius = wheelRadius,
            wheelMass = wheelMass,
            suspensionTravel = suspensionTravel,
            springRate = springRate,
            damperRate = damperRate,
            antiRollRate = antiRollRate,
            driveShare = driveShare,
            limitedSlip = limitedSlip,
            brakeShare = brakeShare,
            handbrakeShare = handbrakeShare,
            steering = steering,
            tire = tire.Clone(),
        };
    }

    /// <summary>The whole machine (All settings).</summary>
    [Serializable]
    public class VehicleSettings
    {
        public float mass = 1420f;                         // kg
        public Vec3 centerOfMass = new Vec3(0f, 0.45f, 0.125f); // local
        public float turningCircle = 10.4f;                // m, diameter at the turn centre line
        public float turnCenter = -1.26f;                  // m, local z, usually the rear axle
        public float maxBrakeTorque = 4000f;               // N*m per wheel at full pedal
        public float maxHandbrakeTorque = 8000f;           // N*m per wheel at full pull
        public float speedLimit = 0f;                      // km/h, 0 = none
        public float dragArea = 0.64f;                     // m2, Cd * A
        public List<AxleSettings> axles = new List<AxleSettings>();

        public float MinTurnRadius => turningCircle * 0.5f;

        public void Prepare(float gravity = 9.81f)
        {
            if (axles == null) return;
            for (int i = 0; i < axles.Count; i++)
            {
                AxleSettings axle = axles[i];
                if (axle != null && axle.tire != null) axle.tire.Prepare();
            }
            // Static spring equilibrium on level ground, including pitch. Works for any axle count.
            float k = 0, kz = 0, kzz = 0, kb = 0, kbz = 0;
            foreach (AxleSettings axle in axles)
            {
                float weight = 2 * axle.springRate, z = axle.position;
                float baseline = axle.suspensionTravel - axle.mountHeight + axle.wheelRadius;
                k += weight; kz += weight * z; kzz += weight * z * z;
                kb += weight * baseline; kbz += weight * baseline * z;
            }
            float determinant = k * kzz - kz * kz;
            if (MathF.Abs(determinant) < 1e-5f)
            {
                foreach (AxleSettings axle in axles) axle.staticWheelLoad = mass * gravity / Math.Max(1, axles.Count * 2);
                return;
            }
            float force = kb - mass * gravity, moment = kbz - mass * gravity * centerOfMass.z;
            float height = (force * kzz - moment * kz) / determinant;
            float pitch = (moment * k - force * kz) / determinant;
            foreach (AxleSettings axle in axles)
                axle.staticWheelLoad = MathF.Max(0, axle.springRate *
                    (axle.suspensionTravel - axle.mountHeight + axle.wheelRadius - height - pitch * axle.position));
        }

        public VehicleSettings Clone()
        {
            var copy = new VehicleSettings
            {
                mass = mass,
                centerOfMass = centerOfMass,
                turningCircle = turningCircle,
                turnCenter = turnCenter,
                maxBrakeTorque = maxBrakeTorque,
                maxHandbrakeTorque = maxHandbrakeTorque,
                speedLimit = speedLimit,
                dragArea = dragArea,
                axles = new List<AxleSettings>(axles != null ? axles.Count : 0),
            };
            if (axles != null)
            {
                for (int i = 0; i < axles.Count; i++)
                    if (axles[i] != null) copy.axles.Add(axles[i].Clone());
            }
            return copy;
        }
    }

    /// <summary>Combustion engine settings (Engine and torque curve, All settings).</summary>
    [Serializable]
    public class EngineSettings
    {
        public float maxTorque = 320f;   // N*m at the peak
        public Curve torqueCurve = new Curve(new[]
        {
            Keys.Key(0f, 0.6f, 1.5f),
            Keys.Key(0.42f, 1f),
            Keys.Key(0.81f, 0.95f, -0.3f),
            Keys.Key(0.97f, 0.9f, -0.5f),
            Keys.Key(1f, 0f, -30f),
        });
        public float idleRpm = 1000f;
        public float maxRpm = 7200f;
        public float engineBrakeTorque = 60f; // N*m near max rpm
        public float inertia = 0.2f;          // kg*m2 at the crankshaft

        public EngineSettings Clone() => new EngineSettings
        {
            maxTorque = maxTorque,
            torqueCurve = torqueCurve.Clone(),
            idleRpm = idleRpm,
            maxRpm = maxRpm,
            engineBrakeTorque = engineBrakeTorque,
            inertia = inertia,
        };
    }

    /// <summary>Automatic gearbox settings (Gearbox and shifting, All settings).</summary>
    [Serializable]
    public class GearboxSettings
    {
        public float[] gearRatios = { 4.20f, 2.49f, 1.66f, 1.24f, 1.00f };
        public float reverseRatio = 3.8f;
        public float finalDriveRatio = 3.556f;
        public float shiftUpRpm = 6500f;
        public float shiftDownRpm = 3000f;

        public GearboxSettings Clone() => new GearboxSettings
        {
            gearRatios = gearRatios != null ? (float[])gearRatios.Clone() : new float[0],
            reverseRatio = reverseRatio,
            finalDriveRatio = finalDriveRatio,
            shiftUpRpm = shiftUpRpm,
            shiftDownRpm = shiftDownRpm,
        };
    }

    /// <summary>Electric motor settings (Electric drive, All settings).</summary>
    [Serializable]
    public class MotorSettings
    {
        public float maxTorque = 300f; // N*m at the motor shaft
        public float maxPower = 150f;  // kW
        public float gearRatio = 9f;   // motor turns per wheel turn
        public float maxRpm = 16000f;
        public float inertia = 0.05f;  // kg*m2 of the rotor

        public MotorSettings Clone() => new MotorSettings
        {
            maxTorque = maxTorque,
            maxPower = maxPower,
            gearRatio = gearRatio,
            maxRpm = maxRpm,
            inertia = inertia,
        };
    }

    /// <summary>Which drivetrain the editable vehicle data uses.</summary>
    public enum DriveKind
    {
        Combustion,
        Electric,
    }
}
