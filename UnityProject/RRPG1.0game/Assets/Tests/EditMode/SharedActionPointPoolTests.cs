using NUnit.Framework;

/// <summary>
/// SharedActionPointPool（全队共享行动力）EditMode 测试
///
/// 覆盖 Battle GameSpec V0.1 Stage 1 要求：Shared AP / Card Cost。
/// </summary>
public class SharedActionPointPoolTests
{
    [Test]
    public void Constructor_StartsAtMax()
    {
        var pool = new SharedActionPointPool(3);

        Assert.AreEqual(3, pool.Max);
        Assert.AreEqual(3, pool.Current);
    }

    [Test]
    public void Constructor_NegativeMax_ClampedToZero()
    {
        var pool = new SharedActionPointPool(-5);

        Assert.AreEqual(0, pool.Max);
        Assert.AreEqual(0, pool.Current);
    }

    [Test]
    public void CanAfford_Enough_True()
    {
        var pool = new SharedActionPointPool(3);

        Assert.IsTrue(pool.CanAfford(1));
        Assert.IsTrue(pool.CanAfford(3));
    }

    [Test]
    public void CanAfford_NotEnough_False()
    {
        var pool = new SharedActionPointPool(2);

        Assert.IsFalse(pool.CanAfford(3));
    }

    [Test]
    public void Spend_Enough_ReducesCurrent()
    {
        var pool = new SharedActionPointPool(3);

        Assert.IsTrue(pool.Spend(2));
        Assert.AreEqual(1, pool.Current);
    }

    [Test]
    public void Spend_NotEnough_FailsWithoutChange()
    {
        var pool = new SharedActionPointPool(2);

        Assert.IsFalse(pool.Spend(3));
        Assert.AreEqual(2, pool.Current);
    }

    [Test]
    public void Spend_ExactCost_Succeeds()
    {
        var pool = new SharedActionPointPool(2);

        Assert.IsTrue(pool.Spend(2));
        Assert.AreEqual(0, pool.Current);
        Assert.IsFalse(pool.Spend(1)); // 归零后不能再消耗
    }

    [Test]
    public void RestoreToMax_ResetsCurrent()
    {
        var pool = new SharedActionPointPool(4);
        pool.Spend(3);
        Assert.AreEqual(1, pool.Current);

        pool.RestoreToMax();

        Assert.AreEqual(4, pool.Current);
    }

    [Test]
    public void SetMax_ClampsCurrent()
    {
        var pool = new SharedActionPointPool(5);
        pool.Spend(2);
        Assert.AreEqual(3, pool.Current);

        pool.SetMax(2);

        Assert.AreEqual(2, pool.Max);
        Assert.AreEqual(2, pool.Current);
    }

    [Test]
    public void SetMax_Expand_CurrentUnchanged()
    {
        var pool = new SharedActionPointPool(3);

        pool.SetMax(6);

        Assert.AreEqual(6, pool.Max);
        Assert.AreEqual(3, pool.Current);
    }

    [Test]
    public void CardCost_Integration_SharedPoolPaysCards()
    {
        // 模拟：共享行动力池支付多张牌的费用
        var pool = new SharedActionPointPool(5);
        int card1Cost = 2, card2Cost = 1, card3Cost = 2;

        Assert.IsTrue(pool.Spend(card1Cost));
        Assert.IsTrue(pool.Spend(card2Cost));
        Assert.IsTrue(pool.Spend(card3Cost));
        Assert.AreEqual(0, pool.Current);
        Assert.IsFalse(pool.Spend(1));
    }
}
