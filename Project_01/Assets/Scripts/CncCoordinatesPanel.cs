using TMPro;
using UnityEngine;

// Панель на Canvas: показывает координаты осей, полученные клиентом CncJsonController.
public class CncCoordinatesPanel : MonoBehaviour
{
    public TMP_Text axesText;
    public TMP_Text statusText;

    // Если ответов нет дольше этого времени, данные помечаются как неактуальные.
    public float staleAfter = 3f;

    private float lastSuccess;
    private bool hasData;

    private void Awake()
    {
        axesText.text = "X: - мм\nY: - мм\nZ: - мм";
        statusText.text = "Ожидание ответа сервера";
    }

    public void ShowCoordinates(float xMeters, float yMeters, float zMeters, int channel)
    {
        axesText.text =
            $"X: {xMeters * 1000f:F3} мм\n" +
            $"Y: {yMeters * 1000f:F3} мм\n" +
            $"Z: {zMeters * 1000f:F3} мм";
        statusText.text = $"Канал {channel} - данные получены";
        statusText.color = new Color(0.4f, 0.9f, 0.4f);
        lastSuccess = Time.unscaledTime;
        hasData = true;
    }

    public void ShowError(string message)
    {
        statusText.text = message;
        statusText.color = new Color(1f, 0.6f, 0.3f);
    }

    private void Update()
    {
        if (hasData && Time.unscaledTime - lastSuccess > staleAfter)
            ShowError("Нет свежего ответа, данные неактуальны");
    }
}
