using NUnit.Framework;

/// <summary>
/// Gate 1：VillageResourceState 单元测试。
/// 覆盖：唯一写入口、负数/不足防护、Save/Restore 往返与旧档缺省安全。
/// 全部数值 Test / Not Frozen。
/// </summary>
public class VillageResourceStateTests
{
    private VillageResourceState NewState(int ingredients, int food)
    {
        var s = new VillageResourceState();
        if (ingredients > 0) s.AddIngredients(ingredients);
        if (food > 0) s.AddFood(food);
        return s;
    }

    [Test]
    public void NewState_StartsAtZero()
    {
        var s = new VillageResourceState();
        Assert.AreEqual(0, s.Ingredients);
        Assert.AreEqual(0, s.Food);
    }

    [Test]
    public void AddAndRead_UpdatesInventory()
    {
        var s = NewState(5, 7);
        Assert.AreEqual(5, s.Ingredients);
        Assert.AreEqual(7, s.Food);
    }

    [Test]
    public void AddNegativeOrZero_IsNoOp()
    {
        var s = NewState(0, 0);
        s.AddIngredients(-1);
        s.AddIngredients(0);
        s.AddFood(-5);
        s.AddFood(0);
        Assert.AreEqual(0, s.Ingredients);
        Assert.AreEqual(0, s.Food);
    }

    [Test]
    public void TryConsumeFood_Success_DecreasesOnlyFood()
    {
        var s = NewState(3, 10);
        Assert.IsTrue(s.TryConsumeFood(4));
        Assert.AreEqual(6, s.Food);
        Assert.AreEqual(3, s.Ingredients);
    }

    [Test]
    public void TryConsume_Insufficient_ReturnsFalseAndKeepsState()
    {
        var s = NewState(2, 1);
        Assert.IsFalse(s.TryConsumeFood(2));
        Assert.IsFalse(s.TryConsumeIngredients(3));
        Assert.AreEqual(1, s.Food);
        Assert.AreEqual(2, s.Ingredients);
    }

    [Test]
    public void TryConsume_NegativeAmount_ReturnsFalseAndKeepsState()
    {
        var s = NewState(4, 4);
        Assert.IsFalse(s.TryConsumeFood(-1));
        Assert.IsFalse(s.TryConsumeIngredients(-1));
        Assert.AreEqual(4, s.Food);
        Assert.AreEqual(4, s.Ingredients);
    }

    [Test]
    public void ConsumeExactBalance_LeavesZero_NoNegative()
    {
        var s = NewState(2, 2);
        Assert.IsTrue(s.TryConsumeFood(2));
        Assert.IsTrue(s.TryConsumeIngredients(2));
        Assert.AreEqual(0, s.Food);
        Assert.AreEqual(0, s.Ingredients);
    }

    [Test]
    public void SaveRoundTrip_PreservesValues()
    {
        var s = NewState(12, 34);
        var dto = s.ToSaveData();
        Assert.AreEqual(12, dto.Ingredients);
        Assert.AreEqual(34, dto.Food);

        var restored = new VillageResourceState();
        restored.Restore(dto);
        Assert.AreEqual(12, restored.Ingredients);
        Assert.AreEqual(34, restored.Food);
    }

    [Test]
    public void RestoreNull_DefaultsToZero()
    {
        var restored = new VillageResourceState();
        restored.AddFood(99);
        restored.Restore(null);
        Assert.AreEqual(0, restored.Ingredients);
        Assert.AreEqual(0, restored.Food);
    }

    [Test]
    public void RestoreNegative_ClampsToZero()
    {
        var restored = new VillageResourceState();
        restored.Restore(new VillageResourceSaveData { Ingredients = -1, Food = -5 });
        Assert.AreEqual(0, restored.Ingredients);
        Assert.AreEqual(0, restored.Food);
    }
}
