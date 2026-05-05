using UnityEngine;
using NWH.WheelController3D;

namespace NWH.VehiclePhysics
{
    /// <summary>
    /// Applies aerodynamic downforce to a sportbike based on speed.
    /// Works with WheelController3D system for realistic high-speed handling.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SportbikeDownforce : MonoBehaviour
    {
        [Header("Downforce Settings")]
        [Tooltip("Maximum downforce in Newtons at top speed")]
        [Range(0f, 5000f)]
        public float maxDownforce = 1500f;

        [Tooltip("Speed (m/s) at which maximum downforce is achieved")]
        [Range(10f, 150f)]
        public float maxDownforceSpeed = 80f; // ~180 mph

        [Tooltip("Downforce curve - allows fine-tuning of force application")]
        public AnimationCurve downforceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Distribution")]
        [Tooltip("Percentage of downforce applied to front wheel (0-1)")]
        [Range(0f, 1f)]
        public float frontBias = 0.45f;

        [Header("Wheel References")]
        [Tooltip("Front wheel controller")]
        public WheelController frontWheel;

        [Tooltip("Rear wheel controller")]
        public WheelController rearWheel;

        [Header("Advanced Settings")]
        [Tooltip("Apply downforce only when wheels are grounded")]
        public bool requireGroundContact = true;

        [Tooltip("Drag coefficient increase with speed")]
        [Range(0f, 2f)]
        public float dragCoefficient = 0.3f;

        [Tooltip("Center of pressure offset from center of mass (local space)")]
        public Vector3 centerOfPressure = new Vector3(0f, 0.5f, 0f);

        private Rigidbody _rigidbody;
        private float _currentSpeed;
        private float _currentDownforce;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            // Auto-find wheels if not assigned
            if (frontWheel == null || rearWheel == null)
            {
                WheelController[] wheels = GetComponentsInChildren<WheelController>();
                if (wheels.Length >= 2)
                {
                    // Assume front wheel is forward-most
                    float maxZ = float.MinValue;
                    float minZ = float.MaxValue;

                    foreach (var wheel in wheels)
                    {
                        float localZ = transform.InverseTransformPoint(wheel.transform.position).z;
                        if (localZ > maxZ)
                        {
                            maxZ = localZ;
                            frontWheel = wheel;
                        }
                        if (localZ < minZ)
                        {
                            minZ = localZ;
                            rearWheel = wheel;
                        }
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (_rigidbody == null || (frontWheel == null && rearWheel == null))
                return;

            // Calculate current speed
            _currentSpeed = _rigidbody.linearVelocity.magnitude;

            // Check if downforce should be applied
            if (requireGroundContact)
            {
                bool hasGroundContact = false;
                if (frontWheel != null && frontWheel.IsGrounded) hasGroundContact = true;
                if (rearWheel != null && rearWheel.IsGrounded) hasGroundContact = true;

                if (!hasGroundContact) return;
            }

            // Calculate downforce based on speed
            float speedRatio = Mathf.Clamp01(_currentSpeed / maxDownforceSpeed);
            float curveMultiplier = downforceCurve.Evaluate(speedRatio);
            _currentDownforce = maxDownforce * curveMultiplier;

            // Apply downforce to wheels and body
            ApplyDownforceToWheels(_currentDownforce);
            ApplyAerodynamicDrag();
        }

        private void ApplyDownforceToWheels(float totalDownforce)
        {
            // Calculate force distribution
            float frontForce = totalDownforce * frontBias;
            float rearForce = totalDownforce * (1f - frontBias);

            // Apply to front wheel
            if (frontWheel != null)
            {
                Vector3 frontDownforceVector = -frontWheel.transform.up * frontForce;
                _rigidbody.AddForceAtPosition(frontDownforceVector, frontWheel.transform.position, ForceMode.Force);
            }

            // Apply to rear wheel
            if (rearWheel != null)
            {
                Vector3 rearDownforceVector = -rearWheel.transform.up * rearForce;
                _rigidbody.AddForceAtPosition(rearDownforceVector, rearWheel.transform.position, ForceMode.Force);
            }
        }

        private void ApplyAerodynamicDrag()
        {
            if (dragCoefficient <= 0f) return;

            // Calculate drag force (proportional to velocity squared)
            float speedSquared = _currentSpeed * _currentSpeed;
            float dragMagnitude = dragCoefficient * speedSquared;

            // Apply drag opposite to velocity direction
            Vector3 dragForce = -_rigidbody.linearVelocity.normalized * dragMagnitude;

            // Apply at center of pressure for realistic pitching moments
            Vector3 applicationPoint = _rigidbody.worldCenterOfMass + transform.TransformVector(centerOfPressure);
            _rigidbody.AddForceAtPosition(dragForce, applicationPoint, ForceMode.Force);
        }

        /// <summary>
        /// Gets the current downforce being applied in Newtons
        /// </summary>
        public float GetCurrentDownforce()
        {
            return _currentDownforce;
        }

        /// <summary>
        /// Gets the current speed in m/s
        /// </summary>
        public float GetCurrentSpeed()
        {
            return _currentSpeed;
        }

        /// <summary>
        /// Gets the current speed in km/h
        /// </summary>
        public float GetCurrentSpeedKMH()
        {
            return _currentSpeed * 3.6f;
        }

        /// <summary>
        /// Gets the current speed in mph
        /// </summary>
        public float GetCurrentSpeedMPH()
        {
            return _currentSpeed * 2.23694f;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (_rigidbody == null) return;

            // Draw downforce visualization
            if (frontWheel != null)
            {
                float frontForce = _currentDownforce * frontBias;
                Vector3 frontForceVector = -frontWheel.transform.up * (frontForce * 0.001f); // Scale for visibility
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(frontWheel.transform.position, frontForceVector);
                Gizmos.DrawWireSphere(frontWheel.transform.position + frontForceVector, 0.05f);
            }

            if (rearWheel != null)
            {
                float rearForce = _currentDownforce * (1f - frontBias);
                Vector3 rearForceVector = -rearWheel.transform.up * (rearForce * 0.001f); // Scale for visibility
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(rearWheel.transform.position, rearForceVector);
                Gizmos.DrawWireSphere(rearWheel.transform.position + rearForceVector, 0.05f);
            }

            // Draw center of pressure
            Vector3 cop = _rigidbody.worldCenterOfMass + transform.TransformVector(centerOfPressure);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cop, 0.1f);

            // Draw velocity vector
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_rigidbody.worldCenterOfMass, _rigidbody.linearVelocity * 0.1f);
        }
    }
}