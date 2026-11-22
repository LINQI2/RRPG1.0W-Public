# RRPG 美术批量导入与 Art Import Skill 规划 V0.1

> **状态声明（2026-09-20）：SUPERSEDED / DO NOT IMPLEMENT。**
>
> - 本规划已于 **2026-09-20** 被新的 **RRPG Configuration Architecture Replan** 取代；
> - 原状态 `PLANNING / NOT IMPLEMENTED` 不再有效；
> - 本文件正文作为**历史规划记录原样保留**，不重写、不改写为新方案、不追认为新方向；
> - 本文件**不再作为当前开发输入**，不得据本文件启动任何实现任务；
> - 本文件不改变 Current Gameplay / Architecture Authority。

---

> **退役范围（2026-09-20）：以下内容不得继续作为当前开发输入。**
>
> 1. CSV 作为长期制作人 Authoring Authority；
> 2. `PersonVisual.csv` / `JourneyEventPresentation.csv` 作为正式生产入口；
> 3. `Incoming` 作为长期 Source Art Authority；
> 4. 「空白 = 保持旧值」的增量 Patch 语义（见本文件原 §9）；
> 5. `@CLEAR` 作为长期完整配置体系语义（见本文件原 §9）；
> 6. CSV + Catalog 混合人工 Authority；
> 7. 在该 CSV Pipeline 上继续扩 Enemy Visual / Battle Card Art（见本文件原 §25）；
> 8. 旧文件「下一开发任务 = RRPG Art Batch Import Pipeline V0.1」的结论（见本文件原 §27）。
>
> 上述条目只在“历史规划记录”语义下保留，不再具备 Current Direction 地位。

---

> **旧 CSV Art Batch Import 实验事实（2026-09-20）：**
>
> - 未 Commit；
> - 未 Push；
> - 未进入 Product History；
> - 已仓库外归档；
> - 其经过真实验证的工程技术经验仍可供新架构参考（见替代 Checkpoint §5），但 **不拥有新架构 Authority**。

---

> **替代 Checkpoint：**
> `Docs/DesignCheckpoints/2026-09-20_RRPG_Configuration-Architecture_Replan-Checkpoint-V0.1.md`

---

> **以下为 2026-09-20 历史规划正文，保持历史语义，不再更新。**
> 正文中出现的 Authority 声明、目录建议、错误等级与“下一步”结论均以当时语境为准，不得在缺少新裁决的情况下恢复引用。

---

> 日期：2026-09-20  
> 定位：Production Pipeline Planning Note（Historical Record）  
> 状态：SUPERSEDED / DO NOT IMPLEMENT  
> 重要（历史声明，已失效）：本文件记录已讨论并认可的生产方向，不代表功能已经实现，不改变 Current Gameplay / Architecture Authority。

## 1. 背景

RRPG 当前已经完成两条可实际使用的表现绑定基础：

1. Person Visual Binding  
   `AppearanceKey → PersonVisualBindingCatalog → Sprite`

2. Journey Event Illustration Binding  
   `PresentationKey → JourneyEventPresentationCatalog → Illustration`

制作人已完成手工素材导入 / Catalog 配置验证。

下一阶段目标：

> 当人物、事件插图数量增加后，不再依赖制作人在 Inspector 中逐条添加 Element、拖 Sprite。

目标生产链：

`Excel / CSV 配置 + Incoming 素材箱 + AI/Cline 执行 + Unity Editor Importer 自动导入 / 校验 / 绑定`

---

## 2. 已有项目基础

仓库已存在：

`Development/Excel/*.csv`

以及：

`Assets/Editor/ExcelImporter.cs`

当前已有 CSV → ScriptableObject 导入能力，包括：

- Profession
- Person
- Character
- Skill
- Status
- Monster
- ItemCard

因此新美术导入管线应复用现有 CSV / Editor Import 思路，而不是建立完全独立的数据体系。

---

## 3. 正式方案选择

### 3.1 配置表是 Binding Authority

正式主流程采用：

> CSV / Excel 显式配置“哪个 Key 对应哪张图片”。

不采用：

> 仅通过文件名自动猜绑定关系。

原因：

- 更直观；
- 更稳定；
- 更容易审计；
- 更容易批量管理；
- 允许素材文件名与内部 Key 解耦；
- 更适合 AI 辅助维护。

文件名自动匹配可以保留为辅助检查，但不拥有正式 Binding Authority。

---

## 4. V0.1 范围

第一版只覆盖：

### 4.1 Person Visual
输入：
`PersonVisual.csv`

输出：
`PersonVisualBindingCatalog`

### 4.2 Journey Event Illustration
输入：
`JourneyEventPresentation.csv`

输出：
`JourneyEventPresentationCatalog`

---

## 5. V0.1 明确不做

不做：

- Enemy Visual
- Battle Card Art
- Status Icon 批量绑定
- Biome Environment
- Battle Environment 多背景系统
- 正式 Event Content
- Event Title / Body / Options 导表
- Event Reward / Condition / Probability
- 正式 EventId 冻结
- Addressables
- Unity 6 Agent Plugin
- Unity CLI / com.unity.pipeline / eval
- AI 图像识别猜人物 / 事件身份

---

## 6. 建议目录

```text
Development/
├─ ArtImport/
│  └─ Incoming/
│     ├─ Person/
│     └─ JourneyEvent/
│
└─ Excel/
   ├─ PersonVisual.csv
   └─ JourneyEventPresentation.csv
```

Incoming 位于 Assets 外。

目的：

- Unity 不会提前为所有原始图生成 `.meta`；
- Importer 可以先 Dry Run；
- 合法素材再进入正式 Assets。

---

## 7. PersonVisual.csv

建议字段：

```text
AppearanceKey,
DefaultBodyImage,
JourneyImage,
BattleImage,
PrepImage,
HeroSelectImage
```

示例：

```csv
AppearanceKey,DefaultBodyImage,JourneyImage,BattleImage,PrepImage,HeroSelectImage
person_su_wan,苏婉_第一版.png,,,,
person_lin_en,林恩.png,,林恩_战斗.png,,
hero_001,青鹿.png,,青鹿_战斗.png,青鹿_头像.png
```

---

## 8. JourneyEventPresentation.csv

建议字段：

```text
PresentationKey,
IllustrationImage
```

示例：

```csv
PresentationKey,IllustrationImage
node_0,狼群袭击.png
node_1,废弃小屋.png
node_3,林间岔路.png
```

当前 PresentationKey 可暂时使用稳定 NodeId。

重要：

> 不冻结 `NodeId == EventId`。

未来正式 EventDefinition / EncounterDefinition 出现后，只替换 Key 来源。

---

## 9. 空白 / 清空语义

### 空白
表示：

> 保持当前值，不修改。

Importer 不得因为 CSV 某格为空就清除已有绑定。

### @CLEAR
表示：

> 制作人明确要求清空该字段。

例如：

```csv
AppearanceKey,BattleImage
person_su_wan,@CLEAR
```

表示清除苏婉的 Battle Sprite Override。

---

## 10. 人工 Scale / Offset 保护

V0.1 CSV 默认不包含 Scale / Offset。

Importer 只更新 Sprite 引用，不得重置：

- Journey Scale
- Journey Offset
- Battle Scale
- Battle Offset
- Prep Scale
- Prep Offset
- 其它人工视觉调参

未来如需表驱动这些参数，应通过独立明确列实现，不能隐式覆盖。

---

## 11. 图片正式落点

建议：

```text
Assets/Art/Imported/Person/
Assets/Art/Imported/JourneyEvent/
```

Importer 从 Incoming Copy 到正式 Assets。

V0.1 不自动删除 Incoming 源文件。

---

## 12. Sprite Import Settings

V0.1 普通单图使用当前引擎兼容 Editor API：

- `TextureImporter`
- `AssetDatabase`

自动设置：

```text
Texture Type = Sprite (2D and UI)
Sprite Mode = Single
```

不依赖 Unity 6+ Agent Plugin 的 sprite-editor 执行链。

---

## 13. GUID / .meta 保护

更新已经存在的正式图片时：

> 不得 DeleteAsset 后重新创建。

原则：

- 尽量保留原 `.meta`
- 保留 GUID
- 更新 PNG 内容后 Reimport

避免已有 Prefab / Catalog Sprite 引用断裂。

---

## 14. Dry Run

V0.1 必须支持：

> Dry Run = 只检查，不修改。

示例报告：

```text
准备新增 Person：12
准备更新 Person：3
准备新增 Event Illustration：8

Hard Error：
person_xxx → 找不到 xxx.png
node_3 → PresentationKey 重复

Warning：
Incoming/test.png 未被任何配置引用

不会修改：
Scale / Offset / Gameplay
```

存在 Hard Error 时：

> 禁止 Apply。

---

## 15. Apply Import

Dry Run 通过后才允许正式 Apply。

Apply 负责：

1. 读取 CSV；
2. 检查 Incoming；
3. Copy / Update 正式 Assets；
4. 设置 Sprite Import Settings；
5. Reimport；
6. 加载 Sprite；
7. 更新既有 Catalog；
8. 保存 Catalog；
9. 输出 Import Report；
10. Refresh / Compile / 验证。

---

## 16. 错误等级

### Hard Error
禁止导入：

- Key 为空
- CSV Key 重复
- 配置图片但找不到文件
- 同一目标字段出现歧义
- AppearanceKey 无法对应合法目标
- 目标路径冲突

### Warning
允许继续：

- Incoming 有图片但 CSV 未使用
- 只配置 Default、未配置 Override
- 当前 Event PresentationKey 尚未形成正式 EventId

### Info
记录：

- 新增数量
- 更新数量
- 保持数量
- 空白字段跳过数量
- @CLEAR 数量

---

## 17. Import Report

建议输出：

`Development/ArtImport/LastImportReport.txt`

示例：

```text
========== RRPG Art Import ==========
新增 Person Binding: 6
更新 Person Binding: 3
新增 Event Illustration: 12

跳过:
2

错误:
person_xxx → 找不到图片
node_7 → Key 重复

未使用:
test.png
old_suwan.png

未修改:
Scale / Offset / Gameplay
====================================
```

供制作人、GPT 开发顾问、Cline共同审计。

---

## 18. AI / Cline 的职责

未来制作人操作：

1. 将图片放入 Incoming；
2. 修改 CSV；
3. 告诉 AI：“这批素材已经准备好，帮我导入。”

Cline：

1. 读取 CSV；
2. 检查 Incoming；
3. 执行 Dry Run；
4. 读取报告；
5. 无 Hard Error 后 Apply；
6. 回读 Catalog；
7. 编译 / 测试；
8. 输出导入报告。

制作人不再逐条：

> Catalog Size +1 → Element → 拖 Sprite。

---

## 19. MCP 定位

Unity MCP 适合：

- 调用 Unity
- Refresh
- 查看 Console
- 运行测试
- 读取 / 验证 Scene / Catalog
- 截图验证

MCP 不应承担：

> 每张图片逐项手工绑定的核心生产规则。

核心绑定规则应在确定性 Importer 中。

---

## 20. Unity Official Skills 判断

### 可继续使用
当前独立 Skills：

- `ui`
- `ui-ugui`

适合作为 Cline 的通用 Unity / uGUI 工程知识。

### 当前不直接执行
Unity 6+ Agent Plugin 中的：

- sprite-editor Skill
- Unity CLI
- com.unity.pipeline
- eval

当前项目为：

> 团结引擎 1.9.1 / Unity 2022.3.62 兼容体系

不能因为官方 Skill 是新方案就直接替换当前工具链。

---

## 21. Sprite Skill 的未来价值

官方 sprite-editor 类 Skill 更适合：

- Sprite Sheet 切片
- Pivot
- Border
- Sprite Rect
- Outline
- 多 Sprite slicing

当前 Person / Event：

> 一张 PNG = 一个 Sprite

无需该复杂链路。

未来如需要 Sprite Sheet，可以参考官方 Skill 工程经验，并使用 2022.3 / 团结引擎实际可用 API 编写项目兼容版流程。

---

## 22. RRPG 自定义 Art Import Skill

Art Batch Import Pipeline 稳定后，计划提炼：

```text
.cline/skills/rrpg-art-import/SKILL.md
```

定位：

> RRPG 项目本地美术生产 SOP。

建议未来 Skill 包含：

1. 先读 Current Art Import 规则；
2. CSV 是 Binding Authority；
3. 不根据图像内容猜身份；
4. 不以文件名作为唯一正式绑定依据；
5. 先 Dry Run；
6. Hard Error 禁止 Apply；
7. 更新 PNG 必须保护 `.meta / GUID`；
8. 不重置人工 Scale / Offset；
9. 使用当前引擎兼容 `TextureImporter / AssetDatabase`；
10. 导入后验证 Catalog / Console / Tests；
11. 不越权修改 Gameplay / Save / Runtime Authority。

---

## 23. Skill 的正确建设顺序

采用：

```text
先实现 Pipeline
↓
真实素材批次验证
↓
流程稳定
↓
总结 SOP
↓
写 rrpg-art-import Skill
```

不采用：

```text
先写 Skill
↓
再猜执行工具是否兼容
```

---

## 24. 正式 Event Excel 的后续前置

未来制作人希望在 Excel 中配置：

- EventId
- 标题
- 正文
- 插图
- 选项
- 条件
- 结果
- 奖励

在正式实现前仍需设计：

1. EventId Authority
2. Event 类型
3. Option 数据结构
4. Result 类型
5. Option 数量规则
6. Event → Battle / Reward / Recruit 等引用
7. IllustrationKey 最终归属

因此 JourneyEventPresentation.csv 当前只负责 Illustration Binding。

不得因为 Art Import Pipeline 顺手冻结正式 Event Content Schema。

---

## 25. 后续扩展顺序

当前：

1. Person Visual ✅
2. Journey Event Illustration ✅

下一批适合扩展：

3. Enemy Visual Binding
4. Battle Card Art Binding

更后：

5. Status Icon
6. Biome Environment
7. Battle Environment
8. 正式 Event Content Excel

每类内容先有稳定 Binding / ID Authority，再进入批量导入。

---

## 26. 当前决策摘要

已确认：

- 配置表驱动是正式方案；
- 文件名自动匹配仅辅助；
- 复用当前 CSV / Editor Import 基础；
- V0.1 先 Person + Journey Event；
- Incoming 位于 Assets 外；
- 空白 = 保持；
- `@CLEAR` = 明确清除；
- 必须 Dry Run；
- Missing / Duplicate = Hard Error；
- Unused Asset = Warning；
- 更新 Sprite 不重置 Scale / Offset；
- Copy，不删除源素材；
- 保护 `.meta / GUID`；
- 输出 Import Report；
- Cline / AI 负责批量执行与验证；
- 不引入 Addressables；
- 不直接依赖 Unity 6 Agent Plugin；
- Pipeline 验证后再写 RRPG Art Import Skill。

---

## 27. 当前下一步

下一开发任务：

> RRPG Art Batch Import Pipeline V0.1

T0 应先只读核对：

- 现有 `ExcelImporter.cs`
- PersonVisualBindingCatalog
- JourneyEventPresentationCatalog
- 当前 Editor API / 团结引擎兼容性
- 当前目录与 Git ignore
- 现有数据校验工具

确认无冲突后再实现。

本文件本身不代表功能已经开发完成。
