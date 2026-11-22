#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

/// <summary>测试默认 Instant；专项正常/2x 测试显式覆盖。不会影响普通玩家 Play。</summary>
[InitializeOnLoad]
public sealed class BattleFeedbackTestClock : ICallbacks
{
    static BattleFeedbackTestClock()
    {
        BattleFeedbackPlayer.DefaultInstant = SessionState.GetBool("RRPG.Feedback.TestInstant", false);
        UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new BattleFeedbackTestClock());
    }
    public void RunStarted(ITestAdaptor testsToRun) { SessionState.SetBool("RRPG.Feedback.TestInstant", true); BattleFeedbackPlayer.DefaultInstant = true; }
    public void RunFinished(ITestResultAdaptor result) { SessionState.SetBool("RRPG.Feedback.TestInstant", false); BattleFeedbackPlayer.DefaultInstant = false; }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
}
#endif
