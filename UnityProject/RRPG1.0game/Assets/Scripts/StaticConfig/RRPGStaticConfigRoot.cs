using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 生成配置的**唯一 Root**（FDS §3.3：Wave 0 V0.1 Runtime Root 策略）。
    ///
    /// 定位：
    ///   - 由 Unity Builder 生成到 `Assets/Generated/StaticConfig/Resources/RRPGStaticConfigRoot.asset`；
    ///   - **不进 Git**（Migration V0.1 Implementation Decision，FDS §8.2），由 Builder 一键重建；
    ///   - Runtime 只通过**一个固定 exact path** 取得它：
    ///     <c>Resources.Load&lt;RRPGStaticConfigRoot&gt;("RRPGStaticConfigRoot")</c>；
    ///   - **禁止** Resources.LoadAll / 场景搜索 / FindObjectOfType / AssetDatabase；
    ///   - 产品 Scene / Prefab **不得**直接引用 per-domain 生成资产（只能引用本 Root / 经 Registry 解析）。
    ///
    /// M0 边界：只建立稳定容器骨架；**不装任何业务数据**、**不接 Gameplay**
    /// （不引用 Battle / Expedition / DataManager / 任何 Legacy Monster 类型）。
    /// </summary>
    public sealed class RRPGStaticConfigRoot : ScriptableObject
    {
        /// <summary>exact Resources 路径（不含扩展名）。改这个常量 = 改架构契约。</summary>
        public const string ResourcesPath = "RRPGStaticConfigRoot";

        /// <summary>Mirror 格式版本（与 Compiler 的 CanonicalIrWriter.IrVersion 对应）。</summary>
        public const string SupportedFormatVersion = "rrpg-static-config-mirror/1";

        [SerializeField] private string formatVersion = SupportedFormatVersion;
        [SerializeField] private string contentSignature = string.Empty;

        [SerializeField] private MonsterConfigDatabase monsterDatabase;
        [SerializeField] private EnemyPresentationDatabase enemyPresentationDatabase;
        [SerializeField] private VisualAssetCatalog visualAssetCatalog;

        /// <summary>WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础）的只读静态配置库。</summary>
        [SerializeField] private EncounterConfigDatabase encounterDatabase;

        /// <summary>WP-Event-1A：Event 领域（统一事件内容基础）的只读静态配置库。</summary>
        [SerializeField] private EventConfigDatabase eventDatabase;

        [SerializeField] private AutonomousLifeConfigDatabase autonomousLifeDatabase;
        public AutonomousLifeConfigDatabase AutonomousLifeDatabase => autonomousLifeDatabase;
        [SerializeField] private SocialConfigDatabase socialDatabase;
        public SocialConfigDatabase SocialDatabase => socialDatabase;
        [SerializeField] private BuildingConfigDatabase buildingDatabase;
        public BuildingConfigDatabase BuildingDatabase => buildingDatabase;
        [SerializeField] private VillagePresentationDatabase villagePresentationDatabase;
        public VillagePresentationDatabase VillagePresentationDatabase => villagePresentationDatabase;
        public string FormatVersion { get { return formatVersion; } }

        /// <summary>Builder 计算的内容签名：相同输入 → 相同签名（no-op 判定的唯一依据）。</summary>
        public string ContentSignature { get { return contentSignature; } }

        public MonsterConfigDatabase MonsterDatabase { get { return monsterDatabase; } }
        public EnemyPresentationDatabase EnemyPresentationDatabase { get { return enemyPresentationDatabase; } }
        public VisualAssetCatalog VisualAssetCatalog { get { return visualAssetCatalog; } }

        /// <summary>Encounter 领域库（生成物；不可用时为 null，由 StaticConfigRegistry 给出明确诊断）。</summary>
        public EncounterConfigDatabase EncounterDatabase { get { return encounterDatabase; } }

        /// <summary>Event 领域库（生成物；不可用时为 null，由 StaticConfigRegistry 给出明确诊断）。</summary>
        public EventConfigDatabase EventDatabase { get { return eventDatabase; } }

        /// <summary>
        /// Builder 唯一写入口（只由 Editor Builder 调用；Runtime 只读）。
        /// </summary>
        public void ApplyGeneratedContent(
            string format, string signature,
            MonsterConfigDatabase monsters,
            EnemyPresentationDatabase presentations,
            VisualAssetCatalog catalog,
            EncounterConfigDatabase encounters,
            EventConfigDatabase events, AutonomousLifeConfigDatabase autonomousLife = null, SocialConfigDatabase social = null, BuildingConfigDatabase buildings = null,
            VillagePresentationDatabase villagePresentation = null)
        {
            formatVersion = format;
            contentSignature = signature;
            monsterDatabase = monsters;
            enemyPresentationDatabase = presentations;
            visualAssetCatalog = catalog;
            encounterDatabase = encounters;
            eventDatabase = events;
            autonomousLifeDatabase = autonomousLife;
            socialDatabase = social;
            buildingDatabase = buildings;
            villagePresentationDatabase = villagePresentation;
        }

        /// <summary>
        /// 唯一稳定加载入口：**exact single-root Load**（不是按目录批量发现的 LoadAll 模型）。
        /// 找不到时返回 null：调用方（StaticConfigRegistry）必须给出明确诊断，不得静默回退。
        /// </summary>
        public static RRPGStaticConfigRoot Load()
        {
            return Resources.Load<RRPGStaticConfigRoot>(ResourcesPath);
        }
    }
}
