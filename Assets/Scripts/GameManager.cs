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
    public Font uiFont;

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
        Entangle,
        Wither,
        ReverseControl,
        Penetrate,
        BurstGrowth,
        ResourceSiphon,
        Collapse,
        RootSnare,
        NutrientSteal,
        CatchUp,
        PruneRoot
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

    public TurnPlayer currentTurn = TurnPlayer.PlayerA;

    SkillUnlockType pendingSkillUnlock = SkillUnlockType.None;
    TurnPlayer pendingSkillPlayer = TurnPlayer.PlayerA;
    SkillChoice[] pendingChoices = new SkillChoice[2];
    string winnerMessage = string.Empty;
    string battleMessage = string.Empty;

    Canvas canvasUi;
    Image leftPanel;
    Image rightPanel;
    Image centerPanel;
    Image skillPanel;
    Text leftText;
    Text rightText;
    Text centerText;
    Text skillTitleText;
    Text skill1Text;
    Text skill2Text;
    Button skill1Button;
    Button skill2Button;
    Button restartButton;

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
        UpdateRespawns(Time.deltaTime);
        UpdateTemporaryStones(Time.deltaTime);
        UpdateUiAnimations();

        if (gameOver)
        {
            RefreshUi();
            return;
        }

        if (pendingSkillUnlock == SkillUnlockType.None)
        {
            EvaluateUltimateUnlockForCurrentTurn();
        }

        if (pendingSkillUnlock != SkillUnlockType.None)
        {
            HandleSkillInput();
            RefreshUi();
            return;
        }

        PlayerRoot current = GetCurrentPlayer();
        if (current.stunnedTurns > 0)
        {
            current.stunnedTurns--;
            battleMessage = current.playerName + " 本回合僵直";
            EndTurn();
            RefreshUi();
            return;
        }

        if (TryReadMoveInput(out MoveDirection inputDirection))
        {
            MoveDirection finalDirection = ApplyDirectionModifiers(current, inputDirection);
            TryMoveCurrentPlayer(finalDirection);
        }

        RefreshUi();
    }

    void ResetPlayers()
    {
        ResetPlayerState(playerA);
        ResetPlayerState(playerB);
        playerA.body.Add(new GridPos(2, 0));
        playerB.body.Add(new GridPos(gridManager.width - 3, 0));
        pendingSkillUnlock = SkillUnlockType.None;
        pendingChoices = new SkillChoice[2];
        currentTurn = TurnPlayer.PlayerA;
        battleMessage = "目标：率先到达最底层";
    }

    void ResetPlayerState(PlayerRoot player)
    {
        player.body.Clear();
        player.ClearResources();
        player.stunnedTurns = 0;
        player.forceLeftNextMove = false;
        player.reverseControlsTimer = 0f;
        player.resourceSiphonTimer = 0f;
        player.ResetGrowthSpeed();
        player.nextDashDownDistance = 0;
        player.canPenetrateStoneNextMove = false;
        player.hasUsedUltimate = false;
        player.ResetBreakAttempt();
    }

    bool TryReadMoveInput(out MoveDirection dir)
    {
        dir = MoveDirection.Down;

        if (currentTurn == TurnPlayer.PlayerA)
        {
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
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                dir = MoveDirection.Left;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                dir = MoveDirection.Right;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                dir = MoveDirection.Down;
                return true;
            }
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

    public void TryMoveCurrentPlayer(MoveDirection dir)
    {
        PlayerRoot current = GetCurrentPlayer();
        PlayerRoot enemy = GetEnemyPlayer();

        if (dir == MoveDirection.Down && current.nextDashDownDistance > 1)
        {
            int dashDistance = current.nextDashDownDistance;
            current.nextDashDownDistance = 0;
            int moved = GrowDownSteps(current, enemy, dashDistance);
            battleMessage = moved > 0
                ? current.playerName + " 发动冲刺，向下延展 " + moved + " 格"
                : current.playerName + " 的冲刺被阻挡";

            if (!gameOver && pendingSkillUnlock == SkillUnlockType.None)
            {
                EndTurn();
            }

            return;
        }

        GridPos target = current.Head + Offset(dir);

        if (!gridManager.IsInside(target))
        {
            battleMessage = "该方向超出地图";
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
            if (current.canPenetrateStoneNextMove)
            {
                current.canPenetrateStoneNextMove = false;
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

        if (!player.hasBreakTarget || player.breakTarget.x != target.x || player.breakTarget.y != target.y)
        {
            player.hasBreakTarget = true;
            player.breakTarget = target;
            player.breakStoneHits = 0;
        }

        player.breakStoneHits++;

        if (player.breakStoneHits < 3)
        {
            battleMessage = player.playerName + " 冲击石头 " + player.breakStoneHits + "/3";
            viewDirty = true;
            EndTurn();
            return true;
        }

        gridManager.cells[target.x, target.y] = CellType.Soil;
        StepIntoCell(player, target);
        ApplyGrowthSpeedBonus(player, GetEnemyPlayerFor(player));
        player.ResetBreakAttempt();
        battleMessage = player.playerName + " 击碎石头并向前延展";
        FinalizeSuccessfulAction(player);
        return true;
    }

    void ResolveHeadCollision(PlayerRoot current, PlayerRoot enemy)
    {
        current.stunnedTurns = Mathf.Max(current.stunnedTurns, 1);
        enemy.stunnedTurns = Mathf.Max(enemy.stunnedTurns, 1);

        CellType fromEnemy = TakeRandomResource(enemy);
        CellType fromCurrent = TakeRandomResource(current);

        GiveResource(current, fromEnemy);
        GiveResource(enemy, fromCurrent);

        current.ResetBreakAttempt();
        enemy.ResetBreakAttempt();
        battleMessage = "头部相撞，双方僵直并互偷一个资源";
        viewDirty = true;
        EndTurn();
    }

    void ResolveBodyCollision(PlayerRoot attacker, PlayerRoot defender)
    {
        int retreated = attacker.RetreatHead(3);
        GiveRandomCombatResource(defender);
        attacker.ResetBreakAttempt();
        defender.ResetBreakAttempt();
        battleMessage = "撞上敌方根身，撞击方后退 1 格，被撞方获得 1 个随机资源";
        viewDirty = true;
        EndTurn();
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
        if (pendingSkillUnlock == SkillUnlockType.None)
        {
            EndTurn();
        }
    }

    int GrowDownSteps(PlayerRoot player, PlayerRoot enemy, int steps)
    {
        int moved = 0;

        for (int i = 0; i < steps; i++)
        {
            GridPos target = player.Head + Offset(MoveDirection.Down);
            if (!CanGrowStraightDown(player, enemy, target))
            {
                break;
            }

            StepIntoCell(player, target);
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
            GridPos target = player.Head + Offset(MoveDirection.Down);
            if (!CanGrowStraightDown(player, enemy, target))
            {
                break;
            }

            StepIntoCell(player, target);
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
            return false;
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

    PlayerRoot GetCurrentPlayer()
    {
        return currentTurn == TurnPlayer.PlayerA ? playerA : playerB;
    }

    PlayerRoot GetEnemyPlayer()
    {
        return currentTurn == TurnPlayer.PlayerA ? playerB : playerA;
    }

    PlayerRoot GetEnemyPlayerFor(PlayerRoot player)
    {
        return player == playerA ? playerB : playerA;
    }

    void EndTurn()
    {
        currentTurn = currentTurn == TurnPlayer.PlayerA ? TurnPlayer.PlayerB : TurnPlayer.PlayerA;
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
        int amount = player.resourceSiphonTimer > 0f ? 2 : 1;

        for (int i = 0; i < amount; i++)
        {
            GiveResource(player, resourceType);
        }

        gridManager.cells[pos.x, pos.y] = CellType.Soil;
        respawns.Add(new RespawnRequest
        {
            timer = resourceRespawnDelay,
            minRow = Mathf.Clamp(pos.y + 2, 2, gridManager.height - 2)
        });
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

    void EvaluateUltimateUnlockForCurrentTurn()
    {
        PlayerRoot current = GetCurrentPlayer();
        PlayerRoot enemy = GetEnemyPlayer();

        if (current.hasUsedUltimate)
        {
            return;
        }

        if (enemy.Head.y - current.Head.y < 5)
        {
            return;
        }

        pendingSkillUnlock = SkillUnlockType.Ultimate;
        pendingSkillPlayer = currentTurn;
        pendingChoices[0] = new SkillChoice(SkillType.CatchUp, "追平深度", "立即向下追到与敌方相同深度，遇阻则停止");
        pendingChoices[1] = new SkillChoice(SkillType.PruneRoot, "断尾提速", "清除自身所有身体，只保留头部");
        battleMessage = current.playerName + " 落后 5 格以上，触发终极技能";
    }

    void EvaluateSkillUnlock(PlayerRoot player)
    {
        if (pendingSkillUnlock != SkillUnlockType.None)
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

        pendingSkillUnlock = unlockType;
        pendingSkillPlayer = currentTurn;
        PrepareSkillChoices(unlockType);
        battleMessage = player.playerName + " 解锁了技能选择";
    }

    void PrepareSkillChoices(SkillUnlockType unlockType)
    {
        if (unlockType == SkillUnlockType.NpkSet)
        {
            SkillChoice[] selfPool =
            {
                new SkillChoice(SkillType.Sprint, "冲刺", "下一次向下直接延展 3 格，不能穿过障碍"),
                new SkillChoice(SkillType.DrawWater, "汲水", "立刻获得 1 个水")
            };

            SkillChoice[] enemyPool =
            {
                new SkillChoice(SkillType.Entangle, "缠绕", "敌方下次移动强制向左"),
                new SkillChoice(SkillType.Wither, "枯竭", "清除敌方根尖附近 3 格内的资源"),
                new SkillChoice(SkillType.ReverseControl, "迷向", "敌方 3 秒内左右方向反转")
            };

            pendingChoices[0] = selfPool[Random.Range(0, selfPool.Length)];
            pendingChoices[1] = enemyPool[Random.Range(0, enemyPool.Length)];
            return;
        }

        SkillChoice[] tripleSelfPool =
        {
            new SkillChoice(SkillType.Penetrate, "穿透", "下一次移动可穿过 1 个石头"),
            new SkillChoice(SkillType.BurstGrowth, "爆发生长", "立刻向下延展最多 5 格"),
            new SkillChoice(SkillType.ResourceSiphon, "资源虹吸", "5 秒内采集资源按双倍计算")
        };

        SkillChoice[] tripleEnemyPool =
        {
            new SkillChoice(SkillType.Collapse, "塌方", "在敌方前方生成 1 个临时石头"),
            new SkillChoice(SkillType.RootSnare, "根须绞杀", "敌方删除最后 2 节身体"),
            new SkillChoice(SkillType.NutrientSteal, "养分掠夺", "偷取敌方各 1 个氮磷钾")
        };

        pendingChoices[0] = tripleSelfPool[Random.Range(0, tripleSelfPool.Length)];
        pendingChoices[1] = tripleEnemyPool[Random.Range(0, tripleEnemyPool.Length)];
    }

    void HandleSkillInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            ChooseSkill(0);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            ChooseSkill(1);
        }
    }

    void ChooseSkill(int index)
    {
        if (index < 0 || index >= pendingChoices.Length)
        {
            return;
        }

        NormalizePendingChoices();

        PlayerRoot player = pendingSkillPlayer == TurnPlayer.PlayerA ? playerA : playerB;
        PlayerRoot enemy = pendingSkillPlayer == TurnPlayer.PlayerA ? playerB : playerA;

        SkillChoice choice = pendingChoices[index];
        bool consumesResources = pendingSkillUnlock == SkillUnlockType.NpkSet || pendingSkillUnlock == SkillUnlockType.TripleResource;

        if (consumesResources)
        {
            player.ClearResources();
        }

        ApplySkill(choice.type, player, enemy);

        if (pendingSkillUnlock == SkillUnlockType.Ultimate)
        {
            player.hasUsedUltimate = true;
        }

        pendingSkillUnlock = SkillUnlockType.None;
        pendingChoices[0] = new SkillChoice();
        pendingChoices[1] = new SkillChoice();

        if (!gameOver)
        {
            EndTurn();
        }

        viewDirty = true;
    }

    void ApplySkill(SkillType skillType, PlayerRoot player, PlayerRoot enemy)
    {
        if (skillType == SkillType.RootSnare)
        {
            int retreated = enemy.RetreatHead(2);
            battleMessage = retreated > 0
                ? enemy.playerName + " 被根须绞杀，沿原路径后退 " + retreated + " 格"
                : enemy.playerName + " 的根尖无法继续后退";
            viewDirty = true;
            return;
        }

        if (skillType == SkillType.PruneRoot)
        {
            int removed = player.PruneToMainRoot(6);
            player.growthSpeedMultiplier = 1.2f;
            battleMessage = removed > 0
                ? player.playerName + " 收缩老根，仅保留前端主根 6 节，生长速度提高到 1.2 倍"
                : player.playerName + " 获得 1.2 倍生长速度";
            viewDirty = true;
            return;
        }

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
        else if (skillType == SkillType.Entangle)
        {
            enemy.forceLeftNextMove = true;
            battleMessage = enemy.playerName + " 下次移动被强制向左";
        }
        else if (skillType == SkillType.Wither)
        {
            int removed = RemoveNearbyResources(enemy.Head, 3);
            battleMessage = "枯竭生效，清除了 " + removed + " 个资源";
        }
        else if (skillType == SkillType.ReverseControl)
        {
            enemy.reverseControlsTimer = Mathf.Max(enemy.reverseControlsTimer, 3f);
            battleMessage = enemy.playerName + " 进入 3 秒迷向状态";
        }
        else if (skillType == SkillType.Penetrate)
        {
            player.canPenetrateStoneNextMove = true;
            battleMessage = player.playerName + " 获得一次穿透";
        }
        else if (skillType == SkillType.BurstGrowth)
        {
            int moved = GrowDownSteps(player, enemy, 5);
            battleMessage = moved > 0
                ? player.playerName + " 爆发生长，向下延展 " + moved + " 格"
                : player.playerName + " 的爆发生长被阻挡";
        }
        else if (skillType == SkillType.ResourceSiphon)
        {
            player.resourceSiphonTimer = Mathf.Max(player.resourceSiphonTimer, 5f);
            battleMessage = player.playerName + " 进入 5 秒资源虹吸";
        }
        else if (skillType == SkillType.Collapse)
        {
            if (PlaceTemporaryStone(enemy.Head + Offset(MoveDirection.Down)))
            {
                battleMessage = "塌方生效，敌方前方出现临时石头";
            }
            else
            {
                battleMessage = "塌方未能生成石头";
            }
        }
        else if (skillType == SkillType.RootSnare)
        {
            enemy.TrimFromTail(2);
            battleMessage = enemy.playerName + " 被切断了 2 节根身";
        }
        else if (skillType == SkillType.NutrientSteal)
        {
            int stolen = StealNpk(player, enemy);
            battleMessage = "养分掠夺生效，偷取了 " + stolen + " 个氮磷钾";
        }
        else if (skillType == SkillType.CatchUp)
        {
            int moved = GrowDownToDepth(player, enemy, enemy.Head.y);
            battleMessage = moved > 0
                ? player.playerName + " 追平了 " + moved + " 格深度"
                : player.playerName + " 的追平深度被阻挡";
        }
        else if (skillType == SkillType.PruneRoot)
        {
            player.KeepOnlyHead();
            battleMessage = player.playerName + " 清除了多余根身，只保留根尖";
        }

        viewDirty = true;
    }

    int RemoveNearbyResources(GridPos center, int range)
    {
        int removed = 0;

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                GridPos pos = new GridPos(x, y);
                int distance = Mathf.Abs(pos.x - center.x) + Mathf.Abs(pos.y - center.y);
                if (distance > range || !gridManager.IsResource(pos))
                {
                    continue;
                }

                gridManager.cells[x, y] = CellType.Soil;
                removed++;
            }
        }

        if (removed > 0)
        {
            viewDirty = true;
        }

        return removed;
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

    int StealNpk(PlayerRoot receiver, PlayerRoot target)
    {
        int total = 0;

        if (target.nitrogen > 0)
        {
            target.nitrogen--;
            receiver.nitrogen++;
            total++;
        }

        if (target.phosphorus > 0)
        {
            target.phosphorus--;
            receiver.phosphorus++;
            total++;
        }

        if (target.potassium > 0)
        {
            target.potassium--;
            receiver.potassium++;
            total++;
        }

        return total;
    }

    int GrowDownToDepth(PlayerRoot player, PlayerRoot enemy, int targetDepth)
    {
        int moved = 0;
        while (player.Head.y < targetDepth && !gameOver)
        {
            GridPos next = player.Head + Offset(MoveDirection.Down);
            if (!CanGrowStraightDown(player, enemy, next))
            {
                break;
            }

            StepIntoCell(player, next);
            moved++;

            if (player.Head.y >= gridManager.height - 1)
            {
                gameOver = true;
                winnerMessage = player.playerName + " 获胜";
                battleMessage = winnerMessage;
                break;
            }
        }

        return moved;
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

        leftPanel = MakePanel(canvasObj.transform, new Vector2(16f, -18f), new Vector2(390f, 214f), new Vector2(0f, 1f));
        leftText = MakeText(leftPanel.transform, font, new Vector2(18f, -16f), new Vector2(324f, 168f), TextAnchor.UpperLeft, 32);

        rightPanel = MakePanel(canvasObj.transform, new Vector2(-16f, -18f), new Vector2(390f, 214f), new Vector2(1f, 1f));
        rightText = MakeText(rightPanel.transform, font, new Vector2(18f, -16f), new Vector2(324f, 168f), TextAnchor.UpperLeft, 32);

        centerPanel = MakePanel(canvasObj.transform, new Vector2(0f, -18f), new Vector2(660f, 136f), new Vector2(0.5f, 1f));
        centerText = MakeText(centerPanel.transform, font, Vector2.zero, new Vector2(560f, 94f), TextAnchor.MiddleCenter, 34, new Vector2(0.5f, 0.5f));

        skillPanel = MakePanel(canvasObj.transform, new Vector2(0f, -120f), new Vector2(960f, 220f), new Vector2(0.5f, 0.5f));
        skillTitleText = MakeText(skillPanel.transform, font, new Vector2(0f, -14f), new Vector2(860f, 36f), TextAnchor.UpperCenter, 36, new Vector2(0.5f, 1f));

        skill1Button = MakeButton(skillPanel.transform, font, new Vector2(28f, 22f), new Vector2(410f, 134f), new Vector2(0f, 0f), out skill1Text);
        skill2Button = MakeButton(skillPanel.transform, font, new Vector2(-28f, 22f), new Vector2(410f, 134f), new Vector2(1f, 0f), out skill2Text);
        skill1Button.onClick.AddListener(() => ChooseSkill(0));
        skill2Button.onClick.AddListener(() => ChooseSkill(1));

        restartButton = MakeButton(canvasObj.transform, font, new Vector2(0f, 88f), new Vector2(260f, 74f), new Vector2(0.5f, 0.5f), out Text restartText);
        restartText.text = "重新开始";
        restartText.text = "重新开始";
        restartButton.onClick.AddListener(RestartGame);
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

        leftText.text = BuildPlayerText(playerA, currentTurn == TurnPlayer.PlayerA);
        rightText.text = BuildPlayerText(playerB, currentTurn == TurnPlayer.PlayerB);

        if (gameOver)
        {
            centerText.text = winnerMessage + "\n点击下方按钮重新开始";
        }
        else if (pendingSkillUnlock != SkillUnlockType.None)
        {
            centerText.text = GetUnlockTitle() + "\n按 1 / 2 或点击按钮选择一个技能";
        }
        else
        {
            centerText.text = "当前行动：" + GetCurrentPlayer().playerName + "\n" + battleMessage;
        }

        bool showSkill = pendingSkillUnlock != SkillUnlockType.None && !gameOver;
        skillPanel.gameObject.SetActive(showSkill);

        if (showSkill)
        {
            NormalizePendingChoices();
            skillTitleText.text = GetUnlockTitle();
            skill1Text.text = "1. " + pendingChoices[0].title + "\n" + pendingChoices[0].description;
            skill2Text.text = "2. " + pendingChoices[1].title + "\n" + pendingChoices[1].description;
        }

        restartButton.gameObject.SetActive(gameOver);
    }

    void NormalizePendingChoices()
    {
        for (int i = 0; i < pendingChoices.Length; i++)
        {
            if (pendingChoices[i].type == SkillType.RootSnare)
            {
                pendingChoices[i] = new SkillChoice(SkillType.RootSnare, "根须绞杀", "敌方根尖沿原路径后退 2 格");
            }
            else if (pendingChoices[i].type == SkillType.PruneRoot)
            {
                pendingChoices[i] = new SkillChoice(SkillType.PruneRoot, "断尾提速", "收缩老根，仅保留前端主根 6 节，并获得 1.2 倍生长速度");
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
        leftPanel.color = currentTurn == TurnPlayer.PlayerA && !gameOver
            ? Color.Lerp(new Color(0.22f, 0.16f, 0.1f, 0.66f), new Color(0.48f, 0.3f, 0.14f, 0.78f), pulse)
            : new Color(0.12f, 0.1f, 0.07f, 0.58f);

        rightPanel.color = currentTurn == TurnPlayer.PlayerB && !gameOver
            ? Color.Lerp(new Color(0.1f, 0.14f, 0.1f, 0.66f), new Color(0.18f, 0.34f, 0.2f, 0.78f), pulse)
            : new Color(0.12f, 0.1f, 0.07f, 0.58f);

        centerPanel.color = gameOver
            ? Color.Lerp(new Color(0.28f, 0.2f, 0.1f, 0.68f), new Color(0.52f, 0.36f, 0.14f, 0.8f), pulse)
            : new Color(0.12f, 0.1f, 0.07f, 0.58f);

        skillPanel.transform.localScale = pendingSkillUnlock != SkillUnlockType.None && !gameOver
            ? Vector3.one * Mathf.Lerp(0.985f, 1.025f, pulse)
            : Vector3.one;
    }

    string BuildPlayerText(PlayerRoot player, bool active)
    {
        List<string> statuses = new List<string>();

        if (active)
        {
            statuses.Add("当前行动");
        }

        if (player.stunnedTurns > 0)
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

        if (player.canPenetrateStoneNextMove)
        {
            statuses.Add("已储备穿透");
        }

        if (player.hasBreakTarget)
        {
            statuses.Add("冲石 " + player.breakStoneHits + "/3");
        }

        string statusText = statuses.Count > 0 ? string.Join("、", statuses.ToArray()) : "无";

        return player.playerName
            + "\n深度：" + (player.Head.y + 1)
            + "    长度：" + player.body.Count
            + "\n氮：" + player.nitrogen
            + "    磷：" + player.phosphorus
            + "\n钾：" + player.potassium
            + "    水：" + player.water
            + "\n状态：" + statusText;
    }

    string GetUnlockTitle()
    {
        if (pendingSkillUnlock == SkillUnlockType.NpkSet)
        {
            return "已解锁：氮 + 磷 + 钾 套装";
        }

        if (pendingSkillUnlock == SkillUnlockType.TripleResource)
        {
            return "已解锁：同种资源 3 个";
        }

        if (pendingSkillUnlock == SkillUnlockType.Ultimate)
        {
            return "已解锁：终极技能";
        }

        return string.Empty;
    }

    void RestartGame()
    {
        respawns.Clear();
        temporaryStones.Clear();
        pendingSkillUnlock = SkillUnlockType.None;
        pendingChoices = new SkillChoice[2];
        gameOver = false;
        winnerMessage = string.Empty;
        battleMessage = "目标：率先到达最底层";
        gridManager.InitGrid();
        ResetPlayers();
        RefreshUi();
        viewDirty = true;
    }

    void ResolveBodyCollisionV2(PlayerRoot attacker, PlayerRoot defender)
    {
        int retreated = attacker.RetreatHead(3);
        GiveRandomCombatResource(defender);
        attacker.ResetBreakAttempt();
        defender.ResetBreakAttempt();
        battleMessage = "撞上敌方根身，撞击方沿原路径后退 " + retreated + " 格，被撞方获得 1 个随机养分";
        viewDirty = true;
        EndTurn();
    }

    void GiveRandomCombatResource(PlayerRoot player)
    {
        int roll = Random.Range(0, 3);
        GiveResource(player, (CellType)((int)CellType.ResourceN + roll));
    }
}
