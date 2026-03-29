using UnityEngine;

public class GridManager : MonoBehaviour
{
    public enum CameraFitMode
    {
        FitAll,
        FillByHeight,
        FillByWidth
    }

    public int width = 50;
    public int height = 21;
    public float cellWorldSize = 4f;
    public int stoneCount = 42;
    public int resourceCount = 42;
    public int stoneMinSpacing = 2;
    public int resourceMinSpacing = 2;
    public bool autoFitMainCamera = true;
    public float cameraPadding = 0.5f;
    [Range(0.5f, 1f)] public float cameraFillScale = 0.72f;
    public Vector2 cameraOffset = new Vector2(-10f, 0f);
    public CameraFitMode cameraFitMode = CameraFitMode.FillByHeight;
    public bool followDeepestRoot = true;
    public float followStartRow = 5f;
    public float followStrength = 0.22f;
    public float followSmoothTime = 0.18f;

    public CellType[,] cells;
    float baseCameraY;
    float cameraVelocityY;

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

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null || gameManager.playerA.body.Count == 0 || gameManager.playerB.body.Count == 0)
        {
            return;
        }

        float deepestRow = Mathf.Max(gameManager.playerA.Head.y, gameManager.playerB.Head.y);
        float extraRows = Mathf.Max(0f, deepestRow - followStartRow);
        float targetY = baseCameraY - extraRows * cellWorldSize * followStrength;

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
        pos.y = -(height - 1) * cellWorldSize * 0.5f + cameraOffset.y;
        pos.z = -10f;
        cam.transform.position = pos;
        baseCameraY = pos.y;
        cameraVelocityY = 0f;
    }
}
