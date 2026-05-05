using UnityEngine;

public class PlayerTriggerCheckpointCheck : MonoBehaviour
{
    public bool hasBeenPassed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            hasBeenPassed = true;
        }
    }
}

