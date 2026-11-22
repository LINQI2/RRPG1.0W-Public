using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// M3 Slice 1C：Enemy Presentation 的**唯一解析入口**（纯 Runtime；无 Editor / 无场景依赖）。
    ///
    /// 链路：MonsterId → <see cref="EnemyPresentationBinding.VisualAssetId"/> → <see cref="VisualAssetDefinition.Sprite"/>。
    ///
    /// 失败保护（Fallback）**只存在于本类**（硬约束）：
    ///   - Root / 生成根不可用（Registry 诊断非空）；
    ///   - Monster 没有 Presentation 绑定（未登记 / 领域为空）；
    ///   - VisualAssetId 不在 VisualAssetCatalog 中；
    ///   - VisualAsset 存在但 Sprite 为 null（生成根不完整）
    ///   → **一律不静默**：返回可安全显示的 fallback sprite + 一次（WarnOnce）明确、可操作的中文诊断。
    ///   业务层（Battle / Expedition / UI）**不得**再判断或自行拼装 fallback：只调用 <see cref="Resolve"/> /
    ///   <see cref="TryResolve"/>，把「是否回退」这件事完全留给本类。
    ///
    /// 两个入口的语义差异（刻意明确）：
    ///   - <see cref="TryResolve"/>：**严格** —— 仅当 Authority 链路完整时返回 true（此时 diagnostic 为空）。
    ///     返回 false 时 sprite **仍是 fallback（非 null）**，diagnostic 明确失败原因 → 调用方若只关心画面，
    ///     直接用返回的 sprite 即可；若要区分「正式 / 回退」，看返回值与 diagnostic。
    ///   - <see cref="Resolve"/>：**显示安全** —— 永不返回 null（Authority 成功 → 其 Sprite；否则 fallback）。
    ///
    /// 为什么 fallback 是运行时生成的 sprite（而不是某个资产 / Inspector 字段）：
    ///   Runtime 不得依赖 Editor、不得依赖场景引用；用 1×1 明显色块 + WarnOnce 诊断表达「这不是正式视觉」。
    /// </summary>
    public sealed class EnemyPresentationResolver
    {
        /// <summary>WarnOnce 日志前缀（测试与排查用）。</summary>
        public const string FallbackWarningPrefix = "[Enemy Presentation FALLBACK]";

        public const string MissingPresentationDiagnosticPrefix =
            "Enemy Presentation 回退：该 Monster 没有 Presentation 绑定：";

        public const string MissingVisualAssetDiagnosticPrefix =
            "Enemy Presentation 回退：VisualAssetId 不在 VisualAssetCatalog 中：";

        public const string MissingSpriteDiagnosticPrefix =
            "Enemy Presentation 回退：VisualAsset 的 Sprite 不可用（生成根不完整？）：";

        public const string RegistryUnavailableDiagnosticPrefix =
            "Enemy Presentation 回退：StaticConfig 生成根 / 领域不可用：";

        public const string NullMonsterIdDiagnostic =
            "Enemy Presentation 回退：monsterId 为 null / 空（调用方错误）。";

        /// <summary>fallback 颜色（洋红 = 与 Unity missing-asset 同色，肉眼即可识别「这是回退」）。</summary>
        public static readonly Color FallbackColor = new Color(1f, 0f, 1f, 1f);

        private static Sprite _fallbackSprite;
        private static bool _warnIssued;
        private static int _warnCount;

        private readonly EnemyPresentationDatabase _presentations;
        private readonly VisualAssetCatalog _catalog;
        private readonly string _presentationDiagnostic;
        private readonly string _visualAssetDiagnostic;

        private EnemyPresentationResolver(EnemyPresentationDatabase presentations, VisualAssetCatalog catalog,
            string presentationDiagnostic, string visualAssetDiagnostic)
        {
            _presentations = presentations;
            _catalog = catalog;
            _presentationDiagnostic = presentationDiagnostic ?? string.Empty;
            _visualAssetDiagnostic = visualAssetDiagnostic ?? string.Empty;
            Diagnostic = string.Empty;
        }

        /// <summary>最近一次解析的诊断（正式路径成功时为空字符串）。</summary>
        public string Diagnostic { get; private set; }

        /// <summary>本次进程内是否已输出过回退警告（WarnOnce 状态；测试与诊断用）。</summary>
        public static bool WarnIssued { get { return _warnIssued; } }

        /// <summary>回退发生次数（每次都计数；日志只输出一次 → 用于证明「回退不静默也不刷屏」）。</summary>
        public static int WarnCount { get { return _warnCount; } }

        /// <summary>清空 WarnOnce 状态与计数（仅供测试与 StaticConfigRegistry.Reset 使用）。</summary>
        public static void ResetWarnOnce()
        {
            _warnIssued = false;
            _warnCount = 0;
        }

        /// <summary>
        /// Runtime fallback sprite（1×1 <see cref="FallbackColor"/>；进程内缓存）。
        /// 不读任何资产、不依赖 Editor —— 因此「生成根完全缺失」时也能安全显示。
        /// </summary>
        public static Sprite FallbackSprite
        {
            get
            {
                if (_fallbackSprite != null) return _fallbackSprite;

                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.name = "RRPGEnemyPresentationFallbackTexture";
                texture.SetPixel(0, 0, FallbackColor);
                texture.Apply(false, false);
                texture.hideFlags = HideFlags.HideAndDontSave;

                _fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
                _fallbackSprite.name = "RRPGEnemyPresentationFallbackSprite";
                _fallbackSprite.hideFlags = HideFlags.HideAndDontSave;

                return _fallbackSprite;
            }
        }

        /// <summary>
        /// 由 <see cref="StaticConfigRegistry"/> 取得两个领域（lazy / 幂等；不依赖 Editor）并构造 Resolver。
        /// Registry 的明确诊断会被保留，供回退诊断使用。
        /// </summary>
        public static EnemyPresentationResolver FromStaticConfig()
        {
            EnemyPresentationDatabase presentations;
            string presentationDiagnostic;
            StaticConfigRegistry.TryGetEnemyPresentation(out presentations, out presentationDiagnostic);

            VisualAssetCatalog catalog;
            string visualAssetDiagnostic;
            StaticConfigRegistry.TryGetVisualAssets(out catalog, out visualAssetDiagnostic);

            return new EnemyPresentationResolver(presentations, catalog, presentationDiagnostic, visualAssetDiagnostic);
        }

        /// <summary>
        /// 以给定数据构造（**不读任何全局状态**）：供测试与未来的显式装配使用。
        /// 传入 null 表示「该领域不可用」→ 解析时走 fallback 并给出明确诊断。
        /// </summary>
        public static EnemyPresentationResolver Create(EnemyPresentationDatabase presentations, VisualAssetCatalog catalog)
        {
            return new EnemyPresentationResolver(presentations, catalog, string.Empty, string.Empty);
        }

        /// <summary>
        /// 严格解析：仅当 Authority 链路（Monster → VisualAsset → Sprite）完整时返回 true。
        ///
        /// 返回 false 时：
        ///   - <paramref name="sprite"/> = **fallback sprite（非 null）**（调用方无需再处理 null）；
        ///   - <paramref name="diagnostic"/> 必为明确、可操作的中文诊断，并已按 WarnOnce 记录一次警告。
        /// </summary>
        public bool TryResolve(string monsterId, out Sprite sprite, out string diagnostic)
        {
            sprite = null;
            diagnostic = string.Empty;

            if (string.IsNullOrEmpty(monsterId))
            {
                return Fallback(null, NullMonsterIdDiagnostic, out sprite, out diagnostic);
            }

            if (_presentations == null)
            {
                return Fallback(null, RegistryUnavailableDiagnosticPrefix + UnavailableReason(), out sprite, out diagnostic);
            }

            EnemyPresentationBinding binding;
            if (!_presentations.TryGetByMonsterId(monsterId, out binding)
                || binding == null
                || string.IsNullOrEmpty(binding.VisualAssetId))
            {
                return Fallback(monsterId, MissingPresentationDiagnosticPrefix + monsterId, out sprite, out diagnostic);
            }

            if (_catalog == null)
            {
                return Fallback(monsterId, RegistryUnavailableDiagnosticPrefix + UnavailableReason(), out sprite, out diagnostic);
            }

            VisualAssetDefinition asset;
            if (!_catalog.TryGetById(binding.VisualAssetId, out asset) || asset == null)
            {
                return Fallback(monsterId,
                    MissingVisualAssetDiagnosticPrefix + binding.VisualAssetId + "（Monster=" + monsterId + "）",
                    out sprite, out diagnostic);
            }

            if (asset.Sprite == null)
            {
                return Fallback(monsterId,
                    MissingSpriteDiagnosticPrefix + binding.VisualAssetId + "（Monster=" + monsterId + "）",
                    out sprite, out diagnostic);
            }

            Diagnostic = string.Empty;
            sprite = asset.Sprite;
            return true;
        }

        /// <summary>
        /// 显示安全解析：**永不返回 null**（Authority 成功 → 其 Sprite；否则 fallback sprite）。
        /// 需要区分「正式 / 回退」时改用 <see cref="TryResolve"/> 或读取 <see cref="Diagnostic"/>。
        /// </summary>
        public Sprite Resolve(string monsterId)
        {
            Sprite sprite;
            string diagnostic;
            TryResolve(monsterId, out sprite, out diagnostic);

            return sprite != null ? sprite : FallbackSprite;
        }

        /// <summary>回退统一出口：设置 fallback sprite + 明确诊断 + WarnOnce（本类内唯一的回退实现）。</summary>
        private bool Fallback(string monsterId, string diagnostic, out Sprite sprite, out string diagnosticOut)
        {
            diagnosticOut = diagnostic;
            Diagnostic = diagnostic;
            WarnOnce(diagnostic);

            sprite = FallbackSprite;
            return false;
        }

        /// <summary>Registry 不可用时的原因（优先显示绑定侧诊断，其次资源侧诊断）。</summary>
        private string UnavailableReason()
        {
            if (!string.IsNullOrEmpty(_presentationDiagnostic)) return _presentationDiagnostic;
            if (!string.IsNullOrEmpty(_visualAssetDiagnostic)) return _visualAssetDiagnostic;
            return "StaticConfigRegistry 未提供 Enemy Presentation 领域（生成根缺失或未包含该领域）。";
        }

        /// <summary>WarnOnce：回退每次都计数，但日志只输出一次（不静默、也不刷屏）。</summary>
        private static void WarnOnce(string diagnostic)
        {
            _warnCount++;
            if (_warnIssued) return;

            _warnIssued = true;
            Debug.LogWarning(FallbackWarningPrefix + " " + diagnostic);
        }
    }
}
