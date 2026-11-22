# RRPG｜WP-AL-1 Social Foundation（社会域基础）｜Closeout

> 日期：2026-10-04（Asia/Shanghai）
> 实际基线：`main@700343f66a96e00579efe374195879fb05d00cad`；本轮增量尚未包含在该提交中。
> Target Branch：main
> 状态：Implementation Verified / Runtime Semantic Review PASS / Closeout 文档同步完成；尚未 stage / commit / push。
> 验收：制作人确认开发15.0 Runtime Semantic Review PASS；不宣称 Work Package / Branch / Repository Closed。
> 职责：本 WP 实施与验证证据，不替代产品规则、Freeze 或长期 Current Architecture。
> 输入：[WP-AL-1 Development Spec V0.2](2026-10-04_RRPG_WP-AL-1_Social-Foundation_Development-Spec-V0.2.md)、[Autonomous Life Target Technical Architecture](2026-10-04_RRPG_Autonomous-Life_Technical-Architecture-V0.1.md)。两份日期化输入的 388d00d / Awaiting authorization / Not Implemented 是发布时语境；实际执行遵循后续明确授权的 700343f，不回退旧基线。

## 1. 实际交付范围

WP-AL-1 建立独立 Relational Fact Authority、Directed Relationship State、Exactly-once Social Commit 与 Social World Save / Load / NewGame isolation。没有 Production Activity / Event Producer 或 Production Relationship Interpreter，也没有可游玩的完整 Autonomous Life。

两个既有源码修改：

| 文件 | 实际变化 |
|---|---|
| [SaveData.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveData.cs) | 新增顶层 SocialWorld 可空增量区块 |
| [SaveSystem.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveSystem.cs) | 所有 Save 携带 Social snapshot；Player 成功后 Restore Social；successful NewGame reset Social；校验真实根 Player object |

新增 17 个 C# / asmdef 文件：

| 文件 | 职责 |
|---|---|
| [Social.Runtime.asmdef](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/Social.Runtime.asmdef) | 独立纯域程序集 |
| [RelationalFactRecord.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/RelationalFactRecord.cs) | 不可变 Canonical occurrence |
| [RelationalFactLedger.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/RelationalFactLedger.cs) | Fact Authority / 只读历史与 occurrence 索引 |
| [DirectedRelationshipState.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/DirectedRelationshipState.cs) | 不可变有向状态 |
| [DirectedRelationshipRegistry.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/DirectedRelationshipRegistry.cs) | 状态 Registry / IRelationshipQuery |
| [RelationshipMutation.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/RelationshipMutation.cs) | Interpreter 提案 |
| [IRelationshipInterpreter.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/IRelationshipInterpreter.cs) | 纯规划契约 |
| [SocialCommitService.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialCommitService.cs) | Exactly-once / 逻辑原子性 / SocialCommitResult |
| [SocialWorldState.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialWorldState.cs) | world-social-domain-owned Facts + Relationships |
| [SocialWorldSaveData.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialWorldSaveData.cs) | SocialWorld / Fact / Relationship DTO |
| [SocialWorldPersistence.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialWorldPersistence.cs) | Build / Replacement Restore / SocialRestoreResult |
| [SocialRuntimeBridge.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/SocialHost/SocialRuntimeBridge.cs) | Assembly-CSharp 世界宿主与 Save 接缝 |
| [SocialFoundation.Tests.asmdef](../../UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialFoundation.Tests.asmdef) | Editor-only Test Assembly |
| [RelationalFactLedgerTests.cs](../../UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/RelationalFactLedgerTests.cs) | Gate A / Test Interpreter；关系查询测试合并于此 |
| [SocialCommitServiceTests.cs](../../UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialCommitServiceTests.cs) | Gate B |
| [SocialWorldPersistenceTests.cs](../../UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialWorldPersistenceTests.cs) | Gate C |
| [SocialSaveIntegrationTests.cs](../../UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialSaveIntegrationTests.cs) | Gate D / 真实 SaveSystem 反射接合 |

Unity 自动生成的新 .meta 共 21 个：17 个新增源码 / asmdef 的对应 .meta，加 Social.meta、Social/Runtime.meta、SocialHost.meta、Tests/EditMode/AutonomousLife.meta 四个目录 meta。未手工修改 GUID 或 meta 结构；未来获准提交时必须随对应新资产一起提交。

本次文档同步修改技术架构总览、核心对象与术语映射、项目状态快照、问题清单、开发验收交接、DevelopmentSpecs README，并新增本 Closeout。GameDesign、GameSpec、Runtime、Tests、Scene / Prefab / Config / 美术均未因文档同步再次修改。

## 2. 模块与 Public API

Social.Runtime：`references=[]`、`noEngineReferences=true`、`autoReferenced=true`。不依赖 Village.Runtime、Event.Runtime、Person.Runtime、Battle、UI 或 UnityEngine。PersonId 只作为稳定 string identity；未修改既有 asmdef。

Assembly-CSharp 中的 SocialRuntimeBridge / SaveSystem 依赖纯 Social 域。SocialFoundation.Tests 仅显式 reference Social.Runtime，Editor only、UNITY_INCLUDE_TESTS、TestAssemblies；通过反射访问 Assembly-CSharp，不形成反向程序集引用。

| API | 边界 |
|---|---|
| RelationalFactRecord 构造 / get-only 字段；Ledger.Facts / ContainsSourceOccurrence | Record 不可变，集合真正只读；一般外部无 Append / Clear |
| IRelationshipQuery.Count / TryGet / GetOrNeutral | 返回不可变状态；Missing → Neutral，zero side effect；A→B / B→A 独立 |
| IRelationshipInterpreter.Interpret(fact, relationships) | 只读 Query facade，输出 IEnumerable&lt;RelationshipMutation&gt;，不直接写 Ledger / Registry |
| RelationshipMutation(source, target, affinityDelta) | 纯提案；AffinityDelta 是 V0.1 Test / Not Frozen channel |
| SocialCommitService(world, interpreter).Commit(fact) | 唯一业务修改入口；业务失败均先于正式内存 mutation |
| SocialWorldPersistence.Build(world) / Restore(world, data) | 深快照 / 字面 Replacement；Restore 返回过滤与冲突计数 |
| Bridge.EnsureInitialized / QueryFacts / QueryRelationship | 显式初始化；查询不隐式创建持久世界或记录，不公开可写 Current |
| Bridge.CommitRelationalFact(fact, interpreter) | 当前由调用方传 Interpreter；Production ownership 后续裁决 |
| Bridge.BuildSaveData / Restore / ResetForNewGame / ResetForTests | Build 不隐式初始化持久世界；显式替换 / reset |

SocialCommitResult：Success、Duplicate、SourceOccurrenceConflict、InvalidFact、InterpreterFailure、InvalidMutation、DuplicatePairMutation、CommitInProgress。

## 3. Authority / Exactly-once / Immutable 边界

当前正式数据链：

```text
Semantic Relational Fact
→ SocialCommitService
→ IRelationshipInterpreter
→ RelationshipMutation
→ Directed Relationship State
```

Fact = 过去发生了什么；Relationship State = 截至现在关系是什么。两者是不同 Authority，均由 Social World 持有；不将 Shared Relational Fact 复制到人物历史，也不扩展 VillageSocialFactLedger / WorldEventHistory / PersonLifeHistory。

SourceOccurrenceId 采用字面 ordinal identity，不 Trim / 大小写折叠。相同 id / payload → Duplicate / No-op，不追加、不再次解释；不同 payload → SourceOccurrenceConflict / Fail Closed。Runtime Fact 为 sealed / get-only；Query 状态不可变，旧查询对象不随未来提交变化。读取 Missing Pair 返回 Neutral（测试通道 0），不创建记录；self Pair 拒绝。

全部 Interpreter 结果先完整枚举并预验证：每个 Directed Pair 最多一条 Mutation；非法 Pair、非有限 Delta / 结果溢出、重复 Pair、Interpreter / 延迟枚举异常均拒绝，Fact 与 Relationship 保持此前状态。只有计划有效后才 Append / Apply；正式提交阶段无外部业务回调。重入 Commit / Restore 被保护。这是逻辑内存原子性，不是数据库或其它 World Host 的统一事务。

Affinity 是 **V0.1 Test / Not Frozen channel**，不冻结正式 Relationship 模型、范围、维度或产品规则。`test_support` 与 `Target→Actor +10` 仅用于 Fake/Test Interpreter 与测试代码，没有 Production Interpreter。

## 4. SocialWorld Save Schema 与生命周期

SaveVersion = **1**，没有升级。

```text
SaveData
├ Player
├ Village
├ EventWorld
└ SocialWorld : SocialWorldSaveData
   ├ RelationalFacts[] : RelationalFactSaveData
   └ Relationships[] : DirectedRelationshipSaveData
```

| DTO | 字段 |
|---|---|
| RelationalFactSaveData | string SourceOccurrenceId、FactType、ActorPersonId、TargetPersonId；int Year、Month、Day |
| DirectedRelationshipSaveData | string SourcePersonId、TargetPersonId；float Affinity（Test / Not Frozen） |

Build 是深快照：Facts 保持历史顺序，Relationships 按 SourcePersonId / TargetPersonId ordinal 稳定排序，不返回 Runtime 可写引用，不保存查询产生的 Neutral 缺失 Pair。

Restore = **Replacement，非 Merge**：先 Clear，再直接恢复 DTO 中 Fact + Relationship State，不运行 Interpreter / Replay 历史。null → empty；旧 Save 无 SocialWorld → empty，不猜历史。过滤非法 Fact / Relationship；同 occurrence 相同 / 冲突 payload、重复 Pair 保留首条有效记录，并通过 SocialRestoreResult 返回计数。unknown / inactive PersonId 保留，不要求当前 PlayerCollection 能解析。

Save / SaveWithVillage 共用 SaveCore / BuildSaveData，所有入口携带 SocialRuntimeBridge.BuildSaveData 快照。Load 在 JSON 成功解析、SaveVersion 合法、真实根 Player object 存在与 RestorePlayerCollection 成功返回后才 Restore Social。JsonUtility 会物化缺失 / null class 字段，故增加根 Player object 校验；缺失 / null / 重复根成员拒绝，不能被嵌套对象或字符串伪装。Failed Load / Player Restore 失败保留旧 Social。Successful NewGame 在人物初始化成功后显式 ResetForNewGame，形成空 Social State。

## 5. 实际验证与审阅证据

以下为同一 WP Runtime 实现验证轮的 MCP Test Runner 结果；本次文档同步没有重新运行测试，不冒充新一轮 PASS。

| 检查 | 结果 / 覆盖 |
|---|---|
| Compile | 0 error；最终 isCompiling=false、scriptCompilationFailed=false |
| Gate A | 11/11 PASS：Neutral 零副作用、方向性、自关系拒绝、Fact 追加、Duplicate / Conflict、独立 occurrences、Fact / 集合不可变、Query 只读、ordinal identity、纯域引用 |
| Gate B | 13/13 PASS：成功、exactly-once、Interpreter / 非法 Mutation 原子性、多 Pair、重复 Pair、延迟枚举异常、NaN / Infinity、溢出、重入、零 Mutation Fact / 缺 Interpreter |
| Gate C | 8/8 PASS：round trip、replacement、null、非法 / 重复 / 冲突过滤、稳定排序、unknown / inactive PersonId、restore 不重解释、DTO 深快照隔离 |
| Gate D | 16/16 PASS：真实 Save / SaveWithVillage / Load、Legacy 无 SocialWorld、NewGame、Load B 替换 A、版本 1、失败 Load / Player Restore 保留 Social、根 Player 边界、只读 Host 不初始化 |
| Targeted EditMode | **48/48 PASS**；过滤执行数为 48，不把 Runner 全套发现数当 targeted 数 |
| Full EditMode | **1112/1112 PASS**，0 Failed / Skipped，含原有 1064 + 新增 48，零回归 |
| Full PlayMode | **93/93 PASS**，0 Failed / Skipped；与 EditMode 串行执行 |
| Runtime Semantic Review | **开发15.0 PASS**，制作人已在本会话明确确认 |
| git diff --check | Runtime 验证轮 tracked 检查 exit 0；文档同步后的 tracked 文档与 tracked C# 再检查 exit 0 |
| 新文件额外检查 | 17 个新增 C# / asmdef 的 no-index 空白检查无消息；新增本 Closeout 另做同类检查。21 个 Unity meta 的标准空字段可能有尾空格，未手工改动；本轮未 stage，未执行 cached check |
| Scene / Prefab | Runtime 与文档同步均未修改；SampleScene 验证轮哈希不变，Editor 回读 IsDirty=false；Test Runner 临时 Scene 正常清理 |
| 原存档 / 制作人工作区 | Runtime 全量测试改写的实际存档已从测试前备份恢复，SHA-256 一致；当时保护的 16 个原有文件哈希均不变。本次文档同步另核对非授权文档范围的文件元数据及 19 个实现文件哈希不变 |

源码审阅附件为根目录本地 `2026-10-04_RRPG_WP-AL-1_Runtime-Review.diff`，19 个文件、58,583 字节 / 1,149 行；不含 meta 或制作人资源，也不进入未来提交清单。旧 Authority-Landing.diff 同样不提交。

## 6. 两项后续边界

**A. Production Relationship Interpreter ownership**：当前 Bridge 接受调用方 Interpreter，在 WP-AL-1 无 Production Interpreter 阶段允许。首个真实 Activity / Event Producer 接入前，必须裁决生产解释器的统一语义所有权，避免不同调用方自行决定关系解释语义；不在本轮预先选择实现方案，不是本轮返工要求。

**B. 既有 World Host Isolation 风险**：Village / Event NewGame / failed-Load 的世界级统一事务隔离仍是既有遗留风险。WP-AL-1 只保证 Social Domain 自身正确，未顺手重构其它 Host；不是 WP-AL-1 新缺陷，也不据此关闭其它世界事务风险。

两项已登记在[问题清单](../问题清单.md#wp-al-1-boundaries)，Current Architecture / 状态同步保留同一边界。

## 7. Current / Target 与发布边界

Current 只有已实现的 Social Foundation。Activity Framework、VillageTime Activity Window、Utility AI、Weighted Candidate Ordering、Conflict Arbitration、Activity FSM、Recent Activity State、Recognition Projection、Autonomous Life Event Integration、Life Episode Runtime、Navigation / Presentation 仍属后续 WP / Target / Deferred。Life Episode V0.1 的目标是 Derived Projection，Materialized Runtime 后置；本 WP 未建立 Episode Authority。

WP-AL-2 **未启动**。没有 Runtime 语义或写入范围偏离；Spec 的建议测试文件拆分合并为四个测试源文件，根 Player 校验落实失败 Load 边界，均处于授权范围。Runtime 实施未触发 Stop Condition；本次只是文档同步。

当前分支 / Target Branch 均为 main，HEAD 仍为实际基线 700343f；暂存区为空，尚未 commit / push，无 WP-AL-1 commit SHA。Ready for Commit / Push 仅表示材料准备，不构成 Git 写入授权；本轮按制作人要求停在未发布状态，不宣称 Work Package / Branch / Repository Closed。

未来提交候选为 19 个实现源码 / asmdef、21 个对应 meta，以及本次明确同步的 7 份文档；实际 stage 前仍需用户精确授权与重新核对。制作人已有 Config / TMP / QualitySettings / 美术资源、两个本地审阅 .diff 不纳入清单，也不删除。若完整 staged `git diff --cached --check` 仅因 Unity meta 标准空字段尾空格报警，应精确回报文件 / 行，由开发15.0决定处理，不擅自修改 GUID / meta 结构。

Current 入口：[技术架构总览](../Architecture/技术架构总览.md)、[对象与术语映射](../Architecture/核心对象与术语映射.md)、[项目状态快照](../项目状态快照.md)、[最新 Handoff](../开发验收交接.md)、[DevelopmentSpecs 索引](README.md)。
