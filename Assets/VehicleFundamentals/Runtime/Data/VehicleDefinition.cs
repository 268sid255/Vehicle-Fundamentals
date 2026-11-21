using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>
    /// A reusable vehicle asset: vehicle, axle, tire and drivetrain data in one file.
    /// Create with Assets/Create/Vehicle Fundamentals/Vehicle Definition or from a preset.
    /// </summary>
    [CreateAssetMenu(fileName = "New Vehicle", menuName = "Vehicle Fundamentals/Vehicle Definition", order = 0)]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "New Vehicle";
        public VehiclePreset preset = VehiclePreset.SportsCoupe;
        [Min(0)] public int visualVariant;

        [TextArea(2, 5)]
        public string description = "";

        [Header("Vehicle")]
        public VehicleSettings settings = new VehicleSettings();

        [Header("Drivetrain")]
        public DriveKind driveKind = DriveKind.Combustion;
        public EngineSettings engine = new EngineSettings();
        public GearboxSettings gearbox = new GearboxSettings();
        public MotorSettings motor = new MotorSettings();

        /// <summary>Builds a fresh runtime vehicle. The definition itself is never modified.</summary>
        public Vehicle Build(IPhysicsWorld physics, AssistSettings assists)
        {
            VehicleSettings data = settings != null ? settings.Clone() : new VehicleSettings();

            IVehicleDrive drive;
            if (driveKind == DriveKind.Electric)
            {
                drive = new ElectricDrive(motor != null ? motor.Clone() : new MotorSettings());
            }
            else
            {
                drive = new CombustionDrive(
                    engine != null ? engine.Clone() : new EngineSettings(),
                    gearbox != null ? gearbox.Clone() : new GearboxSettings());
            }

            return new Vehicle(physics, data, assists != null ? assists.Clone() : new AssistSettings(), drive);
        }

        /// <summary>Copies every number from a built vehicle, for creating an asset from a preset.</summary>
        public void CopyFrom(Vehicle sample)
        {
            if (sample == null) return;

            settings = sample.Settings.Clone();

            if (sample.Drive is CombustionDrive combustion)
            {
                driveKind = DriveKind.Combustion;
                engine = combustion.engine.Clone();
                gearbox = combustion.gearbox.Clone();
            }
            else if (sample.Drive is ElectricDrive electric)
            {
                driveKind = DriveKind.Electric;
                motor = electric.motor.Clone();
            }
        }

        public void CopyFrom(VehicleDefinition other)
        {
            if (other == null || other == this) return;

            displayName = other.displayName;
            preset = other.preset;
            visualVariant = other.visualVariant;
            description = other.description;
            settings = other.settings != null ? other.settings.Clone() : new VehicleSettings();
            driveKind = other.driveKind;
            engine = other.engine != null ? other.engine.Clone() : new EngineSettings();
            gearbox = other.gearbox != null ? other.gearbox.Clone() : new GearboxSettings();
            motor = other.motor != null ? other.motor.Clone() : new MotorSettings();
        }
    }
}
