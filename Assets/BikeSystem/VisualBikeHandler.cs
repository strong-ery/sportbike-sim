using NWH.WheelController3D;
using UnityEngine;
using UnityEngine.InputSystem;

public class VisualBikeHandler : MonoBehaviour
{
    public InputActionReference toggleLightsAction;

    public Transform steeringRack;
    public Transform rearSuspensionAssembly;
    public WheelController wheelController;
    public WheelController rearWheelController;

    public float baseRearSuspensionAngle = 55f;

    public GameObject lowBeamObject;
    public GameObject highBeamObject;

    public bool lowBeamOn = false;
    public bool highBeamOn = false;

    public void Update()
    {
        bool toggleLights = toggleLightsAction.action.WasPerformedThisFrame();

        if (toggleLights)
        {
            if (!lowBeamOn && !highBeamOn)
            {
                lowBeamObject.SetActive(true);
                highBeamObject.SetActive(false);
                lowBeamOn = true;
            }

            else if (lowBeamOn && !highBeamOn)
            {
                lowBeamObject.SetActive(true);
                highBeamObject.SetActive(true);
                lowBeamOn = true;
                highBeamOn = true;
            }
            else if (lowBeamOn && highBeamOn)
            {
                lowBeamObject.SetActive(false);
                highBeamObject.SetActive(false);
                lowBeamOn = false;
                highBeamOn = false;
            }
        }
    }

    public void LateUpdate()
    {
        float currentSteerAngle = wheelController.SteerAngle;

        float compressionPercent = rearWheelController.SpringCompression;

        if (steeringRack != null)
        {
            steeringRack.localRotation = Quaternion.Euler(-113f, (currentSteerAngle * 0.67f), 0f);
        }

        if (rearSuspensionAssembly != null)
        {
            rearSuspensionAssembly.localRotation = Quaternion.Euler(0f, -90f, baseRearSuspensionAngle * compressionPercent + 94f);
        }
    }
}