using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using Valve.VR.InteractionSystem;

// Нажатие кнопок панели мышью, когда шлем не подключён.
// В шлеме кнопки нажимает рука через UIElement, а этот скрипт ничего не делает.
// У каждой кнопки есть BoxCollider, по нему луч от камеры находит кнопку.
public class PanelMouseClick : MonoBehaviour
{
    public float maxDistance = 10f;

    private void Update()
    {
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess)
            return;
        if (!Input.GetMouseButtonDown(0))
            return;

        Camera camera = ActiveCamera();
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
            return;

        Button button = hit.collider.GetComponent<Button>();
        if (button != null && button.interactable && button.transform.IsChildOf(transform))
            button.onClick.Invoke();
    }

    // В сцене со SteamVR Player активна камера режима без шлема, иначе - Main Camera.
    private static Camera ActiveCamera()
    {
        if (Player.instance != null && Player.instance.hmdTransform != null)
        {
            Camera playerCamera = Player.instance.hmdTransform.GetComponent<Camera>();
            if (playerCamera != null && playerCamera.isActiveAndEnabled)
                return playerCamera;
        }
        return Camera.main;
    }
}
