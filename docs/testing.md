# 工厂二维设计平台 测试指南

## 1. 测试分层

| 项目 | 类型 | 依赖 | 覆盖内容 |
|------|------|------|----------|
| `tests/WinDesign.UnitTests` | 纯单元测试 | 无外部依赖 | 密码哈希、JWT 签发与 client / 工作区声明 |
| `tests/WinDesign.Api.Tests` | API 集成测试 | `WebApplicationFactory` + 临时 SQLite | 登录/权限、客户端类型隔离、PBS 约束、索引表 CRUD 与过滤 |
| `tests/WinDesign.Client.Tests` | ViewModel 单元测试 | 假 API 客户端，无 UI/服务端 | 管理端导航/PBS/配置表单、设计端导航/工作区切换/索引表、共用登录与设备弹窗 |

> 客户端测试项目同时引用 `WinDesign.Client.Shared`、`WinDesign.AdminClient` 与 `WinDesign.DesignClient`，但测试只实例化 ViewModel，不启动任何 WPF 窗口。

设计原则：

- **API 集成测试不依赖 PostgreSQL**：使用临时 SQLite 文件，启动时自动建表与写入种子数据，测试结束删除。
- **客户端测试不启动 WPF 窗口**：通过 `IApiClient` 注入 `FakeApiClient`，直接验证 ViewModel 行为。
- **约束在服务端测试**：PBS 单根、编码唯一、循环引用、设备位号唯一等规则由 API 测试覆盖。

## 2. 环境要求

- Windows（客户端测试引用 WPF 项目）
- .NET SDK 10.0+
- 无需 PostgreSQL、Docker 或已启动的 API 进程

验证环境：

```bash
dotnet --version
```

## 3. 运行测试

### 运行全部

```bash
cd WinDesign
dotnet test WinDesign.slnx
```

### 只运行某一层

```bash
# 纯单元测试
dotnet test tests/WinDesign.UnitTests/WinDesign.UnitTests.csproj

# API 集成测试
dotnet test tests/WinDesign.Api.Tests/WinDesign.Api.Tests.csproj

# 客户端 ViewModel 测试
dotnet test tests/WinDesign.Client.Tests/WinDesign.Client.Tests.csproj
```

### 按名称过滤

```bash
dotnet test WinDesign.slnx --filter "FullyQualifiedName~PasswordHasher"
dotnet test WinDesign.slnx --filter "FullyQualifiedName~PbsTests"
dotnet test WinDesign.slnx --filter "DisplayName~登录"
```

### 详细输出

```bash
dotnet test WinDesign.slnx -v normal
```

## 4. 覆盖率

```bash
dotnet test WinDesign.slnx --collect:"XPlat Code Coverage"
```

结果位于各测试项目的 `TestResults/<guid>/coverage.cobertura.xml`。

生成 HTML 报告（可选）：

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

打开 `coverage-report/index.html`。

## 5. 测试数据与环境

### API 集成测试

`ApiFactory` 继承 `WebApplicationFactory<Program>`：

- 为每个测试类创建独立的临时 SQLite 文件。
- 通过内存配置覆盖 `Database:Provider=Sqlite` 与连接串。
- 启动时执行 `EnsureCreated` + `DatabaseSeeder`，自动获得演示账号与项目。
- 测试结束删除临时数据库。
- 已禁用测试并行执行，避免共享测试库互相干扰。

默认账号：

| 用户名 | 密码 | 角色 |
|--------|------|------|
| `admin` | `admin123` | Admin |
| `designer` | `designer123` | Designer |
| `viewer` | `viewer123` | Viewer |

### 客户端测试

`FakeApiClient` 实现 `IApiClient`，维护内存中的 PBS 节点、设备与字典数据。测试不启动 WPF 窗口，也不需要服务端。

## 6. 持续集成

`.github/workflows/dotnet-tests.yml` 在 `windows-latest` 上：

1. 安装 .NET 10 SDK
2. `dotnet restore`
3. `dotnet build -c Release`
4. `dotnet test --collect:"XPlat Code Coverage"`
5. 上传 `TestResults/**` 与覆盖率文件

推送 `main` 或 `feature/**` 分支、以及向 `main` 提交 PR 时自动运行。

## 7. 新增测试的建议

- 新接口：在 `WinDesign.Api.Tests` 增加一个 `XxxTests : ApiTestBase`，覆盖成功、角色权限、客户端类型权限、校验失败四类路径；管理端接口用 `LoginManagementAsync()`，设计端接口用 `LoginAsync()`。
- 新 ViewModel：在 `WinDesign.Client.Tests` 扩展 `FakeApiClient`，验证状态变化与命令结果；管理端 ViewModel 用 `CreateManagementSession()`，设计端用 `CreateSession()`。
- 新客户端行为：在 `AdminMainViewModelTests` 或 `DesignMainViewModelTests` 中断言导航树只包含本客户端模块，防止两个客户端再次串页。
- 新领域规则：优先放到 `WinDesign.UnitTests`，保持无外部依赖。
- 命名：`Method_Scenario_ExpectedResult`，例如 `Pbs_Cycle_ReturnsBadRequest`。

## 8. 常见问题

| 问题 | 处理 |
|------|------|
| 提示找不到 .NET 10 SDK | 安装 .NET 10 SDK；或把 `TargetFramework` 改回 `net8.0` / `net8.0-windows` |
| 客户端测试在非 Windows 上失败 | WPF 仅支持 Windows，请在 Windows 或 `windows-latest` 上运行 |
| API 测试连到 PostgreSQL | 确认 `ApiFactory` 的内存配置生效；不要删除 `Database:Provider=Sqlite` 覆盖 |
| 测试库残留 | 临时库位于 `%TEMP%\windesign_test_*.db`，正常退出会自动删除；可手动清理 |
| 端口冲突 | API 测试使用进程内 TestServer，不占用 5080 端口 |
