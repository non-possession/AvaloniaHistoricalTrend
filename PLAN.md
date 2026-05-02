# 历史曲线工作台架构重构方案

## Summary
在不改变当前界面行为和交互效果的前提下，把当前“超大 `MainWindow.axaml.cs`”重构成 `View + ViewModel + TrendDomain/State + ScottPlot Adapter` 四层结构。  
目标是先把“状态、业务规则、图表渲染、控件桥接”拆开，让后续新增需求只改一层或两层，而不是每次都回到 View 的 code-behind 里连锁修改。

## Key Changes
### 1. 页面职责重分层
- `MainWindow.axaml` 保持现有布局和控件名，避免这次重构引入视觉回归。
- `MainWindow.axaml.cs` 收缩为“UI 桥接层”：
  - 只保留 Avalonia/ScottPlot 控件初始化
  - 只处理必须依赖控件实例的 Pointer/Size/Loaded 事件
  - 不再持有趋势业务状态、变量数组、量程规则、时间窗口规则
- `MainWindowViewModel` 升级为真正的页面状态入口：
  - 持有顶部状态文字、左右轴当前选中变量、变量卡状态、时间窗口显示状态
  - 暴露可绑定的属性和命令入口
  - 不直接调用 ScottPlot API

### 2. 抽出趋势工作台领域状态
- 新增一个独立状态模型，例如 `TrendWorkbenchState`，集中保存：
  - 8 个变量的名称、颜色、所属轴组、显隐状态
  - 每个变量的默认量程、自定义量程、当前刷子范围
  - 当前 `Y1` / `Y2` 选中变量
  - 当前时间窗口、总时间范围、十字光标状态
- 新增一个领域协调器，例如 `TrendWorkbenchCoordinator`，集中处理规则：
  - `Y1` 只轮换前 4 个变量，`Y2` 只轮换后 4 个变量
  - 每个变量的显示区间独立记忆
  - 切换当前轴变量时，其它曲线保持静态
  - 量程、刷子 fraction、显示范围之间的换算
  - 当前时间点对应变量值的读取
- 这一层不引用 Avalonia 控件，也不直接操作 XAML 元素。

### 3. 抽出 ScottPlot 适配层
- 新增一个图表适配器，例如 `ScottPlotTrendAdapter`，专门负责：
  - 创建和缓存 scatter / axis / plot objects
  - 根据 `TrendWorkbenchState` 刷新曲线、轴、limits、crosshair、brush guide line
  - 处理 ScottPlot 的 pixel/coordinate 转换
  - 屏蔽 ScottPlot 细节，避免 ViewModel 和领域层直接依赖图表库
- View 只把控件实例和 Pointer 坐标传给 adapter，不在 code-behind 里直接写 plot 业务逻辑。

### 4. 渐进式迁移顺序
- 第一阶段先迁移“静态状态和规则”：
  - 变量定义
  - 默认量程/自定义量程
  - 选中轴变量
  - 时间窗口
  - 刷子范围
- 第二阶段迁移“图表渲染和轴同步”到 adapter。
- 第三阶段迁移“变量卡、状态文字、量程面板”到 ViewModel 绑定。
- 第四阶段只保留必要的 UI 事件桥接在 `MainWindow.axaml.cs`。
- 这次重构不新增功能，不主动调整现有交互，不更改当前 UI 布局策略，除非为了消除重构阻塞。

### 5. 接口与类型调整
- 新增内部模型类型：
  - `TrendSeriesState`
  - `AxisSelectionState`
  - `TimeWindowState`
  - `CrosshairState`
- 新增内部服务/适配器类型：
  - `TrendWorkbenchCoordinator`
  - `ScottPlotTrendAdapter`
- `MainWindowViewModel` 对外只暴露页面绑定需要的属性集合，不暴露 ScottPlot 类型。
- 现有 View 层事件方法将逐步改成“调用 coordinator / adapter 的薄包装”。

## Test Plan
- 构建验证：
  - `dotnet build` 持续通过
- 回归场景：
  - 8 条曲线加载、显隐、变量卡状态显示正常
  - `Y1` / `Y2` 分组选择逻辑保持现状
  - 切换 `Y1` 或 `Y2` 当前变量时，其他曲线位置不跳动
  - 每个变量的量程和刷子范围记忆保持现状
  - X 轴时间刷子、Y1/Y2 刷子、十字光标交互保持现状
  - 顶部状态文字、右侧变量当前值、左右量程面板保持现状
- 领域层单测：
  - 量程换算
  - brush fraction 与显示区间换算
  - 变量分组轮换
  - 时间窗口边界保护
  - “切换当前变量但其他曲线不动”的状态更新规则

## Assumptions
- 这次重构以“结构优化”为目标，严格保持当前用户可见行为不变。
- ScottPlot 相关逻辑不强行纯 MVVM 化，而是通过 adapter 隔离。
- 当前 `MainWindow.axaml` 的控件结构和命名尽量保留，避免大规模 XAML 重写。
- 可以新增若干内部类和文件夹来承载状态模型、协调器和图表适配器。

***

# PLAN.md 收尾阶段的 6 项未完成清单

## Summary
当前重构已经完成主干拆层，剩余工作属于“收口型重构”。目标不是再引入新能力，而是把现有结构收紧到可长期维护、可验证、可交接的状态。下面这 6 条按优先级给出明确边界、完成标准和可提交证据。

## Key Changes

### 1. 将 `MainWindow.axaml.cs` 继续收缩到“纯桥接层”
**边界**
- 允许保留：
  - Avalonia 控件初始化
  - 必须依赖控件实例或像素坐标的桥接调用
  - 把事件转发给 helper/presenter/adapter
- 不再允许继续新增：
  - 业务规则判断
  - 时间窗口规则
  - 轴切换规则
  - 重复的交互状态计算
- 本项不要求彻底移除所有事件方法，但要求事件方法本身变成薄包装。

**完成标准**
- `MainWindow.axaml.cs` 不再直接包含重复的时间窗口计算、刷子距离判断、十字光标线段赋值逻辑。
- 至少将以下 4 组交互各自收敛成清晰 helper/presenter：
  - 左 Y 刷子
  - 右 Y 刷子
  - X 轴时间刷子
  - 十字光标
- `MainWindow.axaml.cs` 体量继续下降，且阅读时能明显按“桥接职责”分段。

**证据**
- 代码层：
  - `Views/Interaction/` 或类似目录下新增/完善交互 helper/presenter
  - `MainWindow.axaml.cs` 行数继续下降，且不存在大段重复 Pointer 逻辑
- 构建层：
  - `dotnet build` 通过
- 审查层：
  - 代码 review 可以指出 `MainWindow` 中每个事件方法只负责“调用谁”，而不是“决定怎么做”

---

### 2. 为领域层补齐单元测试
**边界**
- 测试对象聚焦于不依赖 Avalonia 控件的纯规则层：
  - `TrendWorkbenchCoordinator`
  - 相关模型换算逻辑
- 不在本项中做 UI 自动化测试。
- 不在本项中做 ScottPlot 渲染截图测试。

**完成标准**
至少覆盖以下规则：
- 量程换算正确
- brush fraction 到显示区间换算正确
- 时间窗口最小跨度保护正确
- 左右轴变量轮换规则正确
- 单轴/双轴模式下选择规则正确
- “切换当前变量但其他曲线状态保持”规则正确

**证据**
- 新增测试项目或测试文件
- 至少有一组可运行的自动化测试命令
- 测试输出证明：
  - 测试通过
  - 核心规则被断言覆盖
- 代码 review 可指出每条 PLAN.md 中的领域规则至少有对应测试名

---

### 3. 将刷子/十字光标交互完整 presenter/helper 化
**边界**
- 本项是第 1 条的深化，但聚焦“交互模块化”，不是单纯减行数。
- 要求交互代码按功能域拆开，而不是零散提几个工具函数就结束。
- 不要求做全新的交互行为变更，只要求把现有行为结构化。

**完成标准**
至少形成以下清晰模块中的大部分：
- `YBrushInteraction` 或等价模块
- `XBrushInteraction` 或等价模块
- `CrosshairInteraction` 或等价模块
- 如有必要，左右 Y 刷子可共享一个统一交互模块
- `MainWindow` 中不再自己维护大段“按下/移动/释放”的完整流程

**证据**
- 新增的 helper/presenter 不是只有 1-2 个函数，而是能表达完整交互责任
- `MainWindow.axaml.cs` 中对应事件方法明显退化为转发
- 构建通过，且现有交互回归不坏：
  - Y 刷子还能拖
  - X 轴刷子还能拖
  - 十字光标还能显示/隐藏

---

### 4. 让 `MainWindowViewModel` 从“展示状态容器”升级为“交互入口”
**边界**
- 不强求纯命令化到每个 Pointer 事件都进 ViewModel。
- 重点是把页面级动作入口提升到 ViewModel，而不是继续留在 code-behind。
- ScottPlot 像素级操作仍可留在 adapter/helper，不硬塞进 ViewModel。

**完成标准**
- 至少页面级动作应有明确 ViewModel 入口或等价 presentation action：
  - 切换布局模式
  - 选择时间跨度
  - 变量卡切轴选择
  - 可能的话包括量程应用/恢复默认
- `MainWindow` 不再自己承担这些动作的状态编排，只负责把 UI 行为转给 ViewModel/协调器。

**证据**
- `MainWindowViewModel` 中新增明确的页面交互入口
- `MainWindow.axaml.cs` 中对应按钮点击逻辑减少为一层调用
- review 时可以说清：
  - “页面级动作在哪一层定义”
  - “控件级像素交互在哪一层定义”

---

### 5. 将 `trend-workbench.settings.json` 扩展为正式的工作台参数入口
**边界**
- 本项不是“想到什么都配”，而是建立稳定、可解释、可持续扩展的配置边界。
- 当前至少覆盖“启动时就需要决定”的工作台默认项。
- 不在本项中引入复杂配置热加载，默认采用启动时读取。

**完成标准**
配置模型至少明确分成两类：
- 已支持并稳定生效的参数
  - `LayoutMode`
  - `ShowLegend`
  - `DefaultVisibleDurationMinutes`
- 明确预留可扩展方向，但不提前做复杂配置系统
  - 默认可见变量
  - 默认布局偏好
  - 主题/样式偏好
- 需要定义清楚：
  - 文件不存在时的默认值
  - 文件字段缺失时的回退值
  - 非法值时的兜底行为

**证据**
- 项目目录存在稳定格式的配置文件
- 配置模型类和服务类职责明确
- 可通过修改配置文件并重启，肉眼验证至少 3 个参数确实生效
- 有最少的配置读取测试或行为验证说明

---

### 6. 输出一份完整的回归验证清单
**边界**
- 本项不等于“口头说已验证”。
- 需要沉淀成清晰的人工回归步骤，最好可放进文档。
- 不要求全部自动化，但至少要可重复执行。

**完成标准**
回归清单至少覆盖：
- 8 条曲线加载和显隐
- 单轴/双轴切换
- `Y1/Y2` 变量切换
- 每个变量量程和刷子记忆
- X 轴时间刷子和平移/缩放
- 十字光标显示与退出
- 配置文件生效
- 构建通过

**证据**
- 仓库中存在一份明确的验证文档或测试清单
- 每条清单项包含：
  - 操作步骤
  - 预期结果
- 最终交付时能附带一次实际执行结果摘要

## Test Plan
- 构建验证：
  - `dotnet build` 持续为 `0` 警告、`0` 错误
- 单元测试验证：
  - 领域层测试全部通过
- 人工回归验证：
  - 按第 6 条清单执行，所有关键交互无回退
- 配置验证：
  - 修改 `trend-workbench.settings.json` 后重启，布局模式、图例开关、默认时间跨度生效

## Assumptions
- 当前阶段以“结构收口和验证补齐”为主，不主动新增新的业务功能。
- ScottPlot 仍通过 adapter/h​​elper 隔离，不追求完全纯 MVVM。
- UI 自动化不是本阶段必做项，优先完成领域测试和人工回归清单。
- 对 `PLAN.md` 的“完成”判定，以这 6 条全部满足完成标准并具备对应证据为准。

# View 层瘦身实施计划与 AGENTS.md 架构说明补充

## Summary
实施上一轮瘦身计划，但按低风险顺序推进：先把架构职责写入 `AGENTS.md`，再迁移最明显不属于 View 的 CSV 导出和页面级动作，最后把控件显式同步封装起来。目标是减少 `MainWindow.axaml.cs` 的业务编排职责，同时保持当前应用行为不回退。

## Key Changes

### 1. 更新 `AGENTS.md` 的 MVVM 分层说明
- 在 `AGENTS.md` 的 MVVM 部分补充各层职责：
  - `Model/State`：保存趋势变量、时间窗口、量程、刷子、十字光标等纯数据状态。
  - `Coordinator/Domain Service`：处理业务规则，例如变量显隐、轴选择、量程换算、时间窗口约束。
  - `ViewModel`：承接页面展示状态和页面级动作入口，不直接依赖 ScottPlot 控件。
  - `View`：只显示数据、接收用户操作、转发控件事件；不写业务规则。
  - `Adapter/Presenter/Service`：隔离 ScottPlot、CSV 文件写入、配置文件读写等外部依赖。
- 明确规定：后续新增功能优先放进对应层，不直接塞进 `MainWindow.axaml.cs`。

### 2. 把 CSV 导出从 View 移到 ViewModel/应用服务
- 新增一个轻量应用服务，例如 `TrendWorkbenchApplicationService`，统一持有：
  - `TrendWorkbenchCoordinator`
  - `TrendCsvExportService`
  - `TrendWorkbenchSettingsService`
- `MainWindowViewModel` 新增 `ExportStatusText`，并通过应用服务执行导出。
- `MainWindow.axaml.cs` 中的 `ExportCurrentWindowCsv()` 删除或退化为一行转发。
- CSV 导出行为保持不变：
  - 当前 X 轴窗口
  - 当前勾选变量
  - 输出到 `output`
  - 缺失值为空

### 3. 把页面级动作从 View 迁到应用服务
- 把以下动作从 `MainWindow.axaml.cs` 中逐步迁出：
  - 单轴/双轴切换
  - 固定时间跨度选择
  - 时间窗口平移、跳转、缩放
  - 变量卡选择轴变量
  - 自定义量程应用/恢复默认
- View 按钮事件保留，但只调用 ViewModel 方法。
- ViewModel 调应用服务完成状态更新，并返回是否需要刷新图表/控件。

### 4. 封装仍需保留的控件显式同步
- 新增 `ViewControlSynchronizer` 或等价 helper。
- 将这些容易失效的显式同步集中封装：
  - X 轴左右边界时间文本
  - `Y1/Y2` 下拉选项
  - 当前参考变量文本
  - 当前量程文本
  - 变量名输入框
  - 右侧变量值文本
- 第一阶段不强行删除这些同步，避免再次出现 UI 不显示或显隐失效。
- `MainWindow.axaml.cs` 只调用同步器，不直接散落多段控件赋值。

### 5. 保持像素级交互仍在 View/Interaction helper
- `YBrush`、`XBrush`、`Crosshair` 继续保留在 View 与 `Views/Interaction` helper 内。
- 它们只处理控件尺寸、鼠标位置、hover/drag 视觉状态。
- 业务状态变化仍调用应用服务或 coordinator，不在 helper 中自行决定业务规则。

## Test Plan
- 运行 `dotnet build`，要求 `0` 警告、`0` 错误。
- 运行 `dotnet run --project Tests/TrendWorkbench.Tests/TrendWorkbench.Tests.csproj`，要求全部通过。
- 扩展测试覆盖：
  - CSV 导出仍只包含当前可见变量。
  - 时间窗口动作迁出后仍能正确修改 visible start/end。
  - 单轴/双轴模式切换仍更新状态。
- 人工回归重点：
  - X 轴起止时间启动可见。
  - 变量显隐仍能控制曲线隐藏/恢复。
  - 右侧变量卡随十字光标更新值。
  - `Y1/Y2` 下拉框有选项且可切换。
  - 当前参考变量和当前量程仍显示。
  - CSV 按钮仍能导出文件到 `output`。

## Assumptions
- 本轮不追求完全移除所有 code-behind 逻辑；像素坐标相关交互仍允许留在 View 层。
- 为避免 UI 再次失效，控件显式同步先集中封装，不立即删除。
- CSV 导出和配置读写属于服务层，不属于 View。
- 应用服务是当前项目里连接 ViewModel 与 Coordinator/Service 的推荐扩展点。

# 历史/实时趋势、可插拔数据源与可配置双轴分组实施计划

## Summary
把当前静态趋势 Demo 升级为“可插拔存储 + 可插拔采集 + 历史/实时双模式”的工作台。第一版采用接口化设计，实际落地 SQLite；实时采集默认每 1 秒生成随机数据并立即写入存储；实时曲线显示最近 1 小时。双轴模式下，Y1/Y2 的候选变量集合不再硬编码为前 4/后 4，而是由配置决定，默认仍保持前 4 个 Y1、后 4 个 Y2。

## Key Changes
- 新增数据模型与接口层：
  - `TrendSample`：表示一个采样时刻下的变量值集合。
  - `ITrendDataStore`：历史数据读写接口，支持初始化、追加采样、按时间段读取。
  - `SqliteTrendDataStore`：第一版真实存储实现。
  - `ITrendDataCollector`：采集接口，第一版实现 `RandomTrendDataCollector`。
- 扩展工作台模式：
  - 新增 `TrendMode`：`Historical` / `Realtime`。
  - `Historical Trend` 按钮切换到历史曲线，显示选中时间段内的静态数据。
  - `Real Trend` 和 `Now` 切换到当前曲线，显示最近 1 小时并随新数据自动滚动。
  - 拖动时间滑块或手动修改起止时间时，自动切换为历史曲线。
- 扩展双轴候选集合：
  - 新增轴分组配置，例如 `AxisGroupAssignments`，保存每个变量属于 `Y1` 或 `Y2`。
  - 默认值为变量 `0..3` 属于 Y1，变量 `4..7` 属于 Y2。
  - 每个变量第一版只能属于一个轴，不允许同时属于 Y1/Y2。
  - Y1/Y2 下拉框候选项来自配置分组，不再使用硬编码切片。
  - `SelectSeriesFromCard`、变量卡高亮、轴轮换、显隐后的自动兜底选择都改为按配置分组判断。
- 配置与持久化：
  - `trend-workbench.settings.json` 增加 `PersistAxisGroupAssignments`，用于控制轴分组修改是否写回配置。
  - 第一版分组修改入口以配置文件为主，UI 保持现有交互；后续可再加设置面板。
  - 若后续 UI 修改分组且 `PersistAxisGroupAssignments=true`，修改后立即保存。
  - 配置缺失、非法或导致某一轴无变量时回退到默认前 4/后 4。
- UI 调整：
  - 底部控制区新增起始/结束时间输入，按年、月、日、时、分、秒输入。
  - 保留现有时间滑块；输入框和滑块共同驱动历史时间窗口。
  - 保留现有 Y1/Y2 变量下拉框，但候选项改为来自轴分组配置。

## Architecture Placement
- `Model/State`：新增采样点、趋势模式、轴分组状态、采集配置。
- `Coordinator/Domain Service`：处理历史/实时切换、时间窗口约束、实时窗口滚动、轴分组规则、Y1/Y2 候选选择。
- `ViewModel`：暴露历史/实时切换、Now、起止时间输入、Y1/Y2 当前变量选择等页面动作。
- `View`：只负责显示控件和转发事件；不直接访问数据库、不生成随机数据、不判断轴分组业务规则。
- `Adapter/Presenter/Service`：SQLite 存储、随机采集、实时采集循环、ScottPlot 渲染全部通过服务或适配器隔离。

## Test Plan
- 存储与采集：
  - SQLite 可初始化、追加采样、按时间段读取。
  - 随机采集器生成值落在变量当前值域内。
  - 实时采集数据立即写入存储，切回历史后可查询。
- 历史/实时模式：
  - `Real Trend` 和 `Now` 切换到实时模式，窗口为最近 1 小时。
  - 新数据到来后实时窗口自动滚动。
  - 手动输入时间或拖动时间滑块后自动切换到历史模式。
- 双轴分组：
  - 默认配置保持前 4 个变量为 Y1 候选，后 4 个变量为 Y2 候选。
  - 修改配置后，Y1/Y2 下拉框候选项按配置更新。
  - 轴轮换、变量卡点击、显隐兜底选择均遵守配置分组。
  - 非法配置会回退默认分组。
- 回归：
  - 现有 8 条曲线显隐、Y1/Y2 量程、导出 CSV、保存图片、打印继续可用。
  - `dotnet build` 通过，现有测试继续通过，并新增领域层测试覆盖上述规则。

## Assumptions
- 第一版真实数据库只实现 SQLite；MySQL 只通过 `ITrendDataStore` 预留。
- 实时采集默认每 1 秒执行一次，配置文件可改；本轮不做采集频率 UI。
- 实时数据立即写入 SQLite。
- 当前曲线默认显示最近 1 小时并自动滚动。
- Y1/Y2 轴候选变量集合第一版通过配置文件修改；变量只能属于一个轴。
- `PersistAxisGroupAssignments` 先作为配置能力保留，后续新增 UI 修改分组时按该开关决定是否立即写回配置。
  
