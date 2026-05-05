// RainOnLensController.cs
// Simple script to control rain parameters at runtime

using UnityEngine;

public class RainOnLensController : MonoBehaviour
{
    [Header("Rain Settings")]
    [Range(0f, 1f)]
    public float intensity = 0.5f;

    [Range(0f, 2f)]
    public float windStrength = 0.5f;

    [Range(0.5f, 3f)]
    public float dropSize = 1f;

    [Range(0f, 0.1f)]
    public float distortion = 0.02f;

    [Range(0f, 2f)]
    public float speed = 1f;

    [Header("Speed-based Wind")]
    public bool useSpeedForWind = true;
    public float maxSpeed = 100f; // Max bike speed
    private Rigidbody rb;

    private RainOnLensFeature rainFeature;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Find the rain feature in the URP renderer
        // You'll need to manually assign this in the inspector or find it via reflection
    }

    void Update()
    {
        // Calculate wind strength from speed if enabled
        if (useSpeedForWind && rb != null)
        {
            float speed = rb.linearVelocity.magnitude;
            windStrength = Mathf.Clamp01(speed / maxSpeed) * 2f;
        }

        // Update rain feature settings
        // Note: You'll need to access your renderer feature instance
        // This is a simplified example
    }
}