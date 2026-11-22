using RRPG.ConfigCompiler.Core;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 诊断收集器的薄包装：让解析器能携带 workbook / sheet / row 上下文而无需到处传 DiagContext。
    /// </summary>
    public sealed class DiagBag
    {
        private readonly DiagnosticBag _bag;
        private readonly DiagContext _context;

        public DiagBag(DiagnosticBag bag, DiagContext context)
        {
            _bag = bag;
            _context = context;
        }

        public void Error(string code, string message, string column = null, int row = 0, string rule = null)
        {
            _bag.Error(code, message, Context(row), column, rule);
        }

        public void Warn(string code, string message, string column = null, int row = 0, string rule = null)
        {
            _bag.Warn(code, message, Context(row), column, rule);
        }

        /// <summary>同一个 workbook / sheet，但换到另一行（数据行诊断用）。</summary>
        public DiagBag At(int row)
        {
            return new DiagBag(_bag, DiagContext.Of(_context.Workbook, _context.Sheet, row));
        }

        private DiagContext Context(int row)
        {
            if (row <= 0) return _context;
            return DiagContext.Of(_context.Workbook, _context.Sheet, row);
        }
    }
}
