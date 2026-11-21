using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VehicleFundamentals.EditorTools
{
    /// <summary>
    /// Vehicle asset inspector with a readable summary, validation and live refresh.
    /// </summary>
    [CustomEditor(typeof(VehicleDefinition))]
    public sealed class VehicleDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("preset"));
            if ((VehiclePreset)serializedObject.FindProperty("preset").enumValueIndex == VehiclePreset.Drift)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("visualVariant"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Vehicle", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Drivetrain", EditorStyles.boldLabel);
            SerializedProperty driveKind = serializedObject.FindProperty("driveKind");
            EditorGUILayout.PropertyField(driveKind);

            if ((DriveKind)driveKind.enumValueIndex == DriveKind.Electric)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("motor"), true);
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("engine"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gearbox"), true);
            }

            serializedObject.ApplyModifiedProperties();

            var definition = (VehicleDefinition)target;
            EditorGUILayout.Space();
            DrawSummary(definition);
            DrawValidation(definition);
        }

        private static void DrawSummary(VehicleDefinition definition)
        {
            VehicleSettings settings = definition.settings;
            if (settings == null) return;

            string drive = DriveLayout(settings);
            string limit = settings.speedLimit > 0f ? $"{settings.speedLimit:0} km/h limit" : "no speed limit";
            EditorGUILayout.HelpBox(
                $"{settings.mass:0} kg   |   {settings.axles?.Count ?? 0} axles   |   {drive}   |   {limit}",
                MessageType.None);
        }

        private static string DriveLayout(VehicleSettings settings)
        {
            if (settings.axles == null) return "no axles";

            float front = 0f;
            float rear = 0f;
            for (int i = 0; i < settings.axles.Count; i++)
            {
                AxleSettings axle = settings.axles[i];
                if (axle == null || axle.driveShare <= 0f) continue;
                if (axle.position < settings.centerOfMass.z) rear += axle.driveShare;
                else front += axle.driveShare;
            }

            if (front > 0f && rear > 0f) return "AWD";
            if (rear > 0f) return "RWD";
            if (front > 0f) return "FWD";
            return "no drive";
        }

        private static void DrawValidation(VehicleDefinition definition)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            Validate(definition, errors, warnings);

            if (errors.Count > 0)
            {
                EditorGUILayout.HelpBox("Fix these before driving:\n• " + string.Join("\n• ", errors),
                    MessageType.Error);
            }

            if (warnings.Count > 0)
            {
                EditorGUILayout.HelpBox("Warnings:\n• " + string.Join("\n• ", warnings),
                    MessageType.Warning);
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("The vehicle data is valid.", MessageType.Info);
            }
        }

        public static void Validate(VehicleDefinition definition, List<string> errors, List<string> warnings)
        {
            if (definition == null || definition.settings == null)
            {
                errors.Add("Vehicle settings are missing.");
                return;
            }

            VehicleSettings settings = definition.settings;
            if (settings.mass <= 0f) errors.Add("Mass must be greater than zero.");
            if (settings.turningCircle <= 0f) errors.Add("Turning circle must be greater than zero.");
            if (settings.dragArea < 0f) errors.Add("Drag area cannot be negative.");
            if (settings.speedLimit < 0f) errors.Add("Speed limit cannot be negative.");
            if (settings.centerOfMass.y <= 0f)
                warnings.Add("Centre of mass is at or below the ground plane.");

            if (settings.axles == null || settings.axles.Count == 0)
            {
                errors.Add("Add at least one axle.");
            }
            else
            {
                bool hasDrive = false;
                for (int i = 0; i < settings.axles.Count; i++)
                {
                    AxleSettings axle = settings.axles[i];
                    if (axle == null)
                    {
                        errors.Add($"Axle {i} is missing.");
                        continue;
                    }

                    string label = string.IsNullOrEmpty(axle.label) ? $"Axle {i}" : axle.label;
                    if (axle.trackWidth <= 0f) errors.Add($"{label}: track width must be positive.");
                    if (axle.wheelRadius <= 0f) errors.Add($"{label}: wheel radius must be positive.");
                    if (axle.suspensionTravel <= 0f) errors.Add($"{label}: suspension travel must be positive.");
                    if (axle.springRate <= 0f) errors.Add($"{label}: spring rate must be positive.");
                    if (axle.damperRate < 0f) errors.Add($"{label}: damper rate cannot be negative.");
                    if (axle.driveShare > 0f) hasDrive = true;

                    if (axle.tire == null)
                    {
                        errors.Add($"{label}: tire settings are missing.");
                        continue;
                    }

                    ValidateCurve(axle.tire.longitudinal, label + " longitudinal tire curve", errors);
                    ValidateCurve(axle.tire.lateral, label + " lateral tire curve", errors);

                    if (axle.tire.longitudinal != null && axle.tire.longitudinal.PeakValue() <= 0f)
                        errors.Add($"{label}: longitudinal grip peak must be positive.");
                    if (axle.tire.lateral != null && axle.tire.lateral.PeakValue() <= 0f)
                        errors.Add($"{label}: lateral grip peak must be positive.");
                }

                if (!hasDrive) errors.Add("No axle has a drive share.");
            }

            if (definition.driveKind == DriveKind.Electric)
            {
                MotorSettings motor = definition.motor;
                if (motor == null) errors.Add("Electric motor settings are missing.");
                else
                {
                    if (motor.maxTorque <= 0f) errors.Add("Motor max torque must be positive.");
                    if (motor.maxPower <= 0f) errors.Add("Motor max power must be positive.");
                    if (motor.gearRatio <= 0f) errors.Add("Motor gear ratio must be positive.");
                    if (motor.maxRpm <= 0f) errors.Add("Motor max rpm must be positive.");
                }
            }
            else
            {
                EngineSettings engine = definition.engine;
                if (engine == null) errors.Add("Engine settings are missing.");
                else
                {
                    if (engine.maxTorque <= 0f) errors.Add("Engine max torque must be positive.");
                    if (engine.maxRpm <= engine.idleRpm) errors.Add("Engine max rpm must be above idle.");
                    if (engine.inertia <= 0f) errors.Add("Engine inertia must be positive.");
                    ValidateCurve(engine.torqueCurve, "Engine torque curve", errors);
                }

                GearboxSettings gearbox = definition.gearbox;
                if (gearbox == null) errors.Add("Gearbox settings are missing.");
                else
                {
                    if (gearbox.gearRatios == null || gearbox.gearRatios.Length == 0)
                        errors.Add("Gearbox needs at least one gear ratio.");
                    if (gearbox.reverseRatio <= 0f) errors.Add("Reverse ratio must be positive.");
                    if (gearbox.finalDriveRatio <= 0f) errors.Add("Final drive ratio must be positive.");
                }
            }
        }

        private static void ValidateCurve(Curve curve, string label, List<string> errors)
        {
            if (curve == null || curve.keys == null || curve.keys.Count < 2)
                errors.Add(label + " needs at least two keys.");
        }
    }
}
