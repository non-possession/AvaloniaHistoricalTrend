using System;
using System.Collections.Generic;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 历史数据存储接口。
// 工作台只依赖这个接口，不直接绑定 SQLite。
// 后续如果接 MySQL、文件或真实平台数据源，只需要新增实现类。
public interface ITrendDataStore
{
    void Initialize(IReadOnlyList<TrendSeriesState> series);
    void AppendSample(TrendSample sample, IReadOnlyList<TrendSeriesState> series);
    List<TrendSample> QuerySamples(DateTime start, DateTime end, int seriesCount);
}
