# Save GameSpec V0.1

> 状态：Current persistence contract；只汇总已确认边界，不定义各领域完整字段清单，也不从 Legacy DTO 推导玩法。

## 1. Current Model

- 当前为本地 JSON、单存档槽位，`SaveVersion = 1`。
- 产品入口包括 New Game、Continue / Load 与 Delete Save。具体 UI 和调用时机以当前实现与任务验收核对，不由本节冻结新的触发点。
- SaveVersion 不支持或文件损坏时应安全失败，不让坏档造成未处理异常；这不是跨版本自动迁移承诺。

## 2. Serialization Contract

- 持久化使用 DTO 与稳定 ID；Runtime 对象、配置对象与 Persistence DTO 分离。
- 不直接序列化 `ScriptableObject` 或其它 `UnityEngine.Object`。`DisplayName` 只用于呈现，不承担身份 Authority。
- 配置 ID 变更有兼容风险；具体 missing-ID 的 fail / skip / fallback 策略若各领域尚未统一，不得由 Save 总入口擅自统一为新规则。

## 3. Domain Ownership

- Person 长期身份、职业与成长状态服从 [Person Current GameSpec](../Person/Person-Profession-Growth-GameSpec-V0.1.md)。
- Village 世界、时间、人员与资源的状态语义服从 [Village Current GameSpec](../Village/Village-GameSpec-V0.1.md)及其[资源耦合规格](../Village/Village-Resource-Coupling-GameSpec-V0.1.md)。
- Expedition 的 Progression、Return 与持久化边界服从 [Expedition Current GameSpec](../Expedition/Expedition-Progression-GameSpec-V0.1.md)。当前已有状态存储不等于完整跨进程 Expedition checkpoint / Resume 系统。
- 单场 Battle runtime 不成为长期 Save Authority；其玩家规则仍由 [Battle Current GameSpec](../Battle/Battle-GameSpec-V0.1.md)负责。

## 4. Runtime Exclusion 与兼容

当前不将完整 Battle runtime、Hand / Draw / Discard、Battle StatusRuntime、UI State 或完整 Expedition Progression checkpoint 作为正式跨进程恢复系统。

Legacy DTO fields may remain for backward compatibility without restoring retired product semantics. 例如 `OwnedItemCardIds` 可作为 compatibility no-op；`Injury` / `IsDead` 等旧字段即使存在，也不表示 Current Battle 再次支持永久伤残或死亡结果。兼容字段的读写事实应以当前代码核对，不能从字段名推导玩家规则。

## 5. Save Trigger Ownership：Awaiting Architecture Decision

谁在何时发起保存尚未统一为长期 Architecture Ownership，见 [问题 40](../../Docs/问题清单.md)。不得从旧 Save 13 或当前调用位置反向裁决；新的触发策略需要独立技术决定。

## 6. Not Guaranteed

当前不承诺完整 Expedition 跨进程 Resume、Battle 中断恢复、cloud save、multi-slot，或通用 rollback / transaction / exactly-once Save 系统。这些是 **Not Current**，并非永久禁止未来设计。

## 7. 日内兼容与持续世界（Published Main / Runtime Verified）

[A V0.2](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-Time-Expedition-Arrival_Development-Spec-V0.2.md)及[B～D 规格](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-Work-Lifecycle-Onsite_Development-Spec-V0.1.md)的 additive nullable 区块已本地验证；实际数值/失败记录见[实施记录](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-AD_Execution-Record.md)。顶层 SaveVersion 仍为1。

- VillageTimeSaveData.IntradayTime SchemaVersion=1：CurrentWorldMinute、DriverRemainder、LastCompletedDailyBoundaryMinute。旧档明确近似迁移，无 Load Tick/Selection/Drain。
- Village.IntradayWork：材料已投入/批次余量、实际劳动聚合、到岗与 Noon/营养 evidence；replacement，不重放生产/供餐/Join，旧块缺失不猜历史。
- ActivityWorld IntradaySchemaVersion=1：最小 PersonActivityStates 及 Scheduled ActiveInstances 的稳定身份/机会/候选语义/目的地/目标参与方式/计划时刻/Committed或Executing。旧 daily API 不持久化其 unscheduled transient；本次日内迁移明确保存连续执行语义，恢复重建本人 Claim，不 replay 或重开 Window。
- PersonActivityState 的 Presence 与最近扑空仅持必要地点/类型/目标/分钟，不保存整本活动历史。DecisionWorld 增加最后 resolved opportunity minute/id，NoMajor 同样去重；不保存 Candidate Order、Trace、Proposal、RNG。SocialWorld/Outbox 仍为已有权威。

非法新块和与时间不匹配的活跃计划在任何 World Restore 前 preflight 拒绝，保留旧 world；Player Restore 成功后 replacement。显式 SaveWithVillage 尊重传入快照，存在日内状态却缺匹配时钟则明确拒绝写出。不能由此推导 Village/Event 全世界统一 Load transaction 已完成。

普通 Save 只允许 Idle 且无未完成 Expedition/Return；Running/Yield/Waiting/Failed 拒绝，不能生成丢请求的中间档。OccupancyCleared 后 Return FinalSave 必须匹配可信会话，失败继续冻结、成功才释放。推进请求和返程进度不持久化，未承诺途中跨进程 Resume。
