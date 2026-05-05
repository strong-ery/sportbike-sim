using NWH.WheelController3D;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class MenuUI : MonoBehaviour
{
    public Canvas menuCanvas;
    public UnityEngine.UI.Slider masterSlider;
    public UnityEngine.UI.Slider engineSlider;
    public UnityEngine.UI.Slider windSlider;
    public UnityEngine.UI.Toggle particleRainToggle;
    public UnityEngine.UI.Toggle lensRainToggle;
    public UnityEngine.UI.Toggle automaticTransToggle;
    public UnityEngine.UI.Toggle hideTimerToggle;
    public AudioSource engineSource;
    public AudioSource windSource;
    private float currentMasterVolume;
    private float currentEngineVolume;
    private float currentWindVolume;
    private bool currentParticleRain;
    private bool currentLensRain;
    private bool currentAutomaticTrans;
    private bool currentHideTimer;
    public SportbikeController bikeController;
    public RawImage lensRainImage;
    public GameObject particleRainEffect;
    public TextMeshProUGUI timerText;
    private bool isMenuVisible = false;
    public InputActionReference toggleMenuAction;

    // PlayerPrefs keys
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string ENGINE_VOLUME_KEY = "EngineVolume";
    private const string WIND_VOLUME_KEY = "WindVolume";
    private const string PARTICLE_RAIN_KEY = "ParticleRain";
    private const string LENS_RAIN_KEY = "LensRain";
    private const string AUTO_TRANS_KEY = "AutomaticTransmission";
    private const string HIDE_TIMER_KEY = "HideTimer";

    void Start()
    {
        LoadSettings();
        ApplySettings();

        Cursor.lockState = isMenuVisible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isMenuVisible;
    }

    private void LoadSettings()
    {
        // Load saved values or use defaults
        currentMasterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, masterSlider.value);
        currentEngineVolume = PlayerPrefs.GetFloat(ENGINE_VOLUME_KEY, engineSlider.value);
        currentWindVolume = PlayerPrefs.GetFloat(WIND_VOLUME_KEY, windSlider.value);
        currentParticleRain = PlayerPrefs.GetInt(PARTICLE_RAIN_KEY, particleRainToggle.isOn ? 1 : 0) == 1;
        currentLensRain = PlayerPrefs.GetInt(LENS_RAIN_KEY, lensRainToggle.isOn ? 1 : 0) == 1;
        currentAutomaticTrans = PlayerPrefs.GetInt(AUTO_TRANS_KEY, automaticTransToggle.isOn ? 1 : 0) == 1;
        currentHideTimer = PlayerPrefs.GetInt(HIDE_TIMER_KEY, hideTimerToggle.isOn ? 1 : 0) == 1;
    }

    private void ApplySettings()
    {
        // Apply loaded values to UI controls
        masterSlider.value = currentMasterVolume;
        engineSlider.value = currentEngineVolume;
        windSlider.value = currentWindVolume;
        particleRainToggle.isOn = currentParticleRain;
        lensRainToggle.isOn = currentLensRain;
        automaticTransToggle.isOn = currentAutomaticTrans;
        hideTimerToggle.isOn = currentHideTimer;

        // Apply settings to game objects
        UpdateVolumes();

        if (particleRainEffect != null)
        {
            particleRainEffect.SetActive(currentParticleRain);
        }

        if (lensRainImage != null)
        {
            lensRainImage.material.SetFloat("_RainEffectiveness", currentLensRain ? 0.281f : 0.0f);
        }

        if (bikeController != null)
        {
            bikeController.transmission.autoShift = currentAutomaticTrans;
        }

        if (timerText != null)
        {
            timerText.enabled = !currentHideTimer;
        }
    }

    private void Update()
    {
        if (toggleMenuAction != null && toggleMenuAction.action.WasPerformedThisFrame())
        {
            isMenuVisible = !isMenuVisible;
            menuCanvas.enabled = isMenuVisible;
            Cursor.lockState = isMenuVisible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isMenuVisible;
        }

        if (!isMenuVisible)
        {
            masterSlider.enabled = false;
            engineSlider.enabled = false;
            windSlider.enabled = false;
        }
        else
        {
            masterSlider.enabled = true;
            engineSlider.enabled = true;
            windSlider.enabled = true;
        }
    }

    public void OnMasterVolumeChanged()
    {
        currentMasterVolume = masterSlider.value;
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, currentMasterVolume);
        PlayerPrefs.Save();
        UpdateVolumes();
    }

    public void OnEngineVolumeChanged()
    {
        currentEngineVolume = engineSlider.value;
        PlayerPrefs.SetFloat(ENGINE_VOLUME_KEY, currentEngineVolume);
        PlayerPrefs.Save();
        UpdateVolumes();
    }

    public void OnWindVolumeChanged()
    {
        currentWindVolume = windSlider.value;
        PlayerPrefs.SetFloat(WIND_VOLUME_KEY, currentWindVolume);
        PlayerPrefs.Save();
        UpdateVolumes();
    }

    public void OnParticleRainToggleChanged()
    {
        currentParticleRain = particleRainToggle.isOn;
        PlayerPrefs.SetInt(PARTICLE_RAIN_KEY, currentParticleRain ? 1 : 0);
        PlayerPrefs.Save();

        if (particleRainEffect != null)
        {
            particleRainEffect.SetActive(currentParticleRain);
        }
    }

    public void OnLensRainToggleChanged()
    {
        currentLensRain = lensRainToggle.isOn;
        PlayerPrefs.SetInt(LENS_RAIN_KEY, currentLensRain ? 1 : 0);
        PlayerPrefs.Save();

        lensRainImage.material.SetFloat("_RainEffectiveness", currentLensRain ? 0.281f : 0.0f);
    }

    public void OnAutomaticTransToggleChanged()
    {
        currentAutomaticTrans = automaticTransToggle.isOn;
        PlayerPrefs.SetInt(AUTO_TRANS_KEY, currentAutomaticTrans ? 1 : 0);
        PlayerPrefs.Save();

        if (bikeController != null)
        {
            bikeController.transmission.autoShift = currentAutomaticTrans;
        }
    }

    public void OnHideTimerToggleChanged()
    {
        currentHideTimer = hideTimerToggle.isOn;
        PlayerPrefs.SetInt(HIDE_TIMER_KEY, currentHideTimer ? 1 : 0);
        PlayerPrefs.Save();

        if (timerText != null)
        {
            timerText.enabled = !currentHideTimer;
        }
    }

    private void UpdateVolumes()
    {
        if (engineSource != null)
        {
            engineSource.volume = currentEngineVolume * currentMasterVolume;
        }
        if (windSource != null)
        {
            windSource.volume = currentWindVolume * currentMasterVolume;
        }
    }

    // Optional: Add a method to reset settings to defaults
    public void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(MASTER_VOLUME_KEY);
        PlayerPrefs.DeleteKey(ENGINE_VOLUME_KEY);
        PlayerPrefs.DeleteKey(WIND_VOLUME_KEY);
        PlayerPrefs.DeleteKey(PARTICLE_RAIN_KEY);
        PlayerPrefs.DeleteKey(LENS_RAIN_KEY);
        PlayerPrefs.DeleteKey(AUTO_TRANS_KEY);
        PlayerPrefs.DeleteKey(HIDE_TIMER_KEY);
        PlayerPrefs.Save();

        LoadSettings();
        ApplySettings();
    }
}