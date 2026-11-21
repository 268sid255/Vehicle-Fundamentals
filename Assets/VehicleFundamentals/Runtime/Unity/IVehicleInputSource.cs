namespace VehicleFundamentals
{
    /// <summary>Anything that can drive the vehicle: a keyboard, a gamepad, a replay or an AI.</summary>
    public interface IVehicleInputSource
    {
        VehicleInput ReadInput(Vehicle vehicle, float dt);

        void ResetToStandstill();
    }
}
