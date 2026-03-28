using UnityEngine;

public class GridManager : MonoBehaviour
{
    // 地图真实宽高（会根据 mapData 自动计算）
    private int width;
    private int height;

    // 游戏运行时真正使用的地图数据
    private CellType[,] grid;

    // 两个玩家出生点
    public Vector2Int player1Start;
    public Vector2Int player2Start;

    // 对外只读
    public int Width => width;
    public int Height => height;

    // 0 = 空
    // 1 = 玩家1起点
    // 2 = 玩家2起点
    // 3 = 资源
    private int[,] mapData =
    {
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,0,3,0,0,0,0,3,0,0,0,0,3,0,0,0,0,3,0,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,0,0,0,0,3,0,0,0,0,0,0,0,3,0,0,0,0,0,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
        {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}
    };

    void Awake()
    {
        InitializeGridFromMapData();
    }

    private void InitializeGridFromMapData()
    {
        // 行数 = 高，列数 = 宽
        height = mapData.GetLength(0);
        width = mapData.GetLength(1);

        grid = new CellType[width, height];

        bool foundP1 = false;
        bool foundP2 = false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int value = mapData[y, x];

                switch (value)
                {
                    case 0:
                        grid[x, y] = CellType.Empty;
                        break;

                    case 1:
                        grid[x, y] = CellType.Player1Root;
                        player1Start = new Vector2Int(x, y);
                        foundP1 = true;
                        break;

                    case 2:
                        grid[x, y] = CellType.Player2Root;
                        player2Start = new Vector2Int(x, y);
                        foundP2 = true;
                        break;

                    case 3:
                        grid[x, y] = CellType.Resource;
                        break;

                    default:
                        Debug.LogWarning($"未知地图值: {value}，位置 ({x}, {y})，已按 Empty 处理");
                        grid[x, y] = CellType.Empty;
                        break;
                }
            }
        }

        if (!foundP1)
        {
            Debug.LogError("地图里没有玩家1起点（数字 1）");
        }

        if (!foundP2)
        {
            Debug.LogError("地图里没有玩家2起点（数字 2）");
        }
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public CellType GetCell(int x, int y)
    {
        if (!IsInside(x, y))
            return CellType.Empty;

        return grid[x, y];
    }

    public void SetCell(int x, int y, CellType type)
    {
        if (!IsInside(x, y))
            return;

        grid[x, y] = type;
    }

    public bool IsEmpty(int x, int y)
    {
        if (!IsInside(x, y))
            return false;

        return grid[x, y] == CellType.Empty;
    }

    public bool IsResource(int x, int y)
    {
        if (!IsInside(x, y))
            return false;

        return grid[x, y] == CellType.Resource;
    }

    public bool IsPlayerRoot(int x, int y)
    {
        if (!IsInside(x, y))
            return false;

        return grid[x, y] == CellType.Player1Root || grid[x, y] == CellType.Player2Root;
    }

    public void ClearCell(int x, int y)
    {
        if (!IsInside(x, y))
            return;

        grid[x, y] = CellType.Empty;
    }
}