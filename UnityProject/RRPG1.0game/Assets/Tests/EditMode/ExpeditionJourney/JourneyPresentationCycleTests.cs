using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// WP-E｜T3 Presentation Cycle / exactly-once / single-transition 长期契约测试（EditMode）。
///
/// 覆盖 FDS §4.3 / §4.4 / §5：
///   1. 0 / 1 / N 表现投影；
///   2. 一个 Cycle 最多消费一个 Progression Transition；
///   3. 消费后必须重新读取 Current Progression State 并建立新 Cycle（deferred dispatch）；
///   4. 连续 Placeholder / N=1 链 A → B → C 不得在同一旧 callback 中被连续吞掉；
///   5. 自动完成与 Skip 汇合到唯一完成入口（重复调用 no-op）；
///   6. Branch 不被 Skip / Continue 代选；选择前重新验证合法性，失效不静默改选；
///   7. Return / Hide / Rebind / Invalidate 后旧 callback 全部 no-op。
///
/// 本测试直接引用 ExpeditionJourney.Runtime（与 ExpeditionProgression.Tests 同一模式）。
/// </summary>
public class JourneyPresentationCycleTests
{
    private sealed class FakeDriver : IJourneyPresentationDriver
    {
        public int TravelBegun;
        public int TravelToBranchBegun;
        public int TerminalCount;
        public int BranchShownCount;
        public int HideBranchCount;
        public int CancelCount;
        public int PlaceholderEncounterCount;
        public Action LastTravelComplete;
        public Action<string> LastBranchChosen;
        public List<JourneyNodeRef> LastNodes = new List<JourneyNodeRef>();

        public void PresentTravel(JourneyNodeRef node, int cycleIndex, Action onComplete)
        {
            TravelBegun++;
            LastNodes = new List<JourneyNodeRef> { node };
            LastTravelComplete = onComplete;
        }

        public void PresentTravelToBranch(IList<JourneyNodeRef> nodes, int cycleIndex, Action onComplete)
        {
            TravelToBranchBegun++;
            LastNodes = nodes != null ? new List<JourneyNodeRef>(nodes) : new List<JourneyNodeRef>();
            LastTravelComplete = onComplete;
        }

        public void PresentTerminal(IList<JourneyNodeRef> none, int cycleIndex)
        {
            TerminalCount++;
        }

        public void ShowBranchChoice(IList<JourneyNodeRef> nodes, Action<string> onChosen)
        {
            BranchShownCount++;
            LastNodes = nodes != null ? new List<JourneyNodeRef>(nodes) : new List<JourneyNodeRef>();
            LastBranchChosen = onChosen;
        }

        public void HideBranchChoice()
        {
            HideBranchCount++;
        }

        public void PresentPlaceholderEncounter(JourneyNodeRef node, Action onShown)
        {
            PlaceholderEncounterCount++;
        }

        public void CancelPresentation()
        {
            CancelCount++;
        }
    }

    private sealed class Harness
    {
        public readonly List<string> Available = new List<string>();
        public readonly List<string> Entered = new List<string>();
        public readonly Queue<Action> Deferred = new Queue<Action>();
        public readonly FakeDriver Driver = new FakeDriver();
        public readonly JourneyPresentationCycleController Controller;
        public int ReadCount;

        /// <summary>产品 Node Entry 是否成功（Issue E：false = 入口被拒绝，例 InsufficientSupply）。</summary>
        public bool NextEntrySucceeds = true;

        public Harness()
        {
            Controller = new JourneyPresentationCycleController(
                () =>
                {
                    ReadCount++;
                    return Available.Select(id => new JourneyNodeRef(id, string.Empty)).ToList();
                },
                id => { Entered.Add(id); return NextEntrySucceeds; },
                action => Deferred.Enqueue(action),
                Driver);
        }

        /// <summary>执行一次 deferred 重调度（= 下一帧建立新 Cycle）。</summary>
        public void NextFrame()
        {
            if (Deferred.Count == 0) return;
            Deferred.Dequeue()();
        }
    }

    // ==================== 1. 0 / 1 / N 投影 ====================

    [Test]
    public void Projection_ZeroOneN()
    {
        Assert.AreEqual(JourneyProjection.None, JourneyPresentationCycleController.Project(0));
        Assert.AreEqual(JourneyProjection.Single, JourneyPresentationCycleController.Project(1));
        Assert.AreEqual(JourneyProjection.Branch, JourneyPresentationCycleController.Project(2));
        Assert.AreEqual(JourneyProjection.Branch, JourneyPresentationCycleController.Project(5));
    }

    [Test]
    public void Terminal_WithNoNextNode_ShowsTerminal_AndConsumesNothing()
    {
        var h = new Harness();
        h.Available.Clear();

        h.Controller.Begin();

        Assert.AreEqual(1, h.Driver.TerminalCount, "N=0 必须显示 terminal 表现");
        Assert.AreEqual(0, h.Driver.TravelBegun, "N=0 不得播放旅行");
        Assert.AreEqual(0, h.Entered.Count, "N=0 不得自动进入任何 Node（也不得自动判胜）");
        Assert.AreEqual(0, h.Deferred.Count, "N=0 没有 Transition 可消费，不需要重调度");
        Assert.AreEqual(0, h.Controller.ConsumedTransitionsInCurrentCycle);
    }

    // ==================== 2 / 3. 单 Transition + 重新读取 ====================

    [Test]
    public void Single_UsesOneTransition_ThenRequiresNewCycleFromFreshState()
    {
        var h = new Harness();
        h.Available.Add("n1");

        h.Controller.Begin();
        Assert.AreEqual(1, h.Driver.TravelBegun, "N=1 必须播放一段旅行");
        Assert.AreEqual(0, h.Entered.Count, "旅行完成前不得进入 Node");

        bool first = h.Controller.CompleteCurrentTravelPresentation("auto");

        Assert.IsTrue(first);
        Assert.AreEqual(new[] { "n1" }, h.Entered.ToArray(), "完成表现后恰好进入一次该 Node");
        Assert.AreEqual(1, h.Controller.ConsumedTransitionsInCurrentCycle, "本 Cycle 恰好消费一个 Transition");
        Assert.AreEqual(1, h.Deferred.Count, "消费后必须 deferred 重调度（不得同栈继续）");

        bool second = h.Controller.CompleteCurrentTravelPresentation("skip");
        Assert.IsFalse(second, "同一 Cycle 第二次完成必须是 no-op（exactly-once）");
        Assert.AreEqual(1, h.Entered.Count, "重复完成不得重复进入 Node");

        h.Available.Clear();
        h.Available.Add("n2");
        int readsBefore = h.ReadCount;
        h.NextFrame();

        Assert.AreEqual(readsBefore + 1, h.ReadCount, "新 Cycle 必须重新读取 Current Progression State");
        Assert.AreEqual(3, h.Controller.CycleIndex,
            "重调度后建立新的 Presentation Cycle（消费时 +1，新 Cycle 再 +1）");
    }

    [Test]
    public void AutoAndSkip_ConvergeOnTheSameCompletionEntry()
    {
        var auto = new Harness();
        auto.Available.Add("n1");
        auto.Controller.Begin();
        auto.Controller.CompleteCurrentTravelPresentation("auto");

        var skip = new Harness();
        skip.Available.Add("n1");
        skip.Controller.Begin();
        skip.Controller.CompleteCurrentTravelPresentation("skip");

        CollectionAssert.AreEqual(auto.Entered, skip.Entered, "自动完成与 Skip 必须进入同一个 Node");
        Assert.AreEqual(auto.Controller.ConsumedTransitionsInCurrentCycle,
            skip.Controller.ConsumedTransitionsInCurrentCycle);
    }

    // ==================== 4. 连续 Placeholder / N=1 链 ====================

    [Test]
    public void ConsecutiveSinglePlaceholderChain_DoesNotSwallowMultipleNodesInOneCallback()
    {
        var h = new Harness();
        h.Available.Add("A");
        h.Controller.Begin();

        h.Controller.CompleteCurrentTravelPresentation("auto");

        Assert.AreEqual(1, h.Entered.Count, "A 完成后不得在同一旧 callback 中继续进入 B");
        Assert.AreEqual("A", h.Entered[0]);
        Assert.AreEqual(1, h.ReadCount, "消费后必须等新 Cycle 重新读取，而不是继续使用旧快照");

        // Cycle B
        h.Available.Clear();
        h.Available.Add("B");
        h.NextFrame();
        h.Controller.CompleteCurrentTravelPresentation("auto");

        Assert.AreEqual(new[] { "A", "B" }, h.Entered.ToArray(), "B 必须来自新的 Cycle");
        Assert.AreEqual(2, h.ReadCount);

        // Cycle C
        h.Available.Clear();
        h.Available.Add("C");
        h.NextFrame();
        h.Controller.CompleteCurrentTravelPresentation("auto");

        Assert.AreEqual(new[] { "A", "B", "C" }, h.Entered.ToArray());
        Assert.AreEqual(3, h.ReadCount, "每个 Cycle 必须各自重新读取一次 Current Progression State");
        Assert.AreEqual(3, h.Driver.TravelBegun, "每个 Cycle 各播放一次旅行表现");
    }

    // ==================== 5 / 6. Branch ====================

    [Test]
    public void Branch_ShowsChoice_AndSkipDoesNotAutoSelect()
    {
        var h = new Harness();
        h.Available.Add("b1");
        h.Available.Add("b2");

        h.Controller.Begin();
        Assert.AreEqual(1, h.Driver.TravelToBranchBegun, "N>1 必须播放至 Branch");
        Assert.AreEqual(0, h.Driver.BranchShownCount, "旅行未完成前不得显示 Branch");

        h.Driver.LastTravelComplete();

        Assert.AreEqual(1, h.Driver.BranchShownCount, "抵达后只显示 Branch Choice");
        Assert.IsTrue(h.Controller.IsAwaitingBranch);
        Assert.AreEqual(0, h.Entered.Count, "显示 Branch 不得自动进入任何 Node");

        // Continue / Skip 在 Branch 阶段不得代选
        Assert.IsFalse(h.Controller.CompleteCurrentTravelPresentation("skip"), "Skip 不得代选 Branch");
        Assert.AreEqual(0, h.Entered.Count);
        Assert.AreEqual(2, h.Driver.LastNodes.Count, "Branch 选项只能来自当次合法后继");
    }

    [Test]
    public void Branch_Choice_IsRevalidated_AndConsumesExactlyOneTransition()
    {
        var h = new Harness();
        h.Available.Add("b1");
        h.Available.Add("b2");
        h.Controller.Begin();
        h.Driver.LastTravelComplete();

        bool chosen = h.Controller.ChooseBranch("b2");

        Assert.IsTrue(chosen);
        Assert.AreEqual(new[] { "b2" }, h.Entered.ToArray(), "玩家选择的 Node 恰好进入一次");
        Assert.AreEqual(1, h.Controller.ConsumedTransitionsInCurrentCycle);
        Assert.AreEqual(1, h.Deferred.Count);
        Assert.IsFalse(h.Controller.IsAwaitingBranch, "选择后 Cycle 结束");
    }

    [Test]
    public void Branch_InvalidChoice_IsRejected_AndRefreshesWithoutSilentRePick()
    {
        var h = new Harness();
        h.Available.Add("b1");
        h.Available.Add("b2");
        h.Controller.Begin();
        h.Driver.LastTravelComplete();

        // 玩家选择前 Current Progression State 变化（b1 不再合法）
        h.Available.Clear();
        h.Available.Add("b3");

        bool chosen = h.Controller.ChooseBranch("b1");

        Assert.IsFalse(chosen, "失效后继必须被拒绝");
        Assert.AreEqual(0, h.Entered.Count, "拒绝后不得静默改选其它 Node");
        Assert.AreEqual(1, h.Deferred.Count, "失效后必须重新读取 Current State 刷新可选道路");

        h.NextFrame();

        Assert.AreEqual(1, h.Driver.TravelBegun,
            "刷新后的新 Cycle 只根据新状态播放一次表现（b3 为 N=1，不得复用旧 Branch 快照）");
        Assert.AreEqual(1, h.Driver.TravelToBranchBegun, "旧 Branch 快照不得再被复用");
    }

    // ==================== 7. 失效 ====================

    [Test]
    public void Invalidate_MakesOldCallbacksNoOp()
    {
        var h = new Harness();
        h.Available.Add("n1");
        h.Controller.Begin();
        Action stale = h.Driver.LastTravelComplete;

        h.Controller.Invalidate();

        Assert.IsTrue(h.Driver.CancelCount >= 1, "Invalidate 必须取消当前表现");
        Assert.IsFalse(h.Controller.IsActive);

        stale();
        h.Controller.CompleteCurrentTravelPresentation("auto");

        Assert.AreEqual(0, h.Entered.Count, "Return / Hide / Rebind / Invalidate 后旧 callback 必须 no-op");
        Assert.AreEqual(0, h.Deferred.Count);
    }

    [Test]
    public void Invalidate_ThenNewSession_StartsFromFreshState()
    {
        var h = new Harness();
        h.Available.Add("old");
        h.Controller.Begin();
        h.Controller.Invalidate();

        h.Available.Clear();
        h.Available.Add("new");
        h.Controller.Begin();
        h.Driver.LastTravelComplete();

        Assert.AreEqual(new[] { "new" }, h.Entered.ToArray(), "重新接入后必须从 Current State 重新开始");
    }

    [Test]
    public void NewCycleAfterConsumption_StartsFromFreshState()
    {
        var h = new Harness();
        h.Available.Add("n1");
        h.Controller.Begin();
        h.Controller.CompleteCurrentTravelPresentation("auto");

        h.Available.Clear();
        h.Available.Add("n2");
        int readsBefore = h.ReadCount;
        h.NextFrame();

        Assert.AreEqual(readsBefore + 1, h.ReadCount, "新 Cycle 必须重新读取 Current Progression State");
        Assert.AreEqual(3, h.Controller.CycleIndex,
            "重调度后建立新的 Presentation Cycle（消费时 +1，新 Cycle 再 +1）");
        Assert.AreEqual(2, h.Driver.TravelBegun, "新 Cycle 播放自己的旅行表现");
    }
    // ==================== 8. WP-Event-1 Producer Acceptance Repair 2（Issue E） ====================

    /// <summary>
    /// **Issue E 回归**：产品 Node Entry 失败（例：`InsufficientSupply`）时，
    /// 当前 Branch 呈现必须保持可交互、不消费 Transition、不推进 Cycle、不重调度、不自动重试；
    /// 只有 Entry 成功才允许推进。
    /// </summary>
    [Test]
    public void Branch_EntryRejected_KeepsCurrentPresentation_AndDoesNotAdvance()
    {
        var h = new Harness();
        h.Available.Add("b1");
        h.Available.Add("b2");
        h.Controller.Begin();
        h.Driver.LastTravelComplete();

        Assert.IsTrue(h.Controller.IsAwaitingBranch, "必须已到达 Branch（等待玩家选择）");
        int cycleBefore = h.Controller.CycleIndex;
        int hideBranchBefore = h.Driver.HideBranchCount;
        int presentationBefore = h.Driver.TravelBegun + h.Driver.TravelToBranchBegun;

        // 产品入口拒绝（例：补给不足）
        h.NextEntrySucceeds = false;
        bool chosen = h.Controller.ChooseBranch("b1");

        Assert.IsFalse(chosen, "入口失败时 ChooseBranch 必须返回 false");
        Assert.IsTrue(h.Controller.LastEntryRejected, "必须记录「产品入口被拒绝」");
        Assert.AreEqual("b1", h.Controller.LastEntryRejectedNodeId);
        Assert.AreEqual(0, h.Controller.ConsumedTransitionsInCurrentCycle, "失败不得消费本次 Transition");
        Assert.AreEqual(cycleBefore, h.Controller.CycleIndex, "失败不得推进 Cycle（不累计 cycle）");
        Assert.IsFalse(h.Controller.HasPendingReSchedule, "失败不得重调度");
        Assert.AreEqual(0, h.Deferred.Count, "失败不得排队新 Cycle");
        Assert.IsTrue(h.Controller.IsAwaitingBranch, "失败后必须仍停留在当前 Branch（可选择其它路线）");
        Assert.AreEqual(hideBranchBefore, h.Driver.HideBranchCount, "失败不得关闭当前 Branch 卡面");
        Assert.AreEqual(presentationBefore, h.Driver.TravelBegun + h.Driver.TravelToBranchBegun,
            "失败不得产生新的节点呈现");

        // 连续第二次点击同一非法分支：仍明确失败、不累计、不生成多张卡
        bool chosenAgain = h.Controller.ChooseBranch("b1");
        Assert.IsFalse(chosenAgain, "重复点击仍必须明确失败");
        Assert.AreEqual(2, h.Entered.Count, "每次点击只请求一次产品入口（不自动重试 / 无 duplicate callback）");
        Assert.AreEqual(0, h.Deferred.Count, "重复失败不得排队新 Cycle");
        Assert.AreEqual(cycleBefore, h.Controller.CycleIndex);

        // 改选合法分支 → 成功才允许推进
        h.NextEntrySucceeds = true;
        bool ok = h.Controller.ChooseBranch("b2");

        Assert.IsTrue(ok, "合法分支必须成功");
        Assert.AreEqual(3, h.Entered.Count);
        Assert.AreEqual(1, h.Controller.ConsumedTransitionsInCurrentCycle, "成功必须消费本次 Transition");
        Assert.AreEqual(cycleBefore + 1, h.Controller.CycleIndex, "成功才推进 Cycle");
        Assert.IsFalse(h.Controller.IsAwaitingBranch, "成功后必须结束 Branch 呈现");
        Assert.AreEqual(1, h.Deferred.Count,
            "成功后必须 deferred 重调度（下一 Cycle 重新读取 Current Progression State）");
    }

    /// <summary>
    /// **Issue E 回归（N=1 Travel）**：Travel 呈现完成后的产品入口失败 → 不推进、不重调度；
    /// 条件变化后再次尝试成功才允许推进。
    /// </summary>
    [Test]
    public void Travel_EntryRejected_DoesNotAdvanceCycle()
    {
        var h = new Harness();
        h.Available.Add("n1");
        h.Controller.Begin();
        int cycleBefore = h.Controller.CycleIndex;

        h.NextEntrySucceeds = false;
        bool advanced = h.Controller.CompleteCurrentTravelPresentation("auto");

        Assert.IsFalse(advanced, "入口失败时不得声称已消费 Transition");
        Assert.IsTrue(h.Controller.LastEntryRejected, "必须记录「产品入口被拒绝」");
        Assert.AreEqual(cycleBefore, h.Controller.CycleIndex, "失败不得推进 Cycle");
        Assert.AreEqual(0, h.Controller.ConsumedTransitionsInCurrentCycle);
        Assert.AreEqual(0, h.Deferred.Count, "失败不得重调度（不得出现后续节点呈现）");

        // 条件变化（例：补给补足）后再次尝试 → 成功才推进
        h.NextEntrySucceeds = true;
        bool ok = h.Controller.CompleteCurrentTravelPresentation("auto");

        Assert.IsTrue(ok, "入口成功后必须允许推进");
        Assert.AreEqual(cycleBefore + 1, h.Controller.CycleIndex);
        Assert.AreEqual(1, h.Controller.ConsumedTransitionsInCurrentCycle);
        Assert.AreEqual(1, h.Deferred.Count);
    }

}
