using UnityEngine;
using UnityEngine.InputSystem;

public class ToggleFlyText : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current.fKey.wasPressedThisFrame)
            gameObject.SetActive(false);
    }
}
