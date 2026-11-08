# WinDesign 架构说明（v0.1）

## 1. 总体架构

```
┌───────────────────────────── WPF 客户端 ─────────────────────────────┐
│ LoginWindow → WorkspaceSelectionWindow（工厂 → 项目） → MainWindow      │
│  MainWindow（导航壳） → 索引表 / 设备规格 / 材料 / 报表                │
│                         系统：项目配置 / PBS管理 / 编码定义 / 用户管理  │
│  MVVM：View ←→ ViewModel ←→ ApiClient（HttpClient + JWT）             │
└───────────────────────────────────┬──────────────────────────────────┘
                                    │ HTTPS / JSON
┌───────────────────────────────────▼──────────────────────────────────┐
│                      ASP.NET Core Web API                            │
│  Controllers（Auth / Plants / Projects / Pbs / Equipments / Users /  │
│               Roles / Dictionaries / Dashboard）                     │
│  JWT 认证 + 角色授权 + 审计日志                                        │
│  EF Core（Npgsql / Sqlite）                                           │
└───────────────────────────────────┬──────────────────────────────────┘
                                    │
                    ┌───────────────▼───────────────┐
                    │ PostgreSQL（生产）/ SQLite（开发）│
                    └───────────────────────────────┘
```

## 2. 分层

| 项目 | 职责 | 依赖 |
|------|------|------|
| `WinDesign.Domain` | 实体、枚举、领域常量 | 无 |
| `WinDesign.Contracts` | 客户端/服务端共享 DTO | Domain |
| `WinDesign.Infrastructure` | DbContext、密码哈希、种子数据 | Domain |
| `WinDesign.Api` | HTTP 接口、认证、授权、审计 | Domain / Contracts / Infrastructure |
| `WinDesign.Client` | WPF 界面、MVVM、API 客户端 | Contracts |

## 3. 认证与权限

1. 客户端 `POST /api/auth/login`，服务端校验 PBKDF2 密码哈希。
2. 服务端签发**身份令牌**，包含：
   - `sub` / `nameidentifier`：用户 ID
   - `name`：用户名
   - `role`：Admin / Designer / Viewer
   - `permission`：细粒度权限码（如 `projects.write`）
3. 登录后客户端调用 `GET /api/auth/workspaces`，只返回当前用户参与的工厂与项目。
4. 客户端选择「工厂 → 项目」后调用 `POST /api/auth/workspace`，服务端校验成员身份并签发**工作区令牌**，额外包含：
   - `plantId`：所选工厂（PBS 根节点）
   - `projectId`：所选项目（设计与报表范围）
5. 客户端把令牌放在 `Authorization: Bearer <token>`。所有数据接口从令牌中取工厂/项目并强制过滤：
   - 未选择工作区 → `400`；
   - 请求其他工厂或项目 → `403`。
   - 主界面内切换工作区时，复用 `POST /api/auth/workspace` 换发新令牌，并关闭旧文档、按新范围重建，无需重新登录。
6. 服务端：
   - `[Authorize]`：登录即可。
   - `[Authorize(Roles = "Admin")]`：用户管理、项目归档。
   - `[Authorize(Roles = "Admin,Designer")]`：工厂/项目/PBS/设备写入。
7. 权限码同时写入数据库 `permissions / role_permissions`，为后续菜单/按钮级授权预留。

## 4. 数据模型（v0.7：工厂 → 项目 → PBS）

工厂是 PBS 的根节点，PBS 与设备归工厂所有；项目只是设计与报表范围，通过 `project_equipments` 引用工厂设备，同一设备可被多个项目共享。

```
users ──< user_roles >── roles ──< role_permissions >── permissions

plants（工厂 = PBS 根节点）
  ├──< pbs_nodes（自关联树：区域 → 装置 → 回路 → ...）
  ├──< equipments ──< equipment_specs
  │        ├──< project_equipments >── projects
  │        └──< material_items
  └──< projects (owner) ──< project_members

dictionary_types ──< dictionary_items
audit_logs（独立流水）
```

| 实体 | 关键字段 | 说明 |
|------|----------|------|
| User | UserName、DisplayName、PasswordHash、IsActive | 账号 |
| Role | Code、Name | Admin / Designer / Viewer |
| Permission | Code、Module | 细粒度权限 |
| Plant | Code、Name、ClientName、Location、IsActive | 工厂 / 企业，PBS 根节点；一个工厂承载多个项目 |
| Project | PlantId、Code、Name、ClientName、Location、Phase、StartDate、EndDate、Status、OwnerUserId | 项目（设计 / 报表范围） |
| PbsNode | PlantId、ParentId、Code、Name、NodeType | 工厂分解结构；根节点由 Plant 承载 |
| Equipment | PlantId、PbsNodeId、TagNo、LoopNo、Name、TypeCode、Discipline、Status、Quantity、用途、安装位置、管道设备号、图纸/文档号、IO/KeyWord/Index、工艺条件/规格书 | 设备归工厂；PBS 路径由节点祖先计算；位号工厂内唯一 |
| ProjectEquipment | ProjectId、EquipmentId、AddedAt | 设备的项目范围，支持跨项目共享 |
| EquipmentSpec | EquipmentId、SpecCode、ContentJson、Version | 规格书 |
| MaterialItem | ProjectId、EquipmentId、Code、Name、Qty | 材料/附件 |
| DictionaryType/Item | Code、Name、SortOrder、IsActive | 编码定义 |
| AuditLog | UserName、Action、EntityType、EntityId | 审计 |

枚举：`ProjectStatus`、`Discipline`、`PbsNodeType`、`EquipmentStatus` 均以字符串存库，便于报表与人工排查。

## 5. 客户端结构

```
Views/                 LoginWindow、WorkspaceSelectionWindow、MainWindow、
                       Dashboard、项目配置、PBS管理、编码定义、用户管理、占位页
ViewModels/            Login、Main、Dashboard、WorkspaceSelection、Project、Pbs、
                       Dictionary、User、Placeholder
Services/              ApiClient、SessionService、ProjectContext、SettingsService
Converters/            StringToVisibilityConverter
Styles/Theme.xaml      颜色、按钮、输入框、DataGrid、导航样式
```

导航：左侧 `Domain Explorer` 分两组——

- **项目**：索引表、设备规格、材料附件、报表输出；
- **系统**：项目配置、PBS管理、编码定义、用户管理（仅 Admin）。

`MainViewModel` 按角色动态生成节点，`ExplorerNode.Open` 决定打开哪个文档。

## 6. 服务端接口约定

- 统一 JSON camelCase。
- 错误返回 `{ "message": "...", "details": "..." }`。
- 分页返回 `{ items, total, page, pageSize }`。
- 写操作记录 `audit_logs`。
- 用户管理仅 Admin；项目/编码写入为 Admin/Designer；只读用户只能查询。

## 6.1 工作区与工厂接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/auth/workspaces` | 当前用户可访问的工厂与项目（非管理员只返回已加入的项目） |
| POST | `/api/auth/workspace` | 选择工厂 + 项目，校验成员身份后签发带 `plantId` / `projectId` 的令牌 |
| GET | `/api/plants` | 当前用户可访问的工厂列表 |
| POST/PUT | `/api/plants`、`/api/plants/{id}` | 新建/编辑工厂（Admin/Designer） |

## 6.2 PBS 与设备索引接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/plants/{id}/pbs` | 工厂 PBS 节点平铺列表（含子节点数与设备数） |
| POST | `/api/plants/{id}/pbs` | 新建节点；校验编码唯一、上级归属；工厂即根节点，不接受 Root 类型 |
| PUT | `/api/pbs/{id}` | 编辑节点；校验循环引用、所属工厂 |
| DELETE | `/api/pbs/{id}` | 删除节点；有子节点或设备时拒绝 |
| GET | `/api/projects/{id}/equipments` | 索引表分页查询，只返回纳入当前项目的设备；支持 search/loopNo/discipline/status/pbsNodeId |
| POST | `/api/projects/{id}/equipments` | 在工厂下新建设备并自动纳入当前项目；位号工厂内唯一 |
| GET/PUT/DELETE | `/api/equipments/{id}` | 设备详情/编辑/删除；非管理员只能操作当前项目范围内的设备 |

## 7. 部署

### 开发
- API：`dotnet run --project src/WinDesign.Api --launch-profile http`
- 客户端：`dotnet run --project src/WinDesign.Client`
- 数据库：SQLite（`appsettings.Development.json`）

### 生产
- 数据库：PostgreSQL 16（`docker-compose.yml` 或既有实例）。
- API：`dotnet publish -c Release` 后部署为 Windows 服务 / IIS。
- 客户端：`dotnet publish -c Release -r win-x64 --self-contained false`，分发 `WinDesign.Client.exe`。
- 必须替换 `Jwt:Key`，并使用 HTTPS。

## 8. 设计决策

| 决策 | 原因 |
|------|------|
| WPF 而非 Web | 工业设计用户以 Windows 为主，后续需要 AutoCAD COM / 本地文件集成。 |
| 客户端/服务端分离 | 多人协作、集中权限、集中备份；客户端可离线缓存（后续）。 |
| EF Core | 与 .NET 同栈，PostgreSQL/SQLite 双提供程序，迁移现有表结构成本低。 |
| JWT + 角色/权限 | 无状态、易扩展；权限码为菜单/按钮级控制预留。 |
| 开发 SQLite | 无外部依赖，克隆即可运行；生产一键切 PostgreSQL。 |

## 9. 下一阶段

已完成：工厂（PBS 根节点）与多项目管理、登录工作区选择（工厂 → 项目）、按项目范围的数据授权、PBS 管理（区域 → 装置 → 回路 → 设备）、索引表录入（设备可在 PBS 中创建，索引表浏览/修改）、项目配置。

第二阶段：
1. 设备规格模板/版本 + 缺项校验。
2. 材料汇总 + Excel/Word/PDF 输出。
3. 索引增强：位号自动编制、Excel 批量导入导出、PBS 子树过滤。
4. EF Migrations、刷新令牌、审计日志界面。
5. 旧 WINPCCAD 数据迁移（导出与检查工具见原工作区 `docs/migration/`）。
