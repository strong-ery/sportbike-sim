using UnityEngine;
using System;

namespace NWH.WheelController3D
{
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralEngine : MonoBehaviour
    {
        [Header("Live Inputs (Feed from CarController)")]
        [Range(0, 18000)] public float rpm = 1000f;
        [Range(0, 1)] public float load = 0f;
        [Range(0, 1)] public float throttle = 0f;
        public bool ignitionCut = false;
        public bool exhaustPops = false; // NEW: Trigger downshift/overrun pops
        [Range(0, 1)] public float popFrequency = 0.5f; // NEW: How often pops occur (0 = rare, 1 = constant)
        [Range(0, 5)] public float popIntensity = 3.0f; // NEW: Pop volume/intensity

        [Header("Tuning Parameters")]
        public float bodyGain = 3.7f;
        public float noiseGain = 0.55f;
        public int headerLength = 370; // Samples
        public float midpipeLength = 4.7f;
        public float wallDamping = 0.05f; // Slight damping for stability
        public float scavenging = 0.28f;
        public float mufflerReflection = 0.21f;
        public float exhaustVolume = 2.0f;
        public float intakeVolume = 0.13f;
        public float gearWhineVolume = 0.2f;
        public float intakeFilter = 0.04f;

        // --- THREAD-SAFE CACHED VALUES ---
        private volatile float _cachedRpm;
        private volatile float _cachedLoad;
        private volatile float _cachedThrottle;
        private volatile bool _cachedIgnitionCut;
        private volatile bool _cachedExhaustPops; // NEW: Thread-safe cache
        private volatile float _cachedPopFrequency;
        private volatile float _cachedPopIntensity;

        // --- DSP INTERNAL STATE ---
        private float sampleRate;
        private double crankAngle = 0;
        private int[] seeds = { 1234, 5678, 9012, 3456 };
        private float[] fireOrder = { 0, 180, 360, 540 };
        private Waveguide[] pipes;
        private Waveguide midPipe;
        private DampingFilter intakeDamping = new DampingFilter();
        private double gearWhinePhase = 0;

        // Waveguide Class (Ported)
        class Waveguide
        {
            public float[] bufferRight;
            public float[] bufferLeft;
            public int ptr = 0;
            public int len;
            public int size;
            public DampingFilter damper = new DampingFilter();

            public Waveguide(int length) { UpdateLength(length); }

            public void UpdateLength(int length)
            {
                len = length;
                size = len * 2;
                bufferRight = new float[size];
                bufferLeft = new float[size];
                ptr = 0;
            }

            public void Input(float sample)
            {
                bufferRight[ptr] = sample;
            }

            public float Output()
            {
                int idx = (ptr - len + size) % size;
                return bufferRight[idx];
            }

            public void ReflectInput(float sample, float damping)
            {
                int idx = (ptr - len + size) % size;
                bufferLeft[idx] = damper.Process(sample, damping);
            }

            public float ReflectOutput()
            {
                return bufferLeft[ptr];
            }

            public void Step()
            {
                ptr = (ptr + 1) % size;
            }
        }

        class DampingFilter
        {
            public float last = 0;
            public float Process(float input, float amount)
            {
                last = input * (1f - amount) + last * amount;
                return last;
            }
        }

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;

            // Initialize cached values
            _cachedRpm = rpm;
            _cachedLoad = load;
            _cachedThrottle = throttle;
            _cachedIgnitionCut = ignitionCut;
            _cachedExhaustPops = exhaustPops;
            _cachedPopFrequency = popFrequency;
            _cachedPopIntensity = popIntensity;

            // Initialize Pipes
            int BASE = headerLength;
            pipes = new Waveguide[4];
            pipes[0] = new Waveguide(BASE);
            pipes[1] = new Waveguide(BASE + 15);
            pipes[2] = new Waveguide(BASE - 10);
            pipes[3] = new Waveguide(BASE + 5);

            midPipe = new Waveguide((int)(BASE * midpipeLength));
        }

        void Update()
        {
            // Cache values on main thread for audio thread to read safely
            _cachedRpm = rpm;
            _cachedLoad = load;
            _cachedThrottle = throttle;
            _cachedIgnitionCut = ignitionCut;
            _cachedExhaustPops = exhaustPops;
            _cachedPopFrequency = popFrequency;
            _cachedPopIntensity = popIntensity;
        }

        // Xorshift (Fast Random)
        int FastRandom(ref int seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }

        // The Magic Unity Audio Hook
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (pipes == null) return; // Not initialized yet

            // Read thread-safe cached values
            float currentRpm = _cachedRpm;
            float currentLoad = _cachedLoad;
            float currentThrottle = _cachedThrottle;
            bool currentIgnitionCut = _cachedIgnitionCut;
            bool currentExhaustPops = _cachedExhaustPops;
            float currentPopFrequency = _cachedPopFrequency;
            float currentPopIntensity = _cachedPopIntensity;

            // Per-sample loop
            for (int i = 0; i < data.Length; i += channels)
            {
                // 1. Calculate Crank Angle Step
                // safeRpm clamped to prevent divide by zero or explosion
                float safeRpm = Mathf.Clamp(currentRpm, 0, 18000);
                double dps = (safeRpm / 60.0 * 360.0) / sampleRate;

                crankAngle += dps;
                if (crankAngle >= 720.0) crankAngle -= 720.0;

                // 2. Combustion Logic
                float totalFlow = 0;

                for (int c = 0; c < 4; c++)
                {
                    double angle = crankAngle - fireOrder[c];
                    if (angle < 0) angle += 720;

                    float inputPressure = 0;

                    // NEW: Check for exhaust pops OR ignition cut
                    if (currentExhaustPops || currentIgnitionCut)
                    {
                        // Random pops/bangs - frequency controlled by popFrequency
                        seeds[3] = FastRandom(ref seeds[3]);
                        float randomValue = (seeds[3] / 2147483647.0f + 1.0f) / 2.0f; // Convert to 0-1 range

                        // Pop threshold: lower frequency = higher threshold (fewer pops)
                        float popThreshold = 0.99f - (currentPopFrequency * 0.015f);

                        if (randomValue > popThreshold)
                        {
                            seeds[0] = FastRandom(ref seeds[0]);
                            inputPressure = (seeds[0] / 2147483647.0f) * currentPopIntensity;
                        }
                    }
                    else if (!currentIgnitionCut)
                    {
                        // Normal combustion
                        if (angle < 180)
                        {
                            float phase = (float)(angle / 180.0);

                            // Piston math
                            float theta = phase * Mathf.PI;
                            float pistonPos = (Mathf.Cos(theta) + 0.25f * Mathf.Cos(2 * theta));
                            float pressure = Mathf.Max(0, (pistonPos + 1) / 2);
                            float thud = Mathf.Pow(pressure, 3);

                            float pop = 0;
                            if (phase < 0.3f)
                            {
                                seeds[c] = FastRandom(ref seeds[c]);
                                // Normalize int to -1.0 to 1.0 range approx
                                float rnd = (seeds[c] / 2147483647.0f);
                                pop = rnd * (0.3f - phase);
                            }

                            inputPressure = (thud * (0.4f + currentLoad * bodyGain)) + (pop * (0.2f + currentLoad * noiseGain));
                        }
                    }

                    Waveguide pipe = pipes[c];
                    float backWave = pipe.ReflectOutput();

                    // Input into pipe
                    pipe.Input(inputPressure - (backWave * scavenging));
                    pipe.Step();

                    totalFlow += pipe.Output();
                }

                // 3. Collector & Midpipe
                float incomingSum = totalFlow + midPipe.ReflectOutput();
                float junctionPressure = (2 * incomingSum) / 5;

                for (int c = 0; c < 4; c++)
                {
                    float scatter = junctionPressure - pipes[c].Output();
                    pipes[c].ReflectInput(scatter, wallDamping);
                }

                float toMid = junctionPressure - midPipe.ReflectOutput();
                midPipe.Input((float)System.Math.Tanh(toMid * 1.5));
                midPipe.Step();

                float midOut = midPipe.Output();
                midPipe.ReflectInput(midOut * -mufflerReflection, wallDamping);

                // 4. Noise & Intake layers
                seeds[2] = FastRandom(ref seeds[2]);
                float mechNoise = (seeds[2] / 2147483647.0f) * 0.1f;
                float tappet = mechNoise * (Mathf.Sin((float)crankAngle * Mathf.Deg2Rad * 2) > 0.8f ? 1 : 0.1f);

                // Intake
                seeds[1] = FastRandom(ref seeds[1]);
                float airRaw = seeds[1] / 2147483647.0f;
                float airFiltered = intakeDamping.Process(airRaw, intakeFilter);
                float intakeRoar = airFiltered * currentThrottle * intakeVolume * (0.5f + currentLoad);

                // Gear Whine
                gearWhinePhase += (safeRpm / 60.0 * 1.9 * 360.0) / sampleRate;
                if (gearWhinePhase > 360) gearWhinePhase -= 360;
                float whine = Mathf.Sin((float)gearWhinePhase * Mathf.Deg2Rad) * gearWhineVolume * (safeRpm / 16000f);

                // 5. Final Mix
                float finalSignal = (midOut * exhaustVolume) + intakeRoar + (tappet * 0.15f) + whine;
                finalSignal = (float)System.Math.Tanh(finalSignal); // Soft clip

                // 6. Write to Unity Buffer
                for (int j = 0; j < channels; j++)
                {
                    data[i + j] = finalSignal * 0.5f; // Master volume
                }
            }
        }
    }
}