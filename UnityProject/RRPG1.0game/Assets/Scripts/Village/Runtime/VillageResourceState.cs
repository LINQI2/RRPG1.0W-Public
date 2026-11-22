/// <summary>
/// VillageResourceSaveData —— Village 长期资源存档镜像（Village.Runtime 纯数据）。
/// 与 VillageResourceState 的 Ingredients / Food 一一镜像，由 VillageRuntimeBridge 负责
/// Build（runtime → DTO）与 Restore（DTO → runtime）。旧档缺失 / 默认 0 为合法状态。
/// 数值 Test / Not Frozen。
/// </summary>
[System.Serializable]
public class VillageResourceSaveData
{
    public int Ingredients;
    public int Food;
}

/// <summary>
/// VillageResourceState —— WP-3 Village 长期资源唯一 Authority（Village.Runtime 纯逻辑）。
///
/// 职责（已冻结范围）：只保存 Ingredients / Food 两个长期量，并对外提供原子写入口；
/// 资源后果每日由 VillageResourceSettlement 结算（本类不承载结算逻辑）。
///
/// 禁止（Village-Resource-Coupling-GameSpec-V0.1 §0.3 / WP-3 冻结决定）：
///   - 保存 Production 逻辑 / Person 状态 / Carry 状态 / Building 状态；
///   - 允许外部直接改资源字段（_ingredients / _food 私有，只读属性暴露）；
///   - 出现负数（Add 拒负参、TryConsume 不足/负参返回 false 且不改变状态，Restore 夹取）。
///
/// 全部数值为 Test / Not Frozen，不自动升级为正式数值。
/// </summary>
public class VillageResourceState
{
    private int _ingredients;
    private int _food;

    /// <summary>当前 Ingredients 存量（只读）。</summary>
    public int Ingredients => _ingredients;

    /// <summary>当前 Food 存量（只读）。</summary>
    public int Food => _food;

    public VillageResourceState()
    {
        _ingredients = 0;
        _food = 0;
    }

    // ==================== 唯一写入口 ====================

    /// <summary>入库 Ingredients。负参 / 0 为 no-op；防溢出上限夹取到 int.MaxValue。</summary>
    public void AddIngredients(int amount)
    {
        if (amount <= 0) return;
        _ingredients = ClampAdd(_ingredients, amount);
    }

    /// <summary>入库 Food。负参 / 0 为 no-op；防溢出上限夹取到 int.MaxValue。</summary>
    public void AddFood(int amount)
    {
        if (amount <= 0) return;
        _food = ClampAdd(_food, amount);
    }

    /// <summary>原子扣减 Ingredients。负参 / 存量不足返回 false 且状态不变。</summary>
    public bool TryConsumeIngredients(int amount)
    {
        if (amount < 0 || _ingredients < amount) return false;
        _ingredients -= amount;
        return true;
    }

    /// <summary>原子扣减 Food。负参 / 存量不足返回 false 且状态不变。</summary>
    public bool TryConsumeFood(int amount)
    {
        if (amount < 0 || _food < amount) return false;
        _food -= amount;
        return true;
    }

    // ==================== Save 转换（镜像，不经本类之外序列化） ====================

    public VillageResourceSaveData ToSaveData()
    {
        return new VillageResourceSaveData
        {
            Ingredients = _ingredients,
            Food = _food
        };
    }

    /// <summary>从存档恢复。null / 负值一律回退到安全值（>= 0），不抛异常、不破坏旧档兼容。</summary>
    public void Restore(VillageResourceSaveData data)
    {
        if (data == null)
        {
            _ingredients = 0;
            _food = 0;
            return;
        }
        _ingredients = data.Ingredients > 0 ? data.Ingredients : 0;
        _food = data.Food > 0 ? data.Food : 0;
    }

    private static int ClampAdd(int current, int amount)
    {
        long sum = (long)current + amount;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }
}
