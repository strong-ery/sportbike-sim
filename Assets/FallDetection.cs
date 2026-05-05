using NWH.VehiclePhysics;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using NWH.WheelController3D;

public class FallDetection : MonoBehaviour
{
    public Rigidbody bikeRb;
    public float velocityChangeThreshold = 50f; // meters per second^2.
    public float angularVelocityThreshold = 30f; // radians per second.

    private Vector3 lastVelocity;
    private Vector3 lastAngularVelocity;

    public MotorcycleBalanceController balanceController;
    public JumpStart jumpStarter;
    public SportbikeDownforce downforceController;
    public SportbikeController bikeController;

    private bool hasFallen = false;

    void Start()
    {
        if (bikeRb == null)
        {
            bikeRb = GetComponent<Rigidbody>();
        }

        if (bikeRb != null)
        {
            lastVelocity = bikeRb.linearVelocity;
            lastAngularVelocity = bikeRb.angularVelocity;
        }
    }

    void FixedUpdate()
    {
        if (hasFallen || bikeRb == null) return;

        // Calculate velocity change (acceleration)
        Vector3 velocityChange = (bikeRb.linearVelocity - lastVelocity) / Time.fixedDeltaTime;
        float velocityChangeMagnitude = velocityChange.magnitude;

        // Check angular velocity
        float angularVelocityMagnitude = bikeRb.angularVelocity.magnitude;

        // Check if thresholds are exceeded
        if (velocityChangeMagnitude > velocityChangeThreshold ||
            angularVelocityMagnitude > angularVelocityThreshold)
        {
            TriggerFall();
        }

        // Update last values
        lastVelocity = bikeRb.linearVelocity;
        lastAngularVelocity = bikeRb.angularVelocity;
    }

    private void TriggerFall()
    {
        if (hasFallen) return;

        hasFallen = true;

        // Disable bike components
        if (balanceController != null)
            balanceController.enabled = false;

        if (jumpStarter != null)
            jumpStarter.enabled = false;

        if (downforceController != null)
            downforceController.enabled = false;

        if (bikeController != null)
            bikeController.enabled = false;

        // Start reset coroutine
        StartCoroutine(ResetAfterDelay(5f));
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}