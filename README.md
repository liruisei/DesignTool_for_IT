# 工厂二维设计平台（Plant 2D Design Platform）

> Process / Instrumentation / Telecom / IT 2D Design Tools, instead of Smart Plant Instrumentation (Intergraph)
>
> 产品显示名称为「工厂二维设计平台 / Plant 2D Design Platform」，支撑工艺、仪表、电信及 IT 设计。解决方案、目录、程序集与命名空间仍沿用 `WinDesign.*`，不影响安装路径与代码引用。

C/S 架构的工程设计软件：**两个 WPF 桌面客户端** + ASP.NET Core 服务端 + PostgreSQL（开发环境可切 SQLite）。

- **管理客户端 `WinDesign.AdminClient`**：只做系统配置——工厂、项目配置、PBS管理、编码定义、用户管理。仅管理员可登录，可跨工厂维护，不绑定具体项目。
- **设计客户端 `WinDesign.DesignClient`**：只做设计数据——索引表、规格书、材料表、报表输出，并在左侧常驻只读 PBS 树（当前项目的 PBS 根节点及其子节点）。登录后必须选择「工厂 → 项目」，服务端按项目范围过滤数据。

两个客户端共用 `WinDesign.Client.Shared`（登录、会话、API 客户端、设备属性弹窗、主题），但各自只有自己的页面与导航，可独立发布。服务端通过令牌中的 `client` 声明做强制隔离：管理令牌不能调设计接口、设计令牌不能调管理接口。

当前为 **v0.8 双客户端拆分**：**工厂（企业）是 PBS 树的根实体**，工厂下可有多棵顶层 PBS 子树；每个项目指定其中一个顶层 PBS 节点作为「项目 PBS 根节点」，一个根节点可被多个项目共用。PBS 与设备归工厂所有，项目只是设计与报表范围（同一设备可纳入多个项目）。设备规格、材料附件、报表输出仍为第二阶段。

---

## 1. 技术栈

| 层 | 技术 |
|----|------|
| 客户端 | .NET 10 + WPF + MVVM（CommunityToolkit.Mvvm） |
| 服务端 | ASP.NET Core 10 Web API + JWT |
| 数据访问 | EF Core 10 |
| 数据库 | PostgreSQL（生产）/ SQLite（开发默认，开箱即用） |
| 认证 | JWT Bearer + PBKDF2 密码哈希 |
| API 文档 | Swagger / OpenAPI |

> 本机安装的是 .NET 10 SDK，因此目标框架为 `net10.0` / `net10.0-windows`。代码与 .NET 8 兼容；如需回退，把两个 csproj 的 `TargetFramework` 改为 `net8.0` / `net8.0-windows` 并安装 .NET 8 SDK 即可。

## 2. 目录结构

```
WinDesign/
├── WinDesign.slnx
├── docker-compose.yml              # PostgreSQL 16（可选）
├── docs/
│   ├── architecture.md             # 架构、数据模型、接口、路线图
│   ├── testing.md                  # 测试分层、运行方式、覆盖率、CI
│   └── ui-redesign.md              # SPI 参考、布局、视觉规范与交互规则
├── src/
│   ├── WinDesign.Domain/           # 实体、枚举
│   ├── WinDesign.Contracts/        # 客户端/服务端共享 DTO
│   ├── WinDesign.Infrastructure/   # EF Core DbContext、密码哈希、种子数据
│   ├── WinDesign.Api/              # ASP.NET Core Web API
│   ├── WinDesign.Client.Shared/    # 两个客户端共用：登录、会话、API、设备弹窗、主题
│   ├── WinDesign.AdminClient/      # 管理客户端（配置）
│   └── WinDesign.DesignClient/     # 设计客户端（索引 / 规格 / 材料 / 报表）
└── tests/
    ├── WinDesign.UnitTests/        # 密码哈希、JWT（含 client 声明）
    ├── WinDesign.Api.Tests/        # WebApplicationFactory + 临时 SQLite
    └── WinDesign.Client.Tests/     # 两个客户端的 ViewModel + FakeApiClient
```

## 3. 快速开始

### 3.1 启动服务端（开发环境，SQLite）

```bash
cd WinDesign
dotnet run --project src/WinDesign.Api --launch-profile http
```

- API：<http://localhost:5080>
- Swagger：<http://localhost:5080/swagger>
- 健康检查：<http://localhost:5080/health>
- 首次启动自动创建 `src/WinDesign.Api/windesign.db` 并写入种子数据。
- 种子数据：2 个工厂（PLANT-A / PLANT-B）；PLANT-A 下 2 个项目（DEMO-001 / DEMO-002），设备 PT-1001 同时纳入两个项目。

### 3.2 启动客户端

两个客户端可以同时运行，也可以单独部署。

**管理客户端**（仅管理员）：

```bash
cd WinDesign
dotnet run --project src/WinDesign.AdminClient
```

登录后服务端校验 Admin 角色，并换取「管理令牌」（不含工厂/项目）。进入主界面后只显示系统配置：项目配置、PBS管理、编码定义、用户管理；PBS 管理可在多个工厂之间切换并维护工厂本身。非管理员登录会被直接拒绝，并提示改用设计客户端。

**设计客户端**：

```bash
cd WinDesign
dotnet run --project src/WinDesign.DesignClient
```

登录成功后会弹出**工作区选择**：先单击工厂（PBS 根实体），再在项目列表中单击选中、**双击直接进入**（也可点「进入工作区」）。非管理员只会看到自己参与的项目；进入主界面后，所有数据接口都按该工厂/项目范围授权。左侧只有设计模块：**PBS（只读树，与「项目」同级常驻）**、索引表、规格书、材料表、报表输出。PBS 树只显示当前项目的 PBS 根节点及其子节点，维护仍在管理端。

进入设计主界面后，可通过工具栏的 **⇄ 切换工作区** 或「文件 → 切换工作区」在同厂不同项目（或不同工厂）之间切换。选择窗口会预选当前工厂/项目；**切换前会先自动保存**当前打开文档中尚未提交的表单编辑（索引表、PBS 等弹窗式编辑本身即时保存），若保存失败则取消切换。切换成功后重新签发令牌，并关闭旧文档、按新工作区打开索引表。

表单模块的保存规则：**编辑已有记录**时，鼠标点击表单外的其他区域即自动保存；**新建记录**必须点击「保存」按钮才会创建，切换工作区也不会自动创建半成品记录。

### 3.3 默认账号

| 用户名 | 密码 | 角色 | 可登录的客户端 | 权限 |
|--------|------|------|------|------|
| `admin` | `admin123` | Admin | 管理客户端 + 设计客户端 | 全部 |
| `designer` | `designer123` | Designer | 设计客户端 | 索引 / 规格 / 材料 / 报表 |
| `viewer` | `viewer123` | Viewer | 设计客户端 | 只读 |

> 管理客户端只接受 Admin；Designer / Viewer 在设计客户端中按角色获得只读或写入能力。
> 首次登录后请立即修改 `admin` 密码；生产环境必须替换 `appsettings.json` 中的 `Jwt:Key` 与数据库口令。仓库中的值均为开发占位，不要把真实密钥提交到版本库。

## 4. 已实现功能

| 模块 | 客户端 | 状态 | 说明 |
|------|--------|------|------|
| 登录 | 共用 | ✅ | 服务器地址、账号密码、JWT、错误提示、记住服务器/用户名 |
| 客户端拆分 | 共用 | ✅ | 管理 / 设计两个独立 exe，共用登录、会话、API 客户端、设备属性弹窗与主题 |
| 客户端隔离 | 服务端 | ✅ | 令牌带 `client` 声明；管理接口只接受管理令牌，设计接口只接受设计令牌，跨端调用返回 403 |
| 管理主界面 | 管理 | ✅ | 左侧系统配置导航；顶部只显示管理模块与用户，不绑定工厂/项目 |
| 项目配置 | 管理 | ✅ | 项目代号/名称/客户/地点/阶段/日期/状态，列表、搜索、编辑、归档；跨工厂维护并选择所属工厂与**项目 PBS 根节点**（工厂顶层节点，可被多个项目共用）；编辑栏默认收起，可展开/拖动调宽 |
| 编码定义 | 管理 | ✅ | 编码类型 + 编码项两级维护，系统类型保护；编辑栏默认隐藏，点选编码类型条目弹出类型编辑，点选编码项行弹出编码项编辑（只显示一个）；「收起编辑」位于编辑栏标题行内，不额外占行，可拖动调整宽度 |
| 用户管理 | 管理 | ✅ | 列表、搜索、新建、编辑、角色分配、重置密码、启用/停用、管理员保护；编辑栏默认收起，可展开/拖动调宽 |
| PBS管理 | 管理 | ✅ | **所有工厂同时作为根节点显示**，可独立展开/折叠；可新建/编辑/删除工厂；区域 → 装置 → 回路 → 设备目录树；右键菜单按层级裁剪 |
| 工作区选择 | 设计 | ✅ | 登录后先选工厂（PBS 根实体）再选项目；非管理员只看到参与的项目 |
| 设计主界面 | 设计 | ✅ | 左侧为「项目」「PBS」两个导航组；顶部工厂/项目上下文、切换工作区、刷新、当前用户/角色、退出 |
| PBS 浏览（设计端） | 设计 | ✅ | 与「项目」同级常驻，只读显示当前项目的 PBS 根节点及子节点；PBS 维护仍在管理端 |
| 切换工作区 | 设计 | ✅ | 工具栏/文件菜单可随时切换工厂或项目（预选当前工作区）；切换前自动保存待提交编辑，成功后关闭并重建当前文档，无需重新登录 |
| 表单自动保存 | 管理 | ✅ | 项目配置/编码定义/用户管理：编辑已有记录后，点击表单外的任意区域即自动保存；新建记录必须点击「保存」按钮才会创建 |
| 索引表录入 | 设计 | ✅ | 设备归工厂并纳入当前项目范围；自动显示完整 PBS 路径（区域/装置/回路）；网格浏览、双击属性弹窗、新建/编辑/复制/删除、位号查重与多维过滤；专业为 IT 时回路号不可填写 |
| 规格书 | 设计 | 🟡 第二阶段 | 实体与表已建，模板/版本下一阶段 |
| 材料表 | 设计 | 🟡 第二阶段 | 实体与表已建，汇总下一阶段 |
| 报表输出 | 设计 | 🟡 第二阶段 | 导航与页面占位，下一阶段接 Excel/Word/PDF |

## 5. 切换 PostgreSQL

1. 创建数据库（任选其一）：

```bash
# 本机已安装 PostgreSQL
createdb -U postgres windesign

# 或使用 Docker
docker compose up -d
```

2. 修改 `src/WinDesign.Api/appsettings.json`：

```json
{
  "Database": { "Provider": "Postgres" },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=windesign;Username=postgres;Password=postgres"
  }
}
```

3. 启动 API。首次启动使用 `EnsureCreated` 建表并写入种子数据。

> 骨架阶段使用 `EnsureCreated` 而非 EF Migrations。进入正式迭代后建议切换为 Migrations，以便版本化升级。

## 6. API 概览

令牌中的 `client` 声明决定可调用的接口：
- `auth`：刚登录、尚未选择客户端，只能完成工作区/管理模块选择；
- `admin`：管理客户端令牌，可调用用户、角色、工厂、项目、编码写入、PBS 写入；
- `design`：设计客户端令牌，可调用索引/设备、PBS 与编码读取。

| 方法 | 路径 | 客户端 | 权限 | 说明 |
|------|------|--------|------|------|
| POST | `/api/auth/login` | — | 匿名 | 登录，返回 `client=auth` 的身份 JWT |
| GET | `/api/auth/workspaces` | auth | 登录 | 当前用户可访问的工厂与项目（项目含 `pbsRootNodeId`） |
| POST | `/api/auth/workspace` | auth | 登录 | 选择工厂+项目，返回 `client=design`、带 `plantId` / `projectId` 的 JWT |
| POST | `/api/auth/management` | auth | Admin | 进入管理模块，返回 `client=admin`、不带工厂/项目的 JWT |
| GET | `/api/auth/me` | 任意 | 登录 | 当前用户与权限 |
| GET | `/api/plants` | admin | Admin | 全部工厂列表 |
| POST/PUT | `/api/plants`、`/api/plants/{id}` | admin | Admin/Designer | 新建/编辑工厂 |
| DELETE | `/api/plants/{id}` | admin | Admin | 删除空工厂（有项目/PBS/设备时拒绝） |
| GET | `/api/dashboard` | admin/design | 登录 | 统计与最近项目 |
| GET | `/api/users` | admin | Admin | 分页查询用户 |
| POST/PUT | `/api/users`、`/api/users/{id}` | admin | Admin | 新建/编辑用户 |
| POST | `/api/users/{id}/reset-password` | admin | Admin | 重置密码 |
| POST | `/api/users/{id}/toggle-active` | admin | Admin | 启用/停用 |
| GET | `/api/roles` | admin | 登录 | 角色列表 |
| GET | `/api/projects` | admin | 登录 | 项目列表/搜索 |
| POST/PUT | `/api/projects`、`/api/projects/{id}` | admin | Admin/Designer | 新建/编辑；`pbsRootNodeId` 必须是所属工厂的顶层 PBS 节点 |
| DELETE | `/api/projects/{id}` | admin | Admin | 归档项目 |
| GET | `/api/dictionaries/types` | admin/design | 登录 | 编码类型 |
| POST/PUT/DELETE | `/api/dictionaries/types...` | admin | Admin/Designer | 类型维护 |
| GET | `/api/dictionaries/types/{code}/items` | admin/design | 登录 | 编码项 |
| POST/PUT/DELETE | `/api/dictionaries/items...` | admin | Admin/Designer | 编码项维护 |
| GET | `/api/plants/{id}/pbs` | admin/design | 登录（当前工厂） | PBS 节点（平铺，含设备数） |
| POST | `/api/plants/{id}/pbs` | admin | Admin/Designer | 新建 PBS 节点（工厂即根节点，不接受 Root 类型） |
| PUT/DELETE | `/api/pbs/{id}` | admin | Admin/Designer | 编辑/删除 PBS 节点 |
| GET | `/api/projects/{id}/equipments` | design | 登录（当前项目） | 索引表查询，只返回纳入当前项目的设备 |
| POST | `/api/projects/{id}/equipments` | design | Admin/Designer | 在工厂下新建设备并纳入当前项目 |
| GET/PUT/DELETE | `/api/equipments/{id}` | design | 登录 / Admin/Designer | 设备详情/编辑/删除；非管理员限当前项目范围 |

请求示例见 `src/WinDesign.Api/WinDesign.Api.http`。

## 7. 与 WINPCCAD 的关系

本系统复用 WINPCCAD 的领域模型（仪表索引、工艺条件、规格书、材料、电缆/接线箱/端子等），但按新架构重新分层：

- 旧库结构导出与迁移工具保存在原 WINPCCAD 工作区（未包含在本仓库）；如需迁移，可单独引入 `docs/migration/` 下的导出与检查脚本。
- 旧系统的 `仪表索引表 / 工艺条件表 / 回路数据表 / 材料表` 等表，对应新系统的 `Equipment / EquipmentSpec / MaterialItem / Dictionary` 等实体。
- 建议按阶段迁移：先把用户/项目/字典/索引迁入新库，再迁规格书与材料，最后接报表与 CAD 输出。

## 8. 下一阶段路线图

1. **设备规格**：按设备类型定义字段模板、版本与审批状态、缺项检查。
2. **材料附件**：设备—材料—附件关联、按 PBS/图号汇总。
3. **报表输出**：Excel/Word/PDF 模板，预留 AutoCAD 出图接口。
4. **索引增强**：位号自动编制、Excel 批量导入导出、PBS 子树过滤。
5. **工程化**：EF Migrations、刷新令牌、审计日志界面。

## 9. 已知限制

- 当前覆盖工厂与多项目、工作区授权、管理端配置、设计端索引表；设备规格、材料附件、报表输出为第二阶段。
- 设备的跨项目范围已有数据模型（`project_equipments`）与种子数据，但暂未提供「把已有设备加入其他项目」的界面。
- 两个客户端分别发布；升级时需同时更新对应 exe 与共享库，服务端令牌中的 `client` 声明保证旧令牌不会越权。
- 开发库已按工厂—项目模型重建；旧库不再兼容，需要重新初始化。
- 未实现刷新令牌；JWT 过期后需重新登录。
- 使用 `EnsureCreated` 建库，暂不支持数据库版本升级。
- WPF 的密码框通过 code-behind 传值，未做附加属性绑定。
- 开发默认 SQLite；生产请切换 PostgreSQL 并修改默认 JWT 密钥。

## 10. 测试

已部署三层测试，共 **92 个用例**，全部通过：

| 项目 | 内容 | 依赖 |
|------|------|------|
| `WinDesign.UnitTests` | 密码哈希、JWT 签发（含 client / plantId / projectId 声明） | 无 |
| `WinDesign.Api.Tests` | 登录、工作区选择、客户端类型隔离、PBS 约束、设备索引 CRUD | 进程内 TestServer + 临时 SQLite |
| `WinDesign.Client.Tests` | 两个客户端的登录、导航结构、工作区切换、PBS 树与弹窗、索引网格与属性弹窗 | FakeApiClient，无 UI/服务端 |

运行：

```bash
dotnet test WinDesign.slnx
```

覆盖率：

```bash
dotnet test WinDesign.slnx --collect:"XPlat Code Coverage"
```

CI：`.github/workflows/dotnet-tests.yml`（windows-latest，推送/PR 自动运行）。

详细说明见 [docs/testing.md](docs/testing.md)。

## 11. UI/UX 设计

界面参考 Intergraph/Hexagon **SmartPlant Instrumentation** 的桌面工程软件模式重做：

- 顶部菜单栏 + 工具栏 + 工作区/用户上下文
- 左侧 **Domain Explorer** 导航树：管理端只有「系统」，设计端只有「项目」
- 中部多文档标签页（管理端：项目配置 / PBS管理 / 编码定义 / 用户管理；设计端：索引表 / 规格书 / 材料表 / 报表）
- 底部状态栏（工作区、当前文档、用户、服务器）
- 高密度 DataGrid、紧凑字体、工业蓝灰配色

详见 [docs/ui-redesign.md](docs/ui-redesign.md)。
