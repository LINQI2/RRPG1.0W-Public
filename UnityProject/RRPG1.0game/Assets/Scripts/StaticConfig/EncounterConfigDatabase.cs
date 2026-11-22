using System.Collections.Generic;
using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 生成容器：Encounter 领域（遭遇生成静态配置基础）的只读静态配置库（WP-Encounter-1A）。
    ///
    /// 承载由 Canonical Mirror 经 Unity Builder 生成的 7 张表的记录
    /// （Encounter / EncounterSlot / EncounterSlotRule / EncounterPool / EncounterPoolEntry /
    ///   MonsterEncounterProfile / MonsterRole）—— **生成物**，不是人工 Authoring 对象。
    ///
    /// 为什么 7 张表放进同一个库：
    ///   - 它们的**唯一消费者**是同一件事（遭遇生成），且跨表引用密集（Slot→Encounter、Rule→Slot、
    ///     Entry→Pool/Encounter、Profile/Role→Monster）；放同一个库里，Runtime 不会读到
    ///     「一半新一半旧」的跨库不一致；
    ///   - 领域身份仍与工作簿一致：`RRPG_Encounters.xlsx` 的 5 张表 + Monster 工作簿的 2 张子表；
    ///   - 不修改 `MonsterConfigDatabase` 的既有 8 字段契约，也不建立第二套 Monster Authority。
    ///
    /// 语义（与 MonsterRegistry / EnemyPresentationDatabase 同一套约定）：
    ///   - 每个列表的顺序 = Mirror 的 Canonical 顺序（按身份排序的产物顺序，可复现）；
    ///   - 本类**只提供只读访问**：不查询、不解释、不生成敌人（生成属后续技术检查点 1B）。
    ///
    /// 注意：Unity 要求 ScriptableObject 与其资产脚本**同名文件**（本文件即该约定）。
    /// </summary>
    public sealed class EncounterConfigDatabase : ScriptableObject
    {
        [SerializeField] private string formatVersion = RRPGStaticConfigRoot.SupportedFormatVersion;
        [SerializeField] private string sourceMirrorSha256 = string.Empty;
        [SerializeField] private string monsterProfileSha256 = string.Empty;
        [SerializeField] private string monsterRoleSha256 = string.Empty;

        [SerializeField] private List<EncounterDefinition> encounters = new List<EncounterDefinition>();
        [SerializeField] private List<EncounterSlotDefinition> slots = new List<EncounterSlotDefinition>();
        [SerializeField] private List<EncounterSlotRuleDefinition> slotRules = new List<EncounterSlotRuleDefinition>();
        [SerializeField] private List<EncounterPoolDefinition> pools = new List<EncounterPoolDefinition>();
        [SerializeField] private List<EncounterPoolEntryDefinition> poolEntries = new List<EncounterPoolEntryDefinition>();
        [SerializeField] private List<MonsterEncounterProfileDefinition> monsterProfiles =
            new List<MonsterEncounterProfileDefinition>();
        [SerializeField] private List<MonsterRoleDefinition> monsterRoles = new List<MonsterRoleDefinition>();

        public string FormatVersion { get { return formatVersion; } }

        /// <summary>来源 Mirror 的语义指纹（Encounter.json 的 <c>_generated.sourceSha256</c>）。</summary>
        public string SourceMirrorSha256 { get { return sourceMirrorSha256; } }

        /// <summary>Monster 工作簿两张子表的来源指纹（用于溯源：它们与 Encounter 表不同文件）。</summary>
        public string MonsterProfileSha256 { get { return monsterProfileSha256; } }

        public string MonsterRoleSha256 { get { return monsterRoleSha256; } }

        public int EncounterCount { get { return encounters == null ? 0 : encounters.Count; } }
        public int SlotCount { get { return slots == null ? 0 : slots.Count; } }
        public int SlotRuleCount { get { return slotRules == null ? 0 : slotRules.Count; } }
        public int PoolCount { get { return pools == null ? 0 : pools.Count; } }
        public int PoolEntryCount { get { return poolEntries == null ? 0 : poolEntries.Count; } }
        public int MonsterProfileCount { get { return monsterProfiles == null ? 0 : monsterProfiles.Count; } }
        public int MonsterRoleCount { get { return monsterRoles == null ? 0 : monsterRoles.Count; } }

        /// <summary>全部记录条数（用于 no-op 判定与诊断，不参与生成）。</summary>
        public int TotalCount
        {
            get
            {
                return EncounterCount + SlotCount + SlotRuleCount + PoolCount
                    + PoolEntryCount + MonsterProfileCount + MonsterRoleCount;
            }
        }

        /// <summary>稳定顺序的全部遭遇模板（顺序 = Mirror 的 Canonical 顺序）。</summary>
        public IReadOnlyList<EncounterDefinition> Encounters { get { return List(encounters); } }

        public IReadOnlyList<EncounterSlotDefinition> Slots { get { return List(slots); } }

        public IReadOnlyList<EncounterSlotRuleDefinition> SlotRules { get { return List(slotRules); } }

        public IReadOnlyList<EncounterPoolDefinition> Pools { get { return List(pools); } }

        public IReadOnlyList<EncounterPoolEntryDefinition> PoolEntries { get { return List(poolEntries); } }

        public IReadOnlyList<MonsterEncounterProfileDefinition> MonsterProfiles { get { return List(monsterProfiles); } }

        public IReadOnlyList<MonsterRoleDefinition> MonsterRoles { get { return List(monsterRoles); } }

        /// <summary>Builder 唯一写入口（Editor-only 调用；Runtime 只读）。</summary>
        public void ApplyGeneratedContent(
            string format,
            string encounterSha256, string monsterProfileMirrorSha256, string monsterRoleMirrorSha256,
            List<EncounterDefinition> encounterEntries,
            List<EncounterSlotDefinition> slotEntries,
            List<EncounterSlotRuleDefinition> slotRuleEntries,
            List<EncounterPoolDefinition> poolDefinitions,
            List<EncounterPoolEntryDefinition> poolEntryDefinitions,
            List<MonsterEncounterProfileDefinition> profileEntries,
            List<MonsterRoleDefinition> roleEntries)
        {
            formatVersion = format;
            sourceMirrorSha256 = encounterSha256 ?? string.Empty;
            monsterProfileSha256 = monsterProfileMirrorSha256 ?? string.Empty;
            monsterRoleSha256 = monsterRoleMirrorSha256 ?? string.Empty;

            encounters = encounterEntries ?? new List<EncounterDefinition>();
            slots = slotEntries ?? new List<EncounterSlotDefinition>();
            slotRules = slotRuleEntries ?? new List<EncounterSlotRuleDefinition>();
            pools = poolDefinitions ?? new List<EncounterPoolDefinition>();
            poolEntries = poolEntryDefinitions ?? new List<EncounterPoolEntryDefinition>();
            monsterProfiles = profileEntries ?? new List<MonsterEncounterProfileDefinition>();
            monsterRoles = roleEntries ?? new List<MonsterRoleDefinition>();
        }

        private static IReadOnlyList<T> List<T>(List<T> source)
        {
            return source ?? (IReadOnlyList<T>)new List<T>();
        }

    }
}
