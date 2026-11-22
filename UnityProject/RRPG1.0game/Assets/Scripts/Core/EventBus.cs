using System;
using System.Collections.Generic;

/// <summary>
/// 事件总线
///
/// 负责：系统间的解耦通信
/// 不负责：事件数据的存储、UI显示
///
/// 系统之间优先使用事件通信
/// 避免系统直接调用系统
/// 避免循环依赖
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="handler">事件处理函数</param>
    public static void Subscribe<T>(Action<T> handler) where T : class
    {
        Type type = typeof(T);
        if (_events.ContainsKey(type))
        {
            _events[type] = Delegate.Combine(_events[type], handler);
        }
        else
        {
            _events[type] = handler;
        }
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="handler">事件处理函数</param>
    public static void Unsubscribe<T>(Action<T> handler) where T : class
    {
        Type type = typeof(T);
        if (_events.ContainsKey(type))
        {
            _events[type] = Delegate.Remove(_events[type], handler);
            if (_events[type] == null)
            {
                _events.Remove(type);
            }
        }
    }

    /// <summary>
    /// 发布事件
    /// 所有订阅此事件的监听器将同时收到通知
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    public static void Publish<T>(T eventData) where T : class
    {
        Type type = typeof(T);
        if (_events.ContainsKey(type))
        {
            var handler = _events[type] as Action<T>;
            handler?.Invoke(eventData);
        }
    }

    /// <summary>
    /// 清空所有订阅
    /// 通常用于场景切换或战斗结束时清理
    /// </summary>
    public static void Clear()
    {
        _events.Clear();
    }
}