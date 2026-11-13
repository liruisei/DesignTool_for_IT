# 工厂二维设计平台 架构说明（v0.1）

## 1. 总体架构

```
┌──────────────── 管理客户端 WinDesign.AdminClient ────────────────┐
│ LoginWindow（仅 Admin）→ MainWindow                                │
│  导航：项目配置 / PBS管理 / 编码定义 / 用户管理                      │
│  令牌：client=admin，无 plantId / projectId                        │
└──────────────────────────────┬───────────────────────────────────┘
                               │ HTTPS / JSON
┌──────────────── 设计客户端 WinDesign.DesignClient ───────────────┐
│ LoginWindow → WorkspaceSelectionWindow（工厂 → 项目）→ MainWindow   │
│  导航：索引表 / 规格书 / 材料表 / 报表输出                          │
│  令牌：client=design，带 plantId / projectId                       │
└──────────────────────────────┬───────────────────────────────────┘
                               │
        ┌──────────────────────┴──────────────────────┐
        │  WinDesign.Client.Shared（两个客户端共用）    │
        │  登录窗口 / Session / ApiClient / 主题 /      │
        │  设备属性弹窗                                 │
        └──────────────────────┬──────────────────────┘
                               │ HTTPS / JSON
┌──────────────────────────────▼───────────────────────────────────┐
│                      ASP.NET Core Web API                         │
│  Controllers（Auth / Plants / Projects / Pbs / Equipments / Users /│
│               Roles / Dictionaries / Dashboard）                  │
│  JWT 认证 + 角色授权 + 客户端类型策略 + 审计日志                    │
│  EF Core（Npgsql / Sqlite）                                        │
└──────────────────────────────┬───────────────────────────────────┘
                               │
                 ┌─────────────▼─────────────┐
                 │ PostgreSQL（生产）/ SQLite（开发）│
                 └───────────────────────────┘
```

## 2. 分层

| 项目 | 职责 | 依赖 |
|------|------|------|
| `WinDesign.Domain` | 实体、枚举、领域常量 | 无 |
| `WinDesign.Contracts` | 客户端/服务端共享 DTO | Domain |
| `WinDesign.Infrastructure` | DbContext、密码哈希、种子数据 | Domain |
| `WinDesign.Api` | HTTP 接口、认证、授权、审计 | Domain / Contracts / Infrastructure |
| `WinDesign.Client.Shared` | 登录窗口、Session、ApiClient、主题、设备属性弹窗 | Contracts |
| `WinDesign.AdminClient` | 管理客户端：项目配置 / PBS / 编码 / 用户 | Client.Shared / Contracts |
| `WinDesign.DesignClient` | 设计客户端：索引表 / 规格书 / 材料表 / 报表 | Client.Shared / Contracts |

## 3. 认证与权限

1. 客户端 `POST /api/auth/login`，服务端校验 PBKDF2 密码哈希。
2. 服务端签发**身份令牌**（`client=auth`），包含：
   - `sub` / `nameidentifier`：用户 ID
   - `name`：用户名
   - `role`：Admin / Designer / Viewer
   - `permission`：细粒度权限码（如 `projects.write`）
   - `client`：客户端类型，此阶段为 `auth`
3. **管理客户端**调用 `POST /api/auth/management`，服务端校验 Admin 后签发**管理令牌**（`client=admin`，无 `plantId` / `projectId`）。管理端把“管理员 + 无工作区”识别为跨工厂配置模式。
4. **设计客户端**登录后调用 `GET /api/auth/workspaces`，只返回当前用户参与的工厂与项目；选择「工厂 → 项目」后调用 `POST /api/auth/workspace`，服务端校验成员身份并签发**设计令牌**（`client=design`），额外包含：
   - `plantId`：所选工厂（PBS 根节点）
   - `projectId`：所选项目（设计与报表范围）
5. 客户端把令牌放在 `Authorization: Bearer <token>`。设计接口从令牌中取工厂/项目并强制过滤：
   - 未选择工作区 → `400`；
   - 请求其他工厂或项目 → `403`；
   - 主界面内切换工作区时，复用 `POST /api/auth/workspace` 换发新令牌，并关闭旧文档、按新范围重建，无需重新登录。
6. 服务端授权分两层：
   - **角色**：`[Authorize(Roles = "Admin")]` 用户管理、项目归档；`[Authorize(Roles = "Admin,Designer")]` 配置/设备写入。
   - **客户端类型策略**：
     - `AdminClient`：只接受 `client=admin`，用于用户、角色、工厂、项目、编码写入、PBS 写入；
     - `DesignClient`：只接受 `client=design`，用于索引表 / 设备等设计数据；
     - `ClientAccess`：接受 admin 或 design，用于编码/PBS 读取、工作台统计（管理端 PBS 管理页需要读取 PBS 与编码）。
   - 即使是 Admin，只要持有设计令牌，也不能调用管理接口；反之亦然。
7. 权限码同时写入数据库 `permissions / role_permissions`，为后续菜单/按钮级授权预留。

## 4. 数据模型（v0.8：工厂 → 项目 → PBS）

工厂是 PBS 的根实体，PBS 与设备归工厂所有；每个项目通过 `PbsRootNodeId` 指定工厂下的一个顶层 PBS 节点作为自己的根节点（一个根可被多个项目共用），设计端左侧 PBS 树只显示该根及其子树。项目只是设计与报表范围，通过 `project_equipments` 引用工厂设备，同一设备可被多个项目共享。

```
users ──< user_roles >── roles ──< role_permissions >── permissions

plants（工厂 = PBS 根节点）
  ├──< pbs_nodes（自关联树：区域 → 装置 → 回路 → ...）
  ├──< equipments ──< equipment_specs
  │        ├──< project_equipments >── projects
  │        └──< material_items
  └──< projects (owner, pbs_root_node_id → pbs_nodes) ──< project_members

dictionary_types ──< dictionary_items
audit_logs（独立流水）
```

| 实体 | 关键字段 | 说明 |
|------|----------|------|
| User | UserName、DisplayName、PasswordHash、IsActive | 账号 |
| Role | Code、Name | Admin / Designer / Viewer |
| Permission | Code、Module | 细粒度权限 |
| Plant | Code、Name、ClientName、Location、IsActive | 工厂 / 企业，PBS 根节点；一个工厂承载多个项目 |
| Project | PlantId、PbsRootNodeId、Code、Name、ClientName、Location、Phase、StartDate、EndDate、Status、OwnerUserId | 项目（设计 / 报表范围）；PbsRootNodeId 指向所属工厂的顶层 PBS 节点，被多个项目共用 |
| PbsNode | PlantId、ParentId、Code、Name、NodeType | 工厂分解结构；根节点由 Plant 承载 |
| Equipment | PlantId、PbsNodeId、TagNo、LoopNo、Name、TypeCode、Discipline、Quantity、用途、安装位置、管道设备号、图纸/文档号、IO/KeyWord/Index | 设备归工厂；PBS 路径由节点祖先计算；位号工厂内唯一；专业为 IT 时不使用回路号 |
| ProjectEquipment | ProjectId、EquipmentId、AddedAt | 设备的项目范围，支持跨项目共享 |
| EquipmentSpec | EquipmentId、SpecCode、ContentJson、Version | 规格书 |
| MaterialItem | ProjectId、EquipmentId、Code、Name、Qty | 材料/附件 |
| DictionaryType/Item | Code、Name、SortOrder、IsActive | 编码定义 |
| AuditLog | UserName、Action、EntityType、EntityId | 审计 |

枚举：`ProjectStatus`、`Discipline`、`PbsNodeType` 均以字符串存库，便于报表与人工排查。

## 5. 客户端结构

```
WinDesign.Client.Shared/
  Views/               LoginWindow、EquipmentPropertyDialog
  ViewModels/          Login、ViewModelBase、ExplorerNode、EquipmentEditor、PbsNodeOption
  Services/            ApiClient、SessionService、ProjectContext、SettingsService、
                       EquipmentDialogService
  Converters/          StringToVisibilityConverter
  Styles/Theme.xaml    颜色、按钮、输入框、DataGrid、导航样式

WinDesign.AdminClient/
  MainWindow           管理外壳（无工作区切换）
  Views/               ProjectManagement、Pbs、PbsNodeDialog、PlantDialog、
                       Dictionary、UserManagement、EditorAutoSave
  ViewModels/          Main、Project、Pbs、PbsNodeEditor、PlantEditor、Dictionary、User
  Services/            PbsDialogService

WinDesign.DesignClient/
  MainWindow           设计外壳（支持工作区切换）
  Views/               WorkspaceSelectionWindow、EquipmentIndex、EquipmentPropertyDialog 调用方、
                       ModulePlaceholder
  ViewModels/          Main、WorkspaceSelection、EquipmentIndex、ModulePlaceholder
```

导航：两个客户端各自维护 `ExplorerNode` 树，互不引用对方的页面。

- **管理客户端**：只有「系统」——项目配置、PBS管理、编码定义、用户管理；
- **设计客户端**：两个同级导航组——「项目」（索引表、规格书、材料表、报表输出）与「PBS」（只读，显示当前项目 PBS 范围：指定根节点的子树，根节点为空时显示全厂顶层节点）。

`ExplorerNode.Open` 决定打开哪个文档；管理端默认打开项目配置，设计端默认打开索引表。设备属性弹窗由 `Client.Shared` 提供，管理端 PBS 树与设计端索引表共用。

## 6. 服务端接口约定

- 统一 JSON camelCase。
- 错误返回 `{ "message": "...", "details": "..." }`。
- 分页返回 `{ items, total, page, pageSize }`。
- 写操作记录 `audit_logs`。
- 用户管理仅 Admin；项目/编码写入为 Admin/Designer；只读用户只能查询。
- 所有接口还受 `client` 声明约束：管理端配置写接口要求 `AdminClient`，设计端数据接口要求 `ClientAccess`，跨端返回 `403`。

## 6.1 工作区与工厂接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/auth/workspaces` | 当前用户可访问的工厂与项目（非管理员只返回已加入的项目） |
| POST | `/api/auth/workspace` | 选择工厂 + 项目，校验成员身份后签发带 `plantId` / `projectId` 的令牌 |
| POST | `/api/auth/management` | 管理员进入管理模块，签发不带工厂/项目范围的令牌 |
| GET | `/api/plants` | 当前用户可访问的工厂列表（管理员为全部） |
| POST/PUT | `/api/plants`、`/api/plants/{id}` | 新建/编辑工厂（Admin/Designer） |
| DELETE | `/api/plants/{id}` | 删除空工厂（Admin；有项目/PBS/设备时拒绝） |
| GET/POST | `/api/projects/{id}/members` | 项目成员列表 / 添加成员（Admin；角色为 Manager / Designer / Viewer） |
| PUT/DELETE | `/api/projects/{id}/members/{userId}` | 修改成员角色 / 移除成员（Admin；项目负责人不可修改或移除） |
| POST | `/api/projects/{id}/members/bulk` | 批量授权：一组用户统一授予管理/设计/只读 |
| GET/PUT | `/api/roles/{id}/permissions` | 读取 / 保存用户类的全局 CRUD 权限（Admin） |

### 6.1.1 权限模型

- **用户类（固定三类）**：Admin / Designer / Viewer，按「模块 × 增删改查」配置全局权限，权限码为 `module.action`。
- **用户**：可以属于多个用户类，权限取并集。
- **项目成员**：只控制项目准入；成员角色为 Manager（管理）/ Designer（设计）/ Viewer（只读）。管理角色用于项目成员维护，具体数据增删改查仍由全局用户类权限决定。
- 服务端用 `[RequirePermission("module.action")]` 校验令牌中的 `permission` 声明；权限配置变更后需要重新登录。

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
- 管理客户端：`dotnet run --project src/WinDesign.AdminClient`
- 设计客户端：`dotnet run --project src/WinDesign.DesignClient`
- 数据库：SQLite（`appsettings.Development.json`）

### 生产
- 数据库：PostgreSQL 16（`docker-compose.yml` 或既有实例）。
- API：`dotnet publish -c Release` 后部署为 Windows 服务 / IIS。
- 客户端：分别发布两个 exe：
  ```bash
  dotnet publish src/WinDesign.AdminClient -c Release -r win-x64 --self-contained false
  dotnet publish src/WinDesign.DesignClient -c Release -r win-x64 --self-contained false
  ```
  管理端只发给管理员，设计端发给设计人员；共享库随各自发布目录一起分发。
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

已完成：工厂（PBS 根节点）与多项目管理、登录工作区选择（工厂 → 项目）、按项目范围的数据授权、PBS 管理（区域 → 装置 → 回路 → 设备）、索引表录入（设备可在 PBS 中创建，索引表浏览/修改）、项目配置、**管理/设计双客户端拆分与客户端类型隔离**。

第二阶段：
1. 设备规格模板/版本 + 缺项校验。
2. 材料汇总 + Excel/Word/PDF 输出。
3. 索引增强：位号自动编制、Excel 批量导入导出、PBS 子树过滤。
4. EF Migrations、刷新令牌、审计日志界面。
5. 旧 WINPCCAD 数据迁移（导出与检查工具见原工作区 `docs/migration/`）。
