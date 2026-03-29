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
    public float stunTimer;
    public bool forceLeftNextMove;
    public float reverseControlsTimer;
    public float resourceSiphonTimer;
    public float growthSpeedMultiplier = 1f;
    public float growthSpeedProgress;
    public int nextDashDownDistance;
    public bool hasUsedUltimate;
    public bool hasBreakTarget;
    public GridPos breakTarget;
    public int breakStoneHits;

    public float turgor = 1f;
    public float actionLockTimer;
    public float waterRecoveryBoostTimer;
    public float burstGrowthNoCostTimer;
    public float penetrateStoneTimer;
    public float skillBlockTimer;
    public float maxTurgorMultiplier = 1f;
    public float ultimateTurgorPenaltyTimer;
    public float vacuumHarvestTimer;
    public float vacuumHarvestPulse;
    public bool nextWaterPoisoned;

    public GridPos Head => body[0];
    public float EffectiveMaxTurgor => Mathf.Clamp01(maxTurgorMultiplier);
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

        if (stunTimer > 0f)
        {
            stunTimer = Mathf.Max(0f, stunTimer - deltaTime);
        }

        if (resourceSiphonTimer > 0f)
        {
            resourceSiphonTimer = Mathf.Max(0f, resourceSiphonTimer - deltaTime);
        }

        if (actionLockTimer > 0f)
        {
            actionLockTimer = Mathf.Max(0f, actionLockTimer - deltaTime);
        }

        if (waterRecoveryBoostTimer > 0f)
        {
            waterRecoveryBoostTimer = Mathf.Max(0f, waterRecoveryBoostTimer - deltaTime);
        }

        if (burstGrowthNoCostTimer > 0f)
        {
            burstGrowthNoCostTimer = Mathf.Max(0f, burstGrowthNoCostTimer - deltaTime);
        }

        if (penetrateStoneTimer > 0f)
        {
            penetrateStoneTimer = Mathf.Max(0f, penetrateStoneTimer - deltaTime);
        }

        if (skillBlockTimer > 0f)
        {
            skillBlockTimer = Mathf.Max(0f, skillBlockTimer - deltaTime);
        }

        if (ultimateTurgorPenaltyTimer > 0f)
        {
            ultimateTurgorPenaltyTimer = Mathf.Max(0f, ultimateTurgorPenaltyTimer - deltaTime);
            if (ultimateTurgorPenaltyTimer <= 0f)
            {
                maxTurgorMultiplier = 1f;
            }
        }

        if (vacuumHarvestTimer > 0f)
        {
            vacuumHarvestTimer = Mathf.Max(0f, vacuumHarvestTimer - deltaTime);
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

    public void ResetTurgorState()
    {
        turgor = 1f;
        actionLockTimer = 0f;
        waterRecoveryBoostTimer = 0f;
        burstGrowthNoCostTimer = 0f;
        penetrateStoneTimer = 0f;
        skillBlockTimer = 0f;
        maxTurgorMultiplier = 1f;
        ultimateTurgorPenaltyTimer = 0f;
        vacuumHarvestTimer = 0f;
        vacuumHarvestPulse = 0f;
        nextWaterPoisoned = false;
    }

    public void ClampTurgorToMax()
    {
        turgor = Mathf.Min(turgor, EffectiveMaxTurgor);
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
