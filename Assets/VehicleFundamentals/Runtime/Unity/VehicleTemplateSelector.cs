using UnityEngine;

namespace VehicleFundamentals
{
    /// <summary>
    /// Switches the car between the vehicle assets in the list, so one scene can show every
    /// template. [ and ] cycle, 1..9 select directly, and the gamepad shoulders cycle too.
    /// </summary>
    public sealed class VehicleTemplateSelector : MonoBehaviour
    {
        public RaycastVehicleController controller;
        public VehiclePlayerInput playerInput;
        public VehicleDefinition[] templates;

        [SerializeField] private int index;

        public int Index => index;
        public int Count => templates != null ? templates.Length : 0;

        public VehicleDefinition Current =>
            templates != null && index >= 0 && index < templates.Length ? templates[index] : null;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<RaycastVehicleController>();
            if (playerInput == null) playerInput = GetComponentInParent<VehiclePlayerInput>();

            if (controller != null)
            {
                startPosition = controller.transform.position;
                startRotation = controller.transform.rotation;
            }
        }

        private void OnEnable()
        {
            if (playerInput == null) return;
            playerInput.PreviousTemplatePressed += Previous;
            playerInput.NextTemplatePressed += Next;
            playerInput.TemplatePressed += SelectNumber;
        }

        private void OnDisable()
        {
            if (playerInput == null) return;
            playerInput.PreviousTemplatePressed -= Previous;
            playerInput.NextTemplatePressed -= Next;
            playerInput.TemplatePressed -= SelectNumber;
        }

        private void Start()
        {
            if (Count == 0 || controller == null) return;

            int wrapped = ((index % Count) + Count) % Count;
            Apply(wrapped, controller.Definition != templates[wrapped]);
        }

        public void Next() => Select(index + 1);

        public void Previous() => Select(index - 1);

        public void Select(int value) => Apply(value, true);

        private void SelectNumber(int number) => Select(number - 1);

        private void Apply(int value, bool rebuild)
        {
            if (Count == 0 || controller == null) return;

            index = ((value % Count) + Count) % Count;
            VehicleDefinition definition = templates[index];
            if (definition == null) return;

            controller.SetDefinition(definition, rebuild);
            if (!rebuild) return;

            controller.Teleport(startPosition, startRotation);
            controller.name = definition.displayName;
        }
    }
}
