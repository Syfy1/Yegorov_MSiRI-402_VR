using UnityEngine;
using Valve.VR.InteractionSystem;

// Заготовка учебного сценария: захват и отпускание рукой SteamVR.
// Перед захватом спрашивает у MachineTrainer, разрешён ли он на текущем этапе.
// Заменяет Throwable: скорость броска не передаётся, после отпускания действует обычная гравитация.
[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class TrainingBlank : MonoBehaviour
{
    public MachineTrainer trainer;

    // Предмет прикрепляется к руке; другая рука его не перехватывает, пока он удерживается.
    private const Hand.AttachmentFlags AttachFlags =
        Hand.AttachmentFlags.ParentToHand |
        Hand.AttachmentFlags.DetachFromOtherHand |
        Hand.AttachmentFlags.TurnOnKinematic;

    private Interactable interactable;
    private Rigidbody body;

    public bool IsHeld { get; private set; }
    public float ReleaseTime { get; private set; }
    public int ReleaseNumber { get; private set; }

    // Где был центр предмета в момент отпускания: по нему проверяется попадание в зону,
    // даже если после отпускания предмет успел немного упасть.
    public Vector3 ReleasePosition { get; private set; }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        body = GetComponent<Rigidbody>();
    }

    // Рука наведена на предмет: захват только если сценарий его разрешает.
    private void HandHoverUpdate(Hand hand)
    {
        GrabTypes grab = hand.GetGrabStarting();
        if (grab == GrabTypes.None || IsHeld)
            return;

        if (trainer == null || !trainer.CanGrab())
        {
            if (trainer != null) trainer.NotifyGrabDenied();
            return;
        }

        hand.HoverLock(interactable);
        hand.AttachObject(gameObject, grab, AttachFlags);
    }

    // Предмет в руке: отпускаем, когда закончилось действие захвата.
    private void HandAttachedUpdate(Hand hand)
    {
        if (hand.IsGrabEnding(gameObject))
        {
            hand.DetachObject(gameObject);
            hand.HoverUnlock(interactable);
        }
    }

    private void OnAttachedToHand(Hand hand)
    {
        IsHeld = true;
        if (trainer != null) trainer.NotifyGrab();
    }

    private void OnDetachedFromHand(Hand hand)
    {
        Release();
    }

    // Общее отпускание: обычная физика без скорости броска, номер и время события.
    private void Release()
    {
        ReleasePosition = transform.position;
        IsHeld = false;
        transform.SetParent(null, true);
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        ReleaseTime = Time.time;
        ReleaseNumber++;
    }

    // Точная установка в точку: предмет становится кинематическим и не сдвигается.
    public void Snap(Transform point)
    {
        body.isKinematic = true;
        MoveTo(point.position, point.rotation);
    }

    // Возврат сырой заготовки на исходный стол при «Повторить».
    public void ResetTo(Transform home)
    {
        IsHeld = false;
        transform.SetParent(null, true);
        body.isKinematic = true;
        MoveTo(home.position, home.rotation);
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void MoveTo(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        body.position = position;
        body.rotation = rotation;
    }
}
