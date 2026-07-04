using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FPSDisplay : MonoBehaviour
{
    private TextMeshProUGUI fpsText;
    private float timer;

    void Start()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 0.25f)
        {
            timer = 0f;
            fpsText.text = $"FPS: {(int)(1f / Time.deltaTime)}";
        }
    }
}