using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Reset : MonoBehaviour
{
    [SerializeField] private InputActionReference resetAction;

    void Update()
    {
        bool reset = resetAction.action.WasPerformedThisFrame();

        if (reset)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}   