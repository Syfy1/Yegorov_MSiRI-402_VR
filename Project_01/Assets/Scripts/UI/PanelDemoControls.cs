using UnityEngine;

// Кнопки «Пуск» и «Стоп» в локальном учебном режиме (без POST API).
// Меняют надписи на панели и включают/выключают движение модели
// по координатам GET. Команды на сервер не отправляются.
public class PanelDemoControls : MonoBehaviour
{
    public MachinePanel panel;

    // Клиент GET: «Пуск» включает движение узлов модели, «Стоп» возвращает их в исходное положение.
    public CncJsonController client;

    private void Start()
    {
        panel.ShowDemoState(false);
        panel.ShowCommand("Локальный режим без POST");
    }

    public void StartMachine()
    {
        Debug.Log("Локальный Пуск");
        panel.ShowDemoState(true);
        panel.ShowCommand("Учебное нажатие Пуск");
        if (client != null) client.StartMovement();
    }

    public void StopMachine()
    {
        Debug.Log("Локальный Стоп");
        panel.ShowDemoState(false);
        panel.ShowCommand("Учебное нажатие Стоп");
        if (client != null) client.StopMovement();
    }
}
