using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D1 Phase B（§29 契约矩阵的 Village 部分）：**Unified Event 产品契约**。
    ///
    /// 保护点（真实产品入口，不经测试捷径）：
    ///   - Recognition（SocialFactLedger 年内计数）→ EventResolver → `EventRuntimeBridge.PendingVillageEvent`；
    ///   - **正式路径不再创建 Legacy Pending / fired**（§40）；
    ///   - 选项文案 / 可用性 / 结果全部来自 Event 静态配置（经唯一模板渲染器）；
    ///   - Option → Outcome → Effect Transaction（GrantRest 真实写 Rest Authority；无效果选项真实结算）；
    ///   - exactly-once；Preview == 正式结果；
    ///   - 旧档 Legacy Pending 收敛迁移（已知映射重建、**不重判条件**；未知 Pattern 保留 Legacy 路径）；
    ///   - Save / Load：Pending 身份 / 绑定 / Context 不变；结算后 Pending 不回来、History 与 Rest 保留。
    ///
    /// 边界：`VillageEventFlow` / `VillageRuntimeBridge` / `EventRuntimeBridge` / `SaveSystem` 位于
    /// Assembly-CSharp（产品程序集），因此本测试程序集按仓库既有模式用 `Type.GetType(...)` + 反射调用，
    /// 断言则尽量使用 Event.Runtime / Village.Runtime 的强类型。
    /// </summary>
    public class VillageEventFlowContractTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags IF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private const string EventId = "evt_test_village_temp_draft";
        private const string RoleId = "role_test_village_requester";
        private const string OptionGrantRest = "opt_test_village_grant_rest";
        private const string OptionContinue = "opt_test_village_continue";
        private const string OutcomeGrantRest = "out_test_village_grant_rest";
        private const string OutcomeContinue = "out_test_village_continue";
        private const string PatternKey = "TemporaryDraftBuildup";

        private static Type BridgeType { get { return Type.GetType("VillageRuntimeBridge, Assembly-CSharp"); } }
        private static Type FlowType { get { return Type.GetType("VillageEventFlow, Assembly-CSharp"); } }
        private static Type EventBridgeType { get { return Type.GetType("EventRuntimeBridge, Assembly-CSharp"); } }
        private static Type SaveSystemType { get { return Type.GetType("SaveSystem, Assembly-CSharp"); } }
        private static Type CollectionType { get { return Type.GetType("PlayerCollection, Assembly-CSharp"); } }

        private object _collection;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(BridgeType, "找不到 Assembly-CSharp 中的 VillageRuntimeBridge");
            Assert.IsNotNull(FlowType, "找不到 Assembly-CSharp 中的 VillageEventFlow");
            Assert.IsNotNull(EventBridgeType, "找不到 Assembly-CSharp 中的 EventRuntimeBridge");
            Assert.IsNotNull(SaveSystemType, "找不到 Assembly-CSharp 中的 SaveSystem");
            Assert.IsNotNull(CollectionType, "找不到 Assembly-CSharp 中的 PlayerCollection");

            ResetWorld();
            DeleteSave();
        }

        [TearDown]
        public void TearDown()
        {
            ResetWorld();
            DeleteSave();
        }

        // ==================== helpers ====================

        private static void ResetWorld()
        {
            InvokeStatic(BridgeType, "ResetForTests");
            InvokeStatic(EventBridgeType, "ResetForTests");
            InvokeStatic(FlowType, "ResetSessionState");
        }

        private static void DeleteSave()
        {
            string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
        }

        /// <summary>初始化真实 Village 世界（默认人物集合 + 真相源绑定；不注入任何测试替身）。</summary>
        private void InitializeWorld()
        {
            InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
            InvokeStatic(EventBridgeType, "EnsureInitialized");
            _collection = Activator.CreateInstance(CollectionType);
            InvokeInstance(_collection, "InitializeDefaultCollection");

            var sourceField = BridgeType.GetField("SimulationPersonSource", F);
            Assert.IsNotNull(sourceField, "VillageRuntimeBridge.SimulationPersonSource 字段不存在");
            sourceField.SetValue(null, _collection);
        }

        /// <summary>真实调度：启动一个 Test Project 并推进到完成（释放参与者）。</summary>
        private static void StartAndComplete(string defId, string personId)
        {
            Assert.AreEqual(VillageProjectResult.Success,
                (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", defId, new[] { personId }),
                "加入新 Project 应成功：" + defId);
            TickDays(defId == "prj_test_02" ? 3 : 10);
        }

        private static void TickDays(int days)
        {
            object time = GetBridgeStatic("Time");
            for (int i = 0; i < days; i++) InvokeInstance(time, "TickDay");
        }

        private static object GetBridgeStatic(string name)
        {
            var field = BridgeType.GetField(name, F);
            Assert.IsNotNull(field, "字段不存在：" + name);
            return field.GetValue(null);
        }

        private static object PersonField(int index, string fieldName)
        {
            object collection = GetBridgeStatic("SimulationPersonSource");
            Assert.IsNotNull(collection, "前置：必须已绑定 SimulationPersonSource");

            var list = (IList)collection.GetType().GetField("OwnedCharacters").GetValue(collection);
            Assert.IsNotNull(list);
            Assert.Greater(list.Count, index, "默认人物集合未包含足量人物");

            object person = list[index];
            var personField = person.GetType().GetField(fieldName);
            Assert.IsNotNull(personField, person.GetType().FullName + " 没有字段 " + fieldName);
            return personField.GetValue(person);
        }

        private static string PersonIdAt(int index)
        {
            return (string)PersonField(index, "PersonId");
        }

        private static string DisplayNameAt(int index)
        {
            return (string)PersonField(index, "DisplayName");
        }

        private static ResolvedEventInstance PendingInstance()
        {
            var property = EventBridgeType.GetProperty("PendingVillageEvent", F);
            Assert.IsNotNull(property, "EventRuntimeBridge.PendingVillageEvent 属性不存在");
            return (ResolvedEventInstance)property.GetValue(null);
        }

        private static EventWorldState WorldState()
        {
            return (EventWorldState)EventBridgeType.GetProperty("World", F).GetValue(null);
        }

        private static EventExecutionState ExecutionState()
        {
            return (EventExecutionState)EventBridgeType.GetProperty("Execution", F).GetValue(null);
        }

        private static VillageSocialResponseLog LegacyLog()
        {
            return (VillageSocialResponseLog)GetBridgeStatic("SocialResponse");
        }

        private static bool IsRestBlocked(string personId)
        {
            return (bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", personId);
        }

        private static bool RaiseRecognition(string personId, int observedCount)
        {
            return (bool)InvokeStatic(FlowType, "RaiseRecognition", personId, PatternKey, observedCount, 1);
        }

        private static object InvokeStatic(Type type, string method, params object[] args)
        {
            var selected = FindMethod(type, method, args, true);
            Assert.IsNotNull(selected, "静态方法不存在或参数不匹配：" + method + " @ " + type.FullName);
            return selected.Invoke(null, args);   // out / ref 参数由 args 数组回填
        }

        private static object InvokeInstance(object target, string method, params object[] args)
        {
            var selected = FindMethod(target.GetType(), method, args, false);
            Assert.IsNotNull(selected, "实例方法不存在或参数不匹配：" + method + " @ " + target.GetType().FullName);
            return selected.Invoke(target, args);
        }

        private static MethodInfo FindMethod(Type type, string method, object[] args, bool isStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            foreach (var candidate in type.GetMethods(flags))
            {
                if (candidate.Name != method) continue;
                var ps = candidate.GetParameters();
                if (ps.Length != (args == null ? 0 : args.Length)) continue;

                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] == null) continue;
                    if (ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) continue;
                    // out / ref 参数：按元素类型比较
                    if (ps[i].ParameterType.IsByRef
                        && ps[i].ParameterType.GetElementType().IsAssignableFrom(args[i].GetType())) continue;
                    ok = false;
                    break;
                }
                if (ok) return candidate;
            }
            return null;
        }

        // ==================== V1–V4：Recognition → Event Pending ====================

        [Test]
        public void V1_Recognition_ThirdDraft_CreatesEventPending_AndNeverCreatesLegacyPending()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);

            StartAndComplete("prj_test_01", pid);
            StartAndComplete("prj_test_01", pid);
            Assert.IsNull(PendingInstance(), "未达阈值（2 次）不应建立 Event Pending");
            Assert.IsFalse(LegacyLog().HasPendingResponse, "未达阈值不应建立 Legacy Pending");

            Assert.AreEqual(VillageProjectResult.Success,
                (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid }),
                "第 3 次真实抽调应成功（post-add）");

            var pending = PendingInstance();
            Assert.IsNotNull(pending, "第 3 次成功加入后应建立 Unified Event Pending");
            Assert.AreEqual(EventId, pending.EventId);
            Assert.AreEqual("VillageRecognitionRaised", pending.TriggerType);
            Assert.AreEqual("village-recognition|" + PatternKey + "|" + pid + "|year:1", pending.TriggerKey,
                "TriggerKey 必须由真实事实稳定派生");

            string boundPerson;
            Assert.IsTrue(pending.TryGetBoundPerson(RoleId, out boundPerson), "必须绑定 ContextPerson 角色");
            Assert.AreEqual(pid, boundPerson, "Role 必须绑定触发该识别的具体 Person");

            EventContextEntry entry;
            Assert.IsTrue(pending.ContextSnapshot.TryGetEntry(EventContextEntryKind.Person, "RecognitionPerson",
                out entry), "Context 必须携带 RecognitionPerson");
            Assert.AreEqual(pid, entry.StringValue);
            Assert.IsTrue(pending.ContextSnapshot.TryGetEntry(EventContextEntryKind.String, "RecognitionType",
                out entry));
            Assert.AreEqual(PatternKey, entry.StringValue);
            Assert.IsTrue(pending.ContextSnapshot.TryGetEntry(EventContextEntryKind.Int, "ObservedCount", out entry));
            Assert.AreEqual(3, entry.IntValue);

            // §40：正式路径绝不再写 Legacy（pending / fired 都不得出现）
            Assert.IsFalse(LegacyLog().HasPendingResponse, "新产品路径不得创建 Legacy Pending");
            Assert.AreEqual(0, LegacyLog().Fired.Count, "新产品路径不得写 Legacy fired");
            Assert.AreEqual(0, LegacyLog().Choices.Count, "未做选择前不得写 Legacy ChoiceHistory");
        }

        [Test]
        public void V2_Recognition_BeforeThreshold_ProducesNoEvent()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);

            StartAndComplete("prj_test_01", pid);

            Assert.IsNull(PendingInstance(), "第 1 次抽调不应产生事件");
            Assert.IsFalse(LegacyLog().HasPendingResponse);
            Assert.AreEqual("NoCandidateEvent", (string)FlowType.GetProperty("LastResolutionCode").GetValue(null),
                "「没有候选事件」是正常 Gameplay 结果（条件 ObservedCount == 3 未成立）");
        }

        [Test]
        public void V3_Recognition_SameFact_IsDeterministic()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);

            var first = (EventResolutionResult)InvokeStatic(FlowType, "ResolveRecognition", pid, PatternKey, 3, 1);
            var second = (EventResolutionResult)InvokeStatic(FlowType, "ResolveRecognition", pid, PatternKey, 3, 1);

            Assert.IsTrue(first.Success, first.Diagnostic);
            Assert.IsTrue(second.Success, second.Diagnostic);
            Assert.AreEqual(first.Instance.EventInstanceId, second.Instance.EventInstanceId,
                "同一事实重复求解必须得到同一 EventInstanceId");
            Assert.AreEqual(first.Instance.ResolutionSeed, second.Instance.ResolutionSeed);
            Assert.IsNull(PendingInstance(), "纯求解绝不写 Pending");
        }

        [Test]
        public void V4_Recognition_WhilePendingExists_IsRejectedWithoutOverwrite()
        {
            InitializeWorld();
            string first = PersonIdAt(0);
            string second = PersonIdAt(1);

            Assert.IsTrue(RaiseRecognition(first, 3), "第一次识别应建立 Pending");
            var pendingBefore = PendingInstance();

            Assert.IsFalse(RaiseRecognition(second, 3), "已有 Pending 时不得建立第二条");
            Assert.AreSame(pendingBefore, PendingInstance(), "已存在的 Pending 不得被覆盖");
            Assert.IsTrue(VillageEventFlowDiagnostic().Contains("PendingExists"),
                "诊断必须明确说明 PendingExists：" + VillageEventFlowDiagnostic());
        }

        private static string VillageEventFlowDiagnostic()
        {
            var property = FlowType.GetProperty("LastDiagnostic");
            Assert.IsNotNull(property, "VillageEventFlow.LastDiagnostic 属性不存在");
            return (string)property.GetValue(null) ?? string.Empty;
        }

        private sealed class OptionRow
        {
            public string OptionId;
            public string Label;
            public string SubLabel;
            public string Availability;
            public bool IsSelectable;
        }

        private static List<OptionRow> OptionRows(IList views)
        {
            var rows = new List<OptionRow>();
            if (views == null) return rows;

            foreach (var view in views)
            {
                var type = view.GetType();
                rows.Add(new OptionRow
                {
                    OptionId = (string)type.GetField("OptionId").GetValue(view),
                    Label = (string)type.GetField("Label").GetValue(view),
                    SubLabel = (string)type.GetField("SubLabel").GetValue(view),
                    Availability = type.GetField("Availability").GetValue(view).ToString(),
                    IsSelectable = (bool)type.GetField("IsSelectable").GetValue(view)
                });
            }
            return rows;
        }

        // ==================== V5–V6：Option → Outcome → Effect Transaction ====================

        [Test]
        public void V5_Option_GrantRest_ExecutesTransaction_History_Rest_AndClearsPending()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            Assert.IsTrue(RaiseRecognition(pid, 3), VillageEventFlowDiagnostic());
            var instance = PendingInstance();
            Assert.IsFalse(IsRestBlocked(pid), "前置：该人物当前不在恢复窗口");

            object[] args = { OptionGrantRest, null };
            bool ok = (bool)InvokeStatic(FlowType, "TryExecutePendingOption", args);
            var result = (EventOptionExecutionResult)args[1];

            Assert.IsTrue(ok, result != null ? result.Diagnostic : VillageEventFlowDiagnostic());
            Assert.AreEqual(EventExecutionCode.Success, result.Code);
            Assert.AreEqual(OutcomeGrantRest, result.OutcomeId, "单一 Outcome 必须确定选中");
            Assert.AreEqual(1, result.AppliedEffectCount, "GrantPersonRest 是唯一效果");

            Assert.IsNull(PendingInstance(), "成功结算后 Pending 必须清除");
            Assert.IsTrue(IsRestBlocked(pid), "GrantPersonRest 必须真实写入正式 Rest Authority");
            Assert.AreEqual(0, LegacyLog().Rests.Count, "新路径不得写 Legacy Rests");

            Assert.AreEqual(1, WorldState().History.OccurrenceCount(EventId), "必须记录 World Event History");
            Assert.AreEqual(instance.EventInstanceId, WorldState().History.Records[0].LastEventInstanceId,
                "History 必须指向本次事件实例");
            Assert.IsTrue(ExecutionState().IsCompleted(instance.EventInstanceId), "必须标记实例已完成");
        }

        [Test]
        public void V6_Option_Continue_ExecutesNoEffect_AndRecordsHistory()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            Assert.IsTrue(RaiseRecognition(pid, 3), VillageEventFlowDiagnostic());
            var instance = PendingInstance();

            object[] args = { OptionContinue, null };
            Assert.IsTrue((bool)InvokeStatic(FlowType, "TryExecutePendingOption", args),
                VillageEventFlowDiagnostic());
            var result = (EventOptionExecutionResult)args[1];

            Assert.AreEqual(OutcomeContinue, result.OutcomeId);
            Assert.AreEqual(0, result.AppliedEffectCount, "该选项未配置效果（合法的 no-effect Continue）");

            Assert.IsNull(PendingInstance());
            Assert.IsFalse(IsRestBlocked(pid), "no-effect 选项不得产生 Rest");
            Assert.AreEqual(0, LegacyLog().Rests.Count, "Legacy 侧不得被写入");
            Assert.AreEqual(0, LegacyLog().Choices.Count, "Legacy 侧不得被写入");
            Assert.AreEqual(1, WorldState().History.OccurrenceCount(EventId));
            Assert.IsTrue(ExecutionState().IsCompleted(instance.EventInstanceId));
        }

        [Test]
        public void V7_Execution_IsExactlyOnce()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            Assert.IsTrue(RaiseRecognition(pid, 3), VillageEventFlowDiagnostic());
            var instance = PendingInstance();

            object[] first = { instance, OptionContinue, null };
            Assert.IsTrue((bool)InvokeStatic(FlowType, "TryExecuteOption", first), VillageEventFlowDiagnostic());
            int historyAfterFirst = WorldState().History.OccurrenceCount(EventId);

            object[] second = { instance, OptionGrantRest, null };
            bool ok = (bool)InvokeStatic(FlowType, "TryExecuteOption", second);
            var result = (EventOptionExecutionResult)second[2];

            Assert.IsFalse(ok, "同一实例第二次执行必须被拒绝");
            Assert.AreEqual(EventExecutionCode.InstanceAlreadyResolved, result.Code);
            Assert.AreEqual(historyAfterFirst, WorldState().History.OccurrenceCount(EventId), "History 不得重复计数");
            Assert.IsFalse(IsRestBlocked(pid), "被拒绝的执行不得产生任何 mutation");
        }

        [Test]
        public void V8_PreviewOutcome_MatchesExecutedOutcome_AndDoesNotMutate()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            Assert.IsTrue(RaiseRecognition(pid, 3), VillageEventFlowDiagnostic());
            var instance = PendingInstance();

            var previewGrant = (EventOutcomeDefinition)InvokeStatic(FlowType, "PreviewOutcome", instance,
                OptionGrantRest);
            var previewContinue = (EventOutcomeDefinition)InvokeStatic(FlowType, "PreviewOutcome", instance,
                OptionContinue);

            Assert.IsNotNull(previewGrant);
            Assert.IsNotNull(previewContinue);
            Assert.AreEqual(OutcomeGrantRest, previewGrant.OutcomeId);
            Assert.AreEqual(OutcomeContinue, previewContinue.OutcomeId);

            // 预览 = 纯查询（不清 Pending、不写 History、不写 Rest）
            Assert.IsNotNull(PendingInstance(), "预览不得清除 Pending");
            Assert.AreEqual(0, WorldState().History.OccurrenceCount(EventId), "预览不得写 History");
            Assert.IsFalse(IsRestBlocked(pid), "预览不得写 Rest");

            object[] args = { OptionContinue, null };
            Assert.IsTrue((bool)InvokeStatic(FlowType, "TryExecutePendingOption", args),
                VillageEventFlowDiagnostic());
            var result = (EventOptionExecutionResult)args[1];
            Assert.AreEqual(previewContinue.OutcomeId, result.OutcomeId, "预览结果必须等于正式结果");
        }

        [Test]
        public void V9_OptionAvailability_UnknownBoundPerson_HidesConditionalOption()
        {
            InitializeWorld();

            var dto = new PendingEventInstanceSaveData
            {
                EventInstanceId = "evtinst_test_ghost",
                EventId = EventId,
                TriggerType = "VillageRecognitionRaised",
                TriggerKey = "village-recognition|" + PatternKey + "|person_ghost|year:1",
                ResolutionSeed = 12345,
                RoleBindings = new List<RoleBindingSaveData>
                {
                    new RoleBindingSaveData
                    {
                        RoleId = RoleId,
                        RoleName = "当事人",
                        SelectorType = "ContextPerson",
                        PersonId = "person_ghost",
                        SourceKey = "RecognitionPerson"
                    }
                },
                ContextSnapshot = new List<ContextEntrySaveData>
                {
                    new ContextEntrySaveData
                    {
                        Kind = (int)EventContextEntryKind.Person,
                        Key = "RecognitionPerson",
                        StringValue = "person_ghost"
                    },
                    new ContextEntrySaveData
                    {
                        Kind = (int)EventContextEntryKind.String,
                        Key = "RecognitionType",
                        StringValue = PatternKey
                    },
                    new ContextEntrySaveData
                    {
                        Kind = (int)EventContextEntryKind.Int,
                        Key = "ObservedCount",
                        IntValue = 3
                    }
                }
            };

            var ghost = EventWorldPersistence.FromSaveData(dto);
            Assert.IsNotNull(ghost, "完整 DTO 必须能还原实例");

            object[] buildArgs = { ghost, null };
            var views = (IList)InvokeStatic(FlowType, "BuildOptionViews", buildArgs);
            var rows = OptionRows(views);

            Assert.AreEqual(1, rows.Count, "PersonExists 不成立 + UnavailableMode=Hide → 条件选项必须整条隐藏");
            Assert.AreEqual(OptionContinue, rows[0].OptionId);

            // 防伪：绕过 UI 直接执行被隐藏的选项也必须明确拒绝
            object[] execArgs = { ghost, OptionGrantRest, null };
            bool ok = (bool)InvokeStatic(FlowType, "TryExecuteOption", execArgs);
            var result = (EventOptionExecutionResult)execArgs[2];

            Assert.IsFalse(ok, "被 Hide 的选项不得被静默执行");
            Assert.AreEqual(EventExecutionCode.OptionHidden, result.Code);
            Assert.AreEqual(0, WorldState().History.OccurrenceCount(EventId), "被拒绝的执行不得写 History");
        }

        // ==================== V10–V11：旧档 Legacy Pending 收敛迁移 ====================

        /// <summary>构造「旧档」：只含 Legacy SocialResponse 区块（`fired` + 单条 pending）。</summary>
        private static VillageSaveData LegacyVillageSave(string patternKey, string personId)
        {
            return new VillageSaveData
            {
                SocialResponse = new VillageSocialResponseSaveData
                {
                    Fired = new List<VillageSocialFiredSaveData>
                    {
                        new VillageSocialFiredSaveData { PatternKey = patternKey, PersonId = personId, Year = 1 }
                    },
                    Pending = new VillagePendingResponseSaveData
                    {
                        EventId = patternKey + "_1_1",
                        PatternKey = patternKey,
                        PersonId = personId,
                        Year = 1
                    }
                }
            };
        }

        private void SaveLegacyAndReload(VillageSaveData village)
        {
            Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "SaveWithVillage", _collection, village),
                "旧档 Save 应成功");

            ResetWorld();
            Assert.IsNotNull(InvokeStatic(SaveSystemType, "Load"), "旧档必须可加载");
        }

        [Test]
        public void V10_LegacyMigration_KnownPattern_RebuildsPending_WithoutConditionReEvaluation()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);

            SaveLegacyAndReload(LegacyVillageSave(PatternKey, pid));

            var migrated = PendingInstance();
            Assert.IsNotNull(migrated, "已知 Pattern 的 Legacy Pending 必须收敛为 Unified Event Pending");
            Assert.AreEqual(EventId, migrated.EventId);
            Assert.AreEqual("evtinst_legacy|" + EventId + "|" + PatternKey + "_1_1", migrated.EventInstanceId,
                "迁移实例身份必须由旧档既成事实稳定派生");

            string bound;
            Assert.IsTrue(migrated.TryGetBoundPerson(RoleId, out bound), "迁移必须重建 Role 绑定");
            Assert.AreEqual(pid, bound);

            EventContextEntry entry;
            Assert.IsTrue(migrated.ContextSnapshot.TryGetEntry(EventContextEntryKind.Person, "RecognitionPerson",
                out entry));
            Assert.AreEqual(pid, entry.StringValue);
            Assert.IsTrue(migrated.ContextSnapshot.TryGetEntry(EventContextEntryKind.Int, "ObservedCount", out entry));

            Assert.IsFalse(LegacyLog().HasPendingResponse, "收敛后旧 Pending 不得再次出现");
            Assert.AreEqual(1, LegacyLog().Fired.Count, "旧 fired 记录保持不动（不重写历史）");

            // 关键：账本年内计数为 0（远低于条件阈值）也照样迁移 —— 旧 Pending 是既成事实，不重跑条件判定。
            Assert.AreEqual(0,
                (int)InvokeStatic(BridgeType, "CountTemporaryCommitmentsInYear", pid, 1),
                "前置：账本没有任何既定事实（迁移并非由条件重判驱动）");
        }

        [Test]
        public void V11_LegacyMigration_UnknownPattern_KeepsLegacyPending()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            const string unknownPattern = "LegacyPatternNotInEvents";

            SaveLegacyAndReload(LegacyVillageSave(unknownPattern, pid));

            Assert.IsNull(PendingInstance(), "未知 Pattern 不得凭空映射到某个 Event");
            Assert.IsTrue(LegacyLog().HasPendingResponse, "未知 Legacy Pending 必须保留（不静默删除）");
            Assert.IsTrue(VillageEventFlowDiagnostic().Contains("Unknown"),
                "诊断必须明确说明是未知 Pattern：" + VillageEventFlowDiagnostic());
        }

        // ==================== V12–V13：Save / Load ====================

        [Test]
        public void V12_SaveLoad_PendingIdentityBindingAndContextPreserved()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            Assert.IsTrue(RaiseRecognition(pid, 3), VillageEventFlowDiagnostic());

            var before = PendingInstance();
            var previewBefore = (EventOutcomeDefinition)InvokeStatic(FlowType, "PreviewOutcome", before,
                OptionContinue);

            Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", _collection), "Save 应成功");
            ResetWorld();
            Assert.IsNotNull(InvokeStatic(SaveSystemType, "Load"), "应可加载刚保存的存档");

            var after = PendingInstance();
            Assert.IsNotNull(after, "未结算的 Event Pending 必须随存档恢复");
            Assert.AreEqual(before.EventInstanceId, after.EventInstanceId);
            Assert.AreEqual(before.EventId, after.EventId);
            Assert.AreEqual(before.TriggerType, after.TriggerType);
            Assert.AreEqual(before.TriggerKey, after.TriggerKey);
            Assert.AreEqual(before.ResolutionSeed, after.ResolutionSeed);

            string beforePerson;
            string afterPerson;
            before.TryGetBoundPerson(RoleId, out beforePerson);
            after.TryGetBoundPerson(RoleId, out afterPerson);
            Assert.AreEqual(beforePerson, afterPerson, "Role 绑定必须保持");

            EventContextEntry entry;
            Assert.IsTrue(after.ContextSnapshot.TryGetEntry(EventContextEntryKind.Int, "ObservedCount", out entry));
            Assert.AreEqual(3, entry.IntValue, "Context 必须保持");

            var previewAfter = (EventOutcomeDefinition)InvokeStatic(FlowType, "PreviewOutcome", after, OptionContinue);
            Assert.AreEqual(previewBefore.OutcomeId, previewAfter.OutcomeId, "Load 之后 Outcome 不得被重抽");
        }

        [Test]
        public void V13_SaveLoad_AfterExecution_PendingDoesNotReturn_HistoryAndRestSurvive()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            Assert.IsTrue(RaiseRecognition(pid, 3), VillageEventFlowDiagnostic());

            object[] args = { OptionGrantRest, null };
            Assert.IsTrue((bool)InvokeStatic(FlowType, "TryExecutePendingOption", args),
                VillageEventFlowDiagnostic());

            Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", _collection), "Save 应成功");
            ResetWorld();
            Assert.IsNotNull(InvokeStatic(SaveSystemType, "Load"), "应可加载刚保存的存档");

            Assert.IsNull(PendingInstance(), "事件完成之后 Load 不得让 Pending 回来");
            Assert.AreEqual(1, WorldState().History.OccurrenceCount(EventId), "World Event History 必须保留");
            Assert.IsTrue(IsRestBlocked(pid), "结果的长期后果（Rest）必须保留");
        }

        // ==================== V14–V15：表现来自配置 / 缺实例 Fail Closed ====================

        [Test]
        public void V14_PresentationAndOptions_ComeFromEventConfig()
        {
            InitializeWorld();
            string pid = PersonIdAt(0);
            string displayName = DisplayNameAt(0);
            Assert.IsTrue(RaiseRecognition(pid, 3), VillageEventFlowDiagnostic());
            var instance = PendingInstance();

            object[] textArgs = { instance, null, null, null };
            Assert.IsTrue((bool)InvokeStatic(FlowType, "TryRenderPendingTexts", textArgs), (string)textArgs[3]);

            string title = (string)textArgs[1];
            string body = (string)textArgs[2];

            Assert.AreEqual("村庄里有人私下提起最近的安排", title, "标题必须来自 EventPresentation 配置");
            Assert.AreEqual(displayName + " 已经连续多次承担临时任务，想谈谈接下来的节奏。", body,
                "正文必须由配置模板 + 真实人物显示名渲染");
            Assert.IsFalse(body.Contains("{"), "不得显示原始占位符");

            object[] buildArgs = { instance, null };
            var rows = OptionRows((IList)InvokeStatic(FlowType, "BuildOptionViews", buildArgs));

            Assert.AreEqual(2, rows.Count, "选项数量必须来自配置（不是硬编码两个产品选项）");
            Assert.AreEqual(OptionGrantRest, rows[0].OptionId, "顺序必须按配置 SortOrder");
            Assert.AreEqual("先给他一段恢复时间", rows[0].Label);
            Assert.IsTrue(rows[0].IsSelectable);
            Assert.AreEqual(OptionContinue, rows[1].OptionId);
            Assert.AreEqual("继续让他承担", rows[1].Label);
            Assert.IsTrue(rows[1].IsSelectable);
        }

        [Test]
        public void V15_ExecuteOption_WithoutInstance_FailsClosed_WithoutMutation()
        {
            InitializeWorld();

            object[] args = { null, OptionGrantRest, null };
            bool ok = (bool)InvokeStatic(FlowType, "TryExecuteOption", args);

            Assert.IsFalse(ok, "没有实例时必须 Fail Closed（不得凭空执行）");
            Assert.IsNull(args[2], "没有实例时不得伪造执行结果");
            Assert.IsTrue(VillageEventFlowDiagnostic().Contains("没有已解析的事件实例"),
                "必须给出可操作诊断：" + VillageEventFlowDiagnostic());
            Assert.AreEqual(0, WorldState().History.OccurrenceCount(EventId), "不得写 History");
        }
    }
}
