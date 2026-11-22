using System;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// WP-1 Stage3 seam 测试（EditMode，纯反射访问 Assembly-CSharp 的
/// MapGenerator / LegacyMapGraphAdapter；验证 nodeElapsedDaysFor 透传与默认行为不变）。
/// 规则语义（Resolve 累计 / Supply exactly-once / 0 默认）由 Stage1 ExpeditionProgression EditMode
/// 专项持续覆盖（ExpeditionElapsedDaysExactlyOnceTests / ExpeditionSupplyConsumptionTests）。
/// </summary>
public class ExpeditionStage3SeamEditTests
{
    private const BindingFlags FB = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Type A(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object GetP(object o, string name)
    {
        var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null) return p.GetValue(o, null);
        var f = o.GetType().GetField(name, FB);
        Assert.IsNotNull(f, "成员不存在：" + name);
        return f.GetValue(o);
    }

    private static object Call(object o, string name, params object[] args)
    {
        var m = o.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "方法不存在：" + name);
        return m.Invoke(o, args);
    }

    private static object GenerateGraph(Func<string, int> nodeElapsedDaysFor)
    {
        var mapGenType = A("MapGenerator");
        object generator = Activator.CreateInstance(mapGenType);
        mapGenType.GetMethod("Generate").Invoke(generator, new object[] { 4, 3 });

        var adapter = A("LegacyMapGraphAdapter");
        // LegacyMapGraphAdapter.BuildGraph 是单方法（第二参数可选），无 1 参重载：
        // 默认 null 与自定义都调用 BuildGraph(MapGenerator, Func<string,int>)。
        var build = adapter.GetMethod("BuildGraph", new[] { mapGenType, typeof(Func<string, int>) });
        Assert.IsNotNull(build, "BuildGraph(MapGenerator, Func) 未找到");
        return build.Invoke(null, new object[] { generator, nodeElapsedDaysFor });
    }

    private static object GetNode(object graph, string nodeId)
    {
        var m = graph.GetType().GetMethod("TryGetNode");
        object[] ps = { nodeId, null };
        bool ok = (bool)m.Invoke(graph, ps);
        Assert.IsTrue(ok, "图应含节点：" + nodeId);
        return ps[1];
    }

    private static int NodeCount(object graph)
    {
        var nodes = Call(graph, "GetNodes");
        int count = 0;
        foreach (var n in (System.Collections.IEnumerable)nodes) count++;
        return count;
    }

    [Test]
    public void Seam_CustomNodeElapsed_IsWrittenIntoGraph()
    {
        var graph = GenerateGraph(id => id == "node_0" ? 2 : 0);

        Assert.AreEqual(2, (int)GetP(GetNode(graph, "node_0"), "NodeElapsedDays"));
        Assert.AreEqual(0, (int)GetP(GetNode(graph, "node_3"), "NodeElapsedDays"));

        // Boss（node_10）作为显式成功路线终点（SuccessfulRouteTerminal=true）
        Assert.IsTrue((bool)GetP(GetNode(graph, "node_10"), "SuccessfulRouteTerminal"));

        // 连接目标存在性已由 ExpeditionGraph 构造校验；节点数与 4×3 一致
        Assert.AreEqual(11, NodeCount(graph));
    }

    [Test]
    public void Seam_DefaultNull_AllNodesZero_BehaviorUnchanged()
    {
        var graph = GenerateGraph(null);
        Assert.AreEqual(11, NodeCount(graph));
        foreach (var node in (System.Collections.IEnumerable)Call(graph, "GetNodes"))
            Assert.AreEqual(0, (int)GetP(node, "NodeElapsedDays"));
        Assert.AreEqual(0, (int)GetP(GetNode(graph, "node_0"), "NodeElapsedDays"));
    }
}
