using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NWH.WheelController3D
{
    /// <summary>
    /// UI display for the SportbikeController showing speed, RPM, gear, and other info
    /// Speed is calculated from rear wheel angular velocity for accuracy
    /// </summary>
    public class SportbikeUI : MonoBehaviour
    {
        public GameObject G1D;
        public GameObject G2D;
        public GameObject G3D;
        public GameObject G4D;
        public GameObject G5D;
        public GameObject G6D;

        [Header("References")]
        [Tooltip("The bike controller to read data from")]
        public SportbikeController bikeController;

        [Tooltip("The rear wheel controller to read speed from")]
        public WheelController rearWheel;

        [Header("Speed Display")]
        [Tooltip("Text component for digital speed readout")]
        public TextMeshProUGUI speedText;
        [Tooltip("Use MPH instead of KPH")]
        public bool useMPH = false;

        [Header("RPM Gauge")]
        [Tooltip("Text component for RPM display")]
        public TextMeshProUGUI rpmText;
        [Tooltip("Image component for RPM gauge fill")]
        public Image rpmGaugeFill;
        [Tooltip("Color gradient for RPM gauge (idle to redline)")]
        public Gradient rpmGaugeGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.green, 0f),
                new GradientColorKey(Color.yellow, 0.7f),
                new GradientColorKey(Color.red, 1f)
            }
        };

        [Header("Gear Display")]
        [Tooltip("Text component for gear display")]
        public TextMeshProUGUI gearText;
        [Tooltip("Display 'N' for neutral instead of '0'")]
        public bool showNeutral = true;

        [Header("Additional Info")]
        [Tooltip("Text for horsepower display")]
        public TextMeshProUGUI horsePowerText;
        [Tooltip("Text for torque display")]
        public TextMeshProUGUI torqueText;
        [Tooltip("Text for lean angle display")]
        public TextMeshProUGUI leanAngleText;

        [Header("Warning Lights")]
        [Tooltip("Image for shift light (lights up near redline)")]
        public Image shiftLight;
        [Tooltip("RPM percentage to activate shift light (0.9 = 90%)")]
        public float shiftLightThreshold = 0.9f;
        [Tooltip("Should the shift light blink?")]
        public bool blinkShiftLight = true;
        [Tooltip("Blink speed for shift light")]
        public float blinkSpeed = 10f;

        [Header("Optional Needle Gauges")]
        [Tooltip("Transform to rotate for speedometer needle")]
        public Transform speedNeedle;
        [Tooltip("Min angle for speed needle (typically -130)")]
        public float speedNeedleMinAngle = -130f;
        [Tooltip("Max angle for speed needle (typically 130)")]
        public float speedNeedleMaxAngle = 130f;
        [Tooltip("Max speed on speedometer (KPH or MPH depending on setting)")]
        public float maxSpeedOnGauge = 200f;

        [Tooltip("Transform to rotate for RPM needle")]
        public Transform rpmNeedle;
        [Tooltip("Min angle for RPM needle")]
        public float rpmNeedleMinAngle = -130f;
        [Tooltip("Max angle for RPM needle")]
        public float rpmNeedleMaxAngle = 130f;

        [Header("Smoothing")]
        [Tooltip("Smooth out needle movements")]
        public bool smoothNeedles = true;
        [Tooltip("Needle smoothing speed")]
        public float needleSmoothSpeed = 5f;

        // Private variables
        private float currentSpeedNeedleAngle;
        private float currentRpmNeedleAngle;
        private float blinkTimer;

        private void Start()
        {
            // Auto-find bike controller if not assigned
            if (bikeController == null)
            {
                bikeController = FindObjectOfType<SportbikeController>();
                if (bikeController == null)
                {
                    Debug.LogError("SportbikeUI: No SportbikeController found in scene!");
                }
            }

            // Auto-find rear wheel if not assigned
            if (rearWheel == null && bikeController != null)
            {
                WheelController[] wheels = bikeController.GetComponentsInChildren<WheelController>();
                // Typically rear wheel is the second wheel in the array, but you may need to adjust this
                if (wheels.Length > 1)
                {
                    rearWheel = wheels[1];
                }

                if (rearWheel == null)
                {
                    Debug.LogError("SportbikeUI: No rear wheel found! Please assign it manually.");
                }
            }

            // Initialize gradient if needed
            if (rpmGaugeGradient.colorKeys.Length == 0)
            {
                rpmGaugeGradient = new Gradient()
                {
                    colorKeys = new GradientColorKey[]
                    {
                        new GradientColorKey(Color.green, 0f),
                        new GradientColorKey(Color.yellow, 0.7f),
                        new GradientColorKey(Color.red, 1f)
                    }
                };
            }
        }

        private void Update()
        {
            if (bikeController == null || rearWheel == null) return;

            UpdateSpeedDisplay();
            UpdateRPMDisplay();
            UpdateGearDisplay();
            UpdateAdditionalInfo();
            UpdateWarningLights();
            UpdateNeedles();
        }

        /// <summary>
        /// Calculate speed from wheel angular velocity and circumference
        /// Speed (m/s) = Angular Velocity (rad/s) * Radius (m)
        /// </summary>
        public float GetWheelSpeed()
        {
            // Angular velocity is in rad/s, radius is in meters
            float angularVelocity = rearWheel.AngularVelocity;
            float radius = rearWheel.wheel.radius;

            // Speed in m/s = angular velocity * radius
            return angularVelocity * radius;
        }

        private void UpdateSpeedDisplay()
        {
            if (speedText != null)
            {
                float speedMS = GetWheelSpeed(); // m/s from wheel rotation
                float displaySpeed = useMPH ? speedMS * 2.237f : speedMS * 3.6f;
                string unit = useMPH ? "MPH" : "KPH";
                speedText.text = $"{Mathf.Abs(displaySpeed):F0}";
            }
        }

        private void UpdateRPMDisplay()
        {
            float rpm = bikeController.CurrentRPM;
            float maxRpm = bikeController.engine.redlineRpm;
            float normalizedRpm = Mathf.Clamp01(rpm / maxRpm);

            // Text display
            if (rpmText != null)
            {
                rpmText.text = $"{rpm:F0}";
            }

            // Fill gauge
            if (rpmGaugeFill != null)
            {
                rpmGaugeFill.fillAmount = normalizedRpm;
                rpmGaugeFill.color = rpmGaugeGradient.Evaluate(normalizedRpm);
            }
        }

        private void UpdateGearDisplay()
        {
            if (gearText != null)
            {
                int gear = bikeController.CurrentGear;

                // Optional: Change color based on gear
                if (gear == 0)
                {
                    G1D.SetActive(false);
                    G2D.SetActive(false);
                    G3D.SetActive(false);
                    G4D.SetActive(false);
                    G5D.SetActive(false);
                    G6D.SetActive(false);
                }
                else if (gear == 1)
                {
                    G1D.SetActive(true);
                    G2D.SetActive(false);
                    G3D.SetActive(false);
                    G4D.SetActive(false);
                    G5D.SetActive(false);
                    G6D.SetActive(false);
                }
                else if (gear == 2)
                {
                    G1D.SetActive(false);
                    G2D.SetActive(true);
                    G3D.SetActive(false);
                    G4D.SetActive(false);
                    G5D.SetActive(false);
                    G6D.SetActive(false);
                }
                else if (gear == 3)
                {
                    G1D.SetActive(false);
                    G2D.SetActive(false);
                    G3D.SetActive(true);
                    G4D.SetActive(false);
                    G5D.SetActive(false);
                    G6D.SetActive(false);
                }
                else if (gear == 4)
                {
                    G1D.SetActive(false);
                    G2D.SetActive(false);
                    G3D.SetActive(false);
                    G4D.SetActive(true);
                    G5D.SetActive(false);
                    G6D.SetActive(false);
                }
                else if (gear == 5)
                {
                    G1D.SetActive(false);
                    G2D.SetActive(false);
                    G3D.SetActive(false);
                    G4D.SetActive(false);
                    G5D.SetActive(true);
                    G6D.SetActive(false);
                }
                else if (gear == 6)
                {
                    G1D.SetActive(false);
                    G2D.SetActive(false);
                    G3D.SetActive(false);
                    G4D.SetActive(false);
                    G5D.SetActive(false);
                    G6D.SetActive(true);
                }
            }
        }

        private void UpdateAdditionalInfo()
        {
            if (horsePowerText != null)
            {
                horsePowerText.text = $"{bikeController.CurrentHP:F0} HP";
            }

            if (torqueText != null)
            {
                torqueText.text = $"{bikeController.CurrentTorque:F0} Nm";
            }

            if (leanAngleText != null)
            {
                float leanAngle = bikeController.CurrentLeanAngle;
                leanAngleText.text = $"Lean: {leanAngle:F1}°";

                // Color code based on lean angle
                float absLean = Mathf.Abs(leanAngle);
                if (absLean > 40f)
                {
                    leanAngleText.color = Color.red;
                }
                else if (absLean > 30f)
                {
                    leanAngleText.color = Color.yellow;
                }
                else
                {
                    leanAngleText.color = Color.white;
                }
            }
        }

        private void UpdateWarningLights()
        {
            if (shiftLight != null)
            {
                float rpm = bikeController.CurrentRPM;
                float maxRpm = bikeController.engine.redlineRpm;
                float normalizedRpm = rpm / maxRpm;

                if (normalizedRpm >= shiftLightThreshold)
                {
                    if (blinkShiftLight)
                    {
                        blinkTimer += Time.deltaTime * blinkSpeed;
                        float alpha = (Mathf.Sin(blinkTimer) + 1f) * 0.5f;
                        Color lightColor = shiftLight.color;
                        lightColor.a = alpha;
                        shiftLight.color = lightColor;
                    }
                    else
                    {
                        Color lightColor = shiftLight.color;
                        lightColor.a = 1f;
                        shiftLight.color = lightColor;
                    }
                }
                else
                {
                    Color lightColor = shiftLight.color;
                    lightColor.a = 0f;
                    shiftLight.color = lightColor;
                    blinkTimer = 0f;
                }
            }
        }

        private void UpdateNeedles()
        {
            // Speed needle
            if (speedNeedle != null)
            {
                float speedMS = GetWheelSpeed();
                float displaySpeed = useMPH ? speedMS * 2.237f : speedMS * 3.6f;
                displaySpeed = Mathf.Abs(displaySpeed); // Use absolute value for display

                float normalizedSpeed = Mathf.Clamp01(displaySpeed / maxSpeedOnGauge);
                float targetAngle = Mathf.Lerp(speedNeedleMinAngle, speedNeedleMaxAngle, normalizedSpeed);

                if (smoothNeedles)
                {
                    currentSpeedNeedleAngle = Mathf.Lerp(currentSpeedNeedleAngle, targetAngle, Time.deltaTime * needleSmoothSpeed);
                }
                else
                {
                    currentSpeedNeedleAngle = targetAngle;
                }

                speedNeedle.localRotation = Quaternion.Euler(0f, 0f, currentSpeedNeedleAngle);
            }

            // RPM needle
            if (rpmNeedle != null)
            {
                float rpm = bikeController.CurrentRPM;
                float maxRpm = bikeController.engine.redlineRpm;
                float normalizedRpm = Mathf.Clamp01(rpm / maxRpm);
                float targetAngle = Mathf.Lerp(rpmNeedleMinAngle, rpmNeedleMaxAngle, normalizedRpm);

                if (smoothNeedles)
                {
                    currentRpmNeedleAngle = Mathf.Lerp(currentRpmNeedleAngle, targetAngle, Time.deltaTime * needleSmoothSpeed);
                }
                else
                {
                    currentRpmNeedleAngle = targetAngle;
                }

                rpmNeedle.localRotation = Quaternion.Euler(0f, 0f, currentRpmNeedleAngle);
            }
        }
    }
}