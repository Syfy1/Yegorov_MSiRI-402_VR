using UnityEngine;
using Valve.VR.InteractionSystem;

// Ходьба по полу без шлема: W A S D или стрелки, Shift - быстрее,
// зажатая правая кнопка мыши - поворот головы.
// Висит на камере режима без шлема (FallbackObjects в Player).
// Когда шлем подключён, этот объект выключен и не мешает VR.
public class DesktopWalk : MonoBehaviour
{
    public float walkSpeed = 1.5f;     // м/с
    public float runSpeed = 3f;        // м/с с Shift
    public float eyeHeight = 1.6f;     // высота глаз над полом, м
    public float bodyRadius = 0.25f;   // радиус «тела» для проверки препятствий, м

    private Vector3 mouseStart;
    private Vector3 angleStart;

    private void Start()
    {
        // Штатная камера Valve летает (Q/E), здесь нужна ходьба по полу.
        var flying = GetComponent<FallbackCameraController>();
        if (flying != null)
            flying.enabled = false;

        Vector3 p = transform.position;
        p.y = eyeHeight;
        transform.position = p;
    }

    private void Update()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        Vector3 input = forward * Input.GetAxisRaw("Vertical") + right * Input.GetAxisRaw("Horizontal");
        Vector3 move = Vector3.ClampMagnitude(input, 1f) * speed * Time.deltaTime;

        if (move.sqrMagnitude > 0f && !Blocked(move))
        {
            Vector3 p = transform.position + move;
            p.y = eyeHeight;
            transform.position = p;
        }

        // Поворот: зажать правую кнопку мыши.
        if (Input.GetMouseButtonDown(1))
        {
            mouseStart = Input.mousePosition;
            angleStart = transform.localEulerAngles;
        }
        if (Input.GetMouseButton(1))
        {
            Vector3 shift = Input.mousePosition - mouseStart;
            transform.localEulerAngles = angleStart +
                new Vector3(-shift.y * 180f / Screen.height, shift.x * 360f / Screen.width, 0f);
        }
    }

    // Станок, стол и опоры не дают пройти насквозь.
    private bool Blocked(Vector3 move)
    {
        Vector3 bottom = new Vector3(transform.position.x, 0.3f, transform.position.z);
        Vector3 top = new Vector3(transform.position.x, eyeHeight, transform.position.z);
        foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, bodyRadius, move.normalized,
                     move.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Пол и предметы, которые можно взять, препятствием не считаются.
            if (hit.rigidbody == null && hit.normal.y < 0.5f)
                return true;
        }
        return false;
    }
}
