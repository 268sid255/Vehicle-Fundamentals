using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// An electric motor with one reduction gear: full torque from standstill, constant power above
    /// the base speed, nothing above max rpm (Electric drive).
    /// </summary>
    public class ElectricDrive : IVehicleDrive
    {
        public MotorSettings motor;
        public float rpm;

        public ElectricDrive(MotorSettings motor)
        {
            this.motor = motor.Clone();
        }

        public float Simulate(Vehicle vehicle, VehicleInput input, float dt)
        {
            float motorSpeed = MathF.Abs(vehicle.DrivenTurningSpeed) * motor.gearRatio; // rad/s
            rpm = motorSpeed * MathHelpers.RadPerSecToRpm;
            if (rpm >= motor.maxRpm) return 0f;

            // T = P / omega, capped by the max torque at low speed. The floor of 1 rad/s avoids
            // dividing by zero at a standstill.
            float torque = MathF.Min(motor.maxTorque, (motor.maxPower * 1000f) / MathF.Max(motorSpeed, 1f));
            return input.throttle * torque * motor.gearRatio;
        }

        public float Inertia
        {
            get
            {
                float ratio = motor.gearRatio;
                return motor.inertia * ratio * ratio;
            }
        }

        public void ResetToStandstill()
        {
            rpm = 0f;
        }
    }
}
