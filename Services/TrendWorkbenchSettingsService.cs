using System;
using System.IO;
using System.Text.Json;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 工作台配置文件服务。
// 负责读取、保存和兜底修正 trend-workbench.settings.json。
// 配置异常时返回安全默认值，避免现场因为配置文件损坏导致程序无法启动。
public sealed class TrendWorkbenchSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
    {
        WriteIndented = true,
    };

    private readonly string settingsPath;

    public TrendWorkbenchSettingsService(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? Path.Combine(Directory.GetCurrentDirectory(), "trend-workbench.settings.json");
    }

    public TrendWorkbenchSettings Load()
    {
        try
        {
            if (!File.Exists(settingsPath))
                return Normalize(new TrendWorkbenchSettings());

            string json = File.ReadAllText(settingsPath);
            TrendWorkbenchSettings settings = JsonSerializer.Deserialize<TrendWorkbenchSettings>(json, JsonOptions) ?? new TrendWorkbenchSettings();
            return Normalize(settings);
        }
        catch
        {
            return Normalize(new TrendWorkbenchSettings());
        }
    }

    public void Save(TrendWorkbenchSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath) ?? Directory.GetCurrentDirectory());
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(settingsPath, json);
    }

    private static TrendWorkbenchSettings Normalize(TrendWorkbenchSettings settings)
    {
        // 所有外部配置进入系统前都先规范化，领域层不再重复处理配置文件细节。
        if (!Enum.IsDefined(typeof(TrendLayoutMode), settings.LayoutMode))
            settings.LayoutMode = TrendLayoutMode.DualAxis;

        if (settings.DefaultVisibleDurationMinutes < 1)
            settings.DefaultVisibleDurationMinutes = 1440;

        if (settings.DefaultVisibleDurationSeconds < TrendWorkbenchCoordinator.MinimumTimeSpan.TotalSeconds)
            settings.DefaultVisibleDurationSeconds = settings.DefaultVisibleDurationMinutes * 60;

        if (string.IsNullOrWhiteSpace(settings.StorageProvider))
            settings.StorageProvider = "SQLite";

        if (string.IsNullOrWhiteSpace(settings.SqliteDatabasePath))
            settings.SqliteDatabasePath = "trend-workbench.db";

        if (settings.SamplingIntervalSeconds < 1)
            settings.SamplingIntervalSeconds = 1;

        if (settings.RealtimeWindowMinutes < 1)
            settings.RealtimeWindowMinutes = 60;

        if (settings.RealtimeWindowSeconds < TrendWorkbenchCoordinator.MinimumTimeSpan.TotalSeconds)
            settings.RealtimeWindowSeconds = settings.RealtimeWindowMinutes * 60;

        NormalizeAxisGroups(settings);

        return settings;
    }

    private static void NormalizeAxisGroups(TrendWorkbenchSettings settings)
    {
        if (settings.AxisGroupAssignments.Length != 8)
        {
            settings.AxisGroupAssignments = CreateDefaultAxisGroups();
            return;
        }

        bool hasY1 = false;
        bool hasY2 = false;
        for (int i = 0; i < settings.AxisGroupAssignments.Length; i++)
        {
            if (!Enum.IsDefined(typeof(TrendAxisGroup), settings.AxisGroupAssignments[i]))
            {
                // 轴分组非法时直接回退默认分组，避免 Y1/Y2 下拉框变空。
                settings.AxisGroupAssignments = CreateDefaultAxisGroups();
                return;
            }

            if (settings.AxisGroupAssignments[i] == TrendAxisGroup.Y1)
                hasY1 = true;
            if (settings.AxisGroupAssignments[i] == TrendAxisGroup.Y2)
                hasY2 = true;
        }

        if (!hasY1 || !hasY2)
            settings.AxisGroupAssignments = CreateDefaultAxisGroups();
    }

    private static TrendAxisGroup[] CreateDefaultAxisGroups()
    {
        return new TrendAxisGroup[]
        {
            TrendAxisGroup.Y1,
            TrendAxisGroup.Y1,
            TrendAxisGroup.Y1,
            TrendAxisGroup.Y1,
            TrendAxisGroup.Y2,
            TrendAxisGroup.Y2,
            TrendAxisGroup.Y2,
            TrendAxisGroup.Y2,
        };
    }
}
