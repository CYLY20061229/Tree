using UnityEngine;

/// <summary>
/// 若场景中存在 <see cref="GridManager"/> 且其自动适配相机开启，则由 GridManager 负责镜头位置，本组件不生效。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public GameManager gameManager;
    public float smoothSpeed = 6f;

    float fixedX = 7.5f;
    float zPos = -10f;

    void Start()
    {
        GridManager grid = FindObjectOfType<GridManager>();
        if (grid != null && grid.autoFitMainCamera)
        {
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (gameManager == null)
        {
            return;
        }

        Vector3 target = GetTargetPosition();
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * smoothSpeed);
    }

    Vector3 GetTargetPosition()
    {
        int ya = gameManager.playerA.body.Count > 0 ? gameManager.playerA.Head.y : 0;
        int yb = gameManager.playerB.body.Count > 0 ? gameManager.playerB.Head.y : 0;
        int deepestY = Mathf.Max(ya, yb);

        float targetY = -deepestY;

        // 给一点向下看的提前量
        targetY -= 4f;

        return new Vector3(fixedX, targetY, zPos);
    }
}