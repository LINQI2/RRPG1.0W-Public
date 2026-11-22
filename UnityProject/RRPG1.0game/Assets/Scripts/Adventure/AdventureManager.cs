using System.Collections;
using System.Collections.Generic;
using RRPG.StaticConfig;
using UnityEngine;

/// <summary>
/// 冒险管理器
///
/// 负责：管理完整冒险流程
///   远征准备 → 地图探索 → 节点事件（战斗/事件/商店等）→ 结算 → 回到地图 → ... → Boss 结束
/// 不负责：地图生成、UI 显示
///
/// 由 ExpeditionUI 在确认出征后创建
/// Battle V0.1 / WP-C：不再承载独立道具卡列表（Item 产品语义已退出正式路径）。
///
/// WP-Encounter-1C：本类同时是 **Active Encounter（当前敌方阵容）** 的宿主
/// （Encounter 适配层见 AdventureEncounterRoster.cs：SessionSeed / EncounterSeed / Active EnemyRoster 生命周期）。
/// </summary>
public partial class AdventureManager
{
    /// <summary>地图生成器</summary>
    public MapGenerator MapGen { get; private set; }

    /// <summary>玩家数据</summary>
    public PlayerCollection PlayerData { get; private set; }

    /// <summary>当前冒险是否进行中</summary>
    public bool IsActive { get; private set; }

    /// <summary>当前冒险是否胜利（打完 Boss）</summary>
    public bool IsVictory { get; private set; }

    /// <summary>
    /// Run 运行时状态宿主（Stage 6A）：人物 CurrentHP / Run Deck / 领悟 / Pending Rewards。
    /// 随本次远征创建，远征结束时通过 EndRun 处理待结算收益。
    /// </summary>
    public RunState RunState { get; private set; }

    /// <summary>Run 配置（测试值，未冻结；未传入时使用默认实例）</summary>
    public RunConfig RunConfig { get; private set; }

    /// <summary>
    /// WP-1 Expedition Progression 唯一规则入口（Orchestrator 收敛；会话内，不持久化）。
    /// 在调用 <see cref="StartProgression"/> 成功后非 null。所有推进写操作经此 Resolver；
    /// Legacy Map runtime flags 不成为 Progression Authority。
    /// </summary>
    public ExpeditionProgressionResolver Progression { get; private set; }

    /// <summary>当前 Progression 会话真相（未 StartProgression / 初始化失败时为 null）。</summary>
    public ExpeditionProgressionState ProgressionState => Progression?.State;

    /// <summary>会话累计 elapsed 天数（integer Village Days；未初始化时为 0）。</summary>
    public int ExpeditionElapsedDays => ProgressionState?.ExpeditionElapsedDays ?? 0;
    public long ExpeditionElapsedMinutes => ProgressionState?.ExpeditionElapsedMinutes ?? 0;

    // 战斗相关缓存（用于重新进入战斗时传入数据）
    private List<CharacterCardData> _playerChars;
    private HeroInstance _playerHero;
    private List<CharacterData> _allCharDatas;
    private List<SkillData> _allSkills;

    /// <summary>
    /// M2 Slice 2A（C2）：敌方 Monster 定义 Authority = StaticConfig 的 <see cref="MonsterDefinition"/>。
    /// 本类只**持有 + 透传**，不读 static 字段；**不实现** fallback 判定（由 StaticConfigRegistry 负责）。
    /// </summary>
    private IReadOnlyList<MonsterDefinition> _enemyMonsters;

    /// <summary>
    /// 创建冒险管理器（Stage 6A：同时创建 Run 状态宿主并播种 Run Deck）
    /// </summary>
    /// <param name="runConfig">Run 配置（测试值，未冻结；null 时使用默认实例）</param>
    public AdventureManager(PlayerCollection playerData,
        List<CharacterCardData> playerChars,
        HeroInstance playerHero, List<CharacterData> allCharDatas,
        List<SkillData> allSkills, IReadOnlyList<MonsterDefinition> enemyMonsters,
        RunConfig runConfig = null)
    {
        PlayerData = playerData;
        _playerChars = playerChars;
        _playerHero = playerHero;
        _allCharDatas = allCharDatas;
        _allSkills = allSkills;
        _enemyMonsters = enemyMonsters;

        // Stage 6A：Run 配置 + Run 状态宿主（Run 开始时一次性种子）
        RunConfig = runConfig != null ? runConfig : ScriptableObject.CreateInstance<RunConfig>();
        RunState = new RunState();
        RunState.InsightThreshold = RunConfig.InsightThreshold;
        RunState.PostBattleRecoveryPercent = RunConfig.PostBattleRecoveryPercent;
        RunState.PostBattleRecoveryMin = RunConfig.PostBattleRecoveryMin;

        SeedRunState(playerData, playerChars, playerHero);

        MapGen = new MapGenerator();
        IsActive = true;
        IsVictory = false;
    }

    /// <summary>
    /// 播种 Run 状态：
    ///   人物 HP（来自长期实例/模板，初始满血）；
    ///   Run Deck 临时种子（BattleV1DemoCardPool 内容映射，只此一次；此后每战从 Run Deck 构筑）。
    /// </summary>
    private void SeedRunState(PlayerCollection playerData, List<CharacterCardData> playerChars,
                              HeroInstance playerHero)
    {
        var personIds = new List<string>();

        foreach (var card in playerChars)
        {
            if (card == null || string.IsNullOrEmpty(card.CharacterId)) continue;

            // M1：从卡解析其对应的具体人物实例（卡册显式绑定优先；同模板多实例不塌缩）
            CharacterInstance ci = playerData != null ? playerData.GetCharacterInstanceForCard(card) : null;
            string personId = ci != null ? ci.PersonId : null;

            int maxHp = ci != null && ci.MaxHP > 0 ? ci.MaxHP
                       : (DataManager.Instance != null ? (DataManager.Instance.GetCharacterById(card.CharacterId)?.BaseHP ?? 10) : 10);
            string name = ci != null && !string.IsNullOrEmpty(ci.DisplayName) ? ci.DisplayName : card.CharacterId;

            if (string.IsNullOrEmpty(personId))
            {
                if (playerData == null)
                {
                    // 纯演示链路（无 PlayerCollection）：允许模板 ID 作为本 Run 临时标识（不参与长期归属）
                    Debug.LogWarning("[AdventureManager] SeedRunState：卡 " + card.CardId
                        + " 无 PlayerCollection 上下文，使用模板ID作为 Run 临时标识（仅演示/兼容路径）。");
                    personId = card.CharacterId;
                }
                else
                {
                    // Foundation Identity Cleanup：产品路径只接受显式 Card→Person 绑定；
                    // 解析不到（未绑定，即使模板候选唯一）时禁止把模板 ID 当 PersonId 播种，跳过并告警。
                    Debug.LogWarning("[AdventureManager] SeedRunState：卡 " + card.CardId + "（模板 "
                        + card.CharacterId + "）无显式 Card→Person 绑定，已跳过 Run 播种（不按模板候选推断 Person）；"
                        + "请先为该卡建立显式 PersonId 绑定。");
                    continue;
                }
            }

            RunState.GetOrCreatePerson(personId, name, maxHp);
            personIds.Add(personId);
        }

        if (playerHero != null)
        {
            // Foundation Identity Cleanup：产品路径（有 PlayerData）只允许真实 PersonId 播种；
            // 空 PersonId 的 Hero 属非法身份，跳过播种并告警，不得把模板 HeroId 作为 Run Person seed。
            string heroPersonId = playerHero.PersonId;
            if (string.IsNullOrEmpty(heroPersonId))
            {
                if (playerData == null)
                {
                    // 纯演示（无 PlayerCollection）Run：模板 ID 仅作本 Run 临时标识（不参与长期归属）。
                    Debug.LogWarning("[AdventureManager] SeedRunState：英雄无 PersonId 且无 PlayerData，"
                        + "使用 HeroId（" + playerHero.HeroId + "）作为 Run 临时标识（仅演示/兼容路径）。");
                    heroPersonId = playerHero.HeroId;
                }
                else
                {
                    Debug.LogWarning("[AdventureManager] SeedRunState：英雄（HeroId=" + playerHero.HeroId
                        + "）缺少真实 PersonId，产品路径禁止用 HeroId 播种，已跳过英雄 Run 播种。");
                    heroPersonId = null;
                }
            }

            if (!string.IsNullOrEmpty(heroPersonId))
            {
                RunState.GetOrCreatePerson(heroPersonId, playerHero.DisplayName, playerHero.MaxHP);
                personIds.Add(heroPersonId);
            }
        }

        // V0.1：Run 初始牌条目从人物长期 Starter 槽生成（每个槽 1 张对应起始牌副本 + 通用牌全量）。
        // DemoCardPool 仅作为长期成长初始种子，不作为正式 Run 牌组来源。
        var growthPersons = new List<IPersonGrowth>();
        foreach (var personId in personIds)
        {
            var g = PlayerGrowthLookup.FindPerson(playerData, personId);
            if (g != null) growthPersons.Add(g);
        }
        var starterEntries = StarterDeckBuilder.BuildStarterEntries(growthPersons,
            DataManager.Instance != null ? DataManager.Instance.AllBattleCards : null);
        RunState.AddRunDeckEntries(starterEntries);
    }

    /// <summary>
    /// 开始冒险（生成地图）
    /// </summary>
    /// <param name="rows">地图行数</param>
    /// <param name="columns">每行点数</param>
    public void StartAdventure(int rows = 4, int columns = 3)
    {
        MapGen.Generate(rows, columns);
    }

    /// <summary>
    /// WP-1 Stage3：新增可选 <paramref name="nodeElapsedDaysFor"/> Test seam。
    /// 默认 null 时行为与 Stage2 完全一致（NodeElapsedDays=0）；传入时按 NodeId 提供每 Node
    /// 推进耗时（integer Village Days，Test / Not Frozen），经 LegacyMapGraphAdapter 进入 ExpeditionGraph。
    /// partySize / initialCarryUnits / perPersonDailyConsumptionRate 为 Test / Not Frozen 注入值
    /// （正式 Departure 真实 Carry 转移归 WP-3 seam；本方法不触碰 Village Food）。
    /// 保留 StartAdventure / ProcessNodeClick 等既有 Legacy API 供回归兼容，新产品流程以本方法为准。
    /// </summary>
    public void StartProgression(int rows = 4, int columns = 3,
        int partySize = 1, int initialCarryUnits = 0, int perPersonDailyConsumptionRate = 0,
        System.Func<string, int> nodeElapsedDaysFor = null)
    {
        if (partySize < 1)
        {
            Debug.LogWarning("[AdventureManager] StartProgression partySize=" + partySize
                + " < 1，已夹取为 1（Test / Not Frozen 值）。");
            partySize = 1;
        }
        if (initialCarryUnits < 0)
        {
            Debug.LogWarning("[AdventureManager] StartProgression initialCarryUnits=" + initialCarryUnits
                + " < 0，已夹取为 0（Test / Not Frozen 值）。");
            initialCarryUnits = 0;
        }
        if (perPersonDailyConsumptionRate < 0)
        {
            Debug.LogWarning("[AdventureManager] StartProgression perPersonDailyConsumptionRate="
                + perPersonDailyConsumptionRate + " < 0，已夹取为 0（Test / Not Frozen 值）。");
            perPersonDailyConsumptionRate = 0;
        }

        MapGen.Generate(rows, columns);
        var graph = LegacyMapGraphAdapter.BuildGraph(MapGen, nodeElapsedDaysFor);

        // WP-Event-1D Gate D2 §3：新的一次远征 = 新的事件会话（活动事件 / 会话历史 / exactly-once 记号全部重建）。
        ResetExpeditionEventSession();

        var resolver = new ExpeditionProgressionResolver();
        var init = resolver.InitializeProgression(graph, partySize, initialCarryUnits, perPersonDailyConsumptionRate);
        if (init != ExpeditionProgressionInitResult.Success)
        {
            Debug.LogWarning("[AdventureManager] StartProgression 初始化失败：" + init);
            Progression = null;
            return;
        }
        Progression = resolver;
    }

    /// <summary>handoff 只读：Progression elapsed days（integer Village Days；等价于 <see cref="ExpeditionElapsedDays"/>）。</summary>
    public int GetProgressionElapsedDays() => ExpeditionElapsedDays;

    /// <summary>只读转发：当前合法可进入/可点击的下一 Node（无会话时为空列表）。</summary>
    public IReadOnlyList<ExpeditionNodeData> GetAvailableProgressionNodes()
    {
        return Progression?.GetAvailableNextNodes() ?? new List<ExpeditionNodeData>();
    }

    /// <summary>转发：进入 Node（只表达到达；elapsed/supply 结算发生在 Resolve）。无会话 → NotInitialized。</summary>
    public ExpeditionEnterResult TryEnterNode(string nodeId)
    {
        return Progression != null
            ? Progression.TryEnterNode(nodeId)
            : ExpeditionEnterResult.NotInitialized;
    }

    /// <summary>转发：Branch 选择（N>1 时只能选合法后继）。无会话 → NotInitialized。</summary>
    public ExpeditionEnterResult TryBranch(string nodeId)
    {
        return Progression != null
            ? Progression.TryChooseBranch(nodeId)
            : ExpeditionEnterResult.NotInitialized;
    }

    /// <summary>转发：当前 Node 内容最终 outcome 的唯一结算入口（含明细）。无会话 → NotInitialized。</summary>
    public ExpeditionResolveResult TryResolveCurrentNode(ExpeditionNodeOutcome outcome,
        out ExpeditionNodeResolutionData data)
    {
        if (Progression == null)
        {
            data = null;
            return ExpeditionResolveResult.NotInitialized;
        }

        var result = Progression.TryResolveCurrentNode(outcome, out data);

        // WP-Encounter-1C §6：当前 Node 已被最终 outcome 结算 → 该节点的 Active EnemyRoster 立即失效。
        if (result == ExpeditionResolveResult.Success) ClearActiveEnemyRoster();

        return result;
    }

    /// <summary>转发：当前 Node 内容最终 outcome 的唯一结算入口（忽略明细）。无会话 → NotInitialized。</summary>
    public ExpeditionResolveResult TryResolveCurrentNode(ExpeditionNodeOutcome outcome)
    {
        if (Progression == null) return ExpeditionResolveResult.NotInitialized;

        var result = Progression.TryResolveCurrentNode(outcome);

        // WP-Encounter-1C §6：同上（两个重载保持同一生命周期语义）。
        if (result == ExpeditionResolveResult.Success) ClearActiveEnemyRoster();

        return result;
    }

    /// <summary>转发：Expedition 层独立 Return 请求（handoff 到既有 V4F）。无会话 → NotInitialized。</summary>
    public ExpeditionReturnResult RequestReturn()
    {
        if (Progression == null) return ExpeditionReturnResult.NotInitialized;

        var result = Progression.RequestReturn();

        // WP-Encounter-1C §6：Return / Retreat / Expedition End → 清除 Active EnemyRoster。
        if (result == ExpeditionReturnResult.Success) ClearActiveEnemyRoster();

        return result;
    }

    /// <summary>
    /// 处理节点点击
    /// </summary>
    /// <param name="node">点击的节点</param>
    /// <returns>需要执行的操作类型</returns>
    public NodeAction ProcessNodeClick(MapNode node)
    {
        if (node == null || !node.Reachable || node.Visited)
            return NodeAction.None;

        // 处理节点类型
        switch (node.Type)
        {
            case NodeType.Battle:
            case NodeType.Elite:
            case NodeType.Boss:
                // 先选中节点，再返回需要战斗
                MapGen.SelectNode(node);
                return NodeAction.Battle;

            case NodeType.Event:
                // 非战斗节点需要推进玩家位置（使下层节点可达），但允许重复点击
                MapGen.SelectNode(node);
                node.Visited = false; // 重置为未访问，允许重新进入
                return NodeAction.Event;

            case NodeType.Shop:
                MapGen.SelectNode(node);
                node.Visited = false;
                return NodeAction.Shop;

            case NodeType.Rest:
                MapGen.SelectNode(node);
                node.Visited = false;
                return NodeAction.Rest;

            case NodeType.Recruit:
                MapGen.SelectNode(node);
                node.Visited = false;
                return NodeAction.Recruit;

            default:
                return NodeAction.None;
        }
    }

    /// <summary>
    /// 将新招募的角色卡添加到出征牌组中
    /// 解决招募角色后无法在后续战斗中上场的问题
    /// </summary>
    /// <param name="newCard">新招募的角色卡</param>
    public void SyncRecruitedCharacter(CharacterCardData newCard)
    {
        if (newCard == null) return;
        if (!_playerChars.Exists(c => c.CardId == newCard.CardId))
        {
            _playerChars.Add(newCard);
        }
    }

    /// <summary>
    /// 获取战斗所需的角色实例列表
    /// </summary>
    public List<CharacterInstance> GetCharacterInstances()
    {
        var instances = new List<CharacterInstance>();
        foreach (var card in _playerChars)
        {
            if (card == null) continue;
            var ci = PlayerData != null ? PlayerData.GetCharacterInstanceForCard(card) : null;
            if (ci != null && !ci.IsDead)
                instances.Add(ci);
        }
        return instances;
    }

    /// <summary>
    /// 【LEGACY 模板查询】获取上场角色卡对应的模板 CharacterId 列表。
    /// 不能作为具体人物查询；新产品路径使用 GetExpeditionPersonIds / GetPersonIdsForDeployedChars。
    /// </summary>
    public List<string> GetDeployedCharIds()
    {
        var ids = new List<string>();
        foreach (var card in _playerChars)
        {
            if (card != null) ids.Add(card.CharacterId);
        }
        return ids;
    }

    /// <summary>
    /// 当前远征全部人物 PersonId（角色 + 英雄；供 Village Person Availability 运行时查询）。
    /// M1：必须返回真正 PersonId，不再返回模板 CharacterId / HeroId。
    /// V1：仅运行时使用，不持久化、不写入长期人物字段。
    /// </summary>
    public List<string> GetExpeditionPersonIds()
    {
        var ids = new List<string>();

        foreach (var card in _playerChars)
        {
            if (card == null) continue;
            // 优先卡对应的具体实例 PersonId；无 PlayerData 的纯演示链路才回退模板 ID（仅临时标识）
            string pid = PlayerData != null ? PlayerData.ResolvePersonIdForCard(card) : null;
            if (string.IsNullOrEmpty(pid))
            {
                if (PlayerData == null)
                {
                    pid = card.CharacterId; // 演示/兼容：仅临时标识，不作为长期 Person 归属
                }
                else
                {
                    // Foundation Identity Cleanup：有 PlayerData 但无显式绑定（即使模板候选唯一）
                    // 时不得把模板 ID 当 Person 登记。
                    Debug.LogWarning("[AdventureManager] GetExpeditionPersonIds：卡 " + card.CardId + "（模板 "
                        + card.CharacterId + "）无显式 Card→Person 绑定，已跳过登记；请显式绑定。");
                    continue;
                }
            }
            ids.Add(pid);
        }

        if (_playerHero != null)
        {
            // Foundation Identity Cleanup：Village 远征登记必须使用真实 PersonId；
            // 空 PersonId 的 Hero 属非法身份，跳过登记并告警，不得把模板 HeroId 当作远征 Person 占用。
            string heroPid = _playerHero.PersonId;
            if (string.IsNullOrEmpty(heroPid))
            {
                Debug.LogWarning("[AdventureManager] GetExpeditionPersonIds：英雄（HeroId="
                    + _playerHero.HeroId + "）缺少真实 PersonId，已跳过（不登记模板 ID 为远征占用）。");
                return ids;
            }
            ids.Add(heroPid);
        }

        return ids;
    }

    /// <summary>
    /// 获取玩家角色卡列表
    /// </summary>
    public List<CharacterCardData> GetPlayerChars() => _playerChars;

    /// <summary>
    /// 获取英雄实例
    /// </summary>
    public HeroInstance GetPlayerHero() => _playerHero;

    /// <summary>
    /// 获取所有角色数据
    /// </summary>
    public List<CharacterData> GetAllCharDatas() => _allCharDatas;

    /// <summary>
    /// 获取所有技能数据
    /// </summary>
    public List<SkillData> GetAllSkills() => _allSkills;

    /// <summary>
    /// 获取敌方怪物定义（StaticConfig MonsterDefinition；M2 Slice 2A 起类型见审计 §3.1-C2）
    /// </summary>
    /// <summary>
    /// 敌方 **Monster 定义目录**（名称 / 基础数值 / 阵营；M2 Slice 2A 起 Authority = StaticConfig）。
    ///
    /// WP-Encounter-1C 语义收窄：本列表**不再决定「这一场有几只怪、站哪里」** —— 那是
    /// `Active EnemyRoster`（见 AdventureEncounterRoster.cs）的职责；这里只作为
    /// 「MonsterId → 定义」的只读目录，供敌方阵容解析名称 / 数值 / 美术 / AI 规则键。
    /// 保留 API 是为了不破坏既有 Legacy / Demo 路径与回归测试（物理退休另立任务）。
    /// </summary>
    public IReadOnlyList<MonsterDefinition> GetEnemyMonsters() => _enemyMonsters;

    /// <summary>
    /// Boss 已被击败，冒险胜利
    /// </summary>
    public void SetVictory()
    {
        IsVictory = true;
        IsActive = false;
    }

    /// <summary>
    /// 冒险失败
    /// </summary>
    public void SetDefeat()
    {
        IsVictory = false;
        IsActive = false;
    }

    /// <summary>
    /// 结束本次远征并按退出模式处理待结算收益（Stage 6A / M5 / V4F）。
    ///
    /// V4F（Final Development Spec 修订 1）：本入口保留既有完整语义供旧调用方 / M5 专项测试使用
    /// （= SettleReturn 结算 + 按既有规则立即 Save，行为零回归）。
    /// 产品 Return 路径（ExpeditionUI.FinalizeReturn）改走 SettleReturn（不内含 Save），
    /// 由冻结顺序保证「Settlement + Return Catch-up 后」才做唯一一次最终 Return Save。
    ///
    /// WP-2 Current 边界：本方法 = Legacy / Compatibility / Test 入口（SettleReturn + immediate Save），
    /// Current 产品 Return 使用 ExpeditionUI.FinalizeReturn（SettleReturn → Return Catch-up →
    /// Clear Occupancy → 唯一最终 Return Save）；Current 产品 Scripts 不得新增对本方法的依赖。
    /// </summary>
    public void EndRun(RunEndMode mode)
    {
        if (SettleReturn(mode))
        {
            SaveSystem.Save(PlayerData);
        }
    }

    /// <summary>
    /// Run Return Settlement（纯结算，不内含任何 Save；V4F 冻结顺序第一步）的兼容入口。
    ///
    /// 返回 bool = **旧 EndRun 规则「结算后是否应立即 Save」的指示**（SafeReturn 恒 true；
    /// Retreat/Defeat 仅在发生 ProfessionExp 长期写回时为 true）。该 bool 不承载
    /// 「Settlement 是否真正成功」语义 —— WP-B 起需要该语义的调用方使用 SettleReturnDetailed。
    /// </summary>
    public bool SettleReturn(RunEndMode mode)
    {
        return SettleReturnDetailed(mode).ShouldSave;
    }

    /// <summary>
    /// Run Return Settlement（纯结算，不内含任何 Save；V4F 冻结顺序第一步）—— 完整结果入口（WP-B T1）。
    ///
    /// M5 Run/Return Growth Semantic Migration（Current Rule：GameSpec/Person §10）：
    ///   - 先全量兑现 PendingProfessionExp（CommitPendingProfessionExp，无损失、Exactly-Once）——
    ///     Normal Return（无限远征主动撤退 Retreat / 有限远征完成 SafeReturn）与 Expedition Failure（Defeat）
    ///     都完整保留已经真实产生的 ProfessionExp；
    ///   - 金币 / 建材 / Legacy GrowthExp（Hero Legacy Run Growth）等其它 Pending 仍按既有退出策略：
    ///     SafeReturn = 全额兑现；Retreat / Defeat 的损失比例尚未冻结（RunExitPolicy 占位，不自行填写）。
    ///
    /// WP-B Return Integrity（Correction：Commit Acknowledgement 必须成为 Return 成功前置）：
    ///   - `AllProfessionExpAcknowledged` = 结算后仍未 ack 的 PendingProfessionExp 归零；
    ///     只有它为 true 时 Settlement 才算成功（可写 `MarkExpeditionReturnSettled` → Catch-up）；
    ///   - 未 ack 时：Pending 保留（不用 Discard 语义清除）、`IsActive` 保持 true
    ///     （不得让「AdventureManager.IsActive == false」分支绕过未完成 Return Transaction）、
    ///     允许同会话重试 Settlement（已 ack 的 Person 因 Pending 已清零 → 重试 +0，不重复兑现）；
    ///   - Retreat / Defeat 占位分支只丢弃非 ProfessionExp 的 Pending（DiscardNonProfessionPendingRewards）。
    /// </summary>
    public ReturnSettlementResult SettleReturnDetailed(RunEndMode mode)
    {
        var result = new ReturnSettlementResult();
        var sink = new PlayerRunRewardSink(PlayerData);

        // M5：ProfessionExp 是所有退出方式的唯一长期写回（先于其它 Pending 结算）。
        result.ProfessionExpAcknowledged = RunState.CommitPendingProfessionExp(sink);
        result.ProfessionExpPendingRemaining = RunState.GetPendingProfessionExpTotal();

        float lossRatio = RunState.ResolveLossRatio(mode);
        if (lossRatio >= 0f)
        {
            // 安全返程：全额兑现 Legacy/金币等 Pending 到长期 PlayerCollection
            RunState.CommitPendingRewards(sink, lossRatio);
            result.ShouldSave = true;
        }
        else
        {
            // 未冻结退出模式：金币/建材/Legacy GrowthExp 等其它 Pending 不自行填写损失比例
            // （沿用 Stage 6B 前占位语义：不兑现）。
            // WP-B：只丢弃非 ProfessionExp 的 Pending —— 未 ack 的 ProfessionExp 必须保留，
            // 不得被「丢弃全部 Pending」的旧语义静默清除。
            Debug.LogWarning("[AdventureManager] Run 退出模式 " + mode
                + " 的损失比例尚未冻结（Stage 6B 前占位）：金币/建材/Legacy GrowthExp 等其它 Pending 不兑现；"
                + "ProfessionExp 已按 Current Rule 全量保留。");
            RunState.DiscardNonProfessionPendingRewards();

            // M5：Expedition Failure / 主动撤退也完整保留已产生的 ProfessionExp —— 长期写回需要持久化
            // （V4F：持久化时机 = 冻结顺序的最终 Return Save，不在此处 Save）。
            result.ShouldSave = result.ProfessionExpAcknowledged > 0;
        }

        if (result.AllProfessionExpAcknowledged)
        {
            IsActive = false;
            IsVictory = mode == RunEndMode.SafeReturn;
        }
        else
        {
            // WP-B：仍有未 ack 的 ProfessionExp → Settlement 未成功。
            // 保持 IsActive = true（唯一生产语义 = Settlement 已完成），Pending 保留，供同会话重试；
            // 本方法自身从不 Save。
            Debug.LogError("[AdventureManager] Return Settlement 未成功（" + mode + "）：仍有 "
                + result.ProfessionExpPendingRemaining + " 点 ProfessionExp 未 ack。"
                + "Pending 保留、IsActive 保持 true；不得 Catch-up / Clear Occupancy / Final Save，允许同会话重试 Settlement。");
        }

        return result;
    }
}

/// <summary>
/// AdventureManager.SettleReturnDetailed 的结果（WP-B T1｜Settlement Acknowledgement）。
/// 只表达「本次 Settlement 的兑现结果」，不构成第二套 Return Authority，也不改变既有公共信号：
///   - `ShouldSave`：旧 `SettleReturn` bool 的兼容语义（结算后按旧 EndRun 规则是否应立即 Save）；
///   - `ProfessionExpAcknowledged`：本次实际成功 ack 写入长期的 ProfessionExp 总量；
///   - `ProfessionExpPendingRemaining`：Settlement 后仍未 ack 的 PendingProfessionExp 总量（必须为 0 才算成功）；
///   - `AllProfessionExpAcknowledged` / `SettlementSucceeded`：Return 能否越过 Settlement 的唯一判据。
/// 全部为运行时结果（Test / Not Frozen），不持久化、不进 SaveData。
/// </summary>
public class ReturnSettlementResult
{
    /// <summary>旧 EndRun 规则：结算后是否应立即 Save（兼容语义，不代表 Settlement 成功）。</summary>
    public bool ShouldSave;

    /// <summary>本次实际 ack（成功写入长期 Person Profession Growth）的 ProfessionExp 总量。</summary>
    public int ProfessionExpAcknowledged;

    /// <summary>结算后仍未 ack 的 PendingProfessionExp 总量；&gt; 0 = Settlement 未成功。</summary>
    public int ProfessionExpPendingRemaining;

    /// <summary>是否全部应兑现的 ProfessionExp 均已 ack（Return 可越过 Settlement 的唯一前提）。</summary>
    public bool AllProfessionExpAcknowledged => ProfessionExpPendingRemaining <= 0;

    /// <summary>Settlement 是否成功（= AllProfessionExpAcknowledged）。</summary>
    public bool SettlementSucceeded => AllProfessionExpAcknowledged;
}

/// <summary>
/// 节点点击后的操作类型
/// </summary>
public enum NodeAction
{
    None,       // 无效点击
    Battle,     // 进入战斗
    Event,      // 事件（占位）
    Shop,       // 商店（占位）
    Rest,       // 休息（占位）
    Recruit     // 招募（占位）
}