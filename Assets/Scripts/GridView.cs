using System.Collections.Generic;
using UnityEngine;

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
    public Vector2 worldOffset = new Vector2(-6f, 0f);
    public float cellVisualScale = 1.06f;
    public float featureVisualScale = 1.18f;
    public bool useSmoothRoots = true;
    public float rootThickness = 0.42f;
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

    void CreateGridObjects()
    {
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                GameObject cell = new GameObject($"Cell_{x}_{y}");
                cell.transform.SetParent(transform);
                cell.transform.position = GridToWorldPosition(x, y, 0f);
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
        node.transform.position = new Vector3(jitteredPosition.x, jitteredPosition.y, -0.12f);
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
        start.z = -0.11f;
        end.z = -0.11f;
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
        elbow.transform.position = new Vector3(pos.x, pos.y, -0.105f);
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

    Vector3 GetJitteredPosition(List<GridPos> body, int index, float strengthMultiplier)
    {
        GridPos segment = body[index];
        Vector2 basePos = new Vector2(segment.x * CellSize + worldOffset.x, -segment.y * CellSize + worldOffset.y);
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
        return new Vector3(x * CellSize + worldOffset.x, -y * CellSize + worldOffset.y, z);
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
