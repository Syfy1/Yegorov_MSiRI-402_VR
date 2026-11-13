using TMPro;
using UnityEngine;

// Панель станка: только показывает состояние, координаты, связь с API и последнюю команду.
// Запросы не отправляет и узлы модели не двигает.
public class MachinePanel : MonoBehaviour
{
    public TMP_Text stateText, axesText;
    public TMP_Text connectionText, commandText;
    public float staleAfter = 3f;

    private bool hasData;
    private float lastSuccess;
    private string axesValue;

    private void Awake()
    {
        stateText.text = "Статус из API не получен";
        axesValue = "X: - мм\nY: - мм\nZ: - мм";
        axesText.text = axesValue;
        connectionText.text = "Ожидание первого ответа";
        commandText.text = "Команды не отправлялись";
    }

    public void ShowCoordinatesMeters(float x, float y, float z, int channel)
    {
        axesValue = $"Канал {channel}\n" +
                    $"X: {x * 1000f:F1} мм\n" +
                    $"Y: {y * 1000f:F1} мм\n" +
                    $"Z: {z * 1000f:F1} мм";
        axesText.text = axesValue;
        hasData = true;
        lastSuccess = Time.unscaledTime;
        connectionText.text = "Данные получены";
    }

    public void ShowReadError(string reason)
    {
        connectionText.text = reason;
        axesText.text = axesValue + (hasData
            ? "\nПоследние данные неактуальны"
            : "\nДанные ещё не получены");
    }

    private void Update()
    {
        if (hasData && Time.unscaledTime - lastSuccess > Mathf.Max(0.1f, staleAfter))
        {
            ShowReadError("Нет свежего ответа GET");
        }
    }

    public void ShowCommand(string message)
    {
        commandText.text = message;
    }

    public void ShowDemoState(bool running)
    {
        stateText.text = running
            ? "ЛОКАЛЬНЫЙ ОПЫТ Работает"
            : "ЛОКАЛЬНЫЙ ОПЫТ Остановлен";
    }

    public void ShowConfirmedState(bool running)
    {
        stateText.text = running
            ? "По данным API Работает"
            : "По данным API Остановлен";
    }
}
