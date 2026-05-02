# Workbench Settings

配置文件路径：`trend-workbench.settings.json`

当前稳定支持的参数：

- `LayoutMode`
  - 可选：`SingleAxis`、`DualAxis`
  - 文件缺失、字段缺失或非法值时回退到 `DualAxis`
- `ShowLegend`
  - `true` 或 `false`
  - 启动时读取，控制 ScottPlot 图例默认是否显示
- `DefaultVisibleDurationMinutes`
  - 启动时默认显示的时间跨度，单位分钟
  - 小于 `10` 的值视为非法，回退到 `1440`

默认行为：

- 文件不存在：使用内置默认值并自动创建文件
- 字段缺失：对缺失字段使用默认值
- 字段非法：
  - `LayoutMode` 非法时回退到 `DualAxis`
  - `DefaultVisibleDurationMinutes` 非法时回退到 `1440`

示例：

```json
{
  "LayoutMode": "DualAxis",
  "ShowLegend": false,
  "DefaultVisibleDurationMinutes": 1440
}
```

当前预留但未正式实现为稳定参数的方向：

- 默认可见变量
- 默认布局偏好
- 主题/样式偏好
