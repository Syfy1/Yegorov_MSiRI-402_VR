using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR.InteractionSystem;

// Сборка учебных сцен:
//  09.11 - станок, клиент чтения координат и панель на Canvas;
//  10.11 - VR-сцена со SteamVR: Player, телепорт, стол, заготовка, опора у станка.
public static class ClientPracticeSceneBuilder
{
    private const string Scene0911 = "Assets/Scenes/ClientPractice_09_11.unity";
    private const string Scene1011 = "Assets/Scenes/VR_Workshop_10_11.unity";
    private const string ModelPath = "Assets/СтанокDLSS.fbx";
    private const string MaterialsFolder = "Assets/Materials";
    private const string PlayerPrefab = "Assets/SteamVR/InteractionSystem/Core/Prefabs/Player.prefab";
    private const string TeleportPrefab = "Assets/SteamVR/InteractionSystem/Teleport/Prefabs/Teleporting.prefab";

    private class Machine
    {
        public GameObject root;
        public GameObject model;
        public Bounds bounds;
        public CncJsonController controller;
    }

    // ---------------------------------------------------------------- 09.11

    [MenuItem("Tools/Practice 09.11/Create Scene")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RenameDefaults();

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Пол";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);

        Machine machine = BuildMachine(Vector3.zero);
        if (machine == null) return;

        Vector3 panelPos = new Vector3(machine.bounds.max.x + 0.7f, 1.3f, machine.bounds.center.z);
        machine.controller.panel = CreatePanel(panelPos, Quaternion.identity, machine.controller);
        machine.controller.applyMovement = true;

        // Камера смотрит на станок и панель.
        Camera camera = Camera.main;
        if (camera != null)
        {
            Bounds view = machine.bounds;
            view.Encapsulate(panelPos + Vector3.right * 0.6f);
            float size = Mathf.Max(view.size.x, view.size.y, view.size.z);
            camera.transform.position = view.center + new Vector3(0f, size * 0.4f, -size * 1.3f);
            camera.transform.LookAt(view.center);
        }

        EditorSceneManager.SaveScene(scene, Scene0911);
        Selection.activeGameObject = machine.controller.gameObject;
        Debug.Log("Сцена сохранена: " + Scene0911);
    }

    // ---------------------------------------------------------------- 10.11

    [MenuItem("Tools/Practice 10.11/Create VR Scene")]
    public static void CreateVrScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RenameDefaults();

        // Обычная камера не нужна: сцену показывает камера Player (методичка 10.11, п. 21).
        if (Camera.main != null)
            Object.DestroyImmediate(Camera.main.gameObject);

        // Пол: неподвижный BoxCollider, верх на уровне Y = 0.
        GameObject floor = CreateBox("Пол", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f),
            GetMaterial("Пол", new Color(0.55f, 0.57f, 0.6f)));

        // Зона телепортации поверх пола: по ней можно перемещаться указателем контроллера.
        GameObject teleportZone = GameObject.CreatePrimitive(PrimitiveType.Plane);
        teleportZone.name = "Зона телепортации";
        teleportZone.transform.position = new Vector3(0f, 0.005f, 0f);
        teleportZone.transform.localScale = new Vector3(0.75f, 1f, 0.75f);
        teleportZone.AddComponent<TeleportArea>();

        // Станок стоит за рабочей зоной пользователя.
        Machine machine = BuildMachine(new Vector3(0f, 0f, 0.6f));
        if (machine == null) return;
        AddMachineColliders(machine.model);

        // Стол слева от пользователя и заготовка на нём.
        CreateBox("Стол", new Vector3(-1.5f, 0.4f, -0.5f), new Vector3(0.9f, 0.8f, 0.6f),
            GetMaterial("Стол", new Color(0.45f, 0.32f, 0.2f)));

        GameObject blank = CreateBox("Заготовка", new Vector3(-1.5f, 0.9f, -0.5f),
            new Vector3(0.12f, 0.08f, 0.12f), GetMaterial("Заготовка", new Color(0.75f, 0.75f, 0.78f)));
        var body = blank.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.useGravity = true;
        body.isKinematic = false;
        blank.AddComponent<Interactable>();
        blank.AddComponent<Throwable>();

        // Опора у патрона станка и метка зоны установки (метка без коллайдера).
        Transform chuck = FindNode(machine.model.transform, "Патрон");
        float supportX = chuck != null ? chuck.position.x : machine.bounds.max.x - 0.5f;
        float supportZ = machine.bounds.min.z - 0.3f;
        CreateBox("Опора у станка", new Vector3(supportX, 0.45f, supportZ), new Vector3(0.45f, 0.9f, 0.35f),
            GetMaterial("Опора", new Color(0.35f, 0.38f, 0.42f)));
        GameObject marker = CreateBox("Метка установки", new Vector3(supportX, 0.902f, supportZ),
            new Vector3(0.28f, 0.004f, 0.24f), GetMaterial("Метка установки", new Color(0.2f, 0.75f, 0.3f)));
        Object.DestroyImmediate(marker.GetComponent<Collider>());

        // VR-пользователь SteamVR и телепорт.
        GameObject player = InstantiatePrefab(PlayerPrefab, new Vector3(0f, 0f, -1.2f), Quaternion.identity);
        GameObject teleport = InstantiatePrefab(TeleportPrefab, Vector3.zero, Quaternion.identity);
        if (player == null || teleport == null) return;
        player.name = "Игрок (SteamVR Player)";
        teleport.name = "Телепорт";

        // Без шлема: ходьба по полу на WASD вместо полёта камеры.
        var fallbackCamera = player.GetComponentInChildren<FallbackCameraController>(true);
        if (fallbackCamera != null && fallbackCamera.GetComponent<DesktopWalk>() == null)
            fallbackCamera.gameObject.AddComponent<DesktopWalk>();

        // Панель координат с кнопками «Пуск» и «Стоп» справа от пользователя, лицом к нему.
        Vector3 eye = new Vector3(0f, 1.6f, -1.2f);
        Vector3 panelPos = new Vector3(1.3f, 1.35f, -0.4f);
        Vector3 look = Vector3.ProjectOnPlane(panelPos - eye, Vector3.up);
        machine.controller.panel = CreatePanel(panelPos, Quaternion.LookRotation(look), machine.controller);
        machine.controller.applyMovement = false; // движение включает кнопка «Пуск»

        EditorSceneManager.SaveScene(scene, Scene1011);
        AddScenesToBuild(Scene1011, Scene0911);
        Selection.activeGameObject = blank;
        Debug.Log("Сцена сохранена: " + Scene1011);
    }

    // Старые клавиши Input (WASD) нужны SteamVR Interaction System и режиму без шлема.
    [MenuItem("Tools/Practice 10.11/Enable Old Input (restart)")]
    public static void EnableOldInput()
    {
        Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
        var so = new SerializedObject(settings);
        so.FindProperty("activeInputHandler").intValue = 2; // Both: старый Input и Input System
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
    }

    [MenuItem("Tools/Practice 09.11/Dump Machine Hierarchy")]
    public static void DumpMachineHierarchy()
    {
        GameObject root = GameObject.Find("Станок");
        if (root == null)
        {
            Debug.LogWarning("В сцене нет объекта «Станок»");
            return;
        }

        string path = Path.Combine(Application.dataPath, "../Temp/machine_hierarchy.txt");
        File.WriteAllText(path, DumpHierarchy(root.transform), Encoding.UTF8);
        Debug.Log("Иерархия записана: " + path);
    }

    // ---------------------------------------------------------------- станок

    private static Machine BuildMachine(Vector3 position)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("Не найдена модель " + ModelPath);
            return null;
        }

        var machine = new Machine();

        // Неподвижный корень станка: нулевой поворот, единичный масштаб.
        machine.root = new GameObject("Станок");
        machine.root.transform.position = position;

        machine.model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, machine.root.transform);
        machine.model.name = "Модель станка";
        machine.model.transform.localPosition = Vector3.zero;

        // Распаковываем копию модели в этой сцене, чтобы выстроить кинематику.
        PrefabUtility.UnpackPrefabInstance(machine.model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // Камера и свет из Blender не нужны.
        foreach (Camera blenderCamera in machine.model.GetComponentsInChildren<Camera>(true))
            Object.DestroyImmediate(blenderCamera.gameObject);
        foreach (Light blenderLight in machine.model.GetComponentsInChildren<Light>(true))
            Object.DestroyImmediate(blenderLight.gameObject);

        // Анимация не должна менять те же узлы, что и клиент API.
        foreach (Animator animator in machine.model.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;

        // Детали из Blender названы транслитом - даём русские названия.
        RenameParts(machine.model.transform);

        // Кинематическая цепочка суппорта:
        // Каретка -> Поперечный суппорт -> Резцедержатель (с резцом и болтами).
        Transform karetka = FindNode(machine.model.transform, "Каретка");
        Transform suport = FindNode(machine.model.transform, "Поперечный суппорт");
        Transform holder = FindNode(machine.model.transform, "Резцедержатель");
        Reparent(machine.model.transform, karetka, "Фартук", "Маховик суппорта", "Ручка маховика суппорта");
        Reparent(machine.model.transform, karetka, "Поперечный суппорт");
        Reparent(machine.model.transform, suport, "Резцедержатель");
        Reparent(machine.model.transform, holder, "Резец", "Болт резцедержателя 1", "Болт резцедержателя 2");

        // Ставим станок на пол.
        Bounds bounds = GetBounds(machine.model);
        machine.model.transform.position += new Vector3(position.x - bounds.center.x, -bounds.min.y,
            position.z - bounds.center.z);
        machine.bounds = GetBounds(machine.model);

        // Клиент API.
        var connection = new GameObject("Подключение к станку");
        machine.controller = connection.AddComponent<CncJsonController>();
        machine.controller.url = "https://cnc.kovalev.team/get/3";
        machine.controller.channel = 1;
        machine.controller.interval = 0.5f;
        machine.controller.nodeX = karetka;
        machine.controller.nodeY = suport;
        machine.controller.nodeZ = holder;
        return machine;
    }

    // Русские названия деталей модели из Blender.
    private static readonly Dictionary<string, string> PartNames = new Dictionary<string, string>
    {
        { "Tokarny_Stanok", "Токарный станок" },
        { "Stanina", "Станина" },
        { "Karetka", "Каретка" },
        { "Poperechny_Suport", "Поперечный суппорт" },
        { "Rezcederzhatel", "Резцедержатель" },
        { "Rezcederzhatel_Bolt", "Болт резцедержателя" },
        { "Rezec", "Резец" },
        { "Fartuk", "Фартук" },
        { "Mahovik_Suporta", "Маховик суппорта" },
        { "Mahovik_Ruchka", "Ручка маховика суппорта" },
        { "Perednyaya_Babka", "Передняя бабка" },
        { "Zadnyaya_Babka", "Задняя бабка" },
        { "Pinol", "Пиноль" },
        { "Mahovik_Zadnei", "Маховик задней бабки" },
        { "Mahovik_Zadnei_Ruchka", "Ручка маховика задней бабки" },
        { "Fiksator", "Фиксатор задней бабки" },
        { "Centr", "Центр" },
        { "Shpindel", "Шпиндель" },
        { "Patron", "Патрон" },
        { "Patron_Otverstie", "Отверстие патрона" },
        { "Kulachok", "Кулачок" },
        { "Hodovoy_Vint", "Ходовой винт" },
        { "Napravlyayushaya", "Направляющая" },
        { "Nozhka_0", "Ножка правой тумбы" },
        { "Nozhka_1", "Ножка левой тумбы" },
        { "Tumba", "Тумба" },
        { "Poddon", "Поддон" },
        { "Panel", "Пульт" },
        { "Knopka_Start", "Кнопка Пуск" },
        { "Knopka_Stop", "Кнопка Стоп" },
        { "Regulyator", "Регулятор" },
        { "Kozhuh_Privoda", "Кожух привода" },
        { "Ventilyaciya", "Вентиляция" },
        { "Dver_Ruchka", "Ручка дверцы" },
        { "Dver_Shov", "Шов дверцы" },
        { "Kronshteyn", "Кронштейн" },
        { "Bolt", "Болт" },
    };

    private static void RenameParts(Transform model)
    {
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
        {
            if (PartNames.TryGetValue(part.name, out string russian))
            {
                part.name = russian;
                continue;
            }

            // Нумерованные детали: Kulachok_0 -> «Кулачок 1».
            int underscore = part.name.LastIndexOf('_');
            if (underscore > 0 && int.TryParse(part.name.Substring(underscore + 1), out int index) &&
                PartNames.TryGetValue(part.name.Substring(0, underscore), out string baseName))
                part.name = baseName + " " + (index + 1);
        }
    }

    // Коллайдеры по габаритам каждой детали: рука и заготовка не проходят сквозь станок.
    private static void AddMachineColliders(GameObject model)
    {
        foreach (MeshFilter mesh in model.GetComponentsInChildren<MeshFilter>(true))
            if (mesh.GetComponent<Collider>() == null)
                mesh.gameObject.AddComponent<BoxCollider>();
    }

    // ---------------------------------------------------------------- панель

    // Canvas в пространстве сцены: координаты, связь, команда и кнопки «Пуск» / «Стоп».
    private static CncCoordinatesPanel CreatePanel(Vector3 position, Quaternion rotation,
        CncJsonController controller)
    {
        var canvasGo = new GameObject("Панель координат",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500f, 440f);
        rect.localScale = Vector3.one * 0.002f; // 1 м x 0,88 м
        rect.position = position;
        rect.rotation = rotation;

        var background = new GameObject("Фон", typeof(Image));
        background.transform.SetParent(canvasGo.transform, false);
        Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        background.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.9f);

        CreateText(canvasGo.transform, "Заголовок", "Координаты станка", 34, 0.86f, 0.98f, FontStyles.Bold);
        TMP_Text axes = CreateText(canvasGo.transform, "Координаты", "", 38, 0.5f, 0.86f, FontStyles.Normal);
        TMP_Text status = CreateText(canvasGo.transform, "Состояние связи", "", 24, 0.41f, 0.5f, FontStyles.Italic);
        TMP_Text command = CreateText(canvasGo.transform, "Команда", "", 24, 0.32f, 0.41f, FontStyles.Normal);

        CreateButton(canvasGo.transform, "Кнопка Пуск", "Пуск", new Color(0.15f, 0.6f, 0.25f),
            0.05f, 0.47f, controller.StartMovement);
        CreateButton(canvasGo.transform, "Кнопка Стоп", "Стоп", new Color(0.75f, 0.2f, 0.2f),
            0.53f, 0.95f, controller.StopMovement);

        // Без шлема кнопки нажимаются мышью.
        canvasGo.AddComponent<PanelMouseClick>();

        var panel = canvasGo.AddComponent<CncCoordinatesPanel>();
        panel.axesText = axes;
        panel.statusText = status;
        panel.commandText = command;
        return panel;
    }

    // Кнопка панели: в шлеме её нажимает рука (BoxCollider + Interactable + UIElement),
    // без шлема - мышь (PanelMouseClick).
    private static void CreateButton(Transform parent, string name, string label, Color color,
        float minX, float maxX, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        Stretch(rect, minX, 0.05f, maxX, 0.27f);
        go.GetComponent<Image>().color = color;

        var button = go.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, onClick);

        // Размер коллайдера в пикселях Canvas: ширина и высота кнопки.
        Vector2 canvasSize = ((RectTransform)parent).sizeDelta;
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3((maxX - minX) * canvasSize.x, (0.27f - 0.05f) * canvasSize.y, 20f);

        var interactable = go.AddComponent<Interactable>();
        interactable.highlightOnHover = false;
        go.AddComponent<UIElement>();

        TMP_Text text = CreateText(go.transform, "Надпись", label, 36, 0f, 1f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 0f, 0f, 1f, 1f);
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float size,
        float minY, float maxY, FontStyles style)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>(), 0.05f, minY, 0.95f, maxY);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }

    private static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---------------------------------------------------------------- общее

    private static void RenameDefaults()
    {
        if (Camera.main != null)
            Camera.main.gameObject.name = "Камера";
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            light.gameObject.name = "Солнце";
    }

    private static GameObject CreateBox(string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.localScale = size;
        if (material != null)
            box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static Material GetMaterial(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = MaterialsFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject InstantiatePrefab(string path, Vector3 position, Quaternion rotation)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Не найден prefab " + path);
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetPositionAndRotation(position, rotation);
        return go;
    }

    private static void AddScenesToBuild(params string[] paths)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string path in paths)
            if (!scenes.Exists(s => s.path == path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Transform FindNode(Transform parent, string nodeName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == nodeName)
                return child;

        Debug.LogWarning("Не найден узел " + nodeName);
        return null;
    }

    private static void Reparent(Transform model, Transform parent, params string[] names)
    {
        if (parent == null) return;
        foreach (string name in names)
        {
            Transform child = FindNode(model, name);
            if (child != null)
                child.SetParent(parent, true);
        }
    }

    private static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    private static string DumpHierarchy(Transform node, int depth = 0)
    {
        var sb = new StringBuilder();
        sb.Append(new string(' ', depth * 2))
          .Append(node.name)
          .Append("  pos=").Append(node.localPosition.ToString("F3"))
          .Append(" rot=").Append(node.localEulerAngles.ToString("F0"))
          .Append(" scale=").Append(node.localScale.ToString("F2"))
          .Append('\n');
        foreach (Transform child in node)
            sb.Append(DumpHierarchy(child, depth + 1));
        return sb.ToString();
    }
}
