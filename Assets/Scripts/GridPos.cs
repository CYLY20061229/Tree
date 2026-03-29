[System.Serializable]
public struct GridPos
{
    public int x;
    public int y;

    public GridPos(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static GridPos operator +(GridPos a, GridPos b)
    {
        return new GridPos(a.x + b.x, a.y + b.y);
    }
}