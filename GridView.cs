using UnityEngine;

public class GridView : MonoBehaviour
{
    public GridManager gridManager;
    public GameObject cellPrefab;

    private GameObject[,] cellObjects;

    void Start()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridView 没有连接 GridManager");
            return;
        }

        if (cellPrefab == null)
        {
            Debug.LogError("GridView 没有连接 CellPrefab");
            return;
        }

        int width = gridManager.Width;
        int height = gridManager.Height;

        cellObjects = new GameObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x, y, 0);
                GameObject cell = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                cell.name = $"Cell_{x}_{y}";
                cellObjects[x, y] = cell;
            }
        }

        RefreshAll();
    }

    void Update()
    {
        if (gridManager == null || cellObjects == null) return;
        RefreshAll();
    }

    void RefreshAll()
    {
        for (int x = 0; x < gridManager.Width; x++)
        {
            for (int y = 0; y < gridManager.Height; y++)
            {
                UpdateCellView(x, y);
            }
        }
    }
    bool IsSameRoot(int x, int y, CellType myType)
    {
        if (!gridManager.IsInside(x, y)) return false;
        return gridManager.GetCell(x, y) == myType;
    }
    void UpdateCellView(int x, int y)
    {
        GameObject cell = cellObjects[x, y];
        if (cell == null) return;

        SpriteRenderer sr = cell.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        Transform t = cell.transform;
        CellType type = gridManager.GetCell(x, y);

        // 默认
        t.localScale = new Vector3(0.7f, 0.7f, 1f);
        t.rotation = Quaternion.identity;

        if (type == CellType.Empty)
        {
            sr.color = new Color(0, 0, 0, 0);
            return;
        }

        if (type == CellType.Resource)
        {
            sr.color = new Color(0.9f, 0.8f, 0.2f);
            t.localScale = new Vector3(0.5f, 0.5f, 1f);
            return;
        }

        if (type == CellType.Player1Root || type == CellType.Player2Root)
        {
            bool up = IsSameRoot(x, y + 1, type);
            bool down = IsSameRoot(x, y - 1, type);
            bool left = IsSameRoot(x - 1, y, type);
            bool right = IsSameRoot(x + 1, y, type);

            int connections = 0;
            if (up) connections++;
            if (down) connections++;
            if (left) connections++;
            if (right) connections++;

            if (type == CellType.Player1Root)
                sr.color = new Color(0.45f, 0.32f, 0.22f);
            else
                sr.color = new Color(0.35f, 0.22f, 0.18f);

            // 单独一格
            if (connections == 0)
            {
                t.localScale = new Vector3(0.55f, 0.55f, 1f);
            }
            // 根尖
            else if (connections == 1)
            {
                t.localScale = new Vector3(0.5f, 0.5f, 1f);
            }
            // 两个连接
            else if (connections == 2)
            {
                // 横线
                if (left && right)
                {
                    t.localScale = new Vector3(1.2f, 0.42f, 1f);
                }
                // 竖线
                else if (up && down)
                {
                    t.localScale = new Vector3(0.42f, 1.2f, 1f);
                }
                // 拐角
                else
                {
                    t.localScale = new Vector3(0.8f, 0.8f, 1f);
                }
            }
            // 三向连接
            else if (connections == 3)
            {
                t.localScale = new Vector3(0.9f, 0.9f, 1f);
            }
            // 四向连接
            else if (connections == 4)
            {
                t.localScale = new Vector3(1.0f, 1.0f, 1f);
            }
        }
    }
}