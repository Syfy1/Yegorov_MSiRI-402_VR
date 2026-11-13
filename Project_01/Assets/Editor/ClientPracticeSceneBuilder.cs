using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Собирает сцену практики 09.11: пол, станок и клиент чтения координат.
public static class ClientPracticeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ClientPractice_09_11.unity";
    private const string ModelPath = "Assets/СтанокDLSS.fbx";

    [MenuItem("Tools/Practice 09.11/Create Scene")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Пол.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2f, 1f, 2f);

        // Неподвижный корень станка: нулевой поворот, единичный масштаб.
        var root = new GameObject("MachineRoot");
        root.transform.position = Vector3.zero;

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("Не найдена модель " + ModelPath);
            return;
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
        model.transform.localPosition = Vector3.zero;

        // Распаковываем копию модели в этой сцене, чтобы выстроить кинематику.
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // Камера и свет из Blender не нужны: сцену показывает Main Camera.
        foreach (Camera blenderCamera in model.GetComponentsInChildren<Camera>(true))
            Object.DestroyImmediate(blenderCamera.gameObject);
        foreach (Light blenderLight in model.GetComponentsInChildren<Light>(true))
            Object.DestroyImmediate(blenderLight.gameObject);

        // Анимация не должна менять те же узлы, что и клиент API.
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;

        // Кинематическая цепочка суппорта:
        // Karetka -> Poperechny_Suport -> Rezcederzhatel (с резцом и болтами).
        Transform karetka = FindNode(model.transform, "Karetka");
        Transform suport = FindNode(model.transform, "Poperechny_Suport");
        Transform holder = FindNode(model.transform, "Rezcederzhatel");
        Reparent(model.transform, karetka, "Fartuk", "Mahovik_Suporta", "Mahovik_Ruchka");
        Reparent(model.transform, karetka, "Poperechny_Suport");
        Reparent(model.transform, suport, "Rezcederzhatel");
        Reparent(model.transform, holder, "Rezec", "Rezcederzhatel_Bolt_0", "Rezcederzhatel_Bolt_1");

        // Ставим станок на пол.
        Bounds bounds = GetBounds(model);
        model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        bounds = GetBounds(model);

        // Клиент API.
        var connection = new GameObject("MachineConnection");
        var controller = connection.AddComponent<CncJsonController>();
        controller.url = "https://cnc.kovalev.team/get/3";
        controller.channel = 1;
        controller.interval = 0.5f;
        controller.applyMovement = true;
        controller.panel = CreatePanel(bounds);
        controller.nodeX = FindNode(model.transform, "Karetka");
        controller.nodeY = FindNode(model.transform, "Poperechny_Suport");
        controller.nodeZ = FindNode(model.transform, "Rezcederzhatel");

        // Камера смотрит на станок.
        Camera camera = Camera.main;
        if (camera != null)
        {
            // В кадр попадают и станок, и панель с координатами.
            bounds.Encapsulate(controller.panel.transform.position + Vector3.right * 0.6f);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            camera.transform.position = bounds.center + new Vector3(0f, size * 0.4f, -size * 1.3f);
            camera.transform.LookAt(bounds.center);
        }

        Debug.Log("Иерархия станка:\n" + DumpHierarchy(model.transform));
        Debug.Log($"Размер станка: {bounds.size}, центр: {bounds.center}");

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = connection;
        Debug.Log("Сцена сохранена: " + ScenePath);
    }

    [MenuItem("Tools/Practice 09.11/Dump Machine Hierarchy")]
    public static void DumpMachineHierarchy()
    {
        GameObject root = GameObject.Find("MachineRoot");
        if (root == null)
        {
            Debug.LogWarning("В сцене нет MachineRoot");
            return;
        }

        string path = System.IO.Path.Combine(Application.dataPath, "../Temp/machine_hierarchy.txt");
        System.IO.File.WriteAllText(path, DumpHierarchy(root.transform), Encoding.UTF8);
        Debug.Log("Иерархия записана: " + path);
    }

    private static Transform FindNode(Transform parent, string nodeName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == nodeName)
                return child;

        Debug.LogWarning("Не найден узел " + nodeName);
        return null;
    }

    // Canvas в пространстве сцены рядом со станком: координаты осей и состояние связи.
    private static CncCoordinatesPanel CreatePanel(Bounds machine)
    {
        var canvasGo = new GameObject("CoordinatesCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500f, 320f);
        rect.localScale = Vector3.one * 0.002f; // 1 м x 0,64 м
        rect.position = new Vector3(machine.max.x + 0.7f, 1.3f, machine.center.z);

        var background = new GameObject("Background", typeof(Image));
        background.transform.SetParent(canvasGo.transform, false);
        Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        background.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.9f);

        CreateText(canvasGo.transform, "Title", "Координаты станка", 34, 0.78f, 1f,
            FontStyles.Bold);
        TMP_Text axes = CreateText(canvasGo.transform, "AxesText", "", 40, 0.22f, 0.8f,
            FontStyles.Normal);
        TMP_Text status = CreateText(canvasGo.transform, "StatusText", "", 26, 0f, 0.22f,
            FontStyles.Italic);

        var panel = canvasGo.AddComponent<CncCoordinatesPanel>();
        panel.axesText = axes;
        panel.statusText = status;
        return panel;
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
