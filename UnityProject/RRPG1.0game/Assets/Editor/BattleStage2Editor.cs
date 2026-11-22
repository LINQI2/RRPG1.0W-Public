/// <summary>已退休的 Stage2 演示内容写入入口；保留历史调用名称，但不再重写当前牌资产。</summary>
public static class BattleStage2Editor
{
    public static void ConfigureDemoCardEffects() =>
        BattleStage1Editor.RejectRetiredEntry(nameof(ConfigureDemoCardEffects));
}
