# AI_CONTEXT.md（RRPG Knowledge Governance Update v0.1）

> 本文件用于 AI 接手 RRPG 项目时建立正确理解。 本文件不是规则
> Authority，不替代 GameSpec、GameDesign、Development 文档。

# RRPG Core Understanding

在修改设计、架构或实现前，必须理解以下原则。

## 1. Person 是长期价值对象

RRPG 的长期价值对象是具体人物（Person），而不是简单可替换的战斗单位。

Person 承载：

-   身份；
-   经历；
-   职业形成；
-   长期变化。

注意：

这不否定战斗层中的：

-   BattleUnit；
-   Card；
-   Profession Card Pool。

不同层承担不同职责：

-   Person：长期世界身份；
-   BattleUnit：战斗表现；
-   Card：Run 构筑对象。

------------------------------------------------------------------------

## 2. Village 是共同体空间

Village 不应被简化为：

-   资源生产机器；
-   建筑升级菜单。

Village 的核心价值：

-   人物生活空间；
-   共同体变化空间；
-   长期关系承载空间。

------------------------------------------------------------------------

## 3. Expedition Graph ≠ Map UI

Expedition Graph 是推进规则结构。

负责：

-   Node 关系；
-   Progression 状态；
-   分支合法性。

它不等同于：

-   地图视觉；
-   UI 表现；
-   世界地图。

Graph 与 View 必须保持分离。

------------------------------------------------------------------------

## 4. Battle Outcome ≠ Expedition Completion

一次 Battle 结果代表当前内容节点结果。

不自动代表：

-   整个 Expedition 结束；
-   Run 结束。

Battle Outcome 需要经过对应领域流程转化为 Progression Resolution。

------------------------------------------------------------------------

## 5. Domain Authority 优先于 Presentation

判断优先级：

Domain Authority \> Runtime State \> Presentation/View

UI、地图、美术表现不能拥有核心规则决定权。

------------------------------------------------------------------------

## 6. 修改前确认 Authority

任何修改前：

1.  查当前设计 Authority；
2.  查对应 GameSpec；
3.  查当前阶段范围；
4.  查已拒绝或 Legacy 方案。

不要根据旧代码表现反推当前设计。
