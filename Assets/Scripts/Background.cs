using UnityEngine;

public class BackgroundFollowCamera : MonoBehaviour
{
    public Camera targetCamera;

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 pos = targetCamera.transform.position;
        transform.position = new Vector3(pos.x, pos.y, 5f);
    }
}