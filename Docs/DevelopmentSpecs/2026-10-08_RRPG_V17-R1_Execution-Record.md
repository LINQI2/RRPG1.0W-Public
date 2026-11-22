# V17-R1 动态建筑资源生产闭环

Current（2026-10-09收尾）：制作人明确确认“V17-R1 制作人产品验收通过，收尾”，认可Remote Desktop Commander检查的真实Home变化、执行记录、自动测试与工作区；授权清理、完整复跑、保护恢复、同步文档与精确Commit / Push main。Work Package Closed / Product Accepted / Published main；只关闭R1，不进入下一包。实际发布核验见本文末尾。

## 验收前检查点（2026-10-08，历史事实）

日期：2026-10-08。授权：开发17.0用户任务；仅动态 Farm / Kitchen 资源闭环。基线及 Target Branch：main@157fe9a83f6fc2b443fc3d54559aa9baaa1e6439 / main。本地、缓存 origin/main 与 GitHub main 实核一致。状态：Technical Verified / Playable Milestone Ready / Producer Acceptance Pending；未 Commit / Push，不宣布完整 V17 路线冻结。

## 目的、原方案与 Existing Host Map

V16-C2 完成 Building → Construction Project → Completed Runtime Workplace，资源目录仍为静态 ws_farm_01 / ws_kitchen_01，动态产出主动 Deferred。沿现行 Resource / Building / Save GameSpec 与用户本包授权，补 BuildingInstance → Runtime Workplace → Actual Labor → Resource Batch → Village Resources。

既有宿主：VillageRuntimeBridge.Buildings 管完工与候选恢复；VillageAssignmentSystem 管名额及长期职责；Work.cs 从实际到场、Project / Activity / Expedition / Availability 权威选工人；NextWorkBoundary 与 CompleteWorkInterval 同一资源目录；VillageIntradayWork 按 WorkplaceId 保存批次与已投入材料；VillageResourceState 唯一库存；午餐、Hunger、Carry 与 Return 继续走原链。Home Placement / Inspector / Town / Continue 复用原产品入口。

## 采用与未采用方案

采用实例 WorkplaceId 到既有 Profile 的薄映射，复制只读规则值，不复制容量、Assignment、Clock、库存或批次。完工幂等加入派生目录，候选 Load 同时准备目录与实例岗位，最终 Commit replacement；NewGame 清派生目录与批次。SaveVersion=1，DTO 不变，实例身份直接使用既有 IntradayWork.Batches.WorkplaceId。

未采用：共用 ProfileId 作批次 key（会串余数）；只修产出不修边界（会出现倍速差异）；运行期每帧重建资源域；新 Recipe Framework；Building 自持资源 / 劳动 / Capacity；整日补产、整个经济系统重构。

两个 Kitchen 争料：区间开始以 WorkplaceId ordinal 顺序分配真实材料，只有实际工人可投入；边界用同一顺序作非写入预算。区间末 Farm 新产出不能追溯资助区间内 Kitchen；缺料不累计资源劳动证据。该顺序是确定性技术仲裁，不冻结经济优先政策。

## 风险与验收

风险：动态生命周期目录滞后、实例余数串联、材料重复投入、非法 Load 半注册、高倍速跨过下一批边界。验收覆盖静态兼容、双 Farm 独立、双 Kitchen 稀缺材料、实际施工 / Project / Away / Pause、全部七倍率、午餐 / Hunger / Carry / Return、真实 Save/Continue / invalid Load / NewGame / 旧档。

当前工作区保护：20 项原有任务外文件已逐字节备份及 SHA256；原单槽档已备份。忽略目录 Temp/V17R1 保存保护清单、测试证据与现场副本，不入提交。原档开工 SHA 以 saves.json 为准。Packages / QualitySettings / XLSX / TMP / 图片与原有 diff 不纳入本包。最终保护与测试结果待追加。

完整 Compiler → EditMode → PlayMode 串行；真实 Home 建造 Farm → 实际施工完工 → 分配 → Ingredients → Kitchen → Food → Save/Continue；技术完成停在制作人产品验收。未验收前不覆盖 Handoff76、不标 Closed。

## 本轮专项与环境证据

- Unity Asset Refresh 后程序集已实际加载；CompilationFailed=False，无新增 C# 错误。
- Building namespace EditMode：23通过 / 0失败 / 0跳过，其中新增资源用例14（七档速度、Pause 与逐分钟对照包括在内）。PlayMode：6通过 / 0失败 / 0跳过，其中新增正式 Home 资源/Save 与异常 Load 用例2。
- 两次 class 筛选无测试，确认新类型已在 VillageStage1.Tests / VillageStage1.PlayModeTests 加载后切 namespace 取得实际结果；PlayMode 首次发现无测试后重试成功。空结果不标 PASS。
- 完整 Compiler：264/264，0失败 / 0跳过，TRX在 Temp/V17R1/compiler-results/V17R1-compiler-full.trx。首次沙箱 testhost 本机通信90秒超时 / 0执行；允许本机测试宿主通信后同套全量通过，首次中止保留证据。
- 正式 Home 专项从 SampleScene → SaveSystem.NewGame/Save → MainMenuUI.OnContinue → Town Initialize 进入；Placement Catalog/Confirm与Inspector真实Worker按钮接线，未用SetActive替代初始化。Farm/Kitchen全程从0施工到10完工，未写Progress；施工中0资源批次。第11日到场后08:40 Ingredients=1，09:00厨房已投入 / LaborNumerator=240，Save/Continue快照完全一致，09:20 Food=1、Ingredients=1，已支付批次不重复扣料。
- 非法动态Assignment与future opportunity在候选恢复后、世界发布前拒绝，现有资源 / Building / Assignment / Batch快照不变；继续劳动仍可产出。NewGame清动态岗位、建筑和实例批次；旧null Building / IntradayWork路径及静态生产有专项覆盖。

当前测试为验收期间保留；Final Closeout按Disposable规则收缩为最小长期契约集合。

## 完整回归与真实 Home 最终现场

| 串行验证 | 本轮有效结果 |
|---|---|
| Compiler | 264/264 PASS，0失败/跳过 |
| Full EditMode | 1499/1499 PASS，0失败/跳过，00:02:05.2450111 |
| Full PlayMode | 108/108 PASS，0失败/跳过，00:01:34.4362527 |

两个全量 Unity mode 切换后首次发现无测试；重试取得以上非空完整结果。完整回归包含静态Resource、午餐/Hunger、Expedition Carry / Return Catch-up、原有Save与Building产品链；不借历史结果当本轮PASS。

2026-10-08 20:49:10 +08，原档真实 MainMenu Continue → Town → Home：6人物、0建筑、minute=0、库存0/0、sim=True。之后通过现有Catalog/Confirm与Inspector Worker按钮放置两Farm、一Kitchen；三人分别实际施工，唯一Clock推进至第10日施工全部完成，没有注入Project Progress。完成后通过同一Inspector分配实例岗位。

| 第11日时刻 | Ingredients | Food | 观察 |
|---|---:|---:|---|
| 08:00 | 0 | 0 | 三人到各自实例岗位；尚无劳动产出 |
| 08:40 | 2 | 0 | 两Farm各完成一批，独立WorkplaceId |
| 09:00 | 1 | 0 | Kitchen已投入1；三实例各LaborNumerator=240，厨房InputPaid=true |
| Save / Continue 09:00 | 1 | 0 | 完整Village快照逐字相同，无重新投料/产出/劳动 |
| 09:20 | 3 | 1 | 两Farm再各产1，Kitchen完成已支付批次；三余量为0 |
| 1×实时到09:36 | 2 | 1 | 再投入厨房下一批；三余量均192，Farm实际劳动96分钟/人、Kitchen56分钟 |

当前真实Home停在第11日09:36、Pause，两Farm与Kitchen已完工并分别有人；真实Query和人物表现均在各自岗位，Kitchen被选中。没有修改正式美术。演示档、最终World快照及截图在忽略Temp/V17R1；原档已恢复开工原字节，当前Live演示与磁盘原档明确分开。**当前直接Continue会读取原档；复验当前演示Continue应先使用“建造→保存村庄”。** 演示独立副本demo-save.json保留，原档备份同目录save-backup保留。

正常Home观察自20:49:10至20:52:40 +08：无新增Error / Warning / Exception；最终原生Console计数Error=0、Warning=0，CompilationFailed=False，SceneDirty=False，WorldVisible=True。20:48:24恢复TMP Import时MCP缓存有一条Importer inconsistent警告，属于恢复/Import阶段，单独记录，不算正常Home观察区间，未隐藏为历史Console全空。

最终20/20保护路径SHA256与开工副本一致；TMP自动字形缓存经Unity Editor FileUtil恢复原dirty字节，未用HEAD覆盖。全量既有Onsite测试覆盖了原任务外D-onsite-product.png，先存本轮输出到Temp再恢复开工图片字节。原档SHA256=337AC2B2E3774DA1412852D3CAAE973C9C19FE1E84752C8B88092A46FD9A9173，恢复核验一致。index为空，HEAD仍157fe9a；未Commit/Push，工作区保留20原任务外dirty/untracked，不宣称Branch/Repository Closed。

本包无已知技术阻塞。制作人产品Acceptance尚未取得；Handoff76、长期Architecture同步与Disposable Final Cleanup留在验收后。既有Village/Event全世界Load事务债未解决，本包只保证候选Building/Workplace/Resource目录不半发布；Issue84/85/86/88无关范围、正式经济与美术继续Deferred。

## 本包修改清单（验收前）

共11路径，不暂存：

- UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.cs：静态资源Profile与派生实例目录分开。
- UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Buildings.cs：完工映射、候选恢复目录、动态批次身份检查、NewGame清派生。
- UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Work.cs：同一目录的下一批次边界与区间结算、稳定顺序材料预算/投入。
- UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageIntradayWork.cs：区间开始原子投入第一批材料；已投入批次保留。
- UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingResourceTests.cs及Unity生成.meta：实例批次、材料争用、生命周期与倍率验证。
- UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingResourceProductPlayTests.cs及Unity生成.meta：正式Home完整生产与Save/Continue / invalid Load。
- Docs/DevelopmentSpecs/2026-10-08_RRPG_V17-R1_Execution-Record.md：本包目的、技术取舍、风险与证据。
- Docs/DevelopmentSpecs/README.md、Docs/项目状态快照.md：Current Development窄包导航。

不修改SourceAssets、Authoring XLSX、Generated Config、Scene、Prefab、Packages、ProjectSettings、字体设计、Save DTO或SaveVersion。20项任务外原有文件、原档、Temp证据与演示档均排除；字体自动缓存增量仅恢复本轮开工dirty副本。Handoff76在产品Acceptance前保持原事实。

## Final Closeout：Disposable清理与真实复跑

制作人验收与Commit / Push授权已取得。清理8项Edit验收用例（单Farm/静态重复、Project/Away重复及6档重复速度）和1项Play异常Load重复。保留6项Edit长期契约：双Farm撤工/换工/容量与独立余量、双Kitchen稀缺输入与缺料零劳动、Farm区间末产出不追溯资助Kitchen、已投入半批重复Restore/注册幂等、非法实例批次候选拒绝/NewGame/旧档静态生产、Pause与24×对照逐分钟结果。保留1项真实Home Play：Catalog/Inspector实际施工与分配、资源变化、完整半批Save/Continue快照及后续加工。两份必要.meta保留。忽略目录的HomeProof.cs诊断脚本已删除；备份、演示副本和结果只作为保护/验证证据，不入提交。

清理后串行Compiler（2026-10-08 21:04）264/264 PASS；Full EditMode（21:07）1491/1491 PASS，Duration=00:02:04.3931832，均0失败/跳过。2026-10-09续跑Full PlayMode首次有效执行107项：106通过、1失败、0跳过，Duration=00:01:31.1868445。失败为既有VillageV4ProgressionClosurePlayTests.Progression_InsufficientSupply_ShowsVisibleReason_AndRetreatReturnsToTown：Supply耗尽后没有测试要求的Battle/Elite/Boss后继。MapGenerator使用Unity Random且该测试未固定种子，代码审计提示随机前置假设；不据此宣称R1回归。R1长期Home用例与其他106项本次通过，首次失败完整保存，不以空发现或筛选无测试冒充PASS。完整Play重跑结果随后追加。

Current Architecture对应资源与Building边界、术语表、README/文档地图/DevelopmentSpecs索引、Current Snapshot、日志与Handoff77本轮同步；GameSpec不改，不扩大规则或冻结V17路线。

2026-10-09完整PlayMode未改代码重跑107/107 PASS，0失败/跳过，Duration=00:01:38.0630120。首轮随机后继前置失败仍保留；未修改既有远征测试或产品逻辑，不标该测试稳定性债已修复。清理后最终有效全量为264 / 1491 / 107，全程串行；首次空发现、无效class筛选与工具连接暂不可用均不计PASS。原有静态Farm/Kitchen、午餐/Hunger、携粮/返程、Project独占劳动与Save测试随全量执行；R1独立余量、材料投入/缺料/零工人及重复恢复、新旧档路径由保留长期回归验证。

2026-10-09 12:24:54–12:25:38原始单槽档真实MainMenu Continue → Town → Home：6人、minute0、0建筑、Ingredients/Food=0/0、WorldVisible=True，Clock已Pause；原生Console Error=0 / Warning=0、CompilationFailed=False、SceneDirty=False。退出Play后经Unity Editor FileUtil/Import恢复原dirty TMP字节；截图自动覆盖先保存本轮证据再恢复原字节，原档再恢复。20/20保护路径与原档SHA256均一致。12:26:55退出Play/字体恢复之后Edit场景出现LiberationSans SDF缺中文字符警告（SectionActiveLabel），如实单列，发生于正常Home观察区间之后；未改受保护字体或把它算成资源闭环错误，不宣称最终历史Console全空。

Handoff77已写入并回读HANDOFF区域和diff；保留生成时main@157fe9a、三方一致、index空、尚未Commit/Push事实。Final Diff确认4 Runtime修改、6 Edit / 1 Play长期契约及.meta、9文档，无Save DTO、GameSpec、XLSX、Generated、Scene/Prefab或正式美术变更。原任务外20文件/原档/截图/Temp排除；未启动下一包。

## Closure Commit Staging Manifest

Must Stage（17精确路径）：

- `README.md`
- `Docs/Architecture/技术架构总览.md`
- `Docs/Architecture/核心对象与术语映射.md`
- `Docs/DevelopmentSpecs/2026-10-08_RRPG_V17-R1_Execution-Record.md`
- `Docs/DevelopmentSpecs/README.md`
- `Docs/开发验收交接.md`
- `Docs/文档地图.md`
- `Docs/项目日志.md`
- `Docs/项目状态快照.md`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageIntradayWork.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Buildings.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Work.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingResourceTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingResourceTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingResourceProductPlayTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingResourceProductPlayTests.cs.meta`

Must Not Stage：protected.json列出的20项原有文件（XLSX、TMP、Packages、QualitySettings、原图/.meta、两份diff、D-onsite-product.png）、原始存档、Temp/V17R1、TestResults、其他自动写入资产。Expected staged file count: **17**。新增测试.meta由Unity生成；精确暂存前后逐项比较此manifest，保护SHA再次核验。发布SHA与实际结果只在Commit/Push成功后追加。

暂存17/17与manifest一致。351项文档文件链接核对存在；非.meta暂存diff-check通过。两份Unity原生.meta的空userData / assetBundleName / assetBundleVariant字段自带尾随空格，默认diff-check报告6处；保留Editor生成格式，仅对这两路径关闭blank-at-eol检查，其余空白检查照常，不手工改写.meta。

## 实际发布与最终关闭（2026-10-09）

Closure Commit `dbea0b539a6109dc70d16955e6b5428033439dd5`已正常Push main（157fe9a → dbea0b5，无force）；local HEAD / origin/main / 实时GitHub main均为该SHA，index空。实际提交17路径与manifest完全一致，无保护资产混入。测试264/1491/107为清理后有效全量；首次失败、工具空发现与退出Play后的字体警告保留如上。20/20保护文件、原dirty TMP、原截图及原始存档SHA再次一致；共享工作区仍有原任务外变化，不标Branch / Repository Closed。

V17-R1 Work Package Closed / Product Accepted / Published main。Handoff77保留生成时事实。发布后仅同步这次已实现/已验证状态的7份文档：本执行记录、DevelopmentSpecs/README、项目状态快照、项目日志、开发验收交接首页、文档地图、根README；无Runtime/配置/测试改动，不再扩大验证。此文档同步提交的最新HEAD以最终Git核验为准，不预填自身SHA。

Deferred：完整经济平衡、Wood/Stone、建筑材料成本、Recipe Framework、升级/仓储/物流、新村庄AI、正式美术/完整页面、Issue84/85/86/88无关部分、Village/Event全世界Load事务债；既有随机远征测试稳定性与Edit场景LiberationSans中文fallback警告仅记录，未扩包修复。R1无已知产品阻塞；完整V17产品路线未冻结，不自动进入下一工作包。