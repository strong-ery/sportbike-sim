using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpeedrunManager : MonoBehaviour
{
    public InputActionReference throttleAction;
    public TextMeshProUGUI timerText; // in format 00:00:00

    private float elapsedTime = 0f;
    private bool isRunning = false;
    private bool runCompleted = false;
    private PlayerTriggerCheckpointCheck[] playerTriggerCheckpointChecks;

    private void Start()
    {
        throttleAction.action.performed += OnThrottlePressed;
        playerTriggerCheckpointChecks = FindObjectsByType<PlayerTriggerCheckpointCheck>(FindObjectsSortMode.None);

        // Initialize timer display
        UpdateTimerDisplay();
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (throttleAction != null)
        {
            throttleAction.action.performed -= OnThrottlePressed;
        }
    }

    private void Update()
    {
        if (isRunning && !runCompleted)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerDisplay();

            // Check if all checkpoints have been passed
            if (AllCheckpointsPassed())
            {
                RunComplete();
            }
        }
    }

    private void OnThrottlePressed(InputAction.CallbackContext context)
    {
        if (!isRunning && !runCompleted)
        {
            // Start the run
            isRunning = true;
            elapsedTime = 0f;
            Debug.Log("Speedrun started!");
        }
    }

    private bool AllCheckpointsPassed()
    {
        if (playerTriggerCheckpointChecks == null || playerTriggerCheckpointChecks.Length == 0)
            return false;

        foreach (var checkpoint in playerTriggerCheckpointChecks)
        {
            if (checkpoint != null && !checkpoint.hasBeenPassed)
            {
                return false;
            }
        }

        return true;
    }

    private void RunComplete()
    {
        isRunning = false;
        runCompleted = true;

        Debug.Log($"Run completed in {FormatTime(elapsedTime)}!");

        // You could add additional completion logic here:
        // - Display completion UI
        // - Save best time
        // - Play completion sound/animation
    }

    private void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            timerText.text = FormatTime(elapsedTime);
        }
    }

    private string FormatTime(float time)
    {
        int hours = Mathf.FloorToInt(time / 3600f);
        int minutes = Mathf.FloorToInt((time % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
    }

    // Optional: Public method to reset the run
    public void ResetRun()
    {
        isRunning = false;
        runCompleted = false;
        elapsedTime = 0f;
        UpdateTimerDisplay();

        // Reset all checkpoints
        foreach (var checkpoint in playerTriggerCheckpointChecks)
        {
            if (checkpoint != null)
            {
                checkpoint.hasBeenPassed = false;
            }
        }
    }
}