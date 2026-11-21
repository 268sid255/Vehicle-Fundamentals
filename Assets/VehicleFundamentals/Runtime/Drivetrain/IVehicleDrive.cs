namespace VehicleFundamentals
{
    /// <summary>
    /// What every drive provides (How a drivetrain works). The vehicle calls Simulate once per step,
    /// after the wheel contacts and before the tire forces.
    /// </summary>
    public interface IVehicleDrive
    {
        /// <summary>Total drive torque at the driven wheels, N*m. Negative drives backwards.</summary>
        float Simulate(Vehicle vehicle, VehicleInput input, float dt);

        /// <summary>kg*m2, engine and gearbox as the driven wheels feel them together in the current gear.</summary>
        float Inertia { get; }

        /// <summary>Forget gear and engine speed, for a reset to standstill.</summary>
        void ResetToStandstill();
    }
}
