using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AlignToTablesTemp
{
    [MenuItem("Tools/Align Zones To Tables")]
    public static void Align()
    {
        Align("Исходный стол", "Метка исходного места", "Исходное место заготовки", null);
        Align("Рабочая опора", "Метка рабочей зоны", "Точка установки", "Рабочая зона");
        Align("Стол готовой продукции", "Метка зоны продукции", "Точка готовой продукции", "Зона готовой продукции");

        // Вал - на исходное место.
        GameObject blank = GameObject.Find("Заготовка (вал)");
        GameObject home = GameObject.Find("Исходное место заготовки");
        if (blank != null && home != null)
        {
            Undo.RecordObject(blank.transform, "Align");
            blank.transform.SetPositionAndRotation(home.transform.position, home.transform.rotation);

            // Модель вала - строго в центре коллайдера, лёжа вдоль X.
            Transform visual = blank.transform.Find("Вид вала");
            if (visual != null)
            {
                Undo.RecordObject(visual, "Align");
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.Euler(0f, 0f, 90f);
                visual.localScale = new Vector3(0.06f, 0.075f, 0.06f);
            }
        }

        // Зона телепортации - обратно в «Окружение», на весь пол.
        GameObject teleportZone = GameObject.Find("Зона телепортации");
        GameObject env = GameObject.Find("Окружение");
        if (teleportZone != null && env != null)
        {
            Undo.SetTransformParent(teleportZone.transform, env.transform, "Align");
            teleportZone.transform.SetPositionAndRotation(new Vector3(0f, 0.005f, 0.3f), Quaternion.identity);
            teleportZone.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Зоны, точки и метки выровнены по столам");
    }

    // Рабочая зона - сразу в патроне станка; отдельная опора не нужна.
    [MenuItem("Tools/Move Work Zone To Chuck")]
    public static void MoveToChuck()
    {
        GameObject machine = GameObject.Find("Станок");
        GameObject chuck = GameObject.Find("Патрон");
        GameObject point = GameObject.Find("Точка установки");
        GameObject zone = GameObject.Find("Рабочая зона");
        if (machine == null || chuck == null || point == null || zone == null) { Debug.LogWarning("Нет объектов"); return; }

        Bounds b = chuck.GetComponent<Renderer>().bounds;
        // Вал 150 мм вставлен в кулачки на 30 мм и лежит по оси шпинделя, торцом к задней бабке.
        Vector3 work = new Vector3(b.min.x - 0.075f + 0.03f, b.center.y, b.center.z);

        Undo.SetTransformParent(point.transform, machine.transform, "Chuck");
        point.transform.SetPositionAndRotation(work, Quaternion.identity);
        Undo.SetTransformParent(zone.transform, machine.transform, "Chuck");
        zone.transform.SetPositionAndRotation(work, Quaternion.identity);
        zone.GetComponent<BoxCollider>().size = new Vector3(0.30f, 0.22f, 0.30f);

        GameObject support = GameObject.Find("Рабочая опора");
        if (support != null) Undo.DestroyObjectImmediate(support);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Рабочая зона в патроне: " + work);
    }

    private static void Align(string tableName, string markerName, string pointName, string zoneName)
    {
        GameObject table = GameObject.Find(tableName);
        if (table == null) { Debug.LogWarning("Нет " + tableName); return; }
        Transform board = table.transform.Find("Столешница");
        if (board == null) { Debug.LogWarning("Нет столешницы у " + tableName); return; }

        Bounds b = board.GetComponent<Renderer>().bounds;
        float top = b.max.y;
        Vector3 c = new Vector3(b.center.x, top, b.center.z);
        Quaternion yaw = Quaternion.Euler(0f, board.eulerAngles.y, 0f);

        // Ножки - под углы столешницы.
        Vector3 half = board.lossyScale * 0.5f;
        int i = 0;
        Vector2[] corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
        foreach (Transform child in table.transform)
        {
            if (!child.name.StartsWith("Ножка") || i >= 4) continue;
            Vector2 s = corners[i++];
            Vector3 offset = yaw * new Vector3(s.x * (half.x - 0.04f), 0f, s.y * (half.z - 0.04f));
            float legHeight = top - 0.04f;
            Undo.RecordObject(child, "Align");
            child.SetPositionAndRotation(new Vector3(c.x + offset.x, legHeight * 0.5f, c.z + offset.z), yaw);
            child.localScale = new Vector3(0.04f, legHeight, 0.04f);
        }

        Place(markerName, table.transform, new Vector3(c.x, top + 0.002f, c.z), yaw);
        Place(pointName, table.transform, new Vector3(c.x, top + 0.03f, c.z), Quaternion.identity);
        if (zoneName != null)
            Place(zoneName, table.transform, new Vector3(c.x, top + 0.09f, c.z), yaw);
    }

    private static void Place(string name, Transform table, Vector3 pos, Quaternion rot)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) { Debug.LogWarning("Нет " + name); return; }
        Undo.SetTransformParent(go.transform, table, "Align");
        go.transform.SetPositionAndRotation(pos, rot);
    }
}
