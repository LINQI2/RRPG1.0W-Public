using System;

/// <summary>
/// Initial Person &amp; Profession Bootstrap —— 固定初始人物的静态配置定义（PersonDefinition）。
///
/// 一条 PersonDefinition = 一个固定初始人物定义（配置身份），由 New Game 实例化为一个具体 Runtime Person。
///
/// 身份红线（与审计结论一致）：
///   - PersonDefinitionId 是**静态配置身份**，不是运行身份；New Game 创建的 Person 的 PersonId 仍由
///     PersonIdentity.NewPersonId() 独立生成。禁止 PersonId = PersonDefinitionId。
///   - DisplayName 是玩家表现名称；禁止作为程序 Authority / Key。
///   - CharacterId（战斗模板）不得重新承担 Person Identity；本类只通过 CombatDefinitionId 引用战斗定义。
///
/// 职责边界：
///   本类只负责「这个固定初始人物是谁 + 开局带什么 Profession + 引用哪些既有定义」；
///   不承载 Combat 数值（引用 CombatDefinitionId）、不承载 Profession 模板内容（引用 InitialProfession.ProfessionId）、
///   不承载卡册结构（引用 CardDefinitionId），也不承载运行时成长状态。
/// </summary>
[Serializable]
public class PersonDefinition
{
    /// <summary>静态配置身份（唯一）。不得用作 Runtime PersonId，也不得与 PersonId 取同值。</summary>
    public string PersonDefinitionId;

    /// <summary>人物名字（玩家识别人靠人物本身/名字；职业不得作为人物显示身份）。</summary>
    public string DisplayName;

    /// <summary>
    /// 战斗定义引用（当前 = CharacterData.CharacterId 语义：HP / ATK / 射程 / 攻击次数 / AP / 阵营 / 初始技能）。
    /// 只作战斗定义引用，不承担人物身份。
    /// </summary>
    public string CombatDefinitionId;

    /// <summary>
    /// New Game 初始角色卡定义引用（CharacterCardData.CardId）；可空。
    /// 为空时 New Game 为该人物生成运行时角色卡（CardId 仍与 PersonId 显式绑定）。
    /// </summary>
    public string CardDefinitionId;

    /// <summary>
    /// 开局已有 Profession 配置；null = 该人物开局无职业（当前固定初始名册不使用，为未来随机无职业 Person
    /// → M6 Formation 保留的合法配置分支）。
    /// </summary>
    public InitialProfessionConfig InitialProfession;

    /// <summary>
    /// 初始名册顺序：&gt; 0 = 属于当前 New Game 初始名册；按升序创建（配置 N 行就有 N 个开局人物）。
    /// &lt;= 0 = 不进初始名册（例如未来由其它入口创建的人物）。代码不得写死初始人数或固定名单。
    /// </summary>
    public int InitialRosterOrder;

    /// <summary>是否属于 New Game 初始名册。</summary>
    public bool IsInInitialRoster => InitialRosterOrder > 0;
}
