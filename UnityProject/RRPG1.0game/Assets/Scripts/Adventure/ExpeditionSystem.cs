using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 远征准备系统
///
/// 负责：管理远征准备的完整流程
///   1. 选英雄（1个，可选）
///   2. 选角色卡（上限6）
///   3. 确认出征 → 进入战斗
/// 不负责：战斗流程、地图探索
///
/// Battle V0.1 / WP-C：出征不再选择独立道具卡（不存在 Item Card / Item Selection 产品语义）；
/// 远征准备完成后生成角色队伍与英雄信息，供远征推进与 Battle 使用。
/// </summary>
public class ExpeditionSystem
{
    /// <summary>角色卡选择上限</summary>
    public const int MaxCharacterCards = 6;

    /// <summary>玩家全局数据</summary>
    public PlayerCollection PlayerData { get; private set; }

    /// <summary>当前选中的英雄实例</summary>
    public HeroInstance SelectedHero { get; private set; }

    /// <summary>当前选中的英雄配置数据</summary>
    public HeroData SelectedHeroData { get; private set; }

    /// <summary>当前选中的角色卡列表</summary>
    public List<CharacterCardData> SelectedCharacters { get; private set; }

    /// <summary>
    /// 是否已完成选择（可出征）
    /// 条件：至少选了1个角色卡
    /// （Current GameSpec R4.1：英雄属于正常角色单位，可以上阵也可以不上阵，不再要求必须携带英雄）
    /// </summary>
    public bool IsReady
    {
        get
        {
            return SelectedCharacters.Count > 0;
        }
    }

    /// <summary>
    /// 创建远征准备系统
    /// 英雄初始为空，需调用 SelectHero 选择
    /// </summary>
    /// <param name="playerData">玩家全局数据</param>
    public ExpeditionSystem(PlayerCollection playerData)
    {
        PlayerData = playerData;
        SelectedCharacters = new List<CharacterCardData>();
    }

    /// <summary>
    /// 选择英雄
    /// 由 ExpeditionUI 或外部在进入远征时调用
    /// </summary>
    public void SelectHero(HeroInstance heroInstance, HeroData heroData)
    {
        SelectedHero = heroInstance;
        SelectedHeroData = heroData;
    }

    /// <summary>
    /// 添加角色卡到出征列表
    /// </summary>
    /// <param name="card">角色卡数据</param>
    /// <returns>true=添加成功</returns>
    public bool AddCharacterCard(CharacterCardData card)
    {
        if (card == null)
            return false;

        if (SelectedCharacters.Count >= MaxCharacterCards)
        {
            return false;
        }

        if (SelectedCharacters.Exists(c => c.CardId == card.CardId))
        {
            return false;
        }

        SelectedCharacters.Add(card);
        return true;
    }

    /// <summary>
    /// 从出征列表移除角色卡
    /// </summary>
    /// <param name="card">角色卡数据</param>
    public void RemoveCharacterCard(CharacterCardData card)
    {
        SelectedCharacters.Remove(card);
    }

    /// <summary>
    /// 确认出征
    /// 生成 Deck 并返回战斗所需的参数
    /// </summary>
    /// <param name="playerCharacters">输出：玩家角色卡列表（供 BattleSystem.InitializeBattle）</param>
    /// <param name="playerHero">输出：英雄实例（可为null）</param>
    /// <returns>true=确认成功</returns>
    public bool ConfirmExpedition(
        out List<CharacterCardData> playerCharacters,
        out HeroInstance playerHero)
    {
        playerCharacters = null;
        playerHero = null;

        if (!IsReady)
        {
            Debug.LogWarning("[ExpeditionSystem] 条件不满足，无法出征：需要至少选择 1 个角色卡（英雄可选）");
            return false;
        }

        // V2 Final Closure：Expedition 选人必须读取统一 Temporary Occupancy。
        // 同一 Person 不得同时处于两个 Temporary Occupancy（Active Project / Expedition / Unavailable）。
        string blockedReason = GetFirstOccupiedPersonReason();
        if (!string.IsNullOrEmpty(blockedReason))
        {
            Debug.LogWarning("[ExpeditionSystem] 无法出征：" + blockedReason + "。请先解除该占用（例如等待 Active Project 完成）。");
            return false;
        }

        playerCharacters = new List<CharacterCardData>(SelectedCharacters);
        playerHero = SelectedHero;

        return true;
    }

    /// <summary>
    /// 在已选出征人选中查找统一占用 Person；无则返回空串。
    /// 读取 VillageRuntimeBridge（唯一 Temporary Occupancy 真相源），不复制第二套状态。
    /// </summary>
    private string GetFirstOccupiedPersonReason()
    {
        // 角色卡 → 具体长期 Person（M1：按卡显式绑定解析 PersonId，同模板多实例不互相占用；
        // Foundation Identity Cleanup：无显式绑定（即使模板候选唯一）时不把模板 ID 当 Person，跳过占用判定）
        if (SelectedCharacters != null)
        {
            foreach (var card in SelectedCharacters)
            {
                if (card == null || string.IsNullOrEmpty(card.CharacterId)) continue;
                string pid = null;
                if (PlayerData != null) pid = PlayerData.ResolvePersonIdForCard(card);
                if (string.IsNullOrEmpty(pid)) continue; // 无法确定具体人 → 不占用/不静默模板 ID
                string reason = VillageRuntimeBridge.GetDepartureOccupancyReason(pid);
                if (!string.IsNullOrEmpty(reason)) return reason;
            }
        }
        // 英雄（若被选中）也是长期 Person（M1：用实例 PersonId）。
        // Foundation Identity Cleanup：空 PersonId 的 Hero 不进入 Village occupancy（不得用模板 HeroId）。
        if (SelectedHero != null)
        {
            string heroPid = SelectedHero.PersonId;
            if (string.IsNullOrEmpty(heroPid))
            {
                Debug.LogWarning("[ExpeditionSystem] 所选英雄（HeroId=" + SelectedHero.HeroId
                    + "）缺少真实 PersonId，跳过 Village occupancy 检查（不用模板 HeroId 当 Person）。");
                return "";
            }
            string heroReason = VillageRuntimeBridge.GetDepartureOccupancyReason(heroPid);
            if (!string.IsNullOrEmpty(heroReason)) return heroReason;
        }
        return "";
    }

    /// <summary>
    /// 重置所有选择（回到初始状态）
    /// </summary>
    public void ResetSelection()
    {
        SelectedHero = null;
        SelectedHeroData = null;
        SelectedCharacters.Clear();
    }
}