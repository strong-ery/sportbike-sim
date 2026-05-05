using NWH.Common.Vehicles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NWH.WheelController3D
{
    /// <summary>
    /// Enhanced realistic sportbike controller with proper engine inertia and clutch simulation
    /// NOW WITH: Automatic RPM-scaled downshift pops
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SportbikeController : Vehicle
    {
        [System.Serializable]
        public class BikeWheel
        {
            public WheelUAPI wheelUAPI;
            public bool power;
            public bool steer;
            public bool handbrake;
        }

        [System.Serializable]
        public class EngineConfig
        {
            [Header("Engine Specs (ZX6R Based)")]
            public float peakHorsePower = 130f;
            public float peakTorque = 70f;        // Nm

            [Header("Engine Inertia")]
            [Tooltip("Engine rotational inertia - affects how quickly RPM changes (kg·m²)")]
            [Range(0.01f, 0.1f)]
            public float engineInertia = 0.035f;
            [Tooltip("Constant friction torque (Nm) - parasitic losses")]
            [Range(0.5f, 5f)]
            public float internalFriction = 1.5f;

            [Header("Idle RPM Range")]
            [Tooltip("Minimum idle RPM - engine will fluctuate around this range")]
            public float idleRpmMin = 1450f;
            [Tooltip("Maximum idle RPM - creates natural oscillation")]
            public float idleRpmMax = 1550f;
            [Tooltip("How quickly idle RPM fluctuates - higher = faster 'bum bum' rhythm")]
            [Range(1f, 1000f)]
            public float idleFluctuationSpeed = 4.5f;
            [Tooltip("How sharp the idle pulses are - higher = more distinct 'bum' sounds")]
            [Range(0f, 1f)]
            public float idlePulseSharpness = 0.7f;

            public float redlineRpm = 16000f;
            public float limiterRpm = 16500f;

            [Header("Torque Curve")]
            public AnimationCurve torqueCurve = new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.2f, 0.6f),
                new Keyframe(0.4f, 0.85f),
                new Keyframe(0.6f, 1.0f),
                new Keyframe(0.8f, 0.98f),
                new Keyframe(1.0f, 0.92f)
            );

            [Header("Engine Characteristics")]
            [Tooltip("Base engine braking torque at idle RPM")]
            public float engineBrakingTorqueBase = 15f;
            [Tooltip("Additional engine braking per 1000 RPM")]
            public float engineBrakingPerRpm = 1.5f;
            [Tooltip("Maximum engine braking torque")]
            public float engineBrakingTorqueMax = 60f;

            [Header("Tuning")]
            [Tooltip("Global torque multiplier")]
            [Range(0f, 3f)]
            public float globalTorqueMultiplier = 1.0f;

            [Header("Aerodynamics")]
            public float dragCoefficient = 0.58f;
            public float frontalArea = 0.50f;

            [Header("Drivetrain Losses")]
            [Tooltip("Percentage of power lost through drivetrain (chain, bearings, etc)")]
            [Range(0f, 0.3f)]
            public float drivetrainLoss = 0.15f;

            [Header("Rev Limiter")]
            [Tooltip("How quickly engine returns to idle when off throttle in neutral")]
            public float revDecayRate = 3000f;
            [Tooltip("Duration of ignition cut at limiter (seconds)")]
            [Range(0.05f, 0.2f)]
            public float limiterCutDuration = 0.08f;
            [Tooltip("RPM drop during limiter cut")]
            [Range(100f, 800f)]
            public float limiterRpmDrop = 400f;

            [Header("Downshift Pops (NEW)")]
            [Tooltip("RPM threshold to trigger downshift pops")]
            public float popActivationRpm = 9000f;
            [Tooltip("Base duration of pops at activation RPM (seconds)")]
            [Range(0.1f, 2f)]
            public float popBaseDuration = 0.3f;
            [Tooltip("Max duration of pops at redline (seconds)")]
            [Range(0.5f, 4f)]
            public float popMaxDuration = 2.5f;
            [Tooltip("Base pop intensity at activation RPM")]
            [Range(0.5f, 3f)]
            public float popBaseIntensity = 2.0f;
            [Tooltip("Max pop intensity at redline")]
            [Range(2f, 5f)]
            public float popMaxIntensity = 4.0f;
            [Tooltip("Base pop frequency at activation RPM")]
            [Range(0f, 1f)]
            public float popBaseFrequency = 0.3f;
            [Tooltip("Max pop frequency at redline")]
            [Range(0f, 1f)]
            public float popMaxFrequency = 0.85f;
        }

        [System.Serializable]
        public class TransmissionConfig
        {
            [Header("Gear Ratios (ZX6R)")]
            public float[] gearRatios = new float[] { 2.615f, 1.937f, 1.600f, 1.400f, 1.261f, 1.173f };
            public float finalDrive = 2.933f;

            [Header("Per-Gear Torque Multipliers")]
            public float[] gearTorqueMultipliers = new float[] {
                2.0f, 2.5f, 3.0f, 3.8f, 4.5f, 5.5f
            };

            [Header("Per-Gear Multiplier Activation")]
            public float[] gearActivationRpms = new float[] {
                3000f, 4000f, 5000f, 5500f, 6000f, 6500f
            };

            [Header("Multiplier Ramp")]
            public float multiplierRampRange = 2000f;

            [Header("Clutch Settings")]
            [Tooltip("How quickly clutch engages (0-1 per second)")]
            [Range(1f, 20f)]
            public float clutchEngageSpeed = 15f;
            [Tooltip("How quickly clutch disengages")]
            [Range(1f, 20f)]
            public float clutchDisengageSpeed = 20f;
            [Tooltip("RPM difference where clutch starts slipping")]
            [Range(100f, 2000f)]
            public float clutchSlipThreshold = 800f;
            [Tooltip("Maximum torque clutch can transfer when slipping")]
            [Range(50f, 400f)]
            public float clutchMaxSlipTorque = 250f;

            [Header("Shift Settings")]
            public float shiftTime = 0.10f;
            public bool quickShifter = true;
            public float quickShiftCutTime = 0.06f;

            [Header("Auto Shift")]
            public bool autoShift = false;
            public float autoUpshiftRpm = 15500f;
            public float autoDownshiftRpm = 9000f;

            [Header("Neutral Gear")]
            public bool allowNeutral = true;
        }

        [System.Serializable]
        public class InputConfig
        {
            [Header("Input System Actions")]
            public InputActionReference throttleAction;
            public InputActionReference steeringAction;
            public InputActionReference brakeAction;
            public InputActionReference shiftUpAction;
            public InputActionReference shiftDownAction;
            public InputActionReference neutralAction;
            public InputActionReference handbrakeAction;
        }

        [System.Serializable]
        public class SteeringAssistConfig
        {
            [Header("Master Control")]
            public bool enableSteeringAssist = true;

            [Header("Counter-Steering")]
            public bool enableCounterSteering = true;
            public float counterSteerMinSpeed = 8f;
            public float counterSteerMaxSpeed = 25f;
            public float counterSteerStrength = 0.25f;

            [Header("Auto-Alignment")]
            public bool enableAutoAlignment = true;
            public float alignmentStrength = 1.2f;
            public float alignmentDeadZone = 3f;

            [Header("Turn-In Assistance")]
            public bool enableTurnInAssist = true;
            public float turnInMaxSpeed = 12f;
            public float turnInBoost = 0.3f;

            [Header("High-Speed Stability")]
            public bool enableHighSpeedStability = true;
            public float stabilityMinSpeed = 25f;
            public float stabilityReduction = 0.6f;

            [Header("Lean-Based Steering")]
            public bool enableLeanSteering = true;
            public float leanSteeringSensitivity = 0.15f;
        }

        [Header("Bike Configuration")]
        public InputConfig inputConfig = new InputConfig();
        public EngineConfig engine = new EngineConfig();
        public TransmissionConfig transmission = new TransmissionConfig();
        public SteeringAssistConfig steeringAssist = new SteeringAssistConfig();

        [Header("Braking")]
        public float frontBrakeTorque = 3500f;
        public float rearBrakeTorque = 1800f;

        [Header("Steering")]
        public float maxSteeringAngle = 35f;
        public float minSteeringAngle = 15f;
        public float steeringSpeedThreshold = 30f;
        public float steeringInputSmoothing = 0.1f;

        [Header("Wheels")]
        public List<BikeWheel> wheels;

        [Header("Runtime Info (Read Only)")]
        [SerializeField] private float currentRpm;
        [SerializeField] private float targetIdleRpm;
        [SerializeField] private int currentGear = 1;
        [SerializeField] private float currentTorque;
        [SerializeField] private float currentHorsePower;
        [SerializeField] private bool isShifting;
        [SerializeField] private float clutchPosition = 0f;
        [SerializeField] private float currentLeanAngle;
        [SerializeField] private float assistedSteerInput;
        [SerializeField] private float wheelTorque;
        [SerializeField] public bool isInNeutral;
        [SerializeField] private bool ignitionCut;
        [SerializeField] private float engineLoad;
        [SerializeField] private bool isClutchSlipping;

        public ProceduralEngine engineSound;

        private bool shiftUpRequested;
        private bool shiftDownRequested;
        private bool neutralRequested;

        private float throttleInput;
        private float brakeInput;
        private float rawSteerInput;
        private float shiftTimer;
        private int pendingGear = -1;

        private float smoothSteerInput;
        private float steerInputVelocity;
        private float smoothSpeed;
        private float smoothSpeedVelocity;

        private float idleRpmPhase;
        private float limiterCutTimer;
        private bool isLimiterActive;

        // Engine inertia simulation
        private float engineAngularVelocity;

        // Downshift pops tracking
        private float lastShiftRpm;
        private Coroutine popCoroutine;

        private WheelUAPI poweredWheel;
        private Rigidbody rb;

        private const float AIR_DENSITY = 1.225f;
        private const float RPM_TO_RAD_S = 0.10472f;

        public float CurrentRPM => currentRpm;
        public int CurrentGear => currentGear;
        public float CurrentTorque => currentTorque;
        public float CurrentHP => currentHorsePower;
        public float CurrentLeanAngle => currentLeanAngle;
        public float ClutchPosition => clutchPosition;
        public bool IsInNeutral => isInNeutral;
        public float EngineLoad => engineLoad;

        public override void Awake()
        {
            base.Awake();

            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

            currentGear = 1;
            targetIdleRpm = (engine.idleRpmMin + engine.idleRpmMax) * 0.5f;
            currentRpm = targetIdleRpm;
            engineAngularVelocity = currentRpm * RPM_TO_RAD_S;
            isInNeutral = false;

            idleRpmPhase = Random.Range(0f, Mathf.PI * 2f);

            foreach (var wheel in wheels)
            {
                if (wheel.power)
                {
                    poweredWheel = wheel.wheelUAPI;
                    break;
                }
            }

            EnableInputActions();
        }

        private void OnEnable()
        {
            EnableInputActions();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            DisableInputActions();

            foreach (var wheel in wheels)
            {
                WheelUAPI wc = wheel.wheelUAPI;
                wc.BrakeTorque = frontBrakeTorque;
                wc.MotorTorque = 0f;
                wc.SteerAngle = 0f;
            }

            StartCoroutine(SmoothRPMToIdle());
        }

        private IEnumerator SmoothRPMToIdle()
        {
            float duration = 1f; // Time to transition
            float elapsed = 0f;
            float startRPM = CurrentRPM;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float targetRPM = Mathf.Lerp(startRPM, 1500f, t);
                engineSound.rpm = targetRPM;
                currentRpm = targetRPM;
                yield return null;
            }

            engineSound.rpm = 1500f;
            currentRpm = 1500f;
        }

        private void EnableInputActions()
        {
            if (inputConfig.throttleAction != null) inputConfig.throttleAction.action.Enable();
            if (inputConfig.steeringAction != null) inputConfig.steeringAction.action.Enable();
            if (inputConfig.brakeAction != null) inputConfig.brakeAction.action.Enable();
            if (inputConfig.shiftUpAction != null) inputConfig.shiftUpAction.action.Enable();
            if (inputConfig.shiftDownAction != null) inputConfig.shiftDownAction.action.Enable();
            if (inputConfig.neutralAction != null) inputConfig.neutralAction.action.Enable();
            if (inputConfig.handbrakeAction != null) inputConfig.handbrakeAction.action.Enable();
        }

        private void DisableInputActions()
        {
            if (inputConfig.throttleAction != null) inputConfig.throttleAction.action.Disable();
            if (inputConfig.steeringAction != null) inputConfig.steeringAction.action.Disable();
            if (inputConfig.brakeAction != null) inputConfig.brakeAction.action.Disable();
            if (inputConfig.shiftUpAction != null) inputConfig.shiftUpAction.action.Disable();
            if (inputConfig.shiftDownAction != null) inputConfig.shiftDownAction.action.Disable();
            if (inputConfig.neutralAction != null) inputConfig.neutralAction.action.Disable();
            if (inputConfig.handbrakeAction != null) inputConfig.handbrakeAction.action.Disable();
        }

        private void Update()
        {
            if (inputConfig.shiftUpAction != null && inputConfig.shiftUpAction.action.WasPressedThisFrame())
            {
                shiftUpRequested = true;
            }

            if (inputConfig.shiftDownAction != null && inputConfig.shiftDownAction.action.WasPressedThisFrame())
            {
                shiftDownRequested = true;
            }

            if (transmission.allowNeutral && inputConfig.neutralAction != null && inputConfig.neutralAction.action.WasPressedThisFrame())
            {
                neutralRequested = true;
            }

            if (engineSound != null)
            {
                engineSound.rpm = CurrentRPM;
                engineSound.throttle = throttleInput;
                engineSound.ignitionCut = ignitionCut;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            HandleInput();
            UpdateIdleRpm();
            CalculateLeanAngle();
            ApplySteeringAssistance();
            HandleShiftingAndClutch();
            HandleRevLimiter();
            SimulateEngineWithInertia();
            ApplyToWheels();

            smoothSpeed = Mathf.SmoothDamp(smoothSpeed, Speed, ref smoothSpeedVelocity, 0.2f);
        }

        private void HandleInput()
        {
            throttleInput = inputConfig.throttleAction != null ?
                Mathf.Clamp01(inputConfig.throttleAction.action.ReadValue<float>()) : 0f;

            brakeInput = inputConfig.brakeAction != null ?
                Mathf.Clamp01(inputConfig.brakeAction.action.ReadValue<float>()) : 0f;

            rawSteerInput = inputConfig.steeringAction != null ?
                inputConfig.steeringAction.action.ReadValue<float>() : 0f;

            smoothSteerInput = Mathf.SmoothDamp(smoothSteerInput, rawSteerInput, ref steerInputVelocity, steeringInputSmoothing);

            if (shiftUpRequested)
            {
                ShiftUp();
                shiftUpRequested = false;
            }

            if (shiftDownRequested)
            {
                ShiftDown();
                shiftDownRequested = false;
            }

            if (neutralRequested)
            {
                ToggleNeutral();
                neutralRequested = false;
            }
        }

        private void UpdateIdleRpm()
        {
            idleRpmPhase += Time.fixedDeltaTime * engine.idleFluctuationSpeed;

            float sineWave = Mathf.Sin(idleRpmPhase);
            float sharpPulse = Mathf.Pow(Mathf.Max(0f, sineWave), 1f / Mathf.Max(0.1f, engine.idlePulseSharpness));

            targetIdleRpm = Mathf.Lerp(engine.idleRpmMin, engine.idleRpmMax, sharpPulse);
        }

        private void HandleRevLimiter()
        {
            if (currentRpm >= engine.limiterRpm && throttleInput > 0.1f)
            {
                if (!isLimiterActive)
                {
                    isLimiterActive = true;
                    limiterCutTimer = 0f;
                    ignitionCut = true;
                }

                limiterCutTimer += Time.fixedDeltaTime;

                if (limiterCutTimer < engine.limiterCutDuration)
                {
                    currentRpm = Mathf.Max(engine.redlineRpm, currentRpm - (engine.limiterRpmDrop * Time.fixedDeltaTime / engine.limiterCutDuration));
                    engineAngularVelocity = currentRpm * RPM_TO_RAD_S;
                }
                else
                {
                    isLimiterActive = false;
                    limiterCutTimer = 0f;
                    ignitionCut = false;
                }
            }
            else
            {
                isLimiterActive = false;
                limiterCutTimer = 0f;
                ignitionCut = false;
            }
        }

        private void CalculateLeanAngle()
        {
            Vector3 localUp = transform.up;
            Vector3 projectedUp = Vector3.ProjectOnPlane(localUp, transform.forward).normalized;
            currentLeanAngle = Vector3.SignedAngle(Vector3.up, projectedUp, transform.forward);
        }

        private void ApplySteeringAssistance()
        {
            if (!steeringAssist.enableSteeringAssist)
            {
                assistedSteerInput = smoothSteerInput;
                return;
            }

            float currentSpeed = Speed;
            assistedSteerInput = smoothSteerInput;

            if (steeringAssist.enableCounterSteering && currentSpeed > steeringAssist.counterSteerMinSpeed)
            {
                float counterSteerFactor = Mathf.InverseLerp(steeringAssist.counterSteerMinSpeed, steeringAssist.counterSteerMaxSpeed, currentSpeed);
                float leanDirection = Mathf.Sign(currentLeanAngle);
                float steerDirection = Mathf.Sign(smoothSteerInput);

                if (Mathf.Abs(smoothSteerInput) > 0.1f && leanDirection != steerDirection)
                {
                    float counterSteerAmount = steeringAssist.counterSteerStrength * counterSteerFactor * Mathf.Abs(smoothSteerInput);
                    assistedSteerInput += steerDirection * counterSteerAmount;
                }
            }

            if (steeringAssist.enableAutoAlignment && Mathf.Abs(smoothSteerInput) < 0.1f && Mathf.Abs(currentLeanAngle) > steeringAssist.alignmentDeadZone)
            {
                float alignmentSteer = -Mathf.Sign(currentLeanAngle) * steeringAssist.alignmentStrength * Time.fixedDeltaTime;
                assistedSteerInput += alignmentSteer;
            }

            if (steeringAssist.enableTurnInAssist && currentSpeed < steeringAssist.turnInMaxSpeed)
            {
                float turnInFactor = 1f - Mathf.InverseLerp(0f, steeringAssist.turnInMaxSpeed, currentSpeed);
                if (Mathf.Abs(smoothSteerInput) > 0.1f)
                {
                    float turnInAmount = steeringAssist.turnInBoost * turnInFactor * Mathf.Sign(smoothSteerInput);
                    assistedSteerInput += turnInAmount * Time.fixedDeltaTime;
                }
            }

            if (steeringAssist.enableHighSpeedStability && currentSpeed > steeringAssist.stabilityMinSpeed)
            {
                float stabilityFactor = Mathf.InverseLerp(steeringAssist.stabilityMinSpeed, steeringAssist.stabilityMinSpeed * 1.5f, currentSpeed);
                float reduction = Mathf.Lerp(1f, steeringAssist.stabilityReduction, stabilityFactor);
                assistedSteerInput *= reduction;
            }

            if (steeringAssist.enableLeanSteering)
            {
                float leanSteerAmount = (currentLeanAngle / 45f) * steeringAssist.leanSteeringSensitivity;
                assistedSteerInput += leanSteerAmount;
            }

            assistedSteerInput = Mathf.Clamp(assistedSteerInput, -1f, 1f);
        }

        private void HandleShiftingAndClutch()
        {
            isInNeutral = (currentGear == 0);

            if (isShifting)
            {
                shiftTimer += Time.fixedDeltaTime;

                float shiftDuration = transmission.quickShifter && throttleInput > 0.5f ?
                    transmission.quickShiftCutTime : transmission.shiftTime;

                float halfShift = shiftDuration * 0.5f;
                if (shiftTimer < halfShift)
                {
                    clutchPosition = shiftTimer / halfShift;
                }
                else
                {
                    clutchPosition = 1f - ((shiftTimer - halfShift) / halfShift);
                }

                if (shiftTimer >= shiftDuration)
                {
                    isShifting = false;
                    clutchPosition = 0f;
                    shiftTimer = 0f;

                    if (pendingGear != -1)
                    {
                        currentGear = pendingGear;
                        pendingGear = -1;
                        isShifting = true;
                        shiftTimer = 0f;
                    }
                }
            }

            if (transmission.autoShift && !isShifting && !isInNeutral)
            {
                if (currentRpm > transmission.autoUpshiftRpm && currentGear < transmission.gearRatios.Length)
                {
                    ShiftUp();
                }
                else if (currentRpm < transmission.autoDownshiftRpm && currentGear > 1)
                {
                    ShiftDown();
                }
            }
        }

        private void SimulateEngineWithInertia()
        {
            if (poweredWheel == null)
            {
                currentRpm = targetIdleRpm;
                engineAngularVelocity = currentRpm * RPM_TO_RAD_S;
                return;
            }

            // NEUTRAL GEAR - Free revving with inertia
            if (isInNeutral)
            {
                float targetRpm = throttleInput > 0.05f ?
                    Mathf.Lerp(targetIdleRpm, engine.redlineRpm, throttleInput) :
                    targetIdleRpm;

                float normalizedRpm = Mathf.InverseLerp(targetIdleRpm, engine.redlineRpm, currentRpm);
                float curveMultiplier = engine.torqueCurve.Evaluate(normalizedRpm);
                float throttleTorque = engine.peakTorque * curveMultiplier * throttleInput;

                float frictionTorque = engine.internalFriction;
                float netTorque = throttleTorque - frictionTorque;

                float angularAccel = netTorque / engine.engineInertia;
                engineAngularVelocity += angularAccel * Time.fixedDeltaTime;

                float minAngularVel = engine.idleRpmMin * RPM_TO_RAD_S;
                float maxAngularVel = engine.limiterRpm * RPM_TO_RAD_S;
                engineAngularVelocity = Mathf.Clamp(engineAngularVelocity, minAngularVel, maxAngularVel);

                currentRpm = engineAngularVelocity / RPM_TO_RAD_S;
                currentTorque = 0f;
                wheelTorque = 0f;
                engineLoad = 0f;
                isClutchSlipping = false;

                float theoreticalTorque = engine.peakTorque * curveMultiplier * throttleInput;
                currentHorsePower = (theoreticalTorque * currentRpm) / 7127f;

                return;
            }

            // NORMAL GEAR OPERATION WITH INERTIA
            float gearRatio = transmission.gearRatios[Mathf.Clamp(currentGear - 1, 0, transmission.gearRatios.Length - 1)];
            float totalRatio = gearRatio * transmission.finalDrive;

            float wheelAngularVel = poweredWheel.AngularVelocity;
            float wheelRpm = Mathf.Abs(wheelAngularVel) * 9.5493f;
            float targetEngineRpm = Mathf.Max(wheelRpm * totalRatio, targetIdleRpm);

            targetEngineRpm = Mathf.Clamp(targetEngineRpm, targetIdleRpm, engine.limiterRpm * 1.1f);

            // During shift - clutch disengaged, free revving
            if (isShifting || clutchPosition > 0.9f)
            {
                float targetRpm = throttleInput > 0.05f ?
                    Mathf.Lerp(targetIdleRpm, engine.redlineRpm, throttleInput) :
                    targetIdleRpm;

                float normalizedRpm = Mathf.InverseLerp(targetIdleRpm, engine.redlineRpm, currentRpm);
                float curveMultiplier = engine.torqueCurve.Evaluate(normalizedRpm);
                float throttleTorque = engine.peakTorque * curveMultiplier * throttleInput;

                float frictionTorque = engine.internalFriction;
                float netTorque = throttleTorque - frictionTorque;

                float angularAccel = netTorque / engine.engineInertia;
                engineAngularVelocity += angularAccel * Time.fixedDeltaTime;

                engineAngularVelocity = Mathf.Clamp(engineAngularVelocity,
                    engine.idleRpmMin * RPM_TO_RAD_S,
                    engine.limiterRpm * RPM_TO_RAD_S);

                currentRpm = engineAngularVelocity / RPM_TO_RAD_S;
                currentTorque = 0f;
                wheelTorque = 0f;
                engineLoad = 0f;
                isClutchSlipping = false;

                currentHorsePower = (throttleTorque * currentRpm) / 7127f;
            }
            else
            {
                float rpmDifference = Mathf.Abs(currentRpm - targetEngineRpm);
                float clutchSlipFactor = Mathf.InverseLerp(0f, transmission.clutchSlipThreshold, rpmDifference);

                float normalizedRpm = Mathf.InverseLerp(targetIdleRpm, engine.redlineRpm, currentRpm);
                float curveMultiplier = engine.torqueCurve.Evaluate(normalizedRpm);
                float engineTorque = engine.peakTorque * curveMultiplier * throttleInput;

                float demandedTorque = engineTorque * engine.globalTorqueMultiplier;
                bool heavyLoad = demandedTorque > transmission.clutchMaxSlipTorque * 0.7f;

                if (heavyLoad && throttleInput > 0.5f)
                {
                    clutchSlipFactor = Mathf.Max(clutchSlipFactor, 0.3f);
                }

                isClutchSlipping = clutchSlipFactor > 0.1f;

                if (ignitionCut)
                {
                    engineTorque = 0f;
                }
                else if (currentRpm >= engine.limiterRpm)
                {
                    engineTorque = 0f;
                }
                else
                {
                    engineTorque *= engine.globalTorqueMultiplier;
                }

                float engineBraking = 0f;
                if (throttleInput < 0.05f)
                {
                    engineBraking = Mathf.Clamp(engineBraking, 0f, engine.engineBrakingTorqueMax);
                }

                if (isClutchSlipping)
                {
                    float maxTransferTorque = transmission.clutchMaxSlipTorque * (1f - clutchSlipFactor);
                    engineTorque = Mathf.Min(engineTorque, maxTransferTorque);

                    float targetAngularVel = targetEngineRpm * RPM_TO_RAD_S;
                    float velocityDifference = targetAngularVel - engineAngularVelocity;

                    float clutchDragTorque = Mathf.Clamp(velocityDifference * transmission.clutchEngageSpeed, -100f, 100f);

                    float frictionTorque = engine.internalFriction;

                    float netTorque = engineTorque - engineBraking - frictionTorque + clutchDragTorque;
                    float angularAccel = netTorque / engine.engineInertia;
                    engineAngularVelocity += angularAccel * Time.fixedDeltaTime;

                    if (float.IsNaN(engineAngularVelocity) || float.IsInfinity(engineAngularVelocity))
                    {
                        engineAngularVelocity = targetIdleRpm * RPM_TO_RAD_S;
                    }

                    engineAngularVelocity = Mathf.Clamp(engineAngularVelocity,
                        engine.idleRpmMin * RPM_TO_RAD_S,
                        engine.limiterRpm * RPM_TO_RAD_S);

                    currentRpm = engineAngularVelocity / RPM_TO_RAD_S;
                }
                else
                {
                    currentRpm = targetEngineRpm;
                    engineAngularVelocity = currentRpm * RPM_TO_RAD_S;
                }

                float maxPossibleTorque = engine.peakTorque * curveMultiplier;
                engineLoad = maxPossibleTorque > 0 ? Mathf.Clamp01(engineTorque / maxPossibleTorque) : 0f;

                currentTorque = engineTorque - engineBraking;
                float drivetrainEfficiency = 1f - engine.drivetrainLoss;
                wheelTorque = currentTorque * totalRatio * drivetrainEfficiency;
                currentHorsePower = (currentTorque * currentRpm) / 7127f;
            }
        }

        private void ApplyToWheels()
        {
            float speed = rb.linearVelocity.magnitude;
            float steerAngle = Mathf.Lerp(maxSteeringAngle, minSteeringAngle, smoothSpeed / steeringSpeedThreshold) * assistedSteerInput;
            bool handbrakePressed = inputConfig.handbrakeAction != null && inputConfig.handbrakeAction.action.IsPressed();

            float dragForce = 0.5f * AIR_DENSITY * engine.dragCoefficient * engine.frontalArea * speed * speed;
            Vector3 dragVector = -rb.linearVelocity.normalized * dragForce;
            rb.AddForce(dragVector, ForceMode.Force);

            foreach (var wheel in wheels)
            {
                WheelUAPI wc = wheel.wheelUAPI;
                wc.BrakeTorque = 0f;
                wc.MotorTorque = 0f;

                if (wheel.steer)
                {
                    wc.SteerAngle = steerAngle;
                }

                if (isInNeutral)
                {
                    if (wheel.power)
                    {
                        wc.BrakeTorque = engine.engineBrakingTorqueBase * 0.05f;
                    }
                    continue;
                }

                if (wheel.power && wheelTorque > 0)
                {
                    wc.MotorTorque = wheelTorque;
                }

                if (brakeInput > 0.1f)
                {
                    float brakeTorque = wheel.steer ? frontBrakeTorque : rearBrakeTorque;
                    wc.BrakeTorque = brakeTorque * brakeInput;
                }

                if (wheel.handbrake && handbrakePressed)
                {
                    wc.BrakeTorque = rearBrakeTorque;
                }
            }
        }

        public void ShiftUp()
        {
            int targetGear = -1;

            if (isInNeutral)
            {
                targetGear = 1;
            }
            else if (currentGear < transmission.gearRatios.Length)
            {
                targetGear = currentGear + 1;
            }

            if (targetGear != -1)
            {
                if (isShifting)
                {
                    pendingGear = targetGear;
                }
                else
                {
                    currentGear = targetGear;
                    isShifting = true;
                    shiftTimer = 0f;
                }
            }
        }

        public void ShiftDown()
        {
            int targetGear = -1;

            if (currentGear > 1)
            {
                targetGear = currentGear - 1;
            }

            if (targetGear != -1)
            {
                // Store RPM before shift for pop calculation
                lastShiftRpm = currentRpm;

                if (isShifting)
                {
                    pendingGear = targetGear;
                }
                else
                {
                    currentGear = targetGear;
                    isShifting = true;
                    shiftTimer = 0f;

                    // Trigger pops if RPM is above threshold
                    if (lastShiftRpm >= engine.popActivationRpm)
                    {
                        TriggerDownshiftPops(lastShiftRpm);
                    }
                }
            }
        }

        private void TriggerDownshiftPops(float shiftRpm)
        {
            if (engineSound == null) return;

            // Stop any existing pop coroutine
            if (popCoroutine != null)
            {
                StopCoroutine(popCoroutine);
            }

            // Calculate scaled values based on RPM
            float rpmFactor = Mathf.InverseLerp(engine.popActivationRpm, engine.redlineRpm, shiftRpm);

            float duration = Mathf.Lerp(engine.popBaseDuration, engine.popMaxDuration, rpmFactor);
            float intensity = Mathf.Lerp(engine.popBaseIntensity, engine.popMaxIntensity, rpmFactor);
            float frequency = Mathf.Lerp(engine.popBaseFrequency, engine.popMaxFrequency, rpmFactor);

            popCoroutine = StartCoroutine(PlayDownshiftPops(duration, intensity, frequency));
        }

        private IEnumerator PlayDownshiftPops(float duration, float intensity, float frequency)
        {
            if (engineSound == null) yield break;

            engineSound.exhaustPops = true;
            engineSound.popIntensity = intensity;
            engineSound.popFrequency = frequency;

            yield return new WaitForSeconds(duration);

            engineSound.exhaustPops = false;
            popCoroutine = null;
        }

        public void ToggleNeutral()
        {
            int targetGear = -1;

            if (isInNeutral)
            {
                targetGear = 1;
            }
            else if (currentGear == 1)
            {
                targetGear = 0;
            }

            if (targetGear != -1)
            {
                if (isShifting)
                {
                    pendingGear = targetGear;
                }
                else
                {
                    currentGear = targetGear;
                    isShifting = true;
                    shiftTimer = 0f;
                }
            }
        }

        private void Reset()
        {
            wheels = new List<BikeWheel>();
            WheelUAPI[] wheelUAPIs = GetComponentsInChildren<WheelUAPI>();
            for (int i = 0; i < wheelUAPIs.Length; i++)
            {
                WheelUAPI wheelUAPI = wheelUAPIs[i];
                wheels.Add(new BikeWheel()
                {
                    wheelUAPI = wheelUAPI,
                    steer = i == 0,
                    power = i == 1,
                    handbrake = i == 1
                });
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || rb == null || !steeringAssist.enableSteeringAssist) return;

            Vector3 gizmoCenter = transform.position + Vector3.up * 3f;

            Gizmos.color = Color.red;
            Vector3 rawSteerDir = Quaternion.Euler(0f, rawSteerInput * 45f, 0f) * transform.forward;
            Gizmos.DrawLine(gizmoCenter, gizmoCenter + rawSteerDir * 1.5f);

            Gizmos.color = Color.green;
            Vector3 assistedSteerDir = Quaternion.Euler(0f, assistedSteerInput * 45f, 0f) * transform.forward;
            Gizmos.DrawLine(gizmoCenter, gizmoCenter + assistedSteerDir * 2f);

            Gizmos.color = Color.magenta;
            Vector3 rightDir = Vector3.Cross(transform.forward, Vector3.up).normalized;
            Gizmos.DrawLine(gizmoCenter + Vector3.down * 0.5f,
                           gizmoCenter + Vector3.down * 0.5f + rightDir * (currentLeanAngle / 45f));
        }
    }
}