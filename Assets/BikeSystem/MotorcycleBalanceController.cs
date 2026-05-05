using UnityEngine;
using NWH.WheelController3D;

public class MotorcycleBalanceController : MonoBehaviour
{
    [Header("PID Balance Settings")]
    [SerializeField] private float proportionalGain = 300f;
    [SerializeField] private float integralGain = 50f;
    [SerializeField] private float derivativeGain = 100f;
    [SerializeField] private float maxIntegralAccumulation = 20f;
    [SerializeField] private float maxBalanceAngle = 45f;

    [Header("Speed-Based Assistance")]
    [SerializeField] private float minBalanceSpeed = 1f;
    [SerializeField] private float maxBalanceSpeed = 30f;
    [SerializeField] private AnimationCurve balanceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Lean Angle Settings")]
    [SerializeField] private WheelController frontWheel;
    [SerializeField] private WheelController rearWheel;
    [Tooltip("Maximum lean angle in degrees")]
    [SerializeField] private float maxLeanAngle = 40f;
    [Tooltip("How much steering input affects desired lean")]
    [SerializeField] private float steerToLeanRatio = 1.0f;
    [Tooltip("Lean multiplier at low speed")]
    [SerializeField] private float lowSpeedLeanMultiplier = 0.05f;
    [Tooltip("Lean multiplier at high speed")]
    [SerializeField] private float highSpeedLeanMultiplier = 2.0f;
    [Tooltip("Flip this if leaning the wrong direction")]
    [SerializeField] private bool invertLeanDirection = false;

    [Header("Lean Response Settings")]
    [Tooltip("Smooth transition time when changing lean direction")]
    [SerializeField] private float leanDirectionChangeSmoothing = 0.3f;

    [Header("Load Transfer Settings")]
    [Tooltip("Extra downforce multiplier during cornering (higher = more grip)")]
    [SerializeField] private float corneringDownforceMultiplier = 2.0f;
    [Tooltip("How much to push weight forward onto front wheel during turns")]
    [SerializeField] private float frontWheelBias = 0.7f;
    [Tooltip("Minimum speed before cornering downforce applies")]
    [SerializeField] private float minCorneringSpeed = 3f;

    [Header("Direct Steering Assist")]
    [Tooltip("Enable direct lateral force application to help turn")]
    [SerializeField] private bool enableDirectSteeringAssist = true;
    [Tooltip("Maximum lateral force applied to assist turning (Newtons)")]
    [SerializeField] private float maxSteeringAssistForce = 5000f;
    [Tooltip("Minimum speed before steering assist activates")]
    [SerializeField] private float minSteeringAssistSpeed = 5f;
    [Tooltip("How steering assist strength scales with speed")]
    [SerializeField] private AnimationCurve steeringAssistCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Where to apply the force (0=rear, 1=front, 0.5=center)")]
    [SerializeField] private float steeringForceApplicationPoint = 0.3f;
    [Tooltip("Multiplier when leaning hard into a turn")]
    [SerializeField] private float leanAngleBoostMultiplier = 1.5f;
    [Tooltip("Apply force gradually based on lean commitment")]
    [SerializeField] private bool scaleForceBylean = true;

    [Header("Advanced PID Tuning")]
    [Tooltip("Auto-scale gains based on mass (recommended)")]
    [SerializeField] private bool autoScaleForMass = true;
    [Tooltip("Reference mass for gain tuning (leave at your typical bike mass)")]
    [SerializeField] private float referenceMass = 200f;
    [Tooltip("Derivative filter to reduce noise (0-1, higher = more filtering)")]
    [SerializeField] private float derivativeFilter = 0.1f;

    [Header("References")]
    [SerializeField] private Rigidbody rb;

    // PID state variables
    private float integralError;
    private float previousError;
    private float filteredDerivative;

    private float currentSpeed;
    private float targetLeanAngle;
    private float currentLeanAngle;
    private float smoothedTargetLean;
    private float leanVelocity;

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Initialize PID state
        integralError = 0f;
        previousError = 0f;
        filteredDerivative = 0f;
    }

    void FixedUpdate()
    {
        CalculateSpeed();
        CalculateTargetLeanAngle();
        ApplyPIDBalanceForce();
        ApplyLoadTransfer();

        if (enableDirectSteeringAssist)
            ApplyDirectSteeringAssist();
    }

    void CalculateSpeed()
    {
        currentSpeed = rb.linearVelocity.magnitude;
    }

    void CalculateTargetLeanAngle()
    {
        targetLeanAngle = 0f;

        if (frontWheel == null)
            return;

        // Get steering angle
        float steerAngle = frontWheel.SteerAngle;

        // Base lean from steering input
        float leanDirection = invertLeanDirection ? 1f : -1f;
        float desiredLean = steerAngle * steerToLeanRatio * leanDirection;

        // Calculate speed-based lean multiplier
        float speedFactor = Mathf.Clamp01(currentSpeed / maxBalanceSpeed);
        float speedLeanScale = Mathf.Lerp(lowSpeedLeanMultiplier, highSpeedLeanMultiplier, speedFactor);
        desiredLean *= speedLeanScale;

        // Clamp to max lean angle
        float rawTargetLean = Mathf.Clamp(desiredLean, -maxLeanAngle, maxLeanAngle);

        // Smooth the target lean to prevent jitter
        smoothedTargetLean = Mathf.SmoothDamp(
            smoothedTargetLean,
            rawTargetLean,
            ref leanVelocity,
            leanDirectionChangeSmoothing
        );

        targetLeanAngle = smoothedTargetLean;
    }

    void ApplyPIDBalanceForce()
    {
        // Don't apply balance forces below minimum speed
        if (currentSpeed < minBalanceSpeed)
        {
            // Reset integral when not balancing to prevent windup
            integralError = 0f;
            previousError = 0f;
            return;
        }

        // Get the current lean angle (roll)
        Vector3 localUp = transform.up;
        Vector3 projectedUp = Vector3.ProjectOnPlane(localUp, transform.forward).normalized;
        currentLeanAngle = Vector3.SignedAngle(Vector3.up, projectedUp, transform.forward);

        // Don't apply balance if tilted too far (fallen over)
        if (Mathf.Abs(currentLeanAngle) > maxBalanceAngle)
        {
            integralError = 0f;
            return;
        }

        // Calculate balance strength based on speed
        float speedFactor = Mathf.InverseLerp(minBalanceSpeed, maxBalanceSpeed, currentSpeed);
        float balanceStrength = balanceCurve.Evaluate(speedFactor);

        // Calculate error from target lean angle
        float error = targetLeanAngle - currentLeanAngle;

        // === PROPORTIONAL TERM ===
        float proportionalTerm = error;

        // === INTEGRAL TERM ===
        // Accumulate error over time, but clamp to prevent windup
        integralError += error * Time.fixedDeltaTime;
        integralError = Mathf.Clamp(integralError, -maxIntegralAccumulation, maxIntegralAccumulation);

        // Anti-windup: reset integral if we're far from target and fighting it
        if (Mathf.Abs(error) > maxLeanAngle * 0.8f && Mathf.Sign(error) != Mathf.Sign(integralError))
        {
            integralError = 0f;
        }

        float integralTerm = integralError;

        // === DERIVATIVE TERM ===
        // Calculate derivative of error (rate of change)
        float errorDerivative = (error - previousError) / Time.fixedDeltaTime;

        // Apply low-pass filter to reduce noise
        filteredDerivative = Mathf.Lerp(filteredDerivative, errorDerivative, derivativeFilter);

        float derivativeTerm = filteredDerivative;

        // Store for next frame
        previousError = error;

        // === CALCULATE GAINS ===
        float kp = proportionalGain;
        float ki = integralGain;
        float kd = derivativeGain;

        // Auto-scale gains based on mass if enabled
        if (autoScaleForMass && referenceMass > 0)
        {
            float massScale = rb.mass / referenceMass;
            kp *= massScale;
            ki *= massScale;
            kd *= massScale;
        }

        // Apply speed-based scaling
        kp *= balanceStrength;
        ki *= balanceStrength;
        kd *= balanceStrength;

        // === CALCULATE TOTAL TORQUE ===
        float pidOutput = (kp * proportionalTerm) + (ki * integralTerm) + (kd * derivativeTerm);

        // Apply the torque around the forward axis
        Vector3 torque = transform.forward * pidOutput;
        rb.AddTorque(torque, ForceMode.Force);
    }

    void ApplyLoadTransfer()
    {
        if (frontWheel == null || rearWheel == null)
            return;

        if (currentSpeed < minCorneringSpeed)
            return;

        // Calculate lean intensity (0 to 1)
        float leanIntensity = Mathf.Abs(currentLeanAngle) / maxLeanAngle;

        // Only apply extra downforce when actually cornering
        if (leanIntensity < 0.1f)
            return;

        // Calculate speed factor
        float speedFactor = Mathf.Clamp01(currentSpeed / maxBalanceSpeed);

        // Calculate total extra downforce
        float totalWeight = rb.mass * Mathf.Abs(Physics.gravity.y);
        float extraDownforce = totalWeight * leanIntensity * speedFactor * corneringDownforceMultiplier;

        // Distribute force
        float frontForce = extraDownforce * frontWheelBias;
        float rearForce = extraDownforce * (1f - frontWheelBias);

        // Apply the forces
        Vector3 frontDownforce = Vector3.down * frontForce;
        Vector3 rearDownforce = Vector3.down * rearForce;

        rb.AddForceAtPosition(frontDownforce, frontWheel.transform.position, ForceMode.Force);
        rb.AddForceAtPosition(rearDownforce, rearWheel.transform.position, ForceMode.Force);
    }

    void ApplyDirectSteeringAssist()
    {
        if (frontWheel == null || rearWheel == null)
            return;

        if (currentSpeed < minSteeringAssistSpeed)
            return;

        float steerAngle = frontWheel.SteerAngle;

        if (Mathf.Abs(steerAngle) < 0.1f)
            return;

        // Calculate speed-based assist strength
        float speedFactor = Mathf.InverseLerp(minSteeringAssistSpeed, maxBalanceSpeed, currentSpeed);
        float assistStrength = steeringAssistCurve.Evaluate(speedFactor);

        // Calculate desired lateral force direction
        Vector3 lateralDir = transform.right * Mathf.Sign(steerAngle);

        // Base force magnitude
        float forceMagnitude = maxSteeringAssistForce * assistStrength * (Mathf.Abs(steerAngle) / 45f);

        // Optional: scale force by lean commitment
        if (scaleForceBylean)
        {
            float leanIntensity = Mathf.Abs(currentLeanAngle) / maxLeanAngle;

            if (Mathf.Sign(currentLeanAngle) == Mathf.Sign(steerAngle))
            {
                forceMagnitude *= Mathf.Lerp(0.3f, leanAngleBoostMultiplier, leanIntensity);
            }
            else
            {
                forceMagnitude *= Mathf.Lerp(1f, 0.2f, leanIntensity);
            }
        }

        // Calculate application point
        Vector3 forcePoint = Vector3.Lerp(
            rearWheel.transform.position,
            frontWheel.transform.position,
            steeringForceApplicationPoint
        );

        // Apply the lateral force
        Vector3 steeringForce = lateralDir * forceMagnitude;
        rb.AddForceAtPosition(steeringForce, forcePoint, ForceMode.Force);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (rb == null) return;

        Vector3 gizmoCenter = transform.position + Vector3.up * 2f;

        float rollAngle = currentLeanAngle;

        // Color based on balance state
        if (Mathf.Abs(rollAngle) > maxBalanceAngle)
            Gizmos.color = Color.red;
        else if (Mathf.Abs(rollAngle) > maxBalanceAngle * 0.7f)
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(gizmoCenter, 0.3f);

        // Draw up vectors
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(gizmoCenter, gizmoCenter + Vector3.up * 1.5f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(gizmoCenter, gizmoCenter + transform.up * 1.5f);

        // Draw forward direction
        Gizmos.color = Color.green;
        Gizmos.DrawLine(gizmoCenter, gizmoCenter + transform.forward * 2f);

        // Draw current lean angle
        Gizmos.color = Color.yellow;
        Vector3 rightDir = Vector3.Cross(transform.forward, Vector3.up).normalized;
        float leanVisual = Mathf.Clamp(rollAngle / maxBalanceAngle, -1f, 1f);
        Gizmos.DrawLine(gizmoCenter, gizmoCenter + rightDir * leanVisual * 1.5f);

        // Draw target lean angle
        if (currentSpeed >= minBalanceSpeed)
        {
            Gizmos.color = Color.magenta;
            float targetVisual = Mathf.Clamp(targetLeanAngle / maxBalanceAngle, -1f, 1f);
            Gizmos.DrawLine(gizmoCenter, gizmoCenter + rightDir * targetVisual * 1.8f);
        }

        // Draw speed indicator
        float speedFactor = Mathf.InverseLerp(minBalanceSpeed, maxBalanceSpeed, currentSpeed);
        Gizmos.color = Color.Lerp(Color.red, Color.green, speedFactor);
        Gizmos.DrawWireCube(gizmoCenter + Vector3.down * 0.8f, Vector3.one * (0.2f + speedFactor * 0.3f));

        // Draw PID error indicator
        if (currentSpeed >= minBalanceSpeed)
        {
            float error = targetLeanAngle - currentLeanAngle;
            float errorNormalized = Mathf.Clamp(error / maxLeanAngle, -1f, 1f);
            Gizmos.color = Color.Lerp(Color.red, Color.green, 0.5f + errorNormalized * 0.5f);
            Gizmos.DrawWireCube(gizmoCenter + Vector3.down * 1.2f, Vector3.one * 0.3f);
        }

        // Draw steering assist force direction
        if (enableDirectSteeringAssist && frontWheel != null && currentSpeed >= minSteeringAssistSpeed)
        {
            float steerAngle = frontWheel.SteerAngle;
            if (Mathf.Abs(steerAngle) > 0.1f)
            {
                Gizmos.color = Color.cyan;
                Vector3 forceDir = transform.right * Mathf.Sign(steerAngle);

                if (rearWheel != null)
                {
                    Vector3 forcePoint = Vector3.Lerp(
                        rearWheel.transform.position,
                        frontWheel.transform.position,
                        steeringForceApplicationPoint
                    );

                    Gizmos.DrawSphere(forcePoint, 0.15f);
                    Gizmos.DrawRay(forcePoint, forceDir * 2f);
                }
            }
        }
    }
}