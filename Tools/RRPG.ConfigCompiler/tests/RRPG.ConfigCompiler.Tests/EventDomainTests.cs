using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// WP-Event-1A：Event 领域（统一事件内容基础 / Static Content Foundation）。
    ///
    /// 断言的是**领域契约**而不是实现细节：
    ///   - 事件工作簿被现有发现 / 解析 / Schema / Data / Cross-ref 流程自动接入（不新增第二条链）；
    ///   - 十张表各自产出确定性的 Canonical Mirror（同输入同字节、重复 build = unchanged）；
    ///   - Enabled 结构完整性 / Repeat + Cooldown / Domain 约束 / Role 形状 / 条件与效果形状 /
    ///     顺序规则 / 条件性引用（Role、EncounterPool、VisualAsset）/ 模板最小结构
    ///     违规一律 = Hard Error + 定位（Workbook / Sheet / Row / Column）+ **0 更新**；
    ///   - 迁移后 VisualAsset 仍是**一份**共享 Authority，且 EnemyPresentation 可以跨 Workbook 引用它。
    ///
    /// 边界（1A 不做，因此本类也不测）：事件求解、角色绑定、条件求值、Effect 执行、模板渲染。
    /// </summary>
    public class EventDomainTests
    {
        private static readonly string[] EventMirrorFiles =
        {
            "Event.json", "EventTrigger.json", "EventRole.json", "EventRoleCondition.json",
            "EventCondition.json", "EventPresentation.json", "EventOption.json",
            "EventOptionCondition.json", "EventOutcome.json", "EventEffect.json"
        };

        // ---------------- 语料装配 ----------------

        /// <summary>
        /// 写入 1A 所需的四本工作簿：
        ///   RRPG_Monsters.xlsx（Monster + Encounter 子表）/ RRPG_Encounters.xlsx（5 张表）/
        ///   RRPG_VisualAssets.xlsx（共享 VisualAsset）/ RRPG_Events.xlsx（本类要测的事件语料）。
        /// </summary>
        private static void WriteAll(TestRoot root, params TestSheet[] eventSheets)
        {
            root.WriteSourceAsset("Enemy/test.png", "test-source-bytes");

            // Monster 工作簿：只放 Monster 表（Encounter 子表在 RRPG_Encounters 里由 EncounterDomain 提供，
            // 同一实体名不得跨 Workbook 重复）。
            root.WriteWorkbook("RRPG_Monsters.xlsx", Fixtures.MinimalMonsterSheet());

            root.WriteWorkbook("RRPG_Encounters.xlsx", Fixtures.EncounterDomain());

            root.WriteWorkbook("RRPG_VisualAssets.xlsx",
                Fixtures.VisualAssetSheet("va_test", "Enemy/test.png"));

            root.WriteWorkbook("RRPG_Events.xlsx",
                eventSheets == null || eventSheets.Length == 0 ? EventDomain() : eventSheets);
        }

        private static CompileResult Build(TestRoot root)
        {
            return CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
        }

        private static string DiagText(CompileResult result)
        {
            return string.Join(" | ", result.Diagnostics.Select(d => d.ToString()));
        }

        private static void AssertHardError(CompileResult result, string code)
        {
            Assert.False(result.Success, "本用例必须是 Hard Error，但 build 成功了");
            Assert.Contains(result.Diagnostics, d => d.Code == code);
        }

        private static void AssertHasErrorWithLocation(CompileResult result, string code, string sheet)
        {
            AssertHardError(result, code);

            var hit = result.Diagnostics.First(d => d.Code == code);
            Assert.Equal(sheet, hit.Sheet);
            Assert.False(string.IsNullOrEmpty(hit.Workbook), "诊断必须定位 Workbook");
            Assert.True(hit.Row > 0, "诊断必须定位 Excel Row");
            Assert.False(string.IsNullOrEmpty(hit.Message), "诊断必须有可操作中文原因");
        }

        /// <summary>镜像 0 更新：Hard Error 时不得新增任何 Event Mirror 文件。</summary>
        private static void AssertNoEventMirror(TestRoot root)
        {
            foreach (string file in EventMirrorFiles)
            {
                Assert.False(File.Exists(root.GeneratedFile(file)), "Hard Error 时不得产出：" + file);
            }
        }
        // ---------------- 事件语料定义 ----------------

        private static TestSheet EventSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("Event")
                .Col("EventId", "id", "", "事件ID")
                .Col("DisplayName", "string", "", "识别名")
                .Col("Enabled", "bool", "default=false", "是否启用")
                .Col("Weight", "int", ">=1", "权重")
                .Col("RepeatPolicy", "string", "allowed=Repeatable|OncePerExpedition|OncePerWorld", "重复规则")
                .Col("CooldownType", "string", "allowed=None|VillageDays|ExpeditionNodes", "冷却类型")
                .Col("CooldownValue", "int", ">=0", "冷却值");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private static TestSheet TriggerSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventTrigger")
                .Col("TriggerId", "id", "", "触发配置ID")
                .Col("EventId", "ref:Event", "", "事件ID")
                .Col("TriggerType", "string",
                    "allowed=VillageDaySettled|VillageRecognitionRaised|VillageProjectCompleted|" +
                    "ExpeditionEventNodeReached|ExpeditionBattleResolved|ExpeditionReturned|ManualDebug",
                    "触发时机");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private static TestSheet RoleSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventRole")
                .Col("RoleId", "id", "", "角色ID")
                .Col("EventId", "ref:Event", "", "所属事件")
                .Col("RoleName", "string", "", "角色名")
                .Col("SelectorType", "string",
                    "allowed=ContextPerson|VillagePerson|ExpeditionPartyMember|FixedPerson", "选择方式")
                .Col("SourceKey", "string", "optional", "来源键")
                .Col("FixedPersonId", "string", "optional", "固定人物");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private static TestSheet RoleConditionSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventRoleCondition")
                .Col("ConditionId", "id", "", "条件ID")
                .Col("RoleId", "ref:EventRole", "", "角色ID")
                .Col("ConditionType", "string", "allowed=PersonInVillage|PersonOnExpedition|PersonAvailable", "条件类型")
                .Col("TargetKey", "string", "optional", "目标键")
                .Col("IntValue", "int", "optional", "整数参数")
                .Col("StringValue", "string", "optional", "文本参数");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private const string ConditionTypeRule =
            "allowed=ContextStringEquals|ContextIntAtLeast|ContextIntAtMost|FlagSet|FlagNotSet|" +
            "EventNotSeen|EventSeenCountAtLeast|PersonExists|PersonInVillage|PersonOnExpedition|PersonAvailable|" +
            "VillageResourceAtLeast|VillageResourceAtMost|ExpeditionDepthAtLeast|ExpeditionDepthAtMost|" +
            "SupplyAtLeast|SupplyAtMost|PartySizeAtLeast|PartySizeAtMost";

        private static TestSheet ConditionSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventCondition")
                .Col("ConditionId", "id", "", "条件ID")
                .Col("EventId", "ref:Event", "", "事件ID")
                .Col("GroupId", "int", ">=1", "条件组")
                .Col("ConditionType", "string", ConditionTypeRule, "条件类型")
                .Col("SubjectRoleId", "string", "optional", "事件角色")
                .Col("TargetKey", "string", "optional", "目标键")
                .Col("IntValue", "int", "optional", "整数参数")
                .Col("StringValue", "string", "optional", "文本参数");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private static TestSheet OptionConditionSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventOptionCondition")
                .Col("ConditionId", "id", "", "条件ID")
                .Col("OptionId", "ref:EventOption", "", "选项ID")
                .Col("GroupId", "int", ">=1", "条件组")
                .Col("ConditionType", "string", ConditionTypeRule, "条件类型")
                .Col("SubjectRoleId", "string", "optional", "事件角色")
                .Col("TargetKey", "string", "optional", "目标键")
                .Col("IntValue", "int", "optional", "整数参数")
                .Col("StringValue", "string", "optional", "文本参数");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }
        private static TestSheet PresentationSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventPresentation")
                .Col("EventId", "id", "", "事件ID")
                .Col("PresentationChannel", "string", "allowed=JourneyCard|VillageModal", "渠道")
                .Col("TitleTemplate", "string", "", "标题")
                .Col("BodyTemplate", "string", "", "正文")
                .Col("IllustrationAssetId", "string", "optional", "插图")
                .Col("Urgency", "string", "allowed=Timely|Ignorable", "紧迫性")
                .Col("PauseOnOpen", "bool", "default=false", "打开暂停");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private static TestSheet OptionSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventOption")
                .Col("OptionId", "id", "", "选项ID")
                .Col("EventId", "ref:Event", "", "事件ID")
                .Col("SortOrder", "int", ">=1", "顺序")
                .Col("LabelTemplate", "string", "", "文案")
                .Col("SubLabelTemplate", "string", "optional", "辅助说明")
                .Col("UnavailableMode", "string", "allowed=Hide|Disable", "不可用时");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private static TestSheet OutcomeSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventOutcome")
                .Col("OutcomeId", "id", "", "结果ID")
                .Col("OptionId", "ref:EventOption", "", "选项ID")
                .Col("Weight", "int", ">=1", "权重")
                .Col("ResultTextTemplate", "string", "optional", "结果文本");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        private static TestSheet EffectSheetFixture(params object[][] rows)
        {
            var sheet = new TestSheet("EventEffect")
                .Col("EffectId", "id", "", "效果ID")
                .Col("OutcomeId", "ref:EventOutcome", "", "结果ID")
                .Col("Order", "int", ">=1", "顺序")
                .Col("EffectType", "string",
                    "allowed=SetFlag|ClearFlag|VillageResourceDelta|GrantPersonRest|SupplyDelta|" +
                    "AdvanceExpeditionTime|StartBattle", "效果类型")
                .Col("TargetRoleId", "string", "optional", "目标角色")
                .Col("TargetKey", "string", "optional", "目标键")
                .Col("IntValue", "int", "optional", "整数参数")
                .Col("StringValue", "string", "optional", "文本参数");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }
        /// <summary>
        /// 合法基线语料：Village 事件（evt_test_village）+ Expedition 事件（evt_test_exp）。
        /// <paramref name="overrides"/> 按 Sheet 名替换单张表（负例用）。
        /// </summary>
        private static TestSheet[] EventDomain(params TestSheet[] overrides)
        {
            var sheets = new List<TestSheet>
            {
                EventSheetFixture(
                    new object[] { "evt_test_village", "测试·村庄", true, 100, "Repeatable", "VillageDays", 30 },
                    new object[] { "evt_test_exp", "测试·远征", true, 100, "Repeatable", "None", 0 }),

                TriggerSheetFixture(
                    new object[] { "trg_village", "evt_test_village", "VillageRecognitionRaised" },
                    new object[] { "trg_exp", "evt_test_exp", "ExpeditionEventNodeReached" }),

                RoleSheetFixture(
                    new object[] { "role_village", "evt_test_village", "当事人", "ContextPerson", "RecognitionPerson", null },
                    new object[] { "role_exp", "evt_test_exp", "侦察者", "ExpeditionPartyMember", null, null }),

                RoleConditionSheetFixture(
                    new object[] { "rc_exp", "role_exp", "PersonAvailable", null, null, null }),

                ConditionSheetFixture(
                    new object[] { "cond_village", "evt_test_village", 1, "ContextStringEquals", null, "RecognitionType", null, "TemporaryDraftBuildup" },
                    new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }),

                PresentationSheetFixture(
                    new object[] { "evt_test_village", "VillageModal", "标题", "{role_village.DisplayName} 正文", null, "Timely", true },
                    new object[] { "evt_test_exp", "JourneyCard", "标题", "{role_exp.DisplayName} 正文", null, "Ignorable", false }),

                OptionSheetFixture(
                    new object[] { "opt_v1", "evt_test_village", 1, "给恢复时间", null, "Hide" },
                    new object[] { "opt_v2", "evt_test_village", 2, "继续承担", null, "Hide" },
                    new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                    new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }),

                OptionConditionSheetFixture(
                    new object[] { "oc_v1", "opt_v1", 1, "PersonExists", "role_village", null, null, null }),

                OutcomeSheetFixture(
                    new object[] { "out_v1", "opt_v1", 100, null },
                    new object[] { "out_v2", "opt_v2", 100, null },
                    new object[] { "out_e1a", "opt_e1", 70, null },
                    new object[] { "out_e1b", "opt_e1", 30, null },
                    new object[] { "out_e2", "opt_e2", 100, null }),

                EffectSheetFixture(
                    new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null },
                    new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                    new object[] { "eff_e1b1", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                    new object[] { "eff_e1b2", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                    new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null })
                // 注意：out_v2（继续承担）刻意没有任何 Effect —— 合法的「只记录选择」。
            };

            if (overrides == null) return sheets.ToArray();

            foreach (var sheet in overrides)
            {
                int index = sheets.FindIndex(s => s.Name == sheet.Name);
                if (index >= 0) sheets[index] = sheet;
                else sheets.Add(sheet);
            }
            return sheets.ToArray();
        }
        // ---------------- 测试驱动 ----------------

        /// <summary>以给定单表覆盖建立语料、执行 build，然后把结果交给断言（TestRoot 生命周期内）。</summary>
        private static void WithDomain(Action<CompileResult, TestRoot> assert, params TestSheet[] overrides)
        {
            using (var root = new TestRoot())
            {
                WriteAll(root, EventDomain(overrides));
                var run = Build(root);
                assert(run, root);
            }
        }

        // ---------------- 1. 合法基线：十张表 → 确定性 Mirror ----------------

        [Fact]
        public void Domain_ProducesMirrorFiles_ForAllTenSheets()
        {
            WithDomain((run, root) =>
            {
                Assert.True(run.Success, DiagText(run));

                foreach (string file in EventMirrorFiles)
                {
                    Assert.True(File.Exists(root.GeneratedFile(file)), "缺 Mirror 文件：" + file);
                }

                string eventJson = root.ReadGenerated("Event.json");
                Assert.Contains("\"EventId\": \"evt_test_village\"", eventJson);
                Assert.Contains("\"RepeatPolicy\": \"Repeatable\"", eventJson);
                Assert.DoesNotContain("D:/", eventJson);   // Mirror 不得泄露机器绝对路径

                string effect = root.ReadGenerated("EventEffect.json");
                Assert.Contains("\"EffectType\": \"StartBattle\"", effect);
                Assert.Contains("\"TargetKey\": \"pool_test_general\"", effect);

                string condition = root.ReadGenerated("EventCondition.json");
                Assert.Contains("\"ConditionType\": \"ExpeditionDepthAtLeast\"", condition);
                Assert.Contains("\"IntValue\": 2", condition);

                // 没有 Effect 的 Outcome 仍必须完整进入 Mirror（只记录玩家选择是合法语义）
                Assert.Contains("\"out_v2\"", root.ReadGenerated("EventOutcome.json"));
            });
        }

        [Fact]
        public void Domain_RepeatedBuild_IsByteIdentical_AndValidatePasses()
        {
            WithDomain((first, root) =>
            {
                Assert.True(first.Success, DiagText(first));

                string path = root.GeneratedFile("Event.json");
                byte[] bytes1 = File.ReadAllBytes(path);
                DateTime mtime1 = File.GetLastWriteTimeUtc(path);

                var second = Build(root);
                Assert.True(second.Success, DiagText(second));

                Assert.Equal(bytes1, File.ReadAllBytes(path));
                Assert.Equal(mtime1, File.GetLastWriteTimeUtc(path));
                Assert.Equal("unchanged", second.Outputs.Single(o => o.Name == "Event.json").Action);

                var validate = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.True(validate.Success, DiagText(validate));
            });
        }

        [Fact]
        public void Domain_Absent_IsLegalEmptyState_AndProducesNoEventMirror()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset("Enemy/test.png");
                root.WriteWorkbook("RRPG_Monsters.xlsx", Fixtures.MinimalMonsterSheet());

                var run = Build(root);
                Assert.True(run.Success, DiagText(run));
                AssertNoEventMirror(root);
            }
        }

        [Fact]
        public void Domain_MissingSingleSheet_IsHardError()
        {
            WithDomain((run, root) =>
            {
                // 领域不完整的诊断锚定在「已存在的表」上（这里第一张是 Event），
                // 缺失的表名出现在可操作原因里。
                AssertHardError(run, DiagnosticCodes.DatEventDomainShape);
                var hit = run.Diagnostics.First(d => d.Code == DiagnosticCodes.DatEventDomainShape);
                Assert.Contains("EventEffect", hit.Message);
                AssertNoEventMirror(root);
            }, new TestSheet("EventEffect"));   // 空表：EventEffect 消失（其余九张仍在）
        }
        // ---------------- 2. Repeat / Cooldown ----------------

        [Fact]
        public void Repeat_CooldownNone_WithNonZeroValue_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRepeatCooldownShape, "Event");
                AssertNoEventMirror(root);
            }, EventSheetFixture(
                new object[] { "evt_test_village", "测试·村庄", true, 100, "Repeatable", "VillageDays", 30 },
                new object[] { "evt_test_exp", "测试·远征", true, 100, "Repeatable", "None", 5 }));
        }

        [Fact]
        public void Repeat_CooldownVillageDays_WithZeroValue_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRepeatCooldownShape, "Event");
                AssertNoEventMirror(root);
            }, EventSheetFixture(
                new object[] { "evt_test_village", "测试·村庄", true, 100, "Repeatable", "VillageDays", 0 },
                new object[] { "evt_test_exp", "测试·远征", true, 100, "Repeatable", "None", 0 }));
        }

        [Fact]
        public void Repeat_OncePerWorld_WithCooldown_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRepeatCooldownShape, "Event");
                AssertNoEventMirror(root);
            }, EventSheetFixture(
                new object[] { "evt_test_village", "测试·村庄", true, 100, "OncePerWorld", "VillageDays", 30 },
                new object[] { "evt_test_exp", "测试·远征", true, 100, "Repeatable", "None", 0 }));
        }

        [Fact]
        public void Repeat_OncePerExpedition_WithCooldown_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRepeatCooldownShape, "Event");
                AssertNoEventMirror(root);
            }, EventSheetFixture(
                new object[] { "evt_test_village", "测试·村庄", true, 100, "Repeatable", "VillageDays", 30 },
                new object[] { "evt_test_exp", "测试·远征", true, 100, "OncePerExpedition", "ExpeditionNodes", 3 }));
        }

        [Fact]
        public void Repeat_UnknownRepeatPolicy_IsHardError()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRepeatCooldownShape, "Event");
                AssertNoEventMirror(root);
            }, EventSheetFixture(
                new object[] { "evt_test_village", "测试·村庄", true, 100, "Sometimes", "VillageDays", 30 },
                new object[] { "evt_test_exp", "测试·远征", true, 100, "Repeatable", "None", 0 }));
        }

        [Fact]
        public void Repeat_ExpeditionEvent_WithVillageDaysCooldown_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRepeatCooldownShape, "Event");
                AssertNoEventMirror(root);
            }, EventSheetFixture(
                new object[] { "evt_test_village", "测试·村庄", true, 100, "Repeatable", "VillageDays", 30 },
                new object[] { "evt_test_exp", "测试·远征", true, 100, "Repeatable", "VillageDays", 7 }));
        }
        // ---------------- 3. Domain / Context ----------------

        [Fact]
        public void Domain_MixedVillageAndExpeditionTrigger_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTriggerRule, "EventTrigger");
                AssertNoEventMirror(root);
            }, TriggerSheetFixture(
                new object[] { "trg_village", "evt_test_village", "VillageRecognitionRaised" },
                new object[] { "trg_village_exp", "evt_test_village", "ExpeditionEventNodeReached" },
                new object[] { "trg_exp", "evt_test_exp", "ExpeditionEventNodeReached" }));
        }

        [Fact]
        public void Domain_UnknownTriggerType_IsHardError()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTriggerRule, "EventTrigger");
                AssertNoEventMirror(root);
            }, TriggerSheetFixture(
                new object[] { "trg_village", "evt_test_village", "VillageMoonPhaseChanged" },
                new object[] { "trg_exp", "evt_test_exp", "ExpeditionEventNodeReached" }));
        }

        [Fact]
        public void Domain_VillageEvent_WithJourneyCard_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventPresentationChannelMismatch, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "JourneyCard", "标题", "正文", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Domain_ExpeditionEvent_WithVillageModal_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventPresentationChannelMismatch, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "正文", null, "Timely", true },
                new object[] { "evt_test_exp", "VillageModal", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Domain_UnknownPresentationChannel_IsHardError()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventPresentationChannelMismatch, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "ToastMessage", "标题", "正文", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Domain_VillageEvent_WithExpeditionPartyMemberRole_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleShape, "EventRole");
                AssertNoEventMirror(root);
            }, RoleSheetFixture(
                new object[] { "role_village", "evt_test_village", "当事人", "ExpeditionPartyMember", null, null },
                new object[] { "role_exp", "evt_test_exp", "侦察者", "ExpeditionPartyMember", null, null }));
        }

        [Fact]
        public void Domain_ExpeditionEvent_WithVillagePersonRole_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleShape, "EventRole");
                AssertNoEventMirror(root);
            }, RoleSheetFixture(
                new object[] { "role_village", "evt_test_village", "当事人", "ContextPerson", "RecognitionPerson", null },
                new object[] { "role_exp", "evt_test_exp", "侦察者", "VillagePerson", null, null }));
        }
        [Fact]
        public void Domain_VillageEvent_WithSupplyDelta_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectDomainMismatch, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b1", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b2", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Domain_VillageEvent_WithStartBattle_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectDomainMismatch, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b1", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b2", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Domain_ExpeditionEvent_WithVillageResourceDelta_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectDomainMismatch, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "VillageResourceDelta", null, "Ingredients", 2, null },
                new object[] { "eff_e1b1", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b2", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Domain_ExpeditionEvent_WithGrantPersonRest_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectDomainMismatch, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "GrantPersonRest", "role_exp", null, null, null },
                new object[] { "eff_e1b1", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b2", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Domain_SetFlag_IsAllowedInBothDomains()
        {
            WithDomain((run, root) =>
            {
                Assert.True(run.Success, DiagText(run));
                Assert.Contains("\"EffectType\": \"SetFlag\"", root.ReadGenerated("EventEffect.json"));
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "SetFlag", null, "flag_test_done", null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "ClearFlag", null, "flag_test_done", null, null },
                new object[] { "eff_e1b1", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b2", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }
        // ---------------- 4. Role / Condition ----------------

        [Fact]
        public void Role_ContextPerson_WithoutSourceKey_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleShape, "EventRole");
                AssertNoEventMirror(root);
            }, RoleSheetFixture(
                new object[] { "role_village", "evt_test_village", "当事人", "ContextPerson", null, null },
                new object[] { "role_exp", "evt_test_exp", "侦察者", "ExpeditionPartyMember", null, null }));
        }

        [Fact]
        public void Role_FixedPerson_WithoutFixedPersonId_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleShape, "EventRole");
                AssertNoEventMirror(root);
            }, RoleSheetFixture(
                new object[] { "role_village", "evt_test_village", "当事人", "FixedPerson", null, null },
                new object[] { "role_exp", "evt_test_exp", "侦察者", "ExpeditionPartyMember", null, null }));
        }

        [Fact]
        public void Role_UnknownSelectorType_IsHardError()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleShape, "EventRole");
                AssertNoEventMirror(root);
            }, RoleSheetFixture(
                new object[] { "role_village", "evt_test_village", "当事人", "Anybody", null, null },
                new object[] { "role_exp", "evt_test_exp", "侦察者", "ExpeditionPartyMember", null, null }));
        }

        [Fact]
        public void Condition_RoleRefAcrossEvents_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleCrossEventRef, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_village", "evt_test_village", 1, "PersonExists", "role_exp", null, null, null },
                new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }));
        }

        [Fact]
        public void Condition_OptionCondition_RoleRefAcrossEvents_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleCrossEventRef, "EventOptionCondition");
                AssertNoEventMirror(root);
            }, OptionConditionSheetFixture(
                new object[] { "oc_v1", "opt_v1", 1, "PersonExists", "role_exp", null, null, null }));
        }

        [Fact]
        public void Condition_UnknownRoleRef_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleRefUnresolved, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_village", "evt_test_village", 1, "PersonExists", "role_missing", null, null, null },
                new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }));
        }

        [Fact]
        public void Condition_UnknownConditionType_IsHardError()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionShape, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_village", "evt_test_village", 1, "WeatherIsNice", null, null, null, null },
                new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }));
        }

        [Fact]
        public void Condition_VillageResource_TargetOtherThanIngredientsOrFood_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionShape, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_village", "evt_test_village", 1, "VillageResourceAtLeast", null, "Wood", 10, null },
                new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }));
        }

        [Fact]
        public void Condition_ContextIntAtLeast_WithoutIntValue_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionShape, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_village", "evt_test_village", 1, "ContextIntAtLeast", null, "RecognitionCount", null, null },
                new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }));
        }

        [Fact]
        public void Condition_PersonCondition_WithoutSubjectRole_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionShape, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_village", "evt_test_village", 1, "PersonExists", null, null, null, null },
                new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }));
        }
        // ---------------- 5. Enabled 结构完整性 / Disabled 草稿 ----------------

        [Fact]
        public void Structure_EnabledWithoutTrigger_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEnabledStructure, "Event");
                AssertNoEventMirror(root);
            }, TriggerSheetFixture(
                new object[] { "trg_exp", "evt_test_exp", "ExpeditionEventNodeReached" }));
        }

        [Fact]
        public void Structure_EnabledWithoutPresentation_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEnabledStructure, "Event");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Structure_TwoPresentationRows_ForSameEvent_Fails()
        {
            // 「一个事件必须且只能有一条表现」由两层保证：
            //   - EventPresentation 的身份就是 EventId → 第二行是同 id 重复（DAT005）；
            //   - EventValidator 再保证「必须有」与「不得多于一条」（DAT031，防御性第二道防线）。
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatKeyDuplicate, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题A", "正文", null, "Timely", true },
                new object[] { "evt_test_village", "VillageModal", "标题B", "正文", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Structure_EnabledWithoutOption_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEnabledStructure, "Event");
                AssertNoEventMirror(root);
            }, OptionSheetFixture(
                new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }));
        }

        [Fact]
        public void Structure_OptionWithoutOutcome_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventOptionWithoutOutcome, "EventOption");
                AssertNoEventMirror(root);
            }, OutcomeSheetFixture(
                new object[] { "out_v1", "opt_v1", 100, null },
                new object[] { "out_e1a", "opt_e1", 70, null },
                new object[] { "out_e1b", "opt_e1", 30, null },
                new object[] { "out_e2", "opt_e2", 100, null }));
            // opt_v2 没有任何 Outcome → 必须 Hard Error
        }

        [Fact]
        public void Structure_DisabledDraft_MayBeIncomplete()
        {
            // 未启用（Enabled = FALSE）的事件允许先当草稿写一半：它没有触发 / 表现 / 选项 / 结果也合法。
            WithDomain((run, root) =>
            {
                Assert.True(run.Success, DiagText(run));
                Assert.Contains("\"Enabled\": false", root.ReadGenerated("Event.json"));
            }, EventSheetFixture(
                new object[] { "evt_test_village", "测试·村庄", true, 100, "Repeatable", "VillageDays", 30 },
                new object[] { "evt_test_exp", "测试·远征（草稿）", false, 100, "Repeatable", "None", 0 }),
                TriggerSheetFixture(
                    new object[] { "trg_village", "evt_test_village", "VillageRecognitionRaised" }),
                RoleSheetFixture(
                    new object[] { "role_village", "evt_test_village", "当事人", "ContextPerson", "RecognitionPerson", null }),
                RoleConditionSheetFixture(
                    new object[] { "rc_village", "role_village", "PersonAvailable", null, null, null }),
                ConditionSheetFixture(
                    new object[] { "cond_village", "evt_test_village", 1, "ContextStringEquals", null, "RecognitionType", null, "TemporaryDraftBuildup" }),
                PresentationSheetFixture(
                    new object[] { "evt_test_village", "VillageModal", "标题", "正文", null, "Timely", true }),
                OptionSheetFixture(
                    new object[] { "opt_v1", "evt_test_village", 1, "给恢复时间", null, "Hide" },
                    new object[] { "opt_v2", "evt_test_village", 2, "继续承担", null, "Hide" }),
                OptionConditionSheetFixture(
                    new object[] { "oc_v1", "opt_v1", 1, "PersonExists", "role_village", null, null, null }),
                OutcomeSheetFixture(
                    new object[] { "out_v1", "opt_v1", 100, null },
                    new object[] { "out_v2", "opt_v2", 100, null }),
                EffectSheetFixture(
                    new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null }));
        }
        // ---------------- 6. Outcome / Effect 顺序与参数 ----------------

        [Fact]
        public void Outcome_WeightZero_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHardError(run, DiagnosticCodes.DatMinViolation);
                AssertNoEventMirror(root);
            }, OutcomeSheetFixture(
                new object[] { "out_v1", "opt_v1", 100, null },
                new object[] { "out_v2", "opt_v2", 0, null },
                new object[] { "out_e1a", "opt_e1", 70, null },
                new object[] { "out_e1b", "opt_e1", 30, null },
                new object[] { "out_e2", "opt_e2", 100, null }));
        }

        [Fact]
        public void Effect_DuplicateOrderWithinOutcome_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectOrder, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b1", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b1_dup", "out_e1b", 1, "SupplyDelta", null, null, 2, null },
                new object[] { "eff_e1b2", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Effect_StartBattleNotLast_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectOrder, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_battle", "out_e1b", 1, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e1b_time", "out_e1b", 2, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Effect_TwoStartBattleInSameOutcome_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectOrder, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_time", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b_battle1", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e1b_battle2", "out_e1b", 3, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Effect_StartBattleWithUnknownPool_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventAssetRefUnresolved, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_time", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b_battle", "out_e1b", 2, "StartBattle", null, "pool_not_exists", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }
        [Fact]
        public void Effect_UnknownEffectType_IsHardError()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectShape, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "QueueEvent", null, "evt_other", null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_time", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b_battle", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Effect_VillageResourceDelta_WithZeroValue_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectShape, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "VillageResourceDelta", null, "Ingredients", 0, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_time", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b_battle", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Effect_VillageResourceDelta_WithWoodKey_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectShape, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "VillageResourceDelta", null, "Wood", 5, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_time", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b_battle", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Effect_GrantPersonRest_WithExtraIntValue_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventEffectShape, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                // Rest 窗口必须服从当前 Village Rule；事件表不得另造第二套时间参数。
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_village", null, 30, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_time", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b_battle", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }

        [Fact]
        public void Effect_TargetRoleFromAnotherEvent_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventRoleCrossEventRef, "EventEffect");
                AssertNoEventMirror(root);
            }, EffectSheetFixture(
                new object[] { "eff_v1", "out_v1", 1, "GrantPersonRest", "role_exp", null, null, null },
                new object[] { "eff_e1a", "out_e1a", 1, "SupplyDelta", null, null, 3, null },
                new object[] { "eff_e1b_time", "out_e1b", 1, "AdvanceExpeditionTime", null, null, 1, null },
                new object[] { "eff_e1b_battle", "out_e1b", 2, "StartBattle", null, "pool_test_general", null, null },
                new object[] { "eff_e2", "out_e2", 1, "AdvanceExpeditionTime", null, null, 1, null }));
        }
        // ---------------- 7. Option / VisualAsset / 模板 ----------------

        [Fact]
        public void Option_SortOrderDuplicateWithinEvent_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventOptionSortOrder, "EventOption");
                AssertNoEventMirror(root);
            }, OptionSheetFixture(
                new object[] { "opt_v1", "evt_test_village", 1, "给恢复时间", null, "Hide" },
                new object[] { "opt_v2", "evt_test_village", 1, "继续承担", null, "Hide" },
                new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }));
        }

        [Fact]
        public void Option_UnknownUnavailableMode_IsHardError()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventOptionSortOrder, "EventOption");
                AssertNoEventMirror(root);
            }, OptionSheetFixture(
                new object[] { "opt_v1", "evt_test_village", 1, "给恢复时间", null, "Hide" },
                new object[] { "opt_v2", "evt_test_village", 2, "继续承担", null, "GreyOut" },
                new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }));
        }

        [Fact]
        public void Visual_IllustrationReferencingUnknownAsset_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventAssetRefUnresolved, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "正文", "va_does_not_exist", "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Visual_IllustrationReferencingSharedAsset_Passes()
        {
            WithDomain((run, root) =>
            {
                Assert.True(run.Success, DiagText(run));
                Assert.Contains("\"IllustrationAssetId\": \"va_test\"", root.ReadGenerated("EventPresentation.json"));
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "正文", "va_test", "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Visual_SecondVisualAssetSheet_IsDuplicateEntityHardError()
        {
            // 事件工作簿里再放一张 VisualAsset 表 = 第二套 VisualAsset Authority → 必须被拒绝。
            WithDomain((run, root) =>
            {
                // 「实体名跨 Workbook 重复」的诊断定位在实体名上（没有单一物理行）→ 只断言码与实体名。
                AssertHardError(run, DiagnosticCodes.WbDuplicateEntitySheet);
                var hit = run.Diagnostics.First(d => d.Code == DiagnosticCodes.WbDuplicateEntitySheet);
                Assert.Equal("VisualAsset", hit.Sheet);
                AssertNoEventMirror(root);
            }, Fixtures.VisualAssetSheet("va_second_authority", "Enemy/test.png"));
        }

        [Fact]
        public void Visual_EnemyPresentation_ResolvesSharedVisualAssetAcrossWorkbooks()
        {
            using (var root = new TestRoot())
            {
                WriteAll(root);
                root.WriteWorkbook("RRPG_EnemyPresentation.xlsx",
                    Fixtures.EnemyPresentationSheet(
                        new object[] { "mon_001", "va_test" },
                        new object[] { "mon_002", "va_test" }));

                var run = Build(root);
                Assert.True(run.Success, DiagText(run));

                // 跨 Workbook 的 ref:VisualAsset 仍解析到共享 Authority（只有一份）
                Assert.Contains("\"VisualAssetId\": \"va_test\"", root.ReadGenerated("EnemyPresentation.json"));
                Assert.Contains("\"rowCount\": 1", root.ReadGenerated("VisualAsset.json"));
            }
        }

        [Fact]
        public void Template_UnclosedBrace_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateSyntax, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "{RoleId.DisplayName 没有闭合", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Template_ScriptTrace_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateSyntax, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "if (hasRest) { show }", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "正文", null, "Ignorable", false }));
        }

        [Fact]
        public void Template_IllegalPlaceholderShape_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateSyntax, "EventOption");
                AssertNoEventMirror(root);
            }, OptionSheetFixture(
                new object[] { "opt_v1", "evt_test_village", 1, "给恢复时间", null, "Hide" },
                new object[] { "opt_v2", "evt_test_village", 2, "继续承担 {RoleId.DisplayName + 1}", null, "Hide" },
                new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }));
        }

        // ---------------- WP-Event-1D §27/§10：模板 Role 引用硬校验（DAT047） ----------------

        [Fact]
        public void TemplateRoleRef_SameEventRealRole_Passes()
        {
            // 基线语料已使用真实 RoleId（{role_village.DisplayName} / {role_exp.DisplayName}）→ 必须全绿。
            WithDomain((run, root) =>
            {
                Assert.True(run.Success, DiagText(run));
            });
        }

        [Fact]
        public void TemplateRoleRef_UnknownRole_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "{role_does_not_exist.DisplayName} 正文", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "{role_exp.DisplayName} 正文", null, "Ignorable", false }));
        }

        [Fact]
        public void TemplateRoleRef_LiteralMetavariableName_Fails()
        {
            // WP-Event-1D 实际抓到的内容缺陷形态：把文档里的示例名字 `RoleId` 当真实内容写进模板。
            // 该 Role 不存在 → 必须硬错误（否则 Runtime Renderer 会 Fail Closed，产品静默不显示事件）。
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "{RoleId.DisplayName} 已经连续多次承担临时任务。", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "{role_exp.DisplayName} 正文", null, "Ignorable", false }));
        }

        [Fact]
        public void TemplateRoleRef_RoleOfAnotherEvent_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventPresentation");
                AssertNoEventMirror(root);
            }, PresentationSheetFixture(
                new object[] { "evt_test_village", "VillageModal", "标题", "{role_exp.DisplayName} 正文", null, "Timely", true },
                new object[] { "evt_test_exp", "JourneyCard", "标题", "{role_exp.DisplayName} 正文", null, "Ignorable", false }));
        }

        [Fact]
        public void TemplateRoleRef_CoversAllFiveTemplateFields()
        {
            // 硬校验必须作用于 §27 列出的全部五处模板字段。
            WithDomain((run, root) =>
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventPresentation"),
                PresentationSheetFixture(
                    new object[] { "evt_test_village", "VillageModal", "{role_nope.DisplayName}", "正文", null, "Timely", true },
                    new object[] { "evt_test_exp", "JourneyCard", "标题", "{role_exp.DisplayName} 正文", null, "Ignorable", false }));

            WithDomain((run, root) =>
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventPresentation"),
                PresentationSheetFixture(
                    new object[] { "evt_test_village", "VillageModal", "标题", "{role_nope.DisplayName} 正文", null, "Timely", true },
                    new object[] { "evt_test_exp", "JourneyCard", "标题", "标题2", null, "Ignorable", false }));

            WithDomain((run, root) =>
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventOption"),
                OptionSheetFixture(
                    new object[] { "opt_v1", "evt_test_village", 1, "{role_nope.DisplayName}", null, "Hide" },
                    new object[] { "opt_v2", "evt_test_village", 2, "继续承担", null, "Hide" },
                    new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                    new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }));

            WithDomain((run, root) =>
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventOption"),
                OptionSheetFixture(
                    new object[] { "opt_v1", "evt_test_village", 1, "给恢复时间", "{role_nope.DisplayName}", "Hide" },
                    new object[] { "opt_v2", "evt_test_village", 2, "继续承担", null, "Hide" },
                    new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                    new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }));

            WithDomain((run, root) =>
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventTemplateRoleRef, "EventOutcome"),
                OutcomeSheetFixture(
                    new object[] { "out_v1", "opt_v1", 100, "{role_nope.DisplayName} 已完成" },
                    new object[] { "out_v2", "opt_v2", 100, null },
                    new object[] { "out_e1a", "opt_e1", 70, null },
                    new object[] { "out_e1b", "opt_e1", 30, null },
                    new object[] { "out_e2", "opt_e2", 100, null }));
        }

        [Fact]
        public void TemplateRoleRef_ContextKeyPlaceholder_IsNotStaticallyProven()
        {
            // §9：{Context.X} 本轮只做语法检查（Key 由 Trigger Adapter 提供），不得因为
            // 「Compiler 不知道这个 Key」而报错 —— 缺失由 Runtime Renderer Fail Closed。
            WithDomain((run, root) =>
            {
                Assert.True(run.Success, DiagText(run));
            }, OptionSheetFixture(
                new object[] { "opt_v1", "evt_test_village", 1, "{role_village.DisplayName} 第 {Context.ObservedCount} 次", null, "Hide" },
                new object[] { "opt_v2", "evt_test_village", 2, "继续承担", null, "Hide" },
                new object[] { "opt_e1", "evt_test_exp", 1, "调查", null, "Hide" },
                new object[] { "opt_e2", "evt_test_exp", 2, "离开", null, "Hide" }));
        }

        // ---------------- 1C：事件历史条件的 TargetKey 语义（编译期补强，DAT046） ----------------

        [Fact]
        public void HistoryCondition_EventNotSeen_UnknownEventId_Fails()
        {
            // TargetKey 必须指向真实 EventId：否则条件永远不可能成立（静默内容缺陷）。
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionTargetRef, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_hist", "evt_test_village", 1, "EventNotSeen", null, "evt_does_not_exist", null, null }));
        }

        [Fact]
        public void HistoryCondition_EventNotSeen_RealEventId_Passes()
        {
            WithDomain((run, root) =>
            {
                Assert.True(run.Success, DiagText(run));
                Assert.Contains("\"TargetKey\": \"evt_test_exp\"", root.ReadGenerated("EventCondition.json"));
            }, ConditionSheetFixture(
                new object[] { "cond_hist", "evt_test_village", 1, "EventNotSeen", null, "evt_test_exp", null, null },
                new object[] { "cond_exp", "evt_test_exp", 1, "ExpeditionDepthAtLeast", null, null, 2, null }));
        }

        [Fact]
        public void HistoryCondition_SeenCount_UnknownEventId_Fails()
        {
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionTargetRef, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_hist", "evt_test_village", 1, "EventSeenCountAtLeast", null, "evt_nope", 2, null }));
        }

        [Fact]
        public void HistoryCondition_SeenCount_ZeroIntValue_Fails()
        {
            // 「至少见过 0 次」与「从未见过」语义相同 → 必须写 EventNotSeen（IntValue >= 1）。
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionTargetRef, "EventCondition");
                AssertNoEventMirror(root);
            }, ConditionSheetFixture(
                new object[] { "cond_hist", "evt_test_village", 1, "EventSeenCountAtLeast", null, "evt_test_exp", 0, null }));
        }

        [Fact]
        public void HistoryCondition_OptionCondition_UnknownEventId_Fails()
        {
            // 选项可用条件与事件成立条件共用同一套 ConditionType 语义 → 同样必须校验。
            WithDomain((run, root) =>
            {
                AssertHasErrorWithLocation(run, DiagnosticCodes.DatEventConditionTargetRef, "EventOptionCondition");
                AssertNoEventMirror(root);
            }, OptionConditionSheetFixture(
                new object[] { "oc_hist", "opt_v1", 1, "EventNotSeen", null, "evt_does_not_exist", null, null }));
        }

        [Fact]
        public void HistoryCondition_OptionCondition_SeenCount_RealEventId_Passes()
        {
            WithDomain((run, root) => { Assert.True(run.Success, DiagText(run)); },
                OptionConditionSheetFixture(
                    new object[] { "oc_hist", "opt_v1", 1, "EventSeenCountAtLeast", null, "evt_test_exp", 2, null }));
        }

    }
}
