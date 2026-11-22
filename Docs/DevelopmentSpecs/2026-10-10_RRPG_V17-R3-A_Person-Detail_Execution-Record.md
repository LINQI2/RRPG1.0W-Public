# RRPG V17-R3-A Person Detail — Execution Record

开工：2026-10-09；技术交付：2026-10-10。授权基线：`main@d395604a4d71731c84c636132af1fdf9e094900b`。

状态：**R3-A PRODUCT ACCEPTED / Final Closeout Prepared**。2026-10-10制作人明确回复「验收通过」；本次验收后清理、全量复测与Handoff80已完成，生成时尚未Commit/Push，实际发布后追加证据。R3-P已发布，R3-B未启动。下方验收前技术记录保留当时事实；当前结果见末尾Final Closeout。

## 验收前实现与技术交付记录（保留当时事实）

## Scope / Source → Query → View

- 身份：现有 PlayerCollection → FindPersonByPersonId / Hero 与 Character → PersonDetailReadAdapter 的不可变显示快照；名册按姓名检索，稳定 PersonId 区分同名实例。
- 选择：ProductHomeWorldHost.Selection / HomeSelectionContext 是唯一 Home 选择；World / Inspector / 名册 / Attention / 关系行进入相同 PersonId。原 Growth 名册在 Home 绑定下同步该选择，不保存第二个 Selected Person。
- 概览与生活：ProductHomeSnapshot、VillagePersonAvailability、ActivityRuntimeBridge.QueryClaim/QueryActivityInstance、现有 Presence / 当前地点 / 目的地 / 实际劳动查询、IFulfillmentHost 与 PersonRest；职责不被当成劳动或到场事实。
- 成长：GrowthAuthority.Resolve → 等级、经验、Trait、个人 Unlocked Pool 与 Starter；ProfessionRegistry 的 Full Pool 明确为理论池。命令仍由 CharacterGrowthUI → CharacterGrowthService / CardGrowthResolver 执行。
- 关系：SocialRuntimeBridge.QueryFacts 过滤参与人，再分别 QueryRelationship(A,B) 与 QueryRelationship(B,A)；neutral fallback 不表示已确立关系，无记录为空态，未知对象不可跳转。
- 经历：CharacterInstance.LifeHistory → ProfessionFormed / ExpeditionReturned，按日期稳定排序，同日保留原事实顺序；Hero / 无记录为空态。不借用 Feed、WorldEventHistory 或 Ledger 制造人生记录。
- 保存：仍是 SaveSystem.Save 的 bool 回执；失败保留内存状态并提示重试。不改 SaveVersion、DTO 或持久 pending 格式。

## UI / narrow commands

固定标题、关闭、姓名检索、名册 ScrollRect、五页签、详情 ScrollRect、Blocker 来自 `Assets/Resources/UI/PersonDetailView.prefab`，Editor 可编辑。Runtime 仅创建人物与关系重复行。PersonDetailAuthoring曾仅用于创建这一Prefab，不保存或重构当前Scene；验收后作为一次性工具经AssetDatabase删除，Prefab与必要绑定保留。

沿现有 HomeProductShell/Router 进入 Person Surface；独立排序90、原 Growth 排序100，Blocker 留出顶部时间按钮。模态阻断 World click、Camera input、Build 导航以及旧 Town CanvasGroup 鼠标/键盘提交；打开内部页不暂停模拟，手动 Pause 保留。ESC 与关闭走同一保护入口。正常关闭保留有效 World Selection；来源替换清除共享选择与会话检索，并关闭没有待选事务的旧成长页面。

原 Growth 的窄入口 OpenForPerson / TrySelectPerson / TryHide 复用原 Prefab 与服务。已升级且待选时阻止关闭、重开重置、切人、切页、ESC 与其它 Surface；选择或原有 Skip 后才可离开。返回选牌复用同一 offer，不 reroll。源或人物失效时禁止把待选结果写入另一人物/世界；跨进程待选状态不在本包解决。

## Failures / repairs / evidence

1. 先执行真实升级复现，Lv2 已写入后 Hide 能丢弃待选界面（Expected True / Actual False）。随后加窄关闭保护，复现通过。
2. 人物页第一次专项出现 Unity 缺失组件的假空值：GetComponent<CanvasGroup>() 经 `??` 未创建有效组件。改为 Unity 显式 null 判断后修复。
3. 测试初稿把 VillageRuntimeBridge.Time 字段读为属性，纠正反射回读；不是 Runtime 时间故障。
4. MCP 的 namespace-less class 正则、首次 RetrieveTestList 超时，以及一次用户中断未保留回执均不计 PASS；未修改 MCP 插件。使用完整 namespace 与程序集并取得真实执行回执。
5. 完整旧 EditMode 测试会删除测试存档，使之后依赖 Continue 的专项无档失败。新增专项在缺档时建立隔离存档，逐项 TearDown 恢复原字节/原缺档状态；最终用户存档仍从本任务开工备份精确恢复。
6. 首轮完整测试虽通过，实际截图发现人物 Canvas 挂到 Home 后零尺寸/零缩放。修正 Prefab 根布局与实例挂载，新增实际宽度、缩放和详情视口屏幕范围断言；真实1920/1280画面重新检查。
7. 实际1280成长画面发现长ID折行覆盖等级、名单标题偏低、第三候选与Skip/消息重叠、长描述越界。仅调整原Growth标题、原Prefab标题与消息区、动态按钮文字裁切/字号和Skip位置；不改变服务、牌池、策略或存档规则。修复后两种分辨率候选/Starter选择实际截图可读。
8. 首次人物页编译时把返回bool的OpenGrowth/SaveCurrent直接绑定UnityAction出现CS0407，改为lambda调用；最终代码编译与完整测试通过。工具探针曾误用TestRunnerApi.IsTestRunActive、ProfessionDefinition.DisplayName，探针编译失败不计入产品运行错误，未向项目引入这些接口。

最终版已串行实际执行完整 Compiler264/264、EditMode1495/1495、PlayMode114/114，均0失败/跳过；真实画面与视口已回看，不借用 V17-R2 历史结果。最终Unity测试回执对应包含画布尺寸/缩放/视口断言、关系行真实跳转和全部成长策略的当前代码。

首轮完整PlayMode114/114、0失败/跳过；随后真实画面暴露上述布局缺陷，修正后的最终完整回归另行登记，首轮结果不替代最终版。专项Edit3/3、Play5/5；完整运行数字是本轮真实回执，MCP专项回执的TotalTests为全域发现数，不把发现1495/114冒充专项执行数。

## Product evidence / acceptance scene

真实 MainMenu→Continue 加载原用户6位人物。实际1920×1080概览、生活、Starter选择；1280×720成长、关系空态、人生经历空态、三选一及Starter选择均截图回看。长名册34+行由专项临时添加30位同模板、不同ID人物验证，筛选、滚动和周期刷新不复位；不写用户存档。关系行跳转使用隔离Social fixture经既有Restore/Query路径，未知人物不允许跳转；正常用户世界没有关系/人生经历时保持空态。

原用户人物开工没有可升级职业经验。为方便产品验收，仅在Play内存把原人物林恩的已有职业经验置为10，人物ID、职业、Starter/Unlocked Pool沿用真实用户数据；这是一项显式验收fixture，不声称经验自然获得，不写入用户原档。最终现场停在升级前，未制造持久pending或第二套人物世界。真实成长操作会按原服务保存，验收后如需恢复开工档，应使用本任务字节备份并核对是否已有新进展。

当前职业定义没有DisplayName，人物详情如实显示既有ProfessionId/TraitId，未自行编造职业名或修改配置Authority。原型名称、关系Affinity与美术仍不冻结。

验收最多8步：关闭成长回人物世界 → 重新打开人物并检索姓名 → 五页签与Hero空态 → 生活区分职责/实际活动 → 打开林恩成长 → 升级后三选一 → 试关闭/ESC与返回选牌，完成选择或Skip → 核对Starter、关闭后的同一人物和顶部时间按钮。人工产品验收尚未发生。

## Protection / Git

本任务开工保护22项（5 tracked dirty +17 untracked）：Events XLSX、TMP字体、Packages、QualitySettings、用户图片/meta、.codely、旧diff、原验收截图。逐文件 SHA256 与副本在任务外 work/r3a 保存；测试产生的字体动态缓存从本次备份恢复，绝不从 HEAD 或旧阶段备份覆盖。用户存档来自 Editor 实际 persistentDataPath：`1PUBLIC_USER/AppData/LocalLow/DefaultCompany/RRPG1_0game/rrpg_save_p0.json`，开工21987字节，SHA256 `FDB8FB23BED2270809C3B5BE57D4D66103BE9927CE550D7464E772BCA5E3132C`。

最终22/22保护文件SHA256与本次开工基线完全一致，用户原档21987字节及完整SHA256一致。24路径任务manifest（18项代码/Prefab/meta/test +6份Markdown）与保护项交集为0，暂存区为空。不暂存任何保护项、测试截图或 work 证据。当前无 Git 写操作；不宣称 Work Package / Branch / Repository Closed。

完整旧PlayMode测试还写入其固定路径D-onsite-product.png；任务结束从本次开工备份恢复该原截图，与字体缓存同样逐字节核对。未用HEAD或旧R2备份代替用户开工状态。

## Rejected / Deferred / acceptance boundary

没有新增 Person / Growth / Assignment / Social / History / Save Authority；没有 R3-B 管理命令、正式 Cards/History 页面、关系阈值、死者档案、新职业/卡牌规则、SaveVersion/DTO、美术量产或全 UI 重构。原 VillageCommitmentUI 与 Growth Prefab 保留。

成长待选关闭风险在本轮内存 UI 流程加保护，不能被解释为新增跨进程事务保存。撤职劳动意图风险、关系查询仍只覆盖真实事实参与对象、Hero/历史记录缺口继续保留；Issues84/85/86/88不由本任务关闭。

技术全部完成后只写 **Implemented / Technical PASS / Producer Acceptance Pending**。依据制作人本次 R3-A 指令§7，停一次供真实产品验收；通过后才清理 Disposable、全量重跑、精确 Stage / Commit / Push / 三方 SHA 核验。当前不进入 R3-B。最终 Handoff79 保留历史真实事实，只更新首页当前状态；验收前不预生成最终 Handoff80。


## 验收前技术交付历史 — 2026-10-10 00:20

正常原档Home于2026-10-10 00:15:09～00:16:46（Asia/Shanghai）观察约97秒：按最近2分钟时间窗口读取MCP日志，Error / Exception / Assert / Warning均0；此前工具、故障注入与旧测试日志不混入该窗口。Unity保留真实原档6人、林恩person_6619c15f3cee42549e41e57b4bcb0e8e、Person Growth页、Lv1/Exp10内存验收fixture、无pending，手动暂停以保持现场；未点升级、未保存fixture。

Git：local HEAD / origin/main / 实时远端main均d395604a4d71731c84c636132af1fdf9e094900b；未Commit/Push，index空。248条相对文件引用检查通过。Handoff79标记正文与HEAD相同，首页只同步当前验收前状态。当前Scene保持SampleScene，未保存Scene变化。

24路径清单：

- `UnityProject/RRPG1.0game/Assets/Prefabs/UI/CharacterGrowthUI.prefab`
- `UnityProject/RRPG1.0game/Assets/Scripts/UI/CharacterGrowthUI.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs`
- `UnityProject/RRPG1.0game/Assets/Editor/PersonDetailAuthoring.cs`
- `UnityProject/RRPG1.0game/Assets/Editor/PersonDetailAuthoring.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Resources/UI.meta`
- `UnityProject/RRPG1.0game/Assets/Resources/UI/PersonDetailView.prefab`
- `UnityProject/RRPG1.0game/Assets/Resources/UI/PersonDetailView.prefab.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailReadAdapter.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailReadAdapter.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailView.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailView.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/PersonDetailProjectionTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/PersonDetailProjectionTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/R3AGrowthTransactionPlayTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/R3AGrowthTransactionPlayTests.cs.meta`
- `Docs/DevelopmentSpecs/2026-10-10_RRPG_V17-R3-A_Person-Detail_Execution-Record.md`
- `Docs/DevelopmentSpecs/README.md`
- `Docs/项目状态快照.md`
- `Docs/项目日志.md`
- `Docs/文档地图.md`
- `Docs/开发验收交接.md`

保护项完整清单及最终逐文件SHA256、原档备份、TRX和Unity首次失败/最终回执留在任务work/r3a；用户可读报告与8张真实截图在outputs。测试中的Social和长名单fixtures、职业经验种子只在隔离会话，不进入用户磁盘原档。5项Play / 3项Edit专项尚未做Acceptance后Disposable分类清理；验收通过后按指令清理并再次完整重跑，再精确Stage/Commit/Push；当前不生成最终Handoff80、不跨R3-B。

## Final Closeout — 2026-10-10（验收后当前事实）

制作人本任务明确回复「验收通过」，人物五页签及原成长流程产品Acceptance通过。依据R3-A实施指令§7执行清理、完整复测及精确Commit/Push；当前生成时为PRODUCT ACCEPTED / Final Closeout Prepared，不提前声明发布。仅R3-A，不进入R3-B。

### Disposable / retained contracts

通过当前Editor AssetDatabase.DeleteAsset删除一次性PersonDetailAuthoring.cs及.meta，正式Prefab保留。Play测试5项缩为2项：移除重复关闭复现、长名册/Inspector/Attention/关系跳转综合验收fixture、重复原服务固定节点/Starter策略验收；保留共享PersonId/单一时钟/模态和成长pending不丢失不reroll/真实Save失败反馈/Load兼容契约，合法个人牌池与返回选牌断言合入保留用例。3项Edit分别保护稳定人物身份、只读投影与真实人生事实顺序、canonical事实与双向关系边界，属于最小不同契约集合。没有把专项测试目录用作历史档案库。

清理后串行实际完整Compiler264/264、EditMode1495/1495、PlayMode111/111通过，均0失败/跳过。此前114项Play是验收前历史；本次111项是清理后真实执行数。MCP首次测试发现超时不计PASS，取得实际完整回执后登记。正常Home Console与Editor状态以本次观察回执为准；测试故障注入日志单独区分。

### 最新用户状态保护

2026-10-10验收后从当前Editor重新读取persistentDataPath / SaveSystem.GetSavePath、SampleScene.scene且场景clean；Editor已停止Play。最新用户存档22979字节，SHA256 8883485BCF367445330CF0EFB1AB3071C0DEF8512210D84813B7EEBB23C1616D，与开工21987字节原档不同。先备份最新字节，完整测试后精确恢复；没有覆盖验收期间新增存档进展。

验收后22项保护以本次acceptance-baseline核验22/22；其中21项与开工基线相同，TMP已在验收期间改变，保留最新TMP哈希，不回滚为开工字体。测试写入的TMP动态缓存和D-onsite-product.png仅恢复本轮验收后备份。其它20项从未被本次清理/测试恢复脚本覆盖。存档和保护副本、测试回执在聊天work目录，不入仓库。

### Closure staging manifest / scope

Expected staged file count：22（16项代码/Prefab/meta/长期测试 + 6份Markdown）。Must Stage：

- `UnityProject/RRPG1.0game/Assets/Prefabs/UI/CharacterGrowthUI.prefab`
- `UnityProject/RRPG1.0game/Assets/Scripts/UI/CharacterGrowthUI.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs`
- `UnityProject/RRPG1.0game/Assets/Resources/UI.meta`
- `UnityProject/RRPG1.0game/Assets/Resources/UI/PersonDetailView.prefab`
- `UnityProject/RRPG1.0game/Assets/Resources/UI/PersonDetailView.prefab.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailReadAdapter.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailReadAdapter.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailView.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/PersonDetailView.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/PersonDetailProjectionTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/PersonDetailProjectionTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/R3AGrowthTransactionPlayTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/R3AGrowthTransactionPlayTests.cs.meta`
- `Docs/DevelopmentSpecs/2026-10-10_RRPG_V17-R3-A_Person-Detail_Execution-Record.md`
- `Docs/DevelopmentSpecs/README.md`
- `Docs/项目状态快照.md`
- `Docs/项目日志.md`
- `Docs/文档地图.md`
- `Docs/开发验收交接.md`

Must Not Stage：本轮22项保护文件、存档、一次性工具、截图、测试结果、临时Runner/日志/work。任务manifest与保护项交集0。Handoff80只覆盖HANDOFF标记正文，固定规则保留；五份状态入口及本记录相对链接核验。GameSpec、Architecture、决策记录、问题清单无需本包改动。

成长保护仅当前内存事务，跨进程pending保存仍Deferred；撤职劳动意图属于R3-B待验证；关系只能枚举已有真实事实参与对象、完整关系存在性/历史档案仍有缺口。Issues84/85/86/88保持开放。没有第二套Person/Growth/Assignment/Social/History/Save Authority，没有SaveVersion/DTO变化。共享工作区有用户保护修改，不宣称Branch Closed或Repository Closed。
本次正常Home观察：2026-10-10 12:13:35～12:15:45（Asia/Shanghai），130秒；最近2分钟Error/Exception/Assert/Warning均0。真实Continue载入验收后6人，无经验fixture、无pending，保留玩家Paused状态，观察后退出Play。
