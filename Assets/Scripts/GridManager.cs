using UnityEngine;

public class GridManager : MonoBehaviour
{
    public enum CameraFitMode
    {
        FitAll,
        FillByHeight,
        FillByWidth
    }

    public int width = 21;
    public int height = 50;
    public float cellWorldSize = 4f;
    public int stoneCount = 42;
    public int resourceCount = 42;
    public int stoneMinSpacing = 2;
    public int resourceMinSpacing = 2;
    public bool autoFitMainCamera = true;
    public float cameraPadding = 0.5f;
    [Tooltip("乘以自动算出的正交 size：越小镜头越近（画面放大、地图在屏上显得更大），越大越远")]
    [Range(0.3f, 1.2f)]
    public float cameraFillScale = 0.42f;
    [Tooltip("相对棋盘中心的水平/垂直偏移（世界单位），用于微调构图居中")]
    public Vector2 cameraOffset = new Vector2(-6f, 0f);
    [Tooltip("开局镜头在竖直方向整体上移（世界单位），数值越大越靠上，便于同时看到上方两条树根")]
    public float cameraStartVerticalLift = 16f;
    public CameraFitMode cameraFitMode = CameraFitMode.FillByHeight;
    public bool followDeepestRoot = true;
    [Tooltip("深度超过此行后才开始把镜头向更深处（跟随玩家向下）拉动")]
    public float followStartRow = 2f;
    [Tooltip("随最深根向下跟随的强度，越大镜头越跟手")]
    [Range(0f, 1f)]
    public float followStrength = 0.36f;
    [Tooltip("在 followStrength 之外额外把镜头向网格深处带一点（世界单位/行，乘深度差）")]
    [Range(0f, 0.5f)]
    public float followExtraDownPerRow = 0.12f;
    [Tooltip("镜头跟随响应速度，越小越跟手（但可能略抖）")]
    [Range(0.02f, 0.35f)]
    public float followSmoothTime = 0.065f;
    [Tooltip("与 GridView.worldOffset.y 一致，用于把格行换算成世界 Y 以框住树根在画面内")]
    public float gridWorldOffsetY = 0f;
    [Tooltip("根深世界坐标相对镜头下缘的安全边距（世界单位），越大越不容易把根甩出画面")]
    [Min(0f)]
    public float followFrameMarginWorld = 1.2f;

    public CellType[,] cells;
    float baseCameraY;
    float cameraVelocityY;
    GameManager cachedGameManager;

    public void InitGrid()
    {
        cells = new CellType[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y] = CellType.Soil;
            }
        }

        GenerateStones();
        GenerateResources();
        FitMainCamera();
    }

    void LateUpdate()
    {
        if (!autoFitMainCamera || !followDeepestRoot)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (cachedGameManager == null)
        {
            cachedGameManager = FindObjectOfType<GameManager>();
        }

        GameManager gameManager = cachedGameManager;
        if (gameManager == null || gameManager.playerA.body.Count == 0 || gameManager.playerB.body.Count == 0)
        {
            return;
        }

        int ya = gameManager.playerA.Head.y;
        int yb = gameManager.playerB.Head.y;
        float deepestRow = Mathf.Max(ya, yb);
        float shallowRow = Mathf.Min(ya, yb);

        float extraRows = Mathf.Max(0f, deepestRow - followStartRow);
        float downPull = extraRows * cellWorldSize * followStrength
            + extraRows * cellWorldSize * followExtraDownPerRow;
        float linearTargetY = baseCameraY - downPull;

        float ortho = cam.orthographicSize;
        float margin = followFrameMarginWorld;

        // 与 GridView 一致：行越大，世界 Y 越负（越深）
        float deepHeadWorldY = -deepestRow * cellWorldSize + gridWorldOffsetY;
        float shallowHeadWorldY = -shallowRow * cellWorldSize + gridWorldOffsetY;

        // 深端根尖不要低于镜头下缘：cam.y - ortho <= deepHeadWorldY - margin  => cam.y <= deepHeadWorldY + ortho - margin
        float maxCamYShowDeep = deepHeadWorldY + ortho - margin;
        // 浅端根尖不要高于镜头上缘：cam.y + ortho >= shallowHeadWorldY + margin => cam.y >= shallowHeadWorldY - ortho + margin
        float minCamYShowShallow = shallowHeadWorldY - ortho + margin;

        float targetY = linearTargetY;
        targetY = Mathf.Min(targetY, maxCamYShowDeep);
        targetY = Mathf.Max(targetY, minCamYShowShallow);

        Vector3 pos = cam.transform.position;
        pos.y = Mathf.SmoothDamp(pos.y, targetY, ref cameraVelocityY, followSmoothTime);
        cam.transform.position = pos;
    }

    void GenerateStones()
    {
        int placed = 0;
        int attempts = 0;

        while (placed < stoneCount && attempts < stoneCount * 40)
        {
            int x = Random.Range(0, width);
            int y = Random.Range(2, height - 2);
            attempts++;

            if (!CanPlaceAt(x, y))
            {
                continue;
            }

            if (cells[x, y] != CellType.Soil || !HasMinSpacing(x, y, stoneMinSpacing, false))
            {
                continue;
            }

            cells[x, y] = CellType.Stone;
            placed++;
        }
    }

    void GenerateResources()
    {
        int placed = 0;
        int attempts = 0;

        while (placed < resourceCount && attempts < resourceCount * 40)
        {
            int x = Random.Range(0, width);
            int y = Random.Range(2, height - 2);
            attempts++;

            if (!CanPlaceAt(x, y) || cells[x, y] != CellType.Soil || !HasMinSpacing(x, y, resourceMinSpacing, true))
            {
                continue;
            }

            int r = Random.Range(0, 4);
            cells[x, y] = (CellType)((int)CellType.ResourceN + r);
            placed++;
        }
    }

    public bool IsInside(GridPos pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    public bool IsStone(GridPos pos)
    {
        return cells[pos.x, pos.y] == CellType.Stone;
    }

    public bool IsResource(GridPos pos)
    {
        CellType t = cells[pos.x, pos.y];
        return t == CellType.ResourceN ||
               t == CellType.ResourceP ||
               t == CellType.ResourceK ||
               t == CellType.ResourceW;
    }

    /// <summary>
    /// 左 1/4 仅左玩家，右 1/4 仅右玩家，中间 1/2 双方可进入。
    /// </summary>
    public bool IsCellAllowedForPlayer(GridPos pos, bool isPlayerA)
    {
        if (!IsInside(pos))
        {
            return false;
        }

        int q = Mathf.Max(1, width / 4);
        if (isPlayerA)
        {
            return pos.x < width - q;
        }

        return pos.x >= q;
    }

    bool CanPlaceAt(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return false;
        }

        if ((x <= 3 || x >= width - 4) && y <= 2)
        {
            return false;
        }

        return true;
    }

    bool HasMinSpacing(int x, int y, int minSpacing, bool avoidAnyOccupiedCell)
    {
        if (minSpacing <= 0)
        {
            return true;
        }

        for (int checkX = Mathf.Max(0, x - minSpacing); checkX <= Mathf.Min(width - 1, x + minSpacing); checkX++)
        {
            for (int checkY = Mathf.Max(0, y - minSpacing); checkY <= Mathf.Min(height - 1, y + minSpacing); checkY++)
            {
                if (checkX == x && checkY == y)
                {
                    continue;
                }

                int distance = Mathf.Abs(checkX - x) + Mathf.Abs(checkY - y);
                if (distance > minSpacing)
                {
                    continue;
                }

                CellType cell = cells[checkX, checkY];
                if (avoidAnyOccupiedCell)
                {
                    if (cell != CellType.Soil)
                    {
                        return false;
                    }
                }
                else if (cell == CellType.Stone)
                {
                    return false;
                }
            }
        }

        return true;
    }

    void FitMainCamera()
    {
        if (!autoFitMainCamera)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.orthographic = true;

        float aspect = Mathf.Max(cam.aspect, 0.01f);
        float halfWidth = width * cellWorldSize * 0.5f;
        float halfHeight = height * cellWorldSize * 0.5f;
        float verticalSizeForHeight = halfHeight + cameraPadding;
        float verticalSizeForWidth = halfWidth / aspect + cameraPadding;

        if (cameraFitMode == CameraFitMode.FillByHeight)
        {
            cam.orthographicSize = verticalSizeForHeight;
        }
        else if (cameraFitMode == CameraFitMode.FillByWidth)
        {
            cam.orthographicSize = verticalSizeForWidth;
        }
        else
        {
            cam.orthographicSize = Mathf.Max(verticalSizeForHeight, verticalSizeForWidth);
        }

        cam.orthographicSize *= cameraFillScale;

        Vector3 pos = cam.transform.position;
        pos.x = (width - 1) * cellWorldSize * 0.5f + cameraOffset.x;
        pos.y = -(height - 1) * cellWorldSize * 0.5f + cameraOffset.y + cameraStartVerticalLift;
        pos.z = -10f;
        cam.transform.position = pos;
        baseCameraY = pos.y;
        cameraVelocityY = 0f;
    }
}
