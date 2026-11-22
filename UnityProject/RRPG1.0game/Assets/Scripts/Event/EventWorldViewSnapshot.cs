using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>快照里的一名人物（纯数据；只保留求解需要的布尔事实）。</summary>
    public sealed class EventWorldPersonSnapshot
    {
        public string PersonId { get; internal set; }
        public bool InVillage { get; internal set; }
        public bool OnExpedition { get; internal set; }
        public bool Available { get; internal set; }

        public override string ToString()
        {
            return PersonId + "(village=" + InVillage + ", expedition=" + OnExpedition
                + ", available=" + Available + ")";
        }
    }

    /// <summary>
    /// <see cref="IEventWorldView"/> 的**纯数据快照实现**（测试 Fixture 与 1D Adapter 都可使用）。
    ///
    /// 定位：
    ///   - 它是「一次求解时的世界事实冻结」——不是新的世界 Authority；
    ///   - 生产使用时由 1D Adapter 在触发点采集（从正式 Village / Expedition Authority 只读读取），
    ///     解析过程结束后即丢弃（**不进 Save**）；
    ///   - 1B 只用它构造 Fixture。
    /// </summary>
    public sealed class EventWorldViewSnapshot : IEventWorldView
    {
        private readonly Dictionary<string, EventWorldPersonSnapshot> _persons =
            new Dictionary<string, EventWorldPersonSnapshot>(StringComparer.Ordinal);
        private readonly List<string> _villagePersons = new List<string>();
        private readonly List<string> _party = new List<string>();

        public int VillageDayOrdinal { get; private set; }
        public int Ingredients { get; private set; }
        public int Food { get; private set; }
        public int ExpeditionDepth { get; private set; }
        public int ExpeditionSupply { get; private set; }
        public int ExpeditionPartySize { get; private set; }
        public int ExpeditionNodeOrdinal { get; private set; }

        public IReadOnlyList<string> VillagePersonIds { get { return _villagePersons; } }

        public IReadOnlyList<string> ExpeditionPartyPersonIds { get { return _party; } }

        public IReadOnlyList<EventWorldPersonSnapshot> Persons
        {
            get
            {
                var list = new List<EventWorldPersonSnapshot>(_persons.Values);
                list.Sort((a, b) => string.CompareOrdinal(a.PersonId, b.PersonId));
                return list;
            }
        }

        // ---------------- Fixture / Adapter 构造入口 ----------------

        /// <summary>
        /// 登记一名人物的事实；`inVillage` / `onExpedition` 会自动同步候选池
        /// （保持「在村 → 村庄候选池」「在远征 → 队伍候选池」的一致性，避免 Fixture 自相矛盾）。
        /// </summary>
        public EventWorldViewSnapshot AddPerson(string personId, bool inVillage = false,
            bool onExpedition = false, bool available = true)
        {
            if (string.IsNullOrEmpty(personId)) return this;

            if (!_persons.ContainsKey(personId))
            {
                _persons.Add(personId, new EventWorldPersonSnapshot { PersonId = personId });
            }

            var person = _persons[personId];
            person.InVillage = inVillage;
            person.OnExpedition = onExpedition;
            person.Available = available;

            if (inVillage && !_villagePersons.Contains(personId)) _villagePersons.Add(personId);
            if (!inVillage) _villagePersons.Remove(personId);

            if (onExpedition && !_party.Contains(personId)) _party.Add(personId);
            if (!onExpedition) _party.Remove(personId);

            return this;
        }

        /// <summary>显式加入村庄候选池（不改变 Person 状态；用于表达「候选但状态另算」的边界用例）。</summary>
        public EventWorldViewSnapshot AddVillagePersonCandidate(string personId)
        {
            if (!string.IsNullOrEmpty(personId) && !_villagePersons.Contains(personId)) _villagePersons.Add(personId);
            return this;
        }

        /// <summary>显式加入远征队伍候选池（不改变 Person 状态）。</summary>
        public EventWorldViewSnapshot AddPartyCandidate(string personId)
        {
            if (!string.IsNullOrEmpty(personId) && !_party.Contains(personId)) _party.Add(personId);
            return this;
        }

        public EventWorldViewSnapshot SetVillageState(int villageDayOrdinal, int ingredients, int food)
        {
            VillageDayOrdinal = villageDayOrdinal;
            Ingredients = ingredients;
            Food = food;
            return this;
        }

        public EventWorldViewSnapshot SetExpeditionState(int depth, int supply, int partySize, int nodeOrdinal)
        {
            ExpeditionDepth = depth;
            ExpeditionSupply = supply;
            ExpeditionPartySize = partySize;
            ExpeditionNodeOrdinal = nodeOrdinal;
            return this;
        }

        // ---------------- IEventWorldView ----------------

        public bool PersonExists(string personId)
        {
            return !string.IsNullOrEmpty(personId) && _persons.ContainsKey(personId);
        }

        public bool PersonInVillage(string personId)
        {
            var person = Find(personId);
            return person != null && person.InVillage;
        }

        public bool PersonOnExpedition(string personId)
        {
            var person = Find(personId);
            return person != null && person.OnExpedition;
        }

        public bool PersonAvailable(string personId)
        {
            var person = Find(personId);
            return person != null && person.Available;
        }

        private EventWorldPersonSnapshot Find(string personId)
        {
            if (string.IsNullOrEmpty(personId)) return null;

            EventWorldPersonSnapshot person;
            return _persons.TryGetValue(personId, out person) ? person : null;
        }
    }
}
