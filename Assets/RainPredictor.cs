using UnityEngine;

public class RainPredictor : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Drag your Motorcycle's Rigidbody here")]
    public Rigidbody bikeRigidbody;

    [Header("Prediction Settings")]
    [Tooltip("Time in seconds for a raindrop to hit the ground")]
    public float rainFallTime = 1.29f;

    [Tooltip("Height above the bike to maintain at all times")]
    public float heightAboveBike = 17.34f;

    [Header("Smoothing")]
    public float smoothSpeed = 10f;

    private void LateUpdate()
    {
        if (bikeRigidbody == null) return;

        // 1. Get Velocity
        // (Use bikeRigidbody.velocity if on Unity 2022 or older)
        Vector3 bikeVelocity = bikeRigidbody.linearVelocity;

        // 2. Calculate Horizontal Prediction Only
        // We do NOT predict Y, or the emitter might fly away during jumps
        float predictedX = bikeVelocity.x * rainFallTime;
        float predictedZ = bikeVelocity.z * rainFallTime;

        // 3. Determine Target Position
        // Start at bike's CURRENT position
        Vector3 targetPosition = bikeRigidbody.position;

        // Add the horizontal prediction to place the emitter ahead of the bike
        targetPosition.x += predictedX;
        targetPosition.z += predictedZ;

        // Add the strict vertical offset relative to the bike's current height
        targetPosition.y += heightAboveBike;

        // 4. Move the Rain Emitter
        // Lerp ensures the emitter doesn't snap instantly if physics glitches
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
    }
}