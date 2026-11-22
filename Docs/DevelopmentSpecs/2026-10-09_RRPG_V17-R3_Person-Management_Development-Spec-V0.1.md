# RRPG V17-R3-P｜Person Detail / Management Development Spec V0.1

> 日期：2026-10-09（Asia/Shanghai）。Target Branch：main。
> 审计基线：local HEAD / origin/main / GitHub connector 实时 main 均为 `ef4da17457c7db5f1fd2502212ebf1f842e33d59`。
> **R3-P PLANNING ACCEPTED / PUBLISHED；Work Package Closed（仅R3-P）。R3-A Person Detail为发布后的下一阶段，已获连续推进确认 / Not Started；R3-B Not Implemented / Not Authorized。**
> 本文是开发期规划与静态审计证据，不是新增 GameSpec Authority。只约束对应实施范围；制作人已验收本规划并授权Commit + Push main。本任务仅完成R3-P规划收尾，不实施R3-A/B。

## 1. 请求、Bootstrap 与保护

用户请求“执行v17-R3-p”，采用附件《Codex_V17-R3-P_规划审计_2026-10-09.md》的本包范围。附件关于既有 Gate 自动推进的文字不作为跳过审计、进入 Runtime 或 Git 发布的独立授权。

已回读 README → AI_CONTEXT → 文档地图 → .clinerules / 根 AGENTS → GameDesign README → GameSpec README → Current Snapshot；按任务回读 Village §7.16、当前设计状态、Person / Village / Product Home / Building / Resource / Save GameSpec，Architecture §23～27及对象映射、Handoff78、V16累计记录与 V17-R1/R2发布证据、AI执行规则 §13。仓库没有独立 Card / Social / History GameSpec；卡牌成长规则归 Person §7～10，Social / History 分工归 Village §12及现有技术架构，不能创造第二套规则入口。

代码审计从真实本地文件读取正文和调用链；Prefab只作磁盘静态结构核对，**未读取本轮 Editor 现场、未运行 Unity / Builder / Play / Save / Load**。因此不宣称 Prefab 当前场景绑定、可见性或模态行为已在本轮运行验证。实施开工必须通过 Editor 回读真实宿主。

初始 main 工作区有5项 tracked 修改和17项 untracked 文件，共22项任务外文件：Events XLSX、TMP、Packages、QualitySettings、图片/meta、两份旧 diff、clipboard和旧截图。只记录路径/哈希，不审查任务外正文/diff，不 reset/clean，不覆盖、不暂存。用户存档不执行任何读取/写入游戏操作；本轮未取得 Editor 的实际 persistentDataPath，不借 Handoff78 的历史存档哈希冒充本轮核验。

Git默认代理不可达，单次禁用代理的只读 ls-remote亦遇连接重置；转用 GitHub connector `fetch_commit(main)` 成功取得实时SHA并与 local HEAD / cached origin/main 比较。未更改代理配置。

## 2. Authority → Current Query / Command → 页面

以下类路径均位于 `UnityProject/RRPG1.0game/Assets/Scripts/`，方法名来自正文，不代表方法具备未写明的规则保障。C=Current代码能力；T=Test / Prototype内容或文案；G=缺少产品投影/接线。只读页面复制不可变展示数据，不持有可写领域对象。

| Authority / 来源 | 已存在的真实 Query | 已存在的真实 Command / 边界 | 页面用途 / 审计结论 |
|---|---|---|---|
| PlayerCollection（Core/PlayerCollection.cs）；Person GameSpec §1 | `FindPersonByPersonId`、`GetCharacterByPersonId`、`GetHeroByPersonId`、OwnedCharacters / OwnedHeroes | 详情不创建人物、不改核心字段；不调用按模板移除人物的 Legacy方法 | C：Roster和详情按 PersonId；同模板/同名不得合并。G：无统一已离村/已移除历史人物档案查询 |
| VillagePersonAvailability（Village/Runtime/VillageAvailability.cs） | `Resolve(BuildPersonInputs(source), ExpeditionPersonIds)`；远征优先、IsDead→Unavailable | 页面不登记远征、不同步死亡；不用有同步副作用的 `GetPersonAvailability`作纯投影 | C：在村/远征/不可用；“Unavailable”不直接翻译为“死亡”，须查具体人物事实 |
| ProductHomeReadAdapter / Snapshot | `Read`；`Person(id)`、`Place(id)`、`Building(id)` | 无页面领域写入口 | C：概览、当前活动/地点/目的地、长期职责、现场人数；T：Home/ActivityPlace fallback仅表现投影，无住宅所有权 |
| 唯一 Presence / Intraday Work | `VillageRuntimeBridge.QueryIntradayPresence`、`IsExecutingWork`、`GetWorkExecutionLabel`；`ActivityRuntimeBridge.QueryClaim` / `QueryActivityInstance` / `QueryPersonActivityState` | 不以Sprite位置写Arrival、劳动、活动状态 | C：区分工作/通勤/午休/下班/活动；不可从Assignment或项目名单断言正在劳动。`QueryActiveInstances` / `QueryPersonStates`会EnsureInitialized，不作为无源页面纯查询 |
| Primary Assignment / Workplace | `Assignments.GetRecord`、`GetWorkplaceDef`、`WorkplaceDefs`、`GetOccupantCount`、`GetOccupantPersonIds`；Bridge同源查询 | `TryAssignPerson(personId, workplaceId)`、`RemovePersonAssignment(personId)`；满员/AlreadyAssigned由领域返回 | C：人口职责列及实例岗位表。命令仅守世界锁，未校验人物存在/在村；产品调用前须重查来源/Availability，见§7 |
| Building / Project | `Buildings.All` / `Find`、ConstructionProjectId、`Projects.GetActiveProject` / `GetActiveProjectOfPerson` / `GetProjectDef` | `TryAddConstructionWorker(buildingId, personId)`、`TryRemoveConstructionWorker(buildingId, personId)`；只有真实劳动推进 | C：正式施工职责管理。T：底层施工复用 `prj_test_01`及Test参数；产品不提供“创建Test Project”目录 |
| 临时占用 / Rest | `GetPersonOccupancyReason`、`IsPersonTemporarilyOccupied`；`PersonRest.IsRestBlocked` / `GetLatestRest`，合法日期由Time取 | 不新建Occupancy，不从UI授予Rest；远征入口保持既有调度 | C：长期职责保留，当前执行可暂停；项目名单不是全天占用。Rest截止记录不等于当前仍阻断 |
| GrowthAuthority / CharacterGrowthService | `GrowthAuthority.Resolve(person)` → `IsAvailable` / `IsProfessionGrowth` / Level / Exp / UnlockedPoolCardIds / TraitLevel / StarterSlots；`CanLevelUp` / `GetExpRequired` / `GetNodeForLevel` | `TryLevelUpOnce(..., out node)`、`ApplyFixedNode(...)` | C：普通人有职业读ProfessionProgress；无职业不可成长且无Legacy fallback；Hero维持Legacy扩展。T：曲线/节点/特性数值未冻结 |
| CardGrowthResolver / Person §7～9 | `BuildOffer(person, allTemplates)`最多3张去重、按当前解锁池顺序；不是随机抽样API | `ApplyToStarter(person, cardId, strategy, slotIndex, curve)`；支持空槽/替换/熟练度，Skip由现有UI处理 | C：复用真实选择流程；不能从Full Pool补候选。该低层命令不验证选牌来自当前offer，调用流程必须保持同一待处理候选 |
| ProfessionDefinition / ProfessionProgress | Character.ActiveProfessionProgress；ProfessionDefinition.FullCardPool / ProfessionTraitId；现有 DataManager.ProfessionRegistry | 不写模板、不手动Formation/转职 | C：当前职业ID、个人成长/牌池；无职业DisplayName字段，缺配置则显示ID或“资料暂缺”。T：正式职业内容/命名仍未冻结 |
| Social DirectedRelationshipRegistry | `IRelationshipQuery.TryGet` / `GetOrNeutral`只支持有向pair；Bridge只公开 `QueryRelationship(source, target)`，无存在标记 | 页面绝不 CommitRelationalFact 或改Affinity | C：方向性；G：Bridge没有关系枚举/存在性产品接口。禁止将neutral fallback渲染成已记录关系 |
| Social canonical relational facts | `SocialRuntimeBridge.QueryFacts()`；RelationalFactRecord的Actor/Target/日期/SourceOccurrenceId | 只由生产Social解释，不由页面结算/Drain | C：可按当前 PersonId过滤真实互动参与对象，去重后逐pair查询；不推导对称关系、友情称号或正式强度档位 |
| PersonLifeHistory（Profession/Runtime/PersonLifeHistory.cs） | Character.LifeHistory.Events；ProfessionFormed / ExpeditionReturned；日期、ProfessionId或Outcome | 页面不Append、不补历史 | C：最小人生事实，按日期和原顺序展示；Hero没有LifeHistory字段，显示“尚无记录”。G：完整日记/Episode/死亡/离村事实及长期移除后档案 |
| WorldEventHistory（Event/WorldEventHistory.cs） | `Records`、`OccurrenceCount`、`TryGetLastVillageDayOrdinal` | 只由Event成功事务 `RecordOccurrence` | C：EventId聚合次数/最近日/最近实例，无Person绑定/逐次明细；不能当人物人生经历，完整History Surface不在R3 |
| VillageSocialFactLedger | `Facts`、`CountTemporaryCommitmentsInYear(personId, year)` | Bridge只在真实成功Project加入后记录 | C：真实临时承诺动作，与Social relational ledger不同；T：早期TemporaryCommitment内容/文案。不能用次数编造抱怨/人生片段 |
| Resource / IFulfillmentHost | `Resources.Ingredients` / Food；Character/Hero.Hungry / ConsecutiveUnderfedDays；`IntradayWork.ToSaveData`已有批次/营养镜像 | 管理页不 Add/Consume库存、不手动加工；领域自然劳动/午餐结算 | C：库存与已有个人饥饿事实；G：Home没有实时批次/已结算供餐的专用只读产品投影；不凭配置产能冒充实际日产/预测 |
| Save / NewGame / Continue | `SaveSystem.Save`返回bool；`Load`、`NewGame`与Town.Initialize现有链 | 普通Save受 `CanSaveWorld`约束；推进/返程锁内不能保存 | C：保持SaveVersion=1、无新增DTO/UI State。旧Growth UI忽略Save返回值，旧Commitment操作不显式Save；不得宣称已自动持久化 |

### 2.1 真实宿主与旧UI定位

`TownUI.Initialize(collection)`绑定唯一SimulationPersonSource并启用Simulation；MainMenu Continue经Load→EnterTown进入。ProductHomeWorldHost独立于TownCanvas，持有同一个Selection / WorldCamera / Inspector / Shell；换PlayerCollection清Selection和表现缓存。Router只是World / Person / Management / Cards / History枚举与Open/Close，不拥有Clock。

HomeProductShell目前运行时创建TopHUD/导航及 `HomeInternalWhitebox`，Person/Management文本明确“完整页面待后续实现”。HomeWorldInspector已有正式施工加入/撤出、完工岗位派人动作，不应另造Building管理Authority；完工岗位只取首个function且隐藏已有职责人物，没有集中撤职/多Slot选择。

CharacterGrowthUI是已有模态成长控制器；Prefab `Assets/Prefabs/UI/CharacterGrowthUI.prefab`静态包含GrowthCanvas、Content、Mask_Dim（raycastTarget=1）、ListRoot、DetailRoot、BtnClose/BtnLevelUp及组件引用，sortingOrder=100。`Open()`无PersonId参数，会清选择/待处理选牌；`Hide()`只隐藏；人物选择方法为内部流程，未接HomeSelection。完整人物页不能只重命名它。

VillageCommitmentUI是已有Functional/Test界面，复用的View含Test Contribution、Test Project、人数×日进度估计与旧社会回应文案。`BuildPersonRows`会把有Assignment的人标为Working，即使现时实际下班/通勤，不能原样当生活页。可复用纯查询组合思路，产品文案须来自真实日内执行；旧界面保留在“调试/对照”，不进入新的正式管理目录。

## 3. R3-A 最小 Vertical Slice：人物详情

目标：从真实World或人物名册找到同一人，读懂当下生活与职业成长，使用已有成长操作，看见已有真实人生事实。五页签都有可用最小内容；数据缺失提供空态，不以待开发标签掩盖可查询内容。

| 页签 | 最小交付 | 缺口与边界 |
|---|---|---|
| 概览 | 姓名/PersonId调试识别、人物或Hero类别、当前职业、可用状态、主要职责、临时执行状态；Roster选择并支持姓名检索 | 无源/人物消失明确“人物资料暂不可用”，禁写入；Hero不假造职业 |
| 生活 | 实际活动、当前位置/目的地、通勤、职责与当前执行分列、已有恢复期、饥饿和连续不足天数 | 不显示心理/疲劳/住所产权/年龄/每日完整Schedule；没有营养明细Query则只显示已保存的最小事实 |
| 职业成长 | 经GrowthAuthority读Level/Exp、个人Unlocked Pool、Starter、Trait progression；Full Pool另标理论池；打开所选人物真实成长流程 | 无职业显示“尚未形成职业”；不暴露Formation内部数值。Trait定义缺失只显示已有ID/等级，禁止编效果 |
| 关系 | 从QueryFacts按Actor/Target收集当前人物的真实互动对象；显示参与事实与两个方向当前查询结果，点对方跳同一Selection | 仅已知事实参与对象，不宣称完整关系清单；未知ID显示ID且不能跳不存在的人。没有事实显示“尚无记录”，不生成全名册neutral关系 |
| 人生经历 | Character.LifeHistory中的职业形成与远征返回；日期、真实类型和合法Outcome映射；同日保留原记录顺序 | 未知Outcome只显示“返回结果资料暂缺”；无记录/Hero显示“尚无记录”；不引入WorldEvent/Feed补档，不造Episode |

R3-A准确最小范围包括World→Inspector→人物详情、导航→Roster→人物详情、Attention→同一人成长页；所有选择调用现有 `Selection.SelectPerson(id)`。关系中的对方已离开集合时保留事实ID，不造一个Person实例。

成长整合建议：复用唯一CharacterGrowthUI控制器/Prefab及服务，在原控制器增加窄的所选Person绑定和Home关闭回调接缝（**建议接口，当前不存在**），不复制算法/节点流程、不另建Growth Manager。详情固定页签及容器作为Shell原有Person Surface的可编辑宿主；成长内容的布局在原Prefab小范围调整，确保Top HUD仍可見/可用，模态阻断其余Home/World操作。不强制重组整张Scene。

待处理选牌必须保持同一人物和offer：切页/切人/关闭不能静默丢失或刷新候选。最低风险候选为完成当前选择或显式Skip后才切换/关闭；Skip沿现有语义消耗已升级经验且保留等级。是否允许“先退出、稍后处理”需制作人另行决定，现有临时字段不支持跨进程恢复，R3不自行加入持久pending状态。

拒绝实现：新增职业/Formation规则、转职/多职业、生命模拟、关系编辑/称号/阈值、新卡牌构筑体系、随机reroll、日记/Episode/完整History页、死者档案系统、把Legacy Injury/IsDead当成新Battle永久死亡玩法。

## 4. R3-B 最小 Vertical Slice：集中管理

目标：跨人物和具体岗位/建筑比较现状，在合法命令上完成派人、撤职、施工加入/撤出，看到现有Ingredients→Food链继续运行。

| 子页 | 最小交付 | Command及限制 |
|---|---|---|
| 人口 | 同一Roster，姓名/职业/在村或不可用/主要职责/现时执行/临时承诺/饥饿可比较；按名称和状态筛选，点人物进详情 | 过滤排序只属会话，人数由同一数据快照计算；选人不自动派工 |
| 职责与场所 | 一行一个WorkplaceId，关联BuildingInstanceId + FunctionSlotId，容量/已绑定名额/实际劳动人数/现场人数分列；施工行单列进度及工人 | 已Completed动态岗位可 `TryAssignPerson` / `RemovePersonAssignment`；施工只调用 `TryAddConstructionWorker` / `TryRemoveConstructionWorker` |
| 资源 | 当前Ingredients/Food、在村供餐对象及已有Hungry人数；列出现有Farm/Kitchen实例、名额与实际劳动人数，可导航到职责 | 无资源写按钮、无预测警报/效率排名；批次/供餐细节须先增加薄只读投影，首片可后置，不阻塞已有真实库存展示 |

动态岗位通过正式Building registry的FunctionBindings枚举slot，使用现有 `BuildingWorkplaceId(instance, slot)`与实际注册 `GetWorkplaceDef`相互核对，不按同名合并，不仅取FirstOrDefault。未完工建筑不出可派工岗位；0工人表示当前无人推动，保留项目进度，不写Pause进度字段。

现有静态岗位不能被静默丢掉，也不能假装正式建筑：若存档已有静态Assignment，在“既有职责（兼容记录）”组显示当前绑定/容量与撤职；本片正式新增派工目标只来自Completed Building实例的已注册Function。保留静态生产能力和记录，不把`ws_farm_01`等固定种子扩展为正式建筑全集。

人口/岗位/施工跨页使用同一Selection；选Building或Place可进入对应行，选择Person可查看该人的职责。岗位名额是长期Assignment绑定数，即使人在Project/远征仍可能占名额；实际在场/实际劳动数独立计算，不能用视觉重叠或ParticipantCount替代。

不提供原子“改派”按钮：已有Assignment时领域返回AlreadyAssigned，先显式撤职再派新岗位。撤职会移除该Assignment记录及其累计工作统计，不包装成可随时无损Undo，也不自动Remove→Assign或失败再造原记录。若产品要求原子换岗/保留累计工作记录，需要额外领域契约审计与制作人确认。

新增管理操作成功后更新视图，允许显式“保存”调用 `SaveSystem.Save(source)`且显示bool结果；不得把内存改变称为已保存。失败显示保存未成功、保持当前状态供稍后重试；不自动rollback，不改变全项目保存Ownership。旧调试操作继续按原行为保留。推进/返程busy时命令和保存禁用并给出已有诊断，不自动暂停/解锁。

拒绝实现：Test Project创建目录、Test Contribution排行榜、人数估算完成日、全自动最优派工、岗位→职业即时转换、无来源适配分数、批量事务、正式经济重平衡、材料/仓储/物流、建筑升级/拆除、新Activity规则、静态岗位或历史档案破坏性迁移。

## 5. 页面、UI与路由接缝

| 既有宿主 | 建议增量 / 退出策略 |
|---|---|
| ProductHomeWorldHost | 保留唯一世界、镜头、Selection；只提供当前源与快照，读模型不得初始化/写Authority |
| HomeProductShell / HomeProductRouter | 原Person / Management Surface替换白模正文；保留TopHUD/底部导航/World背景；无额外Scene或第二Home入口 |
| HomeSelectionContext | Roster / World / Attention / 关系跳转共同消费Changed；列表自己不保存第二份长期当前人物。来源替换取消页面选择/订阅 |
| HomeWorldInspector | 继续短摘要/聚焦；“查看人物/管理”进入同一页；施工快速动作与集中页调用同一Bridge，集中页验收后再移除对照入口导流文案 |
| CharacterGrowthUI / Prefab | 同一控制器增窄绑定接缝，复用已有节点流程；产品入口拒绝SaveSystem.NewGame/Load兜底，必须传入当前源；旧成长按钮最后迁移，不先删除Prefab |
| VillageCommitmentUI / View | 保留Test / Functional调试入口；复用容量Query，替换过时Working/估算文案；正式管理切换验证后退出玩家主入口，无Runtime整体删除 |
| 新薄展示组件（名称建议） | PersonDetailView / ManagementView及不可变读模型只服务两个已确认Surface；共享Person行模板，专用增长/职责回调；不预建万能Framework/导航总线 |

正式固定UI结构应通过Editor创建/绑定可编辑Prefab：标题、关闭、页签、滚动列表容器、详情字段/操作区域及Blocker持久化；Runtime仅生成重复行/候选/卡牌内容。现有Shell程序化白模不作为永久规范，本片仅替换Person/Management固定结构，不顺带重做整个HUD/世界。

内部页面和成长模态不调用SetSimulationActive(false)、Time.Pause或改Speed。正常浏览世界实体继续更新；手动Pause则保持玩家选择。关闭Person/Management/成长主模态返回World，保留仍有效的选择，不能让旧`Hide()`只露出一层残留页面。ESC与关闭按钮走同一回调。

模态必须有全屏Raycast Blocker，并结合World输入拦截；不能仅靠Panel覆盖区域或Canvas sortingOrder。TopHUD时间控制置于可操作的模态允许区域；世界点击、镜头输入、建造、底部导航、旧Town按钮在模态中都须阻断，嵌套选择不能让底层抢ESC。Home host 当前按EventSystem Raycast命中阻断鼠标，键盘/镜头/旧模态组合仍需Play实证；不在R3-P宣称已经符合。

Expedition仍通过Town.EnterProductExpedition→现有准备/远征gating；不能借内部页规则恢复Away Simulation。既有Pending事件/Return推进阻断优先保留，不能把事件PauseOnOpen或busy状态统改成不暂停。

## 6. R3-A/B 实施顺序与验收路径

制作人已明确确认R3-P发布成功后下一阶段为R3-A Person Detail，按连续推进规则无需重复授权；当前任务只收尾发布，不启动A。R3-B尚未获实施授权。依次：现场Bootstrap/保护/Editor宿主回读→薄Query投影与专项测试→在原Surface绑定可编辑UI→真实MainMenu入口集成→产品操作验收→Disposable清理与最终回归→授权范围内Closeout；R3-P本轮停止在发布收尾；下一轮A沿已确认连续推进规则进入，B不由此自动授权。

R3-A先完成同PersonId五页签最小只读片，再接入同一人成长流程；A验证后B复用Roster与详情导航，接实例岗位/施工命令。缺合法数据/Query时保留明确空态和延期，不为页面齐全而发明事实。

制作人产品验收卡（将来实现后执行，本轮未执行）：

1. Continue进村庄，点击一人再打开人物页 → 名字一致，五页签可读；关闭回到原世界。
2. 从人物列表选择同一人 → 资料一致；同名人物可独立选择，互不串数据。
3. 浏览生活与成长页 → 村庄仍活动，顶部暂停/速度可用；手动暂停后切页不自行恢复。
4. 有资格的人升级一次并完成选牌/跳过 → 仅此人成长改变；候选来源于其已解锁牌，关闭不会遗失未完成选择。
5. 看关系和人生经历 → 只有已发生记录；没有记录时明确“尚无记录”，点击现有关系对象可查看本人。
6. 打开管理，在两座同类完工建筑分别派人 → 各自名额和人物独立；满员时拒绝，通勤与实际劳动可区分。
7. 撤职，再分配到另一岗位 → 旧岗位名额释放，新岗位显示正确；不把撤职当作撤施工。
8. 加入/撤出一座施工建筑的工人 → 原主要职责仍保留；无人时进度停，有人实际到场才继续，完工仍是同一建筑。
9. 在资源页观察、保存后Continue → 库存/职责/施工及成长结果正确恢复，无重复生产或历史新增；保存失败明确提示。
10. 从World进入远征准备，再回村 → 原世界与资料恢复，内部页未绕过远征/返程阻断；模态期间点击背景不触发选人/派工/建造。

## 7. 风险、未决与Stop Conditions

| 风险 / 真实证据 | 处理与停止边界 |
|---|---|
| Growth UI的Open清 `_pendingNode/_pendingOffer`，Hide不结算；升级先写等级，CardGrowth后才保存；保存bool未检查 | A实施先复现关闭/重开/切人/失败Save。会话内保留或要求处理/显式Skip可作窄UI修复；需要新增持久pending、退款规则、跨进程恢复或改变既有成长契约即Stop并报制作人 |
| TryAssignPerson仅世界锁+AssignmentSystem；TryAssign只校验非空ID/岗位/容量，没有真实人物/Availability防御 | 产品动作必须即时从当前源重新解析，远征/死亡/消失/busy不提交。若要求将领域公共接口全局加强或改既有规则，单独审计报制作人，不把本片UI防御称为修复全局Authority |
| RemovePersonAssignment没有RefreshWorkIntent，而TryAssign有；IntradayWork批次/到岗可能滞后 | B专项验证“劳动中撤职→立即停止后续贡献/清旧意图”。若重现需要改Work/Clock已关闭包核心行为，停止相关写操作并报告，不在规划阶段宣称已证实或已修复 |
| Social Bridge没有关系存在标记/枚举，neutral无法区分真实0关系与缺失 | 最小片仅事实参与对象+现有pair查询，明确范围。完整关系含无Fact种子、已移除对象资料、语义档位需新查询/规则决策，不能绕到internal Records或用Save DTO当Runtime Authority |
| 人物删除会从OwnedCharacters移除；RemoveDeadCharacter按模板删除多个实例；没有历史人物Registry | P只记录，不调用/不修；被移除ID不造新对象、不以邻近模板补绑。要求长期死者/离村档案或迁移核心集合即Stop |
| 当前设计/早期GameSpec写Implementation Not Started，与V16/R1/R2后续发布记录不一致；旧Commitment估算Working也过时 | 归类为历史实施状态/展示备注滞后，规则正文仍有效；不自行重写GameSpec或批量治理。冲突涉及真实规则而非状态时交制作人裁决 |
| 无正式职业显示名、Test节点/生产/施工参数 | 显示真实ID和已存在内容，实施报告标Prototype参数，不建立正式名字/经济/成长规则 |
| Modal与持续Simulation，事件/Return busy叠加，低分辨率/中文字体 | Editor回读旧Canvas/Blocker/顺序，专项实证；需巨大Scene/Prefab重构、字体资产/Packages改动时Stop，保留受保护TMP |
| Save Trigger Ownership未统一；世界级Village/Event Load transaction仍未解决 | 仅复用现有合法Save并反馈bool，无新的自动保存总管；SaveVersion/DTO/PlayerCollection/公共核心接口/Authority变化均Stop，明确报告制作人 |

无需制作人裁决即可写入本P的部分：现有Authority事实、只读页面职责、实例名额/现场人数分离、复用宿主、空记录与测试边界。需后续决定：持久待处理成长/原子换岗/完整关系与死者档案是否另立包；本片建议不包含，因此不阻塞规划交付。

## 8. 测试矩阵（未来A/B，本P未运行）

| 模式 / 入口 | 必须验证 | 现有回归入口 |
|---|---|---|
| EditMode：人物只读投影 | 同模板不同PersonId、Hero/无职业、有职业GrowthAuthority路由；读取不改变Player/Village/Social/History/Clock；来源替换/未知ID/空态 | ProductHomeFoundationTests、HomeProductWhiteboxTests、CharacterGrowthServiceTests、ProfessionM4 Growth Tests |
| EditMode：关系/历史 | 双向独立、无事实不生成关系、缺目标只显示ID；LifeHistory两种事件/未知Outcome，同日稳定顺序；WorldEvent/VillageSocialFacts/relational facts不混成账本 | VillageSocialFactLedgerCoreTests、Social域既有测试、Formation / Save历史测试 |
| EditMode：管理命令 | 两同类建筑多Slot独立、未完工拒绝派岗、满员/AlreadyAssigned/重复加入拒绝或幂等；人物离开/消失/busy时不写；静态兼容职责仍可见 | VillageAssignmentCoreTests、VillageCommitmentViewTests、BuildingConstructionTests、BuildingResourceTests |
| PlayMode：真实产品入口 | SampleScene MainMenu Continue→Load→EnterTown→Town.Initialize，不用SetActive代替进入；World/列表/Attention同一选择、来源重绑不残留旧人 | ProductHomeFoundationPlayTests、既有Home集成用例 |
| PlayMode：成长 | 原Prefab真实选人→主动逐级升级→固定/卡牌/空槽/替换/熟练度/Skip；切人/关闭/ESC/重开不丢pending、不reroll；保存失败不声称成功 | CharacterGrowthPlayTests、GrowthRunIntegrationTests |
| PlayMode：模态/布局 | 点击/拖拽/滚轮/键盘不穿透到World/建造/旧Town；TopHUD暂停/速度可操作；关闭回World；中文/滚动长列表/1920×1080和1280×720可读 | CommitmentModal_BlocksTownButtons旧测试只能作机制参考，需新Surface真实产品测试 |
| PlayMode：劳动与资源 | 劳动中撤职、换岗、加入/撤施工立即影响合法后续意图；施工保留Primary Assignment；双Farm/Kitchen实例资源批次与材料独立 | BuildingConstructionProductPlayTests、BuildingResourceProductPlayTests |
| Simulation对照 | 同一测试世界同一seed/目标分钟，World、Person、Management及成长模态无操作时实际Clock/Work/Activity/Social/Resource结果一致；玩家Pause保持；正常/加速/Jump/Return共用唯一推进 | 日内Advance及Home长期不变量；禁止按动画到场判断 |
| Save/Continue/NewGame | 显式合法保存后所有相关事实连续；无UI/Selection入档，无Load Tick/Drain/Join/生产/History重复；旧null区块、非法恢复保留既有领域；新世界清页面/选择/订阅 | VillageAssignmentSaveTests、BuildingResourceProductPlayTests、SocialSave、已有Save/Return回归 |
| 远征/事件gating | 从Home既有入口→准备→远征/Return→Home；busy/pending事件保持既有优先级；关闭内部页不解锁Return或启动第二Clock | 真实Town / ExpeditionV4F Play回归 |

实施验证按Compile/Console→专项Edit→专项Play→受影响完整Edit/Play串行执行；不并行Test Runner、不把0发现算PASS。测试会触及单槽档时，必须先从实际Editor路径备份当前档字节并在结束恢复；不得用R2历史备份覆盖用户新进展。一次性验收测试/截图/Runner放忽略临时目录，收尾只留保护稳定身份、Authority、模态、连续Simulation/Save的最小代表集合。

本P验证仅Markdown范围、diff whitespace、相对链接、Bootstrap入口/状态/原22项保护哈希；不运行编译、测试或无关Builder。V17-R2的264/1492/109 PASS只是历史证据，绝不是本轮R3 PASS。

## 9. 日期化状态与下一步

| 模块 | 原方案 / Current | 本次新增规划 | 已否决 | 确认依据与验证 | 未解决 / 下一步 |
|---|---|---|---|---|---|
| Person | 白模Surface + 独立Growth模态 | 同PersonId五页签、统一Roster/选择、复用Growth | 重命名Growth为完整人物页、模板身份查人、第二成长服务 | Person / Product Home Current规则；代码静态回读 | R3-P制作人规划验收通过；发布后下一阶段A，Not Started；pending成长关闭/Save专项保留 |
| Management | 白模 + Functional/Test Commitment + Inspector | 人口/实例岗位与施工/资源，合法命令，保存结果 | Test目录转正、挂名人数当劳动、自动原子换岗 | Village / Building / Resource规则；代码静态回读 | 规划已采用，B实施仍须授权；撤职意图专项验证与静态兼容边界保留 |
| Social / History | 有向pair/Fact、最小LifeHistory、事件聚合与TemporaryCommitment各自存在 | 只读当前Fact参与对象、已有LifeHistory/空态 | neutral造关系、合并账本、Feed补档、人生故事生成 | Village §12 / Person §12.7；代码静态回读 | 完整关系/历史档案/Instance明细后置，不新增规则 |
| Home / UI | 同一世界/Selection/Router，Shell程序化白模 | 原Surface可编辑宿主、真实模态与TopHUD连续 | 万能Framework、第二Home、导航自动Pause、巨大Scene重构 | Product Home §23 / 既有Prefab静态结构 | Editor现场及运行期交互本轮未验证；A开工回读 |
| Git / Docs | main@ef4da17，既有22项dirty | 新规格+最小导航/状态/日志，规划材料记录 | 将R3-A/B写成已实现、提前宣称发布、关闭84/85/86/88 | local/cached origin+connector实时main一致 | R3-P规划已验收并授权精确发布；本任务不实施A，B未授权 |

GameSpec / Current Architecture / 决策记录无需修改：没有本轮规则裁决或Runtime变化。无需另建Checkpoint重复上述Authority；本规格集中保存范围、缺口、建议与拒绝项。制作人规划验收后更新Handoff79与本包状态/日志/索引；记录生成时与实际发布的不同Git事实。Handoff78作为历史由Git追溯，不把R2历史测试当成R3证据。

## 10. 精确文档范围与制作人报告

本次仅6个仓库Markdown路径：

- `Docs/DevelopmentSpecs/2026-10-09_RRPG_V17-R3_Person-Management_Development-Spec-V0.1.md`（新增）
- `Docs/DevelopmentSpecs/README.md`
- `Docs/文档地图.md`
- `Docs/项目状态快照.md`
- `Docs/项目日志.md`
- `Docs/开发验收交接.md`（首页状态及Handoff79最终交接）

最新Handoff处理：制作人已明确规划验收通过并授权收尾发布，按固定生成规则实际落盘Handoff79；回读正文/diff，保留准确生成时Git快照，后续追加实际Commit / Push核验证据。

制作人已授权仅精确暂存、提交与推送下列6份Markdown；提交名称：`docs(village): plan V17-R3 person detail and management`。预计任务文件数6；禁止包含22项保护路径、Runtime/Scene/Prefab/XLSX/Generated/Save、测试/临时产物。Issues84/85/86/88不关闭；仅实际Push成功并核验后才标R3-P Work Package Closed。共享工作区保留任务外变化，不标Branch / Repository Closed。

### 规划初稿落盘核验（验收前历史事实）

已回读6份最终Markdown及Bootstrap入口：5份既有文件仅增首页状态/日志说明，新增本规格；Handoff78标记区与HEAD逐字一致。`git diff --check`通过，新规格无行尾空白；6份文件内241条相对文件链接目标存在。Git工作区新增变化严格等于本包6份Markdown，全部22项任务外文件SHA256仍匹配本轮基线；HEAD保持ef4da17，index为空。未修改或运行Runtime/Scene/Prefab/XLSX/Generated/Save/测试/Builder，未提交或Push。

实际存档目录未从本轮Editor获得，不能声称本轮存档逐字节PASS；本轮不启动Editor和游戏、不调用存档API，保持存档操作为零。测试矩阵仅是未来A/B验收要求。本次客观文档检查通过，**当时制作人R3-P规划验收尚未取得**；随后验收与收尾授权见下节。

## 11. 制作人规划验收与Final Closeout（2026-10-09）

制作人明确：“V17-R3-P 制作人规划验收通过，允许收尾发布。”开发17.0已检查完整Development Spec、Authority对照、R3-A/B范围、旧UI迁移、测试矩阵及保护记录；确认采用现有规划，不新增第二套Person / Growth / Assignment / Social / History / Save Authority。授权精确6份Markdown的必要状态同步、Commit + Push main与最终Handoff。

**交接生成时：R3-P PLANNING ACCEPTED / Final Closeout Prepared，Commit / Push当时尚待执行。实际PUBLISHED证据见§12。** R3-A/B Runtime均未实施；R3-A是发布成功后的下一阶段且无需重复授权，本次任务只收尾，不自行实施A。R3-B未获实施授权。

成长待处理选牌关闭/保存风险、撤职后的劳动意图风险、关系枚举/存在性与历史人物查询缺口继续按§7处理，没有被规划验收关闭。Issues84/85/86/88不关闭，SaveVersion/DTO/核心公共接口及既有Authority不变。

Final Closeout仅6份Markdown；无仓库内一次性测试/Runner/Builder产物需清理。聊天work中的审计脚本/哈希/备份留在仓库外，不入提交。当前22/22保护文件SHA256与本次规划基线一致，未启动Editor或调用存档API；实际存档哈希仍未验证，不冒称PASS。Markdown内容/引用/diff在暂存前重新核对。

Closure Commit Staging Manifest：Must Stage为§10精确6路径；Must Not Stage为22项保护文件及所有Runtime/Scene/Prefab/资源/XLSX/Generated/Save/临时产物。Expected staged file count=6。Target / current=main；生成时local HEAD / origin/main / 实时GitHub main均ef4da17457c7db5f1fd2502212ebf1f842e33d59，index为空。实际发布SHA和三方核验仅在成功后追加，不预填未来SHA。

## 12. 实际发布核验（2026-10-09）

**R3-P PLANNING ACCEPTED / PUBLISHED / Work Package Closed（仅规划包）。** Closure Commit `db2c7ee6de61bc44f7ed23126de98c01c6bf3307`（`docs(village): plan V17-R3 person detail and management`）已正常Push main，基线ef4da17 → db2c7ee；无force、无历史改写。发布时local HEAD / origin/main / 实时GitHub main均为该完整SHA，git ls-remote与GitHub connector双入口确认。随后仅以同一六份Markdown同步本实际发布状态；同步提交自身SHA以最终实时Git及制作人交付报告为准，不预填自身SHA。

精确暂存及实际提交均6路径，与§10清单逐项一致，包含新增规格和Handoff79；任务外22项未暂存。最终内容已回读，243条相对文件链接目标存在，git diff --check / cached --check通过。提交后index为空，工作区仅原22项（5 tracked dirty +17 untracked），22/22 SHA256匹配规划基线。无仓库内临时产物、新Runtime/测试/资源/配置/Save变化，不宣称Branch Closed / Repository Closed。

规划验收没有关闭成长选牌关闭/重开/切人及Save反馈风险、撤职劳动意图风险、关系枚举/存在性和历史人物查询缺口；Issues84/85/86/88仍开放。Unity / Compiler / Builder / EditMode / PlayMode / Save / Load本轮均未运行；实际存档路径与哈希未验证。

R3-A Person Detail为下一阶段，已有制作人连续推进授权，无需重复等待授权；本轮仅完成R3-P收尾，R3-A Not Started / Not Implemented，R3-B Not Implemented / Not Authorized。下一阶段按已验收规格实施最小五页签与既有Growth接缝，建立实际Editor/最新存档保护，遵守§7 Stop Conditions，不另建Person / Growth / Assignment / Social / History / Save Authority。
