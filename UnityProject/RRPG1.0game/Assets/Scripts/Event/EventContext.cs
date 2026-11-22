using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>
    /// EventContext —— **本次为什么触发事件的轻量事实集合**（WP-Event-1B §3）。
    ///
    /// 三类明确值：
    ///   - String Context（文本上下文，例如 `RecognitionType = TemporaryDraftBuildup`）；
    ///   - Int Context（整数上下文，例如 `ObservedCount = 4`）；
    ///   - Person Context（人物ID上下文，例如 `Observer = person_031`）。
    ///
    /// 硬约束（刻意）：
    ///   - 它**不是**第二套 World State：不得复制全村资源、全 Person 数据或整个 Progression State；
    ///   - 它只描述「这一次为什么触发」；动态状态（Food / Supply / Person Available …）一律通过
    ///     <see cref="IEventWorldView"/> 在求解时读取；
    ///   - Resolver 会把它的**快照**冻结进 <see cref="ResolvedEventInstance"/>（之后调用方再改本对象
    ///     也不能影响已解析实例）。
    ///
    /// 确定性：本类只做键 → 值查询；快照按 Ordinal 排序导出，因此与插入顺序无关。
    /// </summary>
    public sealed class EventContext : IEventContextReader
    {
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _persons = new Dictionary<string, string>(StringComparer.Ordinal);

        public EventContext SetString(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) _strings[key] = value ?? string.Empty;
            return this;
        }

        public EventContext SetInt(string key, int value)
        {
            if (!string.IsNullOrEmpty(key)) _ints[key] = value;
            return this;
        }

        public EventContext SetPerson(string key, string personId)
        {
            if (!string.IsNullOrEmpty(key)) _persons[key] = personId;
            return this;
        }

        public bool TryGetString(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;
            return _strings.TryGetValue(key, out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(key)) return false;
            return _ints.TryGetValue(key, out value);
        }

        public bool TryGetPerson(string key, out string personId)
        {
            personId = null;
            if (string.IsNullOrEmpty(key)) return false;
            return _persons.TryGetValue(key, out personId);
        }

        public int StringCount { get { return _strings.Count; } }
        public int IntCount { get { return _ints.Count; } }
        public int PersonCount { get { return _persons.Count; } }

        /// <summary>
        /// 冻结为**不可变快照**：排序导出（String → Int → Person，各自按键 Ordinal 升序），
        /// 之后本对象的任何修改都不会影响快照（WP-Event-1B §21 / R26）。
        /// </summary>
        public EventContextSnapshot Freeze()
        {
            var entries = new List<EventContextEntry>();

            foreach (var pair in Sorted(_strings))
            {
                entries.Add(new EventContextEntry(EventContextEntryKind.String, pair.Key, pair.Value, 0));
            }
            foreach (var pair in Sorted(_ints))
            {
                entries.Add(new EventContextEntry(EventContextEntryKind.Int, pair.Key, null, pair.Value));
            }
            foreach (var pair in Sorted(_persons))
            {
                entries.Add(new EventContextEntry(EventContextEntryKind.Person, pair.Key, pair.Value, 0));
            }

            return new EventContextSnapshot(entries);
        }

        private static List<KeyValuePair<string, T>> Sorted<T>(Dictionary<string, T> source)
        {
            var list = new List<KeyValuePair<string, T>>(source);
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }
    }

    /// <summary>Context 条目类别。</summary>
    public enum EventContextEntryKind
    {
        String = 0,
        Int = 1,
        Person = 2
    }

    /// <summary>一条冻结的 Context 条目（键 / 值都不可变）。</summary>
    public sealed class EventContextEntry
    {
        public EventContextEntryKind Kind { get; private set; }
        public string Key { get; private set; }

        /// <summary>String / Person 取值（Int 条目为 null）。</summary>
        public string StringValue { get; private set; }

        /// <summary>Int 取值（String / Person 条目为 0）。</summary>
        public int IntValue { get; private set; }

        internal EventContextEntry(EventContextEntryKind kind, string key, string stringValue, int intValue)
        {
            Kind = kind;
            Key = key ?? string.Empty;
            StringValue = stringValue;
            IntValue = intValue;
        }

        public override string ToString()
        {
            if (Kind == EventContextEntryKind.Int) return "int[" + Key + "]=" + IntValue;
            if (Kind == EventContextEntryKind.Person) return "person[" + Key + "]=" + StringValue;
            return "string[" + Key + "]=" + StringValue;
        }
    }
}
