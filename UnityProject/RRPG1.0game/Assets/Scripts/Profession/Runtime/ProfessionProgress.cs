using System;
using System.Collections.Generic;

/// <summary>
/// M3 Ordinary Person Profession Progress — 普通 Person 自己的 Active Profession 进度容器。
///
/// 与 ProfessionDefinition（M2，Profession 共享静态模板）的关系：
///   ProfessionDefinition 描述“某种 Profession 是什么”：ProfessionId / FullCardPool / InitialUnlockedCards，全职业共享；
///   ProfessionProgress   承载某个具体 Person 在此 Profession 内的动态成长（Person-owned，Person 间完全独立）。
///
/// 关键语义（M3 边界）：
///   1) null 合法：普通 Person 在正式形成 Profession 前，宿主上的 ActiveProfessionProgress == null 是合法状态，不是异常。
///      本容器在 Profession 形成时才被创建（形成逻辑属未来 M 阶段，不在 M3），形成即 ProfessionLevel = 1 / ProfessionExp = 0。
///   2) ProfessionLevel / ProfessionExp 只表达该 Person 在此 Profession 的战斗侧成长深度；
///      不是工龄、不是工作熟练度、不是社会身份、不是 Formation 程度、不是 Assignment 时长。
///   3) 同 Profession 的多个 Person 只共享 ProfessionDefinition；每个 Person 独立持有自己的
///      ProfessionProgress（ProfessionLevel / ProfessionExp / UnlockedPoolCardIds / ProfessionTraitLevel）。
///      任何一个 Person 的成长不得影响同 Profession 的其他 Person，也不得写回 ProfessionDefinition。
///   4) M3 只允许 0 或 1 个 Active ProfessionProgress：不实现多职业 / Dormant 列表 / 转职切换 / 重新激活。
///   5) ProfessionTraitLevel 只是具体 Person 的职业特性强化等级（Person-owned 进度槽）；
///      当前不把 Trait Definition（定义/配置）塞进本类（落点后续再审）。
///   6) 旧档没有本数据时宿主保持 null；绝不根据 CharacterId / Assignment / 旧 Level/Exp 自动推断 ProfessionId，也不自动 Formation。
///
/// 存档：五字段与 Save DTO（ProfessionProgressSaveData，SaveData.cs）一一镜像，由 SaveSystem 负责构建 / 恢复。
/// </summary>
[Serializable]
public class ProfessionProgress
{
    /// <summary>
    /// 当前 Profession 模板 ID（引用 ProfessionDefinition.ProfessionId 语义；不创造第二套职业身份）。
    /// 形成前宿主为 null，本类实例只在已形成/已初始化时存在。
    /// </summary>
    public string ProfessionId;

    /// <summary>
    /// 该 Person 在此 Profession 的等级。正式形成 Profession 时起始为 1（形成前不存在本容器）。
    /// 只表达战斗侧成长深度；不表达工龄 / 熟练度 / 社会身份 / Formation / Assignment。
    /// </summary>
    public int ProfessionLevel;

    /// <summary>该 Person 在此 Profession 的当前经验（正式形成时起始为 0）。</summary>
    public int ProfessionExp;

    /// <summary>
    /// 该 Person 自己的 Unlocked Pool：当前已解锁、允许被随机获得的角色牌模板 ID 集合。
    /// 与模板 InitialUnlockedCards / FullCardPool 克隆分离，绝不共享 List 引用（Person 独立成长的前提）。
    /// </summary>
    public List<string> UnlockedPoolCardIds = new List<string>();

    /// <summary>
    /// 该 Person 的 Profession Trait 强化等级（Person-owned 进度槽）。
    /// 当前 Active Profession 切换时 Trait 跟 Profession 同步切换 / 旧 Profession 休眠时一起休眠 —— 均属未来 M 阶段，不在 M3。
    /// </summary>
    public int ProfessionTraitLevel;
}
