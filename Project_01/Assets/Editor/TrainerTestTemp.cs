using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TrainerTestTemp
{
    [MenuItem("Tools/Test/Grab")]
    public static void Grab()
    {
        var t = Object.FindAnyObjectByType<MachineTrainer>();
        if (!t.CanGrab()) { t.NotifyGrabDenied(); Debug.Log("Захват запрещён"); return; }
        var b = t.blank;
        typeof(TrainingBlank).GetProperty("IsHeld").SetValue(b, true);
        b.GetComponent<Rigidbody>().isKinematic = true;
        t.NotifyGrab();
        Debug.Log("Тест: взят");
    }

    [MenuItem("Tools/Test/Release Work")]
    public static void ReleaseWork() => ReleaseAt(Object.FindAnyObjectByType<MachineTrainer>().workPoint.position);

    [MenuItem("Tools/Test/Release Output")]
    public static void ReleaseOutput() => ReleaseAt(Object.FindAnyObjectByType<MachineTrainer>().outputPoint.position);

    [MenuItem("Tools/Test/Release Home Table")]
    public static void ReleaseHome() => ReleaseAt(Object.FindAnyObjectByType<MachineTrainer>().blankHome.position);

    private static void ReleaseAt(Vector3 pos)
    {
        var b = Object.FindAnyObjectByType<MachineTrainer>().blank;
        b.transform.position = pos;
        typeof(TrainingBlank).GetMethod("Release", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(b, null);
        Debug.Log("Тест: отпущен в " + pos);
    }

    [MenuItem("Tools/Test/Start")] public static void Start() => Object.FindAnyObjectByType<MachineTrainer>().StartCycle();
    [MenuItem("Tools/Test/Stop")] public static void Stop() => Object.FindAnyObjectByType<MachineTrainer>().StopCycle();
    [MenuItem("Tools/Test/Restart")] public static void Restart() => Object.FindAnyObjectByType<MachineTrainer>().RestartTraining();
    [MenuItem("Tools/Test/Next")] public static void Next() => Object.FindAnyObjectByType<MachineTrainer>().NextStep();

    [MenuItem("Tools/Test/Log State")]
    public static void Log()
    {
        var t = Object.FindAnyObjectByType<MachineTrainer>();
        Debug.Log($"Этап {(int)t.CurrentStage} | {t.stepText.text} | {t.messageText.text} | {t.resultText.text} | узел {t.movingNode.localPosition} | вал {t.blank.transform.position}");
    }
}
