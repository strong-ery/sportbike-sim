using UnityEngine;

namespace NWH.WheelController3D
{
    /// <summary>
    /// Procedural one-shot sounds for sportbike (shifts, limiter, pops)
    /// NO AUDIO CLIPS NEEDED - generates everything from scratch!
    /// </summary>
    [RequireComponent(typeof(SportbikeController))]
    public class ProceduralOneShotSounds : MonoBehaviour
    {
        [Header("Shift Sounds")]
        [Tooltip("Enable procedural shift sounds")]
        public bool enableShiftSounds = true;

        [Range(0f, 1f)]
        public float shiftVolume = 0.7f;

        [Header("Rev Limiter")]
        [Tooltip("Enable procedural rev limiter")]
        public bool enableRevLimiter = true;

        [Range(0f, 1f)]
        public float limiterVolume = 0.8f;

        [Header("Decel Pops")]
        [Tooltip("Enable procedural exhaust pops")]
        public bool enableDecelerationPops = true;

        [Range(0f, 1f)]
        public float popVolume = 0.6f;

        public float popMinRpm = 8000f;
        public float popChance = 0.3f;
        public float popCooldown = 0.2f;

        // Internal
        private SportbikeController bikeController;
        private AudioSource oneShotSource;

        private bool wasShifting;
        private bool wasRevLimiting;
        private bool wasOffThrottle;
        private int previousGear;
        private float lastPopTime;

        private System.Random random;

        private void Awake()
        {
            bikeController = GetComponent<SportbikeController>();
            random = new System.Random();

            // Create dedicated AudioSource for one-shots
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 1f;
            oneShotSource.minDistance = 5f;
            oneShotSource.maxDistance = 80f;

            previousGear = bikeController.CurrentGear;
        }

        private void Update()
        {
            if (bikeController == null) return;

            float rpm = bikeController.CurrentRPM;
            float throttle = Mathf.Clamp01(Input.GetAxis("Vertical"));
            bool isShifting = bikeController.ClutchPosition > 0.1f;
            bool isRevLimiting = rpm >= bikeController.engine.limiterRpm;
            bool isOffThrottle = throttle < 0.05f;

            // === SHIFT SOUNDS ===
            if (enableShiftSounds && isShifting && !wasShifting)
            {
                if (bikeController.CurrentGear > previousGear)
                {
                    PlayShiftUp();
                }
                else if (bikeController.CurrentGear < previousGear)
                {
                    PlayShiftDown();
                }
            }

            // === REV LIMITER ===
            if (enableRevLimiter && isRevLimiting && !wasRevLimiting)
            {
                PlayRevLimiter();
            }

            // === DECEL POPS ===
            if (enableDecelerationPops && isOffThrottle && !wasOffThrottle && rpm > popMinRpm)
            {
                if (Time.time > lastPopTime + popCooldown && Random.value < popChance)
                {
                    PlayDecelerationPop();
                    lastPopTime = Time.time;
                }
            }

            // Update state
            wasShifting = isShifting;
            wasRevLimiting = isRevLimiting;
            wasOffThrottle = isOffThrottle;
            previousGear = bikeController.CurrentGear;
        }

        private void PlayShiftUp()
        {
            // Quick power cut with slight pitch drop
            AudioClip clip = GenerateShiftSound(0.08f, 200f, 150f, 0.3f);
            oneShotSource.PlayOneShot(clip, shiftVolume);
        }

        private void PlayShiftDown()
        {
            // Blip with slight pitch rise
            AudioClip clip = GenerateShiftSound(0.12f, 180f, 220f, 0.25f);
            oneShotSource.PlayOneShot(clip, shiftVolume * 0.8f);
        }

        private void PlayRevLimiter()
        {
            // Sharp burst/cut
            AudioClip clip = GenerateRevLimiterSound(0.06f);
            oneShotSource.PlayOneShot(clip, limiterVolume);
        }

        private void PlayDecelerationPop()
        {
            // Short explosive pop
            AudioClip clip = GeneratePopSound(0.15f);
            oneShotSource.PlayOneShot(clip, popVolume);
        }

        private AudioClip GenerateShiftSound(float duration, float startFreq, float endFreq, float noiseAmount)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);

            AudioClip clip = AudioClip.Create("ShiftSound", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;

                // Frequency sweep
                float freq = Mathf.Lerp(startFreq, endFreq, t);

                // Envelope: quick attack, exponential decay
                float envelope = Mathf.Exp(-t * 8f);

                // Generate tone
                float tone = Mathf.Sin(phase) * envelope;

                // Add noise for "air/mechanical" character
                float noise = ((float)random.NextDouble() * 2f - 1f) * noiseAmount * envelope;

                samples[i] = (tone * 0.7f + noise * 0.3f) * 0.8f;

                phase += freq * 2f * Mathf.PI / sampleRate;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateRevLimiterSound(float duration)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);

            AudioClip clip = AudioClip.Create("LimiterSound", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            // Sharp percussive burst with harmonics
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;

                // Very fast decay (limiter is sharp!)
                float envelope = Mathf.Exp(-t * 30f);

                // Multiple harmonics for aggressive character
                float sample = 0f;
                sample += Mathf.Sin(t * 300f) * 0.4f;  // Low thump
                sample += Mathf.Sin(t * 800f) * 0.3f;  // Mid punch
                sample += Mathf.Sin(t * 1500f) * 0.2f; // High sizzle

                // Add sharp noise burst
                float noise = ((float)random.NextDouble() * 2f - 1f) * 0.4f;

                samples[i] = (sample + noise) * envelope * 0.9f;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GeneratePopSound(float duration)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);

            AudioClip clip = AudioClip.Create("PopSound", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            // Explosive pop with randomization
            float popFreq = 100f + (float)random.NextDouble() * 100f; // Randomize pitch

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;

                // Sharp attack, medium decay
                float envelope = Mathf.Exp(-t * 12f);

                // Low frequency explosion with harmonics
                float sample = 0f;
                sample += Mathf.Sin(t * popFreq * Mathf.PI) * 0.5f;
                sample += Mathf.Sin(t * popFreq * 2f * Mathf.PI) * 0.3f;
                sample += Mathf.Sin(t * popFreq * 3f * Mathf.PI) * 0.2f;

                // Lots of noise for "crackle"
                float noise = ((float)random.NextDouble() * 2f - 1f) * 0.5f;

                // Extra crackle at the start
                if (t < 0.1f)
                {
                    noise *= 2f;
                }

                samples[i] = (sample * 0.4f + noise * 0.6f) * envelope;
            }

            clip.SetData(samples, 0);
            return clip;
        }
    }
}