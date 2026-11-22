using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>
    /// EventContext 的**不可变冻结快照**（WP-Event-1B §21）。
    ///
    /// 为什么需要它：解析成功后调用方可能继续修改自己的 Context 对象；已解析实例必须保持
    /// 「当时为什么触发」的事实不变（否则 Trace / 未来 1C 的选项条件会看到被污染的历史）。
    ///
    /// 只冻结 Context：当前 Food / Supply / Person Available 等**动态 World State 不冻结**
    /// —— 1C 的 OptionCondition 必须在玩家点击时重新读取当时的 World State。
    /// </summary>
    public sealed class EventContextSnapshot : IEventContextReader
    {
        private readonly List<EventContextEntry> _entries;
        private readonly Dictionary<string, EventContextEntry> _byKey;

        internal EventContextSnapshot(List<EventContextEntry> entries)
        {
            _entries = entries ?? new List<EventContextEntry>();
            _byKey = new Dictionary<string, EventContextEntry>(StringComparer.Ordinal);

            foreach (var entry in _entries)
            {
                // 三类键各自唯一；同名不同类时以「String → Int → Person」中先出现者为准（确定性）
                string composite = ((int)entry.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "\u001f" + entry.Key;
                if (!_byKey.ContainsKey(composite)) _byKey.Add(composite, entry);
            }
        }

        /// <summary>全部条目（顺序 = 冻结时的规范顺序：String → Int → Person，各按键升序）。</summary>
        public IReadOnlyList<EventContextEntry> Entries { get { return _entries; } }

        public int Count { get { return _entries.Count; } }

        public bool TryGetEntry(EventContextEntryKind kind, string key, out EventContextEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(key)) return false;

            string composite = ((int)kind).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\u001f" + key;
            return _byKey.TryGetValue(composite, out entry);
        }

        public bool TryGetString(string key, out string value)
        {
            EventContextEntry entry;
            if (TryGetEntry(EventContextEntryKind.String, key, out entry))
            {
                value = entry.StringValue;
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGetInt(string key, out int value)
        {
            EventContextEntry entry;
            if (TryGetEntry(EventContextEntryKind.Int, key, out entry))
            {
                value = entry.IntValue;
                return true;
            }
            value = 0;
            return false;
        }

        public bool TryGetPerson(string key, out string personId)
        {
            EventContextEntry entry;
            if (TryGetEntry(EventContextEntryKind.Person, key, out entry))
            {
                personId = entry.StringValue;
                return true;
            }
            personId = null;
            return false;
        }

        /// <summary>诊断 / Trace 用文本（空快照 → "(empty)"）。</summary>
        public string ToText()
        {
            if (_entries.Count == 0) return "(empty)";

            var parts = new List<string>();
            foreach (var entry in _entries) parts.Add(entry.ToString());
            return string.Join(", ", parts);
        }

        public override string ToString()
        {
            return ToText();
        }
    }
}
