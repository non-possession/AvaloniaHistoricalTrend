# AvaloniaHistoricalTrend

工业上位机趋势曲线工作台示例项目。项目基于 .NET 8、Avalonia UI 和 ScottPlot，目标是把历史曲线、实时曲线、双轴量程、刷子筛选、十字光标、SQLite 历史数据存储、CSV/图片导出等能力拆成清晰、可维护、便于后续自动化和电气工程师接手的结构。

当前项目更偏“工业上位机组件开发学习和架构验证”，默认数据采集器是随机模拟采集器，真实设备接入应通过新增采集器实现，而不是直接改 View 层。

## 功能概览

- 历史趋势曲线和实时趋势曲线双模式。
- 8 条趋势变量曲线，支持变量显隐、变量名称编辑、变量卡片状态显示。
- 单轴和双轴布局切换。
- Y1/Y2 双轴候选变量由配置驱动，不在业务逻辑里硬编码。
- 每个变量有独立工程量程、自定义量程和刷子显示范围。
- X 轴时间窗口支持预设跨度、平移、缩放、起止时间编辑。
- 主图支持十字光标，右侧变量卡会跟随光标显示当前值。
- 实时模式按采样周期采集数据，写入 SQLite，并滚动显示最近窗口。
- 历史模式从 SQLite 查询指定时间范围数据。
- 支持 CSV 导出、当前图像 PNG 保存、打印入口。
- 配置文件支持布局模式、图例开关、采样周期、实时窗口、SQLite 路径、轴分组等参数。
- ScottPlot 渲染层已显式注册 Windows 中文字体，避免画布内中文乱码。
- 已清理 macOS 迁移残留文件，并通过 `global.json` 固定 .NET 8 SDK。

## 技术栈

- .NET 8.0
- Avalonia 11.3.4
- ScottPlot.Avalonia 5.1.58
- CommunityToolkit.Mvvm 8.4.1
- Microsoft.Data.Sqlite 9.0.4
- SQLite
- Git for Windows / OpenSSH

项目使用 `global.json` 固定 SDK：

```json
{
  "sdk": {
    "version": "8.0.420",
    "rollForward": "latestFeature"
  }
}
```

## 快速开始

确认本机已安装 .NET 8 SDK。

```powershell
dotnet --version
dotnet restore
dotnet build
dotnet run --project AvaloniaApplication2.csproj
```

运行测试：

```powershell
dotnet run --project Tests\TrendWorkbench.Tests\TrendWorkbench.Tests.csproj
```

当前测试覆盖趋势领域规则、配置归一化、CSV 导出、SQLite 读写、实时采集、数据库切换、轴分组、时间窗口编辑等核心行为。

## 项目结构

```text
AvaloniaApplication2/
├── Models/                         # 纯状态和展示快照模型
├── Services/                       # 领域协调器、应用服务、存储、采集、导出、图表适配器
├── ViewModels/                     # 页面展示状态和页面级动作入口
├── Views/                          # Avalonia View 和控件事件桥接
│   └── Interaction/                # 刷子、时间窗口、十字光标等像素级交互 helper
├── Tests/TrendWorkbench.Tests/     # 轻量测试控制台
├── docs/                           # 配置说明和人工回归清单
├── Assets/                         # Avalonia 资源
├── trend-workbench.settings.json   # 工作台配置文件
├── global.json                     # 固定 .NET SDK
└── AvaloniaApplication2.csproj
```

## 架构说明

项目按 MVVM 加领域服务/适配器的方式组织，核心原则是“业务规则不进 View，外部依赖不进 ViewModel”。

### Model / State

`Models/` 保存纯数据状态和展示快照，例如：

- `TrendWorkbenchState`
- `TrendSeriesState`
- `TimeWindowState`
- `AxisSelectionState`
- `CrosshairState`
- `TrendSample`
- `TrendWorkbenchSettings`

这些类型不引用 Avalonia 控件，不读写文件，不调用 ScottPlot。

### Coordinator / Domain Service

`TrendWorkbenchCoordinator` 集中处理趋势工作台核心业务规则：

- 历史/实时模式切换。
- 时间窗口约束和最小跨度保护。
- Y1/Y2 轴候选变量规则。
- 单轴/双轴下的变量选择。
- 工程量程、自定义量程、刷子范围换算。
- 十字光标目标时间对应值读取。
- 从采样数据重建曲线状态。
- 生成 ViewModel 使用的展示快照。

这一层不操作 XAML 控件，不负责文件写入，也不直接渲染图表。

### ViewModel

`MainWindowViewModel` 承接页面展示状态和页面动作入口：

- 顶部状态文本。
- 当前趋势模式。
- 时间窗口显示。
- 左右轴面板状态。
- 变量卡片状态。
- 导出/保存/打印结果提示。
- 数据库路径和采样周期输入。

ViewModel 通过 `TrendWorkbenchApplicationService` 调用领域逻辑和外部服务，不直接依赖 ScottPlot 控件。

### View

`Views/MainWindow.axaml` 和 `Views/MainWindow.axaml.cs` 负责显示、控件初始化和必要的 UI 事件转发。

View 层允许保留这些必须依赖控件实例或像素坐标的逻辑：

- Avalonia 控件事件。
- 鼠标位置。
- 控件尺寸。
- 定时器 Tick 转发。
- 刷子和十字光标覆盖层绘制。

View 层不直接生成采样数据，不直接写数据库，不直接承担 CSV 导出、配置保存等应用动作。

### Adapter / Presenter / Service

外部依赖通过服务和适配器隔离：

- `ScottPlotTrendAdapter`：隔离 ScottPlot 曲线、坐标轴、坐标换算、PNG 保存和中文字体注册。
- `TrendWorkbenchApplicationService`：协调页面动作、配置、数据库、采集、导出、保存、打印。
- `SqliteTrendDataStore`：SQLite 历史数据存储实现。
- `ITrendDataStore`：历史数据读写接口，后续 MySQL 或其他存储应新增实现。
- `RandomTrendDataCollector`：默认模拟采集器。
- `ITrendDataCollector`：采集接口，真实设备采集应新增实现。
- `TrendCsvExportService`：CSV 文件导出。
- `TrendPlotImageExportService`：当前图像 PNG 保存。
- `TrendPlotPrintService`：打印入口，当前实现为保存图片后交给系统默认程序打开。
- `TrendOutputFileNameService`：统一输出文件命名。
- `TrendWorkbenchSettingsService`：配置文件读写和兜底归一化。

## 核心流程

### 启动

1. `App` 创建 `MainWindowViewModel`。
2. ViewModel 创建配置服务、领域协调器、SQLite 存储、随机采集器和应用服务。
3. 配置文件被读取并归一化。
4. SQLite 数据库初始化。
5. 如果数据库已有历史数据，则加载历史数据；否则生成一份 Demo 初始数据并写入数据库。
6. `ScottPlotTrendAdapter` 初始化曲线、坐标轴和字体。

### 历史模式

历史模式用于查看指定时间窗口内的数据。拖动 X 轴刷子、编辑起止时间、平移或缩放时间窗口时，工作台会进入历史模式。

### 实时模式

实时模式由 Avalonia 定时器触发一次采集动作，View 只转发 Tick；应用服务通过 `ITrendDataCollector` 采集数据，再写入 `ITrendDataStore`，最后让 Coordinator 滚动实时窗口。

默认采集器是 `RandomTrendDataCollector`，真实设备接入时建议新增实现，例如：

```csharp
public sealed class PlcTrendDataCollector : ITrendDataCollector
{
    public TrendSample Collect(DateTime timestamp, TrendWorkbenchCoordinator coordinator)
    {
        // 从真实设备读取变量值，并返回 TrendSample。
    }
}
```

## 配置文件

配置文件路径：

```text
trend-workbench.settings.json
```

当前主要配置项：

| 参数 | 说明 |
| --- | --- |
| `LayoutMode` | 布局模式，当前 JSON 中以枚举数值保存。`0` 为单轴，`1` 为双轴 |
| `ShowLegend` | 是否显示 ScottPlot 图例 |
| `DefaultVisibleDurationMinutes` | 默认历史窗口跨度，单位分钟 |
| `DefaultVisibleDurationSeconds` | 默认历史窗口跨度，单位秒，支持 10 秒级窗口 |
| `StorageProvider` | 当前为 `SQLite` |
| `SqliteDatabasePath` | SQLite 数据库路径 |
| `SamplingIntervalSeconds` | 实时采样周期，最小 1 秒 |
| `RealtimeWindowMinutes` | 实时窗口跨度，单位分钟 |
| `RealtimeWindowSeconds` | 实时窗口跨度，单位秒 |
| `PersistAxisGroupAssignments` | 是否把 UI 中修改的轴分组写回配置 |
| `AxisGroupAssignments` | 8 个变量的 Y1/Y2 分组，`0` 为 Y1，`1` 为 Y2 |

配置缺失、非法或导致某一侧轴没有候选变量时，服务会回退到安全默认值，避免现场因为配置文件损坏导致程序无法启动。

示例：

```json
{
  "LayoutMode": 1,
  "ShowLegend": false,
  "DefaultVisibleDurationMinutes": 5,
  "DefaultVisibleDurationSeconds": 300,
  "StorageProvider": "SQLite",
  "SqliteDatabasePath": "trend-workbench.db",
  "SamplingIntervalSeconds": 10,
  "RealtimeWindowMinutes": 5,
  "RealtimeWindowSeconds": 300,
  "PersistAxisGroupAssignments": false,
  "AxisGroupAssignments": [1, 0, 0, 0, 1, 1, 1, 1]
}
```

## 数据与输出文件

运行时会生成这些文件或目录：

- `trend-workbench.db`：SQLite 历史数据库。
- `output/`：CSV、PNG、打印预览图片等输出目录。
- `bin/`、`obj/`、`.vs/`：构建和 IDE 产物。

这些运行产物已在 `.gitignore` 中排除，不应提交到仓库。

## 兼容性说明

项目从 macOS 迁移到 Windows 后，已处理以下兼容性点：

- 删除 `.DS_Store` 和 `._*` 等 macOS 资源分叉文件。
- `.gitignore` 排除 macOS 迁移残留、构建产物、数据库和输出目录。
- 使用 `global.json` 固定 .NET 8 SDK。
- ScottPlot 显式注册 Windows 中文字体，避免画布标题、坐标轴、图例中文乱码。
- SQLite 使用 NuGet 自带 native runtime，不依赖系统 `sqlite3` 命令行工具。
- 打印入口按平台分支处理，Windows 使用 `UseShellExecute=true` 打开图片。

## 验证

推荐每次关键修改后运行：

```powershell
dotnet build AvaloniaApplication2.sln
dotnet run --project Tests\TrendWorkbench.Tests\TrendWorkbench.Tests.csproj
```

当前已验证：

- 构建通过。
- 测试控制台 21 项通过。
- GitHub 远程推送已改为 SSH。

人工回归可参考：

- `docs/REGRESSION_CHECKLIST.md`
- `docs/WORKBENCH_SETTINGS.md`

## 常见问题

### 构建时报 DLL 被占用

Windows 下正在运行的 `AvaloniaApplication2.exe` 会锁住 `bin\Debug\net8.0` 下的 DLL。重新构建前关闭工作台窗口，或执行：

```powershell
Get-Process AvaloniaApplication2 -ErrorAction SilentlyContinue | Stop-Process
```

### GitHub HTTPS 推送失败

当前仓库已改为 SSH remote：

```text
git@github.com:non-possession/AvaloniaHistoricalTrend.git
```

如果普通 SSH 22 端口不可用，本机 `~/.ssh/config` 已配置使用 GitHub 的 SSH-over-443 入口。

### 画布中文乱码

普通 Avalonia 控件字体由 XAML `FontFamily` 控制，但 ScottPlot 使用自己的 Skia 字体解析器。项目已在 `ScottPlotTrendAdapter` 中注册 `Microsoft YaHei` 和 `SimHei`，如果换机器后仍乱码，请确认 `C:\Windows\Fonts\msyh.ttc` 或 `C:\Windows\Fonts\simhei.ttf` 存在。

## 后续扩展建议

- 新增真实设备采集器，实现 `ITrendDataCollector`。
- 新增 MySQL 或工业数据库实现，实现 `ITrendDataStore`。
- 将数据库和配置默认目录迁移到 `%LOCALAPPDATA%`，避免双击启动和命令行启动时工作目录不同。
- 补充 UI 自动化或截图回归测试。
- 为轴分组、采样周期、数据库切换增加更完整的设置页面。
- 将 `docs/WORKBENCH_SETTINGS.md` 和 `docs/REGRESSION_CHECKLIST.md` 的历史编码问题整理为新的 UTF-8 中文文档。

