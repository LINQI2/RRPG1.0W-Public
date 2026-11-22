using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C §9 / §30：**Event.Runtime 的依赖边界守卫**（源码级 + 程序集级）。
    ///
    /// 为什么需要它：1C 的效果会触碰 Village / Expedition / Battle 的**语义**，
    /// 但 Event Core 绝不允许直接依赖那些产品程序集（否则形成循环依赖，并把 Event 绑死在具体实现上）。
    /// 因此外部效果只能经 <see cref="IEventEffectGateway"/> 表达（真实 Adapter 属 1D）。
    /// </summary>
    public class EventRuntimeBoundaryTests
    {
        private static readonly string[] ForbiddenTokens =
        {
            "UnityEngine", "MonoBehaviour", "ScriptableObject",
            "VillageRuntimeBridge", "VillageSocialResponseLog", "VillageSocialEventUI", "VillageResourceState",
            "ExpeditionProgressionResolver", "ExpeditionSupplyState", "AdventureManager", "BattleSystem",
            "EncounterGenerator", "SaveSystem", "JourneyEventCard"
        };

        [Test]
        public void EventRuntime_Assembly_References_Only_PureLogic()
        {
            string asmdef = ReadEventFile("Event.Runtime.asmdef");
            Assert.IsNotNull(asmdef, "Event.Runtime.asmdef 必须存在");

            StringAssert.Contains("\"StaticConfig.Runtime\"", asmdef);
            StringAssert.Contains("\"RRPG.Deterministic.Runtime\"", asmdef);
            StringAssert.Contains("\"noEngineReferences\": true", asmdef);

            string[] forbidden =
            {
                "Encounter.Runtime", "Village", "Adventure", "Battle", "UnityEngine\"", "Assembly-CSharp",
                "ExpeditionProgression.Runtime"
            };
            foreach (string token in forbidden)
            {
                Assert.IsFalse(asmdef.Contains(token),
                    "Event.Runtime 不得引用 '" + token + "'（外部效果只能经 Gateway 契约表达）");
            }
        }

        [Test]
        public void EventRuntime_Sources_DoNotReference_ProductSystems_OrEngineTypes()
        {
            string dir = Path.Combine(Application.dataPath, "Scripts", "Event");
            Assert.IsTrue(Directory.Exists(dir), "找不到 Event Runtime 源码目录：" + dir);

            var violations = new List<string>();

            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("//", System.StringComparison.Ordinal)
                        || line.StartsWith("*", System.StringComparison.Ordinal)
                        || line.StartsWith("/*", System.StringComparison.Ordinal))
                    {
                        continue;   // 注释里提到产品类型名是允许的（用于说明边界），代码里不允许
                    }

                    foreach (string token in ForbiddenTokens)
                    {
                        if (line.Contains(token))
                        {
                            violations.Add(Path.GetFileName(file) + ":" + (i + 1) + " → " + line);
                        }
                    }
                }
            }

            Assert.IsEmpty(violations,
                "Event.Runtime 不得直接依赖产品运行时 / 引擎类型（应经 IEventEffectGateway 或只读契约）：\n"
                + string.Join("\n", violations));
        }

        private static string ReadEventFile(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "Scripts", "Event", fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }
}
