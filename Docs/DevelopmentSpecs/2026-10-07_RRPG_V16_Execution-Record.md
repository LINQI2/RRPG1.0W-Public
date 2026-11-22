# V16 累计执行记录

本记录只记实施与验证事实，不替代 GameSpec、Architecture 或 Gate 授权。

## V16-A Preflight（2026-10-07）

- 授权前 local HEAD / origin/main / GitHub main 均为 `b2b2cb169a8c262b9acac7365dad815eb95a42f1`，该提交是当前基线祖先。当前仅 A 授权；B/C1/C2/D 未授权。
- 任务开始前 17 个任务外 dirty/untracked 文件已建立 SHA256 清单及逐字节备份（本地忽略目录 `Temp/V16A`）。包含 Events XLSX、TMP、QualitySettings、图片/meta 和 review diff；不暂存或修改这些原始内容。
- Unity MCP 已连接到团结引擎现有工程。SampleScene 已保存；非 Play、非编译；初始 Error 查询为 0。现有 Main Camera 为正交相机，TownCanvas 没有全屏不透明背景；无需重排场景或改序列化绑定。
- Person 身份/名字：`VillageRuntimeBridge.BuildPersonInputs(SimulationPersonSource)` 读取现有 PlayerCollection 的真实 PersonId；无模板身份替代。职业读取 CharacterInstance.ActiveProfessionProgress.ProfessionId；现有 ProfessionDefinition 没有 DisplayName，故只展示合法 ID，不造名称映射；英雄无该职业字段时不推断。
- Primary Assignment：Assignments.GetWorkplaceId / GetWorkplaceDef。临时占用：Bridge.GetPersonOccupancyReason；Availability 使用现有纯 VillagePersonAvailability.Resolve（避免调用会同步 Unavailable 的 GetPersonAvailability）。
- 实际 Presence / Place / Destination / Activity：Bridge.QueryIntradayPresence，内含 D 已发布的活动 Claim/Instance 和 IntradayWork 到岗查询；不使用旧 VillageWorldPresence 推演时间或到场。
- Registry 可枚举派生 Workplace 和已完成 ProjectSite。岗位容量/名单只读查询 Assignment Authority。活动现场人数由实际 Presence 投影；Home、Commons、活动目的地和活动中项目 ID 尚非 Registry 时提供诊断投影，不注册、不落档。Issue 84/85/86 保持开放。
- 共享 VisualAsset 当前仅 6 张敌人图，无合适中性白模；RRPG_VisualAssets.xlsx 与 SourceAssets 无任务外 dirty。新增中性几何 PNG 和一条共享 VisualAsset，走 Compiler → Mirror → Builder → Catalog。Compiler 不支持独立 Village Presentation workbook；本轮采用窄范围表现键解析器，不造通用配置域。
- Events XLSX 有制作人 dirty，禁止用其当前内容重新导表。本轮在隔离根用 HEAD Events 和其它干净输入编译，核对非 VisualAsset/AssetManifest 输出不变后仅复制这两份正式 Compiler 产物。
- 既有存档目录已逐文件备份到 `Temp/V16A/save-backup`；回归测试会删除/改写单槽档，验证后恢复原有字节。
- Debug Layer 保留原 Query/Action 与 Simulation gating；仅新增可切换的视觉共存开关。正式实体位于独立 world 根，Inspector 使用 uGUI/TMP，只读，无 B 页面/导航。

## 当前执行状态

V16-A Foundation implemented / technical verified local；制作人实看与开发16.0复核 Pending。B/C1/C2/D 未授权 / 未开始。没有提交或推送。4 份最小授权归位文档曾显式暂存，但 commit/push 的自动审批被拒绝（目的地/文档外发授权不足），已请求明确授权；未收到回复，不绕过审批。最终撤销本轮暂存，所有本轮内容留在工作树供完整审阅。

## V16-A 实际对象与文件

下面路径均相对仓库根；配套 `.meta` 与 ProductHome 目录 meta 由 Unity 导入生成，不手工编辑。

| 文件 | 职责 |
|---|---|
| `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/ProductHomeSnapshot.cs` | 不可变、纯数据 Snapshot；防止可变集合泄漏；同文件含 HomeSelectionContext（None / Person / Place） |
| `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs` | 组合既有查询；实际现场人数与 Assignment / Project 名额分别展示；Home / 活动 / active project 地点仅投影并诊断 |
| 同目录 `HomePresentationResolver.cs` | `village.person.<intent>` / `village.place.<kind>` → `va_village_neutral_marker` → shared Catalog；缺资产诊断与中性 fallback |
| 同目录 `ProductHomeWorldHost.cs` | Town 自托管独立根；按稳定身份增量 reconcile；切换长期来源只重置表现布局；Simulation gating；世界点击及聚焦入口 |
| 同目录 `HomeWorldEntityView.cs` | SpriteRenderer、世界 TMP 名字、Collider2D、选中反馈；Away 隐藏保留引用；通勤视觉停在路程82%，实际 arrived 后 snap；不写模拟 |
| 同目录 `HomeWorldInspector.cs` | uGUI/TMP 只读 Selection Inspector；姓名、现职业ID、活动、地点、职责、状态；地点容量、名额和现场人物；按钮仅 Focus / Clear / Debug toggle |
| 同目录 `HomeWorldCamera.cs` | 正交白模工作相机；右键/中键 Pan、滚轮 Zoom、Focus；独立 layer 31、表现原点，不进 Save |
| `Assets/Scripts/UI/TownUI.cs`（工程根下） | 既有自托管入口追加 ProductHomeWorldHost，其他按钮 / Save / Return 行为不变 |
| `Assets/Scripts/Village/UI/VillageWorldLayer.cs` | 最小开关只控制旧 WorldCanvas 子视觉；CanvasVisible 与旧摘要/Onsite继续服从既有 Simulation，Onsite 写操作不复制进 Inspector |
| `Assets/Tests/EditMode/Village/ProductHomeFoundationTests.cs` | 10项：来源、Assignment / 容量 / 名额、Away、实际 Presence / Activity、无 mutation、只读 fallback、Selection、不可变集合、Resolver 和 Home / project 持久边界 |
| `Assets/Tests/PlayMode/Village/ProductHomeFoundationPlayTests.cs` | 7项：稳定 View / Away / Return / gating、实际到场、世界点击、Inspector、camera / Save / Load、Debug 独立性、真实身份移除、日内 Activity |
| `Development/Config/RRPG_VisualAssets.xlsx` | VisualAsset 表第11行追加中性资源，导航页 / 四行表头 / 6条敌人资源不变 |
| `Development/SourceAssets/Village/neutral_marker.png` | 几何白模源 PNG（96×96）；无正式角色或建筑美术 |
| `Development/Generated/StaticConfig/VisualAsset.json` / `AssetManifest.json` | 既有 Compiler 正式生成，两者之外的35份 Mirror 逐字节不变 |
| 当前快照、日志、交接、问题清单、DevelopmentSpecs README与本记录 | 技术状态与验收 Pending；不修改玩法/设计/Architecture 规则 |

## Preflight 差异与自审

- 无合适既有中性资源，因此新增共享 VisualAsset 一条；不创建 Village Presentation 或 Buildings workbook。
- Artifact Tool import/read/export 可用，但 render 在本机进程退出（code 1，无可用渲染诊断）。导出还会把已有空 shared string 错改成 `1` 并丢失 workbook view。没有把有损全包导出覆盖正式输入；仅将 Artifact 编写的新行转入原始包，沿用原第10行样式。已逐单元格核对原两页所有值，除 sheet2 新行外所有包部分保留原字节；Compiler OpenXML VALID / Build PASS。Excel 原生画面未验证，此限制只涉及工作簿预览，不影响实际资源链实证。
- 最终界面使用独立世界 SpriteRenderer / Camera 根，未改现有 TownCanvas 手调绑定；旧 Town 的名单 / HUD / 入口仍可见，界面整理留 B。本轮只是实体、选择、Inspector、相机的 A 白模承载。
- Known Home 不等于实际在家；Away 时 Home Place view 保留，但现场人数为0。Active Project 地点在非工作时段保留，名单与当前现场分开。使用实际 ProjectId，不把未完成项目伪造为已完成 `site_`；Issue85未关闭。
- 没有第二套 Clock / Arrival / FSM / Assignment / Capacity / Registry / Save / History。快照不初始化 Activity，也不调用会同步 Unavailable 的 Bridge.GetPersonAvailability。演示布局 / Selection / Camera / Entity 不进入存档，SaveVersion仍1。
- 未加入 Building、Grid、NavMesh、Top HUD、Bottom Navigation、Feed、Attention、正式人物/管理页面或最终美术。

## 测试与首次失败

| 运行 | PASS / 执行数 | 结果 |
|---|---:|---|
| 最终 V16-A EditMode（namespace RRPG.ProductHome.Tests） | 10/10 | PASS |
| 最终 V16-A PlayMode（同 namespace） | 7/7 | PASS |
| 既有 VillageStage1.Tests（补强地点前） | 177/177 | PASS |
| 既有 VillageStage1.PlayModeTests（补强地点前） | 29/29 | PASS |
| ExpeditionV4F.PlayModeTests | 13/13 | PASS |
| 最终 Full EditMode（最后 C# 修改后） | 1466/1466 | PASS，0失败、0跳过，1分54.245秒 |
| 最终 Full PlayMode（Full Edit 后串行） | 106/106 | PASS，0失败、0跳过，1分30.371秒 |

针对性 MCP Summary 的 TotalTests 报的是对应 mode 总发现量，不能冒充过滤后的执行数；本表专项执行数用 Passed + Failed + Skipped 的实际结果。早一轮 Full Edit 1465/1465 通过后补了 Known Home / active project 用例，重新跑最后1466/1466，不能用早一轮数字替代。

- 首次真正执行的 V16-A Edit 8/9：测试反射 `params object[]` 把 string[] 展开为字符串参数，修正显式 object cast，后续9/9；增加地点边界后10/10。
- MCP 0.89.0 class regex 要求 namespace，给新测试增加 namespace；域重载 / mode 初次 RetrieveTestList 的5秒 timeout 会报告 No tests found，重试后正常。这些发现失败与一次 Unknown / 0执行不算通过；未改 MCP 包。
- 真实画面自检发现按钮 label 未赋值，补正；名字、选中圈、地点、Inspector文字与三按钮最终 1920×1080 Screenshot 目视确认。新 Home 6名现有真实人物、10个地点，中性实体 `ResolvedSharedAsset=true`。
- Compiler 隔离根：Events 用 HEAD 原始二进制，其余干净输入加本轮 VisualAsset；Build errors=0、warnings=7（现有内容）；35份非目标 Mirror 不变。Unity StaticConfigBuilder.Build(false) Success=true，Catalog entries=7。未导出制作人的 Events dirty。
- 全量回归故障注入（无 Pool、无 Supply、Return partial failure、FinalSave failure等）产生预期 Error；测试均通过，不把这些注入日志隐藏为产品0错误。正常 Home 阶段重新清 Console后原生 `errorCount=0 / warningCount=0`，近期 MCP Error查询空、Exception查询空，`scriptCompilationFailed=false / isCompiling=false`。MCP 历史日志缓存仍保留旧记录，不能说其全历史列表为空。

## 制作人产品验收路径（尚未验收）

当前团结引擎 Play 停留真实 SampleScene → Town，VillageTime暂停、未写 Save；可直接验收：

1. Game View 中央有独立的绿色地点白模、带姓名的人物实体；不是旧 Debug 重绘列表。
2. 左键点人物：黄色选中反馈，右下 readonly Inspector 给出名字、职业ID、活动、实际地点、职责和状态。
3. 左键点地点：Inspector 显示容量、占用名额、现场人数与人物；Known Home 与实际现场区分。
4. 右键 / 中键拖动 Pan，滚轮 Zoom；选人物或地点后点“聚焦”；“取消选择”只清选择。
5. 左下“世界对照”开/关旧 Debug 视觉；当前 Onsite“现场”入口保留，产品 Inspector 不复制写操作。
6. 用既有“承诺管理”安排人物到岗位，关闭管理面板后用既有“继续”恢复时间：实际工作/通勤改变，人物引用不重建，Inspector依据实际Query而非动画报到场。
7. 既有卡册 / 出征准备关闭 Simulation 时产品世界整体停绘；返回恢复。Away/Return、同一 Home/Person view 引用、Load rebuild 已由专项真实 Town 测试验证，可在Test Runner复验 `StableEntity_AwayReturnAndSimulationGating`。

最终截图在本地忽略证据目录 `Temp/V16A/V16-A-home-final.png`。这是 Codex 产品画面验证，不代表制作人 Acceptance。退出当前 Play 后按正常 NewGame 或已有 Save → Continue 重入即可重建；本轮没有写入演示存档。

## Deferred / 收尾边界

只完成 A 技术交付，制作人 Acceptance 与开发16.0复核 Pending。B/C1/C2/D、最终布局、美术、完整页、实体与建筑正式位置后续接线均未开始。84/85/86仍开放，88仅 A Foundation implemented / B product whitebox pending，不关闭。

测试后 TMP 与原始保护清单不同，差异是新 glyph / atlas / rectangles；从本轮开工前备份撤回本轮增量并经Unity重新导入，最终17/17 SHA256一致，原先已存在的dirty字节保留。不是用HEAD覆盖制作人资产。开工前存档目录无文件；全量回归创建的单槽档转移到 `Temp/V16A/test-save-after-full.json`，原空目录恢复。旧 Onsite 回归生成的截图也移入本地忽略目录，未混入交付。

## Git / Working-tree facts

当前 branch / Target Branch 均为 main。Local HEAD 与缓存 origin/main 均为 `b2b2cb169a8c262b9acac7365dad815eb95a42f1`，祖先检查exit0。本轮没有 commit/push；暂存区空。开工时三方SHA已实核一致；收尾 remote 只读再核因网络失败（sandbox getaddrinfo；沙箱外 Empty reply from server），故不宣称收尾 GitHub/main 再核完成；在未来任何发布前须重新核验远端。

本轮31个路径：9个新C#（739行，含2个测试类）、2个既有C#最小修改、Unity生成的10个meta、1个XLSX / 2个Compiler JSON / 1个PNG，以及6份文档（含本记录）。另有17个任务外原始dirty/untracked路径，字节全部保留；不把它们计为任务交付。下面 whole-worktree diff stat 包含 Events / TMP / QualitySettings 原有dirty，且Git普通stat不包含untracked新增文件；实际新增对象详见上方文件表。原始MCP摘要保留于本地 `Temp/V16A/test-evidence.json`。

实时 git diff --stat HEAD：

```text
 Development/Config/RRPG_Events.xlsx                | Bin 34635 -> 27901 bytes
 Development/Config/RRPG_VisualAssets.xlsx          | Bin 10053 -> 10155 bytes
 .../Generated/StaticConfig/AssetManifest.json      |  14 +-
 .../Generated/StaticConfig/VisualAsset.json        |   9 +-
 Docs/DevelopmentSpecs/README.md                    |   2 +
 Docs/开发验收交接.md                               |   2 +
 Docs/问题清单.md                                   |   4 +-
 Docs/项目日志.md                                   |  17 +
 Docs/项目状态快照.md                               |  14 +-
 .../RRPG_SourceHanSansCN_Regular_TMP.asset         | 432 ++++++++++++++++++---
 .../RRPG1.0game/Assets/Scripts/UI/TownUI.cs        |   2 +
 .../Assets/Scripts/Village/UI/VillageWorldLayer.cs |  10 +
 .../ProjectSettings/QualitySettings.asset          | 144 +++++--
 13 files changed, 554 insertions(+), 96 deletions(-)
```

实时 git status --short --untracked-files=all（全工作树）：

```text
 M Development/Config/RRPG_Events.xlsx
 M Development/Config/RRPG_VisualAssets.xlsx
 M Development/Generated/StaticConfig/AssetManifest.json
 M Development/Generated/StaticConfig/VisualAsset.json
 M Docs/DevelopmentSpecs/README.md
 M Docs/开发验收交接.md
 M Docs/问题清单.md
 M Docs/项目日志.md
 M Docs/项目状态快照.md
 M UnityProject/RRPG1.0game/Assets/Art/Fonts/SourceHanSansCN/RRPG_SourceHanSansCN_Regular_TMP.asset
 M UnityProject/RRPG1.0game/Assets/Scripts/UI/TownUI.cs
 M UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageWorldLayer.cs
 M UnityProject/RRPG1.0game/ProjectSettings/QualitySettings.asset
?? 2026-10-04_RRPG_Autonomous-Life_Authority-Landing.diff
?? 2026-10-04_RRPG_WP-AL-1_Runtime-Review.diff
?? Development/SourceAssets/Village/neutral_marker.png
?? Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md
?? "UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_36-3.png"
?? "UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_36-3.png.meta"
?? "UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_43-6.png"
?? "UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_43-6.png.meta"
?? "UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月30日 22_34_47.png"
?? "UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月30日 22_34_47.png.meta"
?? UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/意图1.png
?? UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/意图1.png.meta
?? UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/黄血条1.png
?? UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/黄血条1.png.meta
?? "UnityProject/RRPG1.0game/Assets/ChatGPT 图像 2026年9月30日 22_39_36-3.png"
?? "UnityProject/RRPG1.0game/Assets/ChatGPT 图像 2026年9月30日 22_39_36-3.png.meta"
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome.meta
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs.meta
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldCamera.cs
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldCamera.cs.meta
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs.meta
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs.meta
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs.meta
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs.meta
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/ProductHomeSnapshot.cs
?? UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/ProductHomeSnapshot.cs.meta
?? UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/ProductHomeFoundationTests.cs
?? UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/ProductHomeFoundationTests.cs.meta
?? UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/ProductHomeFoundationPlayTests.cs
?? UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/ProductHomeFoundationPlayTests.cs.meta
```


## V16-A 制作人验收后速度调整（2026-10-07）

- 制作人明确确认此前所有验收项通过，并要求世界正常日长改为现实24分钟、增加0.5倍速。本节是该追加请求，不表示B/C/D授权或自动Closed。
- VillageTime 默认1440现实秒一天；显式倍率映射Normal=1、Slow=0.5、Fast=4、Paused=0。已有存档Speed标识0/1/4不变，新增Slow标识2；LegacySecondsPerDay=5仅保留旧存档迁移。
- HUD按钮读取权威速度，循环1×→4×→0.5×→1×，标签不再持有独立布尔档位；不改通勤路径、到岗条件、世界分钟规则、Activity或结算。
- 正常档1现实秒=1世界分钟；15世界分钟通勤在正常/半速下分别有15/30现实秒展示窗口，取代旧默认约0.052秒。
- 修改运行时代码：VillageTime.cs、VillageSaveData.cs（注释）、VillageHUD.cs；GameSpec补充此次制作人校准。测试覆盖三个日长、半速小数余量与Save/Load、真实场景按钮和标签；既有Intraday/PauseFast测试改用时钟日长而非写死5秒，RoundTrip断言世界时刻兼容。
- 首轮Village EditMode为181通过/1失败：旧测试将5秒fixture的SecondsIntoDay=2.5与默认日长相同解释，修正为比较世界时刻/余量与新校准下720秒。首轮Village PlayMode为29通过/1失败：旧两档循环断言缺少0.5档，已补齐三档验证。MCP首次测试发现超时仍会报告No tests found，重试处理，不计为PASS。
- 完整回归：EditMode 1470/1470（2分15.902秒）、PlayMode 107/107（1分37.506秒），零失败/零跳过；编译通过。测试前重新备份的17个任务外文件最终17/17 SHA256一致；测试存档清理，原空存档目录恢复；生成的D onsite截图移入本地证据目录。
- 最终真实Home现场：SecondsPerDay=1440、Multiplier=1、标签x1、Simulation=true；27.8现实秒推进27世界分钟，正常Console 0 Error / 0 Warning。工程留在Play Mode的1×Home供直接体验；未写入新演示存档，未暂存/commit/push。

## V16-A 归位移动与 HUD 点击拦截修复（2026-10-07）

- 制作人要求修复人物突然归位与部分人物无法点击。归位原因是ApplyPerson的非Traveling分支直接设置transform.position；行走只在Traveling时执行。现改为更新目标后持续MoveTowards；初次建立实体仍直接定位，途中仍不会提前表达实际到场，Simulation/Arrival规则不变。
- 点击问题重新核查：此前“重叠总选第一个人”的解释不足以解释全部无法选中。现场魏安身体位置命中VillageAvailabilityTitle的矩形，底层同时能检测到三个Person collider；临时关闭该文字raycastTarget后UI命中消失，恢复后再次拦截。人物行也有700宽的点击矩形。因此此次修复HUD所有展示TMP与动态人物行raycastTarget=false；按钮Image、现场面板与Inspector仍拦截自身区域，不关闭全局UI门禁。
- 新增两项PlayMode回归并加强已有实际到场测试：到场刷新前后位置不变，分步走完最后一段；无Traveling的地点变化和重复快照持续行走；原HUD汇总覆盖区域允许选中人物，控制按钮仍挡住世界点击且正常切换。表现步骤前后Domain JSON一致。
- targeted ProductHome PlayMode 9/9 PASS；VillageStage1.PlayModeTests 32/32 PASS，零失败/零跳过。编译通过。上一轮世界速度完整回归1470 EditMode/107 PlayMode是历史证据；本次未重跑全工程，验证范围为上述两项行为与Village场景集成。
- 本次开工前已有存档逐字节备份，回归后恢复原文件；17/17保护文件与本次开工前SHA256一致，TMP增字形撤回。未手工修改Scene/Prefab/Asset YAML，未暂存/commit/push。引擎恢复已有存档的Home供体验。
- 最终真实Home：WorldVisible=true、6人，原汇总文本区域PointerOverUI=false、HUD展示文字拦截数=0；原存档SHA/字节一致。诊断脚本曾因Object命名歧义编译失败（仅MCP动态诊断，正式脚本编译已通过），修正限定名后现场检查成功。
- 动态诊断错误已单列记录并清理；正常Home Console 0 Error / 0 Warning。

## V16-A 增加2/8/12/24倍速（2026-10-07）

- 制作人追加要求新增2、8、12、24倍速。当前按钮按0.5→1→2→4→8→12→24→0.5循环；暂停时点速度按钮恢复1倍。1倍日长仍1440现实秒；新增档位分别720/180/120/60秒一天。
- VillageTime集中定义实际倍率与CycleSpeed，HUD读取权威值。保留所有既有存档标识：0暂停、1正常、2半速、4四倍；新增两倍使用标识3，8/12/24使用同名数值标识。未改变SaveVersion、日内推进/结算/等待锁、实际到场或表现移动规则。
- 每个新增档位测试默认完整日长及小数分钟Save/Load后继续推进；场景验证完整七档循环与实际标签，既有TMP按钮回归同步更新。
- VillageStage1.Tests EditMode 190/190 PASS，VillageStage1.PlayModeTests 32/32 PASS，零失败/零跳过。编译通过，未重复全工程测试；首次MCP发现超时报告No tests found，重试后执行成功。
- 原存档文件和本次开工前17个保护文件已逐字节恢复。测试前将实时世界用现有SaveSystem.BuildSaveData只读构建至Temp，结束后通过现有Load恢复运行进度，再恢复原存档字节；不覆盖玩家原存档。未暂存/commit/push。

## V16-A 移动表现跟随世界倍率（2026-10-07）

- 制作人确认继续修复。此前世界时钟使用倍率，但Host使用unscaledDeltaTime直接驱动固定2单位/秒的实体移动；本轮Host统一使用与VillageTimeDriver相同的Time.deltaTime，并乘VillageTime.SpeedMultiplier。
- 暂停倍率0时冻结全部人物行走，包括到场后未走完的归位；Simulation gating关闭、Clock缺失或HasActiveAdvance时不接收新的行走delta。实体目标、Arrival、Presence、Activity与推进规则保持既有Authority。
- 新增PlayMode覆盖0.5/1/2/4/8/12/24倍率下0.25帧秒对应的移动距离、移动前后Domain JSON一致、真实Update暂停冻结、Resume正常恢复及Simulation关闭冻结。
- 首次targeted ProductHome为9通过/1失败（既有通勤测试的Traveling预期）；该通勤用例单独重跑1/1 PASS，随后完整VillageStage1.PlayModeTests 33/33 PASS（33.756秒），零失败/零跳过。新倍率测试在首轮与整组均PASS。未更改该既有用例来掩盖首次失败；未重复全工程回归。
- 本次原存档和实时世界开工前分别备份至Temp；测试后通过既有Load恢复实时进度，再恢复原存档字节。17/17保护文件SHA256不变，TMP自动增字形已撤回；无Scene/Prefab手工修改、无暂存/commit/push。

## V16-A Final Closeout（2026-10-07）

- 制作人已确认既有全部验收项通过，接受日长/七档速度、归位/点击及移动倍率修复后明确“好的完成，收尾”。按原实施提示词§16的“可以收尾”条件，本轮Runtime与必要文档/资源的commit、push授权成立；Target Branch=main，origin=https://github.com/LINQI2/RRPG1.0W.git。
- Codex自审完成；未伪造独立开发16.0复核记录。A交付只读Adapter/Snapshot、持久人物/地点、Selection、Inspector/Camera、中性共享VisualAsset及本轮修复。B/C1/C2/D仍未授权；Issue88仅A Foundation完成，B待授权，84/85/86保持开放。
- Disposable分类：ProductHome专项EditMode从10项压缩至4项（长期PersonId、Assignment与Arrival隔离、Snapshot/Selection不得写Runtime/Registry、不可变DTO）；PlayMode从10项压缩至3项（持久身份与离村显隐/gating、模拟到场唯一权威与表现不改时钟、表现状态不进入Save及Load重建）。删去6+7项一次性Acceptance、另删去重复七档HUD专项验收；既有按钮回归和Clock/Save编码兼容回归保留。一次性测试源码备份、日志、截图只在本地忽略Temp，不提交。
- 清理后最后一次完整回归：EditMode1472/1472（1分54.077秒），PlayMode102/102（1分30.219秒），零失败/零跳过。MCP初次测试发现超时单列，重试结果才计PASS；C#语义修改后已完整覆盖。另清理了已删除专项测试留下的一行空白，无语义变化。
- 授权发布前只读远端核验成功：local HEAD / cached origin/main / GitHub main均为b2b2cb169a8c262b9acac7365dad815eb95a42f1。仅绕过本机失效代理执行命令，不修改Git配置。
- Final Closeout内容与发布清单已准备；本节生成时尚未commit/push，不预写未来Closure SHA或Closed。最终发布事实追加在后续核验记录。

### Closure Commit Staging Manifest

Must Stage
- `Development/Config/RRPG_VisualAssets.xlsx`
- `Development/Generated/StaticConfig/AssetManifest.json`
- `Development/Generated/StaticConfig/VisualAsset.json`
- `Development/SourceAssets/Village/neutral_marker.png`
- `Docs/开发验收交接.md`
- `Docs/文档地图.md`
- `Docs/问题清单.md`
- `Docs/项目日志.md`
- `Docs/项目状态快照.md`
- `Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md`
- `Docs/DevelopmentSpecs/README.md`
- `GameSpec/Village/Product-Home-World-GameSpec-V0.1.md`
- `README.md`
- `UnityProject/RRPG1.0game/Assets/Scripts/UI/TownUI.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldCamera.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldCamera.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/ProductHomeSnapshot.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/ProductHomeSnapshot.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageSaveData.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageTime.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageHUD.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageWorldLayer.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/IntradayTimeTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/ProductHomeFoundationTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/ProductHomeFoundationTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/VillageTimeTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/ProductHomeFoundationPlayTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/ProductHomeFoundationPlayTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageCommitmentProductIntegrationPlayTests.cs`

Must Not Stage
- `Development/Config/RRPG_Events.xlsx`
- `UnityProject/RRPG1.0game/Assets/Art/Fonts/SourceHanSansCN/RRPG_SourceHanSansCN_Regular_TMP.asset`
- `UnityProject/RRPG1.0game/ProjectSettings/QualitySettings.asset`
- `2026-10-04_RRPG_Autonomous-Life_Authority-Landing.diff`
- `2026-10-04_RRPG_WP-AL-1_Runtime-Review.diff`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_36-3.png`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_36-3.png.meta`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_43-6.png`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月29日 21_40_43-6.png.meta`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月30日 22_34_47.png`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/ChatGPT 图像 2026年9月30日 22_34_47.png.meta`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/意图1.png`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/意图1.png.meta`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/黄血条1.png`
- `UnityProject/RRPG1.0game/Assets/Art/UI/Battle/UnitHUD/黄血条1.png.meta`
- `UnityProject/RRPG1.0game/Assets/ChatGPT 图像 2026年9月30日 22_39_36-3.png`
- `UnityProject/RRPG1.0game/Assets/ChatGPT 图像 2026年9月30日 22_39_36-3.png.meta`
- `UnityProject/RRPG1.0game/Packages/manifest.json`
- `UnityProject/RRPG1.0game/Packages/packages-lock.json`
- Temp / TestResults / 测试日志与截图（本地证据，不提交）

Expected staged file count: 40

最终发布前检查：40路径本地Markdown链接与git diff --check通过；Handoff 71已实际落盘并回读。正常Home可见、6人、非编译，Console 0 Error / 0 Warning；实时世界恢复，原存档字节一致。17/17保护文件一致。
暂存后新增meta的diff检查例外：Unity生成的10份meta在空userData/assetBundleName/assetBundleVariant字段保留标准尾空格，共30处；不手工改写序列化文件。排除meta后的完整staged diff --check通过；所有非meta文件无空白错误。

## V16-A 发布核验（2026-10-07）

- Closure Commit：3ec724b6a5bf76f9f39c188eede1e7e10d54f82d，feat(village): deliver V16-A product world foundation；精确40路径，1278插入/59删除。已正常push origin main（b2b2cb1→3ec724b），未force。
- 发布后local HEAD、origin/main、GitHub refs/heads/main三方均为上述SHA，暂存区空，本任务40路径无未提交变化；17/17受保护文件SHA256一致。
- V16-A Work Package Closed / Published Main。共享工作区仍有19个任务外dirty/untracked路径（原17项与Packages连接更新2项），保留，不宣称Branch Closed / Repository Closed。Issue88仍开放且B未授权；84/85/86不关闭。
- 为确保Current状态基于已发生的push/远端核验，追加一个仅含本记录、状态快照、日志、问题清单、DevelopmentSpecs索引的5路径发布证据同步提交；不改变Runtime、测试或资源，不重跑Unity。Handoff71保持生成时Git快照，不回填未来SHA。

## V16-B Home Product Whitebox（2026-10-07，本地待产品验收）

### 授权与现场

制作人提供 V16-B 实施提示词并要求执行。Target Branch / 当前分支 main，开工 local HEAD 与 origin/main 均为 `ee0296bf4a5b618bc5cda443ea46e2421ee02735`，祖先检查通过；实时 remote main 查询因本机代理 `127.0.0.1` 连接失败，未核验远端，不借 A 的历史远端证据声称本轮已核验。仅实施 B，不进入 C1/C2/D。

19 个开工时任务外 dirty/untracked 路径逐字节备份到已忽略的 `Temp/V16B/protected`，SHA256 清单 `Temp/V16B/protection.json`；既有 `rrpg_save_p0.json` 另备份到 `Temp/V16B/save-backup`。没有 reset、clean、暂存、commit 或 push。TMP 测试期自动增字形须按本轮备份恢复，不从 HEAD 恢复制作人 dirty。

已发布 V16 链中，一个 Gate 经制作人/开发16.0验收关闭后，开发16.0可直接进入下一已规划 Gate；执行 Agent 仍只执行当前 Prompt 指定 Gate。本轮只在最终 Producer B Product Acceptance 请求产品判断，不将技术 PASS 写成 B Closed / Review PASS。

### 实现与边界

- 复用 A 的 `ProductHomeWorldHost`、真实 `ProductHomeReadAdapter`、持久 Person/Place、Selection、Camera、中性共享 VisualAsset 和只读 Inspector；不创建第二套 Simulation、Presence、Assignment、Registry 或 Save Authority。
- `HomeProductShell` 挂在既有 Town 创建的 Product Home Host 下。Top HUD 读取 VillageTime 日期/日内时间、Pause/Resume、现行七档倍速，以及 Resources 的 Food/Ingredients；不显示 JumpMonth/TestState/全员 Availability/Gold/卡数。
- Router 仅保存 World/Person/Management/Cards/History 的会话表现状态。四个内部面板明确标注只读白模、保留世界与 Top HUD，不暂停或改 SimulationActive。远征委托既有 TownUI.OnExpedition 合法离开路径；Build Disabled，不做假建造。
- 默认隐藏旧 Town info/Gold/人物数/道具数/Deck/成长红点入口/管理入口/VillageHUD，旧世界和现场入口只在“调试 / 对照”路径显示；不删除兼容业务与事件 UI，不把 TownCanvas 整体关闭。
- Person 默认只有名字，Hover 增加单行 Activity，Selected 用独立选中框与右下 Inspector，Attention 用独立橙色标记。Place 默认无名称，Hover/Selected 才显示。按地点分组分槽排列人物表现位置，不写模拟空间事实；UI Raycast 阻断 world click。
- Attention 只读 `CharacterGrowthService.CanLevelUp` 与 `EventRuntimeBridge.PendingVillageEvent`，项包含 Subject/Id、Type/Message、Target、Navigation Intent；不读旧 UI 红点，不造警报阈值，不初始化或写成长。成长导航到 Person 白模；Pending Event 导航到既有处理 UI。
- Feed 观察 ActivityRuntimeBridge 已成功完成且有 CompletionSnapshot 的薄接缝，按 ActivityInstanceId 去重、最近三条、换会话清空；订阅者异常不能改变 Domain 完成结果。不做 Snapshot diff、不写/复制长期 History。未扩张到其它 occurrence producer。
- Inspector 继续只读 Selection+Snapshot，Person/Place 共框架，未选择收起，Focus/Clear 和白模导航均只影响表现。Home Shell/Inspector Canvas 70/80，低于既有成长模态100与事件模态211，避免穿透。
- 12 人压力 Fixture 仅在 PlayMode 测试程序集，四地点覆盖劳动/途中/交谈/休息；Editor-only `ApplyVisualQaSnapshot` 只用于临时截图，正常 Production 永远读取真实 adapter，无假人补足逻辑、无 PlayerCollection/Save 写入。

### 验证与首次失败

Unity 现场为 Tuanjie 2022.3.62t9，真实项目 `UnityProject/RRPG1.0game`。编译、测试、Play 状态与截图通过 Unity MCP；测试发现冷启动超出 MCP 五秒列表回调时会错误显示 No tests found，使用 Editor TestRunnerApi 预热列表后再执行，失败的发现调用不算 PASS。没有专用方法调用工具时用 script_execute 调用既有产品入口/测试 Fixture，未手工编辑 Scene/Prefab/序列化资源。

专项：A EditMode 4/4 PASS；B EditMode 4/4 PASS；B PlayMode 5/5 PASS。检查覆盖只读 Attention、显式完成 Feed、去重/限长/会话清空、标签层次、真实 Town 默认退场、世界点击/Inspector/UI防穿透、Pause与七速、四内部页持续模拟、远征退出与Build禁用。

首次失败及修复：

1. B 测试反射把 SimulationActive 字段当属性、假设默认 Hero 列表有角色、跨程序集查询 GrowthConfig 类型失败；改为字段读取、显式测试人物、跨程序集查找，不改变玩法。
2. 内部页退出远征后 Shell 可滞留一帧；Shell 直接读取现行 SimulationActive 并在远征出口即时隐藏。
3. 首轮全量 PlayMode 102/107：五项既有测试默认查旧 HUD/名单/远征/现场入口。兼容检查改走真实 MainMenu→Town 并显式开启调试对照；模态检查改点真实 Home Navigate5，保留原射线遮挡断言。
4. 新射线检查暴露 Home Canvas 高于成长模态的真实问题；将 Shell/Inspector 置于既有模态下方后修复。
5. 后续 106/107 的 A 行走测试依赖随机身份排序，首人可能已分配农场，导致无实际旅程或 AlreadyAssigned；使用合法 Assignment 移除/分配 API 先到厨房再调往农场，保留时钟/Save 不变、未到场/实际到场及表现不瞬移断言。A PlayMode 专项最终3/3 PASS。

### 产品验收路径与保留项

正常入口：SampleScene → Play → MainMenu Continue → Home。观察默认世界、顶部日期/时间/暂停/倍速/资源/关注；点击人物或地点查看右侧 Inspector，Focus/Clear；底部依次打开人物/管理/卡牌/历史并确认时钟持续，关闭回世界；远征是离开 Home，建造禁用。调试/对照只作旧功能兼容查验，验收默认保持关闭。

最终产品判断仍按提示词§15：第一眼 Village；12人非文字墙；默认/选中/关注可区分；HUD不抢World；Inspector不是第二套管理页；导航清楚；Feed轻量；内部页同一Home且模拟继续。自动测试不代替该判断。

Deferred：完整Person/Management/Cards/History业务页面、正式美术、更多Feed生产者、Building与C1/C2/D、任何新警报规则。Issue88保持开放，84/85/86不关闭。临时白模专项/Fixture在产品验收 Pending 期间保留供复查，Final Closeout按Disposable规则分类清理；本轮未覆盖最终Handoff。

### B 最终技术现场（验收前）

- **B implemented / technical verified / Producer Product Acceptance Pending**。最终完整 EditMode **1476/1476 PASS**（1:51.763），完整 PlayMode **107/107 PASS**（1:35.455），零失败/跳过。数据为本轮发现及执行数，不引用历史数；最后Runtime变更为模态Canvas层级，最后C#变更为A行走测试前置。
- 正常 SampleScene Play → MainMenu Continue 已恢复原存档，真实6人，Shell可见，Compatibility关闭，Fixture标签/12人/测试Feed均已通过退出Play清除。VillageTime暂停便于实看，顶部“继续”可恢复；无需额外人工Unity配置。
- `scriptCompilationFailed=False`；清理已记录的测试注入与工具调试日志后，正常Home的Unity原生Console **0 Error / 0 Warning**。MCP历史日志缓冲仍含已通过测试的预期失败注入及脚本调试错误；最后一次Console计数脚本先误用不存在的GetCounts，改为回读GetCountsByType后成功，非Runtime异常。
- 1920×1080 Game View与落盘PNG均已目视核对：`Temp/V16B/01-real-home.png`、`02-real-selected.png`、`03-real-cards.png`、`04-test-only-pressure12.png`。压力画面明确标注TEST ONLY，12人全部在画面内、默认单行姓名/选中/橙色关注/行走蓝色/单条Hover活动及三条动态分别可辨。初次Fixture截图沿用了真实世界锚点而超出画面，重置测试表现锚点、重拍后核对；未改Production数据。截图只作本地证据，不提交。
- 原始存档最终 SHA256 `B096DC74C22729DF0710568AE892A4C2B1BE2806DB8B8A52BEDA814A986D8304` 与本轮备份一致。测试删除原路径后，使用受审执行权限恢复该具名文件；没有审批拒绝。
- **17/19开工保护路径最终字节一致**（包含TMP已从本轮副本恢复）；另外2项Packages不是B写入，现场出现 `cn.tuanjie.codely.bridge 1.0.80→1.0.85` 与Generators本地路径更新。开工副本和更新副本均保存在Temp，保留现场外部更新，不覆盖、不计B改动、不宣称19/19一致。更新manifest SHA256 `18BB9B391D8BB86097DC78669071BBCC044FDA5D15E9692C56BE8D29ABDA495A`，lock `ADA3807DE8A53EC183F171C0C8786A6CB7018B84BAED7CCBE919ABFEE3A860D2`。
- HEAD/origin/main仍为开工SHA，暂存区空；本轮没有commit/push。Runtime、测试及下列状态材料均为本地改动，不写Closed/Published。README/文档地图/索引/快照同步当前B状态，日志记本轮事实，问题清单保持88/84/85/86开放；Handoff只改顶部当前任务提醒，A最终交接正文不覆盖。GameSpec/GameDesign/Architecture无需由白模实现反推改写。

### B 当前文件清单（29路径，本地待验收；不是Closure Staging授权）

```text
README.md
Docs/文档地图.md
Docs/项目状态快照.md
Docs/项目日志.md
Docs/问题清单.md
Docs/开发验收交接.md
Docs/DevelopmentSpecs/README.md
Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md
UnityProject/RRPG1.0game/Assets/Scripts/ActivityHost/ActivityRuntimeBridge.cs
UnityProject/RRPG1.0game/Assets/Scripts/UI/TownUI.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductProjection.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductProjection.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductUI.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductUI.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageWorldLayer.Onsite.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/HomeProductWhiteboxTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/HomeProductWhiteboxTests.cs.meta
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/HomeProductWhiteboxPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/HomeProductWhiteboxPlayTests.cs.meta
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/CharacterGrowthPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/IntradayOnsiteProductPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/ProductHomeFoundationPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageCommitmentProductIntegrationPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageHUDIntegrationPlayTests.cs
```



5份新增meta由Unity生成，不手写。Must Not Stage = 本节开工19保护路径（同A清单）与Temp、TestResults、截图/测试日志；兼容测试产生的D-onsite-product.png已移到忽略Temp，测试Runner临时场景已由Runner清除。当前清单保留待验收Disposable专项，Final Closeout需重新核对实际分类与路径数。

验收前最终回读：实际B差异与29路径清单一致，无遗漏或额外测试产物；新增/修改行内26个本地Markdown链接全部存在；任务内tracked diff --check通过，暂存区空。当前Editor在真实SampleScene Play，非编译/更新，正常Home停留供制作人查看。

## V16-B Producer Acceptance / Final Closeout（2026-10-07）

制作人明确“验收通过收尾”。B Product Acceptance Passed，当前任务范围的收尾提交与Push授权按固定口令执行，Target Branch仍为main；不启动C1。验收前的Pending状态、29路径与测试数保留为当时证据，不作为清理后的最终事实。

Disposable分类已执行：删除5项B PlayMode一次性产品验收及12人Fixture（连同Unity生成meta由AssetDatabase删除），移除Runtime的Editor-only ApplyVisualQaSnapshot接口；EditMode删除2项Router枚举/Feed限长白模验收，只保留Attention纯Domain查询不写Player/World、显式Activity完成一次投影且不写History的2项最小Authority回归。既有5份PlayMode回归迁移保留，用于既有单Clock/Presence/模态阻断与兼容入口。专项原文副本与截图只留本地忽略Temp，不提交；Production无Fixture依赖。无需再次产品验收。

清理后完整EditMode1474/1474 PASS（1:57.239）、完整PlayMode102/102 PASS（1:36.502），零失败/零跳过；编译通过。正常Home原存档恢复、真实6人、Compatibility关闭，原生Console0 Error/0 Warning，QA注入接口已不存在。17个保护文件仍一致，2个Packages外部更新保留；原存档SHA256仍与本轮备份一致。实时GitHub main已通过单次直连核验为ee0296bf4a5b618bc5cda443ea46e2421ee02735，与本地及origin/main一致；未修改持久代理配置。Handoff72实际落盘，生成时状态为Acceptance Passed / Final Closeout Prepared，commit/push尚未执行。

### Closure Commit Staging Manifest（27路径）

下列路径覆盖Runtime、必要长期回归与状态/Handoff；无删除既有tracked测试，清理的新一次性文件从未入Git。Must Not Stage仍为开工19任务外路径、Temp、TestResults与截图/日志。Expected staged file count = 27。

提交前自审：Handoff72正文已回读并核对diff；实际任务27路径与Manifest一致，新增/修改行本地Markdown链接均有效，任务tracked diff --check通过；27路径之外的19保护路径保持排除，暂存区在生成Handoff时为空。无Scene/Prefab、Package、ProjectSettings或Building任务变化。

暂存核验：精确27路径，无任务外路径；完整staged diff --check仅4份Unity生成meta的空userData/assetBundleName/assetBundleVariant字段有12处标准尾空格，不手改序列化文件。排除meta后的staged diff --check通过，所有非meta任务文件无空白错误。

```text
README.md
Docs/文档地图.md
Docs/项目状态快照.md
Docs/项目日志.md
Docs/问题清单.md
Docs/开发验收交接.md
Docs/DevelopmentSpecs/README.md
Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md
UnityProject/RRPG1.0game/Assets/Scripts/ActivityHost/ActivityRuntimeBridge.cs
UnityProject/RRPG1.0game/Assets/Scripts/UI/TownUI.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductProjection.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductProjection.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductUI.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductUI.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageWorldLayer.Onsite.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/HomeProductWhiteboxTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/HomeProductWhiteboxTests.cs.meta
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/CharacterGrowthPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/IntradayOnsiteProductPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/ProductHomeFoundationPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageCommitmentProductIntegrationPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageHUDIntegrationPlayTests.cs
```

## V16-B 发布核验（2026-10-07）

- Closure Commit `9f18ac855203ac50b2da70eeee909afd5aeb4f66`：feat(village): deliver accepted V16-B home product whitebox；精确27路径，660插入/57删除。已正常Push origin main（ee0296b→9f18ac8），未force。
- 推送后local HEAD、origin/main、实时GitHub refs/heads/main三方均为上述SHA；暂存区空，本任务27路径无未发布差异，只有19个任务外dirty/untracked保留。17个保护文件与B开工字节一致，2个Packages外部更新保持本轮记录的现场状态，不覆盖、不暂存。
- B Product Acceptance Passed / Work Package Closed / Published Main。Branch/Repository不宣称Closed；C1/C2/D未启动，Issue88及84/85/86保持开放。最终清理后EditMode1474/1474、PlayMode102/102 PASS，正常Home恢复、原生Console0 Error/0 Warning；Handoff72保留生成时Git快照。
- 为使Current状态基于实际发布，追加仅含README、文档地图、项目状态快照、项目日志、问题清单、DevelopmentSpecs索引与本记录的7路径发布证据同步提交；不改变Runtime、测试或资源，不重跑Unity。


## V16-C1 开工（2026-10-07）

制作人明确执行 C1 实施提示词，当前仅 C1。B 已验收发布，C2 / D 未启动，不跨 Gate。按 Bootstrap 与领域 Authority 恢复，Existing Host Map 复用现有 Compiler、StaticConfig Builder / Registry、VillageRuntimeBridge、SaveSystem 与 ProductHomeWorldHost / Shell / Inspector；不新建 Scene 或第二套 Home。

开工 main：local HEAD / origin/main / 实时 GitHub refs/heads/main 均为 `2a2dec7cb187d50fc3fa76795633bf9b73dfa52e`。19项任务外路径已 SHA256 + 字节备份至忽略 Temp/V16C1/protected，原存档另备份。无 reset / clean / 覆盖任务外工作簿，无暂存。原生 Console 0 Error / 0 Warning，编译通过。

配置策略：新建唯一 RRPG_Buildings.xlsx，声明式 Row3 >=1 沿通用 Compiler。因 RRPG_Events.xlsx 为已有未提交改动，在隔离 config-root 中以 HEAD Events 基线生成，并仅复制 Compiler 产生的 BuildingDefinition.json；其它工作簿、Mirror 不重写。4种白模定义，非最终内容或数值。Artifact Tool 成功生成并读取工作簿，PNG 渲染未产生文件，保留此限制，后续核对实际 OpenXML / Compiler 与产品目录。

Save audit：SaveVersion 保持1，Village.Buildings可空增量。实际JSON路径识别缺失/null，旧档为空；完整预校验准备新Runtime，直到所有既有Load步骤成功后才发布建筑替换，异常或失败不会部分修改当前Buildings。实例ID为独立单调序号，Definition与Instance分离；坐标为Village-local，不存Home WorldOrigin。C1不创建Place / Project / Workplace，不完成施工。


## V16-C1 技术交付 / Producer Gate（2026-10-08）

### 已实现链与边界

`RRPG_Buildings.xlsx / BuildingDefinition → generic Compiler → BuildingDefinition.json → StaticConfigBuilder → BuildingConfigDatabase / RRPGStaticConfigRoot → StaticConfigRegistry.TryGetBuildings / BuildingRegistry → Home Catalog → Ghost → BuildingPlacementValidator → BuildingRuntime / BuildingInstance → HomeBuildingSnapshot / World View → Village.Buildings Save / Load`。

五字段身份、名称、分类、宽、深；4种白模，A列遵循现有制作人说明栏，B起机器字段，Row3正整数约束。未新建VillagePresentation workbook。Compiler隔离根生成后只取BuildingDefinition Mirror；其它现有Mirror无diff。Missing/empty数据库诊断，重复定义或非法Footprint拒绝。Builder生成库挂唯一Root并纳入签名/no-op/managed-root所有权；最终实际Build成功且NoOp=True，Root含4种定义。

BuildingRuntime为唯一实例集合，实例对象只读，不由UI列表反向拥有。`building_instance_<sequence>`独立稳定身份，NextSequence随Buildings一起保存；同Definition创建两座实例、位置与身份独立；失败命令不分配ID。居中轴对齐Footprint，白模范围X[-12,12]/Y[-10,8]，矩形相邻合法，越界/重叠无实例；Preview和Command共享Validator，Command基于当前实例集合重新检查。没有Grid、旋转、Road、Terrain、Nav、Cost或Unlock。

Building位置只存Village-local坐标；BuildingWorldMapper是唯一local↔Home WorldOrigin的转换。矩形占地线精确显示Footprint，内部复用共享中性Sprite；视觉替换不改变实例或Save。HomeSelectionKind.Building独立于Place；同一Host/Selection/Inspector展示名称、UnderConstruction与C2未接功能提示，没有Worker / Progress / Assignment / Complete按钮。确认新实例仅UnderConstruction，不创建Place / Project / Construction Binding / Workplace，不产出或扣资源。

SaveVersion1：Village.Buildings nullable additive block，只含必要实例事实与allocator。检查真实JSON属性路径，旧缺失/null不借JsonUtility零值对象伪造事实；未知Definition fail closed并诊断；完整准备候选建筑Runtime直到Load最后成功才发布。Save也预检Buildings；NewGame为空、allocator重置。没有Ghost / Candidate / Selection / Camera / VisualAsset / 表现offset落档。

### 本轮验证与首次问题

| 验证 | 实际结果 |
|---|---|
| Compiler完整测试 | 254/254 PASS，0失败/0跳过；Building schema/重复身份/非正Footprint/列顺序/缺列/确定性Mirror覆盖 |
| 正式Building workbook隔离Compiler build | PASS，0 errors；8 warnings全部是既有机制WB004导航sheet跳过，不是数据问题 |
| C1 targeted EditMode | 11/11 PASS，包含Builder/Root/Registry、identity/roundtrip、bounds/overlap/adjacency/stale preview、旧null/未知定义/allocator/local mapper |
| C1 targeted PlayMode | 最终6/6 PASS，真实SampleScene NewGame→Save→Continue→Home；最后补强后再次6/6 PASS |
| 完整EditMode（串行） | 首次1485/1485 PASS（2:12.234）；矩形占地/提示修正后1485/1485 PASS（2:06.283） |
| 完整PlayMode（串行） | 首次108/108 PASS（1:38.319）；最终表现修正/保存反馈文案后108/108 PASS（1:32.405） |
| 正常真实Home | Compile 0 Error；原生Console 0 Error / 0 Warning；6人、2建筑、0 Active Project，Place Registry仍4 |

完整回归实际包含A/B Home、Save、Village/Intraday、Assignment、Project与StaticConfig Builder既有回归，不用历史PASS冒充本轮。最后仅加强现有Play测试的失败Load断言，不改Runtime；新断言加入后的C1整类6/6通过，未重复完整回归。

首次问题保留：Compiler本地测试宿主90秒连接超时、中止，解除本地宿主连接限制复跑254/254 PASS。新测试最初缺StaticConfig程序集引用，补到现有Village EditMode asmdef；Play fixture误写Projects.Records改ActiveProjects；初次C1 Play为5/6，IsSimulationActive误按property反射导致测试NullReference，修为实际field后6/6。MCP TestList首次5秒发现窗口返回NoTests，先warm-up正确模式树后执行，不计为测试失败或PASS。首次真实Home调用自动审批超时，按工具允许单次重试后成功；没有审批拒绝。画面实看发现说明文字截断，缩短并确认三行完整；补矩形Footprint线。Artifact Tool成功导出XLSX并读回关键值，PNG render未成功，本轮未把工作簿PNG视觉校验标PASS；实际OpenXML / Compiler schema与正式Catalog验证通过。

产品测试覆盖目录/选择、实际Screen→local pointer投影、Ghost不创建实例、取消、bounds/overlap、每确认一次一个实例、同类两实例、Building click/Inspector/focus、Person/Place与内部Surface、无Clock Authority、新实例无Place/Project/Function、Save→重新场景Continue identity/pos/lifecycle精确一致、allocator继续、旧档缺Buildings、NewGame reset、未知定义/失败Load不改当前实例。最后失败Load断言特意使文件只有1实例而当前有2实例，再在建筑候选准备后的Time校验拒绝，仍保留当前2实例及allocator，防止同数据假阳性。

### 实看与可验收现场

已用既有MainMenuUI.OnContinue产品方法进入原6人存档，实际建造命令创建2座房屋，local(2,-5)/(5,-5)，ID building_instance_1/2。通过建造产品Save入口保存，再加载SampleScene并走Continue，事实JSON精确相同；无重新分配或坐标offset污染。重新点击建筑选择与Inspector正确；绿Ghost、overlap红Ghost/拒绝、bounds红Ghost/拒绝实际画面均检查。截图仅忽略Temp/V16C1/C1-green.png、C1-overlap.png、C1-bounds.png，不提交。

截图期间仅短暂禁用Home Host输入协调，避免现实鼠标位置覆盖静态捕获；随后已恢复hostEnabled=True / WorldVisible=True与正常输入，未加Runtime QA注入接口。首次Continue同帧暂停Host产生Debug旧画面，未作为产品证据；恢复一帧真实Home后重新捕获正确产品画面。最终建造目录开启、没有Ghost、第二座Building被选中，VillageTime暂停供制作人检查。

原存档已字节恢复，SHA256 `B096DC74C22729DF0710568AE892A4C2B1BE2806DB8B8A52BEDA814A986D8304`与本轮备份相同；现场2座建筑是已验证Save/Continue的真实Runtime实例，未保存在当前原存档文件。验收Save/Continue时可点“保存村庄”，再重新Play→继续；原档备份及C1演示存档只留Temp。正常现场19/19任务外保护文件SHA256均与C1开工相同，包括字体原dirty内容。没有覆盖其它工作簿或Package/Settings，没有Scene/Prefab变更。

### 当前状态 / Git / Deferred

C1 Technical Check PASS / Local Implemented / Producer C1 Acceptance Pending；未宣称Work Package Closed，不覆盖B的Handoff72。按照C1提示词§18/§20停在唯一制作人产品Gate，下一步只由制作人确认；不进入C2 / D。84/85/86/88保持开放，Architecture/术语只更新实际C1及已发布A/B，GameSpec无普通实现改写。

main HEAD仍`2a2dec7cb187d50fc3fa76795633bf9b73dfa52e`，暂存区为空，没有C1 Commit/Push；任务外19路径完全排除。当前C1路径清单为52项，写在忽略Temp/V16C1/task-paths.json供验收后清理/精确暂存使用；不是Final Staging Manifest。专项测试/Fixture属于Disposable候选，Producer验收后按规则清理，再回归与生成正式Manifest，不提前保留为长期历史。没有启动施工、功能、建筑资源/经济、正式美术或长期范围。

最终归位核对：移走完整回归生成的未忽略TestResults/D-onsite-product.png至Temp，不纳入任务；最终任务清单52路径。Current Snapshot已移除旧B开工/本轮边界的冲突措辞，README / 文档地图 / 索引当前指向C1产品验收。最终QA补存C1-acceptance.png，输入协调已恢复，原生Console仍0 Error / 0 Warning。

## V16-C1 Producer Acceptance / Final Closeout（2026-10-08）

制作人明确“V16-C1 制作人验收通过，收尾”，授权清理、回归、仅暂存C1、Commit与Push main；不实施C2，后续由开发16.0安排。Target Branch=main。下述为生成时准备事实，发布后追加实时SHA核验，不预写Closed。

### 清理与清理后完整验证

删除8个一次性EditMode（含白模目录/缺库/固定bounds/mapper/UI字段验收）、5个产品PlayMode与2个Compiler用例；保留Compiler4个确定性/身份/正Footprint契约，EditMode3个独立identity+旧null+allocator往返、重叠/相邻+stale-preview command重验、未知定义/allocator失败隔离契约，PlayMode1个真实产品初始化后通过领域命令创建建筑、未知定义/版本/后续Time校验拒绝Load仍保留当前建筑的契约。移除StaticConfig.Editor测试依赖，保留StaticConfig.Runtime；不存在一次性Runtime注入接口，SetCandidate/UpdatePointer为真实产品调用。历史验收源码只在忽略Temp保留，不作为交付。

| 清理后验证 | 本轮结果 |
|---|---|
| Compiler完整回归 | 252/252 PASS，0失败/0跳过 |
| 正式配置隔离validate | PASS，0 errors / 8 WB004导航sheet跳过，Mirror不写入 |
| Unity脚本编译 | compileFailed=False；原SampleScene |
| 完整EditMode | 1477/1477 PASS，0失败/0跳过，2:01.055 |
| 完整PlayMode | 103/103 PASS，0失败/0跳过；时长以忽略Temp本轮响应为准 |

本轮开始Editor已关闭，自动启动原工程后通过已配置localhost:29387/mcp实际Editor工具恢复连接，未改Package/Settings。启动Console基线0 Error/636 Warning，清理编译后0 Error/520 Warning；完整测试产生其预期负例日志，不把负例计数冒充正常产品Console。回归后Builder/恢复原档/原字体/正常Home与Console的最终事实继续记录于下方。

### Closure Commit Staging Manifest

生成时local HEAD / cached origin/main / 实时GitHub main均为 `2a2dec7cb187d50fc3fa76795633bf9b73dfa52e`；暂存区为空，Commit/Push尚未执行。Handoff73实际写入并回读，按生成时事实保留。精确52路径（29既有修改、23新增，包含10对Unity源码/meta；不包含Scene/Prefab、Packages/Settings、原始存档、19项保护变化、Temp或TestResults）：

- `Development/Config/README.md`
- `Development/Config/RRPG_Buildings.xlsx`
- `Development/Generated/StaticConfig/BuildingDefinition.json`
- `Docs/Architecture/技术架构总览.md`
- `Docs/Architecture/核心对象与术语映射.md`
- `Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md`
- `Docs/DevelopmentSpecs/README.md`
- `Docs/开发验收交接.md`
- `Docs/文档地图.md`
- `Docs/问题清单.md`
- `Docs/项目日志.md`
- `Docs/项目状态快照.md`
- `README.md`
- `Tools/RRPG.ConfigCompiler/README.md`
- `Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/EntityRequirements.cs`
- `Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/BuildingConfigTests.cs`
- `UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/BuildingMirrorReader.cs`
- `UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/BuildingMirrorReader.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/StaticConfigBuilder.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveSystem.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingConfigDatabase.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingConfigDatabase.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingDefinition.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingDefinition.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingRegistry.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingRegistry.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/RRPGStaticConfigRoot.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/StaticConfigRegistry.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/BuildingRuntime.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/BuildingRuntime.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeBuildingPlacement.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeBuildingPlacement.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductProjection.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldCamera.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/ProductHomeSnapshot.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageBuildingsSaveData.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageBuildingsSaveData.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageSaveData.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Buildings.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Buildings.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingPlacementFoundationTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingPlacementFoundationTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/VillageStage1.Tests.asmdef`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingPlacementProductPlayTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingPlacementProductPlayTests.cs.meta`

### 发布前最终完整性核验

清理后PlayMode实际用时1:31.442。Builder成功/NoOp=True、唯一Root含4个Building定义。正常SampleScene→Play→MainMenu Continue进入原始6人Home，Host enabled=True、Buildings=0（原存档无C1块）、VillageTime Paused；compileFailed=False，原生Console 0 Error / 0 Warning。原始存档13000 bytes及SHA256 B096DC74C22729DF0710568AE892A4C2B1BE2806DB8B8A52BEDA814A986D8304保持一致，19/19任务外文件哈希一致。无额外任务外新增实际diff；完整回归产生的D-onsite-product.png已精确移入忽略Temp。必要meta齐全，52路径=29修改+23新增，保护清单无交集；文档本地链接检查0问题，任务范围diff --check通过。Handoff73已落盘并实际回读核对diff，固定格式保留；暂存前HEAD/origin/live三方仍基线2a2dec7。

暂存核对补充：精确52路径完全一致。源码/文档 staged diff --check PASS；Unity自动生成10份.meta的空值尾空格使全范围check提示30处，保留Editor原生生成格式/GUID，不手改序列化文件。没有源码/文档空白错误，临时产物不在index。

## V16-C1 Published Main / Work Package Closed（2026-10-08）

Closure Commit `4262a6d3a003a6885213006fe7237fd9c5b16818`（feat(village): close accepted V16-C1 building placement foundation）包含精确52路径，已正常Push main。Push成功后实际读取local HEAD、origin/main与git ls-remote GitHub main三方均为该SHA，origin/HEAD→origin/main，index为空。本任务路径无遗漏，19项保护文件及原始存档SHA256仍完全一致。C1 **Work Package Closed / Product Accepted / Published Main**；共享工作区仍有原有19项变化，不能宣称Branch Closed / Repository Closed。

制作人验收、Disposable Cleanup、Compiler252/252、EditMode1477/1477、PlayMode103/103、正常Home Console0/0、Handoff73实际落盘/回读、52路径manifest和正式配置验证均完成。没有C1阻塞；工作簿PNG校验未通过的限制如前记录，正式美术/最终内容/完整页面与Issue84/85/86/88仍Deferred。C2不实施，由开发16.0后续安排。

本段和9份Current/发布证据文档为Push成功后的状态补同步，未更改Runtime、配置、测试或Handoff生成时Git快照；最终状态提交HEAD以Git实时核验为准。

发布状态提交 `016d254424b1ebdb472a7988e6b2bc5db26e82b4` 已正常Push，local/origin/live三方核验一致。最终Current关键词审计发现术语映射仍有2处旧C1本地/Pending措辞，补正为Current/Published；只修本文与术语，不改Runtime或Handoff。最终工作区仅原有19路径、19/19哈希一致，原档哈希一致，C1 dirty=0、index=0；最后正文校正提交的HEAD以Git实时核验为准。

## V16-C2 Technical Checkpoint（2026-10-08）

**Technical Verified Local / Producer Acceptance Pending**。本轮只执行C2，未提交/推送，D未启动。最终Handoff73保留；依交接生成规则，产品验收后再生成C2最终Handoff、Disposable清理、复测与精确发布。Issue84/85/86/88均未关闭：本轮只实现建筑施工Place生命周期与实例岗位注册，旧非建筑Project、Commons与完整Home仍有独立边界。

### Preflight、Authority与配置

- local HEAD / origin/main / 实时GitHub main开工核验均为 `12c3cbb2203d4484fd1353a3f1987b1510d2ee3a`，基线祖先检查通过，Target Branch=main。实时远端核验采用内置Git、禁用失效代理、HTTP/1.1直连；未改变全局Git配置。最后local HEAD仍是该SHA，index为空。
- BuildingRuntime继续拥有原BuildingInstanceId、位置与生命周期；仅增加ConstructionProjectId薄绑定。施工使用既有 `prj_test_01` 白模规则，不新建进度或Clock。Add/Remove调用既有Project/Temporary Commitment；原Primary Assignment保留，零工人保留进度并暂停。实际劳动力仍来自IntradayWork到场和工作时段，动画不宣告到场。
- 施工地点为派生 `construction:<BuildingInstanceId>`。WorkDestination把已绑定Project导向此Place；完成时在同一BuildingInstance上切Completed，清绑定、退役施工Place、释放到场状态并注册功能岗位。全部功能先验证冲突再发布；重复Observe不注册第二份，不追加旧ProjectSite或重复能力授予。
- FunctionBinding只声明BuildingDefinitionId、FunctionSlotId、FunctionType、FunctionProfileId。Farm/main引用ws_farm_01，Kitchen/main引用ws_kitchen_01，Workshop/main引用ws_test_01；House无功能槽。容量和贡献率从现有VillageWorkplaceDef原型读取，没有复制到XLSX。
- `RRPG_Buildings.xlsx`经Artifact Tool新增第三页；原两页48个非空单元格逐值核对无变化。Artifact渲染进程退出code1，无可用PNG，因此不宣称工作簿视觉验收通过。Compiler OpenXML VALID、FunctionBinding引用/重复槽/非法类型/非法Profile校验通过。
- 隔离Config Root使用HEAD Events，原dirty Events未导入。本次Compiler正式生成BuildingFunctionBinding镜像，经现有Unity Builder进入Root/Registry；BuildingDefinition镜像重建后字节不变。39份镜像与隔离输出SHA全部一致；重复build全部unchanged，0错误、8条既有WB004警告。Builder首次Success=True/NoOp=False；后续Success=True/NoOp=True，Registry=4 definitions/3 bindings。未手工写Generated JSON。

### Save与多实例恢复

- SaveVersion继续1。新增Building ConstructionProjectId与Project LastAllocatedSequence水位；后者防止已完成项目退出Active存档后、重载再建项目复用旧ID。旧缺字段以默认值恢复，原C1建筑无绑定合法。
- Restore先构造候选Buildings→Projects→Completed实例Workplaces→Assignments→World/IntradayWork，后续校验全部成功才发布C2五个对象。岗位ID为 `workplace:<BuildingInstanceId>:<FunctionSlotId>`，两座Farm共用Profile但独立容量与Assignment。Place和Workplace目录不另存副本。
- 重复施工绑定、未知动态岗位、无法完整恢复的动态Assignment及超容量动态岗位拒绝加载；候选恢复不修改当前Building实例。达到完成目标的旧绑定可在候选恢复中收口，不重放旧完成痕迹。其他领域已有世界级Load事务Deferred不因此转正。
- NewGame清空建筑、项目、派生岗位、地点与工作到场。原型静态岗位继续登记。零工人施工、施工劳动余数、完工多实例、原C1无绑定、无Buildings旧档均已覆盖。

### 本轮实际验证与首失败

| 验证 | 最终实际结果 |
|---|---:|
| ConfigCompiler全量 | 257/257 PASS，0失败/0跳过 |
| C2 EditMode专项 | 14/14 PASS |
| 完整EditMode | 1491/1491 PASS，0失败/0跳过；1分53.878秒 |
| C2正式产品Play专项 | 3/3 PASS |
| 完整PlayMode | 106/106 PASS，0失败/0跳过；1分33.615秒 |

- Compiler首次在沙箱中testhost连接90秒超时；允许本机测试通信后同套全量257项通过，不把首次中止算代码PASS。
- 新测试首次枚举写成VillageTimeSpeed，修正为VillageTime.VillageTimeSpeed后编译通过。MCP首次检索测试树在重载/切模式后数次返回No tests found；简单类名过滤及后续重试实际执行成功，未把0执行数算通过。
- 产品测试首次反射UnityEvent.Invoke遇到重载歧义，改按参数数目选方法；随后同步重建按钮时误选已禁用待Destroy的旧按钮，改为实际active按钮。真实完整劳动从零推进两座Farm完成；撤出再加入的额外通勤按事实保留，不人为补劳动。
- 坏档超容量新测试首轮13通过/1失败，暴露旧Assignment Restore不校验容量。仅在C2候选实例岗位增加容量拒绝；之后14专项、1491全量Edit、106全量Play通过。全量之后仅修正Inspector提示文案，指向实际调试/对照承诺入口，未改变命令行为；最终Editor无编译错误，Registry回读4/3。
- 正常6人Home从原档Continue，真实劳动创建两座Completed Farm；另建第三座Farm，以真实劳动推进至9/10、余数1，韩昭实际位于construction:building_instance_3，原职责ws_farm_01仍在。世界同一实体保持，Completed绿色、施工棕色；1920×1080 GameView已实看，Inspector工人/现场/按钮未重叠。清Console仅清Unity UI，MCP日志缓存仍含历史预期错误；正常Home随后最近2分钟Error/Warning查询均为空，正常运行0 Error/0意外Warning，不宣称整段历史Console为空。

### 保护与现场验收

- 19项开工任务外文件逐字节备份及SHA清单位于忽略的 `Temp/V16C2/protected` / `protection.json`。回归后用Unity Editor FileUtil恢复原dirty TMP字节，当时19/19哈希一致。原单槽存档真实目录为AppData/LocalLow/DefaultCompany/RRPG1_0game，备份 `Temp/V16C2/actual-save-backup/rrpg_save_p0.json`；最终原档SHA仍为 `B096DC74C22729DF0710568AE892A4C2B1BE2806DB8B8A52BEDA814A986D8304`。
- 现场Play实看/退出及中断后最终回读：18项保护文件仍字节一致；TMP再次出现运行期变化，原dirty字节备份保留。期间新增任务外 `UnityProject/RRPG1.0game/TestResults/D-onsite-product.png`，不读取、不修改、不纳入C2；无法排除并发字体写入，因此不再覆盖当前字体。最终收尾需核对字体归属再还原，不能宣称当前19/19一致或共享工作区clean。
- 人工验收：在Home选施工建筑，撤出/加入工人，核对原职责与进度不丢；1×或2×观察通勤及实际到场，再切24×观察完工；确认同一世界建筑变Completed，已有职责人物可经“调试/对照”承诺入口调整，实例岗位已加入该入口目录，无职责人物也可直接在建筑Inspector分配。分别查询两座Farm岗位容量和人物，再“建造→保存村庄”、退出Play、Continue核对。保存使用正常单槽，原档备份保留；演示现场不提前覆盖原档。

### Deferred与修改清单

动态资源产出单列紧随C2的窄包：现有资源批次只识别固定_resourceWorkplaceDefs ID；需审计实例Profile映射、批次边界、余数恢复与午餐链，C2本轮不重写资源域、不宣称动态Farm产食材或Kitchen产食物。其他FunctionType、正式材料成本、升级/拆除、Grid/NavMesh/道路、最终美术、完整管理页与D均未实施。

任务修改路径（配套meta仅Unity导入生成）：

```text
Development/Config/RRPG_Buildings.xlsx
Development/Generated/StaticConfig/BuildingFunctionBinding.json
Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/CompilerRunner.cs
Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/BuildingFunctionValidator.cs
Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/BuildingConfigTests.cs
UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/BuildingMirrorReader.cs
UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/StaticConfigBuilder.cs
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingConfigDatabase.cs
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingRegistry.cs
UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveSystem.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/BuildingRuntime.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageAssignmentSystem.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageBuildingsSaveData.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageProjectSystem.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageSaveData.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageWorldRegistry.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Buildings.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Intraday.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Work.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageCommitmentUI.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingConstructionTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingConstructionTests.cs.meta
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingConstructionProductPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingConstructionProductPlayTests.cs.meta
Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md
Docs/DevelopmentSpecs/README.md
Docs/项目状态快照.md
Docs/项目日志.md
Docs/开发验收交接.md
```

34个C2路径留工作树供审阅；index为空，未Commit/Push。忽略Temp脚本、备份、TRX与验证输出不交付；19项原任务外路径及中断期间新增截图排除。Final Closeout尚待制作人C2产品验收及保护字体核对；不标C2 Closed。

中断后已重新通过原档Continue准备现场：当前Unity Play、村庄Clock暂停；两座Completed Farm分别有人在实例岗位，第三座building_instance_3施工进度9/10，韩昭实际在施工处，原Primary=ws_farm_01。所有进度均经真实Clock/Person-Time推进，未注入Progress。最新正常现场最近1分钟Error/Warning均为空；原档SHA仍一致，未写入演示世界。可直接在当前Game View验收；先保存演示村庄再退出Play/Continue才能复查演示的多实例存档。


## V16-C2 Final Closeout（2026-10-08）

用户明确“V16-C2验收通过，收尾”，报告开发16.0通过Remote Desktop Commander复核本地记录、测试代码与Git工作区，接受实际到场劳动、撤工/加人、Primary/进度保留、同实例完工、多实例岗位及Save/Continue。仅执行C2收尾；动态资源产出明确Deferred，不是失败也不是已实现；D未启动。

### 清理、完整回归与正常Home

- 移除7项重复倍速/暂停验收、1项重复内部NewGame验收与1项已有C1兼容覆盖验收，保留6个Edit施工核心、2个真实产品Play与5个Compiler配置契约；无临时Runtime/Editor runner入交付。
- Compiler257/257（0失败/跳过）；完整EditMode1483/1483、PlayMode105/105（均0失败/跳过）。MCP两次首轮“No tests found”属于reload后测试发现，重跑取得实际完整结果；没有用空结果代替PASS。
- 正式隔离工作簿validate PASS，0错误、8项既有WB004；Builder Success=True / NoOp=True，Unity CompilationFailed=False / SceneDirty=False。
- 原档SampleScene → Play → MainMenu Continue：HostEnabled=True / WorldVisible=True，6人/0建筑、Clock=Paused；19:04:30 +08后正常观察区间0 Error / Warning / Exception。先前只读脚本误用IsPaused导致MCP工具编译错误，改为Speed后重做检查；产品源代码编译无错误。测试历史预期拒绝日志和测试字库警告不算正常Home区间，未宣称历史Console全空。

### TMP与存档保护

本轮字体差异文件最后写入时刻与此前现场Play退出一致，没有后续写入；审计增量为GlyphTable / CharacterTable / UsedGlyphRects / FreeGlyphRects动态缓存。另存当前副本到忽略Temp，再经Unity Editor FileUtil恢复开工时原dirty字节并Import，未从Git HEAD撤回用户原修改。最终19/19 SHA256一致；TMP=AADE47376B878110C3BFE9568E1DC473628B0FAED5222B94CABDB5ABFA91792C。原单槽存档恢复后与正常Continue/退出后均SHA256=B096DC74C22729DF0710568AE892A4C2B1BE2806DB8B8A52BEDA814A986D8304，字节一致。测试/演示存档、截图、TRX、临时脚本和备份不提交；额外任务外D-onsite-product.png原位保留排除。

### 文档、Issues与交接

Handoff74真实落盘并回读；Current Snapshot、日志、索引、Issue清单、README/地图、Architecture/术语与配置入口同步。84 Commons身份债保持开放；85建筑施工Place范围已解决、旧非Building Project仍开放；86实例岗位注册/恢复次序已解决、通用Def追加同步仍开放；88有限C2范围已验收、正式美术/完整页面仍Deferred。动态资源后续需审计Profile、批次/余数恢复及午餐链；候选五项Load隔离不扩大为世界级事务。

生成时基线/Target/current=main@12c3cbb2203d4484fd1353a3f1987b1510d2ee3a；local/origin/实时GitHub一致，index为空，Acceptance Passed / Final Closeout Prepared。未提前宣称Published / Closed；Git实际发布证据后续追加。最终精确40路径（原34 + 6份必要Current/Issue/配置入口同步）：

```text
Development/Config/RRPG_Buildings.xlsx
Development/Generated/StaticConfig/BuildingFunctionBinding.json
Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/CompilerRunner.cs
Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/BuildingFunctionValidator.cs
Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/BuildingConfigTests.cs
UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/BuildingMirrorReader.cs
UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/StaticConfigBuilder.cs
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingConfigDatabase.cs
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/BuildingRegistry.cs
UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveSystem.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/BuildingRuntime.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageAssignmentSystem.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageBuildingsSaveData.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageProjectSystem.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageSaveData.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/Runtime/VillageWorldRegistry.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Buildings.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Intraday.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.Work.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/UI/VillageCommitmentUI.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldInspector.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeReadAdapter.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingConstructionTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/BuildingConstructionTests.cs.meta
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingConstructionProductPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingConstructionProductPlayTests.cs.meta
Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md
Docs/DevelopmentSpecs/README.md
Docs/项目状态快照.md
Docs/项目日志.md
Docs/开发验收交接.md
Docs/问题清单.md
README.md
Docs/文档地图.md
Docs/Architecture/技术架构总览.md
Docs/Architecture/核心对象与术语映射.md
Development/Config/README.md
```


### C2实际Git发布核验

Closure Commit `a241965e06e081eba200e2727b5d6068d174cf50`（feat(village): close accepted V16-C2 construction and functions）已正常Push main。推送后local HEAD / origin/main / 实时GitHub refs/heads/main均为上述完整SHA；精确40路径=34修改+6新增，manifest与暂存完全一致。源码/文档diff check通过；新增两份meta中空userData / assetBundleName / assetBundleVariant尾空格为Unity原生导入格式，保留Editor生成字节，未手改meta。19项保护路径与额外截图均不在提交内，原存档不在仓库。

C2 **Work Package Closed / Product Accepted / Published main**。Handoff74保持生成时事实；本次仅同步10份Current / 发布证据文档，未再改Runtime或重新解释验收。状态同步提交后以实时Git核验为最新SHA，不回填尚未知的未来提交值。保护19/19与原始存档字节一致，index已空；工作区仍保留任务外dirty/untracked，故不标Branch Closed / Repository Closed。动态资源产出Deferred，84/85/86/88剩余范围保持开放，D未启动。


## V16-D Asset Replacement Proof — 2026-10-08 技术检查点

**D Implemented / Technical Verified / Producer Acceptance Pending**。用户“执行v16-D”授权本Gate；按附带任务契约停在Producer D Acceptance，尚未产品验收、Disposable Cleanup、Commit或Push，不标D Closed。C2仍Closed / Published main。仅PNG→Sprite，最终美术及动态资源产出Deferred，Issue84/85/86/88剩余范围不关闭。

### Preflight / 权威与现场保护

依次读取Bootstrap及文档地图指向的Current Guide、GameSpecs、Architecture、V16 §8/9、累计记录、Handoff74、问题边界、ArtDirection、配置与Compiler/Builder契约。开工local HEAD、cached origin/main、实时GitHub main均为`412d3be1a25d438e8e053707b8e87c322d7c2332`，无需同步。实时读取曾因本机失效代理失败，清除该命令代理后只读ls-remote成功。Target/current branch=main，index为空。

原19项dirty/untracked及额外D-onsite-product.png共20项建立逐字节备份和SHA256，清单`Temp/V16D/protection.json`，副本`Temp/V16D/protected/`。原存档备份`Temp/V16D/original-save.json`。Events已dirty，Compiler在隔离root使用HEAD版本Events，真实Events未写入；其他Authoring从当前root复制。只晋升本任务4份正式镜像，原37份其它镜像哈希无差异。测试造成TMP及既有截图自动增量，恢复到开工dirty副本（字体经Unity Editor API写回并ImportAsset），不恢复HEAD；最终20/20一致。

### 最小实现与单一正式管线

新增窄`RRPG_VillagePresentation.xlsx`：BuildingPresentationBinding以Definition + Lifecycle选PresentationKey，VillagePresentation以Key引用共享VisualAssetId。Farm两生命周期分别有basic/construction Key，其余三Definition使用neutral Key。Compiler只增加必需字段登记，使用既有ref/key/allowed校验；Unity MirrorReader在写入前验证生命周期、引用和重复复合身份。专用Glue负责Unity类型转换与写前预检，未新增Compiler、资源目录或Gameplay Authority。

同一Builder生成VillagePresentationDatabase并挂唯一RRPGStaticConfigRoot，Registry/Resolver只读。HomeWorldHost刷新已有View，每次重新解析Sprite；不销毁重生。Sprite按真实Footprint归一化，Collider/占地选中框/Focus与图像像素尺寸独立。Person/Place仍使用原中性表现。缺失映射或Sprite有明确诊断及既有统一占位，无`if placeholder`资源特例。Gameplay、Save代码和Schema无改动。

共享VisualAssets添加`va_village_farm_basic`及`va_village_farm_visual_b`，均经SourceAssets→Compiler→AssetManifest/Canonical Mirror→Builder→唯一VisualAssetCatalog。原7条业务行和原说明/Schema非空值逐格保留。测试A为300×200绿黄横纹，B为400×240紫蓝竖纹，两图均标Technical Proof / Not Final Art。交付恢复A及basic绑定，authoring/mirrors/managed Sprite一致；复现样本在`Development/SourceAssets/Village/TechnicalProof/`。

### 正式链A / B / C证据

正常SampleScene.scene→Play→MainMenu Continue，使用正式建造/施工/岗位命令准备3座Farm并暂停：building_instance_1=(3,-4)、building_instance_2=(8,-4)，两者Completed且各自Workplace及Assignment独立；building_instance_3=(-2,-4) UnderConstruction，Project Progress1/10、LaborRemainder121、1 worker。以同一现场比较，未为换图重启Unity或重载场景。

| 证明 | 正式输入与重建 | 结果 |
|---|---|---|
| A同ID源图 | 只替换farm-proof.png为B，重新Compiler/Builder；VisualAssetId不变 | 3个现存实体全部更新；只有AssetManifest源内容哈希变化 |
| A恢复 | 恢复A源图再Compiler/Builder | 全部恢复A，与baseline哈希一致 |
| B绑定 | 不动两张源PNG；仅两农田Key的VisualAssetId改visual_b，重新Compiler/Builder | 仅VillagePresentation镜像变化；3实体全部变B |
| B恢复 | 两Key恢复basic并Compiler/Builder | 全部恢复A，最终Builder Success=True / NoOp=True |
| C不变量 | 四次重建前后完整Village序列化比较及逐View证据 | Building/Definition/Position/Lifecycle/Construction/Progress/Workers/Workplace/Assignment均相同；EntityID、ColliderID、ColliderSize、Footprint、Selection、RenderSize相同 |

记录`Temp/V16D/baseline.json`、`source-b.json`、`source-a-restored.json`、`binding-b.json`、`binding-a-restored.json`及`invariance-results.txt`；完整Village字符串逐字一致。演示save的规范化Village与baseline一致；真实原档每次Capture的SHA256始终`B096DC74C22729DF0710568AE892A4C2B1BE2806DB8B8A52BEDA814A986D8304`。演示仅当前Play内存，未永久覆盖用户档；退出后Continue仍是原档。

Management→World重新进入Home，`home-reentered.json`保持全部3个EntityID及配置；逐个实际View.Click→共同Selection/Inspector→Focus成功。缺失源PNG时Builder明确拒绝，Root ContentSignature不变，源图finally恢复，后续正常Builder no-op成功（`missing-source.txt`）。这里验证Editor显式Compiler/Builder后的当前实例刷新；不声称发布Player拥有文件自动热监控。

### 本轮测试 / 首次失败与修复

| 验证 | 最终本轮结果 |
|---|---|
| targeted Compiler | 8/8：同ID、绑定、确定性、无关镜像、非法引用/生命周期/重复、缺源保护 |
| 完整Compiler | 265/265 PASS，0失败/跳过；Temp/V16D/V16D-full.trx |
| 最终完整EditMode | 1486/1486 PASS，0失败/跳过，2:14.6899771 |
| 最终完整PlayMode | 106/106 PASS，0失败/跳过，1:34.7332069 |
| 正式Compiler build / repeat | PASS，0errors / 8既有WB004，重复镜像unchanged；其它37镜像无差异 |
| Builder / Registry | 写入成功、重复no-op；所有3View解析共享managed Sprite |
| 正常Home Console | 2026-10-08 19:57:45～19:59:45 +08，Error/Warning/Exception各0；历史测试拒绝日志不算空 |

首次Compiler用例误用不存在SourceArtDir成员，改为现有目录契约；随后4个负例断言枚举不存在输出目录，改为检查目录未发布，最终8/8。首次EditMode有1个失败：Resolver.Dispose在非Play调用Destroy，修为EditMode DestroyImmediate / Play Destroy。首轮目标测试过滤未按预期限制范围，因此最终使用全量统计；Unity Runner在重载/模式转换后首调“No tests found”，重试全量成功，不当作通过。dotnet test sandbox内testhost通信受限，授权本地执行后成功。

首次现场选择环仍带原图形状，改用已有占地LineRenderer显示建筑Selection，并重新跑最终全量Edit/Play及全套A/B/C。证据脚本初次className不匹配、Newtonsoft不可用及JsonUtility List序列化遗漏，已改正确类名与逐项JsonUtility序列化；最终脚本一次调用各证据成功。最终回放首次误写SampleScene.unity路径，Unity诊断不存在；按实际SampleScene.scene修正，没有更改场景。以上为真实失败，不隐瞒也不冒充产品编译错误。Artifact工作簿导入空字符串会变成1，回填原始矩阵后独立读取核对；PNG工作簿渲染未成功，不宣称工作簿视觉QA PASS。

### Producer验收卡（当前Play现场可直接观察）

1. 打开Unity Game：已停在正常Home、0×暂停，3座绿黄Farm可见。
2. 点击左边施工农田：Inspector应保持施工中、1/10、1人；点“聚焦”。
3. 点击中间及右边农田：两者已完成，查看各自独立岗位/分配；切管理再回村庄。
4. 对照`Temp/V16D/baseline.png`与`source-b.png`：同ID替换后三座同步变紫蓝，施工数据相同。
5. 对照`binding-b.png`：仅表现绑定替换也三座同步变紫蓝；原源图未覆盖。
6. 对照`final-a.png`并看当前Game：已恢复A，选中/Inspector/占地框正常。
7. 认可上述Sprite替换与不变量后确认D产品验收；本轮等待此一次最终确认，不自动进入后续范围。

无需阅读代码验收。实际现场回放与重建可由执行员操作：忽略目录`pipeline.ps1 SourceB/SourceA/BindingB/BindingA`只调用原Compiler，随后Proof.cs.txt对应方法调用原Builder，最后恢复SourceA/BindingA；技术输入的正式长期回放说明见样图README及配置README。原档/dirty保护仍需维持，禁止直接对dirty Events全量生成后晋升。

### Deferred / 交付边界 / Git Facts

只验证Building Sprite；Prefab/Animator/Spine/Material等未验证，Person/Place新配置、anchor/offset、正式Farm美术、Grid/Nav/Economy/Upgrade、其他FunctionType、完整Home页面与动态资源产出均不在D。84/85/86/88不关闭。动态产出可后续独立窄包审计实例Workplace Profile与固定批次映射、余数恢复及午餐链，不在此实施。

开工与本次技术检查点HEAD/cached origin/main=`412d3be1a25d438e8e053707b8e87c322d7c2332`；实时GitHub只读核验一致。未stage/commit/push，Target/current=main。20保护文件及真实存档完整；截图、演示档、操作脚本、结果留忽略Temp，managed assets留既有忽略Generated路径。最终正式authoring=A/basic。Cleanup、必要全量复跑与精确文件Commit/Push等待Producer Acceptance后由开发16.0安排。下列为D工作树路径，排除原20项，未暂存：

```text
Development/Config/README.md
Development/Config/RRPG_VillagePresentation.xlsx
Development/Config/RRPG_VisualAssets.xlsx
Development/Generated/StaticConfig/AssetManifest.json
Development/Generated/StaticConfig/BuildingPresentationBinding.json
Development/Generated/StaticConfig/VillagePresentation.json
Development/Generated/StaticConfig/VisualAsset.json
Development/SourceAssets/Village/TechnicalProof/README.md
Development/SourceAssets/Village/TechnicalProof/farm-a.png
Development/SourceAssets/Village/TechnicalProof/farm-b.png
Development/SourceAssets/Village/farm-proof-b.png
Development/SourceAssets/Village/farm-proof.png
Docs/Architecture/技术架构总览.md
Docs/Architecture/核心对象与术语映射.md
Docs/DevelopmentSpecs/2026-10-07_RRPG_V16_Execution-Record.md
Docs/DevelopmentSpecs/README.md
Docs/开发验收交接.md
Docs/文档地图.md
Docs/项目日志.md
Docs/项目状态快照.md
README.md
Tools/RRPG.ConfigCompiler/README.md
Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/EntityRequirements.cs
Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/VillagePresentationConfigTests.cs
UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/StaticConfigBuilder.cs
UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/VillagePresentationMirrorReader.cs
UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/VillagePresentationMirrorReader.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/RRPGStaticConfigRoot.cs
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/StaticConfigRegistry.cs
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/VillagePresentationDatabase.cs
UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/VillagePresentationDatabase.cs.meta
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs
UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/StaticConfig/VillagePresentationTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/StaticConfig/VillagePresentationTests.cs.meta
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingVisualReplacementPlayTests.cs
UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/BuildingVisualReplacementPlayTests.cs.meta
```

共38条D路径；临时证据/原保护资源不包含在内。


## V16-D Producer Acceptance / Final Closeout — 2026-10-08

用户明确“V16-D 制作人验收通过，收尾”，并报告开发16.0通过Remote Desktop Commander检查四张实际视觉证据、正式资产链替换、不变量与完整测试结果，确认A/B/C三组证明；这是用户明确报告的独立复核，不冒充Codex另行完成的独立审查。用户同时授权Disposable Cleanup、全量复跑、精确暂存、Commit + Push main。仅关闭D，不自动扩张正式美术或新Work Package。

### Disposable Cleanup与保留理由

移除2项重复验收用例：Compiler SameIdSourceReplacementOnlyChangesManifestAndBuildIsDeterministic（同ID内容变更与确定性由既有EnemyPresentationDomainTests的共享VisualAsset契约覆盖）；Edit DefinitionLifecycleAndPresentationKeysRemainSeparateFromSharedAssets（只镜像字段读写，与真实绑定/解析/多实例刷新覆盖重复）。清理相应未使用using。没有向生产代码加入临时验收接口，无需删除正式Builder/Registry/Resolver接口；现场/证据脚本从未放进Assets，只在忽略Temp，保留其原始备份与验收证据、不暂存。Test Runner临时InitTestScene在正常测试结束后退出，不提交。

保留最小长期集合：7个Compiler用例保护正式工作簿绑定隔离、Definition/Key/VisualAsset引用、生命周期allowed、重复复合身份及缺源失败不覆盖旧镜像；2个Edit用例保护缺Key明确拒绝及唯一Root的双生命周期共享managed Sprite；1个真实MainMenu Continue产品Play用例保护既有实体/Collider/位置/世界Save不变量及Click/Focus。不保留重复的Gate历史测试；A/B技术输入样本属于已授权可回放来源，不作为正式美术。

### 清理后本轮Verification

Compiler264/264、EditMode1485/1485、PlayMode106/106 PASS，0失败/0跳过。EditMode耗时00:02:06.7052091，PlayMode耗时00:01:36.1050819；Compiler本轮TRX为`Temp/V16D/V16D-closeout.trx`，Unity结果为`Temp/V16D/closeout-unity-summary.json`。

首次EditMode/PlayMode模式重载调用若返回“No tests found”，仅记录工具发现失败，重试完整无过滤运行的结果才计PASS；完整失败项以真实返回为准。清理后未改产品逻辑。正式Compiler隔离build / validate均PASS，0errors/8既有WB004；全部41份JSON输出unchanged，和正式镜像逐字节一致。此前D技术记录“其它39镜像”计数有误，本次按实际JSON重计修正为37（原39总镜像，其中2份修改；D新增2，最终41）。仅本轮错误计数被纠正，不改C2历史。

正式A源PNG、basic双Key绑定、AssetManifest源SHA及Unity managed Sprite一致；Builder重复Success=True/NoOp=True，生成目录字节与mtime稳定。全量测试结束后恢复正常SampleScene→Play→MainMenu Continue读取原档，暂停、正常Home只读检查，SceneDirty=False；真实运行与Console观察证据追加在下方。此正常原档现场不替代已完成并验收的3Farm演示。

### 保护、文件隔离与待发布Git Facts

开工20项保护SHA全部与原副本一致；原save为B096DC74C22729DF0710568AE892A4C2B1BE2806DB8B8A52BEDA814A986D8304。TMP和旧截图只恢复本轮测试自动增量到开工dirty备份，绝不从HEAD覆盖原dirty。真实Events不变，隔离Compiler使用原HEAD Events；无关37份镜像与HEAD无差异。

Target/current branch=main；生成时local HEAD/cached origin/main/实时GitHub main=412d3be1a25d438e8e053707b8e87c322d7c2332，index为空。Acceptance Passed / Final Closeout Prepared；Commit/Push尚未执行，本处不预填未来SHA或Closed。精确D工作树manifest仍38路径，与上节清单一致；Must Stage只有该38路径（含必要meta）；Must Not Stage为protection.json的20路径、Temp、TestResults/截图/演示存档及其它任务资源。无Scene/Prefab/Gameplay/Save修改。

Current Snapshot、README、地图、DevelopmentSpecs索引、Architecture/术语、日志及Handoff76同步本轮接受与清理后事实。GameSpec/GameDesign/决策无需修改，不从实现反推规则；问题84/85/86/88继续开放，D替换证明不解决Commons、旧Project、通用岗位追加同步或完整Home。仅Building PNG/Sprite已验证，Animator/Prefab/Spine/Material等未验证，最终美术与动态资源产出继续Deferred。

V16链P/A/B/C1/C2已验收发布；D产品验收通过，等待本轮实际Push核验后才标Work Package Closed。共享工作区含任务外dirty，不标Branch Closed或Repository Closed。下一步仅完成此Closure发布，不启动新Work Package。


### Final现场与暂存前复核

2026-10-08 20:15:46.3928 +08，正常SampleScene.scene→Play→MainMenu Continue读取原始存档，persons=6 / buildings=0、Host active=True、Speed=Paused、SceneDirty=False。20:15:46～20:16:56 +08按时间戳过滤的Console Error/Warning/Exception各0。20:15:23场景启动时有既有LiberationSans / Text(Legacy)中文缺字警告；不隐瞒启动阶段，不宣称整段历史Console为空，也不扩大到字体/UI整治。原字体dirty SHA仍一致。

Builder两次均Success=True/NoOp=True；生成目录全文件SHA256及mtime逐项一致，managed A/B PNG分别匹配正式源图哈希，Root PresentationCount=3 / BindingCount=8，Farm两个Lifecycle都绑定basic。Compiler重复build全部41 JSON unchanged，与正式镜像0差异；最终A/basic读回成功。

Closure Commit Staging Manifest：**Must Stage**为上节38条精确D路径；**Must Not Stage**为protection.json的20条原任务外路径及Temp/存档/TestResults/截图。Expected staged file count=38。测试及样图必要.meta由Unity原生导入生成，保留原生空字段行尾空格，不手改序列化文件。Source/Docs diff-check通过；文档新增链接目标已核验。用户授权精确暂存该范围、普通Commit和Push main，无force/rebase/reset/clean。


### V16-D实际Git发布核验

Closure Commit `97ea2598945b48f682ef34f042f4f5074d6c04d1`（feat(village): close accepted V16-D sprite replacement proof）已普通Push main。推送后local HEAD / origin/main / 实时GitHub refs/heads/main均为上述完整SHA；精确38路径=21修改+17新增，与D manifest及实际暂存完全一致。全部代码/配置/SourceAssets/必要.meta/长期测试/Current文档进入Closure，20保护路径、Temp、截图及存档零混入；index为空，剩余工作区路径严格等于原20项保护清单，无D残留。

D **Work Package Closed / Product Accepted / Published main**。用户验收范围为A/B/C三证明；技术验证仅Building PNG/Sprite，不代表Animator/Prefab/Spine等已验证，也不代表正式美术冻结。V16-P/A/B/C1/C2/D既定全阶段均已验收发布；动态资源产出、最终美术、完整Home与Issue84/85/86/88剩余范围继续Deferred，不自动开启新Work Package。

清理后Compiler264/264、EditMode1485/1485、PlayMode106/106 PASS，正式41镜像unchanged、Builder重复no-op；源A/basic和managed assets一致。20/20保护文件与原Save SHA核验一致，任务关闭不等于共享工作区clean，因此不标Branch Closed / Repository Closed。

Handoff76保留生成时真实412d3be / Commit-Push尚未执行事实；本次仅同步9份Current/发布证据文档，不回填未来SHA。该发布状态同步的最终SHA以实时Git核验为准，不再改代码、不借文档同步重新解释产品验收。原验收截图/备份与TRX保留忽略Temp。
