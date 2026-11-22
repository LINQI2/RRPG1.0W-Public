using System;

namespace RRPG.Event
{
    /// <summary>EventTemplateRenderer：占位符解析与失败分类（§10/§11/§12）。</summary>
    public static partial class EventTemplateRenderer
    {
        private const string RoleSuffix = ".DisplayName";
        private const string ContextPrefix = "Context.";

        private static bool TryResolvePlaceholder(string token, ResolvedEventInstance instance,
            EventPersonDisplayNameResolver resolver, out string value, out string diagnostic)
        {
            value = null;
            diagnostic = string.Empty;

            // ---------------- Context.Key ----------------
            if (token.StartsWith(ContextPrefix, StringComparison.Ordinal))
                return TryResolveContext(token.Substring(ContextPrefix.Length), instance, out value, out diagnostic);

            // ---------------- RoleId.DisplayName ----------------
            if (token.EndsWith(RoleSuffix, StringComparison.Ordinal))
                return TryResolveRole(token.Substring(0, token.Length - RoleSuffix.Length), instance, resolver,
                    out value, out diagnostic);

            diagnostic = "不受支持的占位符形状：只允许 {RoleId.DisplayName} 与 {Context.Key}。";
            return false;
        }

        private static bool TryResolveContext(string key, ResolvedEventInstance instance, out string value,
            out string diagnostic)
        {
            value = null;

            if (string.IsNullOrEmpty(key))
            {
                diagnostic = "Context 占位符缺少键名。";
                return false;
            }

            var snapshot = instance.ContextSnapshot;
            if (snapshot == null)
            {
                diagnostic = "事件实例缺少 Context 快照。";
                return false;
            }

            EventContextEntry entry;
            if (snapshot.TryGetEntry(EventContextEntryKind.Int, key, out entry))
            {
                value = entry.IntValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                diagnostic = string.Empty;
                return true;
            }

            if (snapshot.TryGetEntry(EventContextEntryKind.String, key, out entry))
            {
                value = entry.StringValue ?? string.Empty;
                diagnostic = string.Empty;
                return true;
            }

            if (snapshot.TryGetEntry(EventContextEntryKind.Person, key, out entry))
            {
                value = entry.StringValue ?? string.Empty;
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = "Context 中不存在键 '" + key + "'（String / Int / Person 都未命中）。";
            return false;
        }

        private static bool TryResolveRole(string roleId, ResolvedEventInstance instance,
            EventPersonDisplayNameResolver resolver, out string value, out string diagnostic)
        {
            value = null;

            if (string.IsNullOrEmpty(roleId))
            {
                diagnostic = "Role 占位符缺少 RoleId。";
                return false;
            }

            string personId = null;
            bool found = false;
            foreach (var binding in instance.RoleBindings)
            {
                if (binding == null) continue;
                if (!string.Equals(binding.RoleId, roleId, StringComparison.Ordinal)) continue;

                found = true;
                personId = binding.PersonId;
                break;
            }

            if (!found)
            {
                diagnostic = "事件实例中没有角色 '" + roleId + "'。";
                return false;
            }

            if (string.IsNullOrEmpty(personId))
            {
                diagnostic = "角色 '" + roleId + "' 未绑定到具体人物。";
                return false;
            }

            string displayName = null;
            if (resolver == null || !resolver(personId, out displayName) || string.IsNullOrEmpty(displayName))
            {
                diagnostic = "无法解析人物 '" + personId + "' 的正式显示名（Role " + roleId + "）。";
                return false;
            }

            value = displayName;
            diagnostic = string.Empty;
            return true;
        }

        private static EventTemplateRenderFailure ClassifyFailure(string token, string diagnostic)
        {
            if (token.StartsWith(ContextPrefix, StringComparison.Ordinal))
            {
                return diagnostic.Contains("缺少键名") || diagnostic.Contains("缺少 Context 快照")
                    ? EventTemplateRenderFailure.MalformedPlaceholder
                    : EventTemplateRenderFailure.ContextKeyMissing;
            }

            if (token.EndsWith(RoleSuffix, StringComparison.Ordinal))
            {
                if (diagnostic.Contains("没有角色")) return EventTemplateRenderFailure.RoleNotFound;
                if (diagnostic.Contains("未绑定")) return EventTemplateRenderFailure.RoleNotBound;
                if (diagnostic.Contains("无法解析人物")) return EventTemplateRenderFailure.PersonNameUnresolved;
                return EventTemplateRenderFailure.MalformedPlaceholder;
            }

            return EventTemplateRenderFailure.UnsupportedPlaceholder;
        }
    }
}
