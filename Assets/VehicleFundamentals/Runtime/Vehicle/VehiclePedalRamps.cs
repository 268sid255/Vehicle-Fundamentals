using System;

namespace VehicleFundamentals
{
    /// <summary>
    /// Keyboard input and pedal ramps (Keyboard input and pedal ramps): a key is a switch, so the
    /// throttle builds up to what the tires pass on and the brake to the pedal at which all braked
    /// wheels reach their peak. The steering has no ramp on purpose.
    /// </summary>
    public class VehiclePedalRamps
    {
        public const float CatchUpRate = 5f;           // 1/s until the vehicle pulls
        public const float PullingAcceleration = 0.2f; // m/s2; from here the vehicle counts as pulling
        public const float BuildUpTime = 0.6f;         // s on top of where it pulls, up to 30 km/h
        public const float BuildUpTimeHigh = 1.6f;     // s at 80 km/h and above
        public const float BrakeBuildUpTime = 0.3f;    // s until the brake uses the tires' peak
        public const float FullBrakeSpeed = 2f;        // m/s; below it the brake is full at once
        public const float ThrottleLimitFloor = 0.15f;
        public const float ThrottleLimitRecovery = 0.1f; // per step, while the driven wheels grip
        public const float ReverseSpeed = 0.5f;        // m/s; below it the brake key asks to reverse
        public const float BurnoutSpeed = 10f / 3.6f;  // m/s

        private float throttle;
        private float pullingThrottle = -1f;
        private float onTopHeld;
        private float throttleLimit = 1f;
        private float brakeHeld;
        private float previousSpeed;
        private bool hasPreviousSpeed;
        private bool isBurnout;

        public float Throttle => throttle;
        public float ThrottleLimit => throttleLimit;
        public float Brake => MathHelpers.Clamp01(brakeHeld / BrakeBuildUpTime);
        public bool IsBurnout => isBurnout;

        /// <summary>Builds one input from keyboard-like keys. Call once per fixed step, before the vehicle.</summary>
        public VehicleInput Update(Vehicle vehicle, bool accelerator, bool brakePedal, bool handbrake,
            bool steerLeft, bool steerRight, float dt)
        {
            if (vehicle == null) return VehicleInput.None;

            float speed = MathF.Abs(vehicle.TravelSpeed);
            float acceleration = 0f;
            if (hasPreviousSpeed && dt > 0f) acceleration = (vehicle.TravelSpeed - previousSpeed) / dt;
            previousSpeed = vehicle.TravelSpeed;
            hasPreviousSpeed = true;

            // A brake key near a standstill asks to reverse instead of braking.
            bool wantsReverse = brakePedal && !accelerator && vehicle.TravelSpeed <= ReverseSpeed;
            bool wantsForwardBrake = accelerator && !brakePedal && vehicle.TravelSpeed < -ReverseSpeed;
            bool braking = (brakePedal && !wantsReverse) || wantsForwardBrake;
            float key = wantsForwardBrake ? 0f : accelerator ? 1f : wantsReverse ? -1f : 0f;

            UpdateBurnout(accelerator, brakePedal, speed);
            UpdateThrottle(vehicle, MathF.Abs(key), acceleration * MathHelpers.SignPos(key), dt);

            var input = new VehicleInput();
            input.handbrake = handbrake ? 1f : 0f;
            input.burnout = isBurnout ? 1f : 0f;
            input.steer = (steerRight ? 1f : 0f) - (steerLeft ? 1f : 0f);

            if (key != 0f)
            {
                // In a drift the driver asks for (driftIntent) the throttle key is passed through:
                // then spinning wheels are wanted.
                float limited = MathHelpers.Lerp(MathF.Min(throttle, throttleLimit),
                    MathF.Abs(key), vehicle.DriftIntent);
                input.throttle = limited * MathHelpers.SignPos(key);
            }

            input.brake = braking
                ? BrakeFor(vehicle, speed, dt)
                : 0f;
            if (!braking) brakeHeld = 0f;

            return input.Clamped();
        }

        /// <summary>
        /// Analog pedals and steering from a gamepad: passed through without the keyboard ramps.
        /// The brake doubles as reverse near a standstill, as the brake key does.
        /// </summary>
        public VehicleInput UpdateAnalog(Vehicle vehicle, float throttlePedal, float brakePedal,
            float handbrakePedal, float steer, float dt)
        {
            if (vehicle == null) return VehicleInput.None;

            float speed = MathF.Abs(vehicle.TravelSpeed);
            previousSpeed = vehicle.TravelSpeed;
            hasPreviousSpeed = true;

            bool wantsReverse = brakePedal > 0.1f && throttlePedal < 0.1f &&
                                vehicle.TravelSpeed <= ReverseSpeed;
            // The sample driver's opposite pedal brakes first; the drive still receives separate signed throttle and brake.
            bool wantsForwardBrake = throttlePedal > 0.1f && brakePedal < 0.1f &&
                                     vehicle.TravelSpeed < -ReverseSpeed;
            UpdateBurnout(throttlePedal > 0.5f, brakePedal > 0.5f, speed);

            return new VehicleInput(
                wantsReverse ? -brakePedal : wantsForwardBrake ? 0f : throttlePedal,
                wantsReverse ? 0f : wantsForwardBrake ? throttlePedal : brakePedal,
                handbrakePedal,
                isBurnout ? 1f : 0f,
                steer).Clamped();
        }

        private void UpdateBurnout(bool accelerator, bool brakePedal, float speed)
        {
            if (accelerator && brakePedal && speed < BurnoutSpeed) isBurnout = true;
            else if (!accelerator || brakePedal) isBurnout = false;
        }

        private void UpdateThrottle(Vehicle vehicle, float key, float acceleration, float dt)
        {
            if (key <= 0f)
            {
                throttle = 0f;
                pullingThrottle = -1f;
                onTopHeld = 0f;
                return;
            }

            if (pullingThrottle < 0f)
            {
                throttle = MathHelpers.MoveTowards(throttle, key, CatchUpRate * dt);
                if (acceleration >= PullingAcceleration || throttle >= key)
                    pullingThrottle = throttle;
            }
            else
            {
                onTopHeld += dt;
                float buildUpTime = MathHelpers.Lerp(BuildUpTime, BuildUpTimeHigh,
                    MathHelpers.InverseLerp(30f / 3.6f, 80f / 3.6f, MathF.Abs(vehicle.TravelSpeed)));
                float share = MathHelpers.Clamp01(onTopHeld / buildUpTime);
                throttle = MathHelpers.Lerp(pullingThrottle, key, share * share); // gently at first
            }

            LimitThrottle(DriveOverload(vehicle));
        }

        private void LimitThrottle(float driveOverload)
        {
            if (driveOverload > 1f)
            {
                // Back off in proportion to how far past its peak the wheel is carrying.
                throttleLimit = MathHelpers.Clamp(1f / driveOverload, ThrottleLimitFloor, 1f);
            }
            else
            {
                // While they grip, the limit grows back by 10 % per step.
                throttleLimit = MathF.Min(1f, throttleLimit + ThrottleLimitRecovery);
            }
        }

        private float BrakeFor(Vehicle vehicle, float speed, float dt)
        {
            brakeHeld += dt;
            if (speed < FullBrakeSpeed) return 1f;
            if (vehicle.Assists.brakes <= 0f) return 1f;

            float share = MathHelpers.Clamp01(brakeHeld / BrakeBuildUpTime);
            if (share < 1f) return BrakeAtTotalPeak(vehicle) * share;
            return 1f; // then the anti-lock brakes hold each wheel at its own peak
        }

        /// <summary>The largest share of its peak torque that a driven wheel carried in the previous step.</summary>
        public static float DriveOverload(Vehicle vehicle)
        {
            float result = 0f;
            for (int i = 0; i < vehicle.Axles.Count; i++)
            {
                Axle axle = vehicle.Axles[i];
                if (axle.Settings.driveShare <= 0f) continue;
                result = MathF.Max(result, WheelOverload(axle.Left));
                result = MathF.Max(result, WheelOverload(axle.Right));
            }
            return result;
        }

        private static float WheelOverload(Wheel wheel)
        {
            float peak = wheel.PeakTorque();
            return peak > 1e-6f ? MathF.Abs(wheel.DriveTorque) / peak : 0f;
        }

        /// <summary>The pedal at which the braked wheels together get their peak torque.</summary>
        public static float BrakeAtTotalPeak(Vehicle vehicle)
        {
            float peak = 0f;
            float capacity = 0f;
            for (int i = 0; i < vehicle.Axles.Count; i++)
            {
                Axle axle = vehicle.Axles[i];
                float brakeShare = axle.Settings.brakeShare;
                if (brakeShare <= 0f) continue;

                capacity += 2f * vehicle.Settings.maxBrakeTorque * brakeShare;
                if (axle.Left.IsGrounded) peak += axle.Left.PeakTorque();
                if (axle.Right.IsGrounded) peak += axle.Right.PeakTorque();
            }
            return capacity > 0f ? MathHelpers.Clamp01(peak / capacity) : 0f;
        }

        public void ResetToStandstill()
        {
            throttle = 0f;
            pullingThrottle = -1f;
            onTopHeld = 0f;
            throttleLimit = 1f;
            brakeHeld = 0f;
            previousSpeed = 0f;
            hasPreviousSpeed = false;
            isBurnout = false;
        }
    }
}
