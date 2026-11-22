using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：Role 人物唯一性（§10）与回溯求解（§11 / §12）。覆盖：R12、R13。
    /// </summary>
    public class EventResolverRoleSolverTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        private static EventStaticCatalog TwoRoleCatalog()
        {
            return SingleEventCatalog(Evt("evt_two_roles"), VillageTrigger,
                roles: new List<EventRoleDefinition>
                {
                    Role("role_a", "evt_two_roles", "VillagePerson"),
                    Role("role_b", "evt_two_roles", "VillagePerson")
                });
        }

        [Test]
        public void R12_DifferentRoles_MustBindDifferentPersons()
        {
            var catalog = TwoRoleCatalog();

            for (int seed = 0; seed < 30; seed++)
            {
                var result = Resolve(catalog, Request(VillageTrigger, "k_unique", seed));
                Assert.IsTrue(result.Success, result.Diagnostic);

                string a;
                string b;
                Assert.IsTrue(result.Instance.TryGetBoundPerson("role_a", out a));
                Assert.IsTrue(result.Instance.TryGetBoundPerson("role_b", out b));
                Assert.AreNotEqual(a, b, "同一个实例内不同 Role 默认必须绑定不同 PersonId");
            }
        }

        [Test]
        public void R12_OnlyOneEligiblePersonForTwoRoles_IsNoLegalRoleBinding()
        {
            var catalog = TwoRoleCatalog();

            var singlePersonWorld = new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .SetVillageState(100, 0, 0)
                .SetExpeditionState(0, 0, 0, 0);

            var result = Resolve(catalog, Request(VillageTrigger, "k_unique"), world: singlePersonWorld);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoLegalRoleBinding, result.Code);
            Assert.AreEqual(EventRejectStage.RoleBinding, result.Trace.Find("evt_two_roles").RejectStage);
        }

        // ---------------- R13：回溯（不是「抽一次」） ----------------

        private static EventWorldViewSnapshot BacktrackingWorld()
        {
            return new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: false)
                .AddPerson("p2", inVillage: true, available: true)
                .AddPerson("p3", inVillage: true, available: false)
                .AddPerson("p4", inVillage: true, available: false)
                .AddPerson("p5", inVillage: true, available: false)
                .SetVillageState(100, 0, 0)
                .SetExpeditionState(0, 0, 0, 0);
        }

        private static EventStaticCatalog BacktrackingCatalog()
        {
            return SingleEventCatalog(Evt("evt_bt"), VillageTrigger,
                roles: new List<EventRoleDefinition>
                {
                    Role("role_1", "evt_bt", "VillagePerson"),
                    Role("role_2", "evt_bt", "VillagePerson"),
                    Role("role_3", "evt_bt", "VillagePerson")
                },
                roleConditions: new List<EventRoleConditionDefinition>
                {
                    // role_3 只能由「可用」人物承担（本 Fixture 里只有 p2 可用）
                    RoleCondition("rc_3", "role_3", "PersonAvailable")
                });
        }

        [Test]
        public void R13_Backtracking_FindsSolution_WhenFirstChoiceBlocksLaterRole()
        {
            var catalog = BacktrackingCatalog();
            var world = BacktrackingWorld();

            int seedRequiringBacktracking = -1;

            for (int seed = 0; seed < 60; seed++)
            {
                var result = Resolve(catalog, Request(VillageTrigger, "k_bt", seed), world: world);
                Assert.IsTrue(result.Success, "seed " + seed + " 必须求解成功（回溯不允许因第一组失败而宣告无解）："
                    + result.Diagnostic);

                string first;
                string second;
                string third;
                Assert.IsTrue(result.Instance.TryGetBoundPerson("role_1", out first));
                Assert.IsTrue(result.Instance.TryGetBoundPerson("role_2", out second));
                Assert.IsTrue(result.Instance.TryGetBoundPerson("role_3", out third));

                Assert.AreNotEqual(first, second);
                Assert.AreNotEqual(first, third);
                Assert.AreNotEqual(second, third, "三个 Role 必须绑定三个不同人物");
                Assert.AreEqual("p2", third, "role_3 只能由唯一「可用」人物承担");

                if (result.Trace.Find("evt_bt").SearchNodes > 3 && seedRequiringBacktracking < 0)
                {
                    seedRequiringBacktracking = seed;
                }
            }

            Assert.Greater(seedRequiringBacktracking, -1,
                "至少应存在一个 Seed 需要回溯（评估次数多于 Role 数量）；否则本用例不能证明「求解而非抽一次」");
        }
    }
}
