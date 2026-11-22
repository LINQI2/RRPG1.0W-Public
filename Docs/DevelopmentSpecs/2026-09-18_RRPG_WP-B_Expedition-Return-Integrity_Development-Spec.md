# RRPG｜WP-B Expedition Return Integrity｜Development Spec

> 日期：2026-09-18  
> 状态：**Development-time Binding / Ready for Repository Landing**  
> Work Package：Expedition Return Integrity  
> 实现基线：`3857389107659090ce78e075457dc5c0da61a7f7`（WP-A Repository Closed）  
> 建议落仓路径：`Docs/DevelopmentSpecs/2026-09-18_RRPG_WP-B_Expedition-Return-Integrity_Development-Spec.md`

---

# -1. 本 WP 的性质

本 WP **不是重新设计 Expedition Return**。

正常成功链已经成立，Current 顺序继续保持：

```text
Run Return Settlement
→ 保持 Expedition Occupancy
→ Return Catch-up（逐日）
→ Return Remaining Carry
→ Person Consequence
→ Clear Expedition Occupancy
→ Final Return Save
→ Completed
```

本 WP 只修复：

> **异常 / 重试 / 部分完成情况下的事务完整性。**

核心原则：

> **Fail Closed + Resume From Existing Phase + No Re-Begin + No Silent Loss**

不建设数据库级事务系统，不承诺跨进程 rollback / exactly-once。

---

# 0. Authority 与已冻结事实

必须服从：

- `GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md`
- `GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md`
- `GameSpec/Village/*` 中 Current Return / Carry / Village Time 规则
- `Docs/Architecture/技术架构总览.md`
- `Docs/问题清单.md` 问题 90
- `.clinerules.md`
- `Docs/AI开发执行规则.md`
- `Docs/AI多Agent开发协作协议.md`

当前已冻结的关键语义：

1. `ProfessionExp` 是 Person-specific 长期成长事实。
2. Safe Return / Retreat / Defeat 都必须**完整保留已经真实产生的 ProfessionExp**。
3. ProfessionExp 只有长期写入明确成功（ack=true）后，才能清除对应 Pending。
4. Return Catch-up 必须逐日执行既有 `Time.TickDay()` 链，不跳日，不另建第二套日结算。
5. Return 最终只在所有前置阶段正确完成后执行最终 Save。
6. 当前不支持正式 Run Save / Resume；stale checkpoint replay 属未来 WP。
7. 本 WP 不承诺应用崩溃 / 进程退出后的 Return 事务恢复。

---

# 1. 当前已证实问题

## P1-A｜Settlement ack 未成为 Return 成功前置

当前 `ExpeditionUI.FinalizeReturn`：

```text
SettleReturn(mode)
→ 无条件 MarkExpeditionReturnSettled()
```

而 `RunState.CommitPendingProfessionExp` 实际采用 ack 语义：

> sink 成功才清 Pending，失败则保留。

当前 `AdventureManager.SettleReturn` 只返回旧 `shouldSave` bool，无法表达：

> “是否所有应兑现 ProfessionExp 均已 ack”。

Retreat / Defeat 当前还会调用旧 `DiscardPendingRewards()`，它会清掉残余 `PendingProfessionExp`，存在静默损失风险。

---

## P1-B｜Return Record 可在 ReturnSettled 阶段被重新 Begin

现有 `BeginExpeditionReturn` 会阻止：

- Completed
- Failed
- CatchUpComplete
- OccupancyCleared
- catch-up running

但 **ReturnSettled** 仍可重新 Begin。

因此：

```text
已经完成若干 Catch-up Day
→ RetryableFailure / OverlapRejected
→ DaysSimulated > 0
→ 再次 FinalizeReturn
→ 新建 Record，DaysSimulated 回到 0
→ 已完整执行过的 Day 再跑
```

可能重复 Village 日结算。

---

## P1-C｜FinalizeReturn 没有统一异常边界

Settlement / Carry / Person Consequence / Save 等异常可能：

- 逃逸 UnityEvent；
- 保留部分内存长期写入；
- 不标记统一 Failed；
- 仍可能通过其它 UI 分支离开；
- elapsed provider / occupancy / adventure 引用进入不一致状态。

本 WP **不做全量 rollback**。

但必须确保：

> 异常以后绝不能继续后续阶段、不能 Save 为成功、不能重新从头执行已完成阶段。

---

## P1-D｜失败 Return 可能被旧“已结束 Adventure”分支绕过

`AdventureManager.SettleReturn` 成功后会：

```text
IsActive = false
```

但 Return 事务可能仍处于：

- ReturnSettled
- CatchUpComplete
- OccupancyCleared

而非 Completed。

当前部分 UI 路径使用：

> `_currentAdventure.IsActive == false`

作为“已经可以普通返回领地”的判断。

因此 Return 后半段失败时，存在绕过未完成事务、直接清 Adventure / Save / 返回 Town 的风险。

Current 产品判断必须改为：

> **Return Transaction 是否 Completed**

而不是仅看：

> Adventure 是否 IsActive。

---

## P2｜Final Save 失败没有清晰的同会话 retry path

当前 Save 失败后：

```text
Phase = OccupancyCleared
Completed = false
```

这是正确基础。

但下一次 `FinalizeReturn` 会从 Begin 阶段进入，而 Begin 又拒绝 OccupancyCleared。

产品层缺少：

> **只重试 Final Save，不重跑 Settlement/Catch-up/Carry/Consequence**

的明确路径。

---

# 2. 本 WP 核心不变量

## I-1｜一个 Return 只对应一个 ExpeditionRecord

从 Return 首次开始直到：

- Completed
- Failed

期间：

> 不得创建第二个 Return Record。

已有未完成 Record 时：

> 必须 resume existing phase。

不得 reset：

- DaysSimulated
- RemainingCarryReturned
- HealthConsequenceCommitted
- ExperienceCommitted
- 其它 exactly-once 标记。

---

## I-2｜ProfessionExp 必须全 Ack 才能越过 Settlement

对于本 Run 所有 `PendingProfessionExp > 0`：

只有当：

> 对应长期 Person Profession 写入明确 ack 成功

才允许清该 Person Pending。

如果存在任意未 ack ProfessionExp：

- Return Settlement 不得视为成功；
- 不得 `MarkExpeditionReturnSettled`；
- 不得 Catch-up；
- 不得 Clear Occupancy；
- 不得 Final Save；
- Pending 必须保留；
- 已 ack 的 Person 不得在重试时重复获得经验。

允许同会话重试 Settlement。

---

## I-3｜Retreat / Defeat 不得丢 ProfessionExp

旧 `DiscardPendingRewards` 的“丢弃全部 Pending”语义：

> 不得再用于清除未 ack ProfessionExp。

技术实现可以：

- 拆分 NonProfession Pending discard；
- 改调用方式；
- 添加明确 API；

由执行 Agent 自主决定。

但必须保持：

> ProfessionExp SafeReturn / Retreat / Defeat 全保留。

金币 / 建材 / Legacy GrowthExp 的 Retreat / Defeat 正式损失比例仍未冻结，本 WP 不改变其现有占位策略。

---

## I-4｜Catch-up 从 `DaysSimulated` 继续

当 Catch-up 返回：

- `RetryableFailure`
- `OverlapRejected`

且 Phase 仍为 `ReturnSettled` 时：

再次尝试必须：

> 从当前 `DaysSimulated` 继续下一天。

已经完整成功的 Day：

> 绝不能再次 `TickDay()`。

---

## I-5｜PartialDayFailure 不自动续跑

如果某 Day 的 `Time.TickDay()` 内部异常，可能已经有部分 Day side effect：

> Record 进入 `Failed`。

同一内存会话内：

- 不自动 retry 当前 Day；
- 不继续后续 Day；
- 不继续 Carry / Consequence / Clear / Save；
- 不重新 Begin。

当前恢复边界继续是：

> Discard Return Record + 从最后一次成功 Save 重新进入产品。

本 WP 不实现内存 rollback。

---

## I-6｜每个后续阶段必须“成功或已完成”才能继续

对于：

- Remaining Carry Return
- Person Consequence
- Occupancy Clear
- Final Save

不得继续使用：

> “失败只 Warning 然后继续”

作为正式事务语义。

每一阶段必须能够区分至少：

- Successfully Applied
- Already Applied / Idempotent
- Rejected / Failed

只有前两者可推进下一阶段。

具体 Result type 由 Agent 决定。

---

## I-7｜Final Save retry 只重试 Save

当：

```text
Phase = OccupancyCleared
Completed = false
```

再次触发 Return：

> 只允许执行 Final Save / CompleteExpeditionReturnSave。

不得重新：

- Settlement
- Catch-up
- Carry Return
- Person Consequence
- Occupancy Clear

Save 成功：

> Phase → Completed。

Save 失败：

> 继续保持 OccupancyCleared / Completed=false。

---

## I-8｜`FinalizeReturn` 返回值必须代表“本次 Return 已完成”

产品层调用者只能在：

> `FinalizeReturn(...) == true`

且 Record Completed 后：

- Reset elapsed provider；
- 清 `_currentAdventure`；
- Hide progression UI；
- 进入普通 Town 完成态。

Return 未完成：

> 必须保留恢复/重试所需状态。

不得因为 `AdventureManager.IsActive == false` 绕过未完成 Return Record。

---

# 3. 建议状态机

不要求新增 Transaction Manager。

优先复用现有：

```text
Active
→ ReturnSettled
→ CatchUpComplete
→ OccupancyCleared
→ Completed

任何不可安全继续的部分写入异常
→ Failed
```

Resume 行为：

| 当前 Phase | FinalizeReturn 再次调用 |
|---|---|
| 无 Record | Begin 新 Return |
| Active | 执行/重试 Settlement |
| ReturnSettled | 从 DaysSimulated 继续 Catch-up |
| CatchUpComplete | 跳过前面阶段，继续 Carry / Consequence / Clear |
| OccupancyCleared | 只 retry Final Save |
| Completed | no-op / already complete |
| Failed | reject；不得自动恢复 |

若现有子阶段标记不足以精确证明 Carry / Consequence exactly-once：

> 优先复用现有 `ExpeditionRecord` flags；

只有确有必要才 additive 增加最小状态。

不得创建平行 Return Transaction Authority。

---

# 4. Existing Host Map

执行前必须重新核对本地现场。

当前已知：

| 职责 | 宿主 |
|---|---|
| 产品 Return orchestration | `ExpeditionUI.FinalizeReturn` |
| Run Settlement | `AdventureManager.SettleReturn` |
| Pending Runtime | `RunState` |
| ProfessionExp sink | `PlayerRunRewardSink` |
| Return transaction record | `VillageRuntimeBridge.ActiveReturnRecord` / `ExpeditionRecord` |
| Catch-up | `VillageRuntimeBridge.RunReturnCatchUp` |
| Carry Return | `VillageRuntimeBridge.ReturnRemainingCarry` |
| Person consequence | `VillageReturnPersonConsequence.TryCommit` |
| Occupancy | `VillageRuntimeBridge.ClearExpeditionPersonIds` |
| Final Save | `TownUI.OnReturnFromExpedition` / `SaveSystem.Save` |
| Product callers | `OnReturnToTown` / `RequestRetreatFromProgression` / `HandleProgressionReturnFromBattle` |

默认原则：

> 原位迁移，不建第二套 Return pipeline。

---

# 5. Technical Checkpoints

```text
WP-B-T1 Settlement Acknowledgement
→ T2 Return Resume State Machine
→ T3 Catch-up Failure / Resume
→ T4 Carry + Consequence + Occupancy Stage Guards
→ T5 Final Save Retry + Product Caller Integration
→ T6 Regression / Product Verification
```

机器可验证阶段自主继续，不逐 Gate 等用户。

---

# 6. T1｜Settlement Acknowledgement

必须证明：

1. 全部 ProfessionExp ack → Settlement 可成功。
2. 任一 Person ack=false → Settlement 不成功。
3. 未 ack Pending 保留。
4. 已 ack Pending 清零。
5. 再次 retry：
   - 已 ack Person +0；
   - 未 ack Person再次尝试；
   - 不重复长期写。
6. Retreat / Defeat 不会通过 discard 清除未 ack ProfessionExp。
7. Settlement 未成功时 `AdventureManager.IsActive` 不得被错误置为 false。

允许增加有限结果对象 / enum。

不允许引入通用事务框架。

---

# 7. T2｜Return Resume State Machine

`FinalizeReturn` 不再假设每次调用都从 Begin 开始。

必须根据现有 Record Phase：

> Begin or Resume。

证明：

- ReturnSettled 不重建；
- CatchUpComplete 不重跑 Settlement / Catch-up；
- OccupancyCleared 不重跑任何前置阶段；
- Completed 不重复；
- Failed 不继续。

---

# 8. T3｜Catch-up

### RetryableFailure
失败发生在两个完整 Day 之间：

> 可同会话 retry。

Retry 后：

> 从 `DaysSimulated` 继续。

### OverlapRejected
不产生本次 Day side effect：

> 保留 Record，可在问题解除后从原进度 retry。

### PartialDayFailure
Day 内部异常：

> Failed / no auto resume。

至少测试：

```text
EffectiveDays = 4
完成 Day0, Day1
RetryableFailure
DaysSimulated == 2
retry
只执行 Day2, Day3
```

不得再次执行 Day0 / Day1。

---

# 9. T4｜Carry / Consequence / Occupancy

### Remaining Carry
只有：

- 成功返还；
- 已返还；

才能继续。

Rejected / exception：

> Fail Closed。

### Person Consequence
只有：

- 成功提交；
- 已提交；

才能继续。

Rejected / exception：

> Fail Closed。

### Occupancy
只有前面全部满足，才能 Clear。

Clear 之后：

> 不得因为 Final Save 失败重新恢复 Expedition Occupancy，也不得重复 Clear side effects。

---

# 10. T5｜Final Save + Product Callers

Final Save：

- 成功 → Completed。
- 返回 false → 保持 OccupancyCleared，允许 retry。
- exception → 不宣称 Completed；必须保持可诊断状态，禁止执行完成态 UI cleanup。

产品调用者必须修正：

## `OnReturnToTown`
如果有未 Completed Return Record：

> 优先 resume Return Transaction。

不能因为 `AdventureManager.IsActive == false` 走普通返回分支。

## `RequestRetreatFromProgression`
只有 FinalizeReturn 成功：

- ResetElapsedProvider
- Hide progression UI

失败时不得破坏 retry surface。

## `HandleProgressionReturnFromBattle`
不能再：

```text
FinalizeReturn(...)
ResetElapsedProvider() // 无条件
```

必须根据 Return 成功结果决定 cleanup。

如果 Return 未 Completed：

> 保留当前产品状态，使同一会话可以安全 retry / 诊断。

---

# 11. Exception Boundary

本 WP 要求在 Return orchestration 层形成明确异常边界。

原则：

### 可安全 retry 的阶段
必须有明确 exactly-once / ack 证据。

### 可能发生部分长期写入的异常
例如一般 Pending settlement 中 sink 已写一部分再 throw：

> 标记 Failed；
> 不自动 retry；
> 不继续后续阶段；
> 不 Final Save。

不要求内存 rollback。

不得 catch 后 Warning + 继续。

---

# 12. 明确不做

本 WP 不处理：

- Run Save / Resume；
- stale checkpoint replay；
- 跨进程 Return transaction；
- 数据库式 rollback；
- SaveVersion 变化；
- Save Schema 重构；
- Return 正式损失比例；
- 金币/建材正式奖励设计；
- Battle；
- Item / TP Legacy Cleanup；
- Shop / Recruit / Event；
- Supply Preview duplicated rule；
- UI 美术；
- Product Home；
- 新 Village 内容。

---

# 13. Regression / Tests

长期最小契约建议至少覆盖：

- `ReturnSettlement_AllProfessionExpAckRequired`
- `ReturnSettlement_PartialAckRetry_NoDuplicate`
- `Retreat_UnackedProfessionExpNeverDiscarded`
- `Defeat_UnackedProfessionExpNeverDiscarded`
- `ReturnSettled_RecordCannotBeRebegun`
- `CatchUp_RetryContinuesFromDaysSimulated`
- `OverlapRetry_DoesNotRepeatCompletedDays`
- `PartialDayFailure_BlocksAutomaticResume`
- `CarryRejected_DoesNotClearOccupancyOrSave`
- `ConsequenceRejected_DoesNotClearOccupancyOrSave`
- `FinalSaveFailure_RetryOnlySave`
- `ReturnCaller_DoesNotCleanupOnIncompleteTransaction`
- `CompletedReturn_IsExactlyOnce`
- 正常 SafeReturn / Retreat / Defeat 既有产品路径保持通过。

现有 WP-2 / WP-3 / V4F Return tests 优先复用与更新。

Disposable Tests by Default。

---

# 14. Product Integration Verification

必须走真实产品路径。

至少证明三组：

## Case A｜正常成功
真实 Expedition → Return → Settlement → Catch-up → Carry → Consequence → Clear → Save → Completed。

## Case B｜可重试失败
注入 safe between-day failure：

```text
完成部分 Days
→ failure
→ 再次触发 Return
→ 从已有 DaysSimulated 继续
→ 不重复 Day side effect
→ 最终 Completed
```

## Case C｜Final Save failure
通过可靠 test seam 模拟 Save false：

```text
OccupancyCleared
→ Save false
→ Return 未 Completed
→ 再次触发
→ 只 retry Save
→ Completed
```

不能用独立 Mock Runner 代替产品入口。

---

# 15. Hard Stop

以下进入 Red / Awaiting Decision：

- 必须改变 Current ProfessionExp 玩家规则；
- 必须改变 Return 顺序；
- 必须实现跨进程事务；
- 必须修改 SaveVersion / Save Schema；
- 必须设计 Retreat/Defeat 正式损失比例；
- 必须重构整个 SaveSystem；
- 必须新建第二套 Return Authority；
- 发现 Current Authority 本身矛盾且无法依据制作人已有裁决解释。

普通 result type / helper / phase routing / test seam：

> Green / Yellow，自主处理。

---

# 16. Final Closeout

完成后：

- 编译 / Console；
- 定向 EditMode；
- 定向 PlayMode；
- 全量回归；
- Product Integration；
- Handoff；
- 项目状态快照；
- 项目日志；
- 问题 90 生命周期；
- Disposable Cleanup；
- Final Diff / Workspace Review；
- Closure Commit Staging Manifest。

本 Spec 不授权 Git 写入。

默认停在：

> **WP-B Expedition Return Integrity Technical Verified / Ready for Commit / Push**

---

# 17. Technical PASS

只有全部成立：

- ProfessionExp 全 ack 才越过 Settlement；
- Retreat / Defeat 不丢 ProfessionExp；
- 未完成 Return Record 永不重新 Begin；
- retryable catch-up 从 DaysSimulated 续跑；
- partial-day failure fail-closed；
- Carry / Consequence rejection 不再 warning-and-continue；
- Occupancy 只在前置阶段完成后清除；
- Save false 可同会话只重试 Save；
- 所有产品 caller 只在 Return Completed 后 cleanup；
- 不再允许 `Adventure.IsActive=false` 绕过未完成 Return transaction；
- 正常 Return 产品路径无回归；
- 无第二套 Return / Save Authority；
- SaveVersion 不变。

才可标：

> **WP-B Technical Verified**
