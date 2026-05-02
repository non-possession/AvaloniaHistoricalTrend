using System;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 默认模拟采集器。
// 每次采集都在变量当前工程量程内生成随机值，便于没有真实设备时验证实时趋势链路。
public sealed class RandomTrendDataCollector : ITrendDataCollector
{
    private readonly Random random = new Random();

    public TrendSample Collect(DateTime timestamp, TrendWorkbenchCoordinator coordinator)
    {
        int seriesCount = coordinator.State.Series.Count;
        double[] values = new double[seriesCount];

        for (int i = 0; i < seriesCount; i++)
        {
            NumericRange range = coordinator.GetEngineeringRange(i);
            // 随机值必须落在工程量程内，避免实时采样把曲线打到不可见区域。
            values[i] = range.Min + random.NextDouble() * range.Span;
        }

        return new TrendSample
        {
            Timestamp = timestamp,
            Values = values,
        };
    }
}
