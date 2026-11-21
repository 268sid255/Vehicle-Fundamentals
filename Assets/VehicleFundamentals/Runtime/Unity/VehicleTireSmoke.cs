using UnityEngine;
using UnityEngine.Rendering;

namespace VehicleFundamentals
{
    /// <summary>
    /// Emits tire smoke from the wheels that slide, spin or lock. One particle system per wheel,
    /// fed by hand so the smoke density follows the actual slip instead of a fixed rate.
    /// </summary>
    [RequireComponent(typeof(RaycastVehicleController))]
    public sealed class VehicleTireSmoke : MonoBehaviour
    {
        public RaycastVehicleController controller;

        [Tooltip("Transparent particle material. A runtime fallback with a soft puff is built when empty.")]
        public Material material;

        [Header("Trigger")]
        [Tooltip("Lateral slip angle in degrees that starts the smoke.")]
        public float slipAngle = 8f;
        [Tooltip("Longitudinal slip ratio magnitude that starts the smoke.")]
        public float slipRatio = 0.35f;
        [Tooltip("Below this travel speed only wheelspin makes smoke, so a parked car stays quiet.")]
        public float minSpeed = 1f;

        [Header("Smoke")]
        public float rate = 90f;
        public float lifetime = 1.8f;
        public float startSize = 0.24f;
        public float endSize = 1.4f;
        public Color color = new Color(0.72f, 0.74f, 0.76f, 0.18f);
        public Vector3 drift = new Vector3(0f, 0.35f, 0f);

        private ParticleSystem[] systems;
        private float[] accumulators;
        private float[] intensities;
        private Vector3[] previousPositions;
        private bool[] hasPosition;
        private Vehicle lastVehicle;
        private Material runtimeMaterial;
        private static Texture2D puffTexture;

        /// <summary>Live particle count across all wheels.</summary>
        public int ParticleCount
        {
            get
            {
                if (systems == null) return 0;
                int total = 0;
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] != null) total += systems[i].particleCount;
                }
                return total;
            }
        }

        private void Start()
        {
            if (controller == null) controller = GetComponent<RaycastVehicleController>();
            EnsureSystems();
        }

        private void LateUpdate()
        {
            Vehicle vehicle = controller != null ? controller.Vehicle : null;
            if (vehicle == null || vehicle.Wheels.Count == 0) return;
            if (!EnsureSystems()) return;
            if (vehicle != lastVehicle)
            {
                ClearSmoke();
                lastVehicle = vehicle;
            }

            Vector3 travel = Vector3.zero;
            Rigidbody body = controller.Body;
            if (body != null)
            {
#if UNITY_6000_0_OR_NEWER
                travel = body.linearVelocity;
#else
                travel = body.velocity;
#endif
            }
            for (int i = 0; i < vehicle.Wheels.Count; i++)
            {
                Wheel wheel = vehicle.Wheels[i];
                float target = Intensity(vehicle, wheel);
                intensities[i] = Mathf.MoveTowards(intensities[i], target, Time.deltaTime * (target > intensities[i] ? 3f : 5f));
                float intensity = intensities[i];
                if (!wheel.IsGrounded || intensity <= 0.001f)
                {
                    accumulators[i] = 0f;
                    hasPosition[i] = false;
                    continue;
                }

                Vector3 normal = wheel.ContactNormal.ToUnity();
                if (normal.sqrMagnitude < 0.1f) normal = Vector3.up;
                Vector3 position = wheel.ContactPoint.ToUnity() + normal * 0.1f;
                if (hasPosition[i] && (position - previousPositions[i]).sqrMagnitude > 16f)
                {
                    systems[i].Clear();
                    hasPosition[i] = false;
                    accumulators[i] = 0f;
                }
                Vector3 previous = hasPosition[i] ? previousPositions[i] : position;
                previousPositions[i] = position;
                hasPosition[i] = true;
                systems[i].transform.position = position;

                accumulators[i] += rate * intensity * Time.deltaTime;
                int count = Mathf.Min(32, Mathf.FloorToInt(accumulators[i]));
                if (count <= 0) continue;
                accumulators[i] -= count;

                for (int k = 0; k < count; k++)
                {
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = Vector3.Lerp(previous, position, (k + Random.value) / count),
                        velocity = wheel.GroundVelocity.ToUnity() + drift + Random.insideUnitSphere * 0.18f
                            + (travel - wheel.GroundVelocity.ToUnity()) * 0.08f,
                        startSize = startSize * Random.Range(0.8f, 1.25f),
                        startLifetime = lifetime * Random.Range(0.7f, 1.3f),
                        startColor = new Color(color.r, color.g, color.b, color.a * Mathf.Lerp(0.4f, 1f, intensity)),
                        rotation = Random.Range(0f, 360f),
                    };
                    systems[i].Emit(emit, 1);
                }
            }
        }

        private float Intensity(Vehicle vehicle, Wheel wheel)
        {
            if (!wheel.IsGrounded || wheel.Load <= 0f) return 0f;

            float speed = Mathf.Abs(vehicle.TravelSpeed);
            float lateral = Mathf.InverseLerp(slipAngle, Mathf.Max(slipAngle * 3f, slipAngle + 1f), Mathf.Abs(wheel.SlipAngleDeg));
            float longitudinal = Mathf.InverseLerp(slipRatio, Mathf.Max(slipRatio * 3f, slipRatio + 0.1f), Mathf.Abs(wheel.SlipRatio));
            float intensity = Mathf.Max(lateral, longitudinal);
            float slipSpeed = Mathf.Sqrt(wheel.SideSpeed * wheel.SideSpeed
                + Mathf.Pow(wheel.ShownAngularVelocity * wheel.Radius - wheel.ForwardSpeed, 2f));
            if (wheel.IsSpinning || wheel.IsLocked) intensity = Mathf.Max(intensity, 0.65f);
            if (speed < minSpeed && !wheel.IsSpinning) return 0f;
            float pressure = Mathf.Clamp01(wheel.Load / Mathf.Max(1f, wheel.Settings.staticWheelLoad));
            return intensity * Mathf.InverseLerp(0.5f, 6f, slipSpeed) * pressure;
        }

        private bool EnsureSystems()
        {
            Vehicle vehicle = controller != null ? controller.Vehicle : null;
            int count = vehicle != null ? vehicle.Wheels.Count : 0;
            if (count == 0) return false;
            if (systems != null && systems.Length == count) return true;

            if (systems != null)
            {
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] != null) Destroy(systems[i].gameObject);
                }
            }

            systems = new ParticleSystem[count];
            accumulators = new float[count];
            intensities = new float[count];
            previousPositions = new Vector3[count];
            hasPosition = new bool[count];
            for (int i = 0; i < count; i++) systems[i] = CreateSystem(i);
            return true;
        }

        private ParticleSystem CreateSystem(int index)
        {
            var go = new GameObject("Tire Smoke " + index);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = startSize;
            main.startColor = color;
            main.gravityModifier = -0.01f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, endSize / Mathf.Max(0.01f, startSize)));

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = RuntimeMaterial();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 0.2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }

        private Material RuntimeMaterial()
        {
            if (runtimeMaterial != null) return runtimeMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            runtimeMaterial = material != null ? new Material(material) : new Material(shader);
            runtimeMaterial.name = "Tire Smoke (runtime)";
            runtimeMaterial.SetColor("_BaseColor", Color.white);
            runtimeMaterial.SetTexture("_BaseMap", RuntimePuff());
            runtimeMaterial.SetFloat("_Surface", 1f);
            runtimeMaterial.SetFloat("_Blend", 0f);
            runtimeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            runtimeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            runtimeMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            runtimeMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            runtimeMaterial.SetFloat("_ZWrite", 0f);
            runtimeMaterial.SetFloat("_Cull", (float)CullMode.Off);
            runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            runtimeMaterial.EnableKeyword("_SOFTPARTICLES_ON");
            runtimeMaterial.EnableKeyword("_FADING_ON");
            runtimeMaterial.SetFloat("_SoftParticlesEnabled", 1f);
            runtimeMaterial.SetFloat("_CameraFadingEnabled", 1f);
            runtimeMaterial.SetVector("_SoftParticleFadeParams", new Vector4(0.05f, 1.333333f, 0f, 0f));
            runtimeMaterial.SetVector("_CameraFadeParams", new Vector4(0.3f, 1.428571f, 0f, 0f));
            runtimeMaterial.renderQueue = (int)RenderQueue.Transparent;
            return runtimeMaterial;
        }

        private static Texture2D RuntimePuff()
        {
            if (puffTexture != null) return puffTexture;

            const int size = 128;
            puffTexture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Smoke Puff (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float cloud = 0.6f * Mathf.PerlinNoise(dx * 2.5f + 7.3f, dy * 2.5f + 3.7f)
                        + 0.4f * Mathf.PerlinNoise(dx * 6f + 1.2f, dy * 6f + 9.4f);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radius) * 1.4f));
                    alpha *= alpha * Mathf.Lerp(0.35f, 1f, cloud);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            puffTexture.SetPixels32(pixels);
            puffTexture.Apply(true, true);
            return puffTexture;
        }

        private void ClearSmoke()
        {
            if (systems == null) return;
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null) systems[i].Clear();
                accumulators[i] = intensities[i] = 0f;
                hasPosition[i] = false;
            }
        }

        private void OnDisable() => ClearSmoke();

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
