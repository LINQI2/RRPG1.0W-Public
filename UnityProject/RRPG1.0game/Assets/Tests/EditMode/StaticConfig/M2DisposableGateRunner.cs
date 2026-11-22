using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// M2 **DISPOSABLE GATE RUNNER**（不是产品代码，也不是长期测试资产；M2 Closeout 删除）。
///
/// 存在原因（环境事实，2026-09-22 实测）：
///   本项目团结引擎编辑器在启动期**必定**发生一次 domain reload（CompileScripts），
///   而内置 CLI 入口 `-runTests … -quit` 会在此之后立刻退出（日志 `Batchmode quit successfully invoked`），
///   Test Starter 从未获得执行机会 → 无 results XML、无任何测试运行。
///   因此本 Runner 用**公开 API**（TestRunnerApi + EditorApplication.update 泵）驱动测试，
///   运行结束写摘要并**自行** `EditorApplication.Exit(code)`；调用方**不要**传 `-quit`。
///
/// PlayMode 支持：进入 Play mode 会再次 domain reload → 通过 `.build-state` 状态文件 +
/// `[InitializeOnLoadMethod]` 在 reload 后**恢复**泵与回调（UF 会把 RunFinished 派发给当前已注册回调）。
///
/// 用法（headless）：
///   Tuanjie.exe -batchmode -projectPath &lt;proj&gt; -executeMethod M2DisposableGateRunner.Run
///               -m2TestMode EditMode|PlayMode                      （默认 EditMode）
///               -m2TestFilter &lt;名称/命名空间/类前缀，可省略 = 全量&gt;
///               -m2Summary &lt;repo-relative summary path&gt;
///               -logFile &lt;log&gt;
/// </summary>
public static class M2DisposableGateRunner
{
    private const string DefaultSummaryRelativePath = "Tools/RRPG.ConfigCompiler/.build-state/m2-gate-summary.txt";
    private const string StateMarkerRelativePath = "Tools/RRPG.ConfigCompiler/.build-state/m2-gate-state.txt";
    private const double CompileWaitSeconds = 600.0;
    private const double RunWaitSeconds = 1800.0;

    private static string _summaryPath;
    private static string _filter;
    private static TestMode _mode = TestMode.EditMode;
    private static ResultCollector _collector;
    private static bool _executed;
    private static bool _subscribed;
    private static double _compileDeadline;
    private static double _runDeadline;

    /// <summary>
    /// 运行等待上限（秒）。默认 1800；可用 <c>-m2RunWaitSeconds &lt;n&gt;</c> 提高（无过滤全量 PlayMode 会超过 30 分钟）。
    /// </summary>
    private static double _runWaitSeconds = RunWaitSeconds;

    /// <summary>本次测试运行真正开始的时刻（用于 wallSeconds 统计；随状态文件跨 domain reload 保留）。</summary>
    private static double _runStartedAt;

    [MenuItem("RRPG/M2 Disposable Gate/Run (args required)")]
    public static void RunFromMenu()
    {
        Run();
    }

    /// <summary>headless 入口（-executeMethod）。</summary>
    public static void Run()
    {
        _summaryPath = ResolvePath(ArgumentValue("-m2Summary") ?? DefaultSummaryRelativePath);
        _filter = ArgumentValue("-m2TestFilter");
        string modeArg = ArgumentValue("-m2TestMode");
        _mode = string.Equals(modeArg, "PlayMode", StringComparison.OrdinalIgnoreCase)
            ? TestMode.PlayMode
            : TestMode.EditMode;

        string waitArg = ArgumentValue("-m2RunWaitSeconds");
        double waitSeconds;
        if (!string.IsNullOrEmpty(waitArg)
            && double.TryParse(waitArg, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out waitSeconds)
            && waitSeconds > 0.0)
        {
            _runWaitSeconds = waitSeconds;
        }

        _collector = null;
        _executed = false;
        _subscribed = false;

        WriteStateFile("executed=false");
        Debug.Log("[M2 Gate Runner] start mode=" + _mode + " filter=" + (_filter ?? "<all>")
            + " runWaitSeconds=" + _runWaitSeconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
            + " summary=" + _summaryPath);

        Subscribe();
    }

    /// <summary>domain reload（启动期编译 / 进入 Play mode）后恢复运行。</summary>
    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (!File.Exists(StateMarkerPath())) return;
        LoadStateFile();
        Debug.Log("[M2 Gate Runner] resumed after domain reload: mode=" + _mode + " executed=" + _executed);
        Subscribe();
    }

    private static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        _compileDeadline = EditorApplication.timeSinceStartup + CompileWaitSeconds;

        // 已开始执行（例如 PlayMode 进入 Play mode 造成 domain reload）→ 静态字段归零，
        // 必须重新计算 deadline 并重新注册回调（UF 会把 RunFinished 派发给当前已注册回调）。
        if (_executed)
        {
            _runDeadline = EditorApplication.timeSinceStartup + RunWaitSeconds;
            EnsureCallbacksRegistered();
        }

        EditorApplication.update += Pump;
    }

    private static void EnsureCallbacksRegistered()
    {
        if (_collector != null) return;

        _collector = new ResultCollector();
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(_collector);
    }

    private static void Pump()
    {
        try
        {
            if (!_executed)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    if (EditorApplication.timeSinceStartup > _compileDeadline)
                        Stop("CompileWaitTimeout", "等待编译 / 导入超时（" + CompileWaitSeconds + "s）");
                    return;
                }

                var filter = new Filter { testMode = _mode };
                if (!string.IsNullOrEmpty(_filter)) filter.testNames = new[] { _filter };

                EnsureCallbacksRegistered();

                _executed = true;
                _runStartedAt = EditorApplication.timeSinceStartup;
                _runDeadline = _runStartedAt + _runWaitSeconds;
                WriteStateFile("executed=true");

                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.Execute(new ExecutionSettings(filter));
                return;
            }

            // 已执行（含 domain reload 恢复）：确保回调仍在注册，然后等 RunFinished
            EnsureCallbacksRegistered();

            if (_collector != null && _collector.Result != null)
            {
                Complete(_collector.Result);
                return;
            }

            if (EditorApplication.timeSinceStartup > _runDeadline)
                Stop("RunTimeout", "测试运行超时（" + RunWaitSeconds + "s）");
        }
        catch (Exception ex)
        {
            Stop("RunnerException", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void Complete(ITestResultAdaptor result)
    {
        EditorApplication.update -= Pump;
        DeleteStateFile();

        var lines = new List<string>();
        lines.Add("result=" + (result.FailCount == 0 ? "PASS" : "FAIL"));
        lines.Add("mode=" + _mode);
        lines.Add("status=" + result.ResultState);
        lines.Add("passed=" + result.PassCount);
        lines.Add("failed=" + result.FailCount);
        lines.Add("skipped=" + result.SkipCount);
        lines.Add("inconclusive=" + result.InconclusiveCount);
        lines.Add("filter=" + (_filter ?? "<all>"));

        var failedTests = new List<string>();
        CollectFailures(result, failedTests);
        foreach (string name in failedTests) lines.Add("failed_test=" + name);

        lines.Add("durationSeconds=" + result.Duration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        double runWall = _runStartedAt > 0.0 ? EditorApplication.timeSinceStartup - _runStartedAt : 0.0;
        lines.Add("wallSeconds=" + runWall.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        lines.Add("testCount=" + (result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount));

        File.WriteAllLines(_summaryPath, lines, new UTF8Encoding(false));
        Debug.Log("[M2 Gate Runner] " + string.Join(" | ", lines.ToArray()));

        EditorApplication.Exit(result.FailCount == 0 ? 0 : 1);
    }

    private static void CollectFailures(ITestResultAdaptor node, List<string> failed)
    {
        if (node == null) return;

        if (node.HasChildren)
        {
            foreach (var child in node.Children) CollectFailures(child, failed);
            return;
        }

        if (node.FailCount > 0 || node.ResultState == TestStatus.Failed.ToString())
        {
            string message = node.Message;
            if (!string.IsNullOrEmpty(message) && message.Length > 300) message = message.Substring(0, 300);
            failed.Add(node.FullName + " :: " + message);
        }
    }

    private static void Stop(string kind, string message)
    {
        EditorApplication.update -= Pump;
        DeleteStateFile();
        try
        {
            File.WriteAllLines(_summaryPath, new[]
            {
                "result=RUNNER_FAILED",
                "kind=" + kind,
                "message=" + message
            }, new UTF8Encoding(false));
        }
        catch (Exception)
        {
            // 摘要写入失败也必须退出（否则 headless 会挂住）
        }

        Debug.LogError("[M2 Gate Runner] " + kind + ": " + message);
        EditorApplication.Exit(2);
    }

    // ---------------- 状态文件（跨 domain reload 存活） ----------------

    private static string StateMarkerPath() { return ResolvePath(StateMarkerRelativePath); }

    private static void WriteStateFile(string executedLine)
    {
        try
        {
            string path = StateMarkerPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, new[]
            {
                executedLine,
                "mode=" + _mode,
                "filter=" + (_filter ?? string.Empty),
                "summary=" + _summaryPath,
                "runWaitSeconds=" + _runWaitSeconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture),
                "runStartedAt=" + _runStartedAt.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            }, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Debug.LogError("[M2 Gate Runner] 写状态文件失败：" + ex.Message);
        }
    }

    private static void LoadStateFile()
    {
        try
        {
            foreach (string line in File.ReadAllLines(StateMarkerPath()))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq);
                string value = line.Substring(eq + 1);

                if (key == "executed") _executed = value == "true";
                else if (key == "mode") _mode = string.Equals(value, "PlayMode", StringComparison.OrdinalIgnoreCase)
                    ? TestMode.PlayMode : TestMode.EditMode;
                else if (key == "filter") _filter = string.IsNullOrEmpty(value) ? null : value;
                else if (key == "summary") _summaryPath = value;
                else if (key == "runWaitSeconds")
                {
                    double parsed;
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out parsed) && parsed > 0.0)
                    {
                        _runWaitSeconds = parsed;
                    }
                }
                else if (key == "runStartedAt")
                {
                    double started;
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out started))
                    {
                        _runStartedAt = started;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[M2 Gate Runner] 读状态文件失败：" + ex.Message);
        }
    }

    private static void DeleteStateFile()
    {
        try
        {
            string path = StateMarkerPath();
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
            // 忽略：清理失败不影响退出
        }
    }

    private static string ArgumentValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }

    private static string ResolvePath(string projectOrRepoRelative)
    {
        if (Path.IsPathRooted(projectOrRepoRelative)) return projectOrRepoRelative;

        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        return Path.Combine(repoRoot, projectOrRepoRelative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class ResultCollector : ICallbacks
    {
        public ITestResultAdaptor Result;

        private int _leafIndex;
        private double _leafStartedAt;

        public void RunStarted(ITestAdaptor testsToRun)
        {
            // 全量运行时给出“共有多少测试”的起点（便于判断进度，而不是只看最终汇总）。
            Debug.Log("[M2 Gate Runner] run started: testCaseCount=" + (testsToRun != null ? testsToRun.TestCaseCount : -1));
        }

        public void RunFinished(ITestResultAdaptor result) { Result = result; }

        public void TestStarted(ITestAdaptor test)
        {
            if (test == null || test.HasChildren) return;   // 只记录叶子测试
            _leafIndex++;
            _leafStartedAt = EditorApplication.timeSinceStartup;
            Debug.Log("[M2 Gate Runner] test#" + _leafIndex + " START :: " + test.FullName);
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result == null || result.HasChildren) return;

            double wall = EditorApplication.timeSinceStartup - _leafStartedAt;
            Debug.Log("[M2 Gate Runner] test#" + _leafIndex + " END " + result.ResultState
                + " duration=" + result.Duration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "s"
                + " wall=" + wall.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "s"
                + " :: " + result.FullName
                + (result.ResultState == TestStatus.Failed.ToString() ? " :: " + result.Message : string.Empty));
        }
    }
}
