using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RRPG.StaticConfig;
using UnityEditor;
using UnityEngine;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>
    /// 正式 Unity Builder（M0 = Builder Foundation；FDS §10 / §11 / §12）。
    ///
    /// 只做这些：
    ///   1. 找到正式 Canonical Mirror（Development/Generated/StaticConfig）；
    ///   2. 校验输入状态（存在性 / 生成标识 / JSON 形状）—— **失败前不写任何产物**；
    ///   3. 生成 / 更新唯一 Generated Root（Assets/Generated/StaticConfig/Resources/RRPGStaticConfigRoot.asset）；
    ///   4. content-signature 驱动的 **no-op**：输入相同则不触碰任何产物（含 mtime）；
    ///   5. **Managed Root ownership**：只管理 Assets/Generated/StaticConfig 之内，绝不删到根外；
    ///   6. rebuild from zero：删除整个 Managed Root 后重建（FDS §11 Git-independent Gate）。
    ///
    /// M0 明确**不做**：Monster 字段迁移 / EnemyPresentation 业务内容 / Battle 视觉绑定。
    ///
    /// headless 约定（沿用本仓既有稳定做法）：由 <c>-batchmode -quit -executeMethod</c> 调用，
    /// 结果写入 Tools/RRPG.ConfigCompiler/.build-state/builder.stage.txt（result=PASS/FAIL），
    /// 由 Producer 入口读取该文件判定成功（不依赖 Editor 退出码）。
    /// </summary>
    public static class StaticConfigBuilder
    {
        public const string ManagedRoot = "Assets/Generated/StaticConfig";
        public const string ResourcesDir = ManagedRoot + "/Resources";
        public const string DatabasesDir = ManagedRoot + "/Databases";

        /// <summary>Managed 资产根（源图生成的 Sprite 落点；与 Mirror 的 ManagedAssetPath 契约同源）。</summary>
        public const string ManagedAssetsDir = ManagedRoot + "/Assets";

        /// <summary>源资产根（相对仓库根；只读输入，绝不写入）。</summary>
        public const string SourceAssetsRelativeDir = "Development/SourceAssets";

        public const string RootAssetPath = ResourcesDir + "/RRPGStaticConfigRoot.asset";
        public const string MonsterDatabasePath = DatabasesDir + "/MonsterConfigDatabase.asset";
        public const string EnemyPresentationDatabasePath = DatabasesDir + "/EnemyPresentationDatabase.asset";
        public const string VisualAssetCatalogPath = DatabasesDir + "/VisualAssetCatalog.asset";

        /// <summary>WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础）的生成容器。</summary>
        public const string EncounterDatabasePath = DatabasesDir + "/EncounterConfigDatabase.asset";

        /// <summary>WP-Event-1A：Event 领域（统一事件内容基础）的生成容器。</summary>
        public const string EventDatabasePath = DatabasesDir + "/EventConfigDatabase.asset";

        public const string MirrorRelativeDir = "Development/Generated/StaticConfig";
        public const string BuilderVersion = "rrpg-static-config-builder/1";

        /// <summary>托管 Sprite 的 PPU（<c>sprite_point_100</c> 预设的一部分；改它 = 改预设契约）。</summary>
        public const float SpritePresetPixelsPerUnit = 100f;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public sealed class BuildOutcome
        {
            public bool Success;
            public bool NoOp;
            public string Signature = string.Empty;
            public string Failure = string.Empty;
            public List<string> Actions = new List<string>();
        }

        [MenuItem("RRPG/Static Config/M0 · Build Generated Root")]
        public static void BuildFromMenu()
        {
            var outcome = Build(false);
            Debug.Log("[RRPG.StaticConfig] menu build: " + (outcome.Success ? "PASS" : "FAILED " + outcome.Failure));
            if (!outcome.Success) EditorUtility.DisplayDialog("RRPG Static Config", outcome.Failure, "关闭");
        }

        /// <summary>构建（幂等）。force = true 时忽略 content-signature，强制重写。</summary>
        public static BuildOutcome Build(bool force)
        {
            var outcome = new BuildOutcome();

            try
            {
                string mirrorDir = Path.Combine(RepoRoot(), MirrorRelativeDir.Replace('/', Path.DirectorySeparatorChar));
                var mirrorFiles = ReadMirror(mirrorDir, outcome);
                if (!outcome.Success) return outcome;

                // M1：Monster Shadow 内容（缺失 = 合法空态；非法 = 写入前 Hard Error）
                string monsterSha256;
                var monsterDefinitions = ReadMonsterDefinitions(mirrorDir, MirrorRelativeDir, outcome, out monsterSha256);
                if (!outcome.Success) return outcome;

                // M3 Slice 1B：Enemy Presentation 域内容（EnemyPresentation / VisualAsset / AssetManifest 三件套）
                var presentation = ReadPresentationInput(mirrorDir, MirrorRelativeDir, outcome);
                if (!outcome.Success) return outcome;

                // WP-Encounter-1A：Encounter 域内容（Encounter 工作簿 5 张表 + Monster 工作簿 2 张子表）
                var encounter = ReadEncounterInput(mirrorDir, MirrorRelativeDir, monsterDefinitions, outcome);
                if (!outcome.Success) return outcome;

                // WP-Event-1A：Event 域内容（RRPG_Events.xlsx 的 10 张表）
                var events = ReadEventInput(mirrorDir, MirrorRelativeDir, outcome);
                if (!outcome.Success) return outcome;

                var autonomous = AutonomousLifeMirrorReader.Read(mirrorDir);
                var social = SocialMirrorReader.Read(mirrorDir, autonomous);
                var buildings = BuildingMirrorReader.Read(mirrorDir);
                BuildingMirrorReader.ReadFunctions(mirrorDir); // Validate references before publishing generated assets.
                var villagePresentation = VillagePresentationMirrorReader.Read(mirrorDir, buildings, presentation.Assets);
                string signature = ComputeSignature(mirrorFiles, outcome);
                if (!outcome.Success) return outcome;
                outcome.Signature = signature;

                if (!force && IsUpToDate(signature, monsterDefinitions.Count, presentation, encounter, events, autonomous, social, buildings, villagePresentation))
                {
                    outcome.NoOp = true;
                    outcome.Success = true;
                    outcome.Actions.Add("unchanged: content-signature 命中，生成根与当前 Mirror 一致（不触碰任何产物）");
                    return outcome;
                }

                WriteManagedRoot(signature, mirrorFiles, monsterDefinitions, monsterSha256, presentation,
                    encounter, events, autonomous, social, buildings, villagePresentation, outcome);
                return outcome;
            }
            catch (Exception ex)
            {
                outcome.Success = false;
                outcome.Failure = "Builder 异常：" + ex.GetType().Name + ": " + ex.Message
                    + " @ " + FirstStackLine(ex);
                WriteCrashLog(ex);
                return outcome;
            }
        }

        /// <summary>把完整异常（含堆栈）落到本地状态目录，便于诊断（不进 Git）。</summary>
        private static void WriteCrashLog(Exception ex)
        {
            try
            {
                string dir = Path.Combine(RepoRoot(), "Tools", "RRPG.ConfigCompiler", ".build-state");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "builder.crash.txt"),
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                    + "\n" + ex + "\n\n", Utf8NoBom);
            }
            catch { /* 诊断写入失败不得掩盖原始错误 */ }
        }

        private static string FirstStackLine(Exception ex)
        {
            string trace = ex.StackTrace;
            if (string.IsNullOrEmpty(trace)) return "-";

            int cut = trace.IndexOf('\n');
            return (cut > 0 ? trace.Substring(0, cut) : trace).Trim();
        }

        /// <summary>重建：删除整个 Managed Root 后从零生成（FDS §11 fresh rebuild 基础策略）。</summary>
        public static BuildOutcome RebuildFromZero()
        {
            var outcome = new BuildOutcome();
            DeleteManagedRoot(outcome);
            if (!outcome.Success) return outcome;

            var built = Build(true);
            built.Actions.InsertRange(0, outcome.Actions);
            return built;
        }

        // ---------------- 输入读取与校验 ----------------

        /// <summary>
        /// 读取 Mirror 中的 Monster.json（若存在）。
        /// 失败语义：返回 null 并设置 outcome.Failure（Builder 必须在任何写入之前返回）。
        /// 不存在 Monster.json 是**合法**状态（例如 M0 空领域 / Presentation-only 场景）→ 返回空列表。
        /// </summary>
        private static List<MonsterDefinition> ReadMonsterDefinitions(string mirrorDir, string mirrorRelative,
            BuildOutcome outcome, out string mirrorSha256)
        {
            mirrorSha256 = string.Empty;
            string path = Path.Combine(mirrorDir, "Monster.json");
            if (!File.Exists(path)) return new List<MonsterDefinition>();

            string json = File.ReadAllText(path, Utf8NoBom);
            string failure;
            var payload = MonsterMirrorReader.Read(json, out failure);
            if (payload == null)
            {
                outcome.Success = false;
                outcome.Failure = "Monster Mirror 输入非法：" + failure + "（文件：" + mirrorRelative + "/Monster.json）";
                return null;
            }

            mirrorSha256 = payload.SourceSha256 ?? string.Empty;
            outcome.Actions.Add("monster.json: rows=" + payload.Monsters.Count
                + " fields=" + MonsterMirrorReader.ExpectedFields.Length
                + " sourceSha256=" + Short(mirrorSha256));
            return payload.Monsters;
        }

        private static string Short(string value)
        {
            if (string.IsNullOrEmpty(value)) return "-";
            return value.Length <= 12 ? value : value.Substring(0, 12);
        }

        /// <summary>Enemy Presentation 域的读取结果（M3 Slice 1B；Builder 内部输入模型）。</summary>
        private sealed class PresentationInput
        {
            public bool DomainAbsent;

            public List<EnemyPresentationBinding> Bindings = new List<EnemyPresentationBinding>();
            public List<VisualAssetDefinition> Assets = new List<VisualAssetDefinition>();
            public List<AssetManifestEntry> Manifest = new List<AssetManifestEntry>();

            public string PresentationSha256 = string.Empty;
            public string VisualAssetSha256 = string.Empty;
            public string SourceSetSha256 = string.Empty;
        }

        /// <summary>
        /// 读取 M3 Slice 1B 的 Enemy Presentation 三件套：
        ///   EnemyPresentation.json（绑定）/ VisualAsset.json（资源）/ AssetManifest.json（源图 → managed 契约）。
        ///
        /// 缺失语义（合法空态 vs Hard Error）：
        ///   - 三者都不存在 → 该领域尚未迁移：**合法空态**（返回空 input）；
        ///   - VisualAsset.json 与 AssetManifest.json 必须**同时**存在（否则 Mirror 不完整 = Hard Error）；
        ///   - EnemyPresentation.json 存在但 VisualAsset 域缺失 → Hard Error（绑定无解析目标）。
        ///
        /// 失败语义：返回 null 且设置 outcome.Failure（必须在任何写入之前返回）。
        /// </summary>
        private static PresentationInput ReadPresentationInput(string mirrorDir, string mirrorRelative,
            BuildOutcome outcome)
        {
            var input = new PresentationInput();

            string presentationPath = Path.Combine(mirrorDir, EnemyPresentationMirrorReader.FileName);
            string visualAssetPath = Path.Combine(mirrorDir, VisualAssetMirrorReader.FileName);
            string manifestPath = Path.Combine(mirrorDir, AssetManifestReader.FileName);

            bool hasPresentation = File.Exists(presentationPath);
            bool hasVisualAsset = File.Exists(visualAssetPath);
            bool hasManifest = File.Exists(manifestPath);

            if (!hasPresentation && !hasVisualAsset && !hasManifest)
            {
                input.DomainAbsent = true;
                outcome.Actions.Add("enemy presentation: 领域尚未迁移（合法空态）");
                return input;
            }

            if (hasVisualAsset != hasManifest)
            {
                return FailWith(outcome, "Mirror 不完整：" + VisualAssetMirrorReader.FileName + " 与 "
                    + AssetManifestReader.FileName + " 必须同时存在（缺失的是："
                    + (hasVisualAsset ? AssetManifestReader.FileName : VisualAssetMirrorReader.FileName)
                    + "）。请重新运行 Compiler build（RRPG_一键导表.cmd）。");
            }

            if (hasPresentation && !hasVisualAsset)
            {
                return FailWith(outcome, "Mirror 不完整：" + EnemyPresentationMirrorReader.FileName + " 存在但 "
                    + VisualAssetMirrorReader.FileName + " 缺失：绑定无法解析到视觉资源。"
                    + "请重新运行 Compiler build（RRPG_一键导表.cmd）。");
            }

            if (hasPresentation)
            {
                string failure;
                var payload = EnemyPresentationMirrorReader.Read(File.ReadAllText(presentationPath, Utf8NoBom), out failure);
                if (payload == null)
                {
                    return FailWith(outcome, "EnemyPresentation Mirror 输入非法：" + failure
                        + "（文件：" + mirrorRelative + "/" + EnemyPresentationMirrorReader.FileName + "）");
                }

                input.Bindings = payload.Bindings;
                input.PresentationSha256 = payload.SourceSha256;
                outcome.Actions.Add("enemy presentation: bindings=" + input.Bindings.Count
                    + " sourceSha256=" + Short(input.PresentationSha256));
            }

            if (hasVisualAsset)
            {
                string failure;
                var assets = VisualAssetMirrorReader.Read(File.ReadAllText(visualAssetPath, Utf8NoBom), out failure);
                if (assets == null)
                {
                    return FailWith(outcome, "VisualAsset Mirror 输入非法：" + failure
                        + "（文件：" + mirrorRelative + "/" + VisualAssetMirrorReader.FileName + "）");
                }

                var manifest = AssetManifestReader.Read(File.ReadAllText(manifestPath, Utf8NoBom), out failure);
                if (manifest == null)
                {
                    return FailWith(outcome, "AssetManifest 输入非法：" + failure
                        + "（文件：" + mirrorRelative + "/" + AssetManifestReader.FileName + "）");
                }

                input.Assets = assets.Assets;
                input.VisualAssetSha256 = assets.SourceSha256;
                input.Manifest = manifest.Assets;
                input.SourceSetSha256 = manifest.SourceSetSha256;

                if (!CrossCheckPresentationDomain(input, mirrorRelative, outcome)) return null;

                outcome.Actions.Add("visual assets: entries=" + input.Assets.Count
                    + " sourceSetSha256=" + Short(input.SourceSetSha256));
            }

            return input;
        }

        /// <summary>
        /// 读取 WP-Encounter-1A 的 Encounter 领域（7 个 Mirror 文件）。
        ///
        /// 缺失 = **合法空态**（该领域尚未迁移）：返回 DomainAbsent 的 payload，不报错、不写数据。
        /// 非法 / 不完整 = Hard Error（返回 null + outcome.Failure，Builder 必须在任何写入之前返回）。
        /// </summary>
        private static EncounterMirrorReader.Payload ReadEncounterInput(string mirrorDir, string mirrorRelative,
            List<MonsterDefinition> monsterDefinitions, BuildOutcome outcome)
        {
            var monsterIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in monsterDefinitions)
            {
                if (definition != null && !string.IsNullOrEmpty(definition.MonsterId))
                {
                    monsterIds.Add(definition.MonsterId);
                }
            }

            string failure;
            var payload = EncounterMirrorReader.Read(mirrorDir, mirrorRelative, monsterIds, out failure);
            if (payload == null)
            {
                Fail(outcome, "Encounter Mirror 输入非法：" + failure + "（目录：" + mirrorRelative + "）");
                return null;
            }

            if (payload.DomainAbsent)
            {
                outcome.Actions.Add("encounter: 领域尚未迁移（合法空态）");
                return payload;
            }

            outcome.Actions.Add("encounter: encounters=" + payload.Encounters.Count
                + " slots=" + payload.Slots.Count
                + " rules=" + payload.SlotRules.Count
                + " pools=" + payload.Pools.Count
                + " entries=" + payload.PoolEntries.Count
                + " profiles=" + payload.MonsterProfiles.Count
                + " roles=" + payload.MonsterRoles.Count
                + " sourceSha256=" + Short(payload.EncounterSha256));
            return payload;
        }

        /// <summary>
        /// 读取 WP-Event-1A 的 Event 领域（10 个 Mirror 文件）。
        ///
        /// 缺失 = **合法空态**（该领域尚未迁移）：返回 DomainAbsent 的 payload，不报错、不写数据。
        /// 非法 / 不完整 = Hard Error（返回 null + outcome.Failure，Builder 必须在任何写入之前返回）。
        /// </summary>
        private static EventMirrorReader.Payload ReadEventInput(string mirrorDir, string mirrorRelative,
            BuildOutcome outcome)
        {
            string failure;
            var payload = EventMirrorReader.Read(mirrorDir, mirrorRelative, out failure);
            if (payload == null)
            {
                Fail(outcome, "Event Mirror 输入非法：" + failure);
                return null;
            }

            if (payload.DomainAbsent)
            {
                outcome.Actions.Add("event: 领域尚未迁移（合法空态）");
                return payload;
            }

            outcome.Actions.Add("event: events=" + payload.Events.Count
                + " triggers=" + payload.Triggers.Count
                + " roles=" + payload.Roles.Count
                + " roleConditions=" + payload.RoleConditions.Count
                + " conditions=" + payload.Conditions.Count
                + " presentations=" + payload.Presentations.Count
                + " options=" + payload.Options.Count
                + " optionConditions=" + payload.OptionConditions.Count
                + " outcomes=" + payload.Outcomes.Count
                + " effects=" + payload.Effects.Count
                + " sourceSha256=" + Short(payload.EventSha256));
            return payload;
        }

        /// <summary>
        /// 交叉校验 Enemy Presentation 域（全部发生在任何写入之前）。
        /// 失败 = Hard Error：绑定无法解析 / **Missing asset** / Manifest 孤儿条目 /
        /// VisualAsset 与 Manifest 字段不一致 / 源图缺失 / 源图内容与 Mirror 哈希不一致。
        /// </summary>
        private static bool CrossCheckPresentationDomain(PresentationInput input, string mirrorRelative,
            BuildOutcome outcome)
        {
            var assetsById = new Dictionary<string, VisualAssetDefinition>(StringComparer.Ordinal);
            foreach (var asset in input.Assets)
            {
                if (asset != null) assetsById[asset.VisualAssetId] = asset;
            }

            var manifestById = new Dictionary<string, AssetManifestEntry>(StringComparer.Ordinal);
            foreach (var entry in input.Manifest)
            {
                if (entry != null) manifestById[entry.VisualAssetId] = entry;
            }

            // 1. 绑定必须能解析到视觉资源（Compiler 的 ref:VisualAsset 校验之外的第二道防线）
            foreach (var binding in input.Bindings)
            {
                if (!assetsById.ContainsKey(binding.VisualAssetId))
                {
                    Fail(outcome, "绑定 " + binding.MonsterId + " → " + binding.VisualAssetId
                        + " 引用了不存在的 VisualAssetId（来源：" + mirrorRelative + "/"
                        + EnemyPresentationMirrorReader.FileName + "）。");
                    return false;
                }
            }

            // 2. 每个 VisualAsset 必须**恰好**有一条 Manifest 条目（Missing asset 明确失败）
            foreach (var asset in input.Assets)
            {
                AssetManifestEntry entry;
                if (!manifestById.TryGetValue(asset.VisualAssetId, out entry))
                {
                    Fail(outcome, "Missing asset：" + asset.VisualAssetId + " 在 " + AssetManifestReader.FileName
                        + " 中没有对应条目（SourceFile=" + asset.SourceFile + "）。"
                        + "请重新运行 Compiler build：AssetManifest 由 SourceAssets + VisualAsset 表共同生成。");
                    return false;
                }

                if (!string.Equals(entry.SourceFile, asset.SourceFile, StringComparison.Ordinal)
                    || !string.Equals(entry.ImportPreset, asset.ImportPreset, StringComparison.Ordinal))
                {
                    Fail(outcome, "Mirror 不一致：" + asset.VisualAssetId + " 在 " + VisualAssetMirrorReader.FileName
                        + "（SourceFile=" + asset.SourceFile + ", ImportPreset=" + asset.ImportPreset + "）与 "
                        + AssetManifestReader.FileName + "（SourceFile=" + entry.SourceFile + ", ImportPreset="
                        + entry.ImportPreset + "）不一致：请重新运行 Compiler build。");
                    return false;
                }
            }

            // 3. Manifest 不得有 VisualAsset 之外的孤儿条目
            foreach (var entry in input.Manifest)
            {
                if (!assetsById.ContainsKey(entry.VisualAssetId))
                {
                    Fail(outcome, "Mirror 不一致：" + AssetManifestReader.FileName + " 条目 " + entry.VisualAssetId
                        + " 在 " + VisualAssetMirrorReader.FileName + " 中不存在（孤儿条目）：请重新运行 Compiler build。");
                    return false;
                }
            }

            // 4. 源图必须存在，且内容必须与 Mirror 记录的哈希一致（换图 = 先重新 build）
            string sourceRoot = Path.Combine(RepoRoot(),
                SourceAssetsRelativeDir.Replace('/', Path.DirectorySeparatorChar));

            foreach (var asset in input.Assets)
            {
                var entry = manifestById[asset.VisualAssetId];
                string sourceAbsolute = Path.Combine(sourceRoot,
                    entry.SourceFile.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(sourceAbsolute))
                {
                    Fail(outcome, "Missing asset：源图不存在 " + SourceAssetsRelativeDir + "/" + entry.SourceFile
                        + "（VisualAssetId=" + asset.VisualAssetId + "）。请放入源图后重新运行 Compiler build。");
                    return false;
                }

                byte[] sourceBytes = File.ReadAllBytes(sourceAbsolute);
                string actualSha = Sha256Hex(sourceBytes);
                if (sourceBytes.Length != entry.SourceBytes
                    || !string.Equals(actualSha, entry.SourceSha256, StringComparison.Ordinal))
                {
                    Fail(outcome, "源图内容与 Mirror 不一致（换图必须先重新 build）："
                        + SourceAssetsRelativeDir + "/" + entry.SourceFile
                        + "（Mirror 期望 sha256=" + Short(entry.SourceSha256) + "，" + entry.SourceBytes + " bytes；"
                        + "实际 sha256=" + Short(actualSha) + "，" + sourceBytes.Length + " bytes）。"
                        + "请运行 RRPG_一键导表.cmd（Compiler build → Unity Builder）。");
                    return false;
                }
            }

            outcome.Success = true;
            return true;
        }

        /// <summary>失败登记（不抛异常；Builder 必须带明确中文原因返回）。</summary>
        private static void Fail(BuildOutcome outcome, string message)
        {
            outcome.Success = false;
            outcome.Failure = message;
        }

        /// <summary>失败登记 + 返回 null（供返回输入模型的读取方法使用）。</summary>
        private static PresentationInput FailWith(BuildOutcome outcome, string message)
        {
            Fail(outcome, message);
            return null;
        }

        private static List<MirrorFile> ReadMirror(string mirrorDir, BuildOutcome outcome)
        {
            var files = new List<MirrorFile>();

            if (!Directory.Exists(mirrorDir))
            {
                // M0：还没有任何已迁移领域是**合法状态**（FDS §3.2 / §7）→ 允许空 Mirror。
                Directory.CreateDirectory(mirrorDir);
            }

            foreach (string path in Directory.GetFiles(mirrorDir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                string text = File.ReadAllText(path, Utf8NoBom);

                if (!LooksLikeGeneratedMirror(text))
                {
                    outcome.Success = false;
                    outcome.Failure = "Mirror 输入非法：" + name
                        + " 缺少生成标识（_generated.doNotEdit）或 JSON 形状不配平。"
                        + "Mirror 必须由 Compiler 生成（不得人工创建 / 不得手工编辑）。";
                    return files;
                }

                files.Add(new MirrorFile { Name = name, Bytes = File.ReadAllBytes(path) });
            }

            outcome.Success = true;
            return files;
        }

        private static bool LooksLikeGeneratedMirror(string text)
        {
            return text.IndexOf("\"_generated\"", StringComparison.Ordinal) >= 0
                && text.IndexOf("\"doNotEdit\"", StringComparison.Ordinal) >= 0
                && text.IndexOf("\"tool\"", StringComparison.Ordinal) >= 0
                && IsBalanced(text);
        }

        /// <summary>极简形状校验（不引入 JSON 依赖）：花括号与引号必须配平。</summary>
        private static bool IsBalanced(string text)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;

            foreach (char c in text)
            {
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth < 0) return false;
                }
            }

            return depth == 0 && !inString;
        }

        private static string ComputeSignature(List<MirrorFile> mirrorFiles, BuildOutcome outcome)
        {
            var sb = new StringBuilder();
            sb.Append(BuilderVersion).Append('\n');
            sb.Append(RRPGStaticConfigRoot.SupportedFormatVersion).Append('\n');
            foreach (var file in mirrorFiles)
            {
                sb.Append(file.Name).Append('\n').Append(Sha256Hex(file.Bytes)).Append('\n');
            }

            outcome.Success = true;
            return Sha256Hex(Utf8NoBom.GetBytes(sb.ToString()));
        }

        private static bool IsUpToDate(string signature, int expectedMonsterCount, PresentationInput presentation,
            EncounterMirrorReader.Payload encounter, EventMirrorReader.Payload events, AutonomousLifeMirrorReader.Payload autonomous, SocialMirrorReader.Payload social, List<BuildingDefinition> buildings, VillagePresentationMirrorReader.Payload villagePresentation)
        {
            if (!File.Exists(Absolute(RootAssetPath))) return false;
            if (!File.Exists(Absolute(MonsterDatabasePath))) return false;
            if (!File.Exists(Absolute(EnemyPresentationDatabasePath))) return false;
            if (!File.Exists(Absolute(VisualAssetCatalogPath))) return false;
            if (!File.Exists(Absolute(EncounterDatabasePath))) return false;
            if (!File.Exists(Absolute(EventDatabasePath))) return false;

            var root = AssetDatabase.LoadAssetAtPath<RRPGStaticConfigRoot>(RootAssetPath);
            if (root == null) return false;
            if (!string.Equals(root.ContentSignature, signature, StringComparison.Ordinal)) return false;

            // M1：Monster DB 的条数必须与 Mirror 一致（防止「签名一致但内容缺失」的假 no-op）
            var monsters = AssetDatabase.LoadAssetAtPath<MonsterConfigDatabase>(MonsterDatabasePath);
            if (monsters == null || monsters.Count != expectedMonsterCount) return false;

            // M3 Slice 1B：绑定条数 / 资源条数 / 源资产指纹 / 每个资源都必须真的持有 Sprite
            // （否则「签名一致但 Sprite 丢失」会被误判为 no-op）。
            var presentations = AssetDatabase.LoadAssetAtPath<EnemyPresentationDatabase>(EnemyPresentationDatabasePath);
            if (presentations == null || presentations.Count != presentation.Bindings.Count) return false;

            var catalog = AssetDatabase.LoadAssetAtPath<VisualAssetCatalog>(VisualAssetCatalogPath);
            if (catalog == null || catalog.Count != presentation.Assets.Count) return false;
            if (!string.Equals(catalog.SourceSetSha256, presentation.SourceSetSha256, StringComparison.Ordinal)) return false;

            foreach (var asset in catalog.All)
            {
                if (asset == null || asset.Sprite == null) return false;
            }

            // WP-Encounter-1A：Encounter 库的条数必须与 Mirror 一致
            // （否则「签名一致但记录缺失 / 领域被清空」会被误判为 no-op）。
            var encounters = AssetDatabase.LoadAssetAtPath<EncounterConfigDatabase>(EncounterDatabasePath);
            if (encounters == null) return false;
            if (encounters.TotalCount != (encounter == null ? 0 : encounter.TotalCount)) return false;

            // WP-Event-1A：Event 库的条数必须与 Mirror 一致（同一套 no-op 防护）。
            var eventConfig = AssetDatabase.LoadAssetAtPath<EventConfigDatabase>(EventDatabasePath);
            if (eventConfig == null) return false;
            if (eventConfig.TotalCount != (events == null ? 0 : events.TotalCount)) return false;

            var life = AssetDatabase.LoadAssetAtPath<AutonomousLifeConfigDatabase>(DatabasesDir + "/AutonomousLifeConfigDatabase.asset");
            if (life == null || root.AutonomousLifeDatabase != life || life.TotalCount != autonomous.Count) return false;
            var socialDb = AssetDatabase.LoadAssetAtPath<SocialConfigDatabase>(DatabasesDir + "/SocialConfigDatabase.asset");
            if (socialDb == null || root.SocialDatabase != socialDb || socialDb.TotalCount != social.Count) return false;
            var buildingDb=AssetDatabase.LoadAssetAtPath<BuildingConfigDatabase>(DatabasesDir+"/BuildingConfigDatabase.asset");
            if(buildingDb==null || root.BuildingDatabase!=buildingDb || buildingDb.Count!=buildings.Count) return false;
            var villageDb = AssetDatabase.LoadAssetAtPath<VillagePresentationDatabase>(DatabasesDir + "/VillagePresentationDatabase.asset");
            if (villageDb == null || root.VillagePresentationDatabase != villageDb
                || villageDb.PresentationCount != villagePresentation.Presentations.Count || villageDb.BindingCount != villagePresentation.Buildings.Count) return false;
            return true;
        }

        // ---------------- 生成写入（只写 Managed Root 之内） ----------------

        private static void WriteManagedRoot(string signature, List<MirrorFile> mirrorFiles,
            List<MonsterDefinition> monsterDefinitions, string monsterSha256, PresentationInput presentation,
            EncounterMirrorReader.Payload encounter, EventMirrorReader.Payload events, AutonomousLifeMirrorReader.Payload autonomous, SocialMirrorReader.Payload social, List<BuildingDefinition> buildings, VillagePresentationMirrorReader.Payload villagePresentation, BuildOutcome outcome)
        {
            Directory.CreateDirectory(Absolute(ResourcesDir));
            Directory.CreateDirectory(Absolute(DatabasesDir));
            AssetDatabase.Refresh();

            // M3 Slice 1B：源图 → managed Sprite（在写任何 SO 之前完成；失败则 SO 保持原样）。
            var keep = new HashSet<string>(StringComparer.Ordinal)
            {
                DatabasesDir + "/VillagePresentationDatabase.asset",
                RootAssetPath, MonsterDatabasePath, EnemyPresentationDatabasePath, VisualAssetCatalogPath,
                EncounterDatabasePath, EventDatabasePath, DatabasesDir + "/AutonomousLifeConfigDatabase.asset", DatabasesDir + "/SocialConfigDatabase.asset", DatabasesDir+"/BuildingConfigDatabase.asset"
            };

            if (!GenerateManagedSprites(presentation, keep, outcome)) return;

            var monsters = LoadOrCreate<MonsterConfigDatabase>(MonsterDatabasePath, outcome);
            var presentations = LoadOrCreate<EnemyPresentationDatabase>(EnemyPresentationDatabasePath, outcome);
            var catalog = LoadOrCreate<VisualAssetCatalog>(VisualAssetCatalogPath, outcome);
            var encounters = LoadOrCreate<EncounterConfigDatabase>(EncounterDatabasePath, outcome);
            var eventConfig = LoadOrCreate<EventConfigDatabase>(EventDatabasePath, outcome);
            var life = LoadOrCreate<AutonomousLifeConfigDatabase>(DatabasesDir + "/AutonomousLifeConfigDatabase.asset", outcome);
            var socialDb = LoadOrCreate<SocialConfigDatabase>(DatabasesDir + "/SocialConfigDatabase.asset", outcome);
            var buildingDb=LoadOrCreate<BuildingConfigDatabase>(DatabasesDir+"/BuildingConfigDatabase.asset",outcome);
            var villageDb=LoadOrCreate<VillagePresentationDatabase>(DatabasesDir+"/VillagePresentationDatabase.asset",outcome);
            if (!outcome.Success) return;

            // 防御性校验：Unity 对「脚本未绑定」的对象会表现为空引用；
            // 这里显式失败并给出可操作诊断，绝不把空对象写进生成根。
            if (monsters == null || presentations == null || catalog == null || encounters == null
                || eventConfig == null || life == null || socialDb == null || buildingDb==null || villageDb==null)
            {
                outcome.Success = false;
                outcome.Failure = "生成容器资产失败（脚本未绑定？）：请确认每个 ScriptableObject 类型都有同名 .cs 文件。";
                return;
            }

            var root = AssetDatabase.LoadAssetAtPath<RRPGStaticConfigRoot>(RootAssetPath);
            if (root == null)
            {
                root = ScriptableObject.CreateInstance<RRPGStaticConfigRoot>();
                if (root == null)
                {
                    outcome.Success = false;
                    outcome.Failure = "无法创建 RRPGStaticConfigRoot 实例。";
                    return;
                }

                AssetDatabase.CreateAsset(root, RootAssetPath);
                outcome.Actions.Add("created: " + RootAssetPath);
            }
            else
            {
                outcome.Actions.Add("updated: " + RootAssetPath);
            }

            root.ApplyGeneratedContent(
                RRPGStaticConfigRoot.SupportedFormatVersion, signature, monsters, presentations, catalog, encounters,
                eventConfig, life, socialDb, buildingDb, villageDb);

            // M1：Monster Shadow 定义写入（生成物；产品 Gameplay 不消费）
            monsters.ApplyGeneratedContent(RRPGStaticConfigRoot.SupportedFormatVersion, monsterSha256,
                monsterDefinitions);
            outcome.Actions.Add("monster database: entries=" + monsterDefinitions.Count);

            // M3 Slice 1B：Enemy Presentation 域写入（生成物；产品 Gameplay 不消费）
            presentations.ApplyGeneratedContent(RRPGStaticConfigRoot.SupportedFormatVersion,
                presentation.PresentationSha256, presentation.Bindings);
            outcome.Actions.Add("enemy presentation database: entries=" + presentation.Bindings.Count);

            catalog.ApplyGeneratedContent(RRPGStaticConfigRoot.SupportedFormatVersion,
                presentation.VisualAssetSha256, presentation.SourceSetSha256, presentation.Assets);
            outcome.Actions.Add("visual asset catalog: entries=" + presentation.Assets.Count);

            // WP-Encounter-1A：Encounter 领域写入（生成物；产品 Gameplay 本轮不消费）
            encounters.ApplyGeneratedContent(RRPGStaticConfigRoot.SupportedFormatVersion,
                encounter.EncounterSha256, encounter.MonsterProfileSha256, encounter.MonsterRoleSha256,
                encounter.Encounters, encounter.Slots, encounter.SlotRules, encounter.Pools,
                encounter.PoolEntries, encounter.MonsterProfiles, encounter.MonsterRoles);
            outcome.Actions.Add("encounter database: encounters=" + encounter.Encounters.Count
                + " slots=" + encounter.Slots.Count
                + " rules=" + encounter.SlotRules.Count
                + " pools=" + encounter.Pools.Count
                + " entries=" + encounter.PoolEntries.Count
                + " profiles=" + encounter.MonsterProfiles.Count
                + " roles=" + encounter.MonsterRoles.Count);

            // WP-Event-1A：Event 领域写入（生成物；产品 Gameplay 本轮不消费）
            eventConfig.ApplyGeneratedContent(RRPGStaticConfigRoot.SupportedFormatVersion,
                events.EventSha256,
                events.Events, events.Triggers, events.Roles, events.RoleConditions, events.Conditions,
                events.Presentations, events.Options, events.OptionConditions, events.Outcomes, events.Effects);
            outcome.Actions.Add("event database: events=" + events.Events.Count
                + " triggers=" + events.Triggers.Count
                + " roles=" + events.Roles.Count
                + " roleConditions=" + events.RoleConditions.Count
                + " conditions=" + events.Conditions.Count
                + " presentations=" + events.Presentations.Count
                + " options=" + events.Options.Count
                + " optionConditions=" + events.OptionConditions.Count
                + " outcomes=" + events.Outcomes.Count
                + " effects=" + events.Effects.Count);

            if (root == null)
            {
                outcome.Success = false;
                outcome.Failure = "生成 Root 资产失败（脚本未绑定？）。";
                return;
            }

            life.ApplyGeneratedContent(autonomous.BehaviorChannel,autonomous.PersonBehavior,autonomous.PersonPreference,autonomous.Place,autonomous.Activity,autonomous.ActivityProvider,autonomous.ActivityBehaviorBinding,autonomous.ActivityPreferenceBinding,autonomous.ActivityRelationshipBinding,autonomous.ActivityContextBinding,autonomous.ActivityRepetition,autonomous.AutonomousLifeCalibration, autonomous.ActivitySocialProjection);
            socialDb.ApplyGeneratedContent(social.FactTypes, social.Rules, social.Calibration);
            buildingDb.ApplyGeneratedContent(buildings);
            buildingDb.ApplyGeneratedFunctions(BuildingMirrorReader.ReadFunctions(Path.Combine(RepoRoot(), MirrorRelativeDir)));
            EditorUtility.SetDirty(buildingDb);
            villageDb.ApplyGeneratedContent(villagePresentation.Presentations, villagePresentation.Buildings);
            EditorUtility.SetDirty(villageDb);
            EditorUtility.SetDirty(socialDb);
            EditorUtility.SetDirty(life);
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(monsters);
            EditorUtility.SetDirty(presentations);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(encounters);
            EditorUtility.SetDirty(eventConfig);
            AssetDatabase.SaveAssets();

            DeleteStaleManagedAssets(keep, outcome);
            AssetDatabase.Refresh();

            outcome.Success = true;
            outcome.Actions.Add("signature: " + signature);
            outcome.Actions.Add("mirror files: " + mirrorFiles.Count);
        }

        private static T LoadOrCreate<T>(string assetPath, BuildOutcome outcome) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            if (asset == null)
            {
                outcome.Success = false;
                outcome.Failure = "无法创建 Unity 资产实例：" + typeof(T).FullName
                    + "（请确认该类型在 Runtime assembly 中可实例化）。";
                return null;
            }

            AssetDatabase.CreateAsset(asset, assetPath);
            if (AssetDatabase.LoadAssetAtPath<T>(assetPath) == null)
            {
                outcome.Success = false;
                outcome.Failure = "创建资产失败：" + assetPath + "（AssetDatabase 未登记该路径）。";
                return null;
            }

            outcome.Actions.Add("created: " + assetPath);
            return asset;
        }

        /// <summary>
        /// 源图 → managed Sprite（M3 Slice 1B）。
        ///
        /// 行为（确定性）：
        ///   1. 目标路径 = AssetManifest 的 ManagedAssetPath（**契约**；Builder 不得自行推导）；
        ///   2. 文件内容 = 源图字节**逐字节复制**（不重编码、不注入元数据）；
        ///   3. 导入设置 = ImportPreset（<c>sprite_point_100</c> → Sprite / Single / Point / 100 PPU / Uncompressed / Center）；
        ///   4. 文件内容与导入设置都不变时**不触碰**该文件（保持 mtime，配合 content-signature no-op）。
        ///
        /// 失败 = Hard Error（返回 false）：源图缺失 / 取得不了 TextureImporter / 导入后拿不到 Sprite。
        /// </summary>
        private static bool GenerateManagedSprites(PresentationInput presentation, ISet<string> keep,
            BuildOutcome outcome)
        {
            if (presentation == null || presentation.Manifest.Count == 0) return true;

            string sourceRoot = Path.Combine(RepoRoot(),
                SourceAssetsRelativeDir.Replace('/', Path.DirectorySeparatorChar));

            foreach (var entry in presentation.Manifest)
            {
                string sourceAbsolute = Path.Combine(sourceRoot,
                    entry.SourceFile.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(sourceAbsolute))
                {
                    Fail(outcome, "Missing asset：源图不存在 " + SourceAssetsRelativeDir + "/" + entry.SourceFile + "。");
                    return false;
                }

                byte[] sourceBytes = File.ReadAllBytes(sourceAbsolute);
                string managedAbsolute = Absolute(entry.ManagedAssetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(managedAbsolute));

                bool fileChanged = !File.Exists(managedAbsolute)
                    || !BytesEqual(File.ReadAllBytes(managedAbsolute), sourceBytes);

                if (fileChanged) File.WriteAllBytes(managedAbsolute, sourceBytes);

                AssetDatabase.ImportAsset(entry.ManagedAssetPath, ImportAssetOptions.ForceSynchronousImport);

                bool presetChanged = ApplyImportPreset(entry.ManagedAssetPath, entry.ImportPreset, outcome);
                if (!outcome.Success) return false;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.ManagedAssetPath);
                if (sprite == null)
                {
                    Fail(outcome, "managed Sprite 生成失败：" + entry.ManagedAssetPath + "（源图 " + entry.SourceFile
                        + " 无法作为 Sprite 导入：请检查图片格式与导入预设）。");
                    return false;
                }

                keep.Add(entry.ManagedAssetPath);

                var definition = FindAsset(presentation.Assets, entry.VisualAssetId);
                if (definition != null)
                {
                    definition.SourceSha256 = entry.SourceSha256;
                    definition.Sprite = sprite;
                }

                outcome.Actions.Add((fileChanged || presetChanged ? "managed asset(updated): " : "managed asset(unchanged): ")
                    + entry.ManagedAssetPath + " preset=" + entry.ImportPreset);
            }

            return true;
        }

        private static VisualAssetDefinition FindAsset(List<VisualAssetDefinition> assets, string visualAssetId)
        {
            foreach (var asset in assets)
            {
                if (asset != null && string.Equals(asset.VisualAssetId, visualAssetId, StringComparison.Ordinal))
                    return asset;
            }
            return null;
        }

        private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

        /// <summary>
        /// 应用导入预设（M3 只支持 <c>sprite_point_100</c>：
        /// Sprite / Single / Point / 100 PPU / Uncompressed / 无 mipmap / AlphaIsTransparency / Center 轴心 / Clamp）。
        ///
        /// 返回 true = 确实改动过设置（已触发 reimport）；false = 设置本就一致（**不触碰**资产）。
        /// </summary>
        private static bool ApplyImportPreset(string projectRelativePath, string preset, BuildOutcome outcome)
        {
            if (!VisualAssetMirrorReader.IsSupportedPreset(preset))
            {
                Fail(outcome, "ImportPreset 不受支持：" + preset + "（当前只支持 "
                    + VisualAssetMirrorReader.SpritePoint100Preset + "）。");
                return false;
            }

            var importer = AssetImporter.GetAtPath(projectRelativePath) as TextureImporter;
            if (importer == null)
            {
                Fail(outcome, "无法取得 TextureImporter：" + projectRelativePath + "（该文件未被 Unity 识别为纹理）。");
                return false;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, SpritePresetPixelsPerUnit))
            {
                importer.spritePixelsPerUnit = SpritePresetPixelsPerUnit;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (importer.spriteImportMode == SpriteImportMode.Single)
            {
                Vector2 pivot = importer.spritePivot;
                if (pivot.x != CenterPivot.x || pivot.y != CenterPivot.y)
                {
                    importer.spritePivot = CenterPivot;
                    changed = true;
                }
            }

            if (changed) importer.SaveAndReimport();
            return changed;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// 只清理 **Managed Root 之内**、不属于预期集合的陈旧产物。
        /// 归属保护：任何位于根外的路径一律跳过（Builder 不得删出 Assets/Generated/StaticConfig）。
        /// </summary>
        private static void DeleteStaleManagedAssets(ISet<string> keep, BuildOutcome outcome)
        {
            keep = keep ?? new HashSet<string>(StringComparer.Ordinal);

            string fullRoot = Absolute(ManagedRoot).Replace('\\', '/').TrimEnd('/');
            string rootDir = Directory.Exists(Absolute(ManagedRoot)) ? Absolute(ManagedRoot) : null;
            if (rootDir == null) return;

            foreach (string file in Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                string full = Path.GetFullPath(file).Replace('\\', '/');
                if (!full.StartsWith(fullRoot + "/", StringComparison.OrdinalIgnoreCase)) continue;   // 归属保护

                string projectRelative = ManagedRoot + full.Substring(fullRoot.Length);

                if (keep.Contains(projectRelative)) continue;
                if (projectRelative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    string owner = projectRelative.Substring(0, projectRelative.Length - ".meta".Length);
                    if (keep.Contains(owner)) continue;

                    // 目录自身的 .meta 不是产物（由 Unity 管理；删掉会造成 GUID churn 与无意义 diff）→ 跳过。
                    if (Directory.Exists(Absolute(owner))) continue;
                }

                File.Delete(file);
                outcome.Actions.Add("deleted(stale): " + projectRelative);
            }

            foreach (string dir in Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
                else
                {
                    bool onlyMeta = Directory.GetFiles(dir).All(f => f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        && Directory.GetDirectories(dir).Length == 0;
                    if (onlyMeta) { foreach (string m in Directory.GetFiles(dir)) File.Delete(m); Directory.Delete(dir); }
                }
            }
        }

        /// <summary>删除整个 Managed Root（归属保护：只接受精确的 ManagedRoot 常量）。</summary>
        private static void DeleteManagedRoot(BuildOutcome outcome)
        {
            if (ManagedRoot != "Assets/Generated/StaticConfig" || ManagedRoot.Contains(".."))
            {
                outcome.Success = false;
                outcome.Failure = "归属保护：拒绝删除非 Managed Root 路径。";
                return;
            }

            AssetDatabase.DeleteAsset(ManagedRoot);
            AssetDatabase.Refresh();

            outcome.Success = true;
            outcome.Actions.Add("deleted root: " + ManagedRoot);
        }

        // ---------------- headless 入口（Producer 一键导表调用） ----------------

        /// <summary>batchmode 入口：构建 + 写状态文件。</summary>
        public static void ExecuteBuild()
        {
            Exit(Build(false), "build");
        }

        /// <summary>batchmode 入口：删除生成根后重建（Fresh Rebuild 基础策略验证）。</summary>
        public static void ExecuteRebuildFromZero()
        {
            Exit(RebuildFromZero(), "rebuild-from-zero");
        }

        private static void Exit(BuildOutcome outcome, string stage)
        {
            foreach (string action in outcome.Actions) Debug.Log("[RRPG.StaticConfig] " + action);

            if (outcome.Success) Debug.Log("[RRPG.StaticConfig] " + stage + " PASS (noOp=" + outcome.NoOp + ")");
            else Debug.LogError("[RRPG.StaticConfig] " + stage + " FAILED: " + outcome.Failure);

            WriteStageFile(outcome, stage);
            AssetDatabase.SaveAssets();
        }

        private static void WriteStageFile(BuildOutcome outcome, string stage)
        {
            try
            {
                string dir = Path.Combine(RepoRoot(), "Tools", "RRPG.ConfigCompiler", ".build-state");
                Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.Append("stage=").Append(stage).Append('\n');
                sb.Append("result=").Append(outcome.Success ? "PASS" : "FAIL").Append('\n');
                sb.Append("noOp=").Append(outcome.NoOp ? "true" : "false").Append('\n');
                sb.Append("signature=").Append(outcome.Signature).Append('\n');
                sb.Append("failure=").Append(string.IsNullOrEmpty(outcome.Failure) ? "-" : outcome.Failure).Append('\n');
                foreach (string action in outcome.Actions) sb.Append("action=").Append(action).Append('\n');

                File.WriteAllText(Path.Combine(dir, "builder.stage.txt"), sb.ToString(), Utf8NoBom);
            }
            catch (Exception ex)
            {
                Debug.LogError("[RRPG.StaticConfig] 写状态文件失败：" + ex.Message);
            }
        }

        // ---------------- 路径 / 哈希工具 ----------------

        /// <summary>仓库根：&lt;repo&gt;/UnityProject/RRPG1.0game/Assets → 上溯三层。</summary>
        public static string RepoRoot()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
        }

        /// <summary>项目相对路径（Assets/...）→ 绝对路径。</summary>
        public static string Absolute(string projectRelativePath)
        {
            string rest = projectRelativePath.StartsWith("Assets/", StringComparison.Ordinal)
                ? projectRelativePath.Substring("Assets/".Length)
                : projectRelativePath;
            return Path.Combine(Application.dataPath, rest.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return ToHex(sha.ComputeHash(bytes));
        }

        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private sealed class MirrorFile
        {
            public string Name;
            public byte[] Bytes;
        }
    }
}
