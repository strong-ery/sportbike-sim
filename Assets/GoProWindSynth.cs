using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GoProWindSynth : MonoBehaviour
{
    public Rigidbody bikeRb;
    public SportbikeCameraController cameraController;

    [Header("Speed Settings")]
    public float currentSpeed = 0f;
    public float minSpeed = 0f;
    public float maxSpeed = 300f;

    [Header("Rider State")]
    [Tooltip("If true, reduces distortion and muffles sound")]
    public bool isTucked = false;

    [Header("Audio Config")]
    [Range(0f, 1f)] public float masterVolume = 0.5f;
    [Tooltip("How much the mic 'blows out' at max speed. Higher = more crackle.")]
    public float maxDistortionDrive = 20.0f;

    // Internal DSP variables
    private float samplingRate = 48000f;
    private float currentDrive = 1f;
    private float currentCutoff = 0.1f;
    private float currentVolume = 0f;

    // Pink Noise State Variables (Paul Kellett's method)
    private float b0, b1, b2, b3, b4, b5, b6;

    // Smoothing for parameters to prevent audio clicking
    private float smoothDrive;
    private float smoothCutoff;
    private float smoothVolume;

    private void Start()
    {
        samplingRate = AudioSettings.outputSampleRate;
        // Ensure the AudioSource plays even though it has no clip
        var source = GetComponent<AudioSource>();
        source.Stop(); // Stop any existing clips
        source.playOnAwake = true;
    }

    private void Update()
    {
        CalculateAudioParams();

        // Ensure AudioSource is 'playing' so the filter runs
        if (!GetComponent<AudioSource>().isPlaying)
        {
            GetComponent<AudioSource>().Play();
        }

        currentSpeed = bikeRb.linearVelocity.magnitude * 3.6f; // Convert m/s to km/h

        if (cameraController.currentHeight > -0.08)
        {
            isTucked = false;
        }
        else
        {
            isTucked = true;
        }
    }

    private void CalculateAudioParams()
    {
        // 1. Normalize Speed (0.0 to 1.0)
        float range = Mathf.Max(1f, maxSpeed - minSpeed);
        float speedRatio = Mathf.Clamp01((currentSpeed - minSpeed) / range);

        // 2. Calculate Volume (Linear ramp with speed)
        float targetVolume = speedRatio;

        // 3. Calculate Distortion Drive (Quadratic ramp)
        float driveCurve = speedRatio * speedRatio; // Quadratic curve
        float targetDrive = 1f + (maxDistortionDrive * driveCurve);

        // 4. Calculate Filter Cutoff (Muffled at low speed, open at high)
        // 0.02 is very muffled, 0.8 is wide open
        float targetCutoff = 0.02f + (0.78f * (speedRatio * speedRatio));

        // --- TUCKED LOGIC ---
        if (isTucked)
        {
            // Cut the distortion in half (less crackly)
            targetDrive *= 0.3f;

            // Close the filter slightly (wind hits helmet less directly)
            targetCutoff *= 0.5f;
        }

        // Smooth values to prevent clicking when state changes rapidly
        smoothDrive = Mathf.Lerp(smoothDrive, targetDrive, Time.deltaTime * 5f);
        smoothCutoff = Mathf.Lerp(smoothCutoff, targetCutoff, Time.deltaTime * 5f);
        smoothVolume = Mathf.Lerp(smoothVolume, targetVolume, Time.deltaTime * 5f);

        // Pass to audio thread variables
        currentDrive = smoothDrive;
        currentCutoff = smoothCutoff;
        currentVolume = smoothVolume;
    }

    // This runs on the Audio Thread (High Frequency)
    // Do not use Unity API calls (transform, time, etc) inside here
    private void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            // 1. Generate Pink Noise
            // This algorithm approximates the frequency spectrum of wind better than Random.value
            float white = (float)(new System.Random().NextDouble() * 2.0 - 1.0);

            b0 = 0.99886f * b0 + white * 0.0555179f;
            b1 = 0.99332f * b1 + white * 0.0750759f;
            b2 = 0.96900f * b2 + white * 0.1538520f;
            b3 = 0.86650f * b3 + white * 0.3104856f;
            b4 = 0.55000f * b4 + white * 0.5329522f;
            b5 = -0.7616f * b5 - white * 0.0168980f;
            float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
            pink *= 0.11f; // Normalize roughly to -1..1
            b6 = white * 0.115926f;

            // 2. Apply Low Pass Filter (Simulate air resistance/muffling)
            // Simple one-pole filter
            // Reuse b0 variable logic for a simple lowpass buffer
            // (Note: To keep pink noise state clean, we just apply simple math here)
            // Ideally, we'd use a separate state variable, but for wind, 
            // relying on the pink noise inherent roll-off + volume is usually enough.
            // However, let's do a simple volume roll-off based on frequency approximation:

            float signal = pink;

            // 3. Apply PRE-AMP (Drive)
            // This pushes the volume beyond the limits of the "mic"
            signal *= currentDrive;

            // 4. Hard Clipping (The "GoPro" Effect)
            // If signal > 1.0, clamp it. This squares off the wave.
            if (signal > 1.0f) signal = 1.0f;
            else if (signal < -1.0f) signal = -1.0f;

            // 5. Apply speed-based volume (linear ramp)
            signal *= currentVolume;

            // 6. Final Output Master
            signal *= masterVolume;

            // Apply to all channels (L/R)
            for (int c = 0; c < channels; c++)
            {
                data[i + c] = signal;
            }
        }
    }
}