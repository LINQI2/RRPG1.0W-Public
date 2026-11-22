using System;
using System.Collections.Generic;

/// <summary>
/// M2 Profession Template Foundation（M6-B 扩展 InitialStarterBuild / ProfessionTraitId）——
/// 单一 Profession 模板的共享静态定义。
///
/// 只描述"某种 Profession 模板是什么"，由五个字段构成：
///   ProfessionId          模板唯一标识（如测试内容 profession_test_a；正式职业生态未冻结）
///   FullCardPool          此 Profession 理论上允许出现的完整角色牌模板集合（共享模板定义）
///   InitialUnlockedCards  某个 Person 将来正式获得该 Profession 时，可作为初始解锁状态来源的模板集合；
///                         必须满足 InitialUnlockedCards ⊆ FullCardPool（validation 强制）
///   InitialStarterBuild   首次 Profession Formation 时固定替换到 Person StarterSlots 的初始构建
///                         （§12.15 Authority：属 Profession Template 的共享初始配置）
///   ProfessionTraitId     该 Profession 固定初始 Profession Trait 定义 ID（§12.15 Authority：
///                         Trait Definition 属 Profession Template；Person 只持有 Trait 强化等级）
///
/// 语义红线（M2 最关键的边界）：
///   本类绝不承载任何具体 Person 的动态成长状态。
///   Person.UnlockedPoolCardIds = 具体人物自己的动态成长集合，属于 M3，不得放入本类，
///   也不得让所有相同 Profession 的人物共享一份动态解锁进度。
///
/// CardId 语义：
///   集合内每个元素引用当前项目已有卡牌模板 ID（BattleCardData.CardId 的字符串语义，
///   与 IPersonGrowth.UnlockedPoolCardIds / StarterSlotEntry.CardId 同源），
///   不创造第二套卡牌身份；本类不依赖任何具体 Person。
///
/// 内容冻结状态：
///   本结构为 Foundation 层。正式职业内容 / 正式卡数量 / 正式强度均未冻结；
///   测试内容只允许出现在 M2 专项测试中，不得写回 GameSpec 作为正式职业内容。
/// </summary>
[Serializable]
public class ProfessionDefinition
{
    /// <summary>职业模板唯一 ID（当前仅 Test/Not Frozen 内容；正式职业生态未冻结）</summary>
    public string ProfessionId;

    /// <summary>
    /// 此 Profession 理论上允许出现的完整角色牌模板集合（Profession 共享模板定义，非人物动态状态）。
    /// 不允许出现空 CardId；同 CardId 不允许重复。
    /// </summary>
    public List<string> FullCardPool = new List<string>();

    /// <summary>
    /// Person 正式获得该 Profession 时可作为初始解锁状态来源的模板集合。
    /// 不允许出现空 CardId；不允许重复；必须是 FullCardPool 的子集（validation 强制）。
    /// </summary>
    public List<string> InitialUnlockedCards = new List<string>();

    /// <summary>
    /// 首次 Profession Formation 时固定替换到 Person StarterSlots 的初始构建（§12.15）。
    /// 条目复用 StarterSlotEntry（CardId + Proficiency）—— 与人物长期 Starter 同一类型；
    /// Formation Commit 会整体深拷贝后原子替换，绝不与模板共享 List / 条目引用。
    /// 允许空构建（合法 = 起始无固定角色牌）；不允许 null 列表 / 空 CardId / 负 Proficiency。
    /// </summary>
    public List<StarterSlotEntry> InitialStarterBuild = new List<StarterSlotEntry>();

    /// <summary>
    /// 该 Profession 固定初始 Profession Trait 定义 ID（§12.15 Authority：Trait Definition 属
    /// Profession Template 共享初始配置，Person 只持有 ProfessionTraitLevel 等动态状态）。
    /// 不允许 null/empty（本字段是否引用到实际 TraitData 资产由内容管线负责，不在本 Validator 冻结）。
    /// </summary>
    public string ProfessionTraitId;
}

