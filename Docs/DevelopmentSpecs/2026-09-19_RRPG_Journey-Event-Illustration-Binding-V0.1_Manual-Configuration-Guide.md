# RRPG｜Journey Event Illustration Binding V0.1｜Manual Configuration Guide（事件插图配置手册）

> 状态：**Implementation-only / Not Frozen**（可替换的生产接线基础，不是最终 Event Content Authority）
> 适用范围（2026-10-06核对）：Legacy / Compatibility 的 Battle前卡与Branch类别fallback；正式统一Event不使用此Catalog作为内容或插图Authority。
> 最后更新：2026-09-19

---

## 当前操作分流

正式Event按EventId读取RRPG_Events.xlsx / EventPresentation.IllustrationAssetId，引用RRPG_VisualAssets.xlsx，源图经Compiler→Generated→Builder→Registry。制作入口见[配置操作手册](../../Development/Config/README.md)。不要为正式Event在本Catalog重复建NodeId插图。

以下保留2026-09-19接线手册，仅在对应Legacy / Compatibility消费者内适用；其“未来Event系统”是当时语境，统一Event与Encounter现已实施，当前状态见[技术总览](../Architecture/技术架构总览.md)。

## 1. 这套东西是什么

**一句话**：以后你想让某个具体事件 / 遭遇用**自己的插图**，只需要「导入 1 张 PNG + 在**一个** Inspector 资产里新增 1 行 + 填键 + 拖图」。

**它做什么**：

- 把「事件卡用哪张插图」集中到一处配置（`JourneyEventPresentationCatalog`）；
- 让**具体事件 / 遭遇 → 独立插图**成为可能（不再是同类事件共用一张图）；
- 你**不需要**每次去 Scene 修改 `ExpeditionJourneyRoot` 的三个类别 Sprite 字段。

**它不是什么（重要）**：

- **不是 EventId / EncounterId 的来源**：本版本的表现键只是**当前节点的 NodeId**（临时实现级来源），
  不冻结 `NodeId == EventId`，也不建立 Encounter Pool / Event Definition；
- **不是事件内容 Authority**：这份表只有「键 + 图」两个字段，**不含** Title / Body / Options / 奖励 / 条件 / 概率 / 文案；
- **不是最终方案**：未来可以由 `Excel Event Table → EventDefinition / EncounterDefinition → PresentationKey / Illustration`
  提供同一张表，而**不需要**重做 `JourneyEventCardConfig` / `JourneyEventCardView`；
- 它不决定任何玩法：不改合法节点、不改推进、不改补给 / 天数、不进入也不跳过战斗、不进存档。

---

## 2. 配置对象在哪里

| 项目 | 内容 |
|---|---|
| 资产路径 | `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/JourneyEventPresentationCatalog.asset` |
| 资产类型 | `JourneyEventPresentationCatalog`（轻量 ScriptableObject） |
| 怎么打开 | 在 Unity/Tuanjie 的 Project 窗口里双击该 `.asset`，右侧 Inspector 出现 `Entries` 列表 |
| 读取方式 | Runtime 通过固定 Resources 路径读取；**不需要**在 Scene / Prefab 里再挂引用 |
| 资产缺失时 | 全部事件卡走类别级 fallback 图（不报错、不阻断任何流程） |
| 资产不存在怎么办 | 菜单 `Tools/Journey Event Presentation Catalog（创建/检查）` 可幂等创建（已存在时**不会**覆盖你的配置） |

**Legacy Catalog 唯一配置面（不包含正式Event XLSX）**：如果你在别处（Scene 组件、Prefab、其它 SO）又看到一份「事件 → 插图」的表，那是遗留物或 Bug，请报告。

---

## 3. 字段含义

`Entries` 每一条 = 一个 **PresentationKey** 的事件卡插图。

| 字段 | 必填 | 含义 |
|---|---|---|
| `PresentationKey` | ✅ | 表现键。**V0.1 填当前节点的稳定 NodeId**（见 §4）。留空 = 该行不生效（Console 会给诊断） |
| `Illustration` | ✅ | 该事件卡使用的插图 Sprite。留空 = 该行不生效，回退类别级 fallback（Console 会给诊断） |

注意：
- 同一个 `PresentationKey` 出现两次时，**只保留第一条**并在 Console 明确指出「生效行 / 被忽略行」；
- 空 `PresentationKey` 的行**永远不会**被解析命中，并会给出诊断（不会静默）；
- 本工具**不会**自动删除或改写你填的任何行。

---

## 4. PresentationKey 现在填什么

| 项目 | 内容 |
|---|---|
| V0.1 来源 | 当前 Journey 节点的**稳定 NodeId**（`JourneyNodeRef.NodeId`，来自当次合法后继快照） |
| 性质 | **Implementation-level 临时来源**。它只是「用哪张图」的键，**不是** EventId |
| 不冻结什么 | 不冻结 `NodeId == EventId`；不建立 Encounter Pool / Event Definition |
| 未来怎么换 | 正式 Event / Encounter Content 系统建立后，把「谁提供 PresentationKey」这一层换成 `EventDefinitionId / EncounterDefinitionId`；Catalog 与 `JourneyEventCardView` **不需要重做** |

### 4.1 制作人怎么拿到一个可填写的 PresentationKey（不需要任何新工具）

**方法 A（推荐，永远有效）：从 Console 诊断里读**

1. Play 一次远征，正常走到出现事件卡；
2. 打开 Console，找这一行（**未配置**的键都会打印，同一个键只打印一次）：

   `[JourneyEventIllustration] PresentationKey=node_0 没有配置行 → 使用类别级 fallback 图（kind=Battle）。`

3. 把 `PresentationKey=` 后面那段字符串**原样**填进 Catalog 的 `PresentationKey`；
4. 再次 Play，同一张卡就会用你拖入的插图。

> 为什么靠这个方法：Journey 卡面当前**不显示** NodeId，也不显示节点类型名（玩家文案未冻结）；
> 节点类型的中文名只存在于 Legacy 地图节点标签（战斗 / 精英 / Boss / 事件 / 商店 / 休息 / 招募），
> 而产品运行时地图已隐藏。因此 **Console 的这一行就是「你刚看到的那张卡用哪个键」的权威答案**。

**方法 B（结构已知，可直接填，用于快速试第一张卡）**

当前产品图由 Legacy 4×3 布局生成，NodeId 固定为 `node_0` … `node_10`（共 11 个）：

| NodeId | 固定性 | 玩家可见含义 | 适合测试 |
|---|---|---|---|
| `node_0` | **固定**（入口节点） | 一局中的**第一张卡**：起点节点的战斗卡（Title「遭遇战斗」，Option「进入战斗」） | ✅ **最推荐**：每次开一局必出现 |
| `node_10` | **固定**（Boss） | 路线终点的战斗卡（Title「遭遇战斗」） | 需要走完整条路线 |
| `node_1` / `node_2` / `node_3` | 位置固定、**类型每局随机** | 第一层三条岔路：走过去之后那一张卡（战斗系 → Battle 卡；事件 / 商店 / 招募 → Content 卡） | 先选一条路，再看 Console 确认键 |

> `node_1` … `node_9` 的类型每局随机，但**都会出一张卡**；不确定时以方法 A 的 Console 行为准。


---

## 5. Fallback 解析顺序

```text
具体 PresentationKey 的 Illustration
  → 没有 → 当前类别级 fallback：
             Battle  → ExpeditionJourneyView.battleEncounterIllustration
             Content → ExpeditionJourneyView.contentEncounterIllustration
             Branch  → ExpeditionJourneyView.branchIllustration
  → 也没有 → Illustration Host 隐藏；卡面 Title / Body / Options 继续正常工作（不阻断流程）
```

Scene 上原有的三个类别 Sprite 字段因此**保留**，但已降级为 **fallback / placeholder**。

---

## 6. Branch 边界（本版本不做逐 Branch 插图）

Branch 卡当前代表「**路线选择表面**」，而不是正式 Event Content：它没有稳定、唯一、合理的逐 Branch 表现键。

- 因此本版本**不伪造** EventId，Branch 卡继续使用 `branchIllustration` 类别 fallback；
- 也不重构 Branch / Progression；
- **什么时候才能进入逐 Branch 插图配置**：当 Branch 拥有正式、稳定、唯一的 Event / Encounter Content 键
  （即 EventDefinitionId / EncounterDefinitionId 冻结）之后 —— 那时只需把 Branch 卡面的解析调用
  从「类别 fallback」换成「具体键」即可，Catalog 与 View 不需要重做。

---

## 7. 配置安全与诊断（Console）

| 情况 | 行为 | Console |
|---|---|---|
| 空 `PresentationKey`（解析侧传入空键） | 不参与解析、不猜、不随机 → 类别 fallback | `PresentationKey 为空：…`（同一会话去重） |
| `Entries` 行 `PresentationKey` 为空 | 该行被忽略（永不命中） | `存在无效配置行：PresentationKey 为空（行 N）` |
| 重复 `PresentationKey` | 确定性使用第一条 | `存在重复 PresentationKey：X（生效：行 0；已忽略：行 1）` |
| 键未配置 / 行没填图 | 类别 fallback | `PresentationKey=X 没有配置行` / `… 的配置行未绑定 Illustration`（每键去重） |
| 类别图也为空 | 隐藏 Illustration Host | `类别 X 既没有具体 PresentationKey 的插图，也没有类别级 fallback 图` |
| Catalog 资产缺失 | 全部走类别 fallback | `未找到配置资产 Resources/ScriptableObjects/Presentation/JourneyEventPresentationCatalog.asset` |

---

## 8. 制作人 6 步流程

1. 把插图（PNG）导入项目，Texture Type 设为 **Sprite (2D and UI)**；
2. 打开唯一 Catalog：`Assets/Resources/ScriptableObjects/Presentation/JourneyEventPresentationCatalog.asset`；
3. `Entries` **+1**；
4. `PresentationKey` 填**当前节点的稳定 NodeId**（怎么拿：见 §4.1；第一次试可以直接填 `node_0`）；
5. 把该 Sprite 拖到 `Illustration`；
6. Play 一次该事件 → 对应 Event Card 自动使用该图（**不需要**改 Scene）。

---

## 9. 本版本明确不包含

正式 Event System、Excel 导表、EventId 正式冻结、Encounter Pool、Event 奖励 / 条件 / 概率、
正式文案配置、Option Gameplay、Event Card UI 重排、Journey Background、Battle、Save。
