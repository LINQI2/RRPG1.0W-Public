using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D1 §10/§11/§12：**最小 Template Renderer**。
    ///
    /// 保护的是产品可见契约：
    ///   - 只支持 `{RoleId.DisplayName}` 与 `{Context.Key}`；
    ///   - Person 显示名**必须**来自解析器（正式长期身份数据），不能用 RoleName / PersonId 冒充；
    ///   - 任何失败都 Fail Closed：不把占位符原样显示、不用空串掩盖配置问题，并给出可定位诊断。
    /// </summary>
    public class EventTemplateRendererTests
    {
        private const string EventId = "evt_render";
        private const string VillageTrigger = "VillageRecognitionRaised";
        private const string Person = "p1";

        private static ResolvedEventInstance Instance(int observedCount = 4)
        {
            var catalog = SingleEventCatalog(Evt(EventId), VillageTrigger,
                roles: new List<EventRoleDefinition>
                {
                    Role("Requester", EventId, "ContextPerson", "Observer")
                });

            var request = Request(VillageTrigger, "village-recognition|TemporaryDraftBuildup|p1|year_2", 7,
                new EventContext()
                    .SetPerson("Observer", Person)
                    .SetString("RecognitionType", "TemporaryDraftBuildup")
                    .SetInt("ObservedCount", observedCount));

            var resolved = Resolve(catalog, request);
            Assert.IsTrue(resolved.Success, "夹具必须能解析出事件实例：" + resolved.Diagnostic);
            return resolved.Instance;
        }

        private static bool NameOf(string personId, out string displayName)
        {
            displayName = personId == Person ? "阿石" : null;
            return displayName != null;
        }

        private static bool NoName(string personId, out string displayName)
        {
            displayName = null;
            return false;
        }

        private static bool EmptyName(string personId, out string displayName)
        {
            displayName = string.Empty;
            return true;
        }

        [Test]
        public void Render_RoleDisplayName_AndContext_WithLiteralText()
        {
            var result = EventTemplateRenderer.Render(
                "今年{Requester.DisplayName}已经第{Context.ObservedCount}次提到草稿（{Context.RecognitionType}）。",
                Instance(observedCount: 4), NameOf, "BodyTemplate");

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual("今年阿石已经第4次提到草稿（TemporaryDraftBuildup）。", result.Text);
        }

        [Test]
        public void Render_PersonContext_RendersRawPersonId()
        {
            var result = EventTemplateRenderer.Render("{Context.Observer}", Instance(), NameOf, "TitleTemplate");

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual(Person, result.Text, "Person Context 渲染的是人物ID（不是显示名）");
        }

        [Test]
        public void Render_EmptyTemplate_SucceedsWithEmptyText()
        {
            var nullResult = EventTemplateRenderer.Render(null, Instance(), NameOf);
            Assert.IsTrue(nullResult.Success);
            Assert.AreEqual(string.Empty, nullResult.Text);
            Assert.AreEqual(EventTemplateRenderFailure.None, nullResult.Failure);

            var emptyResult = EventTemplateRenderer.Render(string.Empty, Instance(), NameOf);
            Assert.IsTrue(emptyResult.Success);
            Assert.AreEqual(string.Empty, emptyResult.Text);
        }

        [Test]
        public void Render_MissingRole_IsRoleNotFound()
        {
            var result = EventTemplateRenderer.Render("{Steward.DisplayName}来了。", Instance(), NameOf,
                "BodyTemplate");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventTemplateRenderFailure.RoleNotFound, result.Failure);
            Assert.AreEqual("{Steward.DisplayName}", result.FailedPlaceholder);
            Assert.IsNull(result.Text, "失败时不得给出可显示文本");
            StringAssert.Contains("Steward", result.Diagnostic);
        }

        [Test]
        public void Render_ContextKeyMissing_IsExplicitFailure()
        {
            var result = EventTemplateRenderer.Render("{Context.NotThere}", Instance(), NameOf, "BodyTemplate");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventTemplateRenderFailure.ContextKeyMissing, result.Failure);
            StringAssert.Contains("NotThere", result.Diagnostic);
        }

        [Test]
        public void Render_PersonNameUnresolved_IsExplicitFailure()
        {
            var noResolver = EventTemplateRenderer.Render("{Requester.DisplayName}", Instance(), NoName,
                "TitleTemplate");
            Assert.IsFalse(noResolver.Success);
            Assert.AreEqual(EventTemplateRenderFailure.PersonNameUnresolved, noResolver.Failure);

            var emptyName = EventTemplateRenderer.Render("{Requester.DisplayName}", Instance(), EmptyName,
                "TitleTemplate");
            Assert.IsFalse(emptyName.Success, "解析出空串必须视为失败（不得用空串掩盖）");
            Assert.AreEqual(EventTemplateRenderFailure.PersonNameUnresolved, emptyName.Failure);

            var noResolverAtAll = EventTemplateRenderer.Render("{Requester.DisplayName}", Instance(), null,
                "TitleTemplate");
            Assert.AreEqual(EventTemplateRenderFailure.PersonNameUnresolved, noResolverAtAll.Failure);
        }

        [Test]
        public void Render_UnsupportedPlaceholderShape_IsRejected()
        {
            Assert.AreEqual(EventTemplateRenderFailure.UnsupportedPlaceholder,
                EventTemplateRenderer.Render("{Requester.RoleName}", Instance(), NameOf).Failure);

            Assert.AreEqual(EventTemplateRenderFailure.UnsupportedPlaceholder,
                EventTemplateRenderer.Render("{ObservedCount}", Instance(), NameOf).Failure);

            Assert.AreEqual(EventTemplateRenderFailure.UnsupportedPlaceholder,
                EventTemplateRenderer.Render("{Context}", Instance(), NameOf).Failure);
        }

        [Test]
        public void Render_UnknownRolePrefix_IsRoleNotFound_NotSilentlyAccepted()
        {
            // `{Person.DisplayName}` 里的 "Person" 只是一个**未定义的 RoleId**：
            // 必须报 RoleNotFound（明确、可定位），既不能当成语法错误混淆，
            // 更不能被当成「人物显示名」偷偷放过（§12 禁止用 PersonId / 模板猜显示名）。
            var result = EventTemplateRenderer.Render("{Person.DisplayName}", Instance(), NameOf);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventTemplateRenderFailure.RoleNotFound, result.Failure);
            StringAssert.Contains("Person", result.Diagnostic);
        }

        [Test]
        public void Render_MalformedPlaceholder_IsRejected()
        {
            var unclosed = EventTemplateRenderer.Render("前缀 {Requester.DisplayName", Instance(), NameOf);
            Assert.AreEqual(EventTemplateRenderFailure.MalformedPlaceholder, unclosed.Failure);

            var stray = EventTemplateRenderer.Render("后缀 }", Instance(), NameOf);
            Assert.AreEqual(EventTemplateRenderFailure.MalformedPlaceholder, stray.Failure);

            var empty = EventTemplateRenderer.Render("{}", Instance(), NameOf);
            Assert.AreEqual(EventTemplateRenderFailure.MalformedPlaceholder, empty.Failure);

            var nested = EventTemplateRenderer.Render("{Context.{ObservedCount}}", Instance(), NameOf);
            Assert.AreEqual(EventTemplateRenderFailure.MalformedPlaceholder, nested.Failure);
        }

        [Test]
        public void Render_NullInstance_IsExplicitFailure()
        {
            var result = EventTemplateRenderer.Render("{Context.ObservedCount}", null, NameOf, "TitleTemplate");

            Assert.IsFalse(result.Success);
            StringAssert.Contains("实例", result.Diagnostic);
        }

        [Test]
        public void Render_UnboundRole_AndEmptyContext_AreExplicitFailures()
        {
            // 成功解析出的实例不会出现「Role 未绑定」，这一条是防御路径（例如未来存档手改 / 迁移残缺）：
            // 用反射构造一条 PersonId 为空的绑定，确认渲染器同样 Fail Closed。
            var instance = UnboundRoleInstance();

            var role = EventTemplateRenderer.Render("{Requester.DisplayName}", instance, NameOf, "TitleTemplate");
            Assert.IsFalse(role.Success);
            Assert.AreEqual(EventTemplateRenderFailure.RoleNotBound, role.Failure);

            var context = EventTemplateRenderer.Render("{Context.ObservedCount}", instance, NameOf);
            Assert.IsFalse(context.Success);
            Assert.AreEqual(EventTemplateRenderFailure.ContextKeyMissing, context.Failure);
        }

        private static ResolvedEventInstance UnboundRoleInstance()
        {
            const System.Reflection.BindingFlags NonPublic = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;

            var snapshot = (EventContextSnapshot)System.Activator.CreateInstance(typeof(EventContextSnapshot),
                NonPublic, null, new object[] { new List<EventContextEntry>() }, null);

            var binding = (EventRoleBinding)System.Activator.CreateInstance(typeof(EventRoleBinding),
                NonPublic, null, new object[] { "Requester", "请求者", "VillagePerson", null, null }, null);

            var bindings = new List<EventRoleBinding> { binding };

            return (ResolvedEventInstance)System.Activator.CreateInstance(typeof(ResolvedEventInstance),
                NonPublic, null,
                new object[] { "inst_unbound", EventId, VillageTrigger, "k_unbound", 1, bindings, snapshot },
                null);
        }
    }
}
