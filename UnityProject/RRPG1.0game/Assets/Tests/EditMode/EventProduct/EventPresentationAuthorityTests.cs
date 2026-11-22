using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RRPG.StaticConfig;
using UnityEngine;
using UnityEngine.UI;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D3：**Presentation Authority Closure** 的长期契约（不依赖 Scene 的部分）。
    ///
    /// 覆盖（对应 D3 Spec §13 的 D3-E1..E12；需要真实产品会话的部分由 PlayMode 承担）：
    ///   D3-E1 / E11：Village 正式模式只读 EventPresentation（Legacy Pattern 文本 / urgency 只服务旧档兼容模式）；
    ///   D3-E2：Expedition 正式路径**不**使用 NodeId → JourneyEventPresentationCatalog 决定事件身份 / 插图；
    ///   D3-E3：Village / Expedition **共用同一个**产品表现文本入口（内部唯一 EventTemplateRenderer）；
    ///   D3-E6：表现渲染 Fail Closed（不显示占位符 / 不产生半成品）；
    ///   D3-E7 / E8：插图只走 `EventId → EventPresentation.IllustrationAssetId → 共享 VisualAsset → Sprite`；
    ///               配置了 AssetId 但失效时**明确诊断**且**不**静默换成其它图片；
    ///   D3-E9 / E10：Journey 统一卡面支持 >2 个配置驱动 Option，并如实反映 Disabled。
    ///
    /// D3-E4 / E5（Role DisplayName / Context 占位符正确性）由 Core `EventTemplateRendererTests`
    /// 与 PlayMode P-D3-1 / P-D3-2 的真实人物名断言共同覆盖；D3-E12 由 PlayMode 覆盖。
    /// </summary>
    public class EventPresentationAuthorityTests
    {
        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static string SourcePath(string relativeUnderScripts)
        {
            return Path.Combine(Application.dataPath, relativeUnderScripts.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Slice(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "源码中找不到起点标记：" + startMarker);

            int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            if (end < 0) end = source.Length;

            return source.Substring(start, end - start);
        }

        private static Type Cs(string name)
        {
            var type = Type.GetType(name + ", Assembly-CSharp");
            Assert.IsNotNull(type, "类型不存在：" + name);
            return type;
        }

        // ==================== D3-E1 / E11：Village 正式模式只读 EventPresentation ====================

        [Test]
        public void D3E1_VillageFormalMode_DoesNotReadLegacyPatternTextsOrUrgency()
        {
            string source = File.ReadAllText(SourcePath("Scripts/Village/UI/VillageSocialEventUI.cs"));
            string formal = Slice(source, "public void ShowEventPending", "private void BuildOptionButtons");

            StringAssert.Contains("TryRenderPendingTexts", formal,
                "正式模式标题 / 正文必须经 Event Flow 的配置文本渲染（EventPresentation）");
            StringAssert.DoesNotContain("ResolveUrgency(", formal,
                "正式模式不得读 Legacy Pattern urgency（Urgency 必须来自 EventPresentation）");
            StringAssert.DoesNotContain("PatternKey", formal,
                "正式模式不得引用 Legacy PatternKey（Legacy 文本只服务旧档兼容模式）");

            // Legacy 兼容模式仍保留（只服务无法迁移的旧档 Pending）—— 它不是本 Gate 要删除的对象。
            string legacy = Slice(source, "public void ShowPending", "public void ShowEventPending");
            StringAssert.Contains("VillageTestSocialPatterns", legacy,
                "Legacy 兼容模式应继续由其既有 Pattern 目录提供文本（本 Gate 不迁移旧档）");
        }

        [Test]
        public void D3E11_VillageFormalMode_UrgencyAndPauseComeFromEventPresentation()
        {
            string source = File.ReadAllText(SourcePath("Scripts/Village/UI/VillageSocialEventUI.cs"));
            string formal = Slice(source, "public void ShowEventPending", "private void BuildOptionButtons");

            StringAssert.Contains("presentation.PauseOnOpen", formal, "PauseOnOpen 必须来自 EventPresentation");
            StringAssert.Contains("presentation.Urgency", formal, "Urgency 必须来自 EventPresentation");
        }

        // ==================== D3-E2：Expedition 正式路径不依赖 Legacy NodeId Catalog ====================

        [Test]
        public void D3E2_ExpeditionFormalPath_DoesNotUseNodeIdPresentationCatalog()
        {
            string adapter = File.ReadAllText(SourcePath("Scripts/EventHost/ExpeditionEventCardAdapter.cs"));

            StringAssert.DoesNotContain("ResolveForEvent(", adapter,
                "正式 Event 卡不得用 NodeId → Catalog 的逐事件插图解析");
            StringAssert.DoesNotContain("ResolveCategoryFallback(", adapter,
                "正式 Event 卡不得走 Legacy 类别回退入口（插图必须由 IllustrationAssetId 解析）");
            StringAssert.DoesNotContain("JourneyEventPresentationCatalog.Instance", adapter,
                "正式 Event 卡不得读取 Legacy Catalog 单例");
            StringAssert.DoesNotContain(".FindEntry(", adapter, "正式 Event 卡不得按 NodeId 查 Legacy Catalog 配置行");

            StringAssert.Contains("EventPresentation", adapter, "正式 Event 卡必须读 EventPresentation");
            StringAssert.Contains("IllustrationAssetId", adapter, "插图必须由 EventPresentation.IllustrationAssetId 驱动");
            StringAssert.Contains("TryGetVisualAssets", adapter, "插图必须走共享 VisualAsset 权威");
        }

        // ==================== D3-E3：两个表面共用唯一表现文本入口 ====================

        [Test]
        public void D3E3_VillageAndExpedition_ShareTheSinglePresentationTextEntry()
        {
            var textsType = Cs("EventPresentationTexts");
            Assert.IsNotNull(textsType);

            string textsSource = File.ReadAllText(SourcePath("Scripts/EventHost/EventPresentationTexts.cs"));
            StringAssert.Contains("EventTemplateRenderer.Render", textsSource,
                "唯一产品表现文本入口内部必须调用唯一 Core 渲染器");
            StringAssert.DoesNotContain(".Replace(", textsSource, "不得自实现第二套模板替换");

            string village = File.ReadAllText(SourcePath("Scripts/EventHost/VillageEventFlow.cs"));
            StringAssert.Contains("EventPresentationTexts.TryRenderTexts", village,
                "Village 必须经共用入口渲染 Title / Body");
            StringAssert.DoesNotContain(".Replace(", village, "Village 不得自实现第二套模板替换");

            string expedition = File.ReadAllText(SourcePath("Scripts/EventHost/ExpeditionEventCardAdapter.cs"));
            StringAssert.Contains("EventPresentationTexts.TryRenderTexts", expedition,
                "Expedition 必须经共用入口渲染 Title / Body");
            StringAssert.Contains("EventPresentationTexts.RenderTemplate", expedition,
                "Expedition Option 文案必须经同一共用入口");
            StringAssert.DoesNotContain(".Replace(", expedition, "Expedition 不得自实现第二套模板替换");
        }

        // ==================== D3-E6：表现渲染 Fail Closed ====================

        [Test]
        public void D3E6_PresentationRendering_FailsClosed_WithoutShowingPlaceholders()
        {
            var textsType = Cs("EventPresentationTexts");

            var render = textsType.GetMethod("RenderTemplate", Flags);
            Assert.IsNotNull(render, "EventPresentationTexts.RenderTemplate 必须存在");

            // 1) 语法非法模板 / 无法解析的角色 → 空文本 + 可操作诊断（绝不返回原始占位符）
            var args = new object[] { null, "{RoleId.DisplayName}", null, "D3-Test", null };
            string value = (string)render.Invoke(null, args);
            string diagnostic = (string)args[4];

            Assert.AreEqual(string.Empty, value, "渲染失败必须返回空串（不得把 {RoleId.DisplayName} 交给 UI）");
            Assert.IsFalse(string.IsNullOrEmpty(diagnostic), "渲染失败必须给出可操作诊断");

            // 2) 没有已解析实例 / 没有表现配置 → 整体失败（不产生半成品卡面）
            var tryRender = textsType.GetMethod("TryRenderTexts", Flags);
            Assert.IsNotNull(tryRender, "EventPresentationTexts.TryRenderTexts 必须存在");

            var noInstance = new object[] { null, null, null, null, null, null };
            Assert.IsFalse((bool)tryRender.Invoke(null, noInstance),
                "没有已解析实例时必须失败（不得渲染空卡面冒充成功）");
            Assert.IsFalse(string.IsNullOrEmpty((string)noInstance[5]), "必须给出可操作诊断");

            // 3) 空模板（配置留空）= 合法状态：成功且文本为空（不是错误，不产生诊断噪音）
            var emptyTemplate = new object[] { null, string.Empty, null, "D3-Test", null };
            Assert.AreEqual(string.Empty, (string)render.Invoke(null, emptyTemplate));
            Assert.IsTrue(string.IsNullOrEmpty((string)emptyTemplate[4]), "空模板不是错误");
        }

        // ==================== D3-E7 / E8：插图唯一正式链 + 失效语义 ====================

        private static EventPresentationDefinition NewPresentation(string eventId, string illustrationAssetId)
        {
            return new EventPresentationDefinition
            {
                EventId = eventId,
                TitleTemplate = "D3 测试标题",
                BodyTemplate = "D3 测试正文",
                PresentationChannel = "JourneyCard",
                IllustrationAssetId = illustrationAssetId
            };
        }

        private static Sprite NewFallbackSprite()
        {
            return Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            if (sprite.texture != null) UnityEngine.Object.DestroyImmediate(sprite.texture);
            UnityEngine.Object.DestroyImmediate(sprite);
        }

        [Test]
        public void D3E7_ConfiguredIllustrationAssetId_ResolvesThroughSharedVisualAsset()
        {
            var adapter = Cs("ExpeditionEventCardAdapter");
            var resolve = adapter.GetMethod("ResolveEventIllustration", Flags);
            Assert.IsNotNull(resolve, "ResolveEventIllustration 必须是正式插图唯一入口");

            VisualAssetCatalog catalog;
            string catalogDiagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetVisualAssets(out catalog, out catalogDiagnostic),
                "共享 VisualAsset 权威必须可用：" + catalogDiagnostic);
            Assert.Greater(catalog.Count, 0, "VisualAsset 表不得为空");

            // 取当前配置中**真实存在且生成根完整**的一条 VisualAsset（内容侧唯一权威；不新建临时资源）。
            VisualAssetDefinition asset = null;
            for (int i = 0; i < catalog.All.Count; i++)
            {
                var candidate = catalog.All[i];
                if (candidate != null && candidate.Sprite != null) { asset = candidate; break; }
            }
            Assert.IsNotNull(asset, "VisualAsset 表里必须至少有一条 managed Sprite 已生成的资源（生成根完整）");

            var presentation = NewPresentation("evt_d3_fixture", asset.VisualAssetId);
            var fallback = NewFallbackSprite();

            var resolved = (Sprite)resolve.Invoke(null, new object[] { presentation, fallback });

            Assert.AreSame(asset.Sprite, resolved,
                "配置了合法 IllustrationAssetId 时必须解析到共享 VisualAsset 的**同一** Sprite");
            Assert.AreEqual("visualasset:" + asset.VisualAssetId,
                (string)adapter.GetProperty("LastIllustrationSource", Flags).GetValue(null),
                "插图来源必须可诊断（visualasset:<id>）");

            DestroySprite(fallback);
        }

        [Test]
        public void D3E8_UnresolvableConfiguredIllustration_DiagnosesAndNeverSubstitutesAnotherImage()
        {
            var adapter = Cs("ExpeditionEventCardAdapter");
            var resolve = adapter.GetMethod("ResolveEventIllustration", Flags);
            var sourceProperty = adapter.GetProperty("LastIllustrationSource", Flags);
            var diagnosticProperty = adapter.GetProperty("LastIllustrationDiagnostic", Flags);

            var fallback = NewFallbackSprite();

            // 1) 配了 AssetId 但不存在 → 明确诊断 + 类别级占位图（绝不换别的图）
            var broken = NewPresentation("evt_d3_broken", "va_d3_does_not_exist");
            var result = (Sprite)resolve.Invoke(null, new object[] { broken, fallback });

            Assert.AreSame(fallback, result, "失效插图不得换成其它图片（只能回退到类别级占位图）");
            Assert.IsFalse(string.IsNullOrEmpty((string)diagnosticProperty.GetValue(null)),
                "配了 AssetId 却无法解析必须产生明确诊断（不得静默）");
            StringAssert.StartsWith("category:", (string)sourceProperty.GetValue(null),
                "来源必须标记为类别级 fallback（与「解析成功」可区分）");

            // 2) 未配置 AssetId（Schema optional）= 有意不配图 → 无诊断噪音
            var unconfigured = NewPresentation("evt_d3_none", null);
            var result2 = (Sprite)resolve.Invoke(null, new object[] { unconfigured, fallback });

            Assert.AreSame(fallback, result2, "未配置插图时使用类别级占位图");
            Assert.IsTrue(string.IsNullOrEmpty((string)diagnosticProperty.GetValue(null)),
                "「有意不配置插图」不是错误：不应产生诊断噪音");
            StringAssert.Contains("unconfigured", (string)sourceProperty.GetValue(null),
                "来源必须能区分「有意不配置」");

            // 3) 若表内存在「已登记但 managed Sprite 未生成」的资源 → 同属失效语义（明确诊断 + 不换图）。
            //    （当前 Event 插图内容尚未入表，因此本分支只在环境存在此类行时执行，属环境相关的加固断言。）
            VisualAssetCatalog catalog;
            string catalogDiagnostic;
            if (StaticConfigRegistry.TryGetVisualAssets(out catalog, out catalogDiagnostic) && catalog != null)
            {
                for (int i = 0; i < catalog.All.Count; i++)
                {
                    var candidate = catalog.All[i];
                    if (candidate == null || candidate.Sprite != null) continue;

                    var spriteMissing = NewPresentation("evt_d3_sprite_missing", candidate.VisualAssetId);
                    Assert.AreSame(fallback, (Sprite)resolve.Invoke(null, new object[] { spriteMissing, fallback }),
                        "已登记但 Sprite 未生成时同样不得换成其它图片");
                    StringAssert.Contains("sprite-missing", (string)sourceProperty.GetValue(null),
                        "来源必须标记 sprite-missing");
                    Assert.IsFalse(string.IsNullOrEmpty((string)diagnosticProperty.GetValue(null)),
                        "Sprite 未生成必须产生明确诊断");
                    break;
                }
            }

            DestroySprite(fallback);
        }

        // ==================== D3-E9 / E10：统一卡面的动态选项与可用性 ====================

        [Test]
        public void D3E9_D3E10_JourneyCard_RendersConfiguredOptions_AndHonorsInteractable()
        {
            var viewType = Cs("JourneyEventCardView");
            var configType = Cs("JourneyEventCardConfig");
            var optionType = Cs("JourneyEventCardOption");

            var host = new GameObject("D3Card", typeof(RectTransform));
            var card = host.AddComponent(viewType);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(host.transform, false);
            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(host.transform, false);
            var imageGo = new GameObject("Illustration", typeof(RectTransform));
            imageGo.transform.SetParent(host.transform, false);
            var optionRoot = new GameObject("ChoiceRoot", typeof(RectTransform));
            optionRoot.transform.SetParent(host.transform, false);

            SetMember(card, "root", host);
            SetMember(card, "titleHost", titleGo.AddComponent<TextMeshProUGUI>());
            SetMember(card, "bodyHost", bodyGo.AddComponent<TextMeshProUGUI>());
            SetMember(card, "illustrationHost", imageGo.AddComponent<Image>());
            SetMember(card, "optionRoot", optionRoot.GetComponent<RectTransform>());

            var first = Activator.CreateInstance(optionType);
            SetMember(first, "Label", "选项一");

            var disabled = Activator.CreateInstance(optionType);
            SetMember(disabled, "Label", "选项二");
            SetMember(disabled, "Interactable", false);   // = EventOptionEvaluator 的 Disable

            var third = Activator.CreateInstance(optionType);
            SetMember(third, "Label", "选项三");

            var config = Activator.CreateInstance(configType);
            SetMember(config, "Title", "D3 卡面");
            SetMember(config, "Body", "D3 正文");
            var listType = typeof(List<>).MakeGenericType(optionType);
            var options = (System.Collections.IList)Activator.CreateInstance(listType);
            options.Add(first);
            options.Add(disabled);
            options.Add(third);
            SetMember(config, "Options", options);

            // 注意：本用例在 EditMode 下只验证「渲染形状」（Option 数 / 可用性如实反映）。
            // 「点击 → 索引回调」需要 PlayMode（JourneyEventCardView.Hide() 内部用 Destroy，Edit 模式下不允许），
            // 由 D3 PlayMode 用例覆盖（P-D3-2 同一卡面入口）。
            Action<int> onChosen = i => { };
            var render = viewType.GetMethod("Render", Flags);
            Assert.IsNotNull(render, "JourneyEventCardView.Render 必须存在");
            render.Invoke(card, new object[] { config, onChosen });

            Assert.AreEqual(3, (int)viewType.GetProperty("OptionCount", Flags).GetValue(card),
                "统一卡面必须按配置渲染 N（>2）个 Option（不得假设固定两个）");

            var buttons = new List<Button>();
            foreach (Transform child in optionRoot.transform)
            {
                var button = child.GetComponent<Button>();
                if (button != null) buttons.Add(button);
            }
            Assert.AreEqual(3, buttons.Count, "应生成 3 个 Option 按钮");
            Assert.IsTrue(buttons[0].interactable, "Available Option 必须可点");
            Assert.IsFalse(buttons[1].interactable, "Disabled Option 必须不可点（availability 如实反映）");
            Assert.IsTrue(buttons[2].interactable, "Available Option 必须可点");

            UnityEngine.Object.DestroyImmediate(host);
        }

        private static void SetMember(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name, Flags);
            if (field != null) { field.SetValue(target, value); return; }

            var property = type.GetProperty(name, Flags);
            Assert.IsNotNull(property, "成员不存在：" + name + " @ " + type.Name);
            property.SetValue(target, value, null);
        }
    }
}
