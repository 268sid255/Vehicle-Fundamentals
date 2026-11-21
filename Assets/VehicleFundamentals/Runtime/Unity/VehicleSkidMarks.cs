using UnityEngine;
using UnityEngine.Rendering;

namespace VehicleFundamentals
{
    /// <summary>Builds continuous, pressure-sensitive tire trails in a bounded world-space mesh.</summary>
    [RequireComponent(typeof(RaycastVehicleController))]
    [DefaultExecutionOrder(100)]
    public sealed class VehicleSkidMarks : MonoBehaviour
    {
        public RaycastVehicleController controller;
        public Material material;

        [Header("Trigger")]
        public float slipAngle = 5f;
        public float slipRatio = 0.3f;

        [Header("Mark")]
        public float width = 0.26f;
        public float minDistance = 0.12f;
        public float groundOffset = 0.008f;
        public int maxQuads = 4000;
        public Color color = new Color(0.045f, 0.04f, 0.035f, 0.5f);

        private struct TrailState
        {
            public bool active;
            public Vector3 center, side;
            public float distance, intensity;
            public int lastQuad;
            public uint sequence;
            public object groundBody;
        }

        private struct Quad
        {
            public Vector3 leftStart, rightStart, leftEnd, rightEnd;
            public float startDistance, endDistance, startAlpha, endAlpha;
            public uint sequence;
        }

        private Quad[] quads;
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Color32[] colors;
        private int[] indices;
        private int quadCount, nextQuad;
        private uint sequence;
        private Vehicle lastVehicle;
        private TrailState[] trails;
        private Mesh mesh;
        private GameObject marksObject;
        private Material runtimeMaterial;
        private bool dirty;

        public int MarkCount => quadCount;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<RaycastVehicleController>();
            marksObject = new GameObject(name + " Skid Marks");
            marksObject.layer = gameObject.layer;
            mesh = new Mesh { name = "Skid Marks" };
            mesh.MarkDynamic();
            marksObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = marksObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterial = RuntimeMaterial();
        }

        private void FixedUpdate()
        {
            Vehicle vehicle = controller != null ? controller.Vehicle : null;
            if (vehicle == null || vehicle.Wheels.Count == 0) return;
            if (trails == null || trails.Length != vehicle.Wheels.Count)
                trails = new TrailState[vehicle.Wheels.Count];
            if (vehicle != lastVehicle)
            {
                for (int i = 0; i < trails.Length; i++) EndTrail(i);
                System.Array.Clear(trails, 0, trails.Length);
                lastVehicle = vehicle;
            }
            int capacity = Mathf.Clamp(maxQuads, 1, 65536);
            if (quads == null || quads.Length != capacity)
            {
                quads = new Quad[capacity];
                vertices = new Vector3[capacity * 4];
                uvs = new Vector2[capacity * 4];
                colors = new Color32[capacity * 4];
                indices = new int[capacity * 6];
                quadCount = nextQuad = 0;
                System.Array.Clear(trails, 0, trails.Length);
                mesh.indexFormat = capacity * 4 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                for (int i = 0; i < capacity; i++)
                {
                    int v = i * 4, t = i * 6;
                    indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                    indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
                }
                dirty = true;
            }
            for (int i = 0; i < vehicle.Wheels.Count; i++)
            {
                Wheel wheel = vehicle.Wheels[i];
                float intensity = Intensity(wheel);
                if (intensity <= 0f) { EndTrail(i); continue; }
                Vector3 normal = wheel.ContactNormal.ToUnity().normalized;
                Vector3 center = wheel.ContactPoint.ToUnity() + normal * groundOffset;
                TrailState trail = trails[i];
                Vector3 delta = center - trail.center;
                float distance = delta.magnitude;
                if (trail.active && (distance > 3f || !ReferenceEquals(trail.groundBody, wheel.GroundBody)))
                {
                    EndTrail(i);
                    trail.active = false;
                }
                if (!trail.active)
                {
                    Vector3 heading = Quaternion.AngleAxis(wheel.SteerAngle, transform.up) * transform.forward;
                    trails[i] = new TrailState
                    {
                        active = true, center = center,
                        side = Vector3.Cross(normal, heading).normalized,
                        groundBody = wheel.GroundBody, lastQuad = -1,
                    };
                    continue;
                }
                if (distance < Mathf.Max(0.02f, minDistance)) continue;
                Vector3 side = Vector3.Cross(normal, delta / distance).normalized;
                if (Vector3.Dot(side, trail.side) < 0f) side = -side;
                side = (side + trail.side).normalized;
                float halfWidth = width * 0.5f;
                quads[nextQuad] = new Quad
                {
                    leftStart = trail.center - trail.side * halfWidth,
                    rightStart = trail.center + trail.side * halfWidth,
                    leftEnd = center - side * halfWidth,
                    rightEnd = center + side * halfWidth,
                    startDistance = trail.distance, endDistance = trail.distance + distance,
                    startAlpha = trail.intensity, endAlpha = intensity,
                    sequence = ++sequence,
                };
                trail.lastQuad = nextQuad;
                trail.sequence = sequence;
                nextQuad = (nextQuad + 1) % capacity;
                quadCount = Mathf.Min(quadCount + 1, capacity);
                trail.center = center;
                trail.side = side;
                trail.distance += distance;
                trail.intensity = intensity;
                trails[i] = trail;
                dirty = true;
            }
        }

        private float Intensity(Wheel wheel)
        {
            if (!wheel.IsGrounded || wheel.Load <= 0f) return 0f;
            float speed = Mathf.Sqrt(wheel.ForwardSpeed * wheel.ForwardSpeed + wheel.SideSpeed * wheel.SideSpeed);
            if (speed < 0.5f) return 0f;
            float lateral = Mathf.InverseLerp(slipAngle, Mathf.Max(slipAngle * 3f, slipAngle + 1f), Mathf.Abs(wheel.SlipAngleDeg));
            float longitudinal = Mathf.InverseLerp(slipRatio, Mathf.Max(slipRatio * 2.5f, slipRatio + 0.1f), Mathf.Abs(wheel.SlipRatio));
            float pressure = Mathf.Clamp01(wheel.Load / Mathf.Max(1f, wheel.Settings.staticWheelLoad));
            float slip = Mathf.Max(lateral, longitudinal);
            if (wheel.IsLocked || wheel.IsSpinning) slip = Mathf.Max(slip, 0.65f);
            return slip * pressure * Mathf.InverseLerp(0.5f, 2f, speed);
        }

        private void EndTrail(int index)
        {
            TrailState trail = trails[index];
            if (!trail.active) return;
            if (quads != null && trail.lastQuad >= 0 && trail.lastQuad < quads.Length
                && quads[trail.lastQuad].sequence == trail.sequence)
            {
                quads[trail.lastQuad].endAlpha = 0f;
                dirty = true;
            }
            trails[index].active = false;
        }

        private void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            for (int i = 0; i < quadCount; i++)
            {
                Quad quad = quads[i];
                int v = i * 4;
                vertices[v] = quad.leftStart; vertices[v + 1] = quad.rightStart;
                vertices[v + 2] = quad.rightEnd; vertices[v + 3] = quad.leftEnd;
                uvs[v] = new Vector2(0f, quad.startDistance); uvs[v + 1] = new Vector2(1f, quad.startDistance);
                uvs[v + 2] = new Vector2(1f, quad.endDistance); uvs[v + 3] = new Vector2(0f, quad.endDistance);
                Color start = color, end = color;
                start.a *= quad.startAlpha; end.a *= quad.endAlpha;
                colors[v] = colors[v + 1] = start;
                colors[v + 2] = colors[v + 3] = end;
            }
            mesh.Clear();
            mesh.SetVertices(vertices, 0, quadCount * 4);
            mesh.SetUVs(0, uvs, 0, quadCount * 4);
            mesh.SetColors(colors, 0, quadCount * 4);
            mesh.SetTriangles(indices, 0, quadCount * 6, 0, false);
            mesh.RecalculateBounds();
        }

        private Material RuntimeMaterial()
        {
            Shader shader = Shader.Find("Vehicle Fundamentals/Skid Mark");
            runtimeMaterial = material != null ? new Material(material) : new Material(shader);
            runtimeMaterial.name = "Skid Marks (runtime)";
            runtimeMaterial.shader = shader;
            return runtimeMaterial;
        }

        private void OnDisable()
        {
            if (trails == null) return;
            for (int i = 0; i < trails.Length; i++) EndTrail(i);
        }

        private void OnDestroy()
        {
            if (marksObject != null) Destroy(marksObject);
            if (mesh != null) Destroy(mesh);
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
