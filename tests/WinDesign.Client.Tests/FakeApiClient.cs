using WinDesign.Client.Services;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Client.Tests;

public sealed class FakeApiClient : IApiClient
{
    public string BaseUrl { get; private set; } = "http://test";
    public string? Token { get; private set; }

    public LoginResponse? LoginResult { get; set; }
    public ApiException? LoginError { get; set; }

    public List<PlantDto> PlantDtos { get; } = new();

    public WorkspacePlantDto Plant { get; set; } = new(
        Guid.NewGuid(),
        "PLANT-A",
        "演示工厂",
        "演示客户",
        "演示地点",
        Array.Empty<WorkspaceProjectDto>());

    public ProjectDto Project { get; set; }

    public FakeApiClient()
    {
        Project = new ProjectDto
        {
            Id = Guid.NewGuid(),
            PlantId = Plant.Id,
            PlantName = Plant.Name,
            Code = "DEMO-001",
            Name = "演示项目",
            ClientName = "演示客户",
            Location = "演示地点",
            Phase = "详细设计",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>构造一个已登录并已选择工作区的测试会话。</summary>
    public SessionService CreateSession()
    {
        var session = new SessionService(this);
        var user = new CurrentUserDto(Guid.NewGuid(), "admin", "测试用户",
            new[] { "Admin" }, new[] { "projects.read", "pbs.write", "equipment.write" });
        session.SignIn(new LoginResponse("fake-token", DateTime.UtcNow.AddHours(1), user));

        var projectRef = new WorkspaceProjectDto(
            Project.Id, Project.Code, Project.Name, Project.Phase, Project.Status);
        session.SetWorkspace(new WorkspaceResponse(
            "fake-workspace-token",
            DateTime.UtcNow.AddHours(1),
            user,
            Plant with { Projects = new[] { projectRef } },
            projectRef));
        return session;
    }

    /// <summary>构造一个管理员、未选择工厂/项目的管理模块会话。</summary>
    public SessionService CreateManagementSession()
    {
        var session = new SessionService(this);
        var user = new CurrentUserDto(Guid.NewGuid(), "admin", "测试管理员",
            new[] { "Admin" }, new[] { "projects.read", "pbs.write", "users.read" });
        session.SignIn(new LoginResponse("fake-token", DateTime.UtcNow.AddHours(1), user));
        session.SetManagementMode(new ManagementSessionResponse(
            "fake-management-token", DateTime.UtcNow.AddHours(1), user));
        return session;
    }

    public List<PbsNodeDto> PbsNodes { get; } = new();
    public List<EquipmentDto> Equipments { get; } = new();
    public List<DictionaryItemDto> EquipmentTypes { get; } = new();
    public List<DictionaryTypeDto> DictionaryTypes { get; } = new();

    public void Configure(string baseUrl) => BaseUrl = baseUrl;

    public void SetToken(string? token) => Token = token;

    public Task<LoginResponse> LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        if (LoginError is not null)
        {
            throw LoginError;
        }

        return Task.FromResult(LoginResult ?? new LoginResponse(
            "fake-token",
            DateTime.UtcNow.AddHours(1),
            new CurrentUserDto(Guid.NewGuid(), userName, "测试用户",
                new[] { "Admin" }, new[] { "users.read", "projects.read" })));
    }

    public Task<CurrentUserDto> GetMeAsync(CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkspacePlantDto>> GetWorkspacesAsync(CancellationToken ct = default)
    {
        var projectRef = new WorkspaceProjectDto(
            Project.Id, Project.Code, Project.Name, Project.Phase, Project.Status);
        IReadOnlyList<WorkspacePlantDto> result = new[]
        {
            Plant with { Projects = new[] { projectRef } }
        };
        return Task.FromResult(result);
    }

    public Task<ManagementSessionResponse> EnterManagementModeAsync(CancellationToken ct = default)
    {
        var user = new CurrentUserDto(Guid.NewGuid(), "admin", "测试管理员",
            new[] { "Admin" }, new[] { "projects.read" });
        return Task.FromResult(new ManagementSessionResponse(
            "fake-management-token", DateTime.UtcNow.AddHours(1), user));
    }

    public Task<IReadOnlyList<PlantDto>> GetPlantsAsync(CancellationToken ct = default)
    {
        if (PlantDtos.Count > 0)
        {
            return Task.FromResult<IReadOnlyList<PlantDto>>(PlantDtos.ToList());
        }

        IReadOnlyList<PlantDto> result = new[]
        {
            new PlantDto(Plant.Id, Plant.Code, Plant.Name, Plant.ClientName, Plant.Location,
                null, true, 1, 0, DateTime.UtcNow)
        };
        return Task.FromResult(result);
    }

    public Task<PlantDto> CreatePlantAsync(CreatePlantRequest request, CancellationToken ct = default)
    {
        var dto = new PlantDto(Guid.NewGuid(), request.Code, request.Name, request.ClientName,
            request.Location, request.Description, true, 0, 0, DateTime.UtcNow);
        PlantDtos.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<PlantDto> UpdatePlantAsync(Guid id, UpdatePlantRequest request, CancellationToken ct = default)
    {
        var index = PlantDtos.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("工厂不存在");
        }

        var dto = PlantDtos[index] with
        {
            Name = request.Name,
            ClientName = request.ClientName,
            Location = request.Location,
            Description = request.Description,
            IsActive = request.IsActive
        };
        PlantDtos[index] = dto;
        return Task.FromResult(dto);
    }

    public Task DeletePlantAsync(Guid id, CancellationToken ct = default)
    {
        PlantDtos.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<WorkspaceResponse> SelectWorkspaceAsync(Guid plantId, Guid projectId, CancellationToken ct = default)
    {
        var projectRef = new WorkspaceProjectDto(
            Project.Id, Project.Code, Project.Name, Project.Phase, Project.Status);
        var plant = Plant with { Projects = new[] { projectRef } };
        return Task.FromResult(new WorkspaceResponse(
            "fake-workspace-token",
            DateTime.UtcNow.AddHours(1),
            LoginResult?.User ?? new CurrentUserDto(Guid.NewGuid(), "admin", "测试用户",
                new[] { "Admin" }, Array.Empty<string>()),
            plant,
            projectRef));
    }

    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default) =>
        Task.FromResult(new DashboardDto(0, 1, 0, 0, new[] { Project }));

    public Task<PagedResult<UserDto>> GetUsersAsync(string? search, int page = 1, int pageSize = 200, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<UserDto>(Array.Empty<UserDto>(), 0, page, pageSize));

    public Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<UserDto> ToggleUserActiveAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RoleDto>>(Array.Empty<RoleDto>());

    public Task<PagedResult<ProjectDto>> GetProjectsAsync(string? search = null, string? status = null, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<ProjectDto>(new[] { Project }, 1, 1, 200));

    public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        Project = Project with
        {
            Name = request.Name,
            ClientName = request.ClientName,
            Location = request.Location,
            Phase = request.Phase,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status ?? Project.Status,
            Description = request.Description
        };
        return Task.FromResult(Project);
    }

    public Task ArchiveProjectAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DictionaryTypeDto>> GetDictionaryTypesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DictionaryTypeDto>>(DictionaryTypes);

    public Task<DictionaryTypeDto> CreateDictionaryTypeAsync(CreateDictionaryTypeRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<DictionaryTypeDto> UpdateDictionaryTypeAsync(Guid id, UpdateDictionaryTypeRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task DeleteDictionaryTypeAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DictionaryItemDto>> GetDictionaryItemsAsync(string typeCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DictionaryItemDto>>(
            EquipmentTypes.Where(x => x.TypeCode == typeCode).ToList());

    public Task<DictionaryItemDto> CreateDictionaryItemAsync(string typeCode, CreateDictionaryItemRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<DictionaryItemDto> UpdateDictionaryItemAsync(Guid id, UpdateDictionaryItemRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task DeleteDictionaryItemAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PbsNodeDto>> GetPbsNodesAsync(Guid plantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PbsNodeDto>>(
            PbsNodes.Where(x => x.PlantId == plantId).ToList());

    public Task<PbsNodeDto> CreatePbsNodeAsync(Guid plantId, CreatePbsNodeRequest request, CancellationToken ct = default)
    {
        var dto = new PbsNodeDto(
            Guid.NewGuid(), plantId, request.ParentId, request.Code, request.Name,
            request.NodeType, request.SortOrder, request.Description, 0, 0);
        PbsNodes.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<PbsNodeDto> UpdatePbsNodeAsync(Guid id, UpdatePbsNodeRequest request, CancellationToken ct = default)
    {
        var index = PbsNodes.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("PBS 节点不存在");
        }

        var old = PbsNodes[index];
        var dto = old with
        {
            ParentId = request.ParentId,
            Code = request.Code,
            Name = request.Name,
            NodeType = request.NodeType,
            SortOrder = request.SortOrder,
            Description = request.Description
        };
        PbsNodes[index] = dto;
        return Task.FromResult(dto);
    }

    public Task DeletePbsNodeAsync(Guid id, CancellationToken ct = default)
    {
        PbsNodes.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<PagedResult<EquipmentDto>> GetEquipmentsAsync(
        Guid projectId,
        string? search = null,
        string? discipline = null,
        string? status = null,
        Guid? pbsNodeId = null,
        int pageSize = 500,
        CancellationToken ct = default)
    {
        IEnumerable<EquipmentDto> query = Equipments.Where(x => x.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.TagNo.Contains(search) || x.Name.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(discipline))
        {
            query = query.Where(x => x.Discipline == discipline);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }
        if (pbsNodeId is Guid nodeId)
        {
            query = query.Where(x => x.PbsNodeId == nodeId);
        }

        var items = query.ToList();
        return Task.FromResult(new PagedResult<EquipmentDto>(items, items.Count, 1, 200));
    }

    public Task<EquipmentDto> CreateEquipmentAsync(Guid projectId, EquipmentUpsertRequest request, CancellationToken ct = default)
    {
        var dto = new EquipmentDto
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PbsNodeId = request.PbsNodeId,
            PbsNodeName = PbsNodes.FirstOrDefault(x => x.Id == request.PbsNodeId)?.Name,
            TagNo = request.TagNo,
            LoopNo = request.LoopNo,
            Name = request.Name,
            TypeCode = request.TypeCode,
            Discipline = request.Discipline,
            Status = request.Status ?? "Planned",
            Location = request.Location,
            PipeEquipmentNo = request.PipeEquipmentNo,
            Quantity = request.Quantity,
            Purpose = request.Purpose,
            PidDrawingNo = request.PidDrawingNo,
            SpecNo = request.SpecNo,
            SpecPageNo = request.SpecPageNo,
            LoopDrawingNo = request.LoopDrawingNo,
            InstallDrawingNo = request.InstallDrawingNo,
            ElectricalDrawingNo = request.ElectricalDrawingNo,
            HeatSupplyDrawingNo = request.HeatSupplyDrawingNo,
            AirSupplyDrawingNo = request.AirSupplyDrawingNo,
            LayingDrawingNo = request.LayingDrawingNo,
            JunctionBoxDrawingNo = request.JunctionBoxDrawingNo,
            Keyword = request.Keyword,
            IoSetting = request.IoSetting,
            IndexNo = request.IndexNo,
            SortOrder = request.SortOrder,
            Description = request.Description,
            ProcessCondition = request.ProcessCondition,
            Specification = request.Specification,
            CreatedAt = DateTime.UtcNow
        };
        Equipments.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<EquipmentDto> UpdateEquipmentAsync(Guid id, EquipmentUpsertRequest request, CancellationToken ct = default)
    {
        var index = Equipments.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("设备不存在");
        }

        var old = Equipments[index];
        var dto = old with
        {
            PbsNodeId = request.PbsNodeId,
            PbsNodeName = PbsNodes.FirstOrDefault(x => x.Id == request.PbsNodeId)?.Name,
            TagNo = request.TagNo,
            LoopNo = request.LoopNo,
            Name = request.Name,
            TypeCode = request.TypeCode,
            Discipline = request.Discipline,
            Status = request.Status ?? old.Status,
            Location = request.Location,
            PipeEquipmentNo = request.PipeEquipmentNo,
            Quantity = request.Quantity,
            Purpose = request.Purpose,
            PidDrawingNo = request.PidDrawingNo,
            SpecNo = request.SpecNo,
            SpecPageNo = request.SpecPageNo,
            LoopDrawingNo = request.LoopDrawingNo,
            InstallDrawingNo = request.InstallDrawingNo,
            ElectricalDrawingNo = request.ElectricalDrawingNo,
            HeatSupplyDrawingNo = request.HeatSupplyDrawingNo,
            AirSupplyDrawingNo = request.AirSupplyDrawingNo,
            LayingDrawingNo = request.LayingDrawingNo,
            JunctionBoxDrawingNo = request.JunctionBoxDrawingNo,
            Keyword = request.Keyword,
            IoSetting = request.IoSetting,
            IndexNo = request.IndexNo,
            SortOrder = request.SortOrder,
            Description = request.Description,
            ProcessCondition = request.ProcessCondition,
            Specification = request.Specification
        };
        Equipments[index] = dto;
        return Task.FromResult(dto);
    }

    public Task DeleteEquipmentAsync(Guid id, CancellationToken ct = default)
    {
        Equipments.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }
}
