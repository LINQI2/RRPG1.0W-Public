using System;

/// <summary>
/// PersonId 生成与规范（M1：人物实例唯一稳定身份）。
///
/// 语义：
///   CharacterId / HeroId = 静态模板 ID；
///   PersonId            = 世界中某个具体人物的稳定唯一实例身份。
///
/// 规则：
///   - 非空、全局足够唯一；
///   - 一个具体人物创建后不变化；
///   - Save / Load 后保持一致；
///   - 同一模板的两个实例必须拥有不同 PersonId；
///   - 不依赖 DisplayName / 岗位 / CardId / 列表索引。
///
/// 所有新人物实例的 PersonId 必须经本类生成（生成逻辑集中，不允许各处随意 Guid.NewGuid）。
/// </summary>
public static class PersonIdentity
{
    /// <summary>
    /// 生成一个新的稳定唯一 PersonId。
    /// 格式：person_&lt;Guid:N&gt;（技术格式不属于游戏设计，不要求固定字符串格式）。
    /// </summary>
    public static string NewPersonId()
    {
        return "person_" + Guid.NewGuid().ToString("N");
    }

    /// <summary>判断字符串是否可用于 PersonId 查询（空串视为无效）。</summary>
    public static bool IsValid(string personId)
    {
        return !string.IsNullOrEmpty(personId);
    }
}
