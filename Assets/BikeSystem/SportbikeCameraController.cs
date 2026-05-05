using NWH.WheelController3D;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SportbikeCameraController : MonoBehaviour
{
    public Camera cam;
    public SportbikeUI bikeUI;
    public SportbikeController bikeController;
    public RawImage rainMaterialImage;

    [Header("Camera Settings")]
    [SerializeField] private Transform bikeTransform;
    [SerializeField] private float lookSensitivity = 120f;
    [SerializeField] private float scrollSensitivity = 0.3f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference focusAction;
    [SerializeField] private InputActionReference heightAdjustAction;

    [Header("Movement Ranges")]
    [SerializeField] private float maxYaw = 85f;
    [SerializeField] private float maxPitchUp = 45f;
    [SerializeField] private float maxPitchDown = 30f;
    [SerializeField] private float maxBodyHeight = 0.4f;
    [SerializeField] private float minBodyHeight = -0.3f;
    [SerializeField] private float maxLean = 0.15f;

    [Header("Natural Coupling")]
    [SerializeField] private float sideLeanFromYaw = 0.3f;
    [SerializeField] private float forwardLeanFromPitch = 0.2f;
    [SerializeField] private float pitchFromHeight = 15f;
    [SerializeField] private float autoLeanAtLowest = -20f;
    [SerializeField] private float autoLeanAtHighest = 5f;

    [Header("Auto-Centering")]
    [SerializeField] private bool enableAutoCentering = true;
    [SerializeField] private float centeringStrength = 2f;
    [SerializeField] private float focusCenteringStrength = 5f;
    [SerializeField] private float focusLookRange = 15f;
    [SerializeField] private Vector3 lookDirectionOffset = new Vector3(-5f, 0f, 0f);

    [Header("Camera Wobble")]
    [SerializeField] private bool enableWobble = true;
    [SerializeField] private float wobbleIntensityAtRest = 0.004f;
    [SerializeField] private float wobbleIntensityAtSpeed = 0.015f;
    [SerializeField] private float wobbleFrequencyAtRest = 3.0f;
    [SerializeField] private float wobbleFrequencyAtSpeed = 8.0f;
    [SerializeField] private float speedRampStart = 5f;
    [SerializeField] private float speedRampEnd = 80f;
    [SerializeField] private float upperBodyWobbleMultiplier = 2.5f;
    [SerializeField] private float wobbleVariation = 0.3f;

    [Header("Lean Response")]
    [SerializeField] private bool enableLeanResponse = true;
    [SerializeField] private float leanLookIntensity = 0.25f;
    [SerializeField] private float leanHeadTiltIntensity = 0.15f;
    [SerializeField] private float leanSideShiftIntensity = 0.08f;
    [SerializeField] private float leanResponseSpeed = 3f;

    [Header("Shift Response")]
    [SerializeField] private bool enableShiftResponse = true;
    [SerializeField] private float upshiftForwardKick = 0.15f;
    [SerializeField] private float downshiftBackwardKick = 0.12f;
    [SerializeField] private float shiftKickDuration = 0.25f;
    [SerializeField] private AnimationCurve shiftKickCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.12f;

    // Current state
    private float currentYaw = 0f;
    private float currentPitch = 0f;
    public float currentHeight = 0f;

    // Target state
    private float targetYaw = 0f;
    private float targetPitch = 0f;
    private float targetHeight = 0f;

    // Smoothing velocities
    private float yawVelocity = 0f;
    private float pitchVelocity = 0f;
    private float heightVelocity = 0f;

    private float rainMult = 1f;

    // Wobble state
    private float wobbleTimeX = 0f;
    private float wobbleTimeY = 0f;
    private float wobbleTimeZ = 0f;
    private float noiseOffsetX = 0f;
    private float noiseOffsetY = 0f;
    private float noiseOffsetZ = 0f;

    private Vector3 baseLocalPosition;
    private bool isFocused = false;

    // Lean response state
    private float smoothedLeanAngle = 0f;
    private float leanVelocity = 0f;

    // Shift response state
    private bool isShiftKickActive = false;
    private float shiftKickTimer = 0f;
    private float shiftKickDirection = 0f;
    private int lastKnownGear = 1;

    private float accumulatedRainTime = 0f;

    void OnEnable()
    {
        if (lookAction != null) lookAction.action.Enable();
        if (focusAction != null)
        {
            focusAction.action.Enable();
            focusAction.action.performed += OnFocusPerformed;
        }
        if (heightAdjustAction != null) heightAdjustAction.action.Enable();
    }

    void OnDisable()
    {
        if (lookAction != null) lookAction.action.Disable();
        if (focusAction != null)
        {
            focusAction.action.performed -= OnFocusPerformed;
            focusAction.action.Disable();
        }
        if (heightAdjustAction != null) heightAdjustAction.action.Disable();
    }

    void Start()
    {
        if (bikeTransform == null)
        {
            bikeTransform = transform.parent;
        }

        baseLocalPosition = transform.localPosition;
        currentHeight = 0f;
        targetHeight = 0f;

        wobbleTimeX = Random.Range(0f, Mathf.PI * 2f);
        wobbleTimeY = Random.Range(0f, Mathf.PI * 2f);
        wobbleTimeZ = Random.Range(0f, Mathf.PI * 2f);

        noiseOffsetX = Random.Range(0f, 1000f);
        noiseOffsetY = Random.Range(0f, 1000f);
        noiseOffsetZ = Random.Range(0f, 1000f);

        if (bikeController != null)
        {
            lastKnownGear = bikeController.CurrentGear;
        }
    }

    void Update()
    {
        if (!isFocused)
        {
            HandleLookInput();
            HandleHeightInput();
        }
        else
        {
            HandleLookInput();
            HandleHeightInput();
        }

        DetectGearChanges();
        UpdateShiftKick();
        ApplyCameraTransform();

        float currentSpeed = bikeUI.GetWheelSpeed();
        cam.fieldOfView = Mathf.Lerp(80f, 120f, currentSpeed / 100);
        // --- 1. SETUP FACTORS ---
        float visibilityFactor = Mathf.Clamp01(currentSpeed / 80f);
        float simSpeedFactor = Mathf.Clamp01(currentSpeed / 135f);

        // --- 2. CALCULATE RAIN AMOUNT ---
        float visibilityCurve = Mathf.Pow(visibilityFactor, 0.5f);
        float rainAmount = Mathf.Lerp(1.0f, 0.15f, visibilityCurve);

        // --- 3. CALCULATE ZOOM (Your Specific Numbers) ---
        // At rest (0 speed), we want 0.346 (Big drops)
        // At max speed, we want 0.863 (Small drops)
        float zoomScale = Mathf.Lerp(0.346f, 0.863f, simSpeedFactor);

        // --- 4. CALCULATE ANIMATION SPEED ---
        float targetSimSpeed = Mathf.Lerp(0.001f, 4.0f, simSpeedFactor);
        accumulatedRainTime += Time.deltaTime * targetSimSpeed;

        // --- 5. APPLY ---
        float currentRainMult = (currentHeight > -0.08f) ? 1f : 0.5f;

        rainMaterialImage.material.SetFloat("_RainAmount", rainAmount * currentRainMult);
        // Passing the Zoom Factor to "_DropSize"
        rainMaterialImage.material.SetFloat("_DropSize", zoomScale);
        rainMaterialImage.material.SetFloat("_RainTime", accumulatedRainTime);
        rainMaterialImage.material.SetFloat("_RadialStrength", 1.0f);
    }

    void OnFocusPerformed(InputAction.CallbackContext context)
    {
        isFocused = !isFocused;

        if (isFocused)
        {
            targetYaw = 0f;
            targetPitch = -10f;
            targetHeight = minBodyHeight;
        }
    }

    void HandleLookInput()
    {
        if (lookAction == null) return;

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();

        float activeMaxYaw = isFocused ? focusLookRange : maxYaw;
        float activeMaxPitchUp = isFocused ? focusLookRange : maxPitchUp;
        float activeMaxPitchDown = isFocused ? focusLookRange : maxPitchDown;
        float activeCenteringStrength = isFocused ? focusCenteringStrength : centeringStrength;

        targetYaw += lookInput.x * lookSensitivity * Time.deltaTime;

        if (enableAutoCentering)
        {
            targetYaw = Mathf.Lerp(targetYaw, 0f, activeCenteringStrength * Time.deltaTime);
        }

        targetYaw = Mathf.Clamp(targetYaw, -activeMaxYaw, activeMaxYaw);

        targetPitch += lookInput.y * lookSensitivity * Time.deltaTime;

        if (enableAutoCentering)
        {
            targetPitch = Mathf.Lerp(targetPitch, 0f, activeCenteringStrength * Time.deltaTime);
        }

        targetPitch = Mathf.Clamp(targetPitch, -activeMaxPitchDown, activeMaxPitchUp);
    }

    void HandleHeightInput()
    {
        if (heightAdjustAction == null) return;

        float heightInput = heightAdjustAction.action.ReadValue<float>();

        if (Mathf.Abs(heightInput) > 0.01f)
        {
            targetHeight += heightInput * scrollSensitivity * Time.deltaTime;
            targetHeight = Mathf.Clamp(targetHeight, minBodyHeight, maxBodyHeight);
        }
    }

    void DetectGearChanges()
    {
        if (!enableShiftResponse || bikeController == null) return;

        int currentGear = bikeController.CurrentGear;

        if (currentGear != lastKnownGear && lastKnownGear > 0 && currentGear > 0)
        {
            if (currentGear > lastKnownGear)
            {
                TriggerShiftKick(upshiftForwardKick);
            }
            else if (currentGear < lastKnownGear)
            {
                TriggerShiftKick(-downshiftBackwardKick);
            }
        }

        lastKnownGear = currentGear;
    }

    void TriggerShiftKick(float direction)
    {
        isShiftKickActive = true;
        shiftKickTimer = 0f;
        shiftKickDirection = direction;
    }

    void UpdateShiftKick()
    {
        if (!isShiftKickActive) return;

        shiftKickTimer += Time.deltaTime;

        if (shiftKickTimer >= shiftKickDuration)
        {
            isShiftKickActive = false;
            shiftKickTimer = 0f;
        }
    }

    Vector3 CalculateShiftKickOffset()
    {
        if (!isShiftKickActive) return Vector3.zero;

        float normalizedTime = shiftKickTimer / shiftKickDuration;
        float curveValue = shiftKickCurve.Evaluate(normalizedTime);
        float kickAmount = shiftKickDirection * curveValue;

        return new Vector3(0f, 0f, kickAmount);
    }

    Vector3 CalculateLeanOffset()
    {
        if (!enableLeanResponse || bikeController == null) return Vector3.zero;

        float targetLeanAngle = bikeController.CurrentLeanAngle;
        smoothedLeanAngle = Mathf.SmoothDamp(smoothedLeanAngle, targetLeanAngle, ref leanVelocity, 1f / leanResponseSpeed);

        float normalizedLean = smoothedLeanAngle / 45f;
        float sideShift = normalizedLean * leanSideShiftIntensity;

        return new Vector3(sideShift, 0f, 0f);
    }

    float CalculateLeanYawOffset()
    {
        if (!enableLeanResponse || bikeController == null) return 0f;

        float normalizedLean = smoothedLeanAngle / 45f;
        return normalizedLean * leanLookIntensity * 15f;
    }

    float CalculateLeanRollOffset()
    {
        if (!enableLeanResponse || bikeController == null) return 0f;

        float normalizedLean = smoothedLeanAngle / 45f;
        return normalizedLean * leanHeadTiltIntensity * 10f;
    }

    Vector3 CalculateWobble()
    {
        if (!enableWobble) return Vector3.zero;

        float currentSpeed = bikeUI != null ? bikeUI.GetWheelSpeed() : 0f;
        float speedRamp = Mathf.InverseLerp(speedRampStart, speedRampEnd, currentSpeed);
        speedRamp = Mathf.Clamp01(speedRamp);

        float baseIntensity = Mathf.Lerp(wobbleIntensityAtRest, wobbleIntensityAtSpeed, speedRamp);
        float currentFrequency = Mathf.Lerp(wobbleFrequencyAtRest, wobbleFrequencyAtSpeed, speedRamp);

        float heightRange = maxBodyHeight - minBodyHeight;
        float heightThreshold = minBodyHeight + (heightRange * 0.4f);

        float finalIntensity = baseIntensity;
        if (currentHeight > heightThreshold)
        {
            finalIntensity *= upperBodyWobbleMultiplier;
        }

        wobbleTimeX += Time.deltaTime * currentFrequency;
        wobbleTimeY += Time.deltaTime * currentFrequency * 1.3f;
        wobbleTimeZ += Time.deltaTime * currentFrequency * 0.8f;

        float noiseX = Mathf.PerlinNoise(noiseOffsetX + wobbleTimeX * 0.5f, 0f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(noiseOffsetY + wobbleTimeY * 0.5f, 0f) * 2f - 1f;
        float noiseZ = Mathf.PerlinNoise(noiseOffsetZ + wobbleTimeZ * 0.5f, 0f) * 2f - 1f;

        float sineX = Mathf.Sin(wobbleTimeX);
        float sineY = Mathf.Sin(wobbleTimeY);
        float sineZ = Mathf.Sin(wobbleTimeZ);

        float wobbleX = Mathf.Lerp(sineX, noiseX, wobbleVariation) * finalIntensity;
        float wobbleY = Mathf.Lerp(sineY, noiseY, wobbleVariation) * finalIntensity * 0.7f;
        float wobbleZ = Mathf.Lerp(sineZ, noiseZ, wobbleVariation) * finalIntensity * 0.5f;

        return new Vector3(wobbleX, wobbleY, wobbleZ);
    }

    void ApplyCameraTransform()
    {
        currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, smoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, smoothTime);
        currentHeight = Mathf.SmoothDamp(currentHeight, targetHeight, ref heightVelocity, smoothTime);

        float heightRatio = (currentHeight - minBodyHeight) / (maxBodyHeight - minBodyHeight);
        float autoLeanPitch = Mathf.Lerp(autoLeanAtLowest, autoLeanAtHighest, heightRatio);

        float sideLean = currentYaw * sideLeanFromYaw * 0.01f;
        float forwardLean = -currentPitch * forwardLeanFromPitch * 0.01f;
        float heightPitchOffset = (currentHeight / maxBodyHeight) * pitchFromHeight;

        float leanYawOffset = CalculateLeanYawOffset();
        float leanRollOffset = CalculateLeanRollOffset();

        float finalPitch = currentPitch + heightPitchOffset + autoLeanPitch + lookDirectionOffset.x;
        float finalYaw = currentYaw + lookDirectionOffset.y + leanYawOffset;
        float finalRoll = lookDirectionOffset.z + leanRollOffset;

        Vector3 wobbleOffset = CalculateWobble();
        Vector3 leanOffset = CalculateLeanOffset();
        Vector3 shiftKickOffset = CalculateShiftKickOffset();

        Vector3 newLocalPos = baseLocalPosition;
        newLocalPos.y += currentHeight;
        newLocalPos.x += sideLean;
        newLocalPos.z += forwardLean;
        newLocalPos += wobbleOffset + leanOffset + shiftKickOffset;
        transform.localPosition = newLocalPos;

        Quaternion targetRotation = Quaternion.Euler(finalPitch, finalYaw, finalRoll);
        transform.localRotation = targetRotation;

        float rollAmount = currentYaw * 0.04f;
        transform.localRotation *= Quaternion.Euler(0, 0, -rollAmount);
    }

    public void ResetCamera()
    {
        targetYaw = 0f;
        targetPitch = 0f;
        targetHeight = 0f;
        isFocused = false;
        isShiftKickActive = false;
        shiftKickTimer = 0f;
        smoothedLeanAngle = 0f;
    }

    public void SetSensitivity(float sensitivity)
    {
        lookSensitivity = sensitivity;
    }

    public bool IsFocused()
    {
        return isFocused;
    }

    public void SetWobbleEnabled(bool enabled)
    {
        enableWobble = enabled;
    }

    public void SetWobbleIntensity(float intensityAtRest, float intensityAtSpeed)
    {
        wobbleIntensityAtRest = intensityAtRest;
        wobbleIntensityAtSpeed = intensityAtSpeed;
    }

    public void SetWobbleFrequency(float frequencyAtRest, float frequencyAtSpeed)
    {
        wobbleFrequencyAtRest = frequencyAtRest;
        wobbleFrequencyAtSpeed = frequencyAtSpeed;
    }

    public void SetLookDirectionOffset(Vector3 offset)
    {
        lookDirectionOffset = offset;
    }

    public Vector3 GetLookDirectionOffset()
    {
        return lookDirectionOffset;
    }

    public void SetLeanResponse(bool enabled, float lookIntensity, float tiltIntensity, float shiftIntensity)
    {
        enableLeanResponse = enabled;
        leanLookIntensity = lookIntensity;
        leanHeadTiltIntensity = tiltIntensity;
        leanSideShiftIntensity = shiftIntensity;
    }

    public void SetShiftResponse(bool enabled, float upshiftKick, float downshiftKick)
    {
        enableShiftResponse = enabled;
        upshiftForwardKick = upshiftKick;
        downshiftBackwardKick = downshiftKick;
    }
}