using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public GridManager gridManager;
    public bool viewDirty = true;
    public PlayerRoot playerA = new PlayerRoot();
    public PlayerRoot playerB = new PlayerRoot();
    public bool gameOver;
    public float resourceRespawnDelay = 3f;
    public float temporaryStoneDuration = 5f;
    public float turgorMoveCost = 0.15f;
    public float matchTimeLimitSeconds = 0f;
    public Font uiFont;

    float matchTimerRemaining;

    public enum TurnPlayer
    {
        PlayerA,
        PlayerB
    }

    public enum SkillUnlockType
    {
        None,
        NpkSet,
        TripleResource,
        Ultimate
    }

    public enum SkillType
    {
        None,
        Sprint,
        DrawWater,
        Wither,
        ReverseControl,
        Penetrate,
        BurstGrowth,
        ResourceSiphon,
        Collapse,
        RootSnare,
        NutrientSteal,
        Pollution,
        VacuumHarvest,
        UltimateDrain,
        Interference
    }

    class RespawnRequest
    {
        public float timer;
        public int minRow;
    }

    class TemporaryStone
    {
        public GridPos pos;
        public float timer;
    }

    struct SkillChoice
    {
        public SkillType type;
        public string title;
        public string description;

        public SkillChoice(SkillType type, string title, string description)
        {
            this.type = type;
            this.title = title;
            this.description = description;
        }
    }

    readonly List<RespawnRequest> respawns = new List<RespawnRequest>();
    readonly List<TemporaryStone> temporaryStones = new List<TemporaryStone>();

    SkillUnlockType pendingSkillUnlockA = SkillUnlockType.None;
    SkillUnlockType pendingSkillUnlockB = SkillUnlockType.None;
    SkillChoice[] pendingChoicesA = new SkillChoice[2];
    SkillChoice[] pendingChoicesB = new SkillChoice[2];
    string winnerMessage = string.Empty;
    string battleMessage = string.Empty;

    Canvas canvasUi;
    Image leftPanel;
    Image rightPanel;
    Image centerPanel;
    Image skillPanelLeft;
    Image skillPanelRight;
    Text leftText;
    Text rightText;
    Text centerText;
    Text skillTitleLeft;
    Text skillTitleRight;
    Text skill1TextLeft;
    Text skill2TextLeft;
    Text skill1TextRight;
    Text skill2TextRight;
    Button skill1ButtonLeft;
    Button skill2ButtonLeft;
    Button skill1ButtonRight;
    Button skill2ButtonRight;
    Button restartButton;

    RectTransform turgorBarLeftFillRt;
    RectTransform turgorBarRightFillRt;
    Image turgorBarLeftFillImg;
    Image turgorBarRightFillImg;
    const float TurgorBarTrackHeight = 508f;
    const float TurgorBarFillMaxHeight = 502f;
    const float TurgorBarSideInset = 400f;
    static readonly Color TurgorBarFillBlue = new Color(0.26f, 0.58f, 0.95f, 0.98f);
    static readonly Color TurgorBarTrackBlue = new Color(0.05f, 0.1f, 0.18f, 0.92f);

    void Start()
    {
        if (gridManager == null)
        {
            enabled = false;
            return;
        }

        gridManager.InitGrid();
        playerA.playerName = "左侧树根";
        playerB.playerName = "右侧树根";
        matchTimerRemaining = matchTimeLimitSeconds;
        ResetPlayers();
        BuildUi();
        RefreshUi();
    }

    void Update()
    {
        if (!enabled)
        {
            return;
        }

        playerA.TickTimers(Time.deltaTime);
        playerB.TickTimers(Time.deltaTime);
        ApplyTurgorRecovery(Time.deltaTime);
        TickVacuumHarvest(playerA, Time.deltaTime);
        TickVacuumHarvest(playerB, Time.deltaTime);
        UpdateRespawns(Time.deltaTime);
        UpdateTemporaryStones(Time.deltaTime);
        UpdateUiAnimations();

        if (gameOver)
        {
            RefreshUi();
            return;
        }

        if (matchTimeLimitSeconds > 0f)
        {
            matchTimerRemaining -= Time.deltaTime;
            if (matchTimerRemaining <= 0f)
            {
                ResolveMatchTimeUp();
                RefreshUi();
                return;
            }
        }

        EvaluateUltimateUnlockForPlayers();
        TryQueueSkillUnlock(playerA);
        TryQueueSkillUnlock(playerB);

        if (pendingSkillUnlockA != SkillUnlockType.None || pendingSkillUnlockB != SkillUnlockType.None)
        {
            HandleSkillInput();
        }

        if (playerA.stunTimer <= 0f && playerA.actionLockTimer <= 0f && TryReadMoveInputPlayerA(out MoveDirection dirA))
        {
            MoveDirection finalA = ApplyDirectionModifiers(playerA, dirA);
            TryMovePlayer(playerA, playerB, true, finalA);
        }

        if (playerB.stunTimer <= 0f && playerB.actionLockTimer <= 0f && TryReadMoveInputPlayerB(out MoveDirection dirB))
        {
            MoveDirection finalB = ApplyDirectionModifiers(playerB, dirB);
            TryMovePlayer(playerB, playerA, false, finalB);
        }

        RefreshUi();
    }

    void ResetPlayers()
    {
        ResetPlayerState(playerA);
        ResetPlayerState(playerB);
        playerA.body.Add(new GridPos(2, 0));
        playerB.body.Add(new GridPos(gridManager.width - 3, 0));
        pendingSkillUnlockA = SkillUnlockType.None;
        pendingSkillUnlockB = SkillUnlockType.None;
        pendingChoicesA = new SkillChoice[2];
        pendingChoicesB = new SkillChoice[2];
        battleMessage = "目标：率先到达最底层（双方可同时移动）";
        matchTimerRemaining = matchTimeLimitSeconds;
    }

    void ResetPlayerState(PlayerRoot player)
    {
        player.body.Clear();
        player.ClearResources();
        player.stunTimer = 0f;
        player.forceLeftNextMove = false;
        player.reverseControlsTimer = 0f;
        player.resourceSiphonTimer = 0f;
        player.ResetGrowthSpeed();
        player.nextDashDownDistance = 0;
        player.hasUsedUltimate = false;
        player.ResetBreakAttempt();
        player.ResetTurgorState();
    }

    bool TryReadMoveInputPlayerA(out MoveDirection dir)
    {
        dir = MoveDirection.Down;

        if (Input.GetKeyDown(KeyCode.A))
        {
            dir = MoveDirection.Left;
            return true;
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            dir = MoveDirection.Right;
            return true;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            dir = MoveDirection.Down;
            return true;
        }

        return false;
    }

    bool TryReadMoveInputPlayerB(out MoveDirection dir)
    {
        dir = MoveDirection.Down;

        if (Input.GetKeyDown(KeyCode.J))
        {
            dir = MoveDirection.Left;
            return true;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            dir = MoveDirection.Right;
            return true;
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            dir = MoveDirection.Down;
            return true;
        }

        return false;
    }

    MoveDirection ApplyDirectionModifiers(PlayerRoot player, MoveDirection input)
    {
        MoveDirection finalDirection = input;

        if (player.forceLeftNextMove)
        {
            player.forceLeftNextMove = false;
            finalDirection = MoveDirection.Left;
            battleMessage = player.playerName + " 被缠绕，强制向左";
        }

        if (player.IsControlReversed)
        {
            if (finalDirection == MoveDirection.Left)
            {
                finalDirection = MoveDirection.Right;
            }
            else if (finalDirection == MoveDirection.Right)
            {
                finalDirection = MoveDirection.Left;
            }
        }

        return finalDirection;
    }

    public void TryMovePlayer(PlayerRoot current, PlayerRoot enemy, bool isPlayerA, MoveDirection dir)
    {
        if (current.burstGrowthNoCostTimer <= 0f && current.turgor < turgorMoveCost)
        {
            battleMessage = "膨压不足，无法移动";
            return;
        }

        if (dir == MoveDirection.Down && current.nextDashDownDistance > 1)
        {
            int dashDistance = current.nextDashDownDistance;
            current.nextDashDownDistance = 0;
            int moved = GrowDownSteps(current, enemy, dashDistance);
            battleMessage = moved > 0
                ? current.playerName + " 发动冲刺，向下延展 " + moved + " 格"
                : current.playerName + " 的冲刺被阻挡";

            return;
        }

        GridPos target = current.Head + Offset(dir);

        if (!gridManager.IsInside(target))
        {
            battleMessage = "该方向超出地图";
            return;
        }

        if (!gridManager.IsCellAllowedForPlayer(target, isPlayerA))
        {
            battleMessage = "该格位于对方专属区域，无法进入";
            return;
        }

        if (current.Contains(target))
        {
            battleMessage = "不能进入自己的根系";
            return;
        }

        if (target.x == enemy.Head.x && target.y == enemy.Head.y)
        {
            ResolveHeadCollision(current, enemy);
            return;
        }

        if (ContainsEnemyBodyButNotHead(enemy, target))
        {
            ResolveBodyCollisionV2(current, enemy);
            return;
        }

        if (gridManager.IsStone(target))
        {
            if (current.penetrateStoneTimer > 0f)
            {
                gridManager.cells[target.x, target.y] = CellType.Soil;
                battleMessage = current.playerName + " 穿透石头";
            }
            else
            {
                TryHandleBreakStone(current, target);
                return;
            }
        }

        StepIntoCell(current, target);
        ApplyTurgorCost(current);
        ApplyGrowthSpeedBonus(current, enemy);
        battleMessage = current.playerName + " 延展了一格";
        FinalizeSuccessfulAction(current);
    }

    bool TryHandleBreakStone(PlayerRoot player, GridPos target)
    {
        if (!gridManager.IsInside(target) || !gridManager.IsStone(target))
        {
            return false;
        }

        if (player.burstGrowthNoCostTimer <= 0f && player.turgor < turgorMoveCost)
        {
            battleMessage = "膨压不足，无法冲击石头";
            return false;
        }

        if (!player.hasBreakTarget || player.breakTarget.x != target.x || player.breakTarget.y != target.y)
        {
            player.hasBreakTarget = true;
            player.breakTarget = target;
            player.breakStoneHits = 0;
        }

        player.breakStoneHits++;

        if (player.breakStoneHits < 3)
        {
            ApplyTurgorCost(player);
            battleMessage = player.playerName + " 冲击石头 " + player.breakStoneHits + "/3";
            viewDirty = true;
            return true;
        }

        gridManager.cells[target.x, target.y] = CellType.Soil;
        StepIntoCell(player, target);
        ApplyTurgorCost(player);
        ApplyGrowthSpeedBonus(player, GetEnemyPlayerFor(player));
        player.ResetBreakAttempt();
        battleMessage = player.playerName + " 击碎石头并向前延展";
        FinalizeSuccessfulAction(player);
        return true;
    }

    void ResolveHeadCollision(PlayerRoot current, PlayerRoot enemy)
    {
        current.stunTimer = Mathf.Max(current.stunTimer, 0.9f);
        enemy.stunTimer = Mathf.Max(enemy.stunTimer, 0.9f);

        CellType fromEnemy = TakeRandomResource(enemy);
        CellType fromCurrent = TakeRandomResource(current);

        GiveResource(current, fromEnemy);
        GiveResource(enemy, fromCurrent);

        current.ResetBreakAttempt();
        enemy.ResetBreakAttempt();
        battleMessage = "头部相撞，双方僵直并互偷一个资源";
        viewDirty = true;
    }

    void StepIntoCell(PlayerRoot player, GridPos target)
    {
        if (gridManager.IsResource(target))
        {
            CollectResource(player, target);
        }

        player.GrowTo(target);
        player.ResetBreakAttempt();
        viewDirty = true;
    }

    void FinalizeSuccessfulAction(PlayerRoot player)
    {
        if (player.Head.y >= gridManager.height - 1)
        {
            gameOver = true;
            winnerMessage = player.playerName + " 获胜";
            battleMessage = winnerMessage;
            return;
        }

        EvaluateSkillUnlock(player);
    }

    int GrowDownSteps(PlayerRoot player, PlayerRoot enemy, int steps)
    {
        int moved = 0;

        for (int i = 0; i < steps; i++)
        {
            if (player.burstGrowthNoCostTimer <= 0f && player.turgor < turgorMoveCost)
            {
                break;
            }

            GridPos target = player.Head + Offset(MoveDirection.Down);
            if (!CanGrowStraightDown(player, enemy, target))
            {
                break;
            }

            StepIntoCell(player, target);
            ApplyTurgorCost(player);
            ApplyGrowthSpeedBonus(player, enemy);
            moved++;

            if (player.Head.y >= gridManager.height - 1)
            {
                gameOver = true;
                winnerMessage = player.playerName + " 获胜";
                battleMessage = winnerMessage;
                return moved;
            }
        }

        EvaluateSkillUnlock(player);
        return moved;
    }

    void ApplyGrowthSpeedBonus(PlayerRoot player, PlayerRoot enemy)
    {
        if (player.growthSpeedMultiplier <= 1f)
        {
            return;
        }

        player.growthSpeedProgress += player.growthSpeedMultiplier - 1f;
        while (player.growthSpeedProgress >= 1f && !gameOver)
        {
            if (player.burstGrowthNoCostTimer <= 0f && player.turgor < turgorMoveCost)
            {
                break;
            }

            GridPos target = player.Head + Offset(MoveDirection.Down);
            if (!CanGrowStraightDown(player, enemy, target))
            {
                break;
            }

            StepIntoCell(player, target);
            ApplyTurgorCost(player);
            player.growthSpeedProgress -= 1f;

            if (player.Head.y >= gridManager.height - 1)
            {
                gameOver = true;
                winnerMessage = player.playerName + " 获胜";
                battleMessage = winnerMessage;
                return;
            }
        }
    }

    bool CanGrowStraightDown(PlayerRoot player, PlayerRoot enemy, GridPos target)
    {
        if (!gridManager.IsInside(target))
        {
            return false;
        }

        if (player.Contains(target) || enemy.Contains(target))
        {
            return false;
        }

        if (gridManager.IsStone(target))
        {
            return player.penetrateStoneTimer > 0f;
        }

        return true;
    }

    GridPos Offset(MoveDirection dir)
    {
        if (dir == MoveDirection.Left)
        {
            return new GridPos(-1, 0);
        }

        if (dir == MoveDirection.Right)
        {
            return new GridPos(1, 0);
        }

        return new GridPos(0, 1);
    }

    PlayerRoot GetEnemyPlayerFor(PlayerRoot player)
    {
        return player == playerA ? playerB : playerA;
    }

    bool ContainsEnemyBodyButNotHead(PlayerRoot enemy, GridPos pos)
    {
        for (int i = 1; i < enemy.body.Count; i++)
        {
            if (enemy.body[i].x == pos.x && enemy.body[i].y == pos.y)
            {
                return true;
            }
        }

        return false;
    }

    void CollectResource(PlayerRoot player, GridPos pos)
    {
        CellType resourceType = gridManager.cells[pos.x, pos.y];

        if (resourceType == CellType.ResourceW && player.nextWaterPoisoned)
        {
            player.nextWaterPoisoned = false;
            gridManager.cells[pos.x, pos.y] = CellType.Soil;
            player.actionLockTimer = Mathf.Max(player.actionLockTimer, 3f);
            respawns.Add(new RespawnRequest
            {
                timer = resourceRespawnDelay,
                minRow = Mathf.Clamp(pos.y + 2, 2, gridManager.height - 2)
            });
            battleMessage = player.playerName + " 触碰污水，暂停行动 3 秒";
            viewDirty = true;
            return;
        }

        PickupResourceAt(player, pos, resourceType);
    }

    void PickupResourceAt(PlayerRoot player, GridPos pos, CellType resourceType)
    {
        int amount = player.resourceSiphonTimer > 0f ? 2 : 1;

        for (int i = 0; i < amount; i++)
        {
            GiveResource(player, resourceType);
        }

        if (resourceType == CellType.ResourceW)
        {
            player.waterRecoveryBoostTimer = 3f;
        }

        gridManager.cells[pos.x, pos.y] = CellType.Soil;
        respawns.Add(new RespawnRequest
        {
            timer = resourceRespawnDelay,
            minRow = Mathf.Clamp(pos.y + 2, 2, gridManager.height - 2)
        });
        TryQueueSkillUnlock(player);
    }

    void GiveResource(PlayerRoot player, CellType resourceType)
    {
        if (resourceType == CellType.ResourceN)
        {
            player.nitrogen++;
        }
        else if (resourceType == CellType.ResourceP)
        {
            player.phosphorus++;
        }
        else if (resourceType == CellType.ResourceK)
        {
            player.potassium++;
        }
        else if (resourceType == CellType.ResourceW)
        {
            player.water++;
        }
    }

    void GiveRandomResource(PlayerRoot player)
    {
        int roll = Random.Range(0, 4);
        GiveResource(player, (CellType)((int)CellType.ResourceN + roll));
    }

    CellType TakeRandomResource(PlayerRoot player)
    {
        List<CellType> owned = new List<CellType>();

        if (player.nitrogen > 0)
        {
            owned.Add(CellType.ResourceN);
        }

        if (player.phosphorus > 0)
        {
            owned.Add(CellType.ResourceP);
        }

        if (player.potassium > 0)
        {
            owned.Add(CellType.ResourceK);
        }

        if (player.water > 0)
        {
            owned.Add(CellType.ResourceW);
        }

        if (owned.Count == 0)
        {
            return CellType.Empty;
        }

        CellType chosen = owned[Random.Range(0, owned.Count)];
        RemoveOneResource(player, chosen);
        return chosen;
    }

    void RemoveOneResource(PlayerRoot player, CellType resourceType)
    {
        if (resourceType == CellType.ResourceN && player.nitrogen > 0)
        {
            player.nitrogen--;
        }
        else if (resourceType == CellType.ResourceP && player.phosphorus > 0)
        {
            player.phosphorus--;
        }
        else if (resourceType == CellType.ResourceK && player.potassium > 0)
        {
            player.potassium--;
        }
        else if (resourceType == CellType.ResourceW && player.water > 0)
        {
            player.water--;
        }
    }

    void UpdateRespawns(float deltaTime)
    {
        for (int i = respawns.Count - 1; i >= 0; i--)
        {
            respawns[i].timer -= deltaTime;
            if (respawns[i].timer > 0f)
            {
                continue;
            }

            for (int attempt = 0; attempt < 24; attempt++)
            {
                int x = Random.Range(0, gridManager.width);
                int y = Random.Range(respawns[i].minRow, gridManager.height - 1);
                GridPos pos = new GridPos(x, y);
                if (gridManager.cells[x, y] != CellType.Soil || IsOccupiedByAnyRoot(pos))
                {
                    continue;
                }

                gridManager.cells[x, y] = (CellType)((int)CellType.ResourceN + Random.Range(0, 4));
                viewDirty = true;
                break;
            }

            respawns.RemoveAt(i);
        }
    }

    void UpdateTemporaryStones(float deltaTime)
    {
        for (int i = temporaryStones.Count - 1; i >= 0; i--)
        {
            temporaryStones[i].timer -= deltaTime;
            if (temporaryStones[i].timer > 0f)
            {
                continue;
            }

            GridPos pos = temporaryStones[i].pos;
            if (gridManager.IsInside(pos) && gridManager.cells[pos.x, pos.y] == CellType.Stone)
            {
                gridManager.cells[pos.x, pos.y] = CellType.Soil;
                viewDirty = true;
            }

            temporaryStones.RemoveAt(i);
        }
    }

    bool IsOccupiedByAnyRoot(GridPos pos)
    {
        return playerA.Contains(pos) || playerB.Contains(pos);
    }

    void EvaluateUltimateUnlockForPlayers()
    {
        TryTriggerUltimate(playerA, playerB);
        TryTriggerUltimate(playerB, playerA);
    }

    void TryQueueSkillUnlock(PlayerRoot player)
    {
        EvaluateSkillUnlock(player);
    }

    bool TryTriggerUltimate(PlayerRoot current, PlayerRoot enemy)
    {
        if (current.hasUsedUltimate)
        {
            return false;
        }

        if (current.skillBlockTimer > 0f)
        {
            return false;
        }

        if (enemy.Head.y - current.Head.y < 8)
        {
            return false;
        }

        bool isA = current == playerA;
        SkillUnlockType pending = isA ? pendingSkillUnlockA : pendingSkillUnlockB;
        if (pending != SkillUnlockType.None)
        {
            return false;
        }

        SkillChoice[] choices = isA ? pendingChoicesA : pendingChoicesB;
        if (isA)
        {
            pendingSkillUnlockA = SkillUnlockType.Ultimate;
        }
        else
        {
            pendingSkillUnlockB = SkillUnlockType.Ultimate;
        }

        choices[0] = new SkillChoice(SkillType.VacuumHarvest, "吸养", "以根尖为中心 5×5 内资源持续吸取，持续 4 秒");
        choices[1] = new SkillChoice(SkillType.UltimateDrain, "资源掠夺", "偷取对方全部氮磷钾，对方膨压上限 50% 持续 5 秒");
        battleMessage = current.playerName + " 落后 8 层以上，触发终极技能";
        return true;
    }

    void EvaluateSkillUnlock(PlayerRoot player)
    {
        bool isA = player == playerA;
        SkillUnlockType pending = isA ? pendingSkillUnlockA : pendingSkillUnlockB;
        if (pending != SkillUnlockType.None)
        {
            return;
        }

        if (player.skillBlockTimer > 0f)
        {
            return;
        }

        SkillUnlockType unlockType = SkillUnlockType.None;
        if (player.HasNpkSet())
        {
            unlockType = SkillUnlockType.NpkSet;
        }
        else if (player.HasTripleResource())
        {
            unlockType = SkillUnlockType.TripleResource;
        }

        if (unlockType == SkillUnlockType.None)
        {
            return;
        }

        SkillChoice[] target = isA ? pendingChoicesA : pendingChoicesB;
        if (isA)
        {
            pendingSkillUnlockA = unlockType;
        }
        else
        {
            pendingSkillUnlockB = unlockType;
        }

        PrepareSkillChoices(unlockType, target);
        battleMessage = player.playerName + " 解锁了技能选择";
    }

    void PrepareSkillChoices(SkillUnlockType unlockType, SkillChoice[] pendingChoices)
    {
        if (unlockType == SkillUnlockType.NpkSet)
        {
            SkillChoice[] selfPool =
            {
                new SkillChoice(SkillType.Sprint, "冲刺", "下一次向下直接延展 3 格，不能穿过障碍"),
                new SkillChoice(SkillType.DrawWater, "汲水", "立刻获得 1 个水"),
                new SkillChoice(SkillType.NutrientSteal, "养分掠夺", "偷取敌方氮磷钾中数量最多的一项 1 个")
            };

            SkillChoice[] enemyPool =
            {
                new SkillChoice(SkillType.Wither, "枯竭", "敌方半区最下方三行内资源消失（不含水）"),
                new SkillChoice(SkillType.ReverseControl, "迷向", "敌方 3 秒内左右反转"),
                new SkillChoice(SkillType.Pollution, "污染", "敌方下一个水格视为污水，触碰暂停行动 3 秒")
            };

            pendingChoices[0] = selfPool[Random.Range(0, selfPool.Length)];
            pendingChoices[1] = enemyPool[Random.Range(0, enemyPool.Length)];
            return;
        }

        SkillChoice[] tripleSelfPool =
        {
            new SkillChoice(SkillType.Penetrate, "穿透", "3 秒内可穿过石头"),
            new SkillChoice(SkillType.BurstGrowth, "爆发生长", "3 秒内移动不消耗膨压"),
            new SkillChoice(SkillType.ResourceSiphon, "资源虹吸", "5 秒内采集资源按双倍计算")
        };

        SkillChoice[] tripleEnemyPool =
        {
            new SkillChoice(SkillType.Collapse, "塌方", "在敌方根尖下第二行水平连续生成 5 块临时石头"),
            new SkillChoice(SkillType.Interference, "干扰", "敌方 4 秒内无法使用技能且膨压条被隐藏"),
            new SkillChoice(SkillType.RootSnare, "根须绞杀", "敌方根尖沿原路径后退 2 格")
        };

        pendingChoices[0] = tripleSelfPool[Random.Range(0, tripleSelfPool.Length)];
        pendingChoices[1] = tripleEnemyPool[Random.Range(0, tripleEnemyPool.Length)];
    }

    void HandleSkillInput()
    {
        if (pendingSkillUnlockA != SkillUnlockType.None)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                ChooseSkill(TurnPlayer.PlayerA, 0);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                ChooseSkill(TurnPlayer.PlayerA, 1);
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ChooseSkill(TurnPlayer.PlayerA, 0);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ChooseSkill(TurnPlayer.PlayerA, 1);
            }
        }

        if (pendingSkillUnlockB != SkillUnlockType.None)
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                ChooseSkill(TurnPlayer.PlayerB, 0);
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                ChooseSkill(TurnPlayer.PlayerB, 1);
            }

            if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                ChooseSkill(TurnPlayer.PlayerB, 0);
            }

            if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                ChooseSkill(TurnPlayer.PlayerB, 1);
            }
        }
    }

    void ChooseSkill(TurnPlayer who, int index)
    {
        SkillUnlockType pendingUnlock = who == TurnPlayer.PlayerA ? pendingSkillUnlockA : pendingSkillUnlockB;
        SkillChoice[] pendingChoices = who == TurnPlayer.PlayerA ? pendingChoicesA : pendingChoicesB;

        if (index < 0 || index >= pendingChoices.Length || pendingUnlock == SkillUnlockType.None)
        {
            return;
        }

        NormalizePendingChoices(pendingChoices);

        PlayerRoot player = who == TurnPlayer.PlayerA ? playerA : playerB;
        PlayerRoot enemy = who == TurnPlayer.PlayerA ? playerB : playerA;

        if (player.skillBlockTimer > 0f)
        {
            battleMessage = "处于干扰状态，无法使用技能";
            return;
        }

        SkillChoice choice = pendingChoices[index];
        bool consumesResources = pendingUnlock == SkillUnlockType.NpkSet || pendingUnlock == SkillUnlockType.TripleResource;

        if (consumesResources)
        {
            ConsumeSkillCost(player, pendingUnlock);
        }

        ApplySkill(choice.type, player, enemy);

        if (pendingUnlock == SkillUnlockType.Ultimate)
        {
            player.hasUsedUltimate = true;
        }

        if (who == TurnPlayer.PlayerA)
        {
            pendingSkillUnlockA = SkillUnlockType.None;
        }
        else
        {
            pendingSkillUnlockB = SkillUnlockType.None;
        }

        pendingChoices[0] = new SkillChoice();
        pendingChoices[1] = new SkillChoice();

        viewDirty = true;
    }

    void ApplySkill(SkillType skillType, PlayerRoot player, PlayerRoot enemy)
    {
        if (skillType == SkillType.Sprint)
        {
            player.nextDashDownDistance = 3;
            battleMessage = player.playerName + " 获得冲刺";
        }
        else if (skillType == SkillType.DrawWater)
        {
            player.water++;
            battleMessage = player.playerName + " 立刻获得 1 个水";
        }
        else if (skillType == SkillType.NutrientSteal)
        {
            int stolen = StealMostAbundantNpk(player, enemy);
            battleMessage = stolen > 0
                ? "养分掠夺生效，偷取 1 个资源"
                : "养分掠夺失败，对方氮磷钾不足";
        }
        else if (skillType == SkillType.Wither)
        {
            int removed = RemoveEnemyHalfBottomResources(enemy);
            battleMessage = "枯竭生效，清除了 " + removed + " 个资源";
        }
        else if (skillType == SkillType.ReverseControl)
        {
            enemy.reverseControlsTimer = Mathf.Max(enemy.reverseControlsTimer, 3f);
            battleMessage = enemy.playerName + " 进入 3 秒迷向状态";
        }
        else if (skillType == SkillType.Pollution)
        {
            enemy.nextWaterPoisoned = true;
            battleMessage = enemy.playerName + " 下一个水格受到污染";
        }
        else if (skillType == SkillType.Penetrate)
        {
            player.penetrateStoneTimer = 3f;
            battleMessage = player.playerName + " 获得 3 秒穿透";
        }
        else if (skillType == SkillType.BurstGrowth)
        {
            player.burstGrowthNoCostTimer = 3f;
            battleMessage = player.playerName + " 获得 3 秒爆发生长（移动不消耗膨压）";
        }
        else if (skillType == SkillType.ResourceSiphon)
        {
            player.resourceSiphonTimer = Mathf.Max(player.resourceSiphonTimer, 5f);
            battleMessage = player.playerName + " 进入 5 秒资源虹吸";
        }
        else if (skillType == SkillType.Collapse)
        {
            int placed = PlaceCollapseStones(enemy);
            battleMessage = placed > 0
                ? "塌方生效，生成 " + placed + " 块临时石头"
                : "塌方未能生成石头";
        }
        else if (skillType == SkillType.RootSnare)
        {
            int retreated = enemy.RetreatHead(2);
            battleMessage = retreated > 0
                ? enemy.playerName + " 被根须绞杀，沿原路径后退 " + retreated + " 格"
                : enemy.playerName + " 的根尖无法继续后退";
        }
        else if (skillType == SkillType.Interference)
        {
            enemy.skillBlockTimer = Mathf.Max(enemy.skillBlockTimer, 4f);
            battleMessage = enemy.playerName + " 受到干扰，4 秒内无法使用技能";
        }
        else if (skillType == SkillType.VacuumHarvest)
        {
            player.vacuumHarvestTimer = 4f;
            player.vacuumHarvestPulse = 0f;
            VacuumHarvestArea(player);
            battleMessage = player.playerName + " 开始吸养";
        }
        else if (skillType == SkillType.UltimateDrain)
        {
            StealAllNpk(player, enemy);
            enemy.maxTurgorMultiplier = 0.5f;
            enemy.ultimateTurgorPenaltyTimer = 5f;
            enemy.turgor = Mathf.Min(enemy.turgor, enemy.EffectiveMaxTurgor);
            battleMessage = enemy.playerName + " 被掠夺全部氮磷钾，膨压上限降低";
        }

        viewDirty = true;
    }

    void ApplyTurgorCost(PlayerRoot p)
    {
        if (p.burstGrowthNoCostTimer > 0f)
        {
            return;
        }

        p.turgor -= turgorMoveCost;
        if (p.turgor <= 0f)
        {
            p.turgor = 0f;
            p.actionLockTimer = 2f;
        }
    }

    void ApplyTurgorRecovery(float deltaTime)
    {
        ApplyTurgorRecoveryForPlayer(playerA, deltaTime);
        ApplyTurgorRecoveryForPlayer(playerB, deltaTime);
        playerA.ClampTurgorToMax();
        playerB.ClampTurgorToMax();
    }

    void ApplyTurgorRecoveryForPlayer(PlayerRoot p, float deltaTime)
    {
        if (p.actionLockTimer > 0f)
        {
            return;
        }

        float max = p.EffectiveMaxTurgor;
        if (p.turgor >= max)
        {
            return;
        }

        float rate = 0.3f;
        if (p.waterRecoveryBoostTimer > 0f)
        {
            rate += 0.5f;
        }

        p.turgor = Mathf.Min(max, p.turgor + rate * deltaTime);
    }

    void ConsumeSkillCost(PlayerRoot player, SkillUnlockType unlockType)
    {
        if (unlockType == SkillUnlockType.NpkSet)
        {
            if (player.nitrogen > 0)
            {
                player.nitrogen--;
            }

            if (player.phosphorus > 0)
            {
                player.phosphorus--;
            }

            if (player.potassium > 0)
            {
                player.potassium--;
            }
        }
        else if (unlockType == SkillUnlockType.TripleResource)
        {
            if (player.nitrogen >= 3)
            {
                player.nitrogen -= 3;
            }
            else if (player.phosphorus >= 3)
            {
                player.phosphorus -= 3;
            }
            else if (player.potassium >= 3)
            {
                player.potassium -= 3;
            }
            else if (player.water >= 3)
            {
                player.water -= 3;
            }
        }
    }

    int StealMostAbundantNpk(PlayerRoot receiver, PlayerRoot target)
    {
        int n = target.nitrogen;
        int p = target.phosphorus;
        int k = target.potassium;
        int max = Mathf.Max(n, Mathf.Max(p, k));
        if (max <= 0)
        {
            return 0;
        }

        if (n == max && n > 0)
        {
            target.nitrogen--;
            receiver.nitrogen++;
            return 1;
        }

        if (p == max && p > 0)
        {
            target.phosphorus--;
            receiver.phosphorus++;
            return 1;
        }

        if (k > 0)
        {
            target.potassium--;
            receiver.potassium++;
            return 1;
        }

        return 0;
    }

    void StealAllNpk(PlayerRoot receiver, PlayerRoot target)
    {
        receiver.nitrogen += target.nitrogen;
        receiver.phosphorus += target.phosphorus;
        receiver.potassium += target.potassium;
        target.nitrogen = 0;
        target.phosphorus = 0;
        target.potassium = 0;
    }

    void TickVacuumHarvest(PlayerRoot player, float deltaTime)
    {
        if (player.vacuumHarvestTimer <= 0f)
        {
            return;
        }

        player.vacuumHarvestPulse += deltaTime;
        while (player.vacuumHarvestPulse >= 0.25f)
        {
            player.vacuumHarvestPulse -= 0.25f;
            VacuumHarvestArea(player);
        }
    }

    void VacuumHarvestArea(PlayerRoot player)
    {
        if (player.body.Count == 0)
        {
            return;
        }

        GridPos c = player.Head;
        int collected = 0;

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                GridPos pos = new GridPos(c.x + dx, c.y + dy);
                if (!gridManager.IsInside(pos) || !gridManager.IsResource(pos))
                {
                    continue;
                }

                CellType t = gridManager.cells[pos.x, pos.y];
                PickupResourceAt(player, pos, t);
                collected++;
            }
        }

        if (collected > 0)
        {
            viewDirty = true;
        }
    }

    int PlaceCollapseStones(PlayerRoot enemy)
    {
        int rowY = enemy.Head.y + 2;
        int hx = enemy.Head.x;
        int placed = 0;

        if (rowY >= gridManager.height)
        {
            return 0;
        }

        for (int k = -2; k <= 2; k++)
        {
            int x = hx + k;
            if (x < 0 || x >= gridManager.width)
            {
                continue;
            }

            GridPos pos = new GridPos(x, rowY);
            if (!gridManager.IsInside(pos))
            {
                continue;
            }

            if (gridManager.IsStone(pos))
            {
                continue;
            }

            if (gridManager.cells[pos.x, pos.y] != CellType.Soil || IsOccupiedByAnyRoot(pos))
            {
                continue;
            }

            gridManager.cells[pos.x, pos.y] = CellType.Stone;
            temporaryStones.Add(new TemporaryStone
            {
                pos = pos,
                timer = 4f
            });
            placed++;
            viewDirty = true;
        }

        return placed;
    }

    int RemoveEnemyHalfBottomResources(PlayerRoot enemy)
    {
        bool enemyIsA = enemy == playerA;
        int bottom = gridManager.height - 3;
        int removed = 0;

        for (int x = 0; x < gridManager.width; x++)
        {
            if (!IsEnemyHalfColumn(x, enemyIsA))
            {
                continue;
            }

            for (int y = bottom; y < gridManager.height; y++)
            {
                GridPos pos = new GridPos(x, y);
                if (!gridManager.IsResource(pos))
                {
                    continue;
                }

                if (gridManager.cells[pos.x, pos.y] == CellType.ResourceW)
                {
                    continue;
                }

                gridManager.cells[pos.x, pos.y] = CellType.Soil;
                removed++;
            }
        }

        if (removed > 0)
        {
            viewDirty = true;
        }

        return removed;
    }

    bool IsEnemyHalfColumn(int x, bool enemyIsPlayerA)
    {
        int mid = gridManager.width / 2;
        if (enemyIsPlayerA)
        {
            return x < mid;
        }

        return x >= mid;
    }

    void ResolveMatchTimeUp()
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        if (playerA.Head.y > playerB.Head.y)
        {
            winnerMessage = playerA.playerName + " 更深，时间到获胜";
        }
        else if (playerB.Head.y > playerA.Head.y)
        {
            winnerMessage = playerB.playerName + " 更深，时间到获胜";
        }
        else
        {
            winnerMessage = "平局（深度相同）";
        }

        battleMessage = winnerMessage;
    }

    bool PlaceTemporaryStone(GridPos pos)
    {
        if (!gridManager.IsInside(pos))
        {
            return false;
        }

        if (gridManager.cells[pos.x, pos.y] != CellType.Soil || IsOccupiedByAnyRoot(pos))
        {
            return false;
        }

        gridManager.cells[pos.x, pos.y] = CellType.Stone;
        temporaryStones.Add(new TemporaryStone
        {
            pos = pos,
            timer = temporaryStoneDuration
        });
        viewDirty = true;
        return true;
    }

    void BuildUi()
    {
        if (canvasUi != null)
        {
            return;
        }

        Font font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObj = new GameObject("GameUI");
        canvasUi = canvasObj.AddComponent<Canvas>();
        canvasUi.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560, 1440);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        leftPanel = MakePanel(canvasObj.transform, new Vector2(16f, -18f), new Vector2(400f, 248f), new Vector2(0f, 1f));
        leftText = MakeText(leftPanel.transform, font, new Vector2(18f, -16f), new Vector2(360f, 216f), TextAnchor.UpperLeft, 28);

        rightPanel = MakePanel(canvasObj.transform, new Vector2(-16f, -18f), new Vector2(400f, 248f), new Vector2(1f, 1f));
        rightText = MakeText(rightPanel.transform, font, new Vector2(18f, -16f), new Vector2(360f, 216f), TextAnchor.UpperLeft, 28);

        centerPanel = MakePanel(canvasObj.transform, new Vector2(0f, -18f), new Vector2(660f, 136f), new Vector2(0.5f, 1f));
        centerText = MakeText(centerPanel.transform, font, Vector2.zero, new Vector2(560f, 94f), TextAnchor.MiddleCenter, 34, new Vector2(0.5f, 0.5f));

        skillPanelLeft = CreateSkillSidePanel(canvasObj.transform, font, true, out skillTitleLeft, out skill1ButtonLeft, out skill2ButtonLeft, out skill1TextLeft, out skill2TextLeft);
        skillPanelRight = CreateSkillSidePanel(canvasObj.transform, font, false, out skillTitleRight, out skill1ButtonRight, out skill2ButtonRight, out skill1TextRight, out skill2TextRight);
        skill1ButtonLeft.onClick.AddListener(() => ChooseSkill(TurnPlayer.PlayerA, 0));
        skill2ButtonLeft.onClick.AddListener(() => ChooseSkill(TurnPlayer.PlayerA, 1));
        skill1ButtonRight.onClick.AddListener(() => ChooseSkill(TurnPlayer.PlayerB, 0));
        skill2ButtonRight.onClick.AddListener(() => ChooseSkill(TurnPlayer.PlayerB, 1));

        restartButton = MakeButton(canvasObj.transform, font, new Vector2(0f, 88f), new Vector2(260f, 74f), new Vector2(0.5f, 0.5f), out Text restartText);
        restartText.text = "重新开始";
        restartButton.onClick.AddListener(RestartGame);

        CreateVerticalTurgorBar(canvasObj.transform, font, true, out turgorBarLeftFillRt, out turgorBarLeftFillImg);
        CreateVerticalTurgorBar(canvasObj.transform, font, false, out turgorBarRightFillRt, out turgorBarRightFillImg);
    }

    void CreateVerticalTurgorBar(Transform parent, Font font, bool leftSide, out RectTransform fillRt, out Image fillImage)
    {
        const float barW = 26f;

        GameObject root = new GameObject(leftSide ? "TurgorBarLeft" : "TurgorBarRight");
        root.transform.SetParent(parent, false);
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rootRt.anchorMax = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rootRt.pivot = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rootRt.anchoredPosition = new Vector2(leftSide ? TurgorBarSideInset : -TurgorBarSideInset, 0f);
        rootRt.sizeDelta = new Vector2(barW + 8f, TurgorBarTrackHeight + 30f);

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        RectTransform lr = labelGo.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.5f, 1f);
        lr.anchorMax = new Vector2(0.5f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.anchoredPosition = new Vector2(0f, -4f);
        lr.sizeDelta = new Vector2(96f, 28f);
        Text lt = labelGo.AddComponent<Text>();
        lt.font = font;
        lt.fontSize = 24;
        lt.fontStyle = FontStyle.Bold;
        lt.alignment = TextAnchor.MiddleCenter;
        lt.color = new Color(0.92f, 0.96f, 1f, 1f);
        lt.text = "膨压";
        lt.horizontalOverflow = HorizontalWrapMode.Overflow;
        lt.verticalOverflow = VerticalWrapMode.Overflow;
        Outline lo = labelGo.AddComponent<Outline>();
        lo.effectColor = new Color(0.02f, 0.06f, 0.14f, 0.96f);
        lo.effectDistance = new Vector2(2f, -2f);
        Shadow ls = labelGo.AddComponent<Shadow>();
        ls.effectColor = new Color(0f, 0f, 0f, 0.55f);
        ls.effectDistance = new Vector2(1f, -2f);

        GameObject track = new GameObject("Track");
        track.transform.SetParent(root.transform, false);
        RectTransform tr = track.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 0f);
        tr.anchorMax = new Vector2(0.5f, 0f);
        tr.pivot = new Vector2(0.5f, 0f);
        tr.anchoredPosition = new Vector2(0f, 8f);
        tr.sizeDelta = new Vector2(barW, TurgorBarTrackHeight);
        Image trackImg = track.AddComponent<Image>();
        trackImg.color = TurgorBarTrackBlue;
        Outline o = track.AddComponent<Outline>();
        o.effectColor = new Color(0.35f, 0.55f, 0.85f, 0.45f);
        o.effectDistance = new Vector2(1f, -1f);

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(track.transform, false);
        RectTransform fr = fillGo.AddComponent<RectTransform>();
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(1f, 0f);
        fr.pivot = new Vector2(0.5f, 0f);
        fr.offsetMin = new Vector2(3f, 0f);
        fr.offsetMax = new Vector2(-3f, 0f);

        fillImage = fillGo.AddComponent<Image>();
        fillImage.color = TurgorBarFillBlue;

        fillRt = fr;
    }

    void UpdateTurgorBars()
    {
        if (turgorBarLeftFillRt == null || turgorBarRightFillRt == null)
        {
            return;
        }

        SetTurgorBarFill(playerA, turgorBarLeftFillRt, turgorBarLeftFillImg);
        SetTurgorBarFill(playerB, turgorBarRightFillRt, turgorBarRightFillImg);
    }

    void SetTurgorBarFill(PlayerRoot player, RectTransform fillRt, Image fillImg)
    {
        // 膨压条总高度 = 槽位高度 × 当前精力值（0~max）；上限被减为 50% 时满条高度约为槽的一半
        float fillH = player.skillBlockTimer > 0f
            ? 0f
            : TurgorBarFillMaxHeight * Mathf.Clamp01(player.turgor);
        fillRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fillH);
        fillImg.color = player.skillBlockTimer > 0f
            ? new Color(0.42f, 0.44f, 0.48f, 0.82f)
            : TurgorBarFillBlue;
    }

    Image MakePanel(Transform parent, Vector2 anchoredPos, Vector2 size, Vector2 anchor)
    {
        GameObject go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.1f, 0.07f, 0.58f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.86f, 0.62f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
        shadow.effectDistance = new Vector2(0f, -5f);

        RectTransform rt = image.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        return image;
    }

    Image CreateSkillSidePanel(Transform parent, Font font, bool leftSide, out Text title, out Button button1, out Button button2, out Text text1, out Text text2)
    {
        GameObject go = new GameObject(leftSide ? "SkillPanelLeft" : "SkillPanelRight");
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.1f, 0.07f, 0.72f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.86f, 0.62f, 0.28f);
        outline.effectDistance = new Vector2(2f, -2f);
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
        shadow.effectDistance = new Vector2(0f, -4f);

        RectTransform rt = img.rectTransform;
        rt.sizeDelta = new Vector2(300f, 340f);
        if (leftSide)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -210f);
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -210f);
        }

        int titleSize = 24;
        int btnFont = 22;
        Vector2 titlePos = leftSide ? new Vector2(12f, -10f) : new Vector2(-12f, -10f);
        Vector2 titleBox = leftSide ? new Vector2(276f, 44f) : new Vector2(276f, 44f);
        Vector2 b1Pos = leftSide ? new Vector2(12f, -58f) : new Vector2(-12f, -58f);
        Vector2 b2Pos = leftSide ? new Vector2(12f, -168f) : new Vector2(-12f, -168f);
        Vector2 btnSize = new Vector2(276f, 96f);

        title = MakeText(
            go.transform,
            font,
            titlePos,
            titleBox,
            leftSide ? TextAnchor.UpperLeft : TextAnchor.UpperRight,
            titleSize,
            leftSide ? new Vector2(0f, 1f) : new Vector2(1f, 1f));
        button1 = MakeButton(go.transform, font, b1Pos, btnSize, leftSide ? new Vector2(0f, 1f) : new Vector2(1f, 1f), out text1);
        button2 = MakeButton(go.transform, font, b2Pos, btnSize, leftSide ? new Vector2(0f, 1f) : new Vector2(1f, 1f), out text2);
        text1.fontSize = btnFont;
        text2.fontSize = btnFont;
        text1.alignment = TextAnchor.MiddleCenter;
        text2.alignment = TextAnchor.MiddleCenter;

        return img;
    }

    Text MakeText(Transform parent, Font font, Vector2 anchoredPos, Vector2 size, TextAnchor anchor, int fontSize = 24, Vector2? pivotAnchor = null)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = new Color(1f, 0.97f, 0.92f, 1f);
        text.supportRichText = true;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.04f, 0.03f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -2f);

        RectTransform rt = text.rectTransform;
        Vector2 finalAnchor = pivotAnchor ?? new Vector2(0f, 1f);
        rt.anchorMin = finalAnchor;
        rt.anchorMax = finalAnchor;
        rt.pivot = finalAnchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        return text;
    }

    Button MakeButton(Transform parent, Font font, Vector2 anchoredPos, Vector2 size, Vector2 anchor, out Text buttonText)
    {
        GameObject go = new GameObject("Button");
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.28f, 0.2f, 0.12f, 0.96f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.9f, 0.64f, 0.24f);
        outline.effectDistance = new Vector2(2f, -2f);
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
        shadow.effectDistance = new Vector2(0f, -4f);

        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 0.95f, 0.78f, 1f);
        colors.pressedColor = new Color(0.9f, 0.82f, 0.62f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        buttonText = MakeText(go.transform, font, Vector2.zero, size - new Vector2(24f, 18f), TextAnchor.MiddleCenter, 30, new Vector2(0.5f, 0.5f));
        return button;
    }

    void RefreshUi()
    {
        if (canvasUi == null)
        {
            return;
        }

        leftText.text = BuildPlayerText(playerA, false);
        rightText.text = BuildPlayerText(playerB, false);
        UpdateTurgorBars();

        if (gameOver)
        {
            centerText.text = winnerMessage + "\n点击下方按钮重新开始";
        }
        else if (pendingSkillUnlockA != SkillUnlockType.None || pendingSkillUnlockB != SkillUnlockType.None)
        {
            centerText.text = battleMessage + "\n（侧边选择技能，对战不暂停）";
        }
        else
        {
            string nearEnd = string.Empty;
            int deepest = Mathf.Max(
                playerA.body.Count > 0 ? playerA.Head.y : 0,
                playerB.body.Count > 0 ? playerB.Head.y : 0);
            if (deepest >= gridManager.height - 1 - 10)
            {
                nearEnd = "（接近终点）\n";
            }

            centerText.text = nearEnd + "实时：A/S/D 与 J/K/L 可同时操作\n" + battleMessage;
        }

        bool showLeftSkill = pendingSkillUnlockA != SkillUnlockType.None && !gameOver;
        bool showRightSkill = pendingSkillUnlockB != SkillUnlockType.None && !gameOver;
        skillPanelLeft.gameObject.SetActive(showLeftSkill);
        skillPanelRight.gameObject.SetActive(showRightSkill);

        if (showLeftSkill)
        {
            NormalizePendingChoices(pendingChoicesA);
            string title = GetUnlockTitle(pendingSkillUnlockA);
            skillTitleLeft.text = title + "\n<color=#cccccc>Q / E 或 1 / 2</color>";
            skill1TextLeft.text = "上：" + pendingChoicesA[0].title + "\n" + pendingChoicesA[0].description;
            skill2TextLeft.text = "下：" + pendingChoicesA[1].title + "\n" + pendingChoicesA[1].description;
        }

        if (showRightSkill)
        {
            NormalizePendingChoices(pendingChoicesB);
            string title = GetUnlockTitle(pendingSkillUnlockB);
            skillTitleRight.text = title + "\n<color=#cccccc>U / O 或小键盘 1 / 2</color>";
            skill1TextRight.text = "上：" + pendingChoicesB[0].title + "\n" + pendingChoicesB[0].description;
            skill2TextRight.text = "下：" + pendingChoicesB[1].title + "\n" + pendingChoicesB[1].description;
        }

        restartButton.gameObject.SetActive(gameOver);
    }

    void NormalizePendingChoices(SkillChoice[] pendingChoices)
    {
        for (int i = 0; i < pendingChoices.Length; i++)
        {
            if (pendingChoices[i].type == SkillType.RootSnare)
            {
                pendingChoices[i] = new SkillChoice(SkillType.RootSnare, "根须绞杀", "敌方根尖沿原路径后退 2 格");
            }
        }
    }

    void UpdateUiAnimations()
    {
        if (canvasUi == null)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
        leftPanel.color = !gameOver
            ? Color.Lerp(new Color(0.18f, 0.14f, 0.09f, 0.62f), new Color(0.4f, 0.28f, 0.14f, 0.74f), pulse)
            : new Color(0.12f, 0.1f, 0.07f, 0.58f);

        rightPanel.color = !gameOver
            ? Color.Lerp(new Color(0.1f, 0.14f, 0.1f, 0.62f), new Color(0.16f, 0.32f, 0.2f, 0.74f), pulse)
            : new Color(0.12f, 0.1f, 0.07f, 0.58f);

        centerPanel.color = gameOver
            ? Color.Lerp(new Color(0.28f, 0.2f, 0.1f, 0.68f), new Color(0.52f, 0.36f, 0.14f, 0.8f), pulse)
            : new Color(0.12f, 0.1f, 0.07f, 0.58f);

        if (skillPanelLeft != null)
        {
            skillPanelLeft.transform.localScale = pendingSkillUnlockA != SkillUnlockType.None && !gameOver
                ? Vector3.one * Mathf.Lerp(0.985f, 1.025f, pulse)
                : Vector3.one;
        }

        if (skillPanelRight != null)
        {
            skillPanelRight.transform.localScale = pendingSkillUnlockB != SkillUnlockType.None && !gameOver
                ? Vector3.one * Mathf.Lerp(0.985f, 1.025f, pulse)
                : Vector3.one;
        }
    }

    string BuildPlayerText(PlayerRoot player, bool active)
    {
        List<string> statuses = new List<string>();

        if (active)
        {
            statuses.Add("当前行动");
        }

        if (player.stunTimer > 0f)
        {
            statuses.Add("僵直");
        }

        if (player.forceLeftNextMove)
        {
            statuses.Add("下次强制左移");
        }

        if (player.IsControlReversed)
        {
            statuses.Add("左右反转");
        }

        if (player.resourceSiphonTimer > 0f)
        {
            statuses.Add("资源虹吸");
        }

        if (player.nextDashDownDistance > 1)
        {
            statuses.Add("已储备冲刺");
        }

        if (player.growthSpeedMultiplier > 1f)
        {
            statuses.Add("生长速度 x" + player.growthSpeedMultiplier.ToString("0.0"));
        }

        if (player.penetrateStoneTimer > 0f)
        {
            statuses.Add("穿透中");
        }

        if (player.burstGrowthNoCostTimer > 0f)
        {
            statuses.Add("爆发生长");
        }

        if (player.hasBreakTarget)
        {
            statuses.Add("冲石 " + player.breakStoneHits + "/3");
        }

        if (player.ultimateTurgorPenaltyTimer > 0f && player.maxTurgorMultiplier < 0.999f)
        {
            statuses.Add("膨压上限 " + Mathf.RoundToInt(player.maxTurgorMultiplier * 100f) + "%（" + player.ultimateTurgorPenaltyTimer.ToString("0.0") + "s）");
        }

        string statusText = statuses.Count > 0 ? string.Join("、", statuses.ToArray()) : "无";

        float maxT = Mathf.Max(0.01f, player.EffectiveMaxTurgor);
        string turgorLine = player.skillBlockTimer > 0f
            ? "膨压：???（干扰中）"
            : "膨压：" + Mathf.RoundToInt(player.turgor / maxT * 100f) + "%"
                + (player.ultimateTurgorPenaltyTimer > 0f && player.maxTurgorMultiplier < 0.999f
                    ? "（上限 " + Mathf.RoundToInt(player.maxTurgorMultiplier * 100f) + "%）"
                    : string.Empty);

        return player.playerName
            + "\n深度：" + (player.Head.y + 1)
            + "    长度：" + player.body.Count
            + "\n" + turgorLine
            + "\n氮：" + player.nitrogen
            + "  磷：" + player.phosphorus
            + "  钾：" + player.potassium
            + "\n状态：" + statusText;
    }

    string GetUnlockTitle(SkillUnlockType unlock)
    {
        if (unlock == SkillUnlockType.NpkSet)
        {
            return "已解锁：氮 + 磷 + 钾 套装";
        }

        if (unlock == SkillUnlockType.TripleResource)
        {
            return "已解锁：同种资源 3 个";
        }

        if (unlock == SkillUnlockType.Ultimate)
        {
            return "已解锁：终极技能";
        }

        return string.Empty;
    }

    void RestartGame()
    {
        respawns.Clear();
        temporaryStones.Clear();
        pendingSkillUnlockA = SkillUnlockType.None;
        pendingSkillUnlockB = SkillUnlockType.None;
        pendingChoicesA = new SkillChoice[2];
        pendingChoicesB = new SkillChoice[2];
        gameOver = false;
        winnerMessage = string.Empty;
        battleMessage = "目标：率先到达最底层（双方可同时移动）";
        matchTimerRemaining = matchTimeLimitSeconds;
        gridManager.InitGrid();
        ResetPlayers();
        RefreshUi();
        viewDirty = true;
    }

    void ResolveBodyCollisionV2(PlayerRoot attacker, PlayerRoot defender)
    {
        attacker.stunTimer = Mathf.Max(attacker.stunTimer, 1f);
        attacker.ResetBreakAttempt();
        defender.ResetBreakAttempt();
        battleMessage = attacker.playerName + " 撞入敌方根身，撞击方短暂僵直";
        viewDirty = true;
    }
}
