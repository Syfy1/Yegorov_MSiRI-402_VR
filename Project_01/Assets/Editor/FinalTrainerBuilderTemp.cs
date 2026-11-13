using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR.InteractionSystem;

public static class FinalTrainerBuilderTemp
{
    private const string ScenePath = "Assets/Scenes/FinalTrainer.unity";
    private const string ModelPath = "Assets/СтанокDLSS.fbx";
    private const string MaterialsFolder = "Assets/FinalTrainer/Materials";
    private const string PlayerPrefab = "Assets/SteamVR/InteractionSystem/Core/Prefabs/Player.prefab";
    private const string TeleportPrefab = "Assets/SteamVR/InteractionSystem/Teleport/Prefabs/Teleporting.prefab";

    [MenuItem("Tools/Build FinalTrainer")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            light.gameObject.name = "Солнце";

        // ---------- станок
        var machineRoot = new GameObject("Станок");
        machineRoot.transform.position = new Vector3(0f, 0f, 0.9f);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),
            machineRoot.transform);
        model.name = "Модель станка";
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (Camera c in model.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (Light l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        RenameParts(model.transform);

        Transform karetka = Find(model.transform, "Каретка");
        Transform suport = Find(model.transform, "Поперечный суппорт");
        Transform holder = Find(model.transform, "Резцедержатель");
        Reparent(model.transform, karetka, "Фартук", "Маховик суппорта", "Ручка маховика суппорта", "Поперечный суппорт");
        Reparent(model.transform, suport, "Резцедержатель");
        Reparent(model.transform, holder, "Резец", "Болт резцедержателя 1", "Болт резцедержателя 2");

        Bounds b = Bounds(model);
        model.transform.position += new Vector3(-b.center.x, -b.min.y, machineRoot.transform.position.z - b.center.z);
        b = Bounds(model);

        // Подвижный узел с единичным масштабом: его локальная X совпадает с осью станины.
        var movingNode = new GameObject("Подвижный узел (каретка)").transform;
        movingNode.SetParent(machineRoot.transform, false);
        movingNode.position = karetka.position;
        karetka.SetParent(movingNode, true);

        // Коллайдеры корпуса (без подвижного узла, чтобы обработка не толкала заготовку).
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            if (mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<BoxCollider>();

        Transform chuck = Find(model.transform, "Патрон");
        float cx = chuck != null ? chuck.position.x : 0.5f;
        float front = b.min.z;

        // ---------- окружение
        var env = new GameObject("Окружение").transform;
        Box("Пол", env, new Vector3(0f, -0.05f, 0.3f), new Vector3(6f, 0.1f, 6f), Mat("Пол", new Color(0.55f, 0.57f, 0.6f)));
        GameObject tz = GameObject.CreatePrimitive(PrimitiveType.Plane);
        tz.name = "Зона телепортации";
        tz.transform.SetParent(env, false);
        tz.transform.position = new Vector3(0f, 0.005f, 0.3f);
        tz.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
        tz.AddComponent<TeleportArea>();

        // Раскладка: исходный стол слева от игрока, рабочая опора перед патроном, стол продукции справа.
        // Верх всех опор на 0.84 м, вал Ø0.06 м лежит на них: центр 0.84 + 0.03 = 0.87 м.
        Vector3 home = new Vector3(cx - 0.85f, 0.87f, front - 0.6f);
        Vector3 work = new Vector3(cx, 0.87f, front - 0.25f);
        Vector3 output = new Vector3(cx + 0.85f, 0.87f, front - 0.6f);

        Material wood = Mat("Дерево столешницы", new Color(0.55f, 0.4f, 0.25f));
        Material metal = Mat("Металл ножек", new Color(0.3f, 0.32f, 0.35f));
        Table("Исходный стол", env, home, new Vector2(0.6f, 0.45f), wood, metal);
        Table("Рабочая опора", env, work, new Vector2(0.4f, 0.3f), Mat("Металл опоры", new Color(0.5f, 0.53f, 0.56f)), metal);
        Table("Стол готовой продукции", env, output, new Vector2(0.6f, 0.45f), wood, metal);
        Marker("Метка исходного места", env, home, new Vector3(0.22f, 0.004f, 0.14f), Mat("Метка исходного места", new Color(0.9f, 0.6f, 0.15f)));
        Marker("Метка рабочей зоны", env, work, new Vector3(0.26f, 0.004f, 0.2f), Mat("Метка рабочей зоны", new Color(0.2f, 0.75f, 0.3f)));
        Marker("Метка зоны продукции", env, output, new Vector3(0.24f, 0.004f, 0.2f), Mat("Метка зоны продукции", new Color(0.2f, 0.45f, 0.9f)));

        // ---------- точки и зоны
        Transform blankHome = Point("Исходное место заготовки", home);
        Transform workPoint = Point("Точка установки", work);
        Transform outputPoint = Point("Точка готовой продукции", output);
        BoxCollider workZone = Zone("Рабочая зона", new Vector3(work.x, 0.93f, work.z), new Vector3(0.30f, 0.22f, 0.30f));
        BoxCollider outputZone = Zone("Зона готовой продукции", new Vector3(output.x, 0.93f, output.z), new Vector3(0.28f, 0.22f, 0.28f));

        // ---------- заготовка: вал Ø60 мм, длина 150 мм, лежит вдоль оси шпинделя (X)
        var blankGo = new GameObject("Заготовка (вал)");
        blankGo.transform.position = home;
        var bc = blankGo.AddComponent<BoxCollider>(); // коробка не даёт валу укатиться со стола
        bc.size = new Vector3(0.15f, 0.06f, 0.06f);
        var rb = blankGo.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.useGravity = true;
        rb.isKinematic = false;
        blankGo.AddComponent<Interactable>();
        var blank = blankGo.AddComponent<TrainingBlank>();
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Вид вала";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(blankGo.transform, false);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        visual.transform.localScale = new Vector3(0.06f, 0.075f, 0.06f);
        visual.GetComponent<Renderer>().sharedMaterial = Mat("Заготовка", new Color(0.6f, 0.6f, 0.63f));

        // ---------- игрок и телепорт
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        player.name = "Игрок (SteamVR Player)";
        player.transform.position = new Vector3(cx, 0f, front - 1.15f);
        var teleport = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TeleportPrefab));
        teleport.name = "Телепорт";
        var fallback = player.GetComponentInChildren<FallbackCameraController>(true);
        if (fallback != null) fallback.gameObject.AddComponent<DesktopWalk>();

        // ---------- контроллер сценария
        var controllerGo = new GameObject("Контроллер обучения");
        var trainer = controllerGo.AddComponent<MachineTrainer>();
        blank.trainer = trainer;

        // ---------- панель
        Vector3 eye = new Vector3(cx, 1.6f, front - 1.15f);
        Vector3 panelPos = new Vector3(cx + 1.5f, 1.4f, front - 0.95f);
        var canvasGo = new GameObject("Панель обучения", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        foreach (Camera pc in player.GetComponentsInChildren<Camera>(true))
            if (pc.name == "VRCamera") canvas.worldCamera = pc;
        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 600f);
        rect.localScale = Vector3.one * 0.001f;
        rect.position = panelPos;
        rect.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(panelPos - eye, Vector3.up));

        var bg = new GameObject("Фон", typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.95f);

        Transform root = canvasGo.transform;
        trainer.stepText = Text(root, "Этап", 38, new Vector2(0, 240), new Vector2(900, 90), TextAlignmentOptions.Center, FontStyles.Bold);
        trainer.messageText = Text(root, "Подсказка", 32, new Vector2(0, 110), new Vector2(900, 150), TextAlignmentOptions.Center, FontStyles.Normal);
        trainer.resultText = Text(root, "Результат", 28, new Vector2(0, -20), new Vector2(900, 100), TextAlignmentOptions.Center, FontStyles.Normal);

        Button(root, "Кнопка Далее", "Далее", new Color(0.25f, 0.45f, 0.7f), new Vector2(-230, -120), new Vector2(400, 70), trainer.NextStep);
        GameObject guardButton = Button(root, "Кнопка Ограждение", "Ограждение", new Color(0.55f, 0.45f, 0.2f), new Vector2(230, -120), new Vector2(400, 70), trainer.ToggleGuard);
        guardButton.SetActive(false); // ограждения в модели нет
        Button(root, "Кнопка Пуск", "Пуск", new Color(0.15f, 0.6f, 0.25f), new Vector2(-310, -230), new Vector2(270, 80), trainer.StartCycle);
        Button(root, "Кнопка Стоп", "Стоп", new Color(0.75f, 0.2f, 0.2f), new Vector2(0, -230), new Vector2(270, 80), trainer.StopCycle);
        Button(root, "Кнопка Повторить", "Повторить", new Color(0.4f, 0.4f, 0.45f), new Vector2(310, -230), new Vector2(270, 80), trainer.RestartTraining);
        canvasGo.AddComponent<PanelMouseClick>();

        // ---------- ссылки сценария
        trainer.blank = blank;
        trainer.blankHome = blankHome;
        trainer.workPoint = workPoint;
        trainer.outputPoint = outputPoint;
        trainer.workZone = workZone;
        trainer.outputZone = outputZone;
        trainer.movingNode = movingNode;
        trainer.blankRenderer = visual.GetComponent<Renderer>();
        trainer.duration = 5f;
        trainer.travel = 0.12f;
        trainer.requireGuard = false;

        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("Сцена сохранена: " + ScenePath + ", патрон X=" + cx + ", фронт станка Z=" + front);
    }

    private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Стол: столешница толщиной 4 см (верх на 0.84 м) и четыре ножки.
    private static void Table(string name, Transform parent, Vector3 center, Vector2 size, Material top, Material legs)
    {
        var table = new GameObject(name).transform;
        table.SetParent(parent, false);
        table.position = new Vector3(center.x, 0f, center.z);
        GameObject board = Box("Столешница", table, new Vector3(center.x, 0.82f, center.z),
            new Vector3(size.x, 0.04f, size.y), top);
        float dx = size.x * 0.5f - 0.04f, dz = size.y * 0.5f - 0.04f;
        int n = 1;
        foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box("Ножка " + n++, table, new Vector3(center.x + s.x * dx, 0.4f, center.z + s.y * dz),
                new Vector3(0.04f, 0.8f, 0.04f), legs);
    }

    private static void Marker(string name, Transform parent, Vector3 point, Vector3 size, Material mat)
    {
        GameObject go = Box(name, parent, new Vector3(point.x, 0.842f, point.z), size, mat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    private static Transform Point(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        return go.transform;
    }

    private static BoxCollider Zone(string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        box.center = Vector3.zero;
        box.isTrigger = true;
        return box;
    }

    private static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/FinalTrainer")) AssetDatabase.CreateFolder("Assets", "FinalTrainer");
        if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/FinalTrainer", "Materials");
        string path = MaterialsFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static TMP_Text Text(Transform parent, string name, float size, Vector2 pos, Vector2 box,
        TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, box);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = Color.white;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static GameObject Button(Transform parent, string name, string label, Color color, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, size);
        var image = go.GetComponent<Image>();
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
        Text(go.transform, "Надпись", 34, Vector2.zero, size, TextAlignmentOptions.Center, FontStyles.Bold).text = label;
        return go;
    }

    private static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static readonly Dictionary<string, string> PartNames = new Dictionary<string, string>
    {
        { "Tokarny_Stanok", "Токарный станок" }, { "Stanina", "Станина" }, { "Karetka", "Каретка" },
        { "Poperechny_Suport", "Поперечный суппорт" }, { "Rezcederzhatel", "Резцедержатель" },
        { "Rezcederzhatel_Bolt", "Болт резцедержателя" }, { "Rezec", "Резец" }, { "Fartuk", "Фартук" },
        { "Mahovik_Suporta", "Маховик суппорта" }, { "Mahovik_Ruchka", "Ручка маховика суппорта" },
        { "Perednyaya_Babka", "Передняя бабка" }, { "Zadnyaya_Babka", "Задняя бабка" }, { "Pinol", "Пиноль" },
        { "Mahovik_Zadnei", "Маховик задней бабки" }, { "Mahovik_Zadnei_Ruchka", "Ручка маховика задней бабки" },
        { "Fiksator", "Фиксатор задней бабки" }, { "Centr", "Центр" }, { "Shpindel", "Шпиндель" },
        { "Patron", "Патрон" }, { "Patron_Otverstie", "Отверстие патрона" }, { "Kulachok", "Кулачок" },
        { "Hodovoy_Vint", "Ходовой винт" }, { "Napravlyayushaya", "Направляющая" },
        { "Nozhka_0", "Ножка правой тумбы" }, { "Nozhka_1", "Ножка левой тумбы" }, { "Tumba", "Тумба" },
        { "Poddon", "Поддон" }, { "Panel", "Пульт" }, { "Knopka_Start", "Кнопка Пуск" },
        { "Knopka_Stop", "Кнопка Стоп" }, { "Regulyator", "Регулятор" }, { "Kozhuh_Privoda", "Кожух привода" },
        { "Ventilyaciya", "Вентиляция" }, { "Dver_Ruchka", "Ручка дверцы" }, { "Dver_Shov", "Шов дверцы" },
        { "Kronshteyn", "Кронштейн" }, { "Bolt", "Болт" },
    };

    private static void RenameParts(Transform model)
    {
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
        {
            if (PartNames.TryGetValue(part.name, out string ru)) { part.name = ru; continue; }
            int u = part.name.LastIndexOf('_');
            if (u > 0 && int.TryParse(part.name.Substring(u + 1), out int i) &&
                PartNames.TryGetValue(part.name.Substring(0, u), out string bn))
                part.name = bn + " " + (i + 1);
        }
    }

    private static Transform Find(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        Debug.LogWarning("Не найден узел " + name);
        return null;
    }

    private static void Reparent(Transform model, Transform parent, params string[] names)
    {
        foreach (string n in names)
        {
            Transform c = Find(model, n);
            if (c != null && parent != null) c.SetParent(parent, true);
        }
    }

    private static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
