using NWH.WheelController3D;
using UnityEngine;
using UnityEngine.InputSystem;
using static NWH.WheelController3D.SportbikeController;

public class JumpStart : MonoBehaviour
{
    public Rigidbody bikeRigidbody;
    public float jumpForce = 500f;
    public float maxSpeedForJumpStart = 10f;
    public float wheelieTorque = 200f;
    public float throttleCurveExponent = 2f; // Adjust for curve steepness (2 = quadratic, 3 = cubic, etc.)
    public InputActionReference throttleAction;
    public SportbikeController bikeController;

    void Start()
    {
        if (bikeRigidbody == null)
        {
            bikeRigidbody = GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        float throttleInput = throttleAction != null ?
            Mathf.Clamp01(throttleAction.action.ReadValue<float>()) : 0f;

        if (bikeRigidbody != null && bikeRigidbody.linearVelocity.magnitude <= maxSpeedForJumpStart && throttleInput >= 0.7 && IsWithinRotationRange() && !bikeController.isInNeutral)
        {
            float decayedMultBySpeed = 1f - (bikeRigidbody.linearVelocity.magnitude / maxSpeedForJumpStart);

            // Apply exponential curve to throttle input
            float exponentialThrottle = Mathf.Pow(throttleInput, throttleCurveExponent);

            // Apply force in the bike's forward direction
            bikeRigidbody.AddForce(transform.forward * jumpForce * exponentialThrottle * decayedMultBySpeed, ForceMode.Acceleration);
            // Apply torque around the bike's right axis (to pitch backward)
            bikeRigidbody.AddTorque(-transform.right * wheelieTorque * exponentialThrottle * decayedMultBySpeed, ForceMode.Acceleration);
        }
    }

    bool IsWithinRotationRange()
    {
        float xRot = bikeRigidbody.transform.rotation.eulerAngles.x;
        // Normalize to -180 to 180 range
        if (xRot > 180f)
            xRot -= 360f;
        // Now check: between -50 and 10 degrees
        return xRot > -40f && xRot < 10f;
    }
}