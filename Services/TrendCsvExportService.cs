using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// CSV 导出服务。
// 只导出当前 X 轴时间窗口内、右侧变量卡勾选的变量。
// 某个变量在某个时间点没有有效值时，写入空单元格，保证列结构稳定。
public sealed class TrendCsvExportService
{
    public string ExportCurrentWindow(TrendWorkbenchState state, string outputDirectory)
    {
        List<int> selectedSeriesIndices = TrendOutputFileNameService.GetVisibleSeriesIndices(state);
        if (selectedSeriesIndices.Count == 0)
            throw new InvalidOperationException("没有勾选任何变量，无法导出 CSV。");

        Directory.CreateDirectory(outputDirectory);

        string fileName = TrendOutputFileNameService.BuildFileName(state, "csv");
        string filePath = Path.Combine(outputDirectory, fileName);
        string uniqueFilePath = TrendOutputFileNameService.EnsureUniqueFilePath(filePath);

        string csvContent = BuildCsvContent(state, selectedSeriesIndices);
        File.WriteAllText(uniqueFilePath, csvContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        return uniqueFilePath;
    }

    private static string BuildCsvContent(TrendWorkbenchState state, List<int> selectedSeriesIndices)
    {
        StringBuilder builder = new StringBuilder();

        builder.Append(EscapeCsvValue("Time"));
        for (int i = 0; i < selectedSeriesIndices.Count; i++)
        {
            int seriesIndex = selectedSeriesIndices[i];
            builder.Append(',');
            builder.Append(EscapeCsvValue(state.Series[seriesIndex].Name));
        }

        builder.AppendLine();

        for (int pointIndex = 0; pointIndex < state.TimePoints.Length; pointIndex++)
        {
            DateTime timePoint = state.TimePoints[pointIndex];
            if (timePoint < state.TimeWindow.VisibleStart || timePoint > state.TimeWindow.VisibleEnd)
                continue;

            // CSV 的第一列始终是时间，后续列按当前可见变量顺序输出。
            builder.Append(EscapeCsvValue(timePoint.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));

            for (int i = 0; i < selectedSeriesIndices.Count; i++)
            {
                int seriesIndex = selectedSeriesIndices[i];
                builder.Append(',');
                builder.Append(GetCsvValueAtPoint(state, seriesIndex, pointIndex));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GetCsvValueAtPoint(TrendWorkbenchState state, int seriesIndex, int pointIndex)
    {
        if (seriesIndex < 0 || seriesIndex >= state.RawSeriesYValues.Length)
            return string.Empty;

        double[] values = state.RawSeriesYValues[seriesIndex];
        if (pointIndex < 0 || pointIndex >= values.Length)
            return string.Empty;

        double value = values[pointIndex];
        if (double.IsNaN(value) || double.IsInfinity(value))
            return string.Empty;

        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsvValue(string value)
    {
        bool mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!mustQuote)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
