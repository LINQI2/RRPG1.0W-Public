using UnityEngine;

/// <summary>
/// Village 世界时间驱动器（常驻场景根，独立于任何 UI 面板/HUD 生命周期）。
///
/// 职责：每帧推进 VillageTime，但只在 VillageRuntimeBridge.IsSimulationActive == true 时推进。
/// 这是显式 runtime/state gating（V1 规则：仅领地界面激活时世界时间推进），
/// 不依赖 HUD 是否启用——HUD 只负责显示与操作，不负责世界时间生命周期。
///
/// V1 不建模远征耗时（Village-GameSpec-V0.1 §6.3 归 V4）：离开领地时 gating 关闭即时间暂停。
/// </summary>
public class VillageTimeDriver : MonoBehaviour
{
    void Start()
    {
        EnsureWorldHost();
    }

    void Update()
    {
        if (VillageRuntimeBridge.Time != null && VillageRuntimeBridge.Time.HasActiveAdvance)
        {
            VillageRuntimeBridge.PumpWorldAdvance();
            return; // Reject all new wall-time delta while one request is active.
        }
        if (!VillageRuntimeBridge.IsSimulationActive) return;
        if (VillageRuntimeBridge.Time != null)
        {
            VillageRuntimeBridge.Time.Update(Time.deltaTime);
        }
    }

    /// <summary>
    /// 确保 VillageWorldLayer 宿主存在（运行时自托管，场景根级；不绑定任何 UI 面板生命周期）。
    /// World 生命周期只服从 VillageRuntimeBridge Simulation 权威状态；本对象常驻仅负责承载视图。
    /// </summary>
    private static void EnsureWorldHost()
    {
        var existing = Object.FindObjectOfType<VillageWorldLayer>(true);
        if (existing != null) return;
        var host = new GameObject("VillageWorldLayer");
        host.AddComponent<VillageWorldLayer>();
    }
}
