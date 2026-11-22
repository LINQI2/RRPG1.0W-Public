using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Battle V0.1 EditMode / PlayMode 测试批处理运行器
///
/// 背景：本团结引擎 fork 的 -runTests 批处理不产出结果文件，
/// 因此通过 TestRunnerApi 手动执行测试并写出摘要。
/// 使用方式：批处理 -executeMethod BattleStage1TestRunner.RunEditModeTests
///       或：批处理 -executeMethod BattleStage1TestRunner.RunPlayModeTests
///
/// batch 模式注意：
///   - RunEditModeTests 会先打开 SampleScene（产品集成验证需真实 UI 宿主）；
///   - 请勿加 -quit（executeMethod 返回后编辑器会立即退出，异步测试回调将不会执行；
///     测试回调内会按结果调用 EditorApplication.Exit）。
/// </summary>
public static class BattleStage1TestRunner
{
    private static readonly string EditModeResultPath =
        System.IO.Path.Combine(Application.dataPath, "../TestResults", "BattleStage1.EditMode.results.txt");

    private static readonly string PlayModeResultPath =
        System.IO.Path.Combine(Application.dataPath, "../TestResults", "BattleStage1.PlayMode.results.txt");

    public static void RunEditModeTests()
    {
        if (Application.isBatchMode)
        {
            // 产品集成验证（BattleV1IntegrationVerify / Stage2 Integration）需要真实场景 UI 宿主；
            // 仅在 batch 中预打开，不影响交互 Editor 的现有场景。
            OpenSampleSceneIfExists();
        }
        Run(TestMode.EditMode, EditModeResultPath, null);
    }

    public static void RunPlayModeTests()
    {
        Run(TestMode.PlayMode, PlayModeResultPath, null);
    }

    /// <summary>
    /// 运行 V4F 产品路径专项（ExpeditionV4FReturnProductPlayTests），结果写到独立文件（定位用）。
    /// 全量 PlayMode 批处理受既有阻塞影响（问题 70），GUI/交互式环境建议用专项组执行。
    /// </summary>
    public static void RunV4FPlayModeTests()
    {
        Run(TestMode.PlayMode,
            System.IO.Path.Combine(Application.dataPath, "../TestResults", "V4F.PlayMode.results.txt"),
            new[] { "ExpeditionV4FReturnProductPlayTests" });
    }

    /// <summary>
    /// 运行指定类（group）的 PlayMode 测试，结果写到独立文件（定位用）。
    /// </summary>
    public static void RunVillagePlayModeTests()
    {
        Run(TestMode.PlayMode,
            System.IO.Path.Combine(Application.dataPath, "../TestResults", "Village.PlayMode.results.txt"),
            new[] { "VillageHUDIntegrationPlayTests" });
    }

    /// <summary>
    /// 运行 Gate 0（3P Shared Deck 配置化卡牌规则）产品集成 PlayMode 专项。
    /// </summary>
    public static void RunBattleGate0PlayModeTests()
    {
        Run(TestMode.PlayMode,
            System.IO.Path.Combine(Application.dataPath, "../TestResults", "BattleGate0.PlayMode.results.txt"),
            new[] { "BattleGate0RulePipelinePlayTests" });
    }

    private static void Run(TestMode mode, string resultPath, string[] groupNames)
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new BatchCallback(resultPath));

        var filter = new Filter
        {
            testMode = mode,
            testNames = null,
            groupNames = groupNames
        };

        api.Execute(new ExecutionSettings(filter));
        Debug.Log("[BattleStage1TestRunner] 测试已提交执行 (" + mode + ")，等待回调…");
    }

    private static void OpenSampleSceneIfExists()
    {
        string fullPath = Path.Combine(Application.dataPath, "Scenes/SampleScene.scene");
        if (File.Exists(fullPath))
        {
            EditorSceneManager.OpenScene(fullPath, OpenSceneMode.Single);
            Debug.Log("[BattleStage1TestRunner] 已预打开场景: " + fullPath);
        }
        else
        {
            Debug.LogWarning("[BattleStage1TestRunner] 未找到 SampleScene（" + fullPath + "），跳过预打开场景");
        }
    }

    private class BatchCallback : ICallbacks
    {
        private readonly string _resultPath;

        public BatchCallback(string resultPath)
        {
            _resultPath = resultPath;
        }

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BattleStage1 Test Results");
            sb.AppendLine("Passed=" + result.PassCount);
            sb.AppendLine("Failed=" + result.FailCount);
            sb.AppendLine("Skipped=" + result.SkipCount);
            sb.AppendLine("Inconclusive=" + result.InconclusiveCount);
            sb.AppendLine("ResultState=" + result.ResultState);

            // 输出失败详情
            AppendFailures(result, sb, 0);

            var dir = Path.GetDirectoryName(_resultPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_resultPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("[BattleStage1TestRunner] 测试完成 Passed=" + result.PassCount + " Failed=" + result.FailCount);

            EditorApplication.Exit(result.FailCount > 0 ? 1 : 0);
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        private static void AppendFailures(ITestResultAdaptor result, StringBuilder sb, int depth)
        {
            if (depth > 3) return;
            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                {
                    AppendFailures(child, sb, depth + 1);
                }
            }
            else if (result.ResultState != null && result.ResultState.StartsWith("Failed"))
            {
                sb.AppendLine("FAILED: " + result.FullName);
                if (!string.IsNullOrEmpty(result.Message))
                    sb.AppendLine("  Message: " + result.Message);
            }
        }
    }
}

