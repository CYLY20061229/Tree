using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 贴图「大小」由 <see cref="GridManager.cellWorldSize"/> 与世界缩放相乘决定。
/// 严格方格模式：<b>strictTileScale</b> 控制土/石/氮磷钾水格子，<b>strictRootSegmentScale</b> 控制树根每节（&lt;1 则比单格小）。
/// 非严格模式：<b>cellVisualScale</b>、<b>featureVisualScale</b>、<b>rootNodeScale</b>（树根节点）等。
/// </summary>
public class GridView : MonoBehaviour
{
    public GameManager gameManager;
    public GridManager gridManager;
    public Sprite soilSprite;
    public Sprite stoneSprite;
    public Sprite resourceNSprite;
    public Sprite resourcePSprite;
    public Sprite resourceKSprite;
    public Sprite resourceWSprite;
    public Sprite playerARootSprite;
    public Sprite playerBRootSprite;
    public Sprite playerAHeadSprite;
    public Sprite playerBHeadSprite;
    public Sprite rootVariant1Sprite;
    public Sprite rootVariant2Sprite;
    [Range(0f, 1f)] public float soilAlpha = 0f;
    public Color soilTint = new Color(0.12f, 0.08f, 0.05f, 0.18f);

    [Header("网格在世界中的位置")]
    [Tooltip("与 GridManager.cameraOffset 通常保持一致，用于和主相机对齐")]
    public Vector2 worldOffset = new Vector2(-6f, 0f);
    [Tooltip("在 worldOffset 之上再平移整块棋盘（世界单位）")]
    public Vector2 gridExtraOffset = Vector2.zero;
    [Tooltip("仅偏移树根/连接件绘制，不影响格子底图")]
    public Vector2 rootDrawOffset = Vector2.zero;

    [Header("深度 Z（越小越靠近相机）")]
    public float zCells = 0f;
    public float zRootConnectors = -0.11f;
    public float zRootElbows = -0.105f;
    public float zRootNodes = -0.12f;

    [Header("严格方格模式（与 GridManager 一格一 CellType 对齐）")]
    [Tooltip("开启后：土/石/氮磷钾水每格一格贴图、树根每节一格、无抖动/无连接条；碰撞在 GameManager 里本来就是按格")]
    public bool strictSquareGridMode = true;
    [Range(0.92f, 1f)]
    [Tooltip("严格模式下格子与资源贴图缩放（1=与格子完全贴合）")]
    public float strictTileScale = 1f;
    [Range(0.25f, 1.2f)]
    [Tooltip("严格模式下树根每节贴图相对一格的缩放：1=贴满一格，小于 1 则每节更小（仍占逻辑一格）")]
    public float strictRootSegmentScale = 1f;
    [Tooltip("严格模式下根尖略大于身体，设为 1 则与身体同大")]
    public float strictHeadScaleMultiplier = 1.08f;

    [Header("网格线（可选）")]
    public bool showGridLines = true;
    public Color gridLineColor = new Color(1f, 1f, 1f, 0.12f);
    [Tooltip("线宽，单位约为世界单位，会与 cellWorldSize 相乘")]
    public float gridLineWidth = 0.03f;
    public float zGridLines = 0.001f;

    public float cellVisualScale = 1.06f;
    public float featureVisualScale = 1.18f;
    public bool useSmoothRoots = true;
    public float rootThickness = 0.42f;
    [Tooltip("非严格平滑根：每节 Sprite 相对一格的缩放，调小则树根贴图更小")]
    [Range(0.2f, 1.5f)]
    public float rootNodeScale = 0.58f;
    public float headScaleMultiplier = 1.2f;
    [Range(0f, 1f)] public float tailThicknessFactor = 0.72f;
    [Range(0f, 1f)] public float rootJitter = 0.1f;
    public Color playerAHeadTint = new Color(0.86f, 0.68f, 0.42f, 1f);
    public Color playerATailTint = new Color(0.52f, 0.34f, 0.2f, 1f);
    public Color playerBHeadTint = new Color(0.42f, 0.64f, 0.46f, 1f);
    public Color playerBTailTint = new Color(0.18f, 0.34f, 0.24f, 1f);
    private Sprite fallbackSquareSprite;
    private Sprite fallbackCircleSprite;
    private Dictionary<string, GameObject> cells = new Dictionary<string, GameObject>();
    private List<GameObject> playerBlocks = new List<GameObject>();
    private bool created = false;
    private GameObject gridLinesRoot;
    private readonly List<LineRenderer> gridLineVertical = new List<LineRenderer>();
    private readonly List<LineRenderer> gridLineHorizontal = new List<LineRenderer>();

    float CellSize => gridManager != null ? gridManager.cellWorldSize : 1f;

    void Start()
    {
        fallbackSquareSprite = CreateFallbackSprite();
        fallbackCircleSprite = CreateCircleSprite();
    }
    void Update()
    {
        if (gridManager == null || gameManager == null) return;
        if (gridManager.cells == null) return;

        if (!created)
        {
            CreateGridObjects();
            created = true;
            gameManager.viewDirty = true;
        }

        if (gameManager.viewDirty)
        {
            RefreshGridObjects();
            gameManager.viewDirty = false;
        }
    }

    void OnDestroy()
    {
        if (gridLinesRoot != null)
        {
            Destroy(gridLinesRoot);
        }
    }

    void CreateGridObjects()
    {
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                GameObject cell = new GameObject($"Cell_{x}_{y}");
                cell.transform.SetParent(transform);
                cell.transform.position = GridToWorldPosition(x, y, zCells);
                cell.transform.localScale = Vector3.one * cellVisualScale * CellSize;
                cell.AddComponent<SpriteRenderer>();

                cells[$"{x}_{y}"] = cell;
            }
        }
    }

    void RefreshGridObjects()
    {
        foreach (var kv in cells)
        {
            string[] parts = kv.Key.Split('_');
            int x = int.Parse(parts[0]);
            int y = int.Parse(parts[1]);

            kv.Value.transform.position = GridToWorldPosition(x, y, zCells);

            SpriteRenderer sr = kv.Value.GetComponent<SpriteRenderer>();
            ApplyCellVisual(sr, gridManager.cells[x, y]);
        }

        foreach (var obj in playerBlocks)
        {
            if (obj != null) Destroy(obj);
        }
        playerBlocks.Clear();

        DrawRoot(gameManager.playerA.body, "PlayerA_Root", playerARootSprite, playerAHeadSprite, playerAHeadTint, playerATailTint);
        DrawRoot(gameManager.playerB.body, "PlayerB_Root", playerBRootSprite, playerBHeadSprite, playerBHeadTint, playerBTailTint);

        RefreshGridLines();
    }

    void ApplyCellVisual(SpriteRenderer spriteRenderer, CellType cellType)
    {
        Sprite sprite = null;
        Color fallbackColor = Color.black;

        switch (cellType)
        {
            case CellType.Soil:
                sprite = soilSprite;
                fallbackColor = soilTint;
                break;
            case CellType.Stone:
                sprite = stoneSprite;
                fallbackColor = Color.gray;
                break;
            case CellType.ResourceN:
                sprite = resourceNSprite;
                fallbackColor = Color.blue;
                break;
            case CellType.ResourceP:
                sprite = resourcePSprite;
                fallbackColor = Color.red;
                break;
            case CellType.ResourceK:
                sprite = resourceKSprite;
                fallbackColor = Color.yellow;
                break;
            case CellType.ResourceW:
                sprite = resourceWSprite;
                fallbackColor = Color.cyan;
                break;
        }

        spriteRenderer.sprite = sprite != null ? sprite : fallbackSquareSprite;
        if (strictSquareGridMode)
        {
            float s = strictTileScale * CellSize;
            spriteRenderer.transform.localScale = Vector3.one * s;
            if (cellType == CellType.Soil)
            {
                spriteRenderer.color = sprite != null
                    ? new Color(soilTint.r, soilTint.g, soilTint.b, soilAlpha)
                    : fallbackColor;
            }
            else
            {
                spriteRenderer.color = sprite != null ? Color.white : fallbackColor;
            }

            return;
        }

        if (cellType == CellType.Soil)
        {
            spriteRenderer.transform.localScale = Vector3.one * cellVisualScale * CellSize;
            spriteRenderer.color = sprite != null
                ? new Color(soilTint.r, soilTint.g, soilTint.b, soilAlpha)
                : fallbackColor;
        }
        else
        {
            spriteRenderer.transform.localScale = Vector3.one * featureVisualScale * CellSize;
            spriteRenderer.color = sprite != null ? Color.white : fallbackColor;
        }
    }

    void ApplyPlayerVisual(SpriteRenderer spriteRenderer, Sprite sprite, Color fallbackColor)
    {
        spriteRenderer.sprite = sprite != null ? sprite : fallbackCircleSprite;
        spriteRenderer.color = sprite != null ? Color.white : fallbackColor;
        spriteRenderer.sortingOrder = 1;
    }

    void DrawRoot(List<GridPos> body, string objectName, Sprite bodySprite, Sprite headSprite, Color headColor, Color tailColor)
    {
        if (body == null || body.Count == 0)
        {
            return;
        }

        if (strictSquareGridMode)
        {
            DrawRootStrictSquare(body, objectName, bodySprite, headSprite, headColor, tailColor);
            return;
        }

        for (int i = 0; i < body.Count; i++)
        {
            float t = body.Count <= 1 ? 0f : (float)i / (body.Count - 1);
            Color tint = Color.Lerp(headColor, tailColor, t);
            Sprite segmentSprite = GetBodySpriteForIndex(i, bodySprite);
            CreateRootNode(objectName, body, i, segmentSprite, headSprite, tint, i == 0);

            if (useSmoothRoots && i > 0 && i < body.Count - 1)
            {
                CreateRootElbow(objectName, body, i, segmentSprite, tint);
            }

            if (!useSmoothRoots || i >= body.Count - 1)
            {
                continue;
            }

            float nextT = body.Count <= 1 ? 0f : (float)(i + 1) / (body.Count - 1);
            Color connectorTint = Color.Lerp(headColor, tailColor, (t + nextT) * 0.5f);
            CreateRootConnector(objectName, body, i, GetBodySpriteForIndex(i, bodySprite), connectorTint);
        }
    }

    void DrawRootStrictSquare(List<GridPos> body, string objectName, Sprite bodySprite, Sprite headSprite, Color headColor, Color tailColor)
    {
        for (int i = 0; i < body.Count; i++)
        {
            float t = body.Count <= 1 ? 0f : (float)i / (body.Count - 1);
            Color tint = Color.Lerp(headColor, tailColor, t);
            bool isHead = i == 0;
            Sprite segmentSprite = GetBodySpriteForIndex(i, bodySprite);
            Vector2 xy = GridCellWorldXY(body[i].x, body[i].y) + rootDrawOffset;
            GameObject node = new GameObject(objectName + "_Cell_" + i);
            node.transform.SetParent(transform);
            node.transform.position = new Vector3(xy.x, xy.y, zRootNodes);
            float segScale = strictRootSegmentScale * CellSize;
            if (isHead)
            {
                segScale *= strictHeadScaleMultiplier;
            }

            node.transform.localScale = Vector3.one * segScale;
            node.transform.rotation = isHead && body.Count >= 2
                ? Quaternion.Euler(0f, 0f, GetHeadAngle(body))
                : Quaternion.identity;

            var sr = node.AddComponent<SpriteRenderer>();
            ApplyPlayerVisual(sr, isHead && headSprite != null ? headSprite : segmentSprite, tint);
            sr.sortingOrder = 5;
            playerBlocks.Add(node);
        }
    }

    Sprite GetBodySpriteForIndex(int index, Sprite fallbackBodySprite)
    {
        Sprite variantA = rootVariant1Sprite != null ? rootVariant1Sprite : fallbackBodySprite;
        Sprite variantB = rootVariant2Sprite != null ? rootVariant2Sprite : variantA;

        return index % 2 == 0 ? variantA : variantB;
    }

    void CreateRootNode(string objectName, List<GridPos> body, int index, Sprite bodySprite, Sprite headSprite, Color tint, bool isHead)
    {
        GameObject node = new GameObject(objectName + "_Node");
        node.transform.SetParent(transform);
        Vector3 jitteredPosition = GetJitteredPosition(body, index, 0.18f);
        node.transform.position = new Vector3(jitteredPosition.x, jitteredPosition.y, zRootNodes);
        node.transform.localScale = Vector3.one * (isHead ? rootNodeScale * headScaleMultiplier : rootNodeScale) * CellSize;

        var sr = node.AddComponent<SpriteRenderer>();
        ApplyPlayerVisual(sr, isHead && headSprite != null ? headSprite : bodySprite, tint);
        sr.sortingOrder = 3;
        if (isHead)
        {
            sr.transform.rotation = Quaternion.Euler(0f, 0f, GetHeadAngle(body));
        }

        playerBlocks.Add(node);
    }

    void CreateRootConnector(string objectName, List<GridPos> body, int index, Sprite sprite, Color tint)
    {
        Vector3 start = GetJitteredPosition(body, index, 0.18f);
        Vector3 end = GetJitteredPosition(body, index + 1, 0.18f);
        start.z = zRootConnectors;
        end.z = zRootConnectors;
        Vector3 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        GameObject connector = new GameObject(objectName + "_Connector");
        connector.transform.SetParent(transform);
        connector.transform.position = (start + end) * 0.5f;
        connector.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        float thickness = GetConnectorThickness(body, index);
        float worldThickness = thickness * CellSize;
        connector.transform.localScale = new Vector3(distance + worldThickness, worldThickness, 1f);

        var sr = connector.AddComponent<SpriteRenderer>();
        ApplyPlayerVisual(sr, sprite, tint);
        sr.sortingOrder = 2;

        playerBlocks.Add(connector);
    }

    void CreateRootElbow(string objectName, List<GridPos> body, int index, Sprite sprite, Color tint)
    {
        Vector2Int inDir = new Vector2Int(body[index - 1].x - body[index].x, body[index - 1].y - body[index].y);
        Vector2Int outDir = new Vector2Int(body[index + 1].x - body[index].x, body[index + 1].y - body[index].y);
        if (inDir == outDir || (inDir.x == -outDir.x && inDir.y == -outDir.y))
        {
            return;
        }

        GameObject elbow = new GameObject(objectName + "_Elbow");
        elbow.transform.SetParent(transform);
        Vector3 pos = GetJitteredPosition(body, index, 0.12f);
        elbow.transform.position = new Vector3(pos.x, pos.y, zRootElbows);
        float size = GetConnectorThickness(body, index) * 1.45f;
        elbow.transform.localScale = new Vector3(size, size, 1f) * CellSize;

        var sr = elbow.AddComponent<SpriteRenderer>();
        ApplyPlayerVisual(sr, sprite, tint);
        sr.sortingOrder = 2;

        playerBlocks.Add(elbow);
    }

    float GetConnectorThickness(List<GridPos> body, int index)
    {
        if (body.Count <= 1)
        {
            return rootThickness;
        }

        float t = (index + 0.5f) / (body.Count - 1f);
        return Mathf.Lerp(rootThickness, rootThickness * tailThicknessFactor, t);
    }

    float GetHeadAngle(List<GridPos> body)
    {
        if (body.Count < 2)
        {
            return 0f;
        }

        Vector2 delta = new Vector2(body[0].x - body[1].x, -(body[0].y - body[1].y));
        if (delta.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
    }

    Vector2 GridCellWorldXY(int x, int y)
    {
        return new Vector2(
            x * CellSize + worldOffset.x + gridExtraOffset.x,
            -y * CellSize + worldOffset.y + gridExtraOffset.y);
    }

    Vector3 GetJitteredPosition(List<GridPos> body, int index, float strengthMultiplier)
    {
        GridPos segment = body[index];
        Vector2 basePos = GridCellWorldXY(segment.x, segment.y) + rootDrawOffset;
        if (rootJitter <= 0f)
        {
            return new Vector3(basePos.x, basePos.y, 0f);
        }

        int hash = segment.x * 73856093 ^ segment.y * 19349663 ^ index * 83492791;
        float angle = Mathf.Abs(hash % 360) * Mathf.Deg2Rad;
        float magnitude = rootJitter * strengthMultiplier * CellSize;
        float offsetX = Mathf.Cos(angle) * magnitude;
        float offsetY = Mathf.Sin(angle) * magnitude;

        return new Vector3(basePos.x + offsetX, basePos.y + offsetY, 0f);
    }

    Vector3 GridToWorldPosition(int x, int y, float z)
    {
        Vector2 xy = GridCellWorldXY(x, y);
        return new Vector3(xy.x, xy.y, z);
    }

    void RefreshGridLines()
    {
        if (gridManager == null || !showGridLines)
        {
            if (gridLinesRoot != null)
            {
                gridLinesRoot.SetActive(false);
            }

            return;
        }

        if (gridLinesRoot == null)
        {
            gridLinesRoot = new GameObject("GridLines");
            gridLinesRoot.transform.SetParent(transform);
        }

        gridLinesRoot.SetActive(true);

        int w = gridManager.width;
        int h = gridManager.height;
        int needV = w + 1;
        int needH = h + 1;

        if (gridLineVertical.Count != needV || gridLineHorizontal.Count != needH)
        {
            foreach (Transform child in gridLinesRoot.transform)
            {
                Destroy(child.gameObject);
            }

            gridLineVertical.Clear();
            gridLineHorizontal.Clear();

            for (int i = 0; i < needV; i++)
            {
                gridLineVertical.Add(CreateGridLineSegment($"GridLine_V_{i}"));
            }

            for (int i = 0; i < needH; i++)
            {
                gridLineHorizontal.Add(CreateGridLineSegment($"GridLine_H_{i}"));
            }
        }

        float ox = worldOffset.x + gridExtraOffset.x;
        float oy = worldOffset.y + gridExtraOffset.y;
        float cs = CellSize;
        float yTop = 0.5f * cs + oy;
        float yBot = -(h - 1) * cs + oy - 0.5f * cs;
        float xLeft = -0.5f * cs + ox;
        float xRight = (w - 0.5f) * cs + ox;

        for (int k = 0; k < needV; k++)
        {
            float xw = (k - 0.5f) * cs + ox;
            LineRenderer lr = gridLineVertical[k];
            lr.SetPosition(0, new Vector3(xw, yTop, zGridLines));
            lr.SetPosition(1, new Vector3(xw, yBot, zGridLines));
        }

        for (int r = 0; r < needH; r++)
        {
            float yw = -(r - 0.5f) * cs + oy;
            LineRenderer lr = gridLineHorizontal[r];
            lr.SetPosition(0, new Vector3(xLeft, yw, zGridLines));
            lr.SetPosition(1, new Vector3(xRight, yw, zGridLines));
        }
    }

    LineRenderer CreateGridLineSegment(string objectName)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(gridLinesRoot.transform);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = 2;
        float w = Mathf.Max(0.002f, gridLineWidth) * CellSize;
        lr.startWidth = w;
        lr.endWidth = w;
        lr.numCapVertices = 2;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        lr.material = shader != null ? new Material(shader) : null;
        lr.startColor = gridLineColor;
        lr.endColor = gridLineColor;
        lr.sortingOrder = 4;
        return lr;
    }

    Sprite CreateFallbackSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1f
        );
    }

    Sprite CreateCircleSprite()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.45f;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                tex.SetPixel(x, y, dist <= radius ? Color.white : clear);
            }
        }

        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }


}
