using System;
using System.IO;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 当前画布图片导出服务。
// 文件名规则由 TrendOutputFileNameService 统一生成，避免 CSV/图片导出重复拼接文件名。
public sealed class TrendPlotImageExportService
{
    private const int DefaultImageWidth = 1600;
    private const int DefaultImageHeight = 900;

    public string ExportCurrentPlotPng(
        TrendWorkbenchState state,
        ScottPlotTrendAdapter chartAdapter,
        string outputDirectory)
    {
        if (TrendOutputFileNameService.GetVisibleSeriesIndices(state).Count == 0)
            throw new InvalidOperationException("没有勾选任何变量，无法保存图片。");

        Directory.CreateDirectory(outputDirectory);

        string fileName = TrendOutputFileNameService.BuildFileName(state, "png");
        string filePath = Path.Combine(outputDirectory, fileName);
        string uniqueFilePath = TrendOutputFileNameService.EnsureUniqueFilePath(filePath);

        // 图片导出复用 ScottPlot 适配器，服务层不直接接触 AvaPlot 控件。
        chartAdapter.SavePng(uniqueFilePath, DefaultImageWidth, DefaultImageHeight);
        return uniqueFilePath;
    }
}
