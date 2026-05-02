using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AvaloniaApplication2.Services;

// 打印入口服务。
// 第一版采用“先保存图片，再调用系统默认程序打开”的方式，
// 这样保持跨平台可用，也避免把平台打印 API 直接耦合到 View。
public sealed class TrendPlotPrintService
{
    public void OpenImageForPrinting(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("待打印的图片不存在。", imagePath);

        ProcessStartInfo startInfo = CreateOpenCommand(imagePath);
        Process.Start(startInfo);
    }

    private static ProcessStartInfo CreateOpenCommand(string imagePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS 使用 open 交给系统预览/默认图片程序处理。
            return new ProcessStartInfo
            {
                FileName = "open",
                ArgumentList = { imagePath },
                UseShellExecute = false,
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                FileName = imagePath,
                UseShellExecute = true,
            };
        }

        return new ProcessStartInfo
        {
            FileName = "xdg-open",
            ArgumentList = { imagePath },
            UseShellExecute = false,
        };
    }
}
