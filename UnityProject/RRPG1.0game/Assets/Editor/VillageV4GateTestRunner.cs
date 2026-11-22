using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;

/// <summary>
/// VillageV4GateTestRunner —— **Disposable Development Test Runner**（V4 Gate 专用；Closeout 删除）。
///
/// 背景：本机 Tuanjie Editor 以 GUI 打开本项目（工程锁），无法再用 batchmode -runTests；
/// 且本会话无 Unity MCP 工具。本文件只提供「触发文件 → TestRunnerApi → 结果文件」的自动化通道，
/// 不进入玩家流程、不修改任何工程数据、不参与正式构建（Editor 程序集、仅测试用途）。
///
/// 用法：
///   1) 写触发文件 `TestResults/village_v4_run.request`，内容为过滤器：
///        `EditMode:VillageV4CapabilityGrantTests`  或 `PlayMode:ExpeditionV4FFullProductPlayTests`
///   2) 聚焦 Editor（触发 AssetDatabase 刷新 / 编译 / 域重载）
///   3) 读取结果文件 `TestResults/village_v4_run.result.txt`
///
/// 结果格式：
///   `RESULT passed=8 failed=0 skipped=0 inconclusive=0`
///   `[Passed] <FullName>`
///   `[Failed] <FullName> :: <Message>`
/// </summary>
[InitializeOnLoad]
public static class VillageV4GateTestRunner
{
    private const string RequestRelativePath = "TestResults/village_v4_run.request";
    private const string ResultRelativePath = "TestResults/village_v4_run.result.txt";
    private const string PendingRelativePath = "TestResults/village_v4_run.pending";

    private static bool _running;
    private static double _nextCheckTime;
    private static bool _callbacksRegistered;

    // 说明：本类型是 Disposable Development Test Runner（V4 Gates 专用），Closeout 时删除。
    static VillageV4GateTestRunner()
    {
        // PlayMode 运行会触发域重载并清空 Editor 侧回调注册 → 重载后按 pending 重新注册回调。
        EditorApplication.delayCall += ResumePendingRun;
        EditorApplication.delayCall += Pump;
        // 轮询触发文件：不依赖域重载（编译后未变更代码时不会再次触发 InitializeOnLoad）。
        EditorApplication.update += Tick;
    }

    private static void ResumePendingRun()
    {
        try
        {
            if (!File.Exists(Path.Combine(ProjectRoot, PendingRelativePath))) return;
            EnsureCallbacks();
            _running = true;
            Debug.Log("[VillageV4GateTestRunner] 域重载后恢复等待运行结果（PlayMode）。");
        }
        catch (Exception ex)
        {
            Debug.LogError("[VillageV4GateTestRunner] ResumePendingRun 失败：" + ex);
        }
    }

    private static void EnsureCallbacks()
    {
        if (_callbacksRegistered) return;
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        _callbacksRegistered = true;
    }

    private static void Tick()
    {
        if (_running) return;
        if (EditorApplication.timeSinceStartup < _nextCheckTime) return;
        _nextCheckTime = EditorApplication.timeSinceStartup + 2.0;
        Pump();
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    private static void Pump()
    {
        try
        {
            if (_running) return;
            string requestPath = Path.Combine(ProjectRoot, RequestRelativePath);
            if (!File.Exists(requestPath)) return;

            string spec = File.ReadAllText(requestPath).Trim();
            File.Delete(requestPath);
            if (string.IsNullOrEmpty(spec)) return;

            Run(spec);
        }
        catch (Exception ex)
        {
            Debug.LogError("[VillageV4GateTestRunner] Pump 失败：" + ex);
        }
    }

    private static void Run(string spec)
    {
        int sep = spec.IndexOf(':');
        string mode = sep > 0 ? spec.Substring(0, sep) : "EditMode";
        string group = sep > 0 ? spec.Substring(sep + 1) : spec;

        var filter = new Filter
        {
            testMode = string.Equals(mode, "PlayMode", StringComparison.OrdinalIgnoreCase)
                ? TestMode.PlayMode
                : TestMode.EditMode
        };
        if (!string.Equals(group, "ALL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(group))
        {
            var groups = new List<string>();
            foreach (var part in group.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed)) groups.Add(trimmed);
            }
            if (groups.Count > 0) filter.groupNames = groups.ToArray();
        }

        _running = true;
        File.WriteAllText(Path.Combine(ProjectRoot, PendingRelativePath), spec); // 跨域重载保持
        EnsureCallbacks();
        Debug.Log("[VillageV4GateTestRunner] 开始执行：" + mode + " / " + group);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(filter));
    }

    private class Callbacks : ICallbacks
    {
        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _failures = new List<string>();

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            try
            {
                if (result == null || result.Test == null || result.Test.HasChildren) return;
                string name = result.Test.FullName;
                if (result.TestStatus == TestStatus.Passed)
                {
                    _lines.Add("[Passed] " + name);
                }
                else if (result.TestStatus == TestStatus.Failed)
                {
                    _lines.Add("[Failed] " + name + " :: " + result.Message);
                    _failures.Add(name + " :: " + result.Message);
                }
                else
                {
                    _lines.Add("[" + result.TestStatus + "] " + name);
                }
            }
            catch (Exception ex)
            {
                _lines.Add("[RunnerError] " + ex.Message);
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                var sb = new StringBuilder();
                if (result != null)
                {
                    sb.AppendLine("RESULT passed=" + result.PassCount
                        + " failed=" + result.FailCount
                        + " skipped=" + result.SkipCount
                        + " inconclusive=" + result.InconclusiveCount);
                }
                else
                {
                    sb.AppendLine("RESULT passed=0 failed=0 skipped=0 inconclusive=0 (null result)");
                }

                foreach (var line in _lines) sb.AppendLine(line);
                foreach (var failure in _failures) sb.AppendLine("FAILURE_DETAIL " + failure);

                string resultPath = Path.Combine(ProjectRoot, ResultRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                File.WriteAllText(resultPath, sb.ToString());
                Debug.Log("[VillageV4GateTestRunner] 完成，结果写入 " + ResultRelativePath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[VillageV4GateTestRunner] 写结果失败：" + ex);
            }
            finally
            {
                _running = false;
                try
                {
                    string pendingPath = Path.Combine(ProjectRoot, PendingRelativePath);
                    if (File.Exists(pendingPath)) File.Delete(pendingPath);
                }
                catch
                {
                    // 忽略：pending 清理失败不影响结果文件
                }
            }
        }
    }
}
