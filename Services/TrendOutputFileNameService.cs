using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 输出文件命名服务。
// CSV、图片和打印预览都使用同一套命名规则：
// 当前时间窗口 + 当前可见变量集合 + 文件扩展名。
public static class TrendOutputFileNameService
{
    public static string BuildFileName(TrendWorkbenchState state, string extension)
    {
        List<int> selectedSeriesIndices = GetVisibleSeriesIndices(state);
        string startText = state.TimeWindow.VisibleStart.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string endText = state.TimeWindow.VisibleEnd.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string seriesText = BuildSeriesNamePart(state, selectedSeriesIndices);

        return $"{startText}-{endText}-[{seriesText}].{extension.TrimStart('.')}";
    }

    public static string EnsureUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        string? directory = Path.GetDirectoryName(filePath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        for (int index = 1; index < 1000; index++)
        {
            // 避免覆盖用户之前导出的文件，重复时自动追加序号。
            string candidate = Path.Combine(directory ?? string.Empty, $"{fileNameWithoutExtension}-{index}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException("无法生成不重复的输出文件名。");
    }

    public static List<int> GetVisibleSeriesIndices(TrendWorkbenchState state)
    {
        List<int> indices = new List<int>();
        for (int i = 0; i < state.Series.Count; i++)
        {
            if (state.Series[i].IsVisible)
                indices.Add(i);
        }

        return indices;
    }

    private static string BuildSeriesNamePart(TrendWorkbenchState state, List<int> selectedSeriesIndices)
    {
        if (selectedSeriesIndices.Count == 0)
            return "NoSeries";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < selectedSeriesIndices.Count; i++)
        {
            if (i > 0)
                builder.Append('+');

            int seriesIndex = selectedSeriesIndices[i];
            builder.Append(SanitizeFileNamePart(state.Series[seriesIndex].Name));
        }

        return builder.ToString();
    }

    private static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unnamed";

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            bool isInvalid = false;
            for (int j = 0; j < invalidCharacters.Length; j++)
            {
                if (character == invalidCharacters[j])
                {
                    isInvalid = true;
                    break;
                }
            }

            builder.Append(isInvalid ? '_' : character);
        }

        return builder.ToString().Trim();
    }
}
