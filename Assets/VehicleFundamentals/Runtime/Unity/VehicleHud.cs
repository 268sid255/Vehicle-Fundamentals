using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleFundamentals
{
    /// <summary>QHD Canvas telemetry with SDF type and analytic antialiased surfaces.</summary>
    public sealed class VehicleHud : MonoBehaviour
    {
        public RaycastVehicleController controller;
        public VehiclePlayerInput playerInput;
        public VehicleTemplateSelector templateSelector;
        public bool visible = true, showControls;
        public KeyCode toggleKey = KeyCode.H;
        public TMP_FontAsset telemetryFont;
        public Material surfaceMaterial;
        public static readonly Vector2 ReferenceResolution = new Vector2(2560, 1440);
        private const float Unit = 4f / 3f;
        private static readonly Color Ink = new Color(0.93f, 0.96f, 0.98f);
        private static readonly Color Muted = new Color(0.54f, 0.62f, 0.69f);
        private static readonly Color Panel = new Color(0.035f, 0.05f, 0.072f, 0.96f);
        private static readonly Color Track = new Color(0.13f, 0.18f, 0.23f);
        private static readonly Color Green = new Color(0.29f, 0.87f, 0.70f);
        private static readonly Color Blue = new Color(0.22f, 0.68f, 1f);
        private static readonly Color Amber = new Color(1f, 0.72f, 0.28f);
        private static readonly Color Red = new Color(1f, 0.34f, 0.30f);
        private readonly List<Card> cards = new List<Card>();
        private readonly VehicleHudSurface[] rpmBars = new VehicleHudSurface[8];
        private readonly RectTransform[] rpmTracks = new RectTransform[8];
        private readonly TMP_Text[] ticks = new TMP_Text[9];
        private readonly VehicleHudSurface[] pedals = new VehicleHudSurface[4];
        private RectTransform root;
        private TMP_Text vehicleName, driveName, rpmText, gearText, transmissionText, speedText, hints;
        private float lastMaxRpm = -1, rpmInterval;
        private int rpmSegments;
        private Vehicle shownVehicle;

        private sealed class Card
        {
            public RectTransform root;
            public TMP_Text status, load, travel, slip, grip, angle, rpm;
            public VehicleHudSurface loadBar, travelBar, gripBar;
        }

        private void Awake() { if (controller == null) controller = GetComponentInParent<RaycastVehicleController>(); }
        private void OnEnable() { if (playerInput != null) playerInput.HudPressed += Toggle; }
        private void OnDisable()
        {
            if (playerInput != null) playerInput.HudPressed -= Toggle;
            if (root != null) root.gameObject.SetActive(false);
        }
        private void Toggle() => visible = !visible;
        private void LateUpdate()
        {
#if !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            if (playerInput == null && Input.GetKeyDown(toggleKey)) Toggle();
#endif
            if (root == null) BuildCanvas();
            Vehicle vehicle = controller != null ? controller.Vehicle : null;
            root.gameObject.SetActive(visible && vehicle != null);
            if (!visible || vehicle == null) return;
            if (cards.Count != vehicle.Wheels.Count) BuildCards(vehicle.Wheels.Count);
            for (int i = 0; i < cards.Count; i++) UpdateCard(cards[i], vehicle.Wheels[i]);
            UpdateDashboard(vehicle);
        }

        public void BuildCanvas()
        {
            Transform old = transform.Find("Telemetry Canvas");
            if (old != null) Remove(old.gameObject);
            cards.Clear(); lastMaxRpm = -1;
            root = Rect(transform, "Telemetry Canvas", 0, 0, 1920, 1080);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100; canvas.pixelPerfect = true;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution; scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;

            RectTransform panel = Box(root, "Dashboard", 0, 0, 362, 174, Panel, 12);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
            panel.anchoredPosition = new Vector2(-58, 20) * Unit;
            vehicleName = Label(panel, "Vehicle", 18, 10, 235, 22, 17);
            rpmText = Label(panel, "RPM", 245, 11, 99, 21, 16, TextAlignmentOptions.MidlineRight);
            driveName = Label(panel, "Drive layout", 18, 32, 230, 16, 10, color: Muted);
            for (int i = 0; i < 8; i++)
            {
                rpmTracks[i] = Box(panel, "RPM track " + i, 18, 70, 38, 8, Track, 2);
                rpmBars[i] = Fill(panel, "RPM segment " + i, 18, 70, 38, 8, Green, false, 2);
            }
            for (int i = 0; i <= 8; i++) ticks[i] = Label(panel, "Tick " + i, 6, 48, 25, 18, 12, TextAlignmentOptions.Midline);
            SetRpmScale(8000, 0);
            Box(panel, "Rule", 18, 89, 326, 1, new Color(0.26f, 0.34f, 0.41f), 0);
            gearText = Label(panel, "Gear", 24, 94, 54, 67, 64, TextAlignmentOptions.Midline);
            transmissionText = Label(panel, "Transmission", 79, 127, 30, 24, 12, color: Muted);
            Box(panel, "Gear divider", 117, 105, 1, 43, Track, 0);
            speedText = Label(panel, "Speed", 132, 95, 141, 67, 64, TextAlignmentOptions.MidlineRight);
            Label(panel, "Units", 284, 126, 61, 26, 15, color: Muted).text = "KM/H";
            string[] pedalNames = { "C", "HB", "T", "B" };
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -33 + i * 16 : 377 + (i - 2) * 16;
                Box(panel, "Pedal track " + i, x, 66, 7, 79, Track, 3);
                pedals[i] = Fill(panel, "Pedal " + i, x, 66, 7, 79, i == 1 || i == 3 ? Amber : Blue, true, 3);
                Label(panel, "Pedal label " + i, x - 6, 150, 20, 17, 10, TextAlignmentOptions.Midline, Muted).text = pedalNames[i];
            }
            hints = Label(root, "Controls", 20, 0, 1000, 25, 13, color: Muted);
            hints.rectTransform.anchorMin = hints.rectTransform.anchorMax = Vector2.zero;
            hints.rectTransform.anchoredPosition = new Vector2(20, 278) * Unit;
            BuildCards(controller != null && controller.Definition != null ? controller.Definition.settings.axles.Count * 2 : 4);
            UpdateDashboard(controller != null ? controller.Vehicle : null);
        }

        private void BuildCards(int count)
        {
            foreach (Card card in cards) Remove(card.root.gameObject);
            cards.Clear();
            for (int i = 0; i < count; i++)
            {
                var card = new Card { root = Box(root, "Wheel card " + i, 0, 0, 104, 236, Panel, 10) };
                card.root.anchorMin = card.root.anchorMax = card.root.pivot = Vector2.zero;
                card.root.anchoredPosition = new Vector2(20 + i * 112, 20) * Unit;
                string axle = i / 2 == 0 ? "F" : count == 4 ? "R" : "R" + i / 2;
                Label(card.root, "Wheel", 12, 9, 37, 26, 21).text = axle + (i % 2 == 0 ? "L" : "R");
                card.status = Label(card.root, "Status", 45, 13, 48, 17, 9, TextAlignmentOptions.MidlineRight, Muted);
                card.status.text = "NO DATA";
                Box(card.root, "Load track", 21, 44, 14, 52, Track, 7);
                Box(card.root, "Travel track", 69, 44, 14, 52, Track, 7);
                card.loadBar = Fill(card.root, "Load fill", 21, 44, 14, 52, Blue, true, 7);
                card.travelBar = Fill(card.root, "Travel fill", 69, 44, 14, 52, Amber, true, 7);
                card.load = Label(card.root, "Load", 4, 100, 48, 24, 16, TextAlignmentOptions.Midline);
                card.travel = Label(card.root, "Travel", 53, 100, 47, 24, 16, TextAlignmentOptions.Midline);
                Label(card.root, "Load unit", 4, 123, 48, 16, 10, TextAlignmentOptions.Midline, Muted).text = "LOAD / kN";
                Label(card.root, "Travel unit", 53, 123, 47, 16, 10, TextAlignmentOptions.Midline, Muted).text = "TRAVEL";
                Box(card.root, "Separator", 12, 146, 80, 1, Track, 0);
                card.slip = Label(card.root, "Slip", 4, 153, 48, 24, 16, TextAlignmentOptions.Midline);
                card.grip = Label(card.root, "Grip", 53, 153, 47, 24, 16, TextAlignmentOptions.Midline);
                Label(card.root, "Slip label", 4, 177, 48, 15, 10, TextAlignmentOptions.Midline, Muted).text = "SLIP";
                Label(card.root, "Grip label", 53, 177, 47, 15, 10, TextAlignmentOptions.Midline, Muted).text = "GRIP USE";
                card.gripBar = Fill(card.root, "Grip fill", 12, 198, 80, 2, Green, false, 1);
                card.angle = Label(card.root, "Angle", 12, 204, 80, 16, 11, color: Muted);
                card.rpm = Label(card.root, "Wheel RPM", 12, 219, 80, 14, 10, color: Muted);
                card.load.text = card.travel.text = card.slip.text = card.grip.text = "—";
                card.angle.text = "ANGLE —"; card.rpm.text = "— RPM";
                cards.Add(card);
            }
        }

        private void UpdateCard(Card card, Wheel wheel)
        {
            bool grounded = wheel.IsGrounded;
            card.status.text = !grounded ? "AIR" : wheel.IsLocked ? "LOCK" : wheel.IsSpinning ? "SPIN" : wheel.IsAntiLockActive ? "ABS" : "CONTACT";
            card.status.color = !grounded ? Muted : wheel.IsLocked || wheel.IsSpinning ? Amber : Green;
            float travel = wheel.Compression / Mathf.Max(0.01f, wheel.Settings.suspensionTravel);
            float fx = wheel.LongitudinalGrip / Mathf.Max(0.01f, wheel.LongitudinalPeakGrip);
            float fy = wheel.LateralGrip / Mathf.Max(0.01f, wheel.LateralPeakGrip);
            float grip = Mathf.Sqrt(fx * fx + fy * fy);
            card.loadBar.fillAmount = grounded ? wheel.Load / Mathf.Max(1, 2.4f * wheel.Settings.staticWheelLoad) : 0;
            card.travelBar.fillAmount = grounded ? travel : 0;
            card.gripBar.fillAmount = grounded ? grip : 0;
            card.gripBar.color = grip > 0.95f ? Amber : Green;
            if (grounded)
            {
                card.load.SetText("{0:1}", wheel.Load / 1000);
                card.travel.SetText("{0:0}%", travel * 100);
                card.slip.SetText("{0:0}%", Mathf.Abs(wheel.SlipRatio) * 100);
                card.grip.SetText("{0:0}%", grip * 100);
                // Direction is undefined at rest; suppress numerical contact jitter in telemetry only.
                float angle = wheel.ForwardSpeed * wheel.ForwardSpeed + wheel.SideSpeed * wheel.SideSpeed < 0.01f
                    ? 0 : wheel.SlipAngleDeg;
                card.angle.SetText("ANGLE {0:1}°", angle);
            }
            else
            {
                card.load.text = card.travel.text = card.slip.text = card.grip.text = "—";
                card.angle.text = "ANGLE —";
            }
            card.rpm.SetText("{0:0} RPM", Mathf.Abs(wheel.ShownAngularVelocity * 9.5492966f));
        }

        private void UpdateDashboard(Vehicle vehicle)
        {
            float rpm = 0, maxRpm = 8000;
            gearText.text = vehicle != null && vehicle.SpeedForward < -0.2f ? "R" : "1";
            transmissionText.text = "EV";
            if (vehicle != null && vehicle.Drive is CombustionDrive combustion)
            {
                rpm = combustion.rpm; maxRpm = combustion.engine.maxRpm;
                if (combustion.gear < 0) gearText.text = "R"; else gearText.SetText("{0:0}", combustion.gear);
                transmissionText.text = "AT";
            }
            else if (vehicle != null && vehicle.Drive is ElectricDrive electric) { rpm = electric.rpm; maxRpm = electric.motor.maxRpm; }
            if (shownVehicle != vehicle || string.IsNullOrEmpty(vehicleName.text))
            {
                shownVehicle = vehicle;
                vehicleName.text = controller != null && controller.Definition != null ? controller.Definition.displayName : controller != null ? controller.Preset.ToString() : "VEHICLE";
            }
            driveName.text = vehicle == null ? "VEHICLE TELEMETRY" : vehicle.HasDrivenFrontAxle && vehicle.HasDrivenRearAxle ? "ALL WHEEL DRIVE" : vehicle.HasDrivenRearAxle ? "REAR WHEEL DRIVE" : "FRONT WHEEL DRIVE";
            rpmText.SetText("{0:0} RPM", rpm);
            speedText.SetText("{0:0}", vehicle != null ? Mathf.Abs(vehicle.SpeedForward) * 3.6f : 0);
            SetRpmScale(maxRpm, rpm);
            pedals[0].fillAmount = vehicle != null && vehicle.Drive is CombustionDrive drive ? 1 - drive.Clutch : 0;
            pedals[1].fillAmount = vehicle != null ? vehicle.Input.handbrake : 0;
            pedals[2].fillAmount = vehicle != null ? Mathf.Abs(vehicle.Input.throttle) : 0;
            pedals[3].fillAmount = vehicle != null ? vehicle.Input.brake : 0;
            hints.gameObject.SetActive(showControls);
            if (showControls) hints.text = "W/S  PEDALS     A/D  STEER     SPACE  HANDBRAKE     C  CAMERA     R  RESET     H  HUD     1–9  PRESET";
        }

        private void SetRpmScale(float maxRpm, float rpm)
        {
            if (maxRpm != lastMaxRpm)
            {
                lastMaxRpm = maxRpm;
                rpmInterval = Mathf.Max(1000, Mathf.Ceil(maxRpm / 8000) * 1000);
                rpmSegments = Mathf.Clamp(Mathf.CeilToInt(maxRpm / rpmInterval), 1, 8);
                float width = 326f / rpmSegments;
                for (int i = 0; i < 8; i++)
                {
                    rpmTracks[i].gameObject.SetActive(i < rpmSegments); rpmBars[i].gameObject.SetActive(i < rpmSegments);
                    rpmTracks[i].anchoredPosition = rpmBars[i].rectTransform.anchoredPosition = new Vector2(18 + i * width, -70) * Unit;
                    rpmTracks[i].sizeDelta = rpmBars[i].rectTransform.sizeDelta = new Vector2(width - 3, 8) * Unit;
                }
                for (int i = 0; i <= 8; i++)
                {
                    ticks[i].gameObject.SetActive(i <= rpmSegments);
                    ticks[i].text = (i * rpmInterval / 1000).ToString("0", CultureInfo.InvariantCulture);
                    ticks[i].rectTransform.anchoredPosition = new Vector2(18 + i * width - 12, -48) * Unit;
                }
            }
            for (int i = 0; i < rpmSegments; i++)
            {
                rpmBars[i].fillAmount = rpm / rpmInterval - i;
                rpmBars[i].color = (i + 1) * rpmInterval > maxRpm * 0.9f ? Red : Green;
            }
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y) * Unit; rect.sizeDelta = new Vector2(width, height) * Unit; return rect;
        }
        private RectTransform Box(Transform parent, string name, float x, float y, float width, float height, Color color, float radius)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var surface = rect.gameObject.AddComponent<VehicleHudSurface>();
            surface.color = color; surface.radius = radius * Unit; surface.material = surfaceMaterial; surface.raycastTarget = false;
            return rect;
        }
        private VehicleHudSurface Fill(Transform parent, string name, float x, float y, float width, float height, Color color, bool vertical, float radius)
        {
            var surface = Box(parent, name, x, y, width, height, color, radius).GetComponent<VehicleHudSurface>();
            surface.vertical = vertical; surface.fillAmount = 0; return surface;
        }
        private TMP_Text Label(Transform parent, string name, float x, float y, float width, float height, int size,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, Color? color = null)
        {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = telemetryFont; text.fontSize = size * Unit; text.color = color ?? Ink; text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false; text.richText = false; text.extraPadding = true;
            return text;
        }
        private static void Remove(GameObject target)
        {
            target.SetActive(false); if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
