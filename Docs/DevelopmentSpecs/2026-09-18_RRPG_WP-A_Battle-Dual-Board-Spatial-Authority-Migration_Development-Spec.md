# RRPG｜WP-A Battle Dual-Board Spatial Authority Migration｜Development Spec

> 日期：2026-09-18
> 状态：**Development-time Binding / Ready for Execution**
> Work Package：Battle Dual-Board Spatial Authority Migration
> 目标执行 Agent：Cline + Unity MCP / 团结引擎 Batchmode
> 正式落仓路径：`Docs/DevelopmentSpecs/2026-09-18_RRPG_WP-A_Battle-Dual-Board-Spatial-Authority-Migration_Development-Spec.md`
>
> Authority 基线：`13a6643297adc60a48c156cf4a5530b897899776` (`docs(authority): align battle expedition product model`)
>
> 本 Spec 只约束本 Work Package 的开发目标、边界与验收。玩家规则继续以 Current GameSpec 为最高领域 Authority；如实现现场与本 Spec 的技术候选冲突，执行 Agent 可在不改变玩家行为与 Authority 的前提下自主调整技术结构。

## -1. Authority 输入

本 WP 必须服从以下已落仓 Current Authority：

- `GameSpec/Battle/Battle-GameSpec-V0.1.md`
  - R5.0：Player / Enemy 独立 3×3 Board
  - R5.1：共享空间语言 ≠ 共享 Occupancy
  - R5.3.1：中间非空阻挡排规则
  - R5.3.3：双 Board 下虚拟六排 Combat Range Projection
  - R6：邻接与 Center 规则
  - R15.4：失能后规则层退出战场并释放有效格
- `GameSpec/Battle/README.md`
- `.clinerules.md`
- `Docs/AI开发执行规则.md`
- `Docs/AI多Agent开发协作协议.md`

本文件不覆盖这些 Authority；只把已经冻结的玩家行为转化为一个有界的技术 Work Package。

---

## 0. Work Package 目标

本 WP 只解决一个核心实现偏差：当前 `BattleField` 使用敌我共享单一 3×3 Occupancy，而制作人已确认 Current 产品模型为 **Player 与 Enemy 各自拥有独立 3×3 Board**。

目标：
- Occupancy Authority 改为按阵营独立 Board；
- 保留现有 Row / Column 空间语言；
- 保留 Current Range 核心规则，以虚拟 6 排战斗距离投影解释双 Board；
- Move / Swap / Adjacency / 失能退场正确服从所属 Board；
- 不破坏 Shared Deck、Shared AP、Intent、Gate 0 Rule Pipeline 和真实 Battle 产品入口。

## 1. Binding｜玩家与规则不变量

### 1.1 双 Board
- Player Board：3 Row × 3 Column。
- Enemy Board：3 Row × 3 Column。
- 两边共享 `BattleRow` / `BattleColumn` 空间语言。
- Occupancy 完全独立。
- Player 与 Enemy 可同时占据各自 Board 相同 `(Row, Column)`。

### 1.2 Occupancy
- Move / Deploy / 空格判断只读取单位所属 Board。
- 同一 Board 内两个有效单位不得占同一格。
- 对方 Board 同坐标永远不构成本 Board 占位。
- 不建立第二套 Position 真相源。

### 1.3 Adjacency
- 只在同一 Board 内计算。
- 不允许跨敌我 Board 邻接。
- Center 占位只读取当前 Board 内仍具战斗能力单位。

### 1.4 Range
本 WP 不重设计 Current Range。继续保留：
- 中间非空阻挡排数量 < 最终 Attack Range；
- 空排不计；
- 同一物理排多个单位只算一排；
- 两端不计；
- 敌我规则对称。

双 Board 的战斗距离投影：

`Player Back → Player Middle → Player Front → Enemy Front → Enemy Middle → Enemy Back`

Enemy 向 Player 攻击时反向读取。

重要：Combat Distance Projection 不是共享 Occupancy Grid。

### 1.5 0HP / 失去战斗能力
HP≤0 后：
- 从所属 Board 的有效 Occupancy 退出；
- 该格重新视为空位；
- 不再参与 Adjacency / normal target / Buff / Trait / action；
- 对应角色牌按 Current Battle Rule 退出本场正常循环。

视觉倒下/灰化/撤离属于 Presentation，不构成规则占位。

### 1.6 Worker Move / Swap
- 《补位》合法空格 = Player Board 内空位。
- Enemy Board 同坐标无关。
- 《换岗》只允许同一 Player Board 内合法我方单位交换。
- Swap 原子：成功则 Position/Occupancy 同步；失败零部分写入。

## 2. 不属于本 WP
禁止顺带开发：
- Legacy Item / TP 清理；
- 旧 Attack / Skill / Guard 删除；
- Return Integrity；
- Common Card 新内容；
- Enemy Shared AP / 新 Enemy 行动系统；
- 新 Range 分类；
- 地形/寻路/连续距离；
- 战场尺寸扩展；
- >9 敌人替补/排队；
- 正式 UI 美术；
- SaveVersion / Save Schema；
- Legacy Expedition Cleanup。

## 3. Existing Host Map｜预期重点
执行前必须以本地代码重新确认。

| 职责 | 当前宿主 | 预期处理 |
|---|---|---|
| Position value | `BattleUnit.Row/Column` | 保留 |
| Occupancy Authority | `BattleField` | 迁移为按 Board / Faction 独立 |
| Player deploy | `BattleSystem.DeployCharacter/DeployHero` | 对齐 Player Board |
| Enemy spawn | `BattleSystem.InitializeBattle` | 对齐 Enemy Board |
| Range | `GetBlockingRowCount/GetPhysicalRowIndex/IsInRange` | 保留规则，重构投影读取 |
| Adjacency | `SkillSystem.IsAdjacentAlly` | 限定同 Board |
| Rule facts | `BattleRuleContextRuntime` | AlliedCells / Move / Swap 适配 |
| UI layout | `BattleUI` / `BattleUIBattlefieldLayout` | 结构尽量不动 |
| Intent | Enemy Intent pipeline | 原则上不动，随 Range 适配 |
| Status iteration | `StatusSystem` | 如依赖共享 Field 全量枚举则最小调整 |

## 4. 技术约束

### 4.1 不建立第二套 Position Authority
允许 `BattleField` 内部变为两个 Board 容器；禁止保留共享 Grid 又额外建立平行 Board 真相源。

### 4.2 Board 归属必须稳定
可使用 enum Side / Faction、两个 Dictionary、Board 容器等有限结构。
不得依赖 Scene hierarchy / UI Row / 字符串拼接判断 Board。

### 4.3 Deploy 不得静默覆盖
同一 Board 已占位时必须失败或由上层先验证；不得继续当前“直接 overwrite”语义。

### 4.4 Range 与 Occupancy 解耦
Range 读取双方单位 Row 与 combat-capable 状态，投影到虚拟战斗排；不得重新制造共享 Occupancy。

## 5. Technical Checkpoints

`WP-A-T1 Spatial Authority → T2 Deploy/Removal → T3 Range/Adjacency → T4 Gate0 Spatial Integration → T5 Product Integration + Regression`

Green / Yellow 自主继续；Red 才升级。

## 6. WP-A-T1｜Spatial Authority
验证：
- Player/Enemy 同坐标可共存；
- 同 Board 重叠被拒；
- `GetUnitAt/IsOccupied` 不产生 side 歧义；
- Move 只检查所属 Board；
- Swap 只允许同 Board；
- Position 与 Occupancy 一致；
- 失败零写入。

## 7. WP-A-T2｜Deploy + Incapacitated Removal
- Enemy spawn 进入 Enemy Board；
- Character/Hero 进入 Player Board；
- 0HP 释放所属 Board 逻辑格；
- 不影响另一 Board 同坐标单位；
- 下一战无残留。

Board 技术上有 9 个逻辑位置，但本 WP 不冻结 Encounter 最大敌人数，也不实现 >9 替补。

## 8. WP-A-T3｜Range + Adjacency

Range 至少覆盖：
- Player Front → Enemy Front；
- Player Back → Enemy Back；
- 双方中间排阻挡；
- 空排不阻挡；
- 同排多单位只计一次；
- RangeModifier；
- Enemy target 与 Player card reach 同规则。

Adjacency 至少覆盖：
- 同 Board 正常邻接；
- 跨 Board 永不邻接；
- Center 只看本 Board；
- 失能单位不阻挡。

## 9. WP-A-T4｜Gate 0 Spatial Integration
重点：
- `BattleRuleContextRuntime.GetAlliedCells`
- `EmptyAllyCells`
- AllyCell manual target
- `TryMoveOwnerTo`
- `TrySwapOwnerWith`
- 《补位》
- 《换岗》

必须证明：Enemy 同坐标不会封锁 Player 空格。

Gate 0 纯规则层无需修改时禁止顺手重构。

## 10. WP-A-T5｜Product Integration
必须从真实产品 Battle 入口验证：
1. 正式进入 Battle；
2. 双 Board 同坐标单位同时存在；
3. Shared Hand / AP 正常；
4. Damage 正常；
5. Intent 正常；
6. Range 正常；
7. 《补位》《换岗》正常；
8. 0HP 后格释放；
9. 新单位可进入释放格；
10. 对面 Board 同坐标不受影响。

Harness 不替代产品集成。

## 11. Regression Tests
长期最小契约建议：
- `DualBoard_SameCoordinate_CanCoexist`
- `SameBoard_OverlapRejected`
- `Move_IgnoresOpposingBoardOccupancy`
- `Swap_RequiresSameBoard`
- `DownedUnit_ReleasesBoardCell`
- `Adjacency_DoesNotCrossBoards`
- `CenterBlocking_UsesSameBoardOnly`
- `Range_UsesVirtualSixRowProjection`
- `BlockingRows_CurrentRulePreserved`
- `EnemyIntent_RangeStillValid`
- `FillGap_UsesPlayerBoardEmptyCells`
- `SwapPost_UsesSingleSpatialAuthority`

Legacy-assumption tests 不直接删，先按最新 Authority 重写或分类。

## 12. UI / Scene
现有 UI 已有 player/enemy 两套 Row 容器，本 WP 不重做 Battle UI。
仅允许为双 Board 正确显示做最小适配：cell marker、Move target、Reposition、Debug label。

需要 Scene/Prefab 修改时必须使用 Unity MCP/Editor；不可直接编辑 YAML。

## 13. Hard Stop
以下属 Red：
- 当前仓库 Current Authority 与本 Spec 出现无法通过最新制作人裁决解释的冲突；
- 必须修改 Range 玩家规则；
- 必须改变 Board 尺寸；
- 必须新增 Enemy 行动系统；
- 必须改 Save Schema/Version；
- 必须同时执行 Item/TP 大清理；
- 必须重做 BattleUI；
- 必须建立第二套 Spatial Authority。

## 14. Verification
`.cs` 修改后至少：
- Asset Refresh；
- 编译；
- Console；
- 定向 EditMode；
- 定向 PlayMode；
- Product Integration。

全量测试串行。Disposable Tests by Default。

## 15. Git
本 Spec 不授权 Commit / Push。默认完成后停在 `Ready for Commit / Push`。

## 16. Technical PASS
只有全部满足才可写 `WP-A Dual-Board Spatial Authority Technical Verified`：
- 双 Board 独立 Occupancy；
- 同坐标可共存；
- 同 Board 不重叠；
- Move/Swap 正确；
- 0HP 释放格；
- Adjacency 不跨 Board；
- Range Current Rule 保持；
- Intent 未破坏；
- Worker 空间牌未破坏；
- Shared Deck/AP 未破坏；
- Product Integration PASS；
- 无第二套 Spatial Authority。

## 17. 当前状态

> **Development-time Binding / Ready for Execution**

启动开发前仍必须：

1. 核对 Local HEAD / branch / worktree；
2. 确认仓库已包含 Authority 基线 `13a6643` 及本 Spec 的独立提交；
3. 回读 `GameSpec/Battle/Battle-GameSpec-V0.1.md` R5.0 / R5.1 / R5.3.1–R5.3.3 / R6 / R15.4；
4. 做 Existing Host Map；
5. 若是 Green / Yellow，直接执行 WP-A-T1 → T5，不逐阶段等待用户确认；
6. 遇到 Red 才进入 `Awaiting Decision`。

本 WP 完成后只可声明：

> `WP-A Dual-Board Spatial Authority Technical Verified`

不得据此宣称：

- Battle UI 已完成；
- Battle 产品体验已验证；
- Legacy Item/TP 已清理；
- Return Integrity 已修复；
- Enemy 正式内容已冻结。
