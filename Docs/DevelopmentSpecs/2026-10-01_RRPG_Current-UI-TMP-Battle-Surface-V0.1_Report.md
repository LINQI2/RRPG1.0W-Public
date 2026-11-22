# Current UI TMP Cutover + Battle Surface Binding Foundation V0.1

日期：2026-10-01。执行：Codex。复核：开发14.0。

开发14.0复核通过；2026-10-02 功能／资源 Checkpoint 已 Push main（ba1e7365c5e9ca5942f0ce65574d2d26e0073f2a）。原始开发事实及本次 Closeout 补充分别记录。不是 Gameplay 重构、UI 重设计或 Combat Feedback。本文记录 Implementation-only V0.1，不冻结终局内容架构。

## A. TMP Migration Audit

| 范围 | Before Current Text | Migrated | Remaining Current Text |
|---|---:|---:|---:|
| SampleScene 的 Current 根对象 | 113 | 113 | 0 |
| 七个 Current Prefab 定义 | 27 | 27 | 0 |
| 合计 | 140 | 140 | 0 |

统计包含隐藏宿主；Scene 宿主与 Prefab 定义分别计数，不是运行时实例去重统计。Scene 的 6 个 UnitCard Text 随 Prefab 迁移，其余 107 个 Scene Text 原位迁移。ItemCard 被 DeckViewer 当前产品路径引用，复核后纳入 Current，3 个 Text 均已迁移。

Current Prefab：BattleCard、UnitCard、CharCard、HeroSelectCard、JourneyPartyMemberView、CharacterGrowthUI、ItemCard。

项目自有正式 Scene 为 SampleScene。LinePrefab 没有文字组件。`Assets/TextMesh Pro/Examples & Extras/**` 是非产品包样例（LEGACY / DEAD / sample-only），其示例 Scene / Prefab / Scripts 不纳入 Current 产品迁移；Package / Library 也不作为 Current Runtime 代码。

显式 Legacy 白名单：MapUI / ShopUI / RecruitUI 的 Scene 根、对应三个脚本，以及 NodePrefab / shopItemPrefab / RecruitItemPrefab。SampleScene 白名单内剩余 3 个 Legacy Text。Map 只保留 Compatibility Graph Source；Shop / Recruit 不是当前正式产品入口。不得把 ItemCard 因名称或旧数据语义误归入 Legacy。

分类：MainMenu、Town、Expedition、Journey、Event、Prep、Battle、Reward、Growth、DeckViewer、Village HUD / Modal 为 CURRENT；Battle InfoText 与 flags 摘要为 DEBUG CURRENT；白名单为 LEGACY / DEAD 或 Compatibility；Tests 为 TEST ONLY；六个 Current Authoring 脚本为 EDITOR ONLY，改为创建 TMP，避免重新生成旧组件。逐消费者扫描清单见本文附录。

Current Presenter 使用 TMP_Text；Canvas 创建使用 TextMeshProUGUI。动态文本同样走字体 Authority。治理测试检查 Current Scene / Prefab 无 Text，并扫描非白名单 Runtime 禁止 AddComponent<Text> / typeof(Text)。从本轮起，Current Runtime UI 禁止新增 UnityEngine.UI.Text；白名单不能扩展为一般兜底。

## B. Font Authority

唯一入口：Assets/Resources/ScriptableObjects/Presentation/CurrentUIFontAuthority.asset → font。
制作人官方来源 Source Han Sans CN 2.005 Regular / Medium OTF 已复制到 Assets/Art/Fonts/SourceHanSansCN/，保留原文件及 LICENSE；未安装系统字体、未重新下载。
默认 RRPG_SourceHanSansCN_Regular_TMP；可选 RRPG_SourceHanSansCN_Medium_TMP；两者 Dynamic + Multi Atlas。静态 CurrentUIFontBinding 与动态 CurrentUIFontAuthority.Default 共用此入口，无需逐个覆盖 Current TMP。
中文人物名、卡名、多行说明、Town / Expedition / Event 及 Rich Text / keyword link 已实机验收；CJK 定向 EditMode 12/12 + PlayMode 4/4 PASS（字体接入阶段）。原字体 Pending 已解除，少量字体度量差异留制作人手调。

## C. Changed Files 与视觉保护

开工核对 git status / diff / HEAD / origin/main；main 与 origin/main 均为 `0f0de37d2670b2fdbc25c417f38cf8ece6afe7ac`。开工现场为 dirty。

仓库外证据：`1PUBLIC_USER/.codex/rrpg-evidence/2026-10-01-tmp-cutover`。包含开工 producer-visual.patch、原 Scene / BattleCard / UnitCard / PersonVisualCatalog 副本、迁移前后 Hierarchy / RectTransform / Component / Reference snapshots、任务增量 diff、测试 XML 与 Game View 图片。

Protected User Visual State：SampleScene、BattleCard、UnitCard 原有制作人改动保留；本任务仅通过 Editor API 替换文字组件、重绑定 Presenter、增加字体绑定、原 Owner 宿主下增加 Image_OwnerPortrait、隐藏正式表面的诊断文本。没有手改 Scene / Prefab YAML。没有 reset / checkout / restore / stash。原 GameObject 名称、父子关系、Sibling Index、激活状态及 Raycast 语义保留，InfoText 默认隐藏是明确任务变更。

332 个原宿主比较：Battle 原有 RectTransform、所有原 Image 的 Sprite / Color / Type / Raycast 未改变。差异限 VillageCommitment / VillageSocialEvent 已有 LayoutGroup / ContentSizeFitter 的驱动值，以及 Battle InfoText active=false；驱动组件证据在 probe.txt。没有关闭 LayoutGroup 或重排用户 Battle 布局来追求逐字度量一致。新增 Portrait 是原 Owner Rect 下的子宿主。

制作人的 Art/Battle、Art/Characters、Art/UI、原导入图片及 meta 未修改；PersonVisualBindingCatalog 原制作人配置保留。CardPortrait 新字段可选，旧配置使用 DefaultBodySprite。

This Task：Current UI / Village UI 类型迁移；Battle Card / HUD / Intent 表现绑定；共享只读 Value Resolver；字体 / 卡面 / 关键词 Presentation 配置；必要长期测试及 TMP assembly references；Current Authoring 脚本迁移。LiberationSans SDF Fallback 的动态字形缓存及 Editor 重序列化属于本任务技术验证增量，不是中文字体方案。

Unrelated Dirty：`Development/Config/RRPG_Events.xlsx` 和开工已 dirty 的 `ProjectSettings/QualitySettings.asset`，没有执行修改。完整文件归属见附录与仓库外 task-files.json；保护文件的 user delta 和 task delta 分开保留，不能把总 git diff 全算成本任务。

## D. Battle Surface Binding

| Surface | 正式只读事实 | Writer / Inspector |
|---|---|---|
| AP | SharedActionPointPool.Current / Max | BattleV1Runtime.txtSharedActionPoint；sharedActionPointFormat=`{0}/{1}` |
| Turn | BattleV1Runtime.TurnNumber | BattleHudPresenter.turnHost / turnFormat=`{0}` |
| Draw | SharedDeck.DrawPileCount | drawPileHost / drawPileFormat=`{0}` |
| Discard | SharedDeck.DiscardPileCount | discardPileHost / discardPileFormat=`{0}` |
| Intent | BattleSystem.GetEnemyIntent(unit).ActionType | 原 Image_IntentIcon；Attack / Defend / Special Sprite Inspector 配置 |

AP 只有 Runtime 一个正式 Writer；HudPresenter 不重复写 AP。HUD 不再强制“行动力 / 抽牌堆 / 弃牌堆”前缀。Invalid Format 给诊断和 `<?>`，不修改事实。

当前 EnemyIntent 没有正式已结算数值事实，所以只显示类型图标，Intent TMP 保持空白，不制造伤害数字。失能或无 Intent 隐藏图标。旧字符串 SetIntentHost 仅为 Compatibility 测试入口，不由 Current Runtime 调用。Enemy AI / Range 不变。

**AP 美术 Pending**：ActionPointRoot/Image 引用的 `Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_43-6.png` 内烘焙了“3/5”。真实 TMP 已显示 Runtime 数值，例如 6/6；PNG 仍显示 3/5。此处不能宣称整个 AP 视觉已消除静态数字。制作人需要提供无数字的同位置底图；本任务按禁止修改图片内容的约束保留原图与布局。

## E. Card Presentation

Character Card：OwnerPersonId → exact Person → AppearanceKey → PersonVisualResolver(CardPortrait)。同 CharacterId 的两个人不会合并。可选 CardPortraitSpriteOverride；缺失则 DefaultBodySprite，再缺失走 placeholder。没有改变任何 Identity Authority。

Common Card：使用 BattleCardView.commonOwnerIcon（Inspector）。commonOwnerIcon 保持空；缺图回退既有 ownerPortraitPlaceholder（player_body_placeholder.png），不使用 card_art_placeholder 冒充 Common 视觉，不新增美术。

Art：TemplateId → BattleCardPresentationCatalog Entry.ArtSprite；缺项或缺 Sprite 回退 artPlaceholder，不阻断出牌。当前三项 bcard_break_poise / bcard_solo_hunt / bcard_finish_prey 的 ArtSprite 均为空，统一走 artPlaceholder；DescriptionTemplate 保留。Card Art = Content Pending；CardPortrait Override = Optional / Pending；Common Owner Icon = Art Pending。均为内容生产状态，不阻塞本轮 Foundation 关闭。

Description：一个 TMP 文本，Resolver 输出 RichDescription；支持动态事实与旧 Description fallback。Name / Cost 仍是独立字段。Owner 内部 person_xxx 不进入玩家表面；Owner 文本可用于诊断显示人名。Flags 默认禁用，仅诊断显示元数据。Kind 为可选小标签，保留制作人的原激活状态。Battle InfoText 默认隐藏，showDebugInfo 开关保留诊断能力。

## F. Dynamic Description Architecture

`BattleCardRuleProgram / Battle Facts → BattleRuleValueResolver → BattleCardPresentationResolver → 单一 TMP Rich Text`。

Executor.ResolveValue 是共享 Resolver 的兼容包装；Fixed / CasterAttack 只保留一套数值解析。Presentation 使用 TryResolve，无法取得事实时给 `<?>` 和去重诊断，不能静默用错误的 0。Executor 保留原无效事实回退 0 行为，规则结算语义不变。

有限 token：`{cost}` 为 ActualCost；`{owner.attack}` 为正式 owner 的当前 Attack；`{step:N.value}` 为 GetProgram(card) 的第 N 步 ValueSource / ValueParam；`{step:N.bonus}` 为该步配置的 BonusValueSource / BonusValueParam。没有任意表达式、反射调用、DSL 或运行时代码里的卡牌 ID 分支。

`bonus` 是条件收益的配置幅度，不是假定条件已经满足的最终伤害预测。初始模板把收益的条件写明；目标相关 Predicate、支付 AP 后条件与护盾减伤仍由 Gameplay 执行。V0.1 没有目标预测器。

View 绑定 Runtime facts / PlayerCollection providers，RefreshPresentation / LateUpdate 读取最新事实，文本未变化不重写。属性 Attack 8→10、ActualCost 改变可刷新。缺 Presentation template 则保留 BattleCardData.Description。

## G. Future Tooltip Contract

稳定 TMP Link ID：`keyword:<id>`，例如 `keyword:damage` / `keyword:shield` / `keyword:exhaust`。DescriptionTemplate 可直接写 `<link="keyword:shield">护盾</link>`；TMP 显示词语，ID 留在 linkInfo 中，不作为玩家文本显示。

BattleKeywordCatalog 保存 presentation-only KeywordId / DisplayName / Description，Definition.LinkId 生成稳定格式。没有 TooltipService、Hover、点击百科或 TMP_TextUtilities.FindIntersectingLink；后续 Tooltip 可以直接消费现有 Link ID。

## H. User Authoring Map

| 制作项目 | Inspector 入口 |
|---|---|
| 卡牌插图 | Resources/ScriptableObjects/Presentation/BattleCardPresentationCatalog.asset → entries → TemplateId / ArtSprite |
| 卡牌动态描述 | 同一 Entry.DescriptionTemplate；可混用有限 token、TMP `<b>` 与 `<link>` |
| 具体人物 Owner 头像 | PersonVisualBindingCatalog 的具体 AppearanceKey 行 → CardPortraitSpriteOverride；空则 DefaultBodySprite |
| 通用牌头像 | BattleCard.prefab → BattleCardView.commonOwnerIcon |
| 缺图占位 | BattleCardView.artPlaceholder / ownerPortraitPlaceholder |
| Keyword 解释 | BattleKeywordCatalog.asset → keywords；稳定 KeywordId 不随 DisplayName 修改 |
| TMP 字体 | CurrentUIFontAuthority.asset → font；提供授权中文源后配置 Dynamic TMP FontAsset |
| AP / Turn / Draw / Discard 文案 | BattleV1Runtime / BattleHudPresenter 对应 Format 字段 |
| Intent 图标 | BattleUI 的 intentAttackPlaceholder / intentDefendPlaceholder / intentSpecialPlaceholder |
| 调试信息 | BattleUI.showDebugInfo；BattleCardView.showDebugMetadata |

不要运行旧 Prototype Layout Authoring 来覆盖制作人当前场景。文字迁移不需要重建 BattleCanvas。

## I. Tests

新增长期 CurrentUIPresentationContractTests 与 exact-person CardPortrait 测试，定向 12 / 12 PASS：Fixed=6；CasterAttack=8→10；ActualCost；共享 Executor / Presentation value；无事实诊断；单 TMP rich link keyword:shield；Art 命中 / fallback；Common icon；legacy Description；View 写 TMP 且隐藏内部 ID / Flags；Current Scene / 七个 Prefab 治理；Current Runtime 动态 Text 禁令；Inspector 格式。

旧 Intent Play 测试改为验证图标和空数值宿主。Journey 插图 singleton 测试收紧为 EventPresentationCatalog 类型，避免把新增 BattleCardPresentationCatalog 误判为第二套 Event Catalog。

Village 两项 Play 测试原来依赖 0.05 秒一天的 wall-clock 等待，TMP 初次文字生成的长帧会跨过 MidProject 检查点。现通过真实 Town driver 逐日推进，仍验证面板不暂停模拟、Project 完成后 Assignment 恢复、保存加载中途状态一致；没有修改 VillageTime 或 Project Gameplay。

## J. Full Validation

Compile：0 Error。定向：12 / 12。全量 EditMode：1048 / 1048，0 failed、0 skipped。全量 PlayMode：88 / 88，0 failed、0 skipped。原基线为 EditMode 1036 / PlayMode 88；测试总数增加正常。

真实 SampleScene 产品链覆盖 MainMenu Continue → Town → Expedition → Journey → Battle Prep → StartBattle → Draw / Hand → Card Display。实际 Fixed 卡显示 4，实际 CasterAttack 卡显示 8→10、keyword:damage linkInfo 存在、exact Owner portrait 有 Sprite；最后恢复测试临时属性和原手牌绑定。继续通过真实手牌 Button / Unit 或 Cell Button 出牌，并通过 EndTurn Button 完成下一回合，验证 Discard / Turn 的真实 TMP 数值。

Game View：仓库外 product-battle-tmp-attack8.png / attack10.png。截图中文字方框属于 CJK Font Pending。没有声称制作人已完成中文排版或人工 Acceptance。

MCP 全量 tests_run 曾返回 No tests found，故本轮使用 live Editor TestRunnerApi 执行并保存真实 XML，不把工具返回替代测试证据。插件旧 callback 的 orphan MCP request 错误单独记录，不是 C# 编译错误。临时 Editor runner 在交付前通过 AssetDatabase 清理，源与证据留仓库外。

git diff --check 的 Scene / Prefab 空字段尾空格由 Editor 序列化产生；没有为了清理空格手改 YAML。源代码检查单独核对。

## K. Remaining / Pending

Card Art = Content Pending；CardPortrait Override = Optional / Pending；Common Owner Icon = Art Pending。均为内容生产状态，不阻塞本轮 Foundation 关闭。
Tooltip / Keyword explanation UI、正式大规模 Card Art 生产、高级字体排版微调、Reward 正式 Presentation 为后续范围。
下一确认节点 Battle Combat Feedback Foundation V0.1；本轮不实施，不优化美术。

## L. Final Closeout（2026-10-02）

- 用户批准 Current Village 日期／暂停／加速／卡册文字修复、Commitment 行复用防闪动及回归测试纳入 Current TMP Checkpoint。
- SampleScene 的未保存制作人现场已通过 EditorSceneManager.SaveScene 正常保存；保存前后 Hierarchy / Transform / active 快照完全一致。AP Circle_BG / Circle_Fill 引用制作人 2026年10月1日 17_13_18-2 / 17_13_17-1 无数字图片。磁盘差异包含原未保存现场和既有布局驱动值，没有重排。
- Catalog 三项 ArtSprite 清空；BattleCard.prefab commonOwnerIcon 清空，Prefab 本次净差异仅此字段，布局快照一致。commonOwnerIcon 保持空；缺图回退既有 ownerPortraitPlaceholder（player_body_placeholder.png），不使用 card_art_placeholder 冒充 Common 视觉，不新增美术。
- 此次最小 Runtime fallback 修正已明确批准；补充既有测试断言，未新增测试项。Compile 0 Error；Card Presentation 定向 12/12；全量 EditMode 1048/1048、PlayMode 89/89，failed=0 / skipped=0，均为最终修正后实跑。旧 J 节 1048/88 与字体缺字截图是初轮历史证据，不代表最终字体状态。
- CJK directed 12/12 + 4/4、中文实机及真实产品链到 Play Card → EndTurn 保留为已验收证据。原 Rule、AP、Deck、Identity 与 Enemy AI Authority 无变化。
- 原始 git diff --check 已执行：Unity 序列化空字段有尾随空格，CurrentUIFontAuthority.cs 新文件有一个末尾空行。没有手改 YAML 或在全量通过后格式化代码；命令级 core.whitespace=-blank-at-eol,-blank-at-eof 的 cached 检查 PASS，没有改全局 Git 设置。
- 功能／资源 commit：ba1e7365c5e9ca5942f0ce65574d2d26e0073f2a（160 files，+33510 / -3963），已 Push main；本报告等五份状态资料随后单独 docs commit，最终 SHA 看 Git。
- 临时 Editor Runner、XML、截图、日志与桥接工具仅留仓库外；测试执行前备份并恢复制作人本地存档。

Producer Visual Included：SampleScene.scene、BattleCard.prefab、UnitCard.prefab、PersonVisualBindingCatalog.asset，及下列正式引用图片和各自 meta／必要目录 meta：

- UnityProject/RRPG1.0game/Assets/Art/Battle/Background/战斗场景1（草地森林）.png
- UnityProject/RRPG1.0game/Assets/Art/Characters/Battle/ChatGPT Image 2026年9月19日 20_36_48.png
- UnityProject/RRPG1.0game/Assets/Art/Characters/Battle/ChatGPT Image 2026年9月20日 12_48_09 (1).png
- UnityProject/RRPG1.0game/Assets/Art/Characters/Battle/ChatGPT Image 2026年9月20日 12_48_10 (2).png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/Card/ChatGPT 图像 2026年9月30日 22_34_47.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/Card/ChatGPT 图像 2026年9月30日 22_39_34-2.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/Card/ChatGPT 图像 2026年9月30日 22_39_36-3.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年10月1日 17_13_17-1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年10月1日 17_13_18-2.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_11_05.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_33-1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_35-2.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_38-4.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_40-5.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_49-9.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月30日 22_39_32-1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/ChatGPT 图像 2026年10月1日 12_27_42-1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/ChatGPT 图像 2026年10月1日 12_27_44-2.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/意图2.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/红血条1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/弃牌堆1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/抽牌堆1.png

Explicitly Excluded（文件保持本地，未 stage / commit）：

- Development/Config/RRPG_Events.xlsx
- UnityProject/RRPG1.0game/ProjectSettings/QualitySettings.asset
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_36-3.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_36-3.png.meta
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_43-6.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_43-6.png.meta
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月30日 22_34_47.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月30日 22_34_47.png.meta
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/意图1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/意图1.png.meta
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/黄血条1.png
- UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/黄血条1.png.meta
- UnityProject/RRPG1.0game/Assets/ChatGPT 图像 2026年9月30日 22_39_36-3.png
- UnityProject/RRPG1.0game/Assets/ChatGPT 图像 2026年9月30日 22_39_36-3.png.meta

## 附录：逐消费者 Text Audit

| 消费者（开工扫描） | 分类 | 处理 |
|---|---|---|
| `Editor/BattlePresentationCutoverAuthoring.cs` | EDITOR ONLY | Current Authoring 创建 / 读取 TMP |
| `Editor/BattlePresentationLayoutAuthoring.cs` | EDITOR ONLY | Current Authoring 创建 / 读取 TMP |
| `Editor/BattleStage1Editor.cs` | EDITOR ONLY | Current Authoring 创建 / 读取 TMP |
| `Editor/CharacterGrowthUIAuthoring.cs` | EDITOR ONLY | Current Authoring 创建 / 读取 TMP |
| `Editor/VillageCommitmentUIAuthoring.cs` | EDITOR ONLY | Current Authoring 创建 / 读取 TMP |
| `Editor/VillageSocialEventUIAuthoring.cs` | EDITOR ONLY | Current Authoring 创建 / 读取 TMP |
| `Scripts/Battle/V1/BattleV1Runtime.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleCardView.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleHudPresenter.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattlePrepCard.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleResultUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleRuleCellMarker.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleUI.cs` | CURRENT + DEBUG CURRENT | 迁移 TMP_Text / TextMeshProUGUI；InfoText 默认隐藏 |
| `Scripts/UI/BattleUIEnemyIntent.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleUIEnemyRoster.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleUIRulePipeline.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/BattleUIUnitVisualHosts.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/CharacterGrowthUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/DeckViewerUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/EventUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/ExpeditionIdentityPanel.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/ExpeditionJourneyView.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/ExpeditionProgressionPanel.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/ExpeditionUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/HeroSelectionUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/JourneyEventCard.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/MainMenuUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/MapUI.cs` | LEGACY / DEAD / Compatibility | 显式白名单保留 |
| `Scripts/UI/RecruitUI.cs` | LEGACY / DEAD / Compatibility | 显式白名单保留 |
| `Scripts/UI/RunRewardUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/UI/ShopUI.cs` | LEGACY / DEAD / Compatibility | 显式白名单保留 |
| `Scripts/UI/TownUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/Village/UI/VillageCommitmentUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/Village/UI/VillageHUD.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/Village/UI/VillageSocialEventUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Scripts/Village/UI/VillageWorldLayer.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |
| `Tests/EditMode/BattleFormationIntegrityTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/EditMode/EventProduct/EventPresentationAuthorityTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/EditMode/ExpeditionJourney/JourneyEventIllustrationBindingTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/BattleStage5IntentPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/BattleVisualFoundationPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/CharacterGrowthPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/ExpeditionEventProductChainPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/PersonBootstrap/PersonBootstrapFreshNewGamePlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/Village/ExpeditionOccupancySelectionPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/Village/VillageCommitmentProductIntegrationPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/Village/VillageEventProductChainPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/Village/VillageHUDIntegrationPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/Village/VillageV3CProductChainPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/VillageV4/JourneyVisualFoundationPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/VillageV4/VillageV4ExpeditionSelectionIdentityPlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `Tests/PlayMode/VillageV4/VillageV4ProgressionClosurePlayTests.cs` | TEST ONLY | 测试宿主 / 断言迁移为 TMP |
| `TextMesh Pro/Examples & Extras/Scripts/Benchmark01_UGUI.cs` | CURRENT | 迁移 TMP_Text / TextMeshProUGUI |

## 附录：Changed Files Manifest

完整逐文件状态与分类：仓库外 `task-files.json`。下表列本任务文件；制作人的未跟踪图片 / meta 以受保护目录归属，不纳入任务文件。

| 文件 | 归属 |
|---|---|
| `Development/Config/RRPG_Events.xlsx` | Unrelated Dirty (baseline) |
| `Docs/DevelopmentSpecs/2026-09-19_RRPG_Person-Visual-Binding-V0.1_Manual-Configuration-Guide.md` | This Task |
| `Docs/DevelopmentSpecs/README.md` | This Task |
| `Docs/项目状态快照.md` | This Task |
| `UnityProject/RRPG1.0game/Assets/Editor/BattlePresentationCutoverAuthoring.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Editor/BattlePresentationLayoutAuthoring.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Editor/BattleStage1Editor.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Editor/CharacterGrowthUIAuthoring.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Editor/VillageCommitmentUIAuthoring.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Editor/VillageSocialEventUIAuthoring.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Prefabs/UI/BattleCard.prefab` | Protected User Visual State + Editor task delta |
| `UnityProject/RRPG1.0game/Assets/Prefabs/UI/CharCard.prefab` | This Task |
| `UnityProject/RRPG1.0game/Assets/Prefabs/UI/CharacterGrowthUI.prefab` | This Task |
| `UnityProject/RRPG1.0game/Assets/Prefabs/UI/HeroSelectCard.prefab` | This Task |
| `UnityProject/RRPG1.0game/Assets/Prefabs/UI/ItemCard.prefab` | This Task |
| `UnityProject/RRPG1.0game/Assets/Prefabs/UI/JourneyPartyMemberView.prefab` | This Task |
| `UnityProject/RRPG1.0game/Assets/Prefabs/UI/UnitCard.prefab` | Protected User Visual State + Editor task delta |
| `UnityProject/RRPG1.0game/Assets/Scenes/SampleScene.scene` | Protected User Visual State + Editor task delta |
| `UnityProject/RRPG1.0game/Assets/Scripts/Battle/Stage1/Rules/BattleCardRuleExecutor.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/Battle/V1/BattleV1Runtime.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleCardView.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleHudPresenter.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattlePrepCard.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleResultUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleRuleCellMarker.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleUIEnemyIntent.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleUIEnemyRoster.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleUIRulePipeline.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleUIUnitVisualHosts.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/CharacterGrowthUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/DeckViewerUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/EventUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/ExpeditionIdentityPanel.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/ExpeditionJourneyView.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/ExpeditionProgressionPanel.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/ExpeditionUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/HeroSelectionUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/JourneyEventCard.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/JourneyEventPresentationCatalog.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/MainMenuUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/PersonVisualBinding.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/RunRewardUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/TownUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageCommitmentUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageHUD.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageSocialEventUI.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageWorldLayer.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/BattleFormationIntegrityTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/BattleStage1.Tests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/EventProduct/EventPresentationAuthorityTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/EventProduct/EventProduct.Tests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/ExpeditionJourney/ExpeditionJourney.Tests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/ExpeditionJourney/JourneyEventIllustrationBindingTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/PersonVisual/PersonVisual.Tests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/PersonVisual/PersonVisualBindingTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/StaticConfig/StaticConfig.Tests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/BattleFormationRosterConsistencyPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/BattleStage1.PlayModeTests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/BattleStage5IntentPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/BattleVisualFoundationPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/CharacterGrowthPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/EncounterProductPathPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/ExpeditionEventProductChainPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/ExpeditionV4F/ExpeditionV4F.PlayModeTests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/PersonBootstrap/PersonBootstrap.PlayModeTests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/PersonBootstrap/PersonBootstrapFreshNewGamePlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/PersonVisualBindingPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/ExpeditionOccupancySelectionPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageCommitmentProductIntegrationPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageEventProductChainPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageHUDIntegrationPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageStage1.PlayModeTests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageV3CProductChainPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/VillageV4/JourneyVisualFoundationPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/VillageV4/VillageV4.PlayModeTests.asmdef` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/VillageV4/VillageV4ExpeditionSelectionIdentityPlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/VillageV4/VillageV4ProgressionClosurePlayTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` | This Task |
| `UnityProject/RRPG1.0game/ProjectSettings/QualitySettings.asset` | Unrelated Dirty (baseline) |
| `Docs/DevelopmentSpecs/2026-10-01_RRPG_Current-UI-TMP-Battle-Surface-V0.1_Report.md` | This Task |
| `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/BattleCardPresentationCatalog.asset` | This Task |
| `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/BattleCardPresentationCatalog.asset.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/BattleKeywordCatalog.asset` | This Task |
| `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/BattleKeywordCatalog.asset.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/CurrentUIFontAuthority.asset` | This Task |
| `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/CurrentUIFontAuthority.asset.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/Battle/Stage1/Rules/BattleRuleValueResolver.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/Battle/Stage1/Rules/BattleRuleValueResolver.cs.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleCardPresentationCatalog.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleCardPresentationCatalog.cs.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleCardPresentationResolver.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleCardPresentationResolver.cs.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleHudFormatting.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleHudFormatting.cs.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleKeywordCatalog.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/BattleKeywordCatalog.cs.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/CurrentUIFontAuthority.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/CurrentUIFontAuthority.cs.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/CurrentUIFontBinding.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Scripts/UI/CurrentUIFontBinding.cs.meta` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/CurrentUIPresentationContractTests.cs` | This Task |
| `UnityProject/RRPG1.0game/Assets/Tests/EditMode/CurrentUIPresentationContractTests.cs.meta` | This Task |
