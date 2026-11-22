using System.Collections.Generic;

/// <summary>
/// M6-A Formation Host —— Formation 可作用的「普通 Person」窄接口。
///
/// 实现者：CharacterInstance（具体普通 Person；唯一正式实现）。
/// Hero 不实现本接口、不拥有 FormationState / LifeHistory（Hero Guard 测试拦截），
/// 因此 Formation 流程在类型层面就不可能作用到 Hero。
///
/// 职责边界：
///   - 本接口只暴露 Formation 写入所需的 Person-owned 数据面；
///   - FormationState（派生状态）与 PersonLifeHistory（事实）保持分离；
///   - StarterSlots 为 set（Formation Commit 用整 List 深拷贝做原子替换，
///     绝不合并 Legacy / Test Starter，也不采用「仅为空才初始化」）。
/// </summary>
public interface IFormationHost
{
    /// <summary>具体人物稳定实例身份（M1 PersonId；不允许用 CharacterId / HeroId 兜底）。</summary>
    string PersonId { get; }

    /// <summary>当前是否有 Active Profession（= ActiveProfessionProgress != null）。</summary>
    bool HasActiveProfession { get; }

    /// <summary>Formation 算法派生状态（Person-owned；空状态合法，恒非 null）。</summary>
    FormationState FormationState { get; }

    /// <summary>Person 人生事实 Source of Record（M6 只记录 ProfessionFormed）。</summary>
    PersonLifeHistory LifeHistory { get; }

    /// <summary>人物长期 Starter 槽（Formation Commit 时整 List 原子替换为模板 InitialStarterBuild 深拷贝）。</summary>
    List<StarterSlotEntry> StarterSlots { get; set; }

    /// <summary>当前 Active Profession 进度（null = 尚未正式形成 Profession；Formation Commit 原子创建）。</summary>
    ProfessionProgress ActiveProfessionProgress { get; set; }
}
