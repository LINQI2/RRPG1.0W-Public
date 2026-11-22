using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 人物长期成长全链路 PlayMode 验证（真实产品路径）。
///
/// 闭环：存档含可升级经验 → Town「人物成长」入口 + 红点 → 人物列表红点 →
///       进入详情点击升级（消耗经验 + 等级+1）→ CardGrowth 三选一/Skip →
///       选定牌 → 替换 Starter 槽 → 存档持久化 → 下一次远征 Run Deck 真实读取新 Starter。
/// </summary>
public class CharacterGrowthPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type GetType(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        return p.GetValue(o, null);
    }

    private static void SetMember(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) { f.SetValue(o, value); return; }
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        p.SetValue(o, value, null);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private static object GetDataManager()
    {
        return GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    private static object GetProperty(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(o, null);
    }

    /// <summary>
    /// M4 测试种子：给真实普通 Character 挂载一个 ActiveProfessionProgress（Profession.Runtime，经反射创建）。
    /// 只做数据级测试准备，不实现 Formation 流程（Formation 属未来 M 阶段）。
    /// </summary>
    private static object AttachProfessionProgress(object character, string professionId, int level, int exp,
        IEnumerable<string> unlockedPool, int traitLevel)
    {
        var ppType = Type.GetType("ProfessionProgress, Profession.Runtime");
        Assert.IsNotNull(ppType, "ProfessionProgress（Profession.Runtime）应存在");
        object pp = Activator.CreateInstance(ppType);
        SetMember(pp, "ProfessionId", professionId);
        SetMember(pp, "ProfessionLevel", level);
        SetMember(pp, "ProfessionExp", exp);
        SetMember(pp, "UnlockedPoolCardIds", unlockedPool == null ? new List<string>() : unlockedPool.ToList());
        SetMember(pp, "ProfessionTraitLevel", traitLevel);
        SetMember(character, "ActiveProfessionProgress", pp);
        return pp;
    }

    [UnityTest]
    public IEnumerator FullLoop_TownGrowthUi_LevelUp_NextRunDeck()
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        try
        {
            // ---------- 进入真实场景 ----------
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            // ---------- 建立带成长经验的存档 ----------
            var collection = CallStatic("SaveSystem", "NewGame");
            Assert.IsNotNull(collection, "NewGame 失败");
            var chars = (IList)GetMember(collection, "OwnedCharacters");
            Assert.Greater(chars.Count, 0, "默认角色卡册不应为空");
            var char001 = chars.Cast<object>().First(c => (string)GetMember(c, "CharacterId") == "char_001");
            // M4：普通 Character 的 Growth 权威 = ActiveProfessionProgress。升级前为战士挂载 ProfessionProgress
            //（数据级测试种子，Formation 属未来 M 阶段，不实现 Formation 流程），ProfessionExp=10 对应测试曲线
            // [10,15,...]：ProfessionLv1→Lv2 需 10 → 可升 1 级（CardGrowth）。
            AttachProfessionProgress(char001, "profession_test_hunter", 1, 10,
                new[] { "bcard_aim", "bcard_sword", "bcard_rally" }, 0);
            CallStatic("SaveSystem", "Save", collection);

            // ---------- CharacterGrowthUI 宿主：场景无固定对象时运行时创建 ----------
            var growth = FindInstance("CharacterGrowthUI");
            if (growth == null)
            {
                var t = GetType("CharacterGrowthUI");
                Assert.IsNotNull(t, "CharacterGrowthUI 类型未找到");
                new GameObject("CharacterGrowthUI", new[] { t });
                yield return null;
                yield return null;
                growth = FindInstance("CharacterGrowthUI");
            }
            Assert.IsNotNull(growth, "CharacterGrowthUI 宿主应存在");
            SetMember(growth, "playerData", collection);
            Call(growth, "RefreshTownEntry", collection);
            Call(growth, "Open");
            yield return null;

            // ---------- Town 入口按钮 + 红点（可升级提示） ----------
            var town = FindInstance("TownUI");
            var townCanvas = (GameObject)GetMember(town, "canvasRoot");
            Assert.IsNotNull(townCanvas);
            var btnGrowthGo = townCanvas.transform.Find("BtnCharacterGrowth");
            Assert.IsNotNull(btnGrowthGo, "Town 应有人物成长入口按钮");
            var redDot = btnGrowthGo.Find("RedDot");
            Assert.IsNotNull(redDot, "Town 入口应有红点子对象");
            Assert.IsTrue(redDot.gameObject.activeSelf, "人物可升级时 Town 入口应显示红点");

            // ---------- 人物列表：战士卡可升级红点 ----------
            var growthGo = (GameObject)GetMember(growth, "gameObject");
            var listRoot = growthGo.transform.Find("GrowthCanvas/Content/ListRoot");
            Assert.IsNotNull(listRoot, "成长界面应有 ListRoot");
            // M1：人物列表项按实例 PersonId 命名（不再是模板 ID）——按该具体人物名字查找
            //（Initial Person & Profession Bootstrap：人物名字来自 Person.csv / PersonDefinition.DisplayName，
            //  不再使用「战士」这类职业词作为人物身份）
            string char001Name = (string)GetMember(char001, "DisplayName");
            Assert.IsFalse(string.IsNullOrEmpty(char001Name), "人物必须有显示名");
            Transform charCard = null;
            foreach (Transform child in listRoot)
            {
                if (child == null || !child.name.StartsWith("CharCard_")) continue;
                var nameText = child.Find("Text_Name");
                var txt = nameText != null ? nameText.GetComponent<TMP_Text>() : null;
                if (txt != null && txt.text.Contains(char001Name))
                {
                    charCard = child;
                    break;
                }
            }
            Assert.IsNotNull(charCard, "人物列表应包含初始人物：" + char001Name);
            Assert.IsNotNull(charCard.Find("RedDot"), "可升级人物卡应有红点");

            // ---------- 点击战士 → 详情 → 点击升级 ----------
            charCard.GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            var detailRoot = growthGo.transform.Find("GrowthCanvas/Content/DetailRoot");
            var btnLevelUp = detailRoot.Find("BtnLevelUp");
            Assert.IsNotNull(btnLevelUp, "详情应有升级按钮");
            Assert.IsTrue(btnLevelUp.GetComponent<Button>().interactable, "经验足够时升级按钮可点");

            btnLevelUp.GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            // 升级后：ProfessionLevel 2、ProfessionExp 0（Legacy Level/Exp 不再承担 Growth 权威）；
            // CardGrowth 三选一 + Skip 出现
            var ppAfter = GetMember(char001, "ActiveProfessionProgress");
            Assert.AreEqual(2, (int)GetMember(ppAfter, "ProfessionLevel"), "点击升级后才提升职业等级（不自动升级）");
            Assert.AreEqual(0, (int)GetMember(ppAfter, "ProfessionExp"), "升级应消耗职业经验");
            Assert.AreEqual(1, (int)GetMember(char001, "Level"), "M4：普通 Character 升级不得改写 Legacy Level");
            Assert.IsNotNull(detailRoot.Find("F_Card0"), "CardGrowth 应显示候选 0");
            Assert.IsNotNull(detailRoot.Find("F_Card1"), "CardGrowth 应显示候选 1");
            Assert.IsNotNull(detailRoot.Find("F_Card2"), "CardGrowth 应显示候选 2");
            Assert.IsNotNull(detailRoot.Find("F_Skip"), "CardGrowth 应提供 Skip");

            // ---------- 选定一张牌（战士池 [aim, sword, rally] → F_Card1 = 挥砍） ----------
            detailRoot.Find("F_Card1").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            var fStrat0 = detailRoot.Find("F_Strat0");
            Assert.IsNotNull(fStrat0, "应显示 Starter 处理策略");
            fStrat0.GetComponent<Button>().onClick.Invoke(); // 替换槽 0
            yield return null;
            yield return null;

            // ---------- Starter 已替换 ----------
            var slots = (IList)GetMember(char001, "StarterSlots");
            Assert.AreEqual("bcard_sword", ((StarterSlotEntry)slots[0]).CardId, "Starter 槽 0 应被替换为所选牌");

            // ---------- 存档持久化（长期状态） ----------
            var reloaded = CallStatic("SaveSystem", "Load");
            Assert.IsNotNull(reloaded, "重载存档失败");
            var rChar = ((IList)GetMember(reloaded, "OwnedCharacters")).Cast<object>()
                .First(c => (string)GetMember(c, "CharacterId") == "char_001");
            Assert.AreEqual(1, (int)GetMember(rChar, "Level"), "存档 Legacy Level 不受职业升级影响（M4）");
            var rPp = GetMember(rChar, "ActiveProfessionProgress");
            Assert.IsNotNull(rPp, "存档应保留 ActiveProfessionProgress（Growth 权威）");
            Assert.AreEqual(2, (int)GetMember(rPp, "ProfessionLevel"), "存档应保留职业等级（ProfessionLevel）");
            var rSlots = (IList)GetMember(rChar, "StarterSlots");
            Assert.AreEqual("bcard_sword", ((StarterSlotEntry)rSlots[0]).CardId, "存档应保留替换后的 Starter");

            // ---------- 下一次远征 Run Deck 真实读取新 Starter ----------
            var dataManager = GetDataManager();
            var expSystem = Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { reloaded });
            var rCharCards = (IList)GetMember(reloaded, "OwnedCharacterCards");
            Call(expSystem, "AddCharacterCard",
                rCharCards.Cast<object>().First(c => (string)GetMember(c, "CharacterId") == "char_001"));
            var rHeroes = (IList)GetMember(reloaded, "OwnedHeroes");
            object rHero = rHeroes.Count > 0 ? rHeroes[0] : null;
            if (rHero != null)
            {
                var hd = Call(dataManager, "GetHeroById", GetMember(rHero, "HeroId"));
                Call(expSystem, "SelectHero", rHero, hd);
            }
            var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
            var args = new object[2];
            Assert.IsTrue((bool)confirm.Invoke(expSystem, args), "确认出征失败");

            var adventure = Activator.CreateInstance(GetType("AdventureManager"),
                new object[]
                {
                    reloaded, args[0], args[1],
                    GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
                    StaticMonsterTestData.Build(), null
                });
            var runState = (RunState)GetProperty(adventure, "RunState");
            // M1：Run Deck 归属使用真实 PersonId（同模板多实例不塌缩），不再用模板 char_001
            string rCharPid = (string)GetMember(rChar, "PersonId");
            Assert.IsFalse(string.IsNullOrEmpty(rCharPid), "人物实例应拥有 PersonId");
            Assert.IsTrue(runState.RunDeckEntries.Any(e => e.PersonId == rCharPid && e.Template.CardId == "bcard_sword"),
                "下一次远征 Run Deck 应使用替换后的 Starter");
            Assert.IsFalse(runState.RunDeckEntries.Any(e => e.PersonId == rCharPid && e.Template.CardId == "bcard_aim"),
                "旧 Starter 不应再出现在 Run Deck");

            // ---------- 升级后无人可升级 → Town 红点清除 ----------
            Call(growth, "RefreshTownEntry", reloaded);
            yield return null;
            Assert.IsFalse(redDot.gameObject.activeSelf, "无人可升级时 Town 红点应隐藏");
        }
        finally
        {
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }

    [UnityTest]
    public IEnumerator ModalBlocker_BlocksTownInteractions_WhileOpen()
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        try
        {
            // ---------- 进入真实场景 ----------
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var collection = CallStatic("SaveSystem", "NewGame");
            CallStatic("SaveSystem", "Save", collection);
            Call(FindInstance("MainMenuUI"), "OnContinue");
            yield return null; yield return null;

            // ---------- 正式宿主（Prefab 实例）或运行时兜底 ----------
            var growth = FindInstance("CharacterGrowthUI");
            if (growth == null)
            {
                var t = GetType("CharacterGrowthUI");
                Assert.IsNotNull(t, "CharacterGrowthUI 类型未找到");
                new GameObject("CharacterGrowthUI", new[] { t });
                yield return null;
                yield return null;
                growth = FindInstance("CharacterGrowthUI");
            }
            Assert.IsNotNull(growth, "CharacterGrowthUI 宿主应存在");
            SetMember(growth, "playerData", collection);
            Call(growth, "Open");
            yield return null;

            var growthGo = (GameObject)GetMember(growth, "gameObject");
            var growthCanvas = growthGo.transform.Find("GrowthCanvas");
            Assert.IsNotNull(growthCanvas, "GrowthCanvas 应存在");
            Assert.IsTrue(growthCanvas.gameObject.activeSelf, "打开后 GrowthCanvas 应激活");

            // B: use the real Home navigation beneath the compatibility growth modal.
            var town = FindInstance("TownUI");
            var townGo = (GameObject)GetMember(town, "gameObject");
            var townCanvas = (GameObject)GetMember(town, "canvasRoot");
            Assert.IsNotNull(townCanvas, "TownCanvas 应存在");
            yield return null;
            yield return null; // 等待 CanvasScaler/布局稳定

            var btnExp = GameObject.Find("Navigate5").transform;
            Assert.IsNotNull(btnExp, "Home 应有远征导航");
            var screenPos = RectTransformUtility.WorldToScreenPoint(null, btnExp.position);

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Assert.IsNotNull(eventSystem, "场景应有 EventSystem");
            var eventData = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = screenPos };
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();

            // ---------- 打开期间：真实 Raycast 顶层命中必须是成长画布内元素，不得穿透到 Town 按钮 ----------
            eventSystem.RaycastAll(eventData, results);
            Assert.Greater(results.Count, 0, "打开成长界面时应有命中");
            var top = results[0].gameObject;
            Assert.IsFalse(top.transform.IsChildOf(btnExp), "成长界面打开时不得命中底层 Town 按钮（交互必须被阻断）");
            Assert.IsTrue(top.transform.IsChildOf(growthCanvas), "顶层命中应为成长画布内元素（模态遮罩/面板）");

            // ---------- 关闭后：底层 Town 交互恢复 ----------
            Call(growth, "Hide");
            yield return null;
            yield return null;
            results.Clear();
            eventSystem.RaycastAll(eventData, results);
            Assert.Greater(results.Count, 0, "关闭后 Town 应可被命中");
            var topAfter = results[0].gameObject;
            Assert.IsTrue(topAfter.transform==btnExp || topAfter.transform.IsChildOf(btnExp),
                "关闭成长界面后底层 Town 交互应恢复（命中 Town 按钮或其子元素）");
        }
        finally
        {
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }

    [UnityTest]
    public IEnumerator CloseButton_HidesGrowthCanvas()
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        try
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var collection = CallStatic("SaveSystem", "NewGame");
            CallStatic("SaveSystem", "Save", collection);

            var growth = FindInstance("CharacterGrowthUI");
            if (growth == null)
            {
                var t = GetType("CharacterGrowthUI");
                Assert.IsNotNull(t, "CharacterGrowthUI 类型未找到");
                new GameObject("CharacterGrowthUI", new[] { t });
                yield return null;
                yield return null;
                growth = FindInstance("CharacterGrowthUI");
            }
            Assert.IsNotNull(growth, "CharacterGrowthUI 宿主应存在");
            SetMember(growth, "playerData", collection);
            Call(growth, "Open");
            yield return null;

            var growthGo = (GameObject)GetMember(growth, "gameObject");
            var growthCanvas = growthGo.transform.Find("GrowthCanvas");
            Assert.IsNotNull(growthCanvas, "GrowthCanvas 应存在");
            Assert.IsTrue(growthCanvas.gameObject.activeSelf, "打开后 GrowthCanvas 应激活");

            // ---------- 关闭按钮应能关闭模态界面 ----------
            var btnClose = growthGo.transform.Find("GrowthCanvas/Content/BtnClose");
            Assert.IsNotNull(btnClose, "应存在 BtnClose 关闭按钮");
            var closeBtnComp = btnClose.GetComponent<UnityEngine.UI.Button>();
            Assert.IsNotNull(closeBtnComp, "BtnClose 应为 Button");

            closeBtnComp.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsFalse(growthCanvas.gameObject.activeSelf, "点击关闭按钮后 GrowthCanvas 应隐藏");
        }
        finally
        {
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }

    /// <summary>
    /// 人物成长固定入口按钮：Edit Mode 序列化 + 点击打开/关闭流程回归。
    /// 核心入口按钮必须作为场景序列化对象（Edit Mode 可调，长期 UI 规则），
    /// 点击后应打开成长界面，关闭按钮应能关闭。
    /// </summary>
    [UnityTest]
    public IEnumerator TownEntryButton_Click_OpensGrowth_AndCloseButton_Closes()
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        try
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var collection = CallStatic("SaveSystem", "NewGame");
            CallStatic("SaveSystem", "Save", collection);

            var growth = FindInstance("CharacterGrowthUI");
            Assert.IsNotNull(growth, "CharacterGrowthUI 宿主应存在");
            SetMember(growth, "playerData", collection);

            var growthGo = (GameObject)GetMember(growth, "gameObject");
            var growthCanvas = growthGo.transform.Find("GrowthCanvas");
            Assert.IsNotNull(growthCanvas, "GrowthCanvas 应存在");
            Assert.IsFalse(growthCanvas.gameObject.activeSelf, "初始成长界面应隐藏");

            // 固定入口按钮必须 Edit Mode 序列化存在（长期 UI 规则：核心按钮不允许 Runtime 创建）
            var town = FindInstance("TownUI");
            var townCanvas = (GameObject)GetMember(town, "canvasRoot");
            Assert.IsNotNull(townCanvas, "TownCanvas 应存在");
            var btnGrowthGo = townCanvas.transform.Find("BtnCharacterGrowth");
            Assert.IsNotNull(btnGrowthGo, "Town 应有序列化的人物成长入口按钮（Edit Mode 可调）");
            Assert.IsNotNull(btnGrowthGo.Find("RedDot"), "入口按钮应含 RedDot 子对象");
            var btnGrowth = btnGrowthGo.GetComponent<UnityEngine.UI.Button>();
            Assert.IsNotNull(btnGrowth, "BtnCharacterGrowth 应为 Button");

            // 点击入口按钮 → 打开成长界面（回调由代码绑定到既有按钮）
            btnGrowth.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(growthCanvas.gameObject.activeSelf, "点击 Town 入口按钮应打开成长界面");

            // 关闭按钮 → 关闭成长界面
            var btnClose = growthGo.transform.Find("GrowthCanvas/Content/BtnClose");
            Assert.IsNotNull(btnClose, "应存在 BtnClose");
            btnClose.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.IsFalse(growthCanvas.gameObject.activeSelf, "点击关闭按钮后成长界面应隐藏");
        }
        finally
        {
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }

    /// <summary>
    /// 正式固定宿主回归：Scene 加载后、调用 Open 前，CharacterGrowthUI 固定宿主
    /// （Assets/Prefabs/UI/CharacterGrowthUI.prefab 的实例）必须已存在且字段已绑定，
    /// Root/Canvas/Blocker/Panel/BtnClose/ListRoot/DetailRoot 全部 Edit Mode 序列化可见。
    /// 证明生产环境不依赖 BuildCanvas() Runtime 创建正式核心 UI。
    /// </summary>
    [UnityTest]
    public IEnumerator GrowthFixedHost_ExistsInScene_BeforeOpen()
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        try
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            // Scene 加载后、Open 前：固定宿主必须已存在（Prefab 实例）
            var growth = FindInstance("CharacterGrowthUI");
            Assert.IsNotNull(growth, "场景应包含 CharacterGrowthUI 固定宿主（Prefab 实例，Edit Mode 可调）");

            // 宿主字段已绑定（来自 Prefab，而非运行时 BuildCanvas）
            var canvasRoot = (GameObject)GetMember(growth, "canvasRoot");
            Assert.IsNotNull(canvasRoot, "固定宿主 canvasRoot 应已绑定（来自 Prefab）");
            var listRoot = (RectTransform)GetMember(growth, "listRoot");
            Assert.IsNotNull(listRoot, "固定宿主 listRoot 应已绑定");
            var detailRoot = (RectTransform)GetMember(growth, "detailRoot");
            Assert.IsNotNull(detailRoot, "固定宿主 detailRoot 应已绑定");
            var maskDim = (GameObject)GetMember(growth, "maskDim");
            Assert.IsNotNull(maskDim, "固定宿主 maskDim 应已绑定");

            // 固定宿主关键 UI 结构（Root/Canvas/Blocker/Panel/BtnClose/ListRoot/DetailRoot）
            Assert.IsNotNull(canvasRoot.GetComponent<Canvas>(), "canvasRoot 应为 Canvas（GrowthCanvas）");
            Assert.IsNotNull(canvasRoot.transform.Find("Mask_Dim"), "应存在 Modal Blocker Mask_Dim");
            Assert.IsNotNull(canvasRoot.transform.Find("Content"), "应存在 Content");
            Assert.IsNotNull(canvasRoot.transform.Find("Content/Panel_Bg"), "应存在 Panel_Bg");
            Assert.IsNotNull(canvasRoot.transform.Find("Content/BtnClose"), "应存在 BtnClose");
            Assert.IsNotNull(canvasRoot.transform.Find("Content/ListRoot"), "应存在 ListRoot");
            Assert.IsNotNull(canvasRoot.transform.Find("Content/DetailRoot"), "应存在 DetailRoot");

            // Open 前初始隐藏
            Assert.IsFalse(canvasRoot.activeSelf, "Open 前 GrowthCanvas 应隐藏");
        }
        finally
        {
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }
}



