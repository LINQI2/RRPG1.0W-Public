/// <summary>已退休的 Stage3A 演示距离写入入口；保留历史调用名称，但不再重写当前牌资产。</summary>
public static class BattleStage3AEditor
{
    public static void ConfigureDemoRangeModifiers() =>
        BattleStage1Editor.RejectRetiredEntry(nameof(ConfigureDemoRangeModifiers));
}
