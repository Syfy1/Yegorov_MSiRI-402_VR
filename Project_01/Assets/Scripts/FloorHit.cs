using UnityEngine;

public class FloorHit : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            Debug.Log(gameObject.name + " задел пол");
        }
    }
}
