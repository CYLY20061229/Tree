using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public GameManager gameManager;
    public float smoothSpeed = 6f;

    private float fixedX = 7.5f;
    private float zPos = -10f;

    void LateUpdate()
    {
        if (gameManager == null) return;

        Vector3 target = GetTargetPosition();
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * smoothSpeed);
    }

    Vector3 GetTargetPosition()
    {
        GridPos head;

        if (gameManager.currentTurn == GameManager.TurnPlayer.PlayerA)
        {
            head = gameManager.playerA.Head;
        }
        else
        {
            head = gameManager.playerB.Head;
        }

        float targetY = -head.y;

        // 给一点向下看的提前量
        targetY -= 4f;

        return new Vector3(fixedX, targetY, zPos);
    }
}