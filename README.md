# Vehicle Fundamentals

Vehicle physics for Unity using raycast wheels, implemented in C# and based on [Vehicle Fundamentals](https://vehicle-fundamentals.prototypeeverything.com/) by Prototype Everything.

[Русская версия](README.ru.md)

## Requirements

- Unity **6000.6.8f1**.
- Git LFS for models, textures and prepared meshes.
- URP, Input System, Cinemachine and uGUI. Package versions are pinned in [Packages/manifest.json](Packages/manifest.json).

## Getting started

1. Clone the repository and fetch its LFS assets:

   ```sh
   git lfs install
   git lfs pull
   ```

2. Open the project in Unity Hub and let Unity import the assets.
3. Open `Assets/VehicleFundamentals/Scenes/PhysicsSandbox.unity` and press **Play**.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Accelerate | W | Right trigger |
| Brake; reverse when stopped | S | Left trigger |
| Steer | A / D | Left stick |
| Handbrake | Space | Bottom button |
| Burnout from a standstill | W + S | Both triggers |
| Reset vehicle | R | View / Select |
| Switch camera | C | North button |
| Toggle HUD | H | West button |
| Previous / next vehicle | [ / ] | LB / RB |
| Select vehicle directly | 1–9 | — |

## Features

- Raycast suspension with springs, dampers, anti-roll bars and wheel loads derived from the rigid body.
- Tire grip curves, combined slip, friction circle, wheel inertia and rolling resistance.
- Combustion and electric drivetrains, automatic shifting, reverse, open and limited-slip differentials.
- Ackermann steering, traction control, ABS, drift and counter-steering assists.
- Multi-axle vehicles, moving-ground support and aerodynamic drag.
- Sandbox scene with vehicle selection, chase and hood cameras, skid marks and tire smoke.
- Canvas / TextMeshPro telemetry HUD with a **2560 × 1440** reference resolution.

## Vehicle presets

| Preset | Drive | Configuration |
| --- | --- | --- |
| SportsCoupe | RWD | 1420 kg sports coupe |
| CityCar | FWD | 990 kg city car |
| Compact | FWD | 1330 kg compact sedan |
| AwdHatch | AWD | 1625 kg hatchback, 50/50 torque split |
| Supercar | RWD | 1510 kg, 770 N·m |
| Minibus | RWD | 4000 kg, 100 km/h limit |
| BoxTruck | RWD | 10 000 kg, 90 km/h limit |
| ElectricSportsCoupe | RWD | 150 kW, single reduction gear |
| Truck | 6×4 | 16 000 kg, three axles, fixed rear tandem |
| Pickup | AWD | 1650 kg pickup |
| Drift | RWD | 500 N·m, six model variants |

The project includes 16 vehicle definition assets with project-specific suspension settings. Suspension is tuned for each preset; the six drift models share one physics profile.

## Vehicle configuration

Duplicate an asset in `Assets/VehicleFundamentals/Definitions`, or create one through **Assets → Create → Vehicle Fundamentals → Vehicle Definition**. Assign it to `RaycastVehicleController → Definition`.

The definition contains settings for mass, center of mass, axle geometry, suspension, tires, brakes and the drivetrain. Assist strengths range from 0 to 1. Use **Rebuild Vehicle** in the controller's context menu to apply changes during Play Mode.

To use a vehicle in another scene, add `RaycastVehicleController` and `VehiclePlayerInput` to a GameObject with a `Rigidbody`. Add the visual components or reuse the sandbox vehicle setup. Custom input sources implement `IVehicleInputSource`.

## Code structure

All project code and assets are under `Assets/VehicleFundamentals/`.

| Directory | Purpose |
| --- | --- |
| Runtime/Core | Math, settings, curves and the `IPhysicsWorld` interface |
| Runtime/Vehicle | Vehicle, axle, wheel, steering and pedal simulation |
| Runtime/Drivetrain | Combustion and electric drives |
| Runtime/Data | `VehicleDefinition` assets |
| Runtime/Unity | Rigidbody adapter, input, visuals, cameras and HUD |
| Definitions | Vehicle presets |
| Editor | Vehicle definition inspector |
| Models | Source models and prepared prefabs |
| Scenes / Settings | Sandbox scene and URP configuration |

The simulation runs in `FixedUpdate` at 50 Hz. The physics core is independent of Unity; `UnityVehiclePhysics` provides the engine adapter. The simulation uses SI units. The local axes are +X right, +Y up and +Z forward.

For formulas and update order, see the [full reference](https://vehicle-fundamentals.prototypeeverything.com/llms-full.txt). Related chapters are named in the source comments.

## Asset storage

Models, textures and `Models/Prepared/* Geometry.asset` files use Git LFS. Scenes, prefabs, materials and `.meta` files remain in regular Git. Commit `.meta` files to preserve asset references.

Unity YAML files use the `unityyamlmerge` attribute in `.gitattributes`. Configure the Git merge driver to use your Unity installation's Smart Merge tool.

## Limitations

- Engine sound and tire deformation are not implemented.
- Raycast wheels do not simulate collisions between tires and wheel arches.

## License and credits

Project code: [MIT License](LICENSE), copyright © 2026 Valery Balakishiev.

- Physics reference: **Prototype Everything — [Vehicle Fundamentals](https://vehicle-fundamentals.prototypeeverything.com/)**.
- Main vehicle models: **[thcyrax](https://sketchfab.com/thcyrax)**, from their [Sketchfab collection](https://sketchfab.com/thcyrax/models).
- Minibus: **[NEWS TRUCK - Stylized Vehicle](https://assetstore.unity.com/packages/3d/vehicles/land/news-truck-stylized-vehicle-210480)**, Unity Asset Store, package ID 210480.
- HUD font: **Barlow Medium**, licensed under the SIL Open Font License. The license is included in `Assets/VehicleFundamentals/Fonts`.

Third-party models, textures and fonts retain their original licenses and are not covered by the project's MIT license.
