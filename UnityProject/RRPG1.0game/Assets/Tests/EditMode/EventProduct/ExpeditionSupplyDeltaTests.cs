using NUnit.Framework;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D0 §21：Supply 增量权威（`CanApplyDelta` / `TryApplyDelta`）。
    /// Supply 仍然只有一个 Authority；失败一律零 mutation，不夹取、不溢出。
    /// </summary>
    public class ExpeditionSupplyDeltaTests
    {
        [Test]
        public void Delta_PositiveAndNegative_AreAtomic()
        {
            var supply = new ExpeditionSupplyState(partySize: 2, initialCarryUnits: 10,
                perPersonDailyConsumptionRate: 1);

            Assert.IsTrue(supply.CanApplyDelta(5));
            Assert.IsTrue(supply.TryApplyDelta(5));
            Assert.AreEqual(15, supply.CurrentCarryUnits, "正 delta 必须增加 Supply");

            Assert.IsTrue(supply.TryApplyDelta(-4));
            Assert.AreEqual(11, supply.CurrentCarryUnits, "负 delta 必须扣减 Supply");
        }

        [Test]
        public void Delta_BelowZero_IsRejected_WithZeroMutation()
        {
            var supply = new ExpeditionSupplyState(1, 10, 1);

            Assert.IsFalse(supply.CanApplyDelta(-11));
            Assert.IsFalse(supply.TryApplyDelta(-11));
            Assert.AreEqual(10, supply.CurrentCarryUnits, "结果 < 0 必须拒绝且不改变状态");

            Assert.IsTrue(supply.TryApplyDelta(-10));
            Assert.AreEqual(0, supply.CurrentCarryUnits);
        }

        [Test]
        public void Delta_Overflow_IsRejected()
        {
            var supply = new ExpeditionSupplyState(1, 10, 1);

            Assert.IsFalse(supply.CanApplyDelta(int.MaxValue), "溢出必须明确拒绝（不夹取）");
            Assert.IsFalse(supply.TryApplyDelta(int.MaxValue));
            Assert.AreEqual(10, supply.CurrentCarryUnits);
        }

        [Test]
        public void Delta_Zero_IsNoOpSuccess()
        {
            var supply = new ExpeditionSupplyState(1, 3, 1);
            Assert.IsTrue(supply.CanApplyDelta(0));
            Assert.IsTrue(supply.TryApplyDelta(0));
            Assert.AreEqual(3, supply.CurrentCarryUnits);
        }

        [Test]
        public void LegacyTryConsume_StillWorks()
        {
            // 旧原语保持兼容（Battle / 普通 Node 结算仍在使用）。
            var supply = new ExpeditionSupplyState(2, 10, 1);
            Assert.IsTrue(supply.TryConsume(4));
            Assert.AreEqual(6, supply.CurrentCarryUnits);
            Assert.IsFalse(supply.TryConsume(7));
            Assert.AreEqual(6, supply.CurrentCarryUnits);
        }
    }
}
