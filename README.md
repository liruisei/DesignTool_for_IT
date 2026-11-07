# DesignTool_for_IT — 仪表 / 电信 / IT 设计平台

> Instrumentation / Telecom / IT Design Tools, instead of Smart Plant Instrumentation (Intergraph)

C/S 架构的工程设计软件骨架：WPF 桌面客户端 + ASP.NET Core 服务端 + PostgreSQL（开发环境可切 SQLite）。

当前为 **v0.6 第一阶段精调**：项目信息录入、工厂 PBS 搭建、索引表录入已打通；PBS 按“区域 → 装置 → 回路 → 设备”组织，设备可直接在 PBS 中录入，索引表自动带出位号与 PBS 路径；索引表采用“网格浏览 + 双击属性弹窗”；设备规格、材料附件、报表输出作为第二阶段。

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
│   └── WinDesign.Client/           # WPF 客户端（MVVM）
└── tests/
    ├── WinDesign.UnitTests/        # 密码哈希、JWT
    ├── WinDesign.Api.Tests/        # WebApplicationFactory + 临时 SQLite
    └── WinDesign.Client.Tests/     # ViewModel + FakeApiClient
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

### 3.2 启动客户端

另开终端：

```bash
cd WinDesign
dotnet run --project src/WinDesign.Client
```

### 3.3 默认账号

| 用户名 | 密码 | 角色 | 权限 |
|--------|------|------|------|
| `admin` | `admin123` | Admin | 全部 |
| `designer` | `designer123` | Designer | 项目、编码（只读）、PBS、设备、规格、材料、报表 |
| `viewer` | `viewer123` | Viewer | 只读 |

> 首次登录后请立即修改 `admin` 密码；生产环境必须替换 `appsettings.json` 中的 `Jwt:Key` 与数据库口令。仓库中的值均为开发占位，不要把真实密钥提交到版本库。

## 4. 已实现功能

| 模块 | 状态 | 说明 |
|------|------|------|
| 登录 | ✅ | 服务器地址、账号密码、JWT、错误提示、记住服务器/用户名 |
| 主界面 | ✅ | 左侧导航、顶部项目选择、刷新、当前用户/角色、退出 |
| 工作台 | ✅ | 用户/项目/编码类型/设备统计、最近项目 |
| 项目信息录入 | ✅ | 项目代号/名称/客户/地点/阶段/开工与完成日期/说明，列表、搜索、编辑、归档 |
| 编码定义 | ✅ | 编码类型 + 编码项两级维护，系统类型保护 |
| 用户管理 | ✅ | 列表、搜索、新建、编辑、角色分配、重置密码、启用/停用、管理员保护 |
| 工厂 PBS | ✅ | 区域 → 装置 → 回路 → 设备目录树；右键新建根/子/同级、添加设备、编辑、删除；设备计数按子树汇总 |
| 索引表录入 | ✅ | 设备在 PBS 中录入后自动进入索引表，并显示完整 PBS 路径（区域/装置/回路）；索引表支持网格浏览、双击属性弹窗、新建/编辑/复制/删除、位号查重与多维过滤 |
| 设备规格 | 🟡 第二阶段 | 实体与表已建，模板/版本下一阶段 |
| 材料附件 | 🟡 第二阶段 | 实体与表已建，汇总下一阶段 |
| 报表输出 | 🟡 第二阶段 | 导航与页面占位，下一阶段接 Excel/Word/PDF |

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

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| POST | `/api/auth/login` | 匿名 | 登录，返回 JWT |
| GET | `/api/auth/me` | 登录 | 当前用户与权限 |
| GET | `/api/dashboard` | 登录 | 统计与最近项目 |
| GET | `/api/users` | Admin | 分页查询用户 |
| POST/PUT | `/api/users`、`/api/users/{id}` | Admin | 新建/编辑用户 |
| POST | `/api/users/{id}/reset-password` | Admin | 重置密码 |
| POST | `/api/users/{id}/toggle-active` | Admin | 启用/停用 |
| GET | `/api/roles` | 登录 | 角色列表 |
| GET | `/api/projects` | 登录 | 项目列表/搜索 |
| POST/PUT | `/api/projects`、`/api/projects/{id}` | Admin/Designer | 新建/编辑 |
| DELETE | `/api/projects/{id}` | Admin | 归档项目 |
| GET | `/api/dictionaries/types` | 登录 | 编码类型 |
| POST/PUT/DELETE | `/api/dictionaries/types...` | Admin/Designer | 类型维护 |
| GET | `/api/dictionaries/types/{code}/items` | 登录 | 编码项 |
| POST/PUT/DELETE | `/api/dictionaries/items...` | Admin/Designer | 编码项维护 |
| GET | `/api/projects/{id}/pbs` | 登录 | PBS 节点（平铺，含设备数） |
| POST | `/api/projects/{id}/pbs` | Admin/Designer | 新建 PBS 节点 |
| PUT/DELETE | `/api/pbs/{id}` | Admin/Designer | 编辑/删除 PBS 节点 |
| GET | `/api/projects/{id}/equipments` | 登录 | 索引表查询（搜索/回路号/专业/状态/PBS 过滤） |
| POST | `/api/projects/{id}/equipments` | Admin/Designer | 新建设备 |
| GET/PUT/DELETE | `/api/equipments/{id}` | 登录 / Admin/Designer | 设备详情/编辑/删除 |

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

- 第一阶段只覆盖项目信息、PBS、索引表；设备规格、材料附件、报表输出为第二阶段。
- 开发库已重建以加入新字段；旧库备份在 `%TEMP%\windesign_phase1_backup.db`。
- 未实现刷新令牌；JWT 过期后需重新登录。
- 使用 `EnsureCreated` 建库，暂不支持数据库版本升级。
- WPF 的密码框通过 code-behind 传值，未做附加属性绑定。
- 开发默认 SQLite；生产请切换 PostgreSQL 并修改默认 JWT 密钥。

## 10. 测试

已部署三层测试，共 **48 个用例**，全部通过：

| 项目 | 内容 | 依赖 |
|------|------|------|
| `WinDesign.UnitTests` | 密码哈希、JWT 签发与声明 | 无 |
| `WinDesign.Api.Tests` | 登录/权限、PBS 约束、设备索引 CRUD | 进程内 TestServer + 临时 SQLite |
| `WinDesign.Client.Tests` | 登录、PBS 树与节点/设备弹窗、索引网格与属性弹窗 ViewModel | FakeApiClient，无 UI/服务端 |

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

- 顶部菜单栏 + 工具栏 + 项目选择
- 左侧 **Domain Explorer** 导航树
- 中部多文档标签页（项目信息 / 工厂 PBS / 索引表 / 规格 / 材料 / 报表）
- 底部状态栏（项目、当前文档、用户、服务器）
- 高密度 DataGrid、紧凑字体、工业蓝灰配色

详见 [docs/ui-redesign.md](docs/ui-redesign.md)。
