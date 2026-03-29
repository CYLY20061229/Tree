using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerRoot
{
    public string playerName;
    public List<GridPos> body = new List<GridPos>();

    public int nitrogen;
    public int phosphorus;
    public int potassium;
    public int water;
    public int stunnedTurns;
    public bool forceLeftNextMove;
    public float reverseControlsTimer;
    public float resourceSiphonTimer;
    public float growthSpeedMultiplier = 1f;
    public float growthSpeedProgress;
    public int nextDashDownDistance;
    public bool canPenetrateStoneNextMove;
    public bool hasUsedUltimate;
    public bool hasBreakTarget;
    public GridPos breakTarget;
    public int breakStoneHits;

    public GridPos Head => body[0];
    public bool IsControlReversed => reverseControlsTimer > 0f;

    public bool Contains(GridPos pos)
    {
        foreach (var seg in body)
        {
            if (seg.x == pos.x && seg.y == pos.y)
                return true;
        }
        return false;
    }

    public void GrowTo(GridPos nextPos)
    {
        body.Insert(0, nextPos);
    }

    public void TickTimers(float deltaTime)
    {
        if (reverseControlsTimer > 0f)
        {
            reverseControlsTimer = Mathf.Max(0f, reverseControlsTimer - deltaTime);
        }

        if (resourceSiphonTimer > 0f)
        {
            resourceSiphonTimer = Mathf.Max(0f, resourceSiphonTimer - deltaTime);
        }
    }

    public bool HasNpkSet()
    {
        return nitrogen > 0 && phosphorus > 0 && potassium > 0;
    }

    public bool HasTripleResource()
    {
        return nitrogen >= 3 || phosphorus >= 3 || potassium >= 3 || water >= 3;
    }

    public void ClearResources()
    {
        nitrogen = 0;
        phosphorus = 0;
        potassium = 0;
        water = 0;
    }

    public void ResetGrowthSpeed()
    {
        growthSpeedMultiplier = 1f;
        growthSpeedProgress = 0f;
    }

    public void TrimFromTail(int count)
    {
        while (count > 0 && body.Count > 1)
        {
            body.RemoveAt(body.Count - 1);
            count--;
        }
    }

    public int RetreatHead(int count)
    {
        int removed = 0;
        while (count > 0 && body.Count > 1)
        {
            body.RemoveAt(0);
            count--;
            removed++;
        }

        return removed;
    }

    public void KeepOnlyHead()
    {
        if (body.Count == 0)
        {
            return;
        }

        GridPos head = Head;
        body.Clear();
        body.Add(head);
    }

    public int PruneToMainRoot(int keepSegments)
    {
        if (body.Count <= 1)
        {
            return 0;
        }

        keepSegments = Mathf.Max(2, keepSegments);
        if (body.Count <= keepSegments)
        {
            return 0;
        }

        int removed = body.Count - keepSegments;
        body.RemoveRange(keepSegments, removed);
        return removed;
    }

    public void ResetBreakAttempt()
    {
        hasBreakTarget = false;
        breakStoneHits = 0;
    }
}
