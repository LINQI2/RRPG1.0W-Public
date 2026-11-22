using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 确定性 JSON 写入器（Spike A 自研，最小实现）。
    ///
    /// 为什么不用第三方序列化器：
    ///   - Canonical IR 要求逐字节稳定：缩进 / 换行 / 数字格式 / 键顺序 / 转义必须完全可控；
    ///   - 键顺序必须等于列声明顺序（不排序、不随机化）；
    ///   - 明确禁止写入时间戳等导致无意义 diff 的字段（由调用方保证，本类只负责格式）。
    ///
    /// 硬约束：
    ///   - 缩进固定 2 空格；换行固定 LF（\n）；文件末尾单个 LF；
    ///   - 数字只使用 InvariantCulture（不受本机 culture 影响）；
    ///   - 非 ASCII（含中文）保持原样输出为 UTF-8 字符，不转义为 \uXXXX（人类 / Git diff 可读）。
    /// </summary>
    public sealed class JsonWriterLite
    {
        private sealed class Frame
        {
            public bool HasItems;
            public bool IsArray;
        }

        private readonly StringBuilder _sb = new StringBuilder();
        private readonly List<Frame> _stack = new List<Frame>();
        private readonly int _indentSize;
        private bool _namePending;

        public JsonWriterLite(int indentSize = 2)
        {
            _indentSize = indentSize;
        }

        public void BeginObject()
        {
            WriteValuePrefix();
            _sb.Append('{');
            _stack.Add(new Frame { IsArray = false });
        }

        public void EndObject()
        {
            CloseFrame('}');
        }

        public void BeginArray()
        {
            WriteValuePrefix();
            _sb.Append('[');
            _stack.Add(new Frame { IsArray = true });
        }

        public void EndArray()
        {
            CloseFrame(']');
        }

        /// <summary>写对象成员名（只能在对象内调用）。</summary>
        public void Name(string name)
        {
            if (_stack.Count == 0 || _stack[_stack.Count - 1].IsArray)
                throw new InvalidOperationException("Name() 只能在对象内调用：" + name);

            var frame = _stack[_stack.Count - 1];
            if (frame.HasItems) _sb.Append(',');
            _sb.Append('\n');
            Indent();
            frame.HasItems = true;

            _sb.Append('"').Append(Escape(name)).Append("\": ");
            _namePending = true;
        }

        public void Value(string value)
        {
            WriteValuePrefix();
            if (value == null) _sb.Append("null");
            else _sb.Append('"').Append(Escape(value)).Append('"');
        }

        public void Value(int value)
        {
            WriteValuePrefix();
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Value(long value)
        {
            WriteValuePrefix();
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Value(bool value)
        {
            WriteValuePrefix();
            _sb.Append(value ? "true" : "false");
        }

        /// <summary>浮点：使用往返格式 "R" + InvariantCulture（2.5 → 2.5；2.0 → 2）。</summary>
        public void Value(double value)
        {
            WriteValuePrefix();
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("Canonical IR 不接受 NaN / Infinity：" + value);
            _sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        public void Null()
        {
            WriteValuePrefix();
            _sb.Append("null");
        }

        /// <summary>便捷：一个对象成员。</summary>
        public void Member(string name, string value) { Name(name); Value(value); }

        public void Member(string name, int value) { Name(name); Value(value); }

        public void Member(string name, long value) { Name(name); Value(value); }

        public void Member(string name, bool value) { Name(name); Value(value); }

        public void Member(string name, double value) { Name(name); Value(value); }

        /// <summary>完成并返回文本（UTF-8 写盘由调用方负责；末尾单个 LF）。</summary>
        public string ToText()
        {
            if (_stack.Count != 0) throw new InvalidOperationException("JSON 结构未闭合（frames=" + _stack.Count + "）");
            return _sb.ToString() + "\n";
        }

        // ---------------- 内部 ----------------

        /// <summary>写值前缀：处理逗号 + 换行 + 缩进；若刚写过成员名则跳过（值内联）。</summary>
        private void WriteValuePrefix()
        {
            if (_namePending)
            {
                _namePending = false;
                return;
            }

            if (_stack.Count == 0) return; // 根值：不缩进

            var frame = _stack[_stack.Count - 1];
            if (frame.HasItems) _sb.Append(',');
            _sb.Append('\n');
            Indent();
            frame.HasItems = true;
        }

        private void CloseFrame(char close)
        {
            if (_stack.Count == 0) throw new InvalidOperationException("JSON 结构闭合不匹配");
            var frame = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);

            if (frame.HasItems)
            {
                _sb.Append('\n');
                Indent();
            }
            _sb.Append(close);

            // 闭合后的括号本身是上一层的“值”，该层已被标记 HasItems
            if (_stack.Count > 0) _namePending = false;
        }

        private void Indent()
        {
            int spaces = _stack.Count * _indentSize;
            for (int i = 0; i < spaces; i++) _sb.Append(' ');
        }

        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
