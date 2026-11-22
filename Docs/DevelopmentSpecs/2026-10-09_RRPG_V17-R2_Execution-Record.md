# V17-R2 Village Visual & Spatial Master Prototype — Execution Record

> 2026-10-09 / Asia/Shanghai. Target Branch: **main**. Baseline: `fa8a78e651cf5fc66a5c3af3dd0451354e66eb19`.
> **Work Package Closed / Published main / Technical Prototype PASS / Art Master NOT FROZEN.** Closure `2a52f856c7b6e3e281f992c7aa15472a0dd2dd6e`已正常Push并三方核验。仅关闭R2技术原型，不自动进入下一工作包。

> 地点跳变已修复；清理后本轮Compiler264/264、EditMode1492/1492、PlayMode109/109 PASS，Builder NoOp。清理前289次真实时间采样稳定。制作人仅通过技术原型；原17张截图及反馈截图为验收历史样本，不是最终美术母版。

## Contract and preflight

Producer request: “执行V17-R2”, referring to `Codex_V17-R2_Village_Visual_Spatial_Master_2026-10-09.md`. The attachment's claim of earlier automatic advancement is not used as independent authorization. This package stops for one visual acceptance before closure/publication.

Read the root Bootstrap chain, AI development rules, Village design/current state and ArtDirection working model, Product Home/Building GameSpecs, Current Architecture/objects §27/§22, V16 specification/execution evidence, R1 record and Handoff77. Local main equals the specified baseline; no newer baseline delta. Date-stamped planning headers do not override the subsequently published V16/R1 facts.

Editor preflight: live Tuanjie `2022.3.62t9`, actual project `UnityProject/RRPG1.0game`, SampleScene loaded/clean, not playing/compiling; scriptCompilationFailed=false. Console already contains historical intentional negative-test errors and LiberationSans missing-glyph warnings from the prior task. These are not R2 failures.

Initial status: 5 modified tracked files plus 17 untracked files = **22 protected paths**, including the previous 20 and two `.codely/clipboard` PNGs. Every path was copied byte-for-byte with SHA256 to ignored `Temp/V17R2/protected/` and `protection.json`; task-external content was not audited or staged. Actual single-slot save copied through Editor to `Temp/V17R2/original-save.json`, 13402 bytes, SHA256 `337AC2B2E3774DA1412852D3CAAE973C9C19FE1E84752C8B88092A46FD9A9173`. No clean/reset, YAML edits, package or project-setting changes.

## Existing Host Map and candidate approach

| Existing host | R2 use |
|---|---|
| ProductHomeWorldHost / ProductHomeReadAdapter | Same real Town/Continue world and snapshots; persistent identity dictionaries retained |
| HomeWorldEntityView | Same SpriteRenderer, selection, labels and footprint collider; B adds roof hit envelope, depth from ground anchor and selected-person visibility |
| HomeWorldCamera | Existing orthographic layer-31 camera; B zoom 4–11, pan center ±18 / ±12; no camera save |
| HomeProductShell / Inspector / BuildingPlacement | Existing HUD/navigation/commands retained; session A/B button and candidate ghost visual |
| HomePresentationResolver | Existing Resolver reads candidate keys from same generated VillagePresentationDatabase and shared VisualAssetCatalog |
| RRPG_VillagePresentation.xlsx / RRPG_VisualAssets.xlsx | Append candidate rows only; unchanged schema, existing cells and lifecycle bindings |
| Compiler / Mirror / StaticConfigBuilder | Existing source PNG → ID → Mirror → Builder → catalog → view pipeline; no parallel production resource loader |

A remains the default published whitebox presentation. B is a reversible **TECHNICAL VISUAL PROTOTYPE / NOT FINAL ART**: oblique PNG artwork in the existing 2D world plane, large procedural ground patches and visual-only path bands, grouped temporary Home/Place anchors, natural sprite aspect for persons, building volume constrained by the existing width, ground-anchor depth and selection outline. Semantic construction/work places do not draw a second building over their actual instance. Person movement continues to consume the existing world clock rate, and visual arrival cannot declare simulation arrival.

The geometry ground uses transient MeshRenderer shapes, not image-library bypasses. Every replaceable person/house/farm/kitchen/workshop PNG goes through the same formal shared catalog. No new world-coordinate authority, Building/Placement rule, simulation, capacity, economy, save field/version, navigation, road network, room/interior or full page.

Five generated transparent PNGs and exact prompts/tool provenance are recorded in [R2 source README](../../Development/SourceAssets/Village/R2Prototype/README.md). These are candidate technical samples; no promise that their drawing style meets final art direction. PNG/Sprite is the only tested asset type.

## Config and implementation evidence

- Added five VisualAsset rows and twelve candidate VillagePresentation keys. Original non-empty cells, navigation sheets, schema/styles and BuildingPresentationBinding remain intact.
- Initial openpyxl save failed strict OpenXML font-order validation. Replaced that save with original-package preservation and only appended worksheet rows using original styles. A first partial repair exposed out-of-range style indices; corrected by preserving all original cells/XML and creating only appended rows. Compiler Hard Error produced zero mirror updates throughout failed attempts.
- Compiler uses an isolated root copied from current authoring/assets, with HEAD Events supplied only inside that root. Real dirty Events remain untouched. Successful build: zero errors / eight existing navigation-sheet skip warnings.
- Promoted **only AssetManifest.json, VisualAsset.json and VillagePresentation.json**; all other **38 mirrors unchanged**.
- Builder succeeds, imports exactly five new managed PNG sprites while nine existing sprites report unchanged; repeated build **Success=true / NoOp=true**, signature `57bb0c9e97e4ddcd5275beed3a687ec620f97e85b3d475828d3130fe898dbea6`.
- Asset Refresh / compilation completes with scriptCompilationFailed=false. Scene/Prefab not saved or modified. Metadata is generated by Editor.
- First real Home screenshot revealed narrow person stretching and overly light mesh ground in Linear color space; corrected to uniform person aspect and linear vertex color encoding. B selection/hover brings obscured persons forward; important selection is also a visible outline/text, not only a color difference.

## Verification

Targeted EditMode: **2/2 PASS** (shared managed sprites/missing-key fallback; reversible camera bounds). Targeted PlayMode: **3/3 PASS** (isolated twelve-person actual Continue, stable views/world/Save/Continue; building/collider/depth/selection; actual travel and labor across profile switching). Tool reports discovery totals separately from executed filtered counts; only executed counts are claimed.

First Play fixture incorrectly added twelve characters plus the Hero, resulting in thirteen people; corrected fixture to count Hero. First travel case could be a no-op from initial farm state; corrected to establish kitchen arrival before actual farm commute. Both initial failure receipts retained; no runtime rules changed to satisfy tests. Subsequent targeted run passed all three. The final full suite additionally checks candidate ghost validity/cancellation.

Full EditMode first discovery returned no tests, preserved as non-PASS; retried after confirming loaded test assemblies. Compiler first sandbox run aborted with testhost localhost communication timeout after 90 seconds, zero valid verdict; same local suite restarted after tool automatic-review approved sandbox escalation. Current complete results and screenshot evidence will be appended below.

## Initial full verification (before acceptance cleanup)

| Final suite | Passed / executed | Failed / skipped | Evidence under repository Temp/V17R2 |
|---|---:|---:|---|
| ConfigCompiler | 264 / 264 | 0 / 0 | compiler-results/compiler-full.trx |
| EditMode | 1493 / 1493 | 0 / 0 | full-edit-valid.json; 00:03:02.1519581 |
| PlayMode | 110 / 110 | 0 / 0 | full-play-valid.json; 00:01:33.5635184 |

Both full Unity modes required one discovery retry; initial empty results full-edit.json/full-play.json are not PASS. No runtime or test code changed after these final passes. Later screenshot helpers are ignored Editor diagnostics.

## Actual Game View / 视觉验收入口

All 17 saved screenshots are actual **1920×1080** Game View captures in [Temp/V17R2](../../Temp/V17R2), with existing Village Save adapter snapshots in corresponding `*-facts.json`. These are local ignored evidence, not published final art. All four comparable A/B pairs below have **byte-identical domain snapshots**.

| Case | A / other | B / selected | Reading |
|---|---|---|---|
| Original six-person save | [original-A](../../Temp/V17R2/original-A.png) | [original-B](../../Temp/V17R2/original-B.png) | Same clock/objects, zoom 7.5 |
| Independent twelve-person world | [stress-12-A](../../Temp/V17R2/stress-12-A.png) | [stress-12-B](../../Temp/V17R2/stress-12-B.png) | Separate collection, Hero counted in twelve |
| Five completed buildings | [multi-building-A](../../Temp/V17R2/multi-building-A.png) | [multi-building-B](../../Temp/V17R2/multi-building-B.png) | Two farms, house, kitchen, workshop; actual placement/workers/clock completed construction |
| Home / labor / travel / social / idle | [rollback-A](../../Temp/V17R2/rollback-A.png) | [life-12-B](../../Temp/V17R2/life-12-B.png) | Same paused world at 1/1/11 09:20; commuting Inspector says 尚未到达 |
| Occlusion stress | [behind](../../Temp/V17R2/occlusion-behind.png), [front](../../Temp/V17R2/occlusion-front.png) | [selected behind roof](../../Temp/V17R2/occlusion-selected.png) | View transform staged around actual workshop; simulation presence unchanged. Depth 305 / 485 / selected 20002 |
| Zoom boundaries | [far 11](../../Temp/V17R2/far.png) | [near 4](../../Temp/V17R2/near.png) | Same focal center; near zoom inspects local area |
| Placement preview | [valid](../../Temp/V17R2/ghost-valid.png) | [overlap invalid](../../Temp/V17R2/ghost-invalid.png) | Formal farm Sprite, exact 3×2 outline, explicit invalid text and disabled confirm; canceled afterward |
| Restored original Continue | — | [restored-original-B](../../Temp/V17R2/restored-original-B.png) | Six persons, zero buildings; fixture not persisted |

QA corrections: autonomous fixture activities initially claimed eight unassigned residents, so manual social setup first found no available pair. After real clock advancement and treating empty-string assignment IDs correctly, an isolated Talk opportunity committed through the real Activity API and reached Executing. The captured snapshot has two 岗位劳动, one 通勤, Talk/Drink, 在家休息 and 空闲. No production schedule changed. Placement mouse-follow initially moved the candidate between setup/capture; captures were repeated with the input host temporarily disabled, then restored and the preview canceled. These corrections only affected ignored QA helpers.

### Scale, tradeoffs and limits

Person Sprite canvas is 1.1 world units high with natural aspect: **79.2 px default / 54 px far / 148.5 px near** at 1080p. Source alpha spans 1324/1374 of height, giving approximately **76 / 52 / 143 px** visible silhouette. Smallest full silhouette is about 4.8% of screen height; roof occlusion can reduce this, and selecting/hovering restores visibility. These measurements are sample evidence, not automatic visual approval.

Default real building Sprite canvas width is approximately **144 px for house/kitchen (2 units)** and **216 px for farm/workshop (3 units)**. Natural aspect and bottom alignment preserve the original 2×2 / 3×2 footprint. Roof hit envelope adds clickability without changing occupancy. Generic Home/Place art remains a conceptual anchor, not an extra constructed instance.

A retains whitebox, labels and footprint reading. B distinguishes residential/work regions, gives larger silhouettes and recognizable categories, reduces default labels and suppresses near-label overlap with selected/hover priority. Tradeoffs: one repeated male silhouette cannot convey identity; roof/crop art has more small detail than the clean large-shape target; housing rows remain schematic; selected persons drawn over roofs look like an inspection overlay; flat meadow/path shapes can read as a board. Near zoom crops the whole village intentionally. There is no animation, terrain, gameplay road, interior or navigation claim. Only PNG/Sprite is validated; Spine/Prefab/Animator/Material resource replacement is not claimed.

After acceptance, possible exploration is less roof/crop texture detail, a few distinct person silhouettes and a less regular housing composition. No bulk art production or style freeze is authorized now. A/B rollback restores mapping, anchor dictionary and legacy visuals; it retains session camera position/zoom rather than restoring a separate saved camera state.

## Protection and final state

Original disk save remains **13402 bytes**, SHA256 `337AC2B2E3774DA1412852D3CAAE973C9C19FE1E84752C8B88092A46FD9A9173`. Fixture was an independent NewGame collection loaded through actual Continue; disk bytes were immediately restored and a test-only failed-save guard protected them. Final original Continue restored six persons/zero buildings; guard cleared and Play stopped.

All **22/22** protected paths match opening SHA256, including original dirty TMP/Events/Packages/QualitySettings and untracked screenshot/clipboard/art. Final audit detected automatic TMP glyph-cache and full-suite D screenshot increments; after Play stopped, both were copied from the opening protection backups through Editor, with synchronous font reimport, then hashes rechecked. No restoration from HEAD was used. Hash evidence and complete task inventory: ignored `Temp/V17R2/final-protection.json` / `task-manifest.txt`. Builder output remains in its existing ignored managed folders.

Final Editor: Play=false, compiling/updating=false, scriptCompilationFailed=false; SampleScene loaded and **IsDirty=false**. Last-four-minute Console observation after restoration/stop: **0 Error / 0 Warning**; historical negative-test logs were not cleared. Index empty; HEAD still baseline; task diff whitespace check passes. No task Scene/Prefab, Save/Simulation/domain, dependency or project-setting changes. Shared repository remains dirty because protected and R2 changes are retained.

## Initial acceptance boundary (superseded by the explicit acceptance below)

One producer visual acceptance must assess: village first impression, person clarity, building/ground proportions and layers, interaction readability, and which style features to retain or explore. Until then: **no commit/push, no package closure, no final Handoff, no GameDesign freeze**. Disposable QA tests and ignored screenshots/fixture scripts await the accepted-package cleanup decision. This record preserves current evidence rather than replacing Handoff77.

## Development-phase file inventory (superseded by closeout manifest)

26 task paths; the 22 protected paths above are excluded. No index entries.

- `Development/Config/RRPG_VillagePresentation.xlsx`
- `Development/Config/RRPG_VisualAssets.xlsx`
- `Development/Generated/StaticConfig/AssetManifest.json`
- `Development/Generated/StaticConfig/VillagePresentation.json`
- `Development/Generated/StaticConfig/VisualAsset.json`
- `Development/SourceAssets/Village/R2Prototype/README.md`
- `Development/SourceAssets/Village/R2Prototype/farm.png`
- `Development/SourceAssets/Village/R2Prototype/home.png`
- `Development/SourceAssets/Village/R2Prototype/kitchen.png`
- `Development/SourceAssets/Village/R2Prototype/person.png`
- `Development/SourceAssets/Village/R2Prototype/workshop.png`
- `Docs/DevelopmentSpecs/2026-10-09_RRPG_V17-R2_Execution-Record.md`
- `Docs/DevelopmentSpecs/README.md`
- `Docs/项目状态快照.md`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeBuildingPlacement.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeVisualPrototype.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeVisualPrototype.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldCamera.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/VillageVisualPrototypeTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/VillageVisualPrototypeTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageVisualPrototypePlayTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageVisualPrototypePlayTests.cs.meta`

## 2026-10-09 视觉验收反馈修复：地点跳变

制作人反馈：Tavern建筑与Farm来回变化，Test工作区域02时有时无。根因位于B Profile的布局：每次Reconcile按当时地点列表重新编号；真实活动临时引用Tavern等ActivityPlace时，注册工作地点的编号随之变化。复现测试明确捕获Farm从(9994,-1.2)跳到(9998,-1.2)，即横移4单位；Test02也会被推到另一行，产生替换/消失的观感。资源绑定本身未被切换。

修复仅涉及原有Host与R2 PlayMode测试：B按PlaceId缓存会话位置，活动暂不引用地点时保留其位置编号；注册地点先分配，临时活动地点后分配。新活动地点不重排已有地点，A/B往返复用原B位置；PlayerCollection来源变化才清空缓存。A的原排序与布局保持原逻辑，实际建筑继续采用原BuildingWorldMapper坐标。没有修改ReadAdapter、Registry、Simulation、Save或资源配置。Tavern这类只读活动投影仍按真实活动引用出现/离开，不会替换Farm，也不被伪造为永久Runtime建筑。

新增ActivityPlacesCannotReflowRegisteredWorkplaces测试覆盖Tavern反复增删、四个注册工作地点身份/位置/Sprite/Renderer启用状态、A/B往返及Simulation/Save不变。修复前3 PASS / 1 FAIL，修复后4/4 PASS；有效执行数按Filtered计，不把工具显示的总发现数111当作定向执行数。

完整PlayMode首轮110 PASS / 1 FAIL，失败来自初轮R2 Save/Continue测试对DriverRemainder的JSON字节断言：0.039107900112867358与0.03910790011286736仅序列化末位不同。该测试改为单独验证DriverRemainder误差≤1e-12，其他存档字段仍精确比较；同一会话A/B切换的完整JSON仍逐字节比较。只修正测试判据，没有修改生产序列化。测试helper首次字符串引号转义编译错误也已纠正，最终compileFailed=false。

反馈修复阶段有效完整回归（清理前）：

- EditMode **1493/1493 PASS**，0失败/跳过，00:02:25.5730331。
- PlayMode **111/111 PASS**，0失败/跳过，00:01:33.8231980。
- Compiler/Builder/配置/资源本次未改变，沿用初轮264/264与Builder NoOp证据，未宣称本次重跑Compiler。

证据位于`Temp/V17R2/fix-anchor/`：before.json、targeted-play.json、full-edit.json、full-play-first.json、full-play-final.json。另加载制作人现场两座施工建筑状态的**隔离副本**，使用真实VillageTime推进一天，每5分钟采样一次，共**289次**。Tavern被引用的采样118次，出现/消失切换5次；四个工作地点位置、Sprite及Renderer启用状态全部保持稳定。明细actual-day-anchor-check.txt；[修复后Test02选中截图](../../Temp/V17R2/fix-anchor/fixed-Test02-B.png)为真实1920×1080 Game View，fixed-anchors.txt记录当时位置。

本次保护以**反馈开始时**的用户状态为准：另存live-save.json现场临时状态与disk-save.json磁盘存档，不拿初轮备份覆盖用户后来保存的进展。结束磁盘存档21987字节、SHA256 `FDB8FB23BED2270809C3B5BE57D4D66103BE9927CE550D7464E772BCA5E3132C`与本次开工副本一致。22/22路径匹配本次保护副本；字体在用户查看期间已有增量，该增量被保留，测试新增缓存和D测试截图仅从本次副本恢复。Play已停止，failed-save guard清空，SampleScene IsDirty=false，index为空。反馈阶段任务为26路径；当时未Commit/Push、待视觉验收。后续明确Acceptance与Closeout见下。

## Final Closeout — 制作人技术原型验收通过

制作人明确“V17-R2 制作人验收通过，允许收尾”，报告开发17.0已复核真实Home截图、12人压力、比例/遮挡/选择/镜头及地点跳变修复证据，并授权精确Commit + Push main。**Technical Prototype PASS / Art Master NOT FROZEN**；保留可逆A/B、单一资源链、独立足迹/碰撞/显示包络、深度与选择、镜头和PlaceId稳定缓存，仅这些技术契约通过验收。当前PNG画风内容、最终比例、地面/道路/住宅布局、镜头值、屋顶遮挡、动画技术及量产规则明确不冻结。

### Disposable Cleanup / 必要长期回归

- 删除1项固定Zoom/Pan数值验收：最终镜头参数未冻结，不长期锁定4/11/±18/±12。
- 删除12人专用Save/Continue验收与重复Travel/Labor专项；12人证明是历史验收样本，不保留假人注入器、仅为该验收写的JSON精度正则。
- 将五建筑压力fixture减为2/3宽足迹的最小代表；普通独立NewGame经过真实Continue，无额外假人。
- 保留1 Edit：共享正式Sprite解析及明确缺失fallback。
- 保留2 Play：默认A、A/B实体与领域/Save隔离、占地Collider和选中/深度/建造预览不变量；动态ActivityPlace增删及A/B往返的PlaceId稳定缓存。
- 已清理6个本任务Temp脚本：configure.py、finalize.py、promote.py、repair_package.py、VisualProof.cs.txt、fix-anchor/LiveProof.cs.txt。原用户D-onsite-product.png受保护，不能以Cleanup删除。忽略的保护副本、截图与有效回执保留作本地核验，绝不暂存。
- Runtime无临时Scene/TestRunner/验收Save注入接口；R2可逆按钮与技术原型标记是获准保留的表现入口。只读DepthOrder为必要排序回归的观测属性，不拥有领域写入能力。

### Cleanup后本轮有效全量验证

| 验证 | PASS / executed | Fail / skip | 证据 Temp/V17R2/closeout |
|---|---:|---:|---|
| Compiler full | 264 / 264 | 0 / 0 | compiler-results/compiler-closeout.trx，9秒 |
| EditMode full | 1492 / 1492 | 0 / 0 | full-edit.json，00:03:03.3098450 |
| PlayMode full | 109 / 109 | 0 / 0 | full-play.json，00:01:35.2884550 |
| Builder | Success=true / NoOp=true | — | builder.txt |

Edit/Play各首次模式切换出现空发现，回执无效；一次重试完成对应全量。无测试失败需要修改生产代码。正式Builder无定向新增写入；源图/绑定/Schema未在收尾扩大。最终实际Continue加载用户**6人/2建筑**，默认A，切B→A前后村庄JSON逐字节相同；正常Home观察最后1分钟Console **0 Error / 0 Warning**。SceneDirty=false、compileFailed=false，随后退出Play。旧负向测试日志与反馈阶段已修编译错误不冒充本轮新缺陷。

### 最新用户进展与保护

本轮重新建立22项副本与SHA清单，另备份当前磁盘存档及现场临时快照，未拿初轮旧存档覆盖后续进展。最终存档**21987字节**、SHA256 **FDB8FB23BED2270809C3B5BE57D4D66103BE9927CE550D7464E772BCA5E3132C**与本轮开工一致。22/22保护项逐字一致；自动TMP与测试截图增量从本轮副本经Editor恢复，同步导入字体。保护回执final-protection.json / final-editor.txt；guard已清空。A默认及B可逆不写Save。保护只恢复已知自动增量，不覆盖其它任务内容。

正式文档影响：执行记录、Current Snapshot、日志、开发规格索引、Handoff78、README/地图、Architecture §27.6/对象映射、SourceAssets说明已同步。GameSpec、GameDesign/ArtDirection、决策记录无需变化；Issue未扩大或代为关闭。Handoff78仅更新HANDOFF区，保留生成时main@fa8a78e未Commit/Push的事实。Final Diff / Workspace Review及链接检查在精确暂存前执行。仅本包Closeout授权有效。

### Closure Commit Staging Manifest

**Must Stage — Expected staged file count: 32**

- `Development/Config/RRPG_VillagePresentation.xlsx`
- `Development/Config/RRPG_VisualAssets.xlsx`
- `Development/Generated/StaticConfig/AssetManifest.json`
- `Development/Generated/StaticConfig/VillagePresentation.json`
- `Development/Generated/StaticConfig/VisualAsset.json`
- `Development/SourceAssets/Village/R2Prototype/farm.png`
- `Development/SourceAssets/Village/R2Prototype/home.png`
- `Development/SourceAssets/Village/R2Prototype/kitchen.png`
- `Development/SourceAssets/Village/R2Prototype/person.png`
- `Development/SourceAssets/Village/R2Prototype/README.md`
- `Development/SourceAssets/Village/R2Prototype/workshop.png`
- `Docs/开发验收交接.md`
- `Docs/文档地图.md`
- `Docs/项目日志.md`
- `Docs/项目状态快照.md`
- `Docs/Architecture/核心对象与术语映射.md`
- `Docs/Architecture/技术架构总览.md`
- `Docs/DevelopmentSpecs/2026-10-09_RRPG_V17-R2_Execution-Record.md`
- `Docs/DevelopmentSpecs/README.md`
- `README.md`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeBuildingPlacement.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomePresentationResolver.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeProductShell.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeVisualPrototype.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeVisualPrototype.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldCamera.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/HomeWorldEntityView.cs`
- `UnityProject/RRPG1.0game/Assets/Scripts/Village/ProductHome/ProductHomeWorldHost.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/VillageVisualPrototypeTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/EditMode/Village/VillageVisualPrototypeTests.cs.meta`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageVisualPrototypePlayTests.cs`
- `UnityProject/RRPG1.0game/Assets/Tests/PlayMode/Village/VillageVisualPrototypePlayTests.cs.meta`

**Must Not Stage — 22项保护 + ignored证据/用户存档/Generated Unity资产**

- `Development/Config/RRPG_Events.xlsx`
- `UnityProject/RRPG1.0game/Assets/Art/Fonts/SourceHanSansCN/RRPG_SourceHanSansCN_Regular_TMP.asset`
- `UnityProject/RRPG1.0game/Packages/manifest.json`
- `UnityProject/RRPG1.0game/Packages/packages-lock.json`
- `UnityProject/RRPG1.0game/ProjectSettings/QualitySettings.asset`
- `2026-10-04_RRPG_Autonomous-Life_Authority-Landing.diff`
- `2026-10-04_RRPG_WP-AL-1_Runtime-Review.diff`
- `UnityProject/RRPG1.0game/.codely/clipboard/clipboard-1791519984537.png`
- `UnityProject/RRPG1.0game/.codely/clipboard/clipboard-1791520308853.png`
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
- `UnityProject/RRPG1.0game/TestResults/D-onsite-product.png`

Temp/V17R2、TestResults的验收产物、用户save路径、Unity Assets/Generated/StaticConfig及其它任务变化均排除。清理后的32路径取代上方开发期26路径清单；包含两个长期测试及必要.meta，无一次性Runner。

生成清单时状态为 **Final Closeout Prepared / Commit & Push Pending**，local HEAD / origin/main / 实时GitHub main均fa8a78e651cf5fc66a5c3af3dd0451354e66eb19，index为空。用户已授权本任务写Git；实际Commit / Push / 三方SHA证据见后续Actual publication，生成时不预宣称Work Package Closed。共享workspace保留保护项，不宣称Branch/Repository Closed。Deferred保持上述美术、动画、玩法/世界级事务等边界；不自动启动下一包。

暂存前格式复核：文档EOF多余空行已清理；3份Editor生成.meta的空userData / assetBundleName / assetBundleVariant行保留Unity原生尾空格，不直接手改序列化元数据。代码、JSON和Markdown的diff whitespace检查通过；.meta由Editor回读GUID验证。

## Actual publication — Work Package Closed

2026-10-09正常Commit + Push main完成：Closure **`2a52f856c7b6e3e281f992c7aa15472a0dd2dd6e`**，32实际提交路径与精确Manifest逐项一致。推送后local HEAD / origin/main / 实时GitHub main（git ls-remote）三方均为该SHA；index为空，22/22保护文件仍与本次开工哈希一致。当前用户save21987字节及上述FDB8FB23…SHA完整。清理后264/1492/109 PASS证据与Builder NoOp有效，后续仅同步本节和3份状态/历史文档，不改Runtime、配置、资源或测试，也不扩大回归。

**R2 Work Package Closed / Producer Technical Acceptance Passed / Published main；Art Master NOT FROZEN。** Handoff78保留生成时fa8a78e、未Commit/Push事实，实际发布读本节；本文随后状态同步提交的最新HEAD以实时Git为准，避免自引用未来SHA。共享workspace的22项任务外变化继续保留，不宣称Branch Closed / Repository Closed。PNG画风内容、最终比例/布局/镜头、屋顶遮挡与动画、美术规范/量产、新玩法和其它工作包仍Deferred；本包完成后停止。
