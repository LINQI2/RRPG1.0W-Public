using System.Collections.Generic;

/// <summary>
/// 本次远征（Run）结束时的退出模式。
/// SafeReturn = 安全返程（全额兑现 Pending Rewards）。
/// Retreat / Defeat = 主动返程 / 被迫撤退 / 全灭；损失比例尚未冻结，仅预留策略入口。
/// </summary>
public enum RunEndMode
{
    SafeReturn,
    Retreat,
    Defeat
}

/// <summary>
/// 授予领悟的“遭遇”类型（Encounter/Reward Source）。
/// 领悟由遭遇授予，战斗只是遭遇类型之一，不按单个怪物授予。
/// BattleVictory 为本轮首个实际接入源（测试值）；Event / Boss / 特殊节点后续可经同一入口接入。
/// </summary>
public enum RunEncounterType
{
    BattleVictory,
    Event,
    Boss,
    Special
}

/// <summary>
/// Run 待结算长期收益的兑现目标（由上层实现，注入 PlayerCollection 等长期数据）。
/// 使 RunState 保持纯逻辑、可脱离 Unity 与 Assembly-CSharp 直接测试。
///
/// M5 Run/Return Growth Semantic Migration（Correction：Commit Acknowledgement）：
///   - AddGrowthExp       = Legacy 成长经验通道（Hero Legacy Run Growth / 一般长期成长；SafeReturn 才兑现）；
///   - TryAddProfessionExp = ProfessionExp 通道（Profession Character 的 Run/Return 成长经验）。带明确成功反馈
///                           （bool）：只有 Person 存在 + Active Profession 有效 + 成功写入才返回 true，
///                           RunState 依据返回值决定是否清零 Pending（Person 找不到 / 无 Active Profession /
///                           Hero / sink 无法执行 → false，Pending 保留，不被误消费）。
/// </summary>
public interface IRunRewardSink
{
    void AddGold(int amount);
    void AddBuildingMaterial(int amount);
    void AddGrowthExp(string personId, int amount);

    /// <summary>
    /// 尝试把某个具体 Person 的 Run ProfessionExp 写入其当前 Profession Growth。
    /// true = 成功写入长期（Person 存在 + Active Profession 有效）；false = no-op
    /// （Person 找不到 / 无 Active Profession / Hero / sink 无法执行）。
    /// </summary>
    bool TryAddProfessionExp(string personId, int amount);
}

/// <summary>
/// Run 退出策略：返回各退出模式的损失比例。
/// 除 SafeReturn 外，损失比例尚未冻结（任务约束），返回 NotConfigured，不自行填写正式数值。
/// </summary>
public class RunExitPolicy
{
    public const float NotConfigured = -1f;

    public virtual float GetLossRatio(RunEndMode mode)
    {
        switch (mode)
        {
            case RunEndMode.SafeReturn:
                return 0f; // 安全返程无损失，全额兑现
            default:
                return NotConfigured; // 未冻结：不得自行填写
        }
    }
}

/// <summary>
/// 本次远征（Run）运行时状态宿主（纯逻辑，BattleStage1.Runtime）。
///
/// Stage 6A 承载（所有数值为测试值、未冻结）：
///   - 每个远征人物的 CurrentHP / MaxHP（跨战斗 HP 继承 + 战后可配置恢复）；
///   - Run Deck Entries（模板 + 人物归属 + 份数，跨战斗保留；每战只从 Run Deck 构筑 SharedDeck）；
///   - CurrentInsight / PendingCardRewardCount（全队共享领悟进度）；
///   - PendingGold / PendingBuildingMaterial / 每人物 PendingGrowthExp（Legacy 待结算长期收益）
///     / 每人物 PendingProfessionExp（M5：ProfessionExp 待结算，Return 全量兑现）。
///
/// M5 Run/Return Growth Semantic Migration（见 GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md §10）：
///   - PendingGrowthExp（Legacy 通道）：Hero 保持 M4 后 Legacy Run Growth；只在 SafeReturn 兑现；
///   - PendingProfessionExp（ProfessionExp 通道）：Battle/Event 为具体 Profession Person 产生，
///     随 Run 保留；Return（无限主动撤退 / 有限完成 / Expedition Failure）都全量兑现到该 Person
///     当前 Profession Growth；普通 Character 无 Active Profession 不产生、不 fallback Legacy；
///     两份 Pending 按 PersonId 独立累计，同 Profession 不同 Person 不共享。
///   - Runtime Checkpoint（RunCheckpointData）：当前产品没有「Run Save/Resume」正式产品路径，
///     本 Checkpoint 只保证【Return 前】的运行时连续性（RunState 宿主内快照，Test / Not Frozen）；
///     不落地 SaveData / SaveVersion。正式 Exactly-Once 保证只覆盖【当前 Active Run 生命周期内的
///     Return Settlement】；Return 后把 Return 前导出的旧 Checkpoint 导入到全新 RunState 的
///     stale replay 幂等保护属未来正式 Run Save/Resume Work Package（本阶段 Not Implemented / Not Frozen）。
///
/// 不依赖 UnityEngine 与 Assembly-CSharp；长期兑现通过 IRunRewardSink 注入。
/// 阈值/恢复比例等参数不冻结，可在构造后由 RunConfig 或测试覆盖。
/// </summary>
public class RunState
{
    /// <summary>远征内一名人物（具体角色/英雄实例）的 Run 状态</summary>
    public class RunPerson
    {
        /// <summary>
        /// 远征内人物的真正 PersonId（M1：具体人物实例唯一身份）。
        /// 不再使用 CharacterData.CharacterId / HeroId（模板 ID）作为 Run 人物键。
        /// 所有调用方必须传真实 PersonId；RunState 不根据模板 ID 反推人物。
        /// </summary>
        public string PersonId;

        public string DisplayName;

        /// <summary>最大生命（来自长期实例/模板，Run 内固定）</summary>
        public int MaxHP;

        /// <summary>Run 当前生命（战后回写 + 恢复；下一战从此初始化）</summary>
        public int CurrentHP;

        /// <summary>待结算长期成长经验（Legacy 通道；Hero Legacy Run Growth 使用；SafeReturn 时兑现）</summary>
        public int PendingGrowthExp;

        /// <summary>
        /// M5：待结算 ProfessionExp（ProfessionExp 通道；只有实际获得 ProfessionExp 的 Person 才 > 0）。
        /// 按 PersonId 独立累计；同 Profession 的不同 Person 不共享。
        /// Return 时（任何退出方式）由 RunState.CommitPendingProfessionExp 全量兑现。
        /// </summary>
        public int PendingProfessionExp;
    }

    /// <summary>
    /// 领悟阈值（测试值，未冻结；可由 RunConfig 覆盖）。
    /// 【测试值】默认 30 = 每场胜利默认获得量，便于 Milestone D 一场触发一次奖励。
    /// </summary>
    public int InsightThreshold = 30;

    /// <summary>战后恢复比例（测试值，未冻结；0.05 = 5% MaxHP）</summary>
    public float PostBattleRecoveryPercent = 0.05f;

    /// <summary>战后恢复最低值（比例 &gt; 0 时至少恢复该值，保证 0HP 战后可恢复）</summary>
    public int PostBattleRecoveryMin = 1;

    /// <summary>当前全队共享领悟进度</summary>
    public int CurrentInsight;

    /// <summary>待触发的卡牌奖励机会次数（跨过阈值累计）</summary>
    public int PendingCardRewardCount;

    /// <summary>待结算金币</summary>
    public int PendingGold;

    /// <summary>待结算建材</summary>
    public int PendingBuildingMaterial;

    private readonly Dictionary<string, RunPerson> _persons = new Dictionary<string, RunPerson>();
    private readonly List<BattlePersonCardEntry> _runDeckEntries = new List<BattlePersonCardEntry>();
    private readonly RunExitPolicy _exitPolicy;

    public RunState(RunExitPolicy exitPolicy = null)
    {
        _exitPolicy = exitPolicy ?? new RunExitPolicy();
    }

    public IReadOnlyDictionary<string, RunPerson> Persons => _persons;
    public IReadOnlyList<BattlePersonCardEntry> RunDeckEntries => _runDeckEntries;

    /// <summary>是否存在待结算收益（含 M5 ProfessionExp 通道）</summary>
    public bool HasPendingRewards
    {
        get
        {
            if (PendingGold > 0 || PendingBuildingMaterial > 0) return true;
            foreach (var p in _persons.Values)
                if (p.PendingGrowthExp > 0 || p.PendingProfessionExp > 0) return true;
            return false;
        }
    }

    // ==================== 人物 HP ====================

    /// <summary>
    /// 获取或创建 Run 人物。首次创建时 CurrentHP = MaxHP；已存在时仅同步 MaxHP/DisplayName，保留 Run HP。
    /// </summary>
    public RunPerson GetOrCreatePerson(string personId, string displayName, int maxHP)
    {
        if (!_persons.TryGetValue(personId, out var p))
        {
            p = new RunPerson
            {
                PersonId = personId,
                DisplayName = string.IsNullOrEmpty(displayName) ? personId : displayName,
                MaxHP = maxHP,
                CurrentHP = maxHP
            };
            _persons[personId] = p;
        }
        else
        {
            if (!string.IsNullOrEmpty(displayName)) p.DisplayName = displayName;
            if (maxHP > 0) p.MaxHP = maxHP; // 未知 MaxHP（≤0）不覆盖已存在值
        }
        return p;
    }

    public bool TryGetPerson(string personId, out RunPerson person)
    {
        return _persons.TryGetValue(personId, out person);
    }

    public RunPerson GetPerson(string personId)
    {
        _persons.TryGetValue(personId, out var p);
        return p;
    }

    public int GetPersonCurrentHp(string personId, int fallback)
    {
        return _persons.TryGetValue(personId, out var p) ? p.CurrentHP : fallback;
    }

    /// <summary>
    /// 战斗结束回写：BattleUnit.CurrentHP → Run 人物状态，并按配置执行战后恢复。
    /// 恢复量 = max(MaxHP × PostBattleRecoveryPercent 向上取整, PostBattleRecoveryMin)；不超过 MaxHP。
    /// 下一场战斗从回写后的 CurrentHP 初始化（不自动满血）。
    /// </summary>
    public void ApplyBattleEndHp(string personId, int battleCurrentHp, int battleMaxHp)
    {
        var p = GetOrCreatePerson(personId, personId, battleMaxHp);

        int recover = 0;
        if (PostBattleRecoveryPercent > 0f)
        {
            // epsilon 避免浮点误差（0.05f*100=5.0000001）导致 5% 向上取整为 6
            int percentRecover = (int)System.Math.Ceiling(battleMaxHp * PostBattleRecoveryPercent - 0.0001);
            recover = System.Math.Max(PostBattleRecoveryMin, percentRecover);
        }

        p.CurrentHP = System.Math.Min(battleMaxHp, battleCurrentHp + recover);
    }

    // ==================== 领悟 ====================

    /// <summary>
    /// 增加全队共享领悟进度。
    /// 每跨过阈值一次 → PendingCardRewardCount +1；触发只扣阈值不清零；溢出完整保留；
    /// 单次调用可累计跨多个阈值。返回本次触发的奖励机会次数。
    /// </summary>
    public int AddInsight(int amount)
    {
        if (amount <= 0) return 0;

        CurrentInsight += amount;
        int chances = 0;
        while (InsightThreshold > 0 && CurrentInsight >= InsightThreshold)
        {
            CurrentInsight -= InsightThreshold;
            chances++;
            PendingCardRewardCount++;
        }
        return chances;
    }

    /// <summary>
    /// 由“遭遇”授予领悟的入口（Encounter/Reward Source → AddInsight）。
    /// 战斗只是遭遇类型之一；BattleVictory 为首个实际接入源（测试值未冻结），
    /// Event / Boss / 特殊节点后续经同一入口授予不同数量，不把“每场战斗固定领悟”固化为唯一来源。
    /// </summary>
    public int AddInsightFromEncounter(RunEncounterType encounter, int amount)
    {
        return AddInsight(amount);
    }

    /// <summary>消耗 1 次卡牌奖励机会（选择 / Skip 后调用；不低于 0）</summary>
    public void ConsumePendingCardReward()
    {
        if (PendingCardRewardCount > 0) PendingCardRewardCount--;
    }




    // ==================== 待结算收益 ====================

    public void AddPendingGold(int amount)
    {
        if (amount > 0) PendingGold += amount;
    }

    public void AddPendingBuildingMaterial(int amount)
    {
        if (amount > 0) PendingBuildingMaterial += amount;
    }

    public void AddPendingGrowthExp(string personId, int amount)
    {
        // Identity Cleanup：下游容器最多做 null/empty guard；非法/空 PersonId 不写 Pending（不猜测 Person）。
        if (amount <= 0 || string.IsNullOrEmpty(personId)) return;
        var p = GetOrCreatePerson(personId, personId, 0);
        p.PendingGrowthExp += amount;
    }

    /// <summary>
    /// M5：把某个具体 Person 的 ProfessionExp 记入本 Run 的 Pending（只累计，不写长期）。
    /// 这是 GameSpec 最小 Event Hook / Battle ProfessionExp 的统一入口（PersonId + amount）；
    /// amount &lt;= 0 不入账。按 PersonId 独立累计，同 Profession 的不同 Person 不共享。
    /// 调用方负责保证该 Person 当前确有 Active Profession（无 Profession 的普通 Character / Hero
    /// 不应调用本入口；本纯逻辑宿主不做宿主分类）。
    /// </summary>
    public void AddPendingProfessionExp(string personId, int amount)
    {
        if (amount <= 0 || string.IsNullOrEmpty(personId)) return;
        var p = GetOrCreatePerson(personId, personId, 0);
        p.PendingProfessionExp += amount;
    }

    /// <summary>
    /// 兑现 PendingGrowthExp（Legacy 通道）等一般 Pending 到 sink（SafeReturn 路径）。
    /// lossRatio：损失比例（0 = 全额兑现；未冻结模式不入此路径）。
    /// 兑现后清零对应 Pending（不含 ProfessionExp 通道）。
    /// </summary>
    public void CommitPendingRewards(IRunRewardSink sink, float lossRatio)
    {
        if (sink == null) return;

        int gold = ApplyLoss(PendingGold, lossRatio);
        int material = ApplyLoss(PendingBuildingMaterial, lossRatio);
        if (gold > 0) sink.AddGold(gold);
        if (material > 0) sink.AddBuildingMaterial(material);

        foreach (var p in _persons.Values)
        {
            int exp = ApplyLoss(p.PendingGrowthExp, lossRatio);
            if (exp > 0) sink.AddGrowthExp(p.PersonId, exp);
        }

        PendingGold = 0;
        PendingBuildingMaterial = 0;
        foreach (var p in _persons.Values) p.PendingGrowthExp = 0;
    }

    /// <summary>
    /// M5：尝试把全部 PendingProfessionExp 按 PersonId 兑现到 sink（无损失、全量）。
    ///
    /// Correction（Commit Acknowledgement）：只在 sink.TryAddProfessionExp 明确返回 true
    /// （= Person 存在 + Active Profession 有效 + 成功写入长期）后才清零对应 Pending；
    /// Person 找不到 / 无 Active Profession / Hero / sink 无法执行（返回 false）时 Pending 保留，
    /// 不被乐观误消费。返回本次实际成功写入的经验总量。
    ///
    /// Exactly-Once 保证范围：当前 Active Run 生命周期内的 Return Settlement —— 成功兑现即清零，
    /// 同一 Active Run 再次 Commit 自然 +0；跨快照 stale replay（Return 后重放旧 Checkpoint）不属本方法保证
    /// （见类文档：未来正式 Run Save/Resume Work Package，Not Implemented / Not Frozen）。
    /// </summary>
    public int CommitPendingProfessionExp(IRunRewardSink sink)
    {
        if (sink == null) return 0;

        int total = 0;
        foreach (var p in _persons.Values)
        {
            if (p.PendingProfessionExp <= 0) continue;
            // 只有 sink 明确反馈“成功写入长期”才清零；no-op 不清零（不被误消费）。
            if (sink.TryAddProfessionExp(p.PersonId, p.PendingProfessionExp))
            {
                total += p.PendingProfessionExp;
                p.PendingProfessionExp = 0;
            }
        }
        return total;
    }

    /// <summary>
    /// 当前仍未 ack 的 PendingProfessionExp 总量（跨全部人物）。
    ///
    /// WP-B Return Integrity：Return Settlement 是否真正成功 = 本值归零
    ///（CommitPendingProfessionExp 的 ack 语义：只有 sink 明确写入长期才清零对应条目）；
    /// 非零表示仍有 Person 的 ProfessionExp 未成功写入长期，不得越过 Settlement
    ///（不 MarkSettled / 不 Catch-up / 不 Clear / 不落盘），Pending 必须保留供同会话重试。
    /// </summary>
    public int GetPendingProfessionExpTotal()
    {
        int total = 0;
        foreach (var p in _persons.Values)
        {
            if (p.PendingProfessionExp > 0) total += p.PendingProfessionExp;
        }
        return total;
    }

    /// <summary>
    /// WP-B Return Integrity：只丢弃非 ProfessionExp 的 Pending（金币 / 建材 / Legacy GrowthExp）。
    ///
    /// Current 产品 Return（Retreat / Defeat 损失比例未冻结的占位分支）必须使用本方法：
    /// ProfessionExp 是已经真实产生的 Person 长期成长事实，SafeReturn / Retreat / Defeat 都必须完整保留，
    /// 只有 sink ack 成功才可由 CommitPendingProfessionExp 清除，不得被任何 Discard 语义静默丢弃。
    /// </summary>
    public void DiscardNonProfessionPendingRewards()
    {
        PendingGold = 0;
        PendingBuildingMaterial = 0;
        foreach (var p in _persons.Values) p.PendingGrowthExp = 0;
    }

    /// <summary>
    /// 丢弃 PendingRewards（金币/建材/Legacy GrowthExp + ProfessionExp 通道都清零）。
    /// 注意：产品 EndRun 必须先 CommitPendingProfessionExp 再走本方法；
    /// 本方法只用于「明确放弃全部待结算」的占位/防御路径（损失比例未冻结，不自行填写），
    /// 与 Commit 的 ack 清零是不同语义（本方法显式放弃，不代表 Commit 成功）。
    ///
    /// WP-B：本「显式放弃全部」语义不得再用于清除未 ack 的 ProfessionExp ——
    /// Current Return 链路（Retreat / Defeat 占位分支）改用 DiscardNonProfessionPendingRewards；
    /// 本方法保留给明确放弃全部（含 ProfessionExp）的防御 / 测试入口，不是 Current Return 路径。
    /// </summary>
    public void DiscardPendingRewards()
    {
        PendingGold = 0;
        PendingBuildingMaterial = 0;
        foreach (var p in _persons.Values)
        {
            p.PendingGrowthExp = 0;
            p.PendingProfessionExp = 0;
        }
    }

    /// <summary>按退出模式解析损失比例（SafeReturn=0；其余=NotConfigured 未冻结）</summary>
    public float ResolveLossRatio(RunEndMode mode)
    {
        return _exitPolicy.GetLossRatio(mode);
    }

    // ==================== Runtime Checkpoint（M5 Correction；Test / Not Frozen） ====================

    /// <summary>
    /// M5 Run 运行时快照（当前产品没有 Run Save/Resume 正式产品路径）。
    /// 本 Checkpoint 只保证【Return 前】的运行时连续性（Pending 含 PendingProfessionExp / HP / 领悟 / 金币）。
    /// 只表达人物/Pending/HP/领悟/金币等纯逻辑数据；Run Deck 模板引用与 RunConfig SO 不在其内，
    /// 恢复后由宿主/调用方重新注入。不落地 SaveData / SaveVersion，不构成产品规则。
    /// Return 后把 Return 前导出的旧快照重放回全新 RunState 的 stale replay 幂等保护
    /// 属未来正式 Run Save/Resume Work Package（本阶段 Not Implemented / Not Frozen）。
    /// </summary>
    [System.Serializable]
    public class RunCheckpointData
    {
        public int InsightThreshold;
        public float PostBattleRecoveryPercent;
        public int PostBattleRecoveryMin;
        public int CurrentInsight;
        public int PendingCardRewardCount;
        public int PendingGold;
        public int PendingBuildingMaterial;
        public System.Collections.Generic.List<RunPersonCheckpoint> Persons =
            new System.Collections.Generic.List<RunPersonCheckpoint>();
    }

    /// <summary>Run 人物运行时快照条目（RunCheckpointData 使用）。</summary>
    [System.Serializable]
    public class RunPersonCheckpoint
    {
        public string PersonId;
        public string DisplayName;
        public int MaxHP;
        public int CurrentHP;
        public int PendingGrowthExp;
        public int PendingProfessionExp;
    }

    /// <summary>
    /// M5：导出当前 Run 的纯逻辑快照（含 PendingGrowthExp 与 PendingProfessionExp），供【Return 前】运行时连续性。
    /// 只导出数据，不写长期、不清空本 Run。本快照不含“已提交”标记；Return 后的 stale replay 不属于本方法保证范围。
    /// </summary>
    public RunCheckpointData ExportCheckpoint()
    {
        var data = new RunCheckpointData
        {
            InsightThreshold = InsightThreshold,
            PostBattleRecoveryPercent = PostBattleRecoveryPercent,
            PostBattleRecoveryMin = PostBattleRecoveryMin,
            CurrentInsight = CurrentInsight,
            PendingCardRewardCount = PendingCardRewardCount,
            PendingGold = PendingGold,
            PendingBuildingMaterial = PendingBuildingMaterial
        };
        foreach (var p in _persons.Values)
        {
            data.Persons.Add(new RunPersonCheckpoint
            {
                PersonId = p.PersonId,
                DisplayName = p.DisplayName,
                MaxHP = p.MaxHP,
                CurrentHP = p.CurrentHP,
                PendingGrowthExp = p.PendingGrowthExp,
                PendingProfessionExp = p.PendingProfessionExp
            });
        }
        return data;
    }

    /// <summary>
    /// M5：从快照恢复 Run 人物 / Pending / HP / 领悟 / 金币等纯逻辑状态（Return 前运行时连续性用）。
    /// 只做字面恢复：不写长期 ProfessionExp / Legacy Exp（Restore 绝不提前兑现）；
    /// 不清空本实例 Run Deck（Run Deck 由持有方跨快照保留）；
    /// RunConfig 阈值由调用方在构造后按 AdventureManager 惯例重新注入（本方法同时把快照内阈值恢复）。
    /// 注意：本方法无“已提交/版本”标记，不提供 Return 后旧快照 stale replay 的幂等保护
    /// （见类文档：未来正式 Run Save/Resume Work Package，Not Implemented / Not Frozen）。
    /// </summary>
    public void RestoreCheckpoint(RunCheckpointData data)
    {
        if (data == null) return;

        InsightThreshold = data.InsightThreshold;
        PostBattleRecoveryPercent = data.PostBattleRecoveryPercent;
        PostBattleRecoveryMin = data.PostBattleRecoveryMin;
        CurrentInsight = data.CurrentInsight;
        PendingCardRewardCount = data.PendingCardRewardCount;
        PendingGold = data.PendingGold;
        PendingBuildingMaterial = data.PendingBuildingMaterial;

        _persons.Clear();
        if (data.Persons != null)
        {
            foreach (var p in data.Persons)
            {
                if (p == null || string.IsNullOrEmpty(p.PersonId)) continue;
                _persons[p.PersonId] = new RunPerson
                {
                    PersonId = p.PersonId,
                    DisplayName = string.IsNullOrEmpty(p.DisplayName) ? p.PersonId : p.DisplayName,
                    MaxHP = p.MaxHP,
                    CurrentHP = p.CurrentHP,
                    PendingGrowthExp = p.PendingGrowthExp,
                    PendingProfessionExp = p.PendingProfessionExp
                };
            }
        }
    }

    // ==================== Run Deck ====================

    /// <summary>
    /// 向 Run Deck 添加一张人物牌（同人物同模板累加份数）。
    /// </summary>
    public void AddRunCard(string personId, BattleCardData template)
    {
        if (template == null || string.IsNullOrEmpty(template.CardId)) return;

        foreach (var entry in _runDeckEntries)
        {
            if (entry == null || entry.Template == null) continue;
            if (entry.PersonId == personId && entry.Template.CardId == template.CardId)
            {
                entry.Copies++;
                return;
            }
        }

        _runDeckEntries.Add(new BattlePersonCardEntry(template, personId, 1));
    }

    public void AddRunDeckEntries(IEnumerable<BattlePersonCardEntry> entries)
    {
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (e != null && e.Template != null) _runDeckEntries.Add(e);
        }
    }

    /// <summary>
    /// Run 开始时的临时种子（BattleV1DemoCardPool 内容映射）。
    /// 只应在 Run 创建时调用一次；此后 Battle 每战必须从当前 Run Deck 构筑 SharedDeck，不得每战重新种子。
    /// personIds 为具体人物 PersonId（M1：Run 播种必须使用真实 PersonId）。
    /// </summary>
    public void SeedRunDeckFromPool(ICollection<string> personIds, IEnumerable<BattleCardData> allTemplates)
    {
        _runDeckEntries.Clear();
        var seeded = BattleV1DemoCardPool.BuildRoster(personIds, allTemplates);
        foreach (var e in seeded)
        {
            if (e != null && e.Template != null) _runDeckEntries.Add(e);
        }
    }

    /// <summary>返回 Run Deck 条目的快照（供 Battle 构筑 SharedDeck；不修改 Run Deck）</summary>
    public List<BattlePersonCardEntry> GetRunDeckEntries()
    {
        return new List<BattlePersonCardEntry>(_runDeckEntries);
    }

    // ==================== 内部 ====================

    private static int ApplyLoss(int value, float lossRatio)
    {
        if (value <= 0) return 0;
        if (lossRatio <= 0f) return value;
        if (lossRatio >= 1f) return 0;
        return value - (int)(value * lossRatio);
    }
}

