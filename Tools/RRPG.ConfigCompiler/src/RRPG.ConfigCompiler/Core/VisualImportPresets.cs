using System;
using System.Collections.Generic;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 视觉导入预设（<c>VisualAsset.ImportPreset</c> 的允许集合；M3 Slice 1A）。
    ///
    /// 边界（重要）：
    ///   - 本类只声明**契约字符串**：把预设翻译成 Unity 导入设置（PPU / Filter / Pivot / 压缩）的是
    ///     Unity Builder（M3 Slice 1B），不在这里；
    ///   - Compiler 只负责「值是否被支持」的校验（DAT015），保证 Mirror 与 Builder 对同一份声明达成一致；
    ///   - 新增预设必须同时更新：本列表 + XLSX 的 Row 3 <c>allowed=</c>（同一声明，两处可见）。
    /// </summary>
    public static class VisualImportPresets
    {
        /// <summary>Point 过滤 / 100 PPU / Center 轴心（确定性、逐像素一致；M3 默认）。</summary>
        public const string SpritePoint100 = "sprite_point_100";

        /// <summary>受支持的预设（顺序即诊断中展示的顺序）。</summary>
        public static readonly IReadOnlyList<string> Supported =
            new List<string> { SpritePoint100 };

        public static bool IsSupported(string preset)
        {
            if (string.IsNullOrEmpty(preset)) return false;

            for (int i = 0; i < Supported.Count; i++)
            {
                if (string.Equals(Supported[i], preset, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static string SupportedListText()
        {
            return string.Join(" / ", Supported);
        }
    }
}
