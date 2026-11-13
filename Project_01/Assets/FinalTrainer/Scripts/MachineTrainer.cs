using System.Collections;
using TMPro;
using UnityEngine;

// Учебный сценарий работы со станком: девять этапов, проверки действий,
// условный цикл обработки, результат и повтор. Работает локально, без API.
public class MachineTrainer : MonoBehaviour
{
    public enum Stage
    {
        Intro = 1,      // ознакомление
        Check = 2,      // исходное состояние
        Grab = 3,       // захват заготовки
        Place = 4,      // установка в рабочую зону
        Guard = 5,      // закрытие ограждения
        Ready = 6,      // запуск
        Processing = 7, // ожидание обработки
        Output = 8,     // готовая продукция
        Done = 9        // результат
    }

    [Header("Заготовка и точки")]
    public TrainingBlank blank;
    public Transform blankHome;
    public Transform workPoint;
    public Transform outputPoint;

    [Header("Зоны (BoxCollider с Is Trigger)")]
    public BoxCollider workZone;
    public BoxCollider outputZone;

    [Header("Станок")]
    public Transform movingNode;
    public float duration = 5f;   // длительность условной обработки, с
    public float travel = 0.12f;  // ход узла вдоль локальной X, м

    [Header("Панель")]
    public TMP_Text stepText;
    public TMP_Text messageText;
    public TMP_Text resultText;
    public TMP_Text counterText;               // «3 / 9» в шапке панели (можно не назначать)
    public UnityEngine.UI.Image[] stageMarks;  // полоса прогресса из 9 сегментов (можно не назначать)
    public Color doneColor = new Color(0.25f, 0.75f, 0.55f);
    public Color currentColor = new Color(1f, 0.75f, 0.2f);
    public Color todoColor = new Color(0.25f, 0.28f, 0.33f);

    [Header("Цвет детали")]
    public Renderer blankRenderer;
    public Color rawColor = new Color(0.6f, 0.6f, 0.63f);
    public Color finishedColor = new Color(0.2f, 0.75f, 0.3f);

    [Header("Ограждение (если есть в модели)")]
    public bool requireGuard = false;
    public Transform guard;
    public Vector3 guardOpenOffset = new Vector3(0.5f, 0f, 0f);

    public Stage CurrentStage { get; private set; }

    private Vector3 nodeHome;
    private Vector3 guardClosed;
    private bool guardOpen;
    private bool placed;
    private bool processed;
    private bool collected;
    private int errors;
    private int handledRelease;
    private float startTime;
    private Coroutine cycle;

    private void Start()
    {
        if (blank == null || blankHome == null || workPoint == null || outputPoint == null ||
            workZone == null || outputZone == null || movingNode == null ||
            stepText == null || messageText == null || resultText == null || blankRenderer == null ||
            (requireGuard && guard == null))
        {
            Debug.LogError("Заполните ссылки MachineTrainer");
            enabled = false;
            return;
        }

        blank.trainer = this;
        nodeHome = movingNode.localPosition;
        if (guard != null) guardClosed = guard.localPosition;
        Restart();
    }

    private void Update()
    {
        // Каждое отпускание обрабатывается один раз и после того, как рука закончила действие.
        if (blank.ReleaseNumber != handledRelease && !blank.IsHeld &&
            Time.time - blank.ReleaseTime >= 0.15f)
        {
            handledRelease = blank.ReleaseNumber;
            HandleRelease();
        }
    }

    // ------------------------------------------------ кнопки панели

    public void NextStep()
    {
        if (CurrentStage == Stage.Intro)
        {
            SetStage(Stage.Check);
            return;
        }

        if (CurrentStage == Stage.Check)
        {
            if (blank.IsHeld) { Message("Сначала положите заготовку на исходный стол."); return; }
            if (Vector3.Distance(blank.transform.position, blankHome.position) > 0.3f)
            { Message("Заготовка должна лежать на исходном столе."); return; }
            if (InZone(workZone, blank.transform.position)) { Message("Патрон должен быть свободен."); return; }
            if (Vector3.Distance(movingNode.localPosition, nodeHome) > 0.0001f)
            { Message("Узел станка должен быть в исходном положении."); return; }
            SetStage(Stage.Grab);
            return;
        }

        // «Далее» используется только в начале: остальные этапы подтверждаются действиями.
        Message("Кнопка «Далее» нужна только в начале. " + Hint(CurrentStage));
    }

    public void ToggleGuard()
    {
        if (!requireGuard) { Message("В этой модели станка ограждения нет."); return; }
        if (CurrentStage == Stage.Processing)
        {
            Error("Нельзя открывать ограждение во время обработки.");
            return;
        }

        SetGuard(!guardOpen);
        if (CurrentStage == Stage.Guard && !guardOpen) SetStage(Stage.Ready);
        else if (CurrentStage == Stage.Ready && guardOpen) SetStage(Stage.Guard);
        else Message(guardOpen ? "Ограждение открыто." : "Ограждение закрыто.");
    }

    public void StartCycle()
    {
        if (CurrentStage == Stage.Processing) { Error("Цикл уже идёт. Дождитесь окончания."); return; }
        if (CurrentStage != Stage.Ready || !placed || blank.IsHeld || !InZone(workZone, blank.transform.position))
        {
            Error("Пуск невозможен: сначала установите заготовку в патрон.");
            return;
        }
        if (requireGuard && guardOpen) { Error("Пуск невозможен: закройте ограждение."); return; }

        SetStage(Stage.Processing);
        cycle = StartCoroutine(ProcessCycle());
    }

    public void StopCycle()
    {
        if (CurrentStage != Stage.Processing) { Message("Цикл не запущен."); return; }

        StopCoroutine(cycle);
        cycle = null;
        movingNode.localPosition = nodeHome;
        SetStage(Stage.Ready);
        Message("Цикл остановлен, деталь не обработана. Нажмите «Пуск», чтобы начать заново.");
    }

    public void RestartTraining()
    {
        if (blank.IsHeld) { Message("Сначала отпустите заготовку."); return; }
        Restart();
    }

    // ------------------------------------------------ вызовы от заготовки

    public bool CanGrab()
    {
        if (CurrentStage == Stage.Grab || CurrentStage == Stage.Place) return true;
        if (CurrentStage == Stage.Output) return !requireGuard || guardOpen;
        return false;
    }

    public void NotifyGrab()
    {
        if (CurrentStage == Stage.Grab)
        {
            SetStage(Stage.Place);
        }
        else if (CurrentStage == Stage.Output)
        {
            collected = true;
            Message("Перенесите деталь в зону готовой продукции и отпустите.");
        }
    }

    // Попытка взять предмет на запрещённом этапе: подсказка без начисления ошибки.
    public void NotifyGrabDenied()
    {
        if (CurrentStage == Stage.Processing) Message("Во время обработки брать деталь нельзя.");
        else if (CurrentStage == Stage.Output) Message("Сначала откройте ограждение.");
        else Message("Сейчас брать заготовку нельзя. " + Hint(CurrentStage));
    }

    private void HandleRelease()
    {
        Vector3 center = blank.ReleasePosition;

        if (CurrentStage == Stage.Place)
        {
            if (InZone(workZone, center))
            {
                blank.Snap(workPoint);
                placed = true;
                SetStage(requireGuard && guardOpen ? Stage.Guard : Stage.Ready);
            }
            else
            {
                Error("Заготовка не установлена в патрон. Возьмите её и вставьте в патрон станка.");
            }
        }
        else if (CurrentStage == Stage.Output && collected)
        {
            if (processed && InZone(outputZone, center))
            {
                blank.Snap(outputPoint);
                SetStage(Stage.Done);
                float time = Time.time - startTime;
                resultText.text = $"Обучение пройдено. Время: {time:F0} с. Ошибочных действий: {errors}";
            }
            else
            {
                Error("Деталь не в зоне готовой продукции. Возьмите её и перенесите ещё раз.");
            }
        }
    }

    // ------------------------------------------------ обработка

    private IEnumerator ProcessCycle()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration));
            movingNode.localPosition = nodeHome + Vector3.right * travel * k;
            yield return null;
        }

        movingNode.localPosition = nodeHome;
        cycle = null;
        processed = true;
        blankRenderer.material.color = finishedColor;
        SetStage(Stage.Output);
    }

    // ------------------------------------------------ сброс и вспомогательное

    private void Restart()
    {
        if (cycle != null) { StopCoroutine(cycle); cycle = null; }
        movingNode.localPosition = nodeHome;
        placed = false;
        processed = false;
        collected = false;
        errors = 0;
        blank.ResetTo(blankHome);
        handledRelease = blank.ReleaseNumber;
        blankRenderer.material.color = rawColor;
        if (requireGuard) SetGuard(true); // в начале сценария ограждение открыто
        startTime = Time.time;
        resultText.text = "";
        SetStage(Stage.Intro);
    }

    // Центр предмета внутри объёма BoxCollider (в локальных координатах зоны).
    private static bool InZone(BoxCollider zone, Vector3 point)
    {
        Vector3 local = zone.transform.InverseTransformPoint(point) - zone.center;
        Vector3 half = zone.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    private void SetGuard(bool open)
    {
        guardOpen = open;
        if (guard != null)
            guard.localPosition = guardClosed + (open ? guardOpenOffset : Vector3.zero);
    }

    private void SetStage(Stage stage)
    {
        CurrentStage = stage;
        stepText.text = $"Этап {(int)stage}. {Title(stage)}";
        messageText.text = Hint(stage);
        if (counterText != null) counterText.text = $"{(int)stage} / 9";

        if (stageMarks != null)
            for (int i = 0; i < stageMarks.Length; i++)
            {
                int number = i + 1;
                stageMarks[i].color = number < (int)stage ? doneColor
                    : number == (int)stage ? currentColor : todoColor;
            }
    }

    private void Message(string text)
    {
        messageText.text = text;
    }

    private void Error(string text)
    {
        errors++;
        messageText.text = text;
    }

    private static string Title(Stage stage)
    {
        switch (stage)
        {
            case Stage.Intro: return "Ознакомление";
            case Stage.Check: return "Исходное состояние";
            case Stage.Grab: return "Захват заготовки";
            case Stage.Place: return "Установка";
            case Stage.Guard: return "Ограждение";
            case Stage.Ready: return "Запуск";
            case Stage.Processing: return "Обработка";
            case Stage.Output: return "Готовая продукция";
            default: return "Результат";
        }
    }

    private string Hint(Stage stage)
    {
        switch (stage)
        {
            case Stage.Intro: return "Осмотрите станок, стол с заготовкой и стол готовой продукции. Нажмите «Далее».";
            case Stage.Check: return "Проверьте: заготовка на исходном столе, патрон свободен, каретка в исходном положении. Нажмите «Далее».";
            case Stage.Grab: return "Возьмите заготовку с исходного стола.";
            case Stage.Place: return "Поднесите вал к патрону станка и отпустите - он зажмётся в кулачках.";
            case Stage.Guard: return "Закройте ограждение.";
            case Stage.Ready: return "Заготовка зажата в патроне. Нажмите «Пуск».";
            case Stage.Processing: return "Идёт обработка. Дождитесь остановки каретки.";
            case Stage.Output:
                return requireGuard && !guardOpen
                    ? "Обработка завершена. Откройте ограждение и возьмите деталь."
                    : "Обработка завершена. Возьмите готовую деталь.";
            default: return "Нажмите «Повторить», чтобы пройти ещё раз.";
        }
    }
}
