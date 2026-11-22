using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M5 Run/Return Growth Semantic Migration 纯逻辑测试（BattleStage1.Runtime 强类型）：
///   P1  Event Hook：PersonId + amount → Add Pending ProfessionExp（只给指定 Person、amount&gt;0 才入账）；
///   P2  Pending 按 PersonId 独立累计（同 Profession 不同 Person 不共享）；
///   P3  Run 中 Pending 不直接写长期 ProfessionExp（Commit 前长期仍 0，只有 Return 兑现才写）；
///   P4  CommitPendingProfessionExp 在成功 ack（TryAddProfessionExp=true）后全量、Exactly-Once（第二次 +0）；
///      sink no-op（返回 false）时 Pending 保留、不被误消费；
///   P5  Legacy（PendingGrowthExp）与 ProfessionExp（PendingProfessionExp）两个通道独立；
///   P6  Runtime Checkpoint（仅保证 Return 前运行时连续性）：Export → Restore 后 Pending（含 ProfessionExp）
///       仍为 X；Restore 本身绝不提前写入长期；同一 Active Run 成功 Commit 后再次 Commit = 0。
///       （Return 后旧快照 stale replay 的跨快照幂等保护属未来正式 Run Save/Resume Work Package，
///       本阶段 Not Implemented / Not Frozen，不在本文件声明“已解决”。）
/// </summary>
public class ProfessionRunPendingAndCheckpointTests
{
    private class FakeSink : IRunRewardSink
    {
        public int Gold;
        public int Material;
        public readonly Dictionary<string, int> Legacy = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Profession = new Dictionary<string, int>();

        public void AddGold(int amount) => Gold += amount;
        public void AddBuildingMaterial(int amount) => Material += amount;
        public void AddGrowthExp(string personId, int amount)
        {
            Legacy.TryGetValue(personId, out int cur);
            Legacy[personId] = cur + amount;
        }

        public bool TryAddProfessionExp(string personId, int amount)
        {
            Profession.TryGetValue(personId, out int cur);
            Profession[personId] = cur + amount;
            return true;
        }
    }

    /// <summary>模拟 sink 无法执行（返回 false）：Commit 不得误消费 Pending。</summary>
    private class RejectProfessionSink : IRunRewardSink
    {
        public int Gold;
        public int Material;

        public void AddGold(int amount) => Gold += amount;
        public void AddBuildingMaterial(int amount) => Material += amount;
        public void AddGrowthExp(string personId, int amount) { }
        public bool TryAddProfessionExp(string personId, int amount) => false;
    }

    [Test]
    public void EventHook_AddsPendingProfessionExp_OnlyForTargetPerson()
    {
        // P1/P2 — Event Hook 只给指定 PersonId；另一个同 Profession 的 Person 不共享
        var run = new RunState();
        run.AddPendingProfessionExp("person_a", 10);
        run.AddPendingProfessionExp("person_a", 5);   // 同一人累计
        run.AddPendingProfessionExp("person_b", 0);   // amount<=0 不入账
        run.AddPendingProfessionExp(null, 3);         // 空 PersonId 不入账

        Assert.AreEqual(15, run.GetPerson("person_a").PendingProfessionExp, "同一 PersonId 应独立累计");
        Assert.IsNull(run.GetPerson("person_b"), "amount<=0 不应创建 Pending 条目");
        Assert.AreEqual(1, run.Persons.Count,
            "null PersonId 不应创建 Pending 条目（AddPendingProfessionExp(null) 为安全 no-op）");
    }

    [Test]
    public void PendingProfessionExp_DoesNotWriteLongTerm_DuringRun()
    {
        // P3 — Run 中只累计 Pending，不写长期（Commit 前长期为 0）
        var run = new RunState();
        run.AddPendingProfessionExp("person_a", 20);
        var sink = new FakeSink();
        Assert.IsTrue(run.HasPendingRewards, "存在 PendingProfessionExp 应算 HasPendingRewards");
        Assert.AreEqual(0, sink.Profession.Count, "Run 中未 Commit 前不得写长期");
    }

    [Test]
    public void CommitPendingProfessionExp_CommitsFull_ExactlyOnce()
    {
        // P4 — Exactly-Once：第一次 Commit 全量 X；第二次 Commit +0
        var run = new RunState();
        run.AddPendingProfessionExp("person_a", 10);
        run.AddPendingProfessionExp("person_b", 7);
        var sink = new FakeSink();

        int first = run.CommitPendingProfessionExp(sink);
        Assert.AreEqual(17, first, "第一次 Commit 应全量兑现");
        Assert.AreEqual(10, sink.Profession["person_a"]);
        Assert.AreEqual(7, sink.Profession["person_b"]);
        Assert.IsFalse(run.HasPendingRewards, "Commit 后不应再有 Pending");
        Assert.AreEqual(0, run.GetPerson("person_a").PendingProfessionExp, "Commit 后 Pending 清零");

        int second = run.CommitPendingProfessionExp(sink);
        Assert.AreEqual(0, second, "重复 Commit 必须 +0（Exactly-Once）");
        Assert.AreEqual(10, sink.Profession["person_a"], "第二次 Commit 不得重复写长期");
    }

    [Test]
    public void CommitPendingProfessionExp_SinkNoAck_DoesNotClearPending()
    {
        // P4b（Correction：Commit Acknowledgement）— sink no-op / 无法执行（返回 false）时
        // Pending 必须保留、不被乐观误消费；Commit 返回 0。
        var run = new RunState();
        run.AddPendingProfessionExp("person_a", 10);
        var rejectSink = new RejectProfessionSink();

        int first = run.CommitPendingProfessionExp(rejectSink);
        Assert.AreEqual(0, first, "sink 无 ack 时不得计入成功写入");
        Assert.AreEqual(10, run.GetPerson("person_a").PendingProfessionExp,
            "sink no-op 时 Pending 不得清零（不被误消费）");

        int second = run.CommitPendingProfessionExp(rejectSink);
        Assert.AreEqual(0, second, "重复 no-op Commit 仍不消费 Pending");
        Assert.AreEqual(10, run.GetPerson("person_a").PendingProfessionExp,
            "重复 no-op 后 Pending 仍保留");
    }

    [Test]
    public void LegacyAndProfessionChannels_CommitIndependently()
    {
        // P5 — Legacy 与 ProfessionExp 两个通道独立，不互相污染
        var run = new RunState();
        run.AddPendingGrowthExp("person_a", 10);
        run.AddPendingProfessionExp("person_a", 20);
        var sink = new FakeSink();

        run.CommitPendingProfessionExp(sink);
        Assert.AreEqual(20, sink.Profession["person_a"], "Profession 通道先兑现");
        Assert.IsFalse(sink.Legacy.ContainsKey("person_a"), "Profession 兑现不得触发 Legacy 通道");

        run.CommitPendingRewards(sink, 0f);
        Assert.AreEqual(10, sink.Legacy["person_a"], "Legacy 通道随后兑现");

        Assert.AreEqual(0, run.GetPerson("person_a").PendingProfessionExp);
        Assert.AreEqual(0, run.GetPerson("person_a").PendingGrowthExp);
    }

    [Test]
    public void Checkpoint_ExportRestore_KeepsPendingProfessionExp_NoEarlyCommit()
    {
        // P6a — Battle/Pending → Export → Restore（Return 前运行时连续性）→ Pending 仍为 X；
        // Restore 本身不提前写长期；同一 Active Run 成功 Commit 后再次 Commit = 0。
        //（本测试只覆盖同一 Active Run 内的 Exactly-Once；Return 后旧 Checkpoint stale replay
        //   不在本文件保证范围，见类注释。）
        var runA = new RunState();
        runA.GetOrCreatePerson("person_a", "战士", 30);
        runA.GetOrCreatePerson("person_b", "猎人", 30);
        runA.AddPendingGold(20);
        runA.AddPendingGrowthExp("person_a", 3);
        runA.AddPendingProfessionExp("person_a", 7);
        runA.AddPendingProfessionExp("person_b", 5);
        runA.CurrentInsight = 60;

        var checkpoint = runA.ExportCheckpoint();

        var runB = new RunState();
        runB.RestoreCheckpoint(checkpoint);
        var sink = new FakeSink();

        Assert.AreEqual(7, runB.GetPerson("person_a").PendingProfessionExp, "Restore 后 PendingProfessionExp 仍为 X");
        Assert.AreEqual(5, runB.GetPerson("person_b").PendingProfessionExp, "同 Run 多 Person 独立恢复");
        Assert.AreEqual(3, runB.GetPerson("person_a").PendingGrowthExp, "Legacy Pending 同样保留");
        Assert.AreEqual(20, runB.PendingGold);
        Assert.AreEqual(60, runB.CurrentInsight);
        Assert.AreEqual(0, sink.Profession.Count, "Restore/Load 不得提前写入长期 ProfessionExp");
        Assert.AreEqual(30, runB.GetPerson("person_a").MaxHP, "Run HP 一并恢复");
        Assert.AreEqual(30, runB.GetPerson("person_a").CurrentHP, "Run CurrentHP 一并恢复");

        // Load 后第一次 Return 才全量兑现（Exactly-Once）
        int committed = runB.CommitPendingProfessionExp(sink);
        Assert.AreEqual(12, committed, "Restore 后 Return 兑现全量 7+5");
        Assert.AreEqual(7, sink.Profession["person_a"]);
        Assert.AreEqual(5, sink.Profession["person_b"]);

        int second = runB.CommitPendingProfessionExp(sink);
        Assert.AreEqual(0, second, "Restore + 兑现一次后重复调用 +0");
    }

    [Test]
    public void Checkpoint_Restore_DoesNotClearRunDeckEntries()
    {
        // P6b — Restore 只重建人物/Pending/HP/领悟，不清空本 Run Deck（由持有方跨快照保留）
        var runA = new RunState();
        runA.AddPendingProfessionExp("person_a", 4);
        var checkpoint = runA.ExportCheckpoint();

        var runB = new RunState();
        var card = ScriptableObject.CreateInstance<BattleCardData>();
        card.CardId = "bcard_keep";
        card.DisplayName = "保留牌";
        card.CardKind = BattleCardKind.Character;
        runB.AddRunCard("person_a", card);
        runB.RestoreCheckpoint(checkpoint);

        Assert.AreEqual(4, runB.GetPerson("person_a").PendingProfessionExp);
        Assert.AreEqual(1, runB.RunDeckEntries.Count, "Restore 不得清空 Run Deck 条目");
    }
}
