using System.Collections.Generic;

namespace VehicleFundamentals
{
    /// <summary>The ready vehicles of Vehicle types and tuning. All settings are plain data.</summary>
    public enum VehiclePreset
    {
        SportsCoupe,
        CityCar,
        Compact,
        AwdHatch,
        Supercar,
        Minibus,
        BoxTruck,
        ElectricSportsCoupe,
        Truck,
        Pickup,
        Drift,
    }

    /// <summary>
    /// Vehicle definitions based on Vehicle types and tuning, with suspension tuned per preset.
    /// Springs follow static axle loads, dampers use corner mass and damping ratio, and anti-roll
    /// bars control body roll without adding artificial weight transfer.
    /// </summary>
    public static class VehiclePresets
    {
        public static Vehicle Create(VehiclePreset preset, IPhysicsWorld physics)
        {
            switch (preset)
            {
                case VehiclePreset.CityCar: return CreateCityCar(physics);
                case VehiclePreset.Compact: return CreateCompact(physics);
                case VehiclePreset.AwdHatch: return CreateAwdHatch(physics);
                case VehiclePreset.Supercar: return CreateSupercar(physics);
                case VehiclePreset.Minibus: return CreateMinibus(physics);
                case VehiclePreset.BoxTruck: return CreateBoxTruck(physics);
                case VehiclePreset.ElectricSportsCoupe: return CreateElectricSportsCoupe(physics);
                case VehiclePreset.Truck: return CreateTruck(physics);
                case VehiclePreset.Pickup: return CreatePickup(physics);
                case VehiclePreset.Drift: return CreateDrift(physics);
                default: return CreateSportsCoupe(physics);
            }
        }

        /// <summary>The default rear-wheel-drive coupé: 1420 kg, 3.0 litre straight six, five-speed automatic.</summary>
        public static Vehicle CreateSportsCoupe(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 1420f,
                centerOfMass = new Vec3(0f, 0.45f, 0.125f),
                turningCircle = 10.4f,
                turnCenter = -1.26f,
                maxBrakeTorque = 4000f,
                maxHandbrakeTorque = 8000f,
                speedLimit = 0f,
                dragArea = 0.64f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 1.51f, 1.48f, 0.53099f, 0.35f, 20f, 0.22f,
                        48000f, 4950f, 10200f,
                        0f, 0f, 1f, 0f, TireSettings.FromPeakLateralGrip(1.05f)),
                    Axle("Rear", -1.26f, 1.48f, 0.53099f, 0.35f, 20f, 0.22f,
                        48000f, 4950f, 10200f,
                        1f, 0.5f, 0.5f, 1f, TireSettings.FromPeakLateralGrip(1.05f)),
                },
            };

            return Build(physics, settings,
                new CombustionDrive(new EngineSettings(), new GearboxSettings()));
        }

        public static Vehicle CreateCityCar(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 990f,
                centerOfMass = new Vec3(0f, 0.55f, 0.476f),
                turningCircle = 9.8f,
                turnCenter = -1.00f,
                maxBrakeTorque = 2500f,
                maxHandbrakeTorque = 4000f,
                dragArea = 0.68f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 1.42f, 1.45f, 0.60403f, 0.30f, 15f, 0.28f,
                        26800f, 3220f, 13500f,
                        1f, 0f, 1f, 0f, TireSettings.FromPeakLateralGrip(0.95f)),
                    Axle("Rear", -1.00f, 1.45f, 0.58558f, 0.30f, 15f, 0.28f,
                        17200f, 2060f, 8600f,
                        0f, 0f, 0.5f, 1f, TireSettings.FromPeakLateralGrip(0.95f)),
                },
            };

            var engine = new EngineSettings
            {
                maxTorque = 91f,
                idleRpm = 800f,
                maxRpm = 6300f,
                engineBrakeTorque = 11f,
                inertia = 0.08f,
            };
            var gearbox = new GearboxSettings
            {
                gearRatios = new[] { 3.64f, 1.96f, 1.28f, 0.97f, 0.80f },
                reverseRatio = 3.4f,
                finalDriveRatio = 4.20f,
                shiftUpRpm = 5900f,
                shiftDownRpm = 2400f,
            };
            return Build(physics, settings, new CombustionDrive(engine, gearbox));
        }

        public static Vehicle CreateCompact(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 1330f,
                centerOfMass = new Vec3(0f, 0.53f, 0.580f),
                turningCircle = 10.9f,
                turnCenter = -1.03f,
                maxBrakeTorque = 3200f,
                maxHandbrakeTorque = 6500f,
                dragArea = 0.66f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 1.61f, 1.50f, 0.6287f, 0.32f, 18f, 0.26f,
                        43600f, 4630f, 15900f,
                        1f, 0f, 1f, 0f, TireSettings.FromPeakLateralGrip(1.00f)),
                    Axle("Rear", -1.03f, 1.50f, 0.62885f, 0.32f, 18f, 0.26f,
                        27900f, 2960f, 10200f,
                        0f, 0f, 0.5f, 1f, TireSettings.FromPeakLateralGrip(1.00f)),
                },
            };

            var engine = new EngineSettings
            {
                maxTorque = 200f,
                idleRpm = 750f,
                maxRpm = 6500f,
                engineBrakeTorque = 15f,
                inertia = 0.12f,
            };
            var gearbox = new GearboxSettings
            {
                gearRatios = new[] { 3.77f, 2.09f, 1.39f, 1.03f, 0.84f, 0.69f },
                reverseRatio = 3.6f,
                finalDriveRatio = 4.00f,
                shiftUpRpm = 6100f,
                shiftDownRpm = 2600f,
            };
            return Build(physics, settings, new CombustionDrive(engine, gearbox));
        }

        /// <summary>All-wheel drive, 50/50, rear limited-slip 0.5, ultra-high-performance tires, 250 km/h limit.</summary>
        public static Vehicle CreateAwdHatch(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 1625f,
                centerOfMass = new Vec3(0f, 0.50f, 0.574f),
                turningCircle = 10.9f,
                turnCenter = -1.03f,
                maxBrakeTorque = 4000f,
                maxHandbrakeTorque = 7000f,
                speedLimit = 250f,
                dragArea = 0.68f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 1.60f, 1.52f, 0.54815f, 0.33f, 20f, 0.24f,
                        74400f, 7290f, 13100f,
                        0.5f, 0f, 1f, 0f, TireSettings.FromPeakLateralGrip(1.16f)),
                    Axle("Rear", -1.03f, 1.52f, 0.54352f, 0.33f, 20f, 0.24f,
                        47600f, 4660f, 8400f,
                        0.5f, 0.5f, 0.5f, 1f, TireSettings.FromPeakLateralGrip(1.16f)),
                },
            };

            var engine = new EngineSettings
            {
                maxTorque = 420f,
                idleRpm = 750f,
                maxRpm = 6700f,
                engineBrakeTorque = 50f,
                inertia = 0.15f,
            };
            var gearbox = new GearboxSettings
            {
                gearRatios = new[] { 3.77f, 2.09f, 1.39f, 1.03f, 0.84f, 0.69f },
                reverseRatio = 3.6f,
                finalDriveRatio = 4.00f,
                shiftUpRpm = 6300f,
                shiftDownRpm = 2800f,
            };
            return Build(physics, settings, new CombustionDrive(engine, gearbox));
        }

        public static Vehicle CreateSupercar(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 1510f,
                centerOfMass = new Vec3(0f, 0.41f, 0.063f),
                turningCircle = 11.5f,
                turnCenter = -1.05f,
                maxBrakeTorque = 4500f,
                maxHandbrakeTorque = 7000f,
                dragArea = 0.62f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 1.60f, 1.62f, 0.52273f, 0.34f, 20f, 0.2f,
                        60600f, 5700f, 0f,
                        0f, 0f, 1f, 0f, TireSettings.FromPeakLateralGrip(1.26f)),
                    Axle("Rear", -1.05f, 1.62f, 0.51686f, 0.34f, 20f, 0.2f,
                        83700f, 7870f, 0f,
                        1f, 0.5f, 0.5f, 1f, TireSettings.FromPeakLateralGrip(1.26f)),
                },
            };

            var engine = new EngineSettings
            {
                maxTorque = 770f,
                idleRpm = 1000f,
                maxRpm = 8200f,
                engineBrakeTorque = 60f,
                inertia = 0.2f,
            };
            var gearbox = new GearboxSettings
            {
                gearRatios = new[] { 3.08f, 2.19f, 1.63f, 1.29f, 1.03f, 0.84f, 0.69f },
                reverseRatio = 3.2f,
                finalDriveRatio = 3.70f,
                shiftUpRpm = 7900f,
                shiftDownRpm = 3800f,
            };
            return Build(physics, settings, new CombustionDrive(engine, gearbox));
        }

        /// <summary>4000 kg, 1.0 m centre of mass: tips before it slides, hence the anti-roll bars and 100 km/h limit.</summary>
        public static Vehicle CreateMinibus(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 4000f,
                centerOfMass = new Vec3(0f, 1.00f, 0.219f),
                turningCircle = 15.7f,
                turnCenter = -1.73f,
                maxBrakeTorque = 6000f,
                maxHandbrakeTorque = 10000f,
                speedLimit = 100f,
                dragArea = 3.0f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 2.60f, 1.75f, 0.74547f, 0.36f, 35f, 0.32f,
                        80000f, 12610f, 98000f,
                        0f, 0f, 1f, 0f, TireSettings.FromPeakLateralGrip(1.00f)),
                    Axle("Rear", -1.73f, 1.75f, 0.71534f, 0.36f, 35f, 0.32f,
                        97700f, 15400f, 119700f,
                        1f, 0f, 0.6f, 1f, TireSettings.FromPeakLateralGrip(1.00f)),
                },
            };

            var engine = new EngineSettings
            {
                maxTorque = 360f,
                idleRpm = 750f,
                maxRpm = 4400f,
                engineBrakeTorque = 30f,
                inertia = 0.25f,
            };
            var gearbox = new GearboxSettings
            {
                gearRatios = new[] { 3.59f, 2.19f, 1.41f, 1.00f, 0.83f },
                reverseRatio = 3.4f,
                finalDriveRatio = 4.18f,
                shiftUpRpm = 4100f,
                shiftDownRpm = 1800f,
            };
            return Build(physics, settings, new CombustionDrive(engine, gearbox));
        }

        /// <summary>10 tonnes, 1.4 m centre of mass, 0.89 grip truck tires and a 90 km/h limit.</summary>
        public static Vehicle CreateBoxTruck(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 10000f,
                centerOfMass = new Vec3(0f, 1.40f, -0.002f),
                turningCircle = 16.5f,
                turnCenter = -1.91f,
                maxBrakeTorque = 12000f,
                maxHandbrakeTorque = 16000f,
                speedLimit = 90f,
                dragArea = 5.0f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 2.86f, 1.80f, 0.82784f, 0.42f, 60f, 0.32f,
                        202100f, 29540f, 239700f,
                        0f, 0f, 1f, 0f, TireSettings.FromPeakLateralGrip(0.89f, 0.007f)),
                    Axle("Rear", -1.91f, 1.80f, 0.80659f, 0.42f, 60f, 0.32f,
                        303200f, 44310f, 359600f,
                        1f, 0f, 0.6f, 1f, TireSettings.FromPeakLateralGrip(0.89f, 0.007f)),
                },
            };

            var engine = new EngineSettings
            {
                maxTorque = 1000f,
                idleRpm = 600f,
                maxRpm = 2500f,
                engineBrakeTorque = 80f,
                inertia = 1.0f,
            };
            var gearbox = new GearboxSettings
            {
                gearRatios = new[] { 6.70f, 3.83f, 2.32f, 1.42f, 1.00f, 0.73f },
                reverseRatio = 6.0f,
                finalDriveRatio = 3.91f,
                shiftUpRpm = 2400f,
                shiftDownRpm = 1200f,
            };
            return Build(physics, settings, new CombustionDrive(engine, gearbox));
        }

        /// <summary>The default coupé with a 150 kW electric motor and one 9:1 reduction gear.</summary>
        public static Vehicle CreateElectricSportsCoupe(IPhysicsWorld physics)
        {
            Vehicle vehicle = CreateSportsCoupe(physics);
            vehicle.Drive = new ElectricDrive(new MotorSettings());
            return vehicle;
        }

        /// <summary>Project extension: a 16 tonne 6x4 rigid truck, fixed tandem rear axles.</summary>
        public static Vehicle CreateTruck(IPhysicsWorld physics)
        {
            var settings = new VehicleSettings
            {
                mass = 16000f,
                centerOfMass = new Vec3(0, 1.25f, -0.2f),
                turningCircle = 19f, turnCenter = -2f,
                maxBrakeTorque = 16000f, maxHandbrakeTorque = 22000f,
                speedLimit = 90f, dragArea = 5.2f,
                axles = new List<AxleSettings>
                {
                    Axle("Front", 2.9f, 2.02f, 0.92449f, 0.50f, 85f, 0.36f,
                        228200f, 34090f, 193800f,
                        0, 0, 1, 0, TireSettings.FromPeakLateralGrip(0.89f, 0.007f)),
                    Axle("Rear 1", -1.3f, 2.02f, 0.93531f, 0.50f, 85f, 0.36f,
                        192600f, 28770f, 163500f,
                        0.5f, 0.4f, 0.65f, 1, TireSettings.FromPeakLateralGrip(0.89f, 0.007f)),
                    Axle("Rear 2", -2.7f, 2.02f, 0.93886f, 0.50f, 85f, 0.36f,
                        198200f, 29620f, 168400f,
                        0.5f, 0.4f, 0.65f, 1, TireSettings.FromPeakLateralGrip(0.89f, 0.007f)),
                },
            };
            settings.axles[1].steering = settings.axles[2].steering = false;
            var engine = new EngineSettings { maxTorque = 1300, idleRpm = 600, maxRpm = 2600,
                engineBrakeTorque = 110, inertia = 1.5f };
            var gearbox = new GearboxSettings { gearRatios = new[] { 6.70f, 3.83f, 2.32f, 1.42f, 1f, 0.73f },
                reverseRatio = 6, finalDriveRatio = 3.91f, shiftUpRpm = 2450, shiftDownRpm = 1150 };
            return Build(physics, settings, new CombustionDrive(engine, gearbox));
        }

        /// <summary>Project extension: an AWD pickup using the same axle, tire and drivetrain simulation.</summary>
        public static Vehicle CreatePickup(IPhysicsWorld physics)
        {
            VehicleSettings settings = CreateAwdHatch(physics).Settings.Clone();
            settings.mass = 1650f;
            settings.centerOfMass = new Vec3(0, 0.65f, 0.1f);
            settings.turningCircle = 12.5f;
            settings.turnCenter = -1.3f;
            settings.axles[0].position = 1.5f;
            settings.axles[1].position = -1.3f;
            settings.axles[0].mountHeight = 0.65781f;
            settings.axles[1].mountHeight = 0.68931f;
            foreach (AxleSettings axle in settings.axles)
            {
                axle.trackWidth = 1.54f;
                axle.wheelRadius = 0.35f;
                axle.suspensionTravel = 0.32f;
                axle.springRate = 41700f;
                axle.damperRate = 4560f;
                axle.antiRollRate = 17700f;
            }
            var engine = new EngineSettings { maxTorque = 475, idleRpm = 750, maxRpm = 6000,
                engineBrakeTorque = 40, inertia = 0.25f };
            return Build(physics, settings, new CombustionDrive(engine, new GearboxSettings()));
        }

        /// <summary>Project extension: a RWD drift setup with increased torque and differential locking.</summary>
        public static Vehicle CreateDrift(IPhysicsWorld physics)
        {
            VehicleSettings settings = CreateSportsCoupe(physics).Settings.Clone();
            settings.turningCircle = 8f;
            settings.axles[1].limitedSlip = 0.9f;
            foreach (AxleSettings axle in settings.axles)
            {
                axle.mountHeight = 0.54146f;
                axle.suspensionTravel = 0.22f;
                axle.springRate = 56100f;
                axle.damperRate = 5360f;
                axle.antiRollRate = 9800f;
            }
            var engine = new EngineSettings { maxTorque = 500, idleRpm = 900, maxRpm = 7500,
                engineBrakeTorque = 40, inertia = 0.25f };
            return Build(physics, settings, new CombustionDrive(engine, new GearboxSettings()));
        }

        private static Vehicle Build(IPhysicsWorld physics, VehicleSettings settings, IVehicleDrive drive)
        {
            return new Vehicle(physics, settings, new AssistSettings(), drive);
        }

        private static AxleSettings Axle(string label, float position, float trackWidth,
            float mountHeight, float wheelRadius, float wheelMass, float travel, float springRate,
            float damperRate, float antiRollRate, float driveShare, float limitedSlip,
            float brakeShare, float handbrakeShare, TireSettings tire)
        {
            return new AxleSettings
            {
                label = label,
                position = position,
                trackWidth = trackWidth,
                mountHeight = mountHeight,
                wheelRadius = wheelRadius,
                wheelMass = wheelMass,
                suspensionTravel = travel,
                springRate = springRate,
                damperRate = damperRate,
                antiRollRate = antiRollRate,
                driveShare = driveShare,
                limitedSlip = limitedSlip,
                brakeShare = brakeShare,
                handbrakeShare = handbrakeShare,
                tire = tire,
            };
        }
    }
}
