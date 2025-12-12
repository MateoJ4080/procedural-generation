using UnityEngine;

public class FindMissingScripts : MonoBehaviour
{
    void Start()
    {
        foreach (var obj in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            var components = obj.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    Debug.LogError($"Missing script at index {i} on: {obj.name}", obj);
            }
        }
    }
}