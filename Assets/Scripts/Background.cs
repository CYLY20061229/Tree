using UnityEngine;

/// <summary>
/// 背景跟随相机；可选按正交视口自动缩放，使贴图始终盖住整块屏幕。
/// </summary>
public class BackgroundFollowCamera : MonoBehaviour
{
    public Camera targetCamera;
    [Tooltip("按当前正交相机的可见范围缩放 Sprite，铺满画面（略放大避免露边）")]
    public bool scaleToFillOrthographicView = true;
    [Tooltip("相对视口边缘的放大系数，略大于 1 可避免黑边")]
    [Min(1f)]
    public float fillPadding = 1.06f;

    SpriteRenderer _sr;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponentInParent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        bool childOfCam = transform.parent != null && transform.parent == targetCamera.transform;
        if (!childOfCam)
        {
            Vector3 pos = targetCamera.transform.position;
            transform.position = new Vector3(pos.x, pos.y, 5f);
        }

        if (!scaleToFillOrthographicView || _sr == null || _sr.sprite == null || !targetCamera.orthographic)
        {
            return;
        }

        float needH = 2f * targetCamera.orthographicSize * fillPadding;
        float needW = needH * Mathf.Max(targetCamera.aspect, 0.01f);
        Vector3 bs = _sr.sprite.bounds.size;
        if (bs.y < 0.0001f || bs.x < 0.0001f)
        {
            return;
        }

        float s = Mathf.Max(needW / bs.x, needH / bs.y);
        transform.localScale = new Vector3(s, s, 1f);
    }
}