namespace RRPG.StaticConfig
{
    /// <summary>
    /// Runtime 侧**稳定加载层**（FDS §3.3 第 3/4 条）：
    ///   - Gameplay **不得**知道 Resources 路径；路径知识只存在于 Root 与本类；
    ///   - 初始化**幂等**：重复调用返回同一结果，不重复加载；
    ///   - 失败给出**明确诊断**（不静默、不猜、不返回 null 假装成功）。
    ///
    /// M0：只提供「Root 是否可用」。
    /// **M2 Slice 1**：新增 Monster 领域访问入口 <see cref="TryGetMonsters"/> / <see cref="Monsters"/>，
    /// 目标链 = Root → MonsterConfigDatabase → MonsterRegistry → MonsterDefinition。
    /// 本类只负责「取得 Registry」；Battle / Expedition / UI 的消费者切换属 M2 Slice 2，不在本类实现。
    /// **M4（Monster Legacy Physical Retirement，FDS §10.5）**：D-M2-1 的 Disposable Legacy 回退
    /// （`LegacyMonsterFallbackAdapter`）已退役。Root / MonsterConfigDatabase 不可用时**明确失败**
    /// （返回 false + 可操作诊断），**不**回退旧 Monster 数据、**不**静默成功；Root 存在但内容非法时同样明确失败。
    ///
    /// 硬约束：不引用 UnityEditor / AssetDatabase（Runtime asmdef 无 Editor 依赖）；
    /// 不依赖场景、不依赖 FindObjectOfType。
    /// </summary>
    public static class StaticConfigRegistry
    {
        /// <summary>Root 缺失时的明确诊断（制作人可操作：先跑一键导表）。</summary>
        public const string MissingRootDiagnostic =
            "StaticConfig Root 未找到：Assets/Generated/StaticConfig 尚未生成（或未导入）。"
            + "请先运行 RRPG_一键导表.cmd（exact path = RRPGStaticConfigRoot）。";

        /// <summary>Root 存在但未挂 Monster 库时的明确诊断（生成根不完整）。</summary>
        public const string MissingMonsterDatabaseDiagnostic =
            "StaticConfig Root 存在，但未包含 MonsterConfigDatabase：生成根不完整。"
            + "请重新运行 RRPG_一键导表.cmd（compiler build → Unity Builder）。";

        /// <summary>Root 存在但 Monster 内容非法时的诊断前缀（Authority 可用 → **不做** Legacy 回退）。</summary>
        public const string MonstersInvalidDiagnostic =
            "Monster 定义非法（Root 存在，按 Authority 语义不回退 Legacy）：";

        /// <summary>Monster 访问入口不可用时的兜底诊断（正常路径不应出现）。</summary>
        public const string MonstersUnavailableDiagnostic =
            "Monster 定义不可用：MonsterRegistry 未能建立。";

        /// <summary>M4：Legacy 回退退役说明（拼在 Root / 库不可用诊断之后，明确「不再回退旧数据」）。</summary>
        public const string LegacyFallbackRetiredNote =
            "（M4：Legacy Monster 回退已退役，不再回退旧 Monster 静态数据。修复生成根后重试。）";

        // ---------------- M3 Slice 1C：Enemy Presentation 领域 ----------------

        /// <summary>Root 存在但未挂 EnemyPresentation 库时的明确诊断（生成根不完整）。</summary>
        public const string MissingEnemyPresentationDatabaseDiagnostic =
            "StaticConfig Root 存在，但未包含 EnemyPresentationDatabase：生成根不完整。"
            + "请重新运行 RRPG_一键导表.cmd（compiler build → Unity Builder）。";

        /// <summary>Root 存在但未挂 VisualAssetCatalog 时的明确诊断（生成根不完整）。</summary>
        public const string MissingVisualAssetCatalogDiagnostic =
            "StaticConfig Root 存在，但未包含 VisualAssetCatalog：生成根不完整。"
            + "请重新运行 RRPG_一键导表.cmd（compiler build → Unity Builder）。";

        /// <summary>Enemy Presentation 领域可用但为空（Mirror 中该领域尚未迁移 / 没有绑定）。</summary>
        public const string EnemyPresentationEmptyDiagnostic =
            "Enemy Presentation 领域可用但没有任何绑定：请检查 Development/Config/RRPG_EnemyPresentation.xlsx"
            + " 的 EnemyPresentation 表，并重新运行 RRPG_一键导表.cmd。";

        /// <summary>VisualAsset 领域可用但为空（Mirror 中该领域尚未迁移 / 没有资源）。</summary>
        public const string VisualAssetsEmptyDiagnostic =
            "VisualAsset 领域可用但没有任何资源：请检查 Development/Config/RRPG_EnemyPresentation.xlsx"
            + " 的 VisualAsset 表，并重新运行 RRPG_一键导表.cmd。";

        /// <summary>Enemy Presentation 入口不可用时的兜底诊断（正常路径不应出现）。</summary>
        public const string EnemyPresentationUnavailableDiagnostic =
            "Enemy Presentation 不可用：EnemyPresentationDatabase 未能取得。";

        /// <summary>VisualAsset 入口不可用时的兜底诊断（正常路径不应出现）。</summary>
        public const string VisualAssetsUnavailableDiagnostic =
            "VisualAsset 不可用：VisualAssetCatalog 未能取得。";

        // ---------------- WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础） ----------------

        /// <summary>Root 存在但未挂 Encounter 库时的明确诊断（生成根不完整）。</summary>
        public const string MissingEncounterDatabaseDiagnostic =
            "StaticConfig Root 存在，但未包含 EncounterConfigDatabase：生成根不完整。"
            + "请重新运行 RRPG_一键导表.cmd（compiler build → Unity Builder）。";

        /// <summary>Encounter 领域可用但为空（Mirror 中该领域尚未建立 / 没有记录）。</summary>
        public const string EncounterConfigEmptyDiagnostic =
            "Encounter 领域可用但没有任何记录：请检查 Development/Config/RRPG_Encounters.xlsx"
            + " 以及 RRPG_Monsters.xlsx 的 MonsterEncounterProfile / MonsterRole 表，"
            + "并重新运行 RRPG_一键导表.cmd。";

        /// <summary>Encounter 入口不可用时的兜底诊断（正常路径不应出现）。</summary>
        public const string EncounterConfigUnavailableDiagnostic =
            "Encounter 静态配置不可用：EncounterConfigDatabase 未能取得。";

        // ---------------- WP-Event-1A：Event 领域（统一事件内容基础） ----------------

        /// <summary>Root 存在但未挂 Event 库时的明确诊断（生成根不完整）。</summary>
        public const string MissingEventDatabaseDiagnostic =
            "StaticConfig Root 存在，但未包含 EventConfigDatabase：生成根不完整。"
            + "请重新运行 RRPG_一键导表.cmd（compiler build → Unity Builder）。";

        /// <summary>Event 领域可用但为空（Mirror 中该领域尚未建立 / 没有记录）。</summary>
        public const string EventConfigEmptyDiagnostic =
            "Event 领域可用但没有任何记录：请检查 Development/Config/RRPG_Events.xlsx 的十张表，"
            + "并重新运行 RRPG_一键导表.cmd。";

        /// <summary>Event 入口不可用时的兜底诊断（正常路径不应出现）。</summary>
        public const string EventConfigUnavailableDiagnostic =
            "Event 静态配置不可用：EventConfigDatabase 未能取得。";

        private static RRPGStaticConfigRoot _root;
        private static bool _initialized;
        private static string _diagnostic = string.Empty;

        private static bool _monstersResolved;
        private static MonsterRegistry _monsters;
        private static string _monsterDiagnostic = string.Empty;

        // M3 Slice 1C：Enemy Presentation 领域（懒解析一次；两个领域在同一趟解析里取得）
        private static bool _presentationResolved;
        private static EnemyPresentationDatabase _presentations;
        private static VisualAssetCatalog _visualAssets;
        private static string _presentationDiagnostic = string.Empty;
        private static string _visualAssetDiagnostic = string.Empty;

        // WP-Encounter-1A：Encounter 领域（懒解析一次）
        private static bool _encounterResolved;
        private static EncounterConfigDatabase _encounterConfig;
        private static string _encounterDiagnostic = string.Empty;

        // WP-Event-1A：Event 领域（懒解析一次）
        private static bool _eventResolved;
        private static EventConfigDatabase _eventConfig;
        private static string _eventDiagnostic = string.Empty;

        public static bool IsInitialized { get { return _initialized; } }
        public static bool TryGetVillagePresentation(out VillagePresentationDatabase database, out string diagnostic)
        {
            database = null;
            if (!TryInitialize(out diagnostic)) return false;
            database = _root.VillagePresentationDatabase;
            diagnostic = database == null ? "VillagePresentationDatabase 缺失，请检查 RRPG_VillagePresentation.xlsx 并重新导表。" : "";
            return database != null;
        }

        /// <summary>已加载的 Root；未初始化或缺失时为 null（先调用 TryInitialize）。</summary>
        public static RRPGStaticConfigRoot Root { get { return _root; } }

        /// <summary>最近一次初始化的诊断（成功时为空字符串）。</summary>
        public static string Diagnostic { get { return _diagnostic; } }
        public static bool TryGetBuildings(out BuildingRegistry buildings,out string diagnostic)
        {
            buildings=null;
            if(!TryInitialize(out diagnostic)) return false;
            if(_root.BuildingDatabase==null) { diagnostic="BuildingConfigDatabase 缺失，请重新一键导表。";return false; }
            try { buildings=_root.BuildingDatabase.CreateRegistry(); }
            catch(System.Exception ex) {diagnostic="Building 定义非法："+ex.Message;return false;}
            if(buildings.All.Count==0) {buildings=null;diagnostic="Building Catalog 为空，请检查 RRPG_Buildings.xlsx 并一键导表。";return false;}
            diagnostic="";return true;
        }

        /// <summary>
        /// 初始化（幂等）。返回 false 时 <paramref name="diagnostic"/> 必为明确的、可操作的中文诊断。
        /// </summary>
        public static bool TryInitialize(out string diagnostic)
        {
            if (!_initialized)
            {
                _root = RRPGStaticConfigRoot.Load();
                _diagnostic = _root == null ? MissingRootDiagnostic : string.Empty;
                _initialized = true;
            }

            diagnostic = _diagnostic;
            return _root != null;
        }

        /// <summary>
        /// 清空缓存，使下一次 TryInitialize / TryGetMonsters 重新解析。
        /// 用途：测试，以及 Builder 重建生成根之后（Runtime 长驻进程需要重新取）。
        /// 同时清空 Monster 领域缓存（M4 起不再有 Legacy 回退状态）。
        /// </summary>
        public static void Reset()
        {
            _root = null;
            _initialized = false;
            _diagnostic = string.Empty;

            _monstersResolved = false;
            _monsters = null;
            _monsterDiagnostic = string.Empty;

            // M3 Slice 1C：Enemy Presentation 领域缓存 + Resolver 的 WarnOnce 一并清空。
            _presentationResolved = false;
            _presentations = null;
            _visualAssets = null;
            _presentationDiagnostic = string.Empty;
            _visualAssetDiagnostic = string.Empty;
            EnemyPresentationResolver.ResetWarnOnce();

            // WP-Encounter-1A：Encounter 领域缓存一并清空。
            _encounterResolved = false;
            _encounterConfig = null;
            _encounterDiagnostic = string.Empty;

            // WP-Event-1A：Event 领域缓存一并清空。
            _eventResolved = false;
            _eventConfig = null;
            _eventDiagnostic = string.Empty;
        }

        // ---------------- M2 Slice 1：Monster 领域访问入口 ----------------

        /// <summary>
        /// **M2 Slice 1 唯一 Monster 领域入口**（纯 Runtime；无 Editor 依赖；幂等）。
        ///
        /// 成功（true，registry 非 null）：正式路径（Root 可用且内容合法）→ <paramref name="diagnostic"/> 为空字符串。
        /// 失败（false，registry = null）：Root / MonsterConfigDatabase 不可用，或 Root 存在但内容非法；
        /// <paramref name="diagnostic"/> 必为明确中文诊断（M4 起**不再**回退 Legacy 旧 Monster 数据）。
        /// </summary>
        public static bool TryGetMonsters(out MonsterRegistry registry, out string diagnostic)
        {
            ResolveMonstersOnce();
            registry = _monsters;
            diagnostic = _monsterDiagnostic;
            return registry != null;
        }

        /// <summary>
        /// Monster Registry 只读入口（首次访问即解析，幂等）。
        /// 不可用时返回 null —— 调用方**必须**同时读取 <see cref="MonsterDiagnostic"/> 并显式处理，不得假装成功。
        /// </summary>
        public static MonsterRegistry Monsters
        {
            get
            {
                ResolveMonstersOnce();
                return _monsters;
            }
        }

        /// <summary>Monster 解析诊断：正式路径为空字符串；回退 / 失败时非空（可直接展示给制作人）。</summary>
        public static string MonsterDiagnostic
        {
            get
            {
                ResolveMonstersOnce();
                return _monsterDiagnostic;
            }
        }

        // ---------------- M3 Slice 1C：Enemy Presentation / VisualAsset 领域访问入口 ----------------

        /// <summary>
        /// **M3 Slice 1C 唯一 Enemy Presentation 领域入口**（纯 Runtime；无 Editor 依赖；lazy；幂等）。
        ///
        /// 成功（true，<paramref name="database"/> 非 null）：
        ///   - 领域有内容 → <paramref name="diagnostic"/> 为空字符串；
        ///   - 领域可用但没有绑定（尚未迁移 / 表为空）→ **合法状态** + 明确诊断（绝不静默）。
        /// 失败（false，database = null）：Root 缺失 / Root 存在但未包含该库 → <paramref name="diagnostic"/>
        /// 必为明确、可操作的中文诊断。
        ///
        /// 说明：本领域**没有** Legacy 回退（Legacy 产品里不存在「每只怪物用哪张图」的事实）；
        /// 显示侧的回退由 <see cref="EnemyPresentationResolver"/> 统一负责（业务层不得判断）。
        /// </summary>
        public static bool TryGetEnemyPresentation(out EnemyPresentationDatabase database, out string diagnostic)
        {
            ResolvePresentationOnce();
            database = _presentations;
            diagnostic = _presentationDiagnostic;
            return database != null;
        }

        /// <summary>Enemy Presentation 只读入口（lazy；不可用时返回 null —— 调用方必须同时读 <see cref="EnemyPresentationDiagnostic"/>）。</summary>
        public static EnemyPresentationDatabase EnemyPresentation
        {
            get
            {
                ResolvePresentationOnce();
                return _presentations;
            }
        }

        /// <summary>Enemy Presentation 解析诊断：正式且非空时为空字符串；否则为明确中文诊断。</summary>
        public static string EnemyPresentationDiagnostic
        {
            get
            {
                ResolvePresentationOnce();
                return _presentationDiagnostic;
            }
        }

        /// <summary>
        /// **M3 Slice 1C 唯一 VisualAsset 领域入口**（纯 Runtime；无 Editor 依赖；lazy；幂等）。
        /// 成功语义与 <see cref="TryGetEnemyPresentation"/> 一致（空领域 = 合法状态 + 明确诊断）。
        /// </summary>
        public static bool TryGetVisualAssets(out VisualAssetCatalog catalog, out string diagnostic)
        {
            ResolvePresentationOnce();
            catalog = _visualAssets;
            diagnostic = _visualAssetDiagnostic;
            return catalog != null;
        }

        /// <summary>VisualAsset 只读入口（lazy；不可用时返回 null —— 调用方必须同时读 <see cref="VisualAssetDiagnostic"/>）。</summary>
        public static VisualAssetCatalog VisualAssets
        {
            get
            {
                ResolvePresentationOnce();
                return _visualAssets;
            }
        }

        /// <summary>VisualAsset 解析诊断：正式且非空时为空字符串；否则为明确中文诊断。</summary>
        public static string VisualAssetDiagnostic
        {
            get
            {
                ResolvePresentationOnce();
                return _visualAssetDiagnostic;
            }
        }

        // ---------------- WP-Encounter-1A：Encounter 领域访问入口 ----------------

        /// <summary>
        /// **Encounter 领域唯一入口**（纯 Runtime；无 Editor 依赖；lazy；幂等）。
        ///
        /// 成功（true，<paramref name="database"/> 非 null）：
        ///   - 领域有记录 → <paramref name="diagnostic"/> 为空字符串；
        ///   - 领域可用但没有记录（尚未建立 / 表为空）→ **合法状态** + 明确诊断（绝不静默）。
        /// 失败（false，database = null）：Root 缺失 / Root 存在但未包含该库 → 明确可操作诊断。
        ///
        /// 边界：本入口**只读**。它不生成敌人、不做威胁预算求解、不接 Battle / Prep（属后续技术检查点）。
        /// </summary>
        public static bool TryGetEncounterConfig(out EncounterConfigDatabase database, out string diagnostic)
        {
            ResolveEncounterOnce();
            database = _encounterConfig;
            diagnostic = _encounterDiagnostic;
            return database != null;
        }

        /// <summary>Encounter 只读入口（lazy；不可用时返回 null —— 调用方必须同时读 <see cref="EncounterConfigDiagnostic"/>）。</summary>
        public static EncounterConfigDatabase EncounterConfig
        {
            get
            {
                ResolveEncounterOnce();
                return _encounterConfig;
            }
        }

        /// <summary>Encounter 解析诊断：正式且非空时为空字符串；否则为明确中文诊断。</summary>
        public static string EncounterConfigDiagnostic
        {
            get
            {
                ResolveEncounterOnce();
                return _encounterDiagnostic;
            }
        }

        /// <summary>Encounter 领域的**一次性懒解析**（Root → 库 → 诊断）。不做任何回退。</summary>
        private static void ResolveEncounterOnce()
        {
            if (_encounterResolved) return;
            _encounterResolved = true;

            string rootDiagnostic;
            if (!TryInitialize(out rootDiagnostic))
            {
                _encounterConfig = null;
                _encounterDiagnostic = rootDiagnostic;
                return;
            }

            EncounterConfigDatabase resolved = _root == null ? null : _root.EncounterDatabase;

            // Unity 的「资产被删除 / 引用未绑定」表现为 fake-null：它满足 Unity 的 == null，但不是 CLR null。
            // 这里统一规范化成**真正 null**，避免调用方（含纯 CLR 断言）看到两种「不可用」语义。
            _encounterConfig = (resolved == null) ? null : resolved;

            _encounterDiagnostic = _encounterConfig == null
                ? MissingEncounterDatabaseDiagnostic
                : (_encounterConfig.TotalCount == 0 ? EncounterConfigEmptyDiagnostic : string.Empty);
        }

        /// <summary>
        /// Enemy Presentation 领域的**一次性懒解析**（Root → 两个库 → 诊断）。
        /// 不做 Legacy 回退：领域缺失就是缺失，由 Resolver 负责显示侧 fallback。
        /// </summary>
        private static void ResolvePresentationOnce()
        {
            if (_presentationResolved) return;
            _presentationResolved = true;

            string rootDiagnostic;
            if (!TryInitialize(out rootDiagnostic))
            {
                _presentations = null;
                _visualAssets = null;
                _presentationDiagnostic = rootDiagnostic;
                _visualAssetDiagnostic = rootDiagnostic;
                return;
            }

            _presentations = _root == null ? null : _root.EnemyPresentationDatabase;
            _presentationDiagnostic = _presentations == null
                ? MissingEnemyPresentationDatabaseDiagnostic
                : (_presentations.Count == 0 ? EnemyPresentationEmptyDiagnostic : string.Empty);

            _visualAssets = _root == null ? null : _root.VisualAssetCatalog;
            _visualAssetDiagnostic = _visualAssets == null
                ? MissingVisualAssetCatalogDiagnostic
                : (_visualAssets.Count == 0 ? VisualAssetsEmptyDiagnostic : string.Empty);
        }

        /// <summary>
        /// 解析顺序（M4 起）：Registry 优先 → Root / Monster 库不可用时**明确失败**（Legacy 回退已退役）→
        /// Root 存在但内容非法时明确失败。两种失败都由**既有产品入口**阻止本次战斗初始化（不新增流程）。
        /// </summary>
        private static void ResolveMonstersOnce()
        {
            if (_monstersResolved) return;
            _monstersResolved = true;

            string rootDiagnostic;
            if (!TryInitialize(out rootDiagnostic))
            {
                _monsters = null;
                _monsterDiagnostic = rootDiagnostic + LegacyFallbackRetiredNote;
                return;
            }

            MonsterConfigDatabase database = _root == null ? null : _root.MonsterDatabase;
            if (database == null)
            {
                _monsters = null;
                _monsterDiagnostic = MissingMonsterDatabaseDiagnostic + LegacyFallbackRetiredNote;
                return;
            }

            try
            {
                _monsters = database.CreateRegistry();
                _monsterDiagnostic = _monsters == null ? MonstersUnavailableDiagnostic : string.Empty;
            }
            catch (System.Exception ex)
            {
                // Root 存在 = Authority 可用；内容非法（例如重复 MonsterId）→ 明确失败，不回退（D-M2-1 A2）。
                _monsters = null;
                _monsterDiagnostic = MonstersInvalidDiagnostic + ex.Message;
            }
        }

        // ---------------- WP-Event-1A：Event 领域访问入口 ----------------

        /// <summary>
        /// **Event 领域唯一入口**（纯 Runtime；无 Editor 依赖；lazy；幂等）。
        ///
        /// 成功（true，<paramref name="database"/> 非 null）：
        ///   - 领域有记录 → <paramref name="diagnostic"/> 为空字符串；
        ///   - 领域可用但没有记录（尚未建立 / 十张表都为空）→ **合法状态** + 明确诊断（绝不静默）。
        /// 失败（false，database = null）：Root 缺失 / Root 存在但未包含该库 → 明确可操作诊断。
        ///
        /// 边界：本入口**只读**。它不求解事件、不绑定人物、不判断条件、不执行效果、不渲染模板
        /// （属后续技术检查点 1B / 1C / 1D）。
        /// </summary>
        public static bool TryGetEventConfig(out EventConfigDatabase database, out string diagnostic)
        {
            ResolveEventOnce();
            database = _eventConfig;
            diagnostic = _eventDiagnostic;
            return database != null;
        }

        /// <summary>Event 只读入口（lazy；不可用时返回 null —— 调用方必须同时读 <see cref="EventConfigDiagnostic"/>）。</summary>
        public static EventConfigDatabase EventConfig
        {
            get
            {
                ResolveEventOnce();
                return _eventConfig;
            }
        }

        /// <summary>Event 解析诊断：正式且非空时为空字符串；否则为明确中文诊断。</summary>
        public static string EventConfigDiagnostic
        {
            get
            {
                ResolveEventOnce();
                return _eventDiagnostic;
            }
        }

        /// <summary>Event 领域的**一次性懒解析**（Root → 库 → 诊断）。不做任何回退。</summary>
        private static void ResolveEventOnce()
        {
            if (_eventResolved) return;
            _eventResolved = true;

            string rootDiagnostic;
            if (!TryInitialize(out rootDiagnostic))
            {
                _eventConfig = null;
                _eventDiagnostic = rootDiagnostic;
                return;
            }

            EventConfigDatabase resolved = _root == null ? null : _root.EventDatabase;

            // Unity 的「资产被删除 / 引用未绑定」表现为 fake-null：统一规范化成**真正 null**。
            _eventConfig = (resolved == null) ? null : resolved;

            _eventDiagnostic = _eventConfig == null
                ? MissingEventDatabaseDiagnostic
                : (_eventConfig.TotalCount == 0 ? EventConfigEmptyDiagnostic : string.Empty);
        }
        public static bool TryGetAutonomousLifeConfig(out AutonomousLifeConfigDatabase database, out string diagnostic)
        {
            database = null;
            if (!TryInitialize(out diagnostic)) return false;
            database = _root.AutonomousLifeDatabase;
            diagnostic = database == null ? "AutonomousLife generated database missing; run Config Compiler/Builder." : "";
            return database != null;
        }
        public static bool TryGetSocialConfig(out SocialConfigDatabase database, out string diagnostic)
        {
            database = null;
            if (!TryInitialize(out diagnostic)) return false;
            database = _root.SocialDatabase;
            diagnostic = database == null ? "Social generated database missing; run Config Compiler/Builder." : "";
            return database != null;
        }
    }
}
