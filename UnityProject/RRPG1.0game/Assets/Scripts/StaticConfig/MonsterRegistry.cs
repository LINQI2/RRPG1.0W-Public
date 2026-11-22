using System;
using System.Collections.Generic;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// Monster Shadow Registry（Pure Runtime）。
    ///
    /// 语义（FDS §13.3）：
    ///   - `All` 顺序 = Database 中的**稳定顺序**（Canonical 按 key 排序的产物顺序，可复现）；
    ///   - `TryGet` 未命中 → false（不抛、不猜、不静默回退到其它怪物）；
    ///   - 重复 ID → 构造期即**明确失败**（不取首个）。
    ///
    /// M1 边界：**产品 Gameplay 不消费它**（Shadow only）；不引用 Editor / AssetDatabase。
    ///
    /// M2 Slice 1：正式查询入口命名为 <see cref="TryGetById"/> / <see cref="GetById"/>；
    /// <see cref="TryGet"/> / <see cref="Get"/> 保留为 M1 契约的纯委托别名（同一实现，不产生第二条数据路径）。
    /// 本类职责仅为**纯 Runtime 数据访问**：不修改数据、不缓存、不做 I/O、不依赖 Unity 场景或 Editor。
    /// </summary>
    public sealed class MonsterRegistry
    {
        public const string DuplicateIdDiagnostic = "Monster 定义出现重复 ID：";
        public const string MissingIdDiagnostic = "Monster 定义不存在该 ID：";

        private readonly List<MonsterDefinition> _all;
        private readonly Dictionary<string, MonsterDefinition> _byId;

        /// <summary>最近一次构造 / 查询的诊断（正常为空）。</summary>
        public string Diagnostic { get; private set; }

        public MonsterRegistry(IEnumerable<MonsterDefinition> definitions)
        {
            _all = new List<MonsterDefinition>();
            _byId = new Dictionary<string, MonsterDefinition>(StringComparer.Ordinal);
            Diagnostic = string.Empty;

            if (definitions == null) return;

            foreach (var definition in definitions)
            {
                if (definition == null) continue;

                string id = definition.MonsterId;
                if (string.IsNullOrEmpty(id)) continue;

                if (_byId.ContainsKey(id))
                {
                    Diagnostic = DuplicateIdDiagnostic + id;
                    throw new InvalidOperationException(Diagnostic);
                }

                _byId.Add(id, definition);
                _all.Add(definition);
            }
        }

        public int Count { get { return _all.Count; } }

        /// <summary>稳定顺序的全部定义（顺序 = Database 顺序）。</summary>
        public IReadOnlyList<MonsterDefinition> All { get { return _all; } }

        /// <summary>
        /// M2 Slice 1 正式查询入口：按 MonsterId 命中（未命中 → false；不猜、不静默回退到其它怪物）。
        /// </summary>
        public bool TryGetById(string monsterId, out MonsterDefinition definition)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                definition = null;
                return false;
            }
            return _byId.TryGetValue(monsterId, out definition);
        }

        /// <summary>未命中返回 null，并把**明确诊断**写入 <see cref="Diagnostic"/>（不静默回退）。</summary>
        public MonsterDefinition GetById(string monsterId)
        {
            MonsterDefinition definition;
            if (TryGetById(monsterId, out definition)) return definition;

            Diagnostic = MissingIdDiagnostic + (monsterId ?? "<null>");
            return null;
        }

        /// <summary>
        /// M1 Shadow 契约的**兼容别名**：与 <see cref="TryGetById"/> 同一实现（纯委托，不构成第二条查询路径）。
        /// 保留原因：M1 冻结证据（MonsterShadowTests S1–S4 / E1–E3）引用这些名字。
        /// </summary>
        public bool TryGet(string monsterId, out MonsterDefinition definition)
        {
            return TryGetById(monsterId, out definition);
        }

        /// <summary>M1 Shadow 契约的**兼容别名**：与 <see cref="GetById"/> 同一实现（纯委托）。</summary>
        public MonsterDefinition Get(string monsterId)
        {
            return GetById(monsterId);
        }

        public bool Contains(string monsterId)
        {
            return !string.IsNullOrEmpty(monsterId) && _byId.ContainsKey(monsterId);
        }
    }
}
