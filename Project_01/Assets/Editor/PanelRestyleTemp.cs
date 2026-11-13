using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR.InteractionSystem;

public static class PanelRestyleTemp
{
    private static Sprite rounded;

    [MenuItem("Tools/Restyle Training Panel")]
    public static void Restyle()
    {
        GameObject old = GameObject.Find("Панель обучения");
        var trainer = Object.FindAnyObjectByType<MachineTrainer>();
        if (old == null || trainer == null) { Debug.LogWarning("Нет панели или контроллера"); return; }

        Vector3 pos = old.transform.position;
        Quaternion rot = old.transform.rotation;
        Camera eventCamera = old.GetComponent<Canvas>().worldCamera;
        Undo.DestroyObjectImmediate(old);

        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var canvasGo = new GameObject("Панель обучения", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Panel");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eventCamera;
        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 600f);
        rect.localScale = Vector3.one * 0.001f;
        rect.SetPositionAndRotation(pos, rot);
        Transform root = canvasGo.transform;

        // Корпус панели и шапка.
        Panel(root, "Корпус", Vector2.zero, new Vector2(1000, 600), new Color(0.11f, 0.13f, 0.17f, 0.97f));
        Panel(root, "Шапка", new Vector2(0, 255), new Vector2(980, 75), new Color(0.13f, 0.45f, 0.5f));
        var title = Text(root, "Название", "Тренажёр: токарный станок", 36, new Vector2(-150, 255), new Vector2(640, 70),
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        var counter = Text(root, "Счётчик этапов", "1 / 9", 36, new Vector2(400, 255), new Vector2(160, 70),
            TextAlignmentOptions.MidlineRight, FontStyles.Bold, new Color(1f, 0.85f, 0.4f));

        // Полоса прогресса: 9 сегментов.
        var marks = new Image[9];
        float segment = 960f / 9f;
        for (int i = 0; i < 9; i++)
            marks[i] = Panel(root, "Прогресс " + (i + 1),
                new Vector2(-480 + segment * (i + 0.5f), 200), new Vector2(segment - 8, 14), new Color(0.25f, 0.28f, 0.33f));

        // Карточка этапа слева.
        Panel(root, "Карточка этапа", new Vector2(-150, -40), new Vector2(660, 420), new Color(0.17f, 0.2f, 0.25f));
        var step = Text(root, "Этап", "", 34, new Vector2(-150, 135), new Vector2(620, 60),
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color(1f, 0.85f, 0.4f));
        var message = Text(root, "Подсказка", "", 30, new Vector2(-150, 10), new Vector2(620, 190),
            TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);
        var result = Text(root, "Результат", "", 28, new Vector2(-150, -165), new Vector2(620, 110),
            TextAlignmentOptions.TopLeft, FontStyles.Italic, new Color(0.55f, 0.95f, 0.7f));

        // Столбец кнопок справа.
        Button(root, "Кнопка Далее", "Далее", new Color(0.2f, 0.45f, 0.75f), new Vector2(345, 120), trainer.NextStep);
        Button(root, "Кнопка Пуск", "Пуск", new Color(0.15f, 0.6f, 0.3f), new Vector2(345, 20), trainer.StartCycle);
        Button(root, "Кнопка Стоп", "Стоп", new Color(0.78f, 0.22f, 0.22f), new Vector2(345, -80), trainer.StopCycle);
        Button(root, "Кнопка Повторить", "Повторить", new Color(0.42f, 0.42f, 0.5f), new Vector2(345, -180), trainer.RestartTraining);
        canvasGo.AddComponent<PanelMouseClick>();

        Undo.RecordObject(trainer, "Panel");
        trainer.stepText = step;
        trainer.messageText = message;
        trainer.resultText = result;
        trainer.counterText = counter;
        trainer.stageMarks = marks;
        EditorUtility.SetDirty(trainer);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Панель обучения переделана");
    }

    private static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box,
        TextAlignmentOptions align, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, box);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = color;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static void Button(Transform parent, string name, string label, Color color, Vector2 pos,
        UnityEngine.Events.UnityAction onClick)
    {
        Vector2 size = new Vector2(250, 80);
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, size);
        var image = go.GetComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.3f);
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, onClick);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(size.x, size.y, 20f);
        var interactable = go.AddComponent<Interactable>();
        interactable.highlightOnHover = false;
        go.AddComponent<UIElement>();
        Text(go.transform, "Надпись", label, 34, Vector2.zero, size, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
    }

    private static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }
}
