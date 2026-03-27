using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    void LateUpdate()
    {
        // Giúp Canvas luôn xoay mặt về phía Camera của người chơi
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward, Camera.main.transform.rotation * Vector3.up);
        }
    }
}