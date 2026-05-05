using NWH.WheelController3D;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class RPMClip
{
    public string name;
    public AudioClip clip;
    public float recordedRPM;
    [Range(0f, 2f)] public float volume = 1f;
}

public class BikeAudioSystem : MonoBehaviour
{
    public SportbikeController bike;
    public Rigidbody bikeRb;
    public InputActionReference throttleAction;

    [Header("Main Engine Sources (2-layer FMOD style)")]
    public AudioSource srcA;
    public AudioSource srcB;

    [Header("Engine Clips")]
    public List<RPMClip> engineClips = new();

    [Header("RPM")]
    public float currentRPM;
    public float minRPM = 1500f;
    public float maxRPM = 16500f;

    [Header("Pitch")]
    public float maxPitchShift = 1.2f;
    public float pitchSmoothSpeed = 20f;

    [Header("Blend / Crossfade")]
    [Range(0.05f, 0.3f)] public float crossfadeSpeed = 0.12f;

    // internal
    private RPMClip lower, upper;
    private float smoothPitchA = 1f, smoothPitchB = 1f;
    private float targetVolA, targetVolB;


    // =====================================================================
    // ----- Procedural Layers -----
    // =====================================================================

    [Header("Procedural – Gear Whine")]
    public AudioSource whineSrc;
    public bool whineEnabled = true;
    [Range(0f, 1f)] public float whineVolume = 0.12f;
    [Range(80f, 600f)] public float whineBaseFrequency = 260f;
    [Range(0f, 0.3f)] public float whineRPMFactor = 0.14f;
    [Range(0.5f, 8f)] public float whinePitchSmoothing = 3.5f;
    [Range(0.5f, 8f)] public float whineVolumeSmoothing = 4f;

    float whinePitchSmooth = 1f;
    float whineVolumeSmooth = 0f;
    float whinePhase;


    [Header("Procedural – Intake Noise")]
    public AudioSource intakeSrc;
    public bool intakeEnabled = true;
    [Range(0f, 0.7f)] public float intakeMaxVolume = 0.35f;
    public float intakeSmoothing = 3.5f;
    float intakeVolSmooth = 0f;


    [Header("Procedural – Chain Noise")]
    public AudioSource chainSrc;
    public bool chainEnabled = true;
    [Range(0f, 1f)] public float chainMaxVolume = 0.25f;
    public float chainSmoothing = 2f;
    public float chainPitchBase = 0.6f;
    public float chainPitchFactor = 0.02f;
    float chainVolSmooth = 0f;


    // =====================================================================
    // INITIALIZATION
    // =====================================================================

    void Start()
    {
        if (engineClips.Count == 0)
        {
            Debug.LogError("No engine clips assigned.");
            enabled = false;
            return;
        }

        engineClips.Sort((a, b) => a.recordedRPM.CompareTo(b.recordedRPM));

        SetupSource(srcA);
        SetupSource(srcB);
        SetupSource(whineSrc);
        SetupSource(intakeSrc);
        SetupSource(chainSrc);

        UpdateClipPair(minRPM);

        srcA.clip = lower.clip;
        srcB.clip = upper.clip;
        srcA.Play();
        srcB.Play();
    }

    void SetupSource(AudioSource a)
    {
        if (a == null) return;
        a.loop = true;
        a.playOnAwake = false;
        a.volume = 0f;
        a.pitch = 1f;
    }


    // =====================================================================
    // UPDATE
    // =====================================================================

    void Update()
    {
        if (bike != null)
            currentRPM = bike.CurrentRPM;

        UpdateClipPair(currentRPM);
        UpdateBlendAndPitch(currentRPM);
        UpdateProcedural(currentRPM);
    }


    // =====================================================================
    // ENGINE CLIP PAIRING
    // =====================================================================

    void UpdateClipPair(float rpm)
    {
        RPMClip newLow = null, newHigh = null;

        foreach (var c in engineClips)
        {
            if (c.recordedRPM <= rpm) newLow = c;
            if (c.recordedRPM >= rpm && newHigh == null) newHigh = c;
        }

        if (newLow == null) newLow = engineClips[0];
        if (newHigh == null) newHigh = engineClips[^1];

        if (lower != newLow)
        {
            lower = newLow;
            if (srcA.clip != lower.clip)
            {
                srcA.clip = lower.clip;
                srcA.Play();
            }
        }

        if (upper != newHigh)
        {
            upper = newHigh;
            if (srcB.clip != upper.clip)
            {
                srcB.clip = upper.clip;
                srcB.Play();
            }
        }
    }


    // =====================================================================
    // ENGINE BLEND + PITCH
    // =====================================================================

    void UpdateBlendAndPitch(float rpm)
    {
        if (lower == null || upper == null) return;

        if (lower == upper)
        {
            targetVolA = lower.volume;
            targetVolB = 0f;

            float p = Mathf.Clamp(rpm / lower.recordedRPM, 0.5f, maxPitchShift);
            smoothPitchA = Mathf.Lerp(smoothPitchA, p, Time.deltaTime * pitchSmoothSpeed);

            srcA.pitch = smoothPitchA;
            srcB.pitch = smoothPitchA;
        }
        else
        {
            float blend = Mathf.InverseLerp(lower.recordedRPM, upper.recordedRPM, rpm);
            blend = Mathf.SmoothStep(0f, 1f, blend);

            targetVolA = (1f - blend) * lower.volume;
            targetVolB = blend * upper.volume;

            float pLow = Mathf.Clamp(rpm / lower.recordedRPM, 0.5f, maxPitchShift);
            float pHigh = Mathf.Clamp(rpm / upper.recordedRPM, 0.5f, maxPitchShift);

            smoothPitchA = Mathf.Lerp(smoothPitchA, pLow, Time.deltaTime * pitchSmoothSpeed);
            smoothPitchB = Mathf.Lerp(smoothPitchB, pHigh, Time.deltaTime * pitchSmoothSpeed);

            srcA.pitch = smoothPitchA;
            srcB.pitch = smoothPitchB;
        }

        float cf = Time.deltaTime * (1f / crossfadeSpeed);
        srcA.volume = Mathf.Lerp(srcA.volume, targetVolA, cf);
        srcB.volume = Mathf.Lerp(srcB.volume, targetVolB, cf);
    }


    // =====================================================================
    // PROCEDURAL AUDIO
    // =====================================================================

    void UpdateProcedural(float rpm)
    {
        UpdateGearWhine(rpm);
        UpdateIntake(rpm);
        UpdateChain(rpm);
    }



    // ---------------------------------------------------------------------
    // GEAR WHINE
    // ---------------------------------------------------------------------

    void UpdateGearWhine(float rpm)
    {
        if (!whineEnabled || whineSrc == null)
        {
            if (whineSrc != null) whineSrc.volume = 0f;
            return;
        }

        float targetPitch = 1f + (rpm * whineRPMFactor) / 10000f;
        whinePitchSmooth = Mathf.Lerp(whinePitchSmooth, targetPitch, Time.deltaTime * whinePitchSmoothing);

        float rpmNorm = Mathf.Clamp01(rpm / maxRPM);
        float targetVol = rpmNorm * whineVolume;

        whineVolumeSmooth = Mathf.Lerp(whineVolumeSmooth, targetVol, Time.deltaTime * whineVolumeSmoothing);

        whineSrc.pitch = whinePitchSmooth;
        whineSrc.volume = whineVolumeSmooth;
    }


    // Gear whine DSP callback
    private void OnAudioFilterRead(float[] data, int channels)
    {
        // Cache procedural values locally (they MUST be simple fields updated in Update())
        if (!whineEnabled || whineVolumeSmooth <= 0.0001f)
            return;

        float freq = whineBaseFrequency * whinePitchSmooth;
        float step = freq / 44100f * Mathf.PI * 2f;
        float volume = whineVolumeSmooth * 0.18f;   // 0.18f = mild gear whine gain

        float phase = whinePhase; // local copy (thread safe)

        for (int i = 0; i < data.Length; i += channels)
        {
            // main gear tone
            float s = Mathf.Sin(phase);

            // 2nd harmonic
            s += Mathf.Sin(phase * 2f) * 0.25f;

            // apply volume
            float sample = s * volume;

            data[i] += sample;
            if (channels == 2)
                data[i + 1] += sample;

            // increment phase
            phase += step;
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
        }

        // write phase back to field (thread-safe)
        whinePhase = phase;
    }




    // ---------------------------------------------------------------------
    // INTAKE NOISE
    // ---------------------------------------------------------------------

    void UpdateIntake(float rpm)
    {
        if (!intakeEnabled || intakeSrc == null)
        {
            if (intakeSrc != null) intakeSrc.volume = 0f;
            return;
        }

        float throttle = throttleAction != null ? throttleAction.action.ReadValue<float>() : 0f;
        throttle = Mathf.Clamp01(throttle);

        float target = throttle * intakeMaxVolume;

        intakeVolSmooth = Mathf.Lerp(
            intakeVolSmooth,
            target,
            Time.deltaTime * intakeSmoothing
        );

        intakeSrc.volume = intakeVolSmooth;
        intakeSrc.pitch = 0.9f + throttle * 0.4f;
    }



    // ---------------------------------------------------------------------
    // CHAIN NOISE
    // ---------------------------------------------------------------------

    void UpdateChain(float rpm)
    {
        if (!chainEnabled || chainSrc == null)
        {
            if (chainSrc != null) chainSrc.volume = 0f;
            return;
        }

        float speed = bikeRb != null ? bikeRb.linearVelocity.magnitude : 0f;  // m/s

        float targetVol = Mathf.Clamp01(speed / 60f) * chainMaxVolume;

        chainVolSmooth = Mathf.Lerp(
            chainVolSmooth,
            targetVol,
            Time.deltaTime * chainSmoothing
        );

        chainSrc.volume = chainVolSmooth;
        chainSrc.pitch = chainPitchBase + speed * chainPitchFactor;
    }
}
