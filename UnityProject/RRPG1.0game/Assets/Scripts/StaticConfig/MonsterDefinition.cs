using System;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// Monster 纯静态定义（Shadow 链 Runtime 侧的只读数据结构）。
    ///
    /// M1 边界（用户 M1 §8）：**只表达当前 8 字段**，不增加 Sprite / AI / 行为 / 掉落 / 技能等未来字段。
    /// 它是生成物（由 Mirror → Builder → MonsterConfigDatabase 而来），不是人工 Authoring 对象。
    /// **Current Monster Definition Authority**（M2 起）：产品链经 `StaticConfigRegistry` → `MonsterRegistry`
    /// 读取本类型；旧 `Monster.csv` / Monster SO / `DataManager` Monster 路径已于 **M4 物理删除**。
    /// </summary>
    [Serializable]
    public sealed class MonsterDefinition
    {
        public string MonsterId;
        public string Name;
        public int MaxHP;
        public int Attack;
        public int AttackRange;
        public int AttackCount;
        public int ActionPoints;
        public string Faction;

        /// <summary>字段数常量（与 Mirror 的 8 列契约一致；用于校验）。</summary>
        public const int FieldCount = 8;

        public override string ToString()
        {
            return MonsterId + "(" + Name + ") HP=" + MaxHP + " ATK=" + Attack + " RNG=" + AttackRange
                + " CNT=" + AttackCount + " AP=" + ActionPoints + " FACTION=" + Faction;
        }
    }
}
