using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Encounter
{
    /// <summary>
    /// Encounter 生成的**输入快照**（WP-Encounter-1B）。
    ///
    /// 为什么要有这一层：
    ///   - 纯契约测试（G1–G18）必须能在**固定测试夹具**上验证算法，不能依赖制作人未来会变化的内容；
    ///   - 因此 Generator 只接受本快照，不直接散读全局单例；
    ///   - 生产路径用 <see cref="FromStaticConfig"/> 从正式只读入口（StaticConfigRegistry）取同一批数据。
    ///
    /// 边界：只读快照。它不修改任何静态配置，也不缓存「生成结果」。
    /// </summary>
    public sealed class EncounterGenerationInput
    {
        public IReadOnlyList<EncounterDefinition> Encounters = new List<EncounterDefinition>();
        public IReadOnlyList<EncounterSlotDefinition> Slots = new List<EncounterSlotDefinition>();
        public IReadOnlyList<EncounterSlotRuleDefinition> SlotRules = new List<EncounterSlotRuleDefinition>();
        public IReadOnlyList<EncounterPoolDefinition> Pools = new List<EncounterPoolDefinition>();
        public IReadOnlyList<EncounterPoolEntryDefinition> PoolEntries = new List<EncounterPoolEntryDefinition>();
        public IReadOnlyList<MonsterEncounterProfileDefinition> MonsterProfiles =
            new List<MonsterEncounterProfileDefinition>();
        public IReadOnlyList<MonsterRoleDefinition> MonsterRoles = new List<MonsterRoleDefinition>();

        /// <summary>正式 Monster 定义（来自 Monster Registry；用于「必须有合法 MonsterDefinition」这一半条件）。</summary>
        public IReadOnlyList<MonsterDefinition> Monsters = new List<MonsterDefinition>();

        /// <summary>
        /// 生产路径：从正式只读链取快照（Encounter 库 + Monster Registry）。
        /// 失败时返回 null 并给出可操作诊断（**不回退**、不猜、不假装成功）。
        /// </summary>
        public static EncounterGenerationInput FromStaticConfig(out string diagnostic)
        {
            EncounterConfigDatabase encounters;
            string encounterDiagnostic;
            if (!StaticConfigRegistry.TryGetEncounterConfig(out encounters, out encounterDiagnostic))
            {
                diagnostic = "Encounter 静态配置不可用：" + encounterDiagnostic;
                return null;
            }

            MonsterRegistry monsters;
            string monsterDiagnostic;
            if (!StaticConfigRegistry.TryGetMonsters(out monsters, out monsterDiagnostic))
            {
                diagnostic = "Monster 静态配置不可用（无法判定合法怪物集合）：" + monsterDiagnostic;
                return null;
            }

            diagnostic = string.Empty;
            return new EncounterGenerationInput
            {
                Encounters = encounters.Encounters,
                Slots = encounters.Slots,
                SlotRules = encounters.SlotRules,
                Pools = encounters.Pools,
                PoolEntries = encounters.PoolEntries,
                MonsterProfiles = encounters.MonsterProfiles,
                MonsterRoles = encounters.MonsterRoles,
                Monsters = monsters.All
            };
        }
    }
}
