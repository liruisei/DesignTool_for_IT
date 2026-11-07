# UI/UX 重设计说明（参考 SmartPlant Instrumentation）

## 1. 参考资料

由于部分截图站点被当前网络策略拦截，界面结构主要取自 Hexagon 官方帮助、用户手册与公开培训资料：

- [Hexagon SmartPlant Instrumentation Help — Explorer 过滤](https://docs.hexagonppm.com/r/en-US/SmartPlant-Instrumentation-Help/2016-SP1-11.00.01/193377)
- [Hexagon SmartPlant Instrumentation Help — Custom View](https://docs.hexagonppm.com/r/en-US/SmartPlant-Instrumentation-Help/2016-SP1-11.00.01/193398)
- [SPI Basic Engineering User's Guide（PDF）](https://ia601502.us.archive.org/11/items/SchemSPIBasicEngineeringUsersGuide/Schem%20SPI%20Basic%20Engineering%20Users%20Guide.pdf)
- [SmartPlant Instrumentation Roadmap（PDF）](https://www.spi-ltuf.org/20160809/HxGN%202016%20-%20SPI%20Roadmap.pdf)
- [INtools v7 Tutorial（SlideShare）](https://www.slideshare.net/slideshow/i-ntoolsv7tutorial/9634680)
- [SmartPlant Instrumentation 概览（Prezi）](https://prezi.com/p/hloa7j6xdxki/charla-smartplant-instrumentation/)
- [Intergraph SPI Brochure（PDF）](http://www.sutera-analytics.com/v2/wp-content/uploads/2015/01/SmartPlant_Instrumentation_Brochure_small.pdf)

## 2. 从 SPI 提炼的界面模式

| SPI 元素 | 作用 | 在 WinDesign 中的对应 |
|----------|------|------------------------|
| Menu bar / Modules menu | 文件、编辑、视图、模块、表格、帮助 | 顶部菜单：文件、视图、模块、帮助 |
| Toolbar | 常用模块与操作入口 | 项目选择 + 项目信息/PBS/索引表/刷新 |
| **Domain Explorer** | 左侧树：Plant → Instruments / Loops / Panels / Cables 等 | 左侧 **Domain Explorer**：项目 → 项目信息/PBS/索引表/规格/材料/报表；系统 → 编码/用户 |
| **Browser**（宽视图网格） | 索引数据的表格浏览、排序、过滤、列配置 | 索引表页：DataGrid + 过滤条 + 表单/属性编辑 |
| **EDE**（Engineering Data Editor） | 工具栏 + 主编辑区 + EDE Explorer 三部分 | 索引表页：顶部操作 + 主网格 + 右侧属性编辑 |
| Property Dialog | 标签/回路的属性编辑（Tag Number Properties） | PBS 节点弹窗；索引行编辑区 |
| Status bar | 项目、用户、连接、状态 | 底部状态栏：项目、当前文档、用户、服务器 |
| Quick Search | 全局快速查找 | 索引表搜索框（位号/回路号/图号/规格书号） |

SPI 的关键不是某个控件，而是 **“Explorer 导航 + 多文档工作区 + 高密度数据网格 + 属性编辑”** 的组合。

## 3. 新版布局

```
┌────────────────────────────────────────────────────────────────────┐
│ 文件  视图  模块  帮助                                              │
├────────────────────────────────────────────────────────────────────┤
│ 项目 [DEMO-001 ▼]  ▤项目信息  ◇工厂PBS  ▦索引表  ⟳刷新   用户/角色 │
├──────────────┬─────────────────────────────────────────────────────┤
│ Domain       │  项目信息 × │ 工厂 PBS × │ 索引表 ×                 │
│ Explorer     │ ┌─────────────────────────────────────────────────┐ │
│              │ │                                                 │ │
│ ▣ 项目       │ │   当前文档（网格 / 树 / 表单）                   │ │
│   ▤ 项目信息 │ │                                                 │ │
│   ◇ 工厂 PBS │ │                                                 │ │
│   ▦ 索引表   │ │                                                 │ │
│   ✎ 设备规格 │ │                                                 │ │
│   ▣ 材料附件 │ │                                                 │ │
│   ▥ 报表输出 │ │                                                 │ │
│ ⚙ 系统       │ │                                                 │ │
│   ☰ 编码定义 │ └─────────────────────────────────────────────────┘ │
│   ● 用户管理 │                                                     │
├──────────────┴─────────────────────────────────────────────────────┤
│ 项目：DEMO-001   当前文档：索引表        admin | http://localhost   │
└────────────────────────────────────────────────────────────────────┘
```

## 4. 交互规则

- **Explorer 双击**打开模块；文件夹节点双击展开/折叠。
- **文档标签页**：同一模块只打开一次；重复打开时切回已有标签；项目信息为常驻标签，不可关闭。
- **菜单/工具栏**提供同一组命令，符合桌面工程软件习惯。
- **状态栏**持续显示项目、当前文档、用户与服务器。
- **索引表**保持“列表 + 属性编辑”模式：左侧网格用于浏览/筛选，右侧用于录入；后续可升级为 SPI 风格的 Tag Properties 弹窗。
- **PBS** 使用目录树 + 右键菜单 + 节点弹窗，支持新建根/子/同级、编辑、删除。
- **项目选择**在工具栏切换，PBS/索引表等项目级文档自动刷新。

## 5. 视觉规范

| 项目 | 取值 |
|------|------|
| 主背景 | `#F5F5F5` |
| 面板/网格 | `#FFFFFF` |
| 分隔线 | `#E5E5E5` |
| 主文字 | `#111111` |
| 次要文字 | `#6B6B6B` |
| 强调色 | `#111111`（黑白灰主色，参考 Tesla App 极简风） |
| 深色导航/登录 | `#111111` |
| 选中态 | `#EEEEEE` 浅灰底 + `#111111` 深色字 |
| 字体 | Microsoft YaHei UI，正文 12.5px |
| 网格行高 | 28px；表头 30px |
| 圆角 | 卡片 6px，按钮/输入 4px |

相比上一版：减少大圆角与大留白，提高信息密度；用菜单/工具栏/状态栏替代纯侧边导航，更接近工程桌面软件。

第二轮微调参考 Tesla App 的极简风格：主色改为黑/白/灰，取消蓝色选中高亮，左侧 Domain Explorer 选中项使用浅灰底和深色文字。

## 6. 后续 UI 工作

1. 索引表改为 SPI 风格 **Tag Properties 弹窗**（基本信息/安装/图纸/其他/工艺条件分页）。
2. 网格列过滤、分组、列宽/列顺序保存（SPI Browser / EDE 的 View Profile）。
3. 全局 Quick Search（跨索引、PBS、材料）。
4. 可停靠面板（Docking）与多窗口 Explorer（SPI 的 F7 多实例）。
5. 图标体系统一（模块图标、节点图标、状态图标）。
