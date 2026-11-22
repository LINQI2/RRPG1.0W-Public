using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RRPG.Event;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D1 §2/§3（Phase A）：**Village 事件产品 Adapter**。
    ///
    /// `VillageEventWorldView` / `VillageEventEffectGateway` 位于 Assembly-CSharp（产品程序集），
    /// 因此本测试程序集**不直接引用**其类型：用仓库既有模式（`Type.GetType("X, Assembly-CSharp")`）
    /// 取得实例，再以 **Event.Core 公共接口**（`IEventWorldView` / `IEventEffectGateway`）断言契约。
    ///
    /// 保护点：
    ///   - Person / 资源 / 日序全部来自既有 Authority（不建第二套状态）；
    ///   - Expedition-only 字段**不伪造**（明确非法哨兵），Expedition-only 效果 **Fail Closed**；
    ///   - `GrantPersonRest` 只写新 Rest Authority，**不**写 Legacy `SocialResponse.Rests`。
    /// </summary>
    public class VillageEventAdapterTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static Type BridgeType { get { return Type.GetType("VillageRuntimeBridge, Assembly-CSharp"); } }
        private static Type CollectionType { get { return Type.GetType("PlayerCollection, Assembly-CSharp"); } }
        private static Type WorldViewType { get { return Type.GetType("VillageEventWorldView, Assembly-CSharp"); } }
        private static Type GatewayType { get { return Type.GetType("VillageEventEffectGateway, Assembly-CSharp"); } }

        private object _collection;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(BridgeType, "找不到 Assembly-CSharp 中的 VillageRuntimeBridge");
            Assert.IsNotNull(CollectionType, "找不到 Assembly-CSharp 中的 PlayerCollection");
            Assert.IsNotNull(WorldViewType, "找不到 Assembly-CSharp 中的 VillageEventWorldView");
            Assert.IsNotNull(GatewayType, "找不到 Assembly-CSharp 中的 VillageEventEffectGateway");

            InvokeStatic(BridgeType, "ResetForTests");
            InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });

            _collection = Activator.CreateInstance(CollectionType);
            InvokeInstance(_collection, "InitializeDefaultCollection");

            var sourceField = BridgeType.GetField("SimulationPersonSource", F);
            Assert.IsNotNull(sourceField, "VillageRuntimeBridge.SimulationPersonSource 字段不存在");
            sourceField.SetValue(null, _collection);
        }

        [TearDown]
        public void TearDown()
        {
            InvokeStatic(BridgeType, "ResetForTests");
        }

        // ==================== helpers ====================

        private IEventWorldView NewWorldView()
        {
            // 与产品一致：`VillageEventWorldView(Func<PlayerCollection> personSource = null)`
            // —— 该可选参数是**有意的可注入设计**（不注入时读取 VillageRuntimeBridge.SimulationPersonSource）。
            // 反射必须显式给出这一个实参：`Activator.CreateInstance(type)` 只匹配真正的无参构造，
            // 带可选参数的构造不会被选中（会抛 MissingMethodException）。产品 API 不为此改动。
            return (IEventWorldView)Activator.CreateInstance(WorldViewType, new object[] { null });
        }

        private IEventEffectGateway NewGateway()
        {
            return (IEventEffectGateway)Activator.CreateInstance(GatewayType);
        }

        /// <summary>默认人物集合中的第 index 个真实 PersonId。</summary>
        private string PersonIdAt(int index)
        {
            var list = (IList)CollectionType.GetField("OwnedCharacters").GetValue(_collection);
            Assert.IsNotNull(list);
            Assert.Greater(list.Count, index, "默认人物集合未包含足量人物");

            object person = list[index];
            var field = person.GetType().GetField("PersonId");
            Assert.IsNotNull(field, person.GetType().FullName + " 没有 PersonId 字段");
            return (string)field.GetValue(person);
        }

        private void SetExpedition(params string[] personIds)
        {
            InvokeStatic(BridgeType, "SetExpeditionPersonIds", new object[] { personIds });
        }

        private bool IsRestBlocked(string personId)
        {
            return (bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", new object[] { personId });
        }

        /// <summary>Legacy `SocialResponse.Rests` 记录数（必须保持 0：新路径不得写 Legacy）。</summary>
        private int LegacyRestCount()
        {
            object log = BridgeType.GetField("SocialResponse", F).GetValue(null);
            if (log == null) return 0;
            var rests = (IList)log.GetType().GetProperty("Rests").GetValue(log);
            return rests == null ? 0 : rests.Count;
        }

        private static object InvokeStatic(Type type, string method, params object[] args)
        {
            var selected = FindMethod(type, method, args, true);
            Assert.IsNotNull(selected, "静态方法不存在或参数不匹配：" + method + " @ " + type.FullName);
            return selected.Invoke(null, args);
        }

        private static object InvokeInstance(object target, string method, params object[] args)
        {
            var selected = FindMethod(target.GetType(), method, args, false);
            Assert.IsNotNull(selected, "实例方法不存在或参数不匹配：" + method + " @ " + target.GetType().FullName);
            return selected.Invoke(target, args);
        }

        private static MethodInfo FindMethod(Type type, string method, object[] args, bool isStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            foreach (var candidate in type.GetMethods(flags))
            {
                if (candidate.Name != method) continue;
                var ps = candidate.GetParameters();
                if (ps.Length != (args == null ? 0 : args.Length)) continue;

                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] == null) continue;
                    if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
                }
                if (ok) return candidate;
            }
            return null;
        }

        // ==================== World View ====================

        [Test]
        public void WorldView_PersonQueries_ReadExistingAuthorities()
        {
            string person = PersonIdAt(0);
            var world = NewWorldView();

            Assert.IsTrue(world.PersonExists(person), "默认人物集合中的人物必须存在");
            Assert.IsTrue(world.PersonInVillage(person), "未登记远征 → InVillage");
            Assert.IsFalse(world.PersonOnExpedition(person));
            Assert.IsTrue(world.PersonAvailable(person), "在村且未占用 → Available");

            SetExpedition(person);
            var after = NewWorldView();

            Assert.IsTrue(after.PersonOnExpedition(person), "登记远征 → OnExpedition");
            Assert.IsFalse(after.PersonInVillage(person), "远征人物绝不同时 InVillage");
            Assert.IsFalse(after.PersonAvailable(person));

            Assert.IsFalse(after.PersonExists("person_does_not_exist"));
            Assert.IsFalse(after.PersonAvailable("person_does_not_exist"));
        }

        [Test]
        public void WorldView_VillagePersonCandidatePool_TracksExpeditionRegistry()
        {
            string person = PersonIdAt(0);

            var before = new List<string>(NewWorldView().VillagePersonIds);
            CollectionAssert.Contains(before, person);

            SetExpedition(person);
            var after = NewWorldView();

            CollectionAssert.DoesNotContain(new List<string>(after.VillagePersonIds), person,
                "远征人物不得留在村庄候选池");
            CollectionAssert.Contains(new List<string>(after.ExpeditionPartyPersonIds), person);
        }

        [Test]
        public void WorldView_ResourcesAndDayOrdinal_ComeFromAuthorities()
        {
            var resources = (VillageResourceState)BridgeType.GetField("Resources", F).GetValue(null);
            Assert.IsNotNull(resources, "Village 资源 Authority 必须存在");
            resources.AddIngredients(7);
            resources.AddFood(5);

            var time = (VillageTime)BridgeType.GetField("Time", F).GetValue(null);
            Assert.IsNotNull(time, "Village 时间 Authority 必须存在");
            var intradaySetup1 = time.ToSaveData();
            intradaySetup1.IntradayTime = null;
            intradaySetup1.Year = 2;
            intradaySetup1.Month = 3;
            intradaySetup1.Day = 5;
            time.Restore(intradaySetup1);


            var world = NewWorldView();

            Assert.AreEqual(7, world.Ingredients);
            Assert.AreEqual(5, world.Food);
            Assert.AreEqual(VillageDayOrdinal.ToOrdinal(2, 3, 5), world.VillageDayOrdinal,
                "VillageDayOrdinal 必须由正式 Village 时间换算");
        }

        [Test]
        public void WorldView_ExpeditionOnlyFields_AreExplicitlyUnknown_NotFabricated()
        {
            var world = NewWorldView();

            Assert.AreEqual(-1, world.ExpeditionDepth, "Village 域不得伪造远征深度");
            Assert.AreEqual(-1, world.ExpeditionSupply, "Village 域不得伪造远征补给");
            Assert.AreEqual(-1, world.ExpeditionPartySize, "Village 域不得伪造远征队伍人数");
            Assert.AreEqual(-1, world.ExpeditionNodeOrdinal, "Village 域不得伪造远征节点序");
        }

        // ==================== Effect Gateway ====================

        [Test]
        public void Gateway_VillageResourceDelta_AddsConsumesAndFailsClosed()
        {
            var gateway = NewGateway();
            var resources = (VillageResourceState)BridgeType.GetField("Resources", F).GetValue(null);
            resources.AddIngredients(10);

            string reason;
            int amount;
            Assert.IsTrue(gateway.TryGetVillageResource("Ingredients", out amount));
            Assert.AreEqual(10, amount);
            Assert.IsFalse(gateway.TryGetVillageResource("Gold", out amount), "未知资源键必须明确不可用");

            Assert.IsTrue(gateway.ApplyVillageResourceDelta("Ingredients", 4, out reason), reason);
            Assert.AreEqual(14, resources.Ingredients);

            Assert.IsTrue(gateway.ApplyVillageResourceDelta("Ingredients", -5, out reason), reason);
            Assert.AreEqual(9, resources.Ingredients);

            Assert.IsFalse(gateway.ApplyVillageResourceDelta("Ingredients", -100, out reason),
                "库存不足必须失败（不得夹取 / 不得部分扣除）");
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(9, resources.Ingredients);

            Assert.IsFalse(gateway.ApplyVillageResourceDelta("Gold", 1, out reason), "未知资源键必须 Fail Closed");
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Gateway_GrantRest_WritesNewRestAuthority_AndNeverLegacy()
        {
            string person = PersonIdAt(0);
            var gateway = NewGateway();

            Assert.IsFalse(IsRestBlocked(person), "前置：该人物当前不在恢复窗口");

            string reason;
            Assert.IsTrue(gateway.GrantRest(person, out reason), reason);
            Assert.IsTrue(IsRestBlocked(person), "GrantRest 后必须由新 Rest Authority 拦截");

            var time = (VillageTime)BridgeType.GetField("Time", F).GetValue(null);
            int month = time.Month;
            var intradaySetup2 = time.ToSaveData();
            intradaySetup2.IntradayTime = null;
            intradaySetup2.Month = month == 12 ? 1 : month + 1;
            intradaySetup2.Day = 1;
            time.Restore(intradaySetup2);

            Assert.IsFalse(IsRestBlocked(person), "跨月后必须恢复");

            Assert.AreEqual(0, LegacyRestCount(), "新路径不得写 Legacy SocialResponse.Rests");
        }

        [Test]
        public void Gateway_GrantRest_UnknownPerson_FailsClosed()
        {
            var gateway = NewGateway();
            string reason;

            Assert.IsFalse(gateway.CanGrantRest("person_does_not_exist", out reason));
            Assert.IsNotEmpty(reason);

            Assert.IsFalse(gateway.GrantRest("person_does_not_exist", out reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Gateway_ExpeditionOnlyEffects_FailClosed()
        {
            var gateway = NewGateway();
            string reason;
            int supply;

            Assert.IsFalse(gateway.TryGetExpeditionSupply(out supply), "Village 网关不拥有远征补给");
            Assert.AreEqual(0, supply);

            Assert.IsFalse(gateway.CanAdvanceExpeditionTime(1, out reason));
            Assert.IsNotEmpty(reason);
            Assert.IsFalse(gateway.CanStartBattle("pool_test_general", out reason));
            Assert.IsNotEmpty(reason);

            Assert.IsFalse(gateway.ApplySupplyDelta(3, out reason));
            Assert.IsNotEmpty(reason);
            Assert.IsFalse(gateway.AdvanceExpeditionTime(1, out reason));
            Assert.IsNotEmpty(reason);
            Assert.IsFalse(gateway.StartBattle("pool_test_general", out reason));
            Assert.IsNotEmpty(reason);
        }
    }
}
