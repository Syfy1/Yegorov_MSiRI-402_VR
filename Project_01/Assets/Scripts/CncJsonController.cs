using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class CncJsonController : MonoBehaviour
{
    public string url = "https://cnc.kovalev.team/get/3";
    public int channel = 1;
    public float interval = 0.5f;
    public bool applyMovement = false;

    // Три отдельных подвижных узла модели.
    public Transform nodeX;
    public Transform nodeY;
    public Transform nodeZ;

    // Панель станка на Canvas для вывода координат и связи (можно не назначать).
    public MachinePanel panel;

    // Начальные положения узлов в сцене.
    private Vector3 startX;
    private Vector3 startY;
    private Vector3 startZ;
    private bool nodesReady;

    // Включение и выключение движения узлов модели (кнопки «Пуск» и «Стоп» панели).
    // При остановке узлы возвращаются в исходное положение.
    public void StartMovement()
    {
        if (!nodesReady) return;
        applyMovement = true;
    }

    public void StopMovement()
    {
        applyMovement = false;
        if (nodesReady)
        {
            nodeX.localPosition = startX;
            nodeY.localPosition = startY;
            nodeZ.localPosition = startZ;
        }
    }

    private IEnumerator Start()
    {
        nodesReady = nodeX != null && nodeY != null && nodeZ != null &&
                     nodeX != nodeY && nodeX != nodeZ && nodeY != nodeZ;

        if (applyMovement && !nodesReady)
        {
            Debug.LogError("Назначьте три разных узла станка.");
            yield break;
        }

        if (nodesReady)
        {
            startX = nodeX.localPosition;
            startY = nodeY.localPosition;
            startZ = nodeZ.localPosition;
        }

        while (true)
        {
            // Отправляем запрос и ждём ответа, не блокируя Unity.
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        Debug.Log("JSON: " + request.downloadHandler.text);
                        MoveNodes(request.downloadHandler.text);
                    }
                    catch (Exception error)
                    {
                        Debug.LogWarning("Ошибка чтения JSON: " + error.Message);
                        if (panel != null) panel.ShowReadError("Ошибка формата данных");
                    }
                }
                else
                {
                    Debug.LogWarning("HTTP " + request.responseCode + ": " + request.error);
                    if (panel != null) panel.ShowReadError("Нет связи с API");
                }
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, interval));
        }
    }

    private void MoveNodes(string json)
    {
        JObject data = JObject.Parse(json);

        // Сначала читаем все три координаты. Переводим миллиметры в метры.
        float x = GetCoordinate(data, "X") * 0.001f;
        float y = GetCoordinate(data, "Y") * 0.001f;
        float z = GetCoordinate(data, "Z") * 0.001f;

        Debug.Log($"Координаты в метрах: X={x}, Y={y}, Z={z}");
        if (panel != null) panel.ShowCoordinatesMeters(x, y, z, channel);
        if (!applyMovement) return;

        // Оси станка X/Y/Z соответствуют осям Unity X/Z/Y.
        nodeX.localPosition = startX + ToParentSpace(nodeX, Vector3.right * x);
        nodeY.localPosition = startY + ToParentSpace(nodeY, Vector3.forward * y);
        nodeZ.localPosition = startZ + ToParentSpace(nodeZ, Vector3.up * z);
    }

    // Модель из Blender повёрнута на -90° по X и имеет масштаб 100,
    // поэтому смещение в метрах по осям сцены переводим в локальные
    // координаты родителя узла. Без этого 38 мм превратились бы в 3,8 м.
    private Vector3 ToParentSpace(Transform node, Vector3 offsetInMeters)
    {
        return node.parent != null
            ? node.parent.InverseTransformVector(offsetInMeters)
            : offsetInMeters;
    }

    private float GetCoordinate(JObject data, string axis)
    {
        // JSON содержит разделы, а каждый раздел - пары «название, значение».
        foreach (JToken section in data["data"])
        {
            if ((string)section[0] != "Оси Канала " + channel)
                continue;

            foreach (JToken row in section[1])
            {
                if ((string)row[0] == "Перемещение по оси " + axis)
                {
                    // Например: «144,157 мм» -> «144.157» -> число 144.157.
                    string text = ((string)row[1]).Replace("мм", "").Trim();
                    text = text.Replace(',', '.');
                    float value = float.Parse(text, CultureInfo.InvariantCulture);
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new FormatException("Некорректная координата " + axis);
                    return value;
                }
            }
        }

        throw new FormatException("Не найдена координата " + axis);
    }
}
