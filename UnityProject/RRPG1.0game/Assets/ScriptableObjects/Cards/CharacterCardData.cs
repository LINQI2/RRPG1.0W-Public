using UnityEngine;

/// <summary>
/// 角色卡配置数据（ScriptableObject）
///
/// 负责：存放角色卡静态配置
/// 角色卡是角色实体的映射
/// 角色进入战斗后转换为 BattleUnit
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterCardData", menuName = "RRPG/角色卡配置")]
public class CharacterCardData : ScriptableObject
{
    /// <summary>
    /// 卡牌ID
    /// 唯一标识此卡牌
    /// </summary>
    [Header("基础信息")]
    public string CardId;

    /// <summary>
    /// 绑定角色ID
    /// 引用 CharacterData.CharacterId
    /// </summary>
    public string CharacterId;

    /// <summary>
    /// 该角色卡在静态配置上代表哪个固定初始人物定义（引用 PersonDefinition.PersonDefinitionId）。
    /// 只表达「卡 → 人物定义」的配置链接；具体人物身份仍由运行时 CardId → PersonId 显式绑定表达。
    /// 为空 = Legacy 卡（迁移前创建的卡/旧档运行时卡），不猜人物定义。
    /// </summary>
    public string PersonDefinitionId;
}
