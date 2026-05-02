using System;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 数据采集接口。
// 当前项目使用随机采集器模拟现场设备，真实设备接入时应实现这个接口，
// 避免把采集协议、串口/网口通信等逻辑写入 View 或 ViewModel。
public interface ITrendDataCollector
{
    TrendSample Collect(DateTime timestamp, TrendWorkbenchCoordinator coordinator);
}
