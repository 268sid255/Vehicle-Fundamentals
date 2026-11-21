using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>Stores the wheel rig and reference dimensions of an imported vehicle prefab.</summary>
    public sealed class VehicleVisualPrefab : MonoBehaviour
    {
        public Transform body;
        public Transform[] wheels;
        public Transform[] axleVisuals;
        public float[] wheelRadii;
        public float frontAxle, rearAxle, trackWidth, frontRestHeight;
        public Vector3 colliderCenter, colliderSize;
    }
}