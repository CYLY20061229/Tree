using UnityEngine;

public class RootPlayer : MonoBehaviour
{
    public GridManager gridManager;
    public CellType rootType;

    public KeyCode upKey;
    public KeyCode downKey;
    public KeyCode leftKey;
    public KeyCode rightKey;

    public int score = 0;

    private int headX;
    private int headY;

    void Start()
    {
        if (gridManager == null) return;

        if (rootType == CellType.Player1Root)
        {
            headX = gridManager.player1Start.x;
            headY = gridManager.player1Start.y;
        }
        else if (rootType == CellType.Player2Root)
        {
            headX = gridManager.player2Start.x;
            headY = gridManager.player2Start.y;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(upKey)) TryGrow(0, 1);
        if (Input.GetKeyDown(downKey)) TryGrow(0, -1);
        if (Input.GetKeyDown(leftKey)) TryGrow(-1, 0);
        if (Input.GetKeyDown(rightKey)) TryGrow(1, 0);
    }

    void TryGrow(int dx, int dy)
    {
        if (gridManager == null) return;

        int nextX = headX + dx;
        int nextY = headY + dy;

        if (!gridManager.IsInside(nextX, nextY)) return;

        CellType target = gridManager.GetCell(nextX, nextY);

        if (target == CellType.Player1Root || target == CellType.Player2Root)
            return;

        if (target == CellType.Resource)
        {
            score += 1;
            Debug.Log(gameObject.name + " score: " + score);
        }

        headX = nextX;
        headY = nextY;
        gridManager.SetCell(headX, headY, rootType);
    }
}