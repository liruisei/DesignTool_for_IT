using WinDesign.Client.Services;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Materials;
using WinDesign.Contracts.Numbering;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Telecom;
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
    public List<WorkspaceUnitDto> WorkspaceUnits { get; } = new();

    public WorkspacePlantDto Plant { get; set; } = new(
        Guid.NewGuid(),
        "PLANT-A",
        "演示工厂",
        "演示客户",
        "演示地点",
        Array.Empty<WorkspaceProjectDto>());

    public ProjectDto Project { get; set; }

    /// <summary>演示项目的 PBS 根节点（工厂顶层节点）。</summary>
    public PbsNodeDto PbsRoot { get; }

    public FakeApiClient()
    {
        PbsRoot = new PbsNodeDto(
            Guid.NewGuid(), Plant.Id, null, "AREA-A", "A 区", "Area", 1, "演示根节点", 0, 0);

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
            PbsRootNodeId = PbsRoot.Id,
            PbsRootNodeName = PbsRoot.Code + " " + PbsRoot.Name,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>把演示根节点加入 PBS 节点集合；PBS 相关测试按需调用。</summary>
    public PbsNodeDto AddDefaultPbsRoot()
    {
        if (!PbsNodes.Contains(PbsRoot))
        {
            PbsNodes.Add(PbsRoot);
        }

        return PbsRoot;
    }

    /// <summary>构造一个已登录并已选择工作区的测试会话。</summary>
    public SessionService CreateSession(string[]? roles = null)
    {
        var session = new SessionService(this);
        var actualRoles = roles ?? new[] { "Admin" };
        var user = new CurrentUserDto(Guid.NewGuid(), "admin", "测试用户",
            actualRoles, new[] { "projects.read", "pbs.write", "equipment.write" });
        session.SignIn(new LoginResponse("fake-token", DateTime.UtcNow.AddHours(1), user));

        var projectRef = new WorkspaceProjectDto(
            Project.Id, Project.Code, Project.Name, Project.Phase, Project.Status)
        {
            PbsRootNodeId = Project.PbsRootNodeId
        };
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
    public List<EquipmentSpecDto> EquipmentSpecs { get; } = new();
    public List<MaterialItemDto> Materials { get; } = new();
    public List<DictionaryItemDto> EquipmentTypes { get; } = new();
    public List<DictionaryTypeDto> DictionaryTypes { get; } = new();
    public List<ProjectMemberDto> ProjectMembers { get; } = new();
    public List<SwitchModelDto> SwitchModels { get; } = new();
    public List<TelecomRoomDto> TelecomRooms { get; } = new();
    public Dictionary<Guid, NumberingRuleDto> NumberingRules { get; } = new();
    public List<UserDto> Users { get; } = new();
    public List<RoleDto> Roles { get; } = new();
    public Dictionary<Guid, List<string>> RolePermissionCodes { get; } = new();

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
            Project.Id, Project.Code, Project.Name, Project.Phase, Project.Status)
        {
            PbsRootNodeId = Project.PbsRootNodeId,
            Units = WorkspaceUnits.ToList()
        };
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
            Project.Id, Project.Code, Project.Name, Project.Phase, Project.Status)
        {
            PbsRootNodeId = Project.PbsRootNodeId
        };
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

    public Task<PagedResult<UserDto>> GetUsersAsync(string? search, int page = 1, int pageSize = 200, CancellationToken ct = default)
    {
        var items = Users
            .Where(x => string.IsNullOrWhiteSpace(search) ||
                        x.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(new PagedResult<UserDto>(items, items.Count, page, pageSize));
    }

    public Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        Users.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<UserDto> ToggleUserActiveAsync(Guid id, CancellationToken ct = default)
    {
        var index = Users.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("用户不存在");
        }

        var updated = Users[index] with { IsActive = !Users[index].IsActive };
        Users[index] = updated;
        return Task.FromResult(updated);
    }

    public Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RoleDto>>(Roles.ToList());

    public Task<RolePermissionDto> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default)
    {
        var code = Roles.FirstOrDefault(x => x.Id == roleId)?.Code ?? "Unknown";
        var codes = RolePermissionCodes.TryGetValue(roleId, out var existing)
            ? existing.ToList()
            : new List<string>();
        return Task.FromResult(new RolePermissionDto(roleId, code, codes));
    }

    public Task<RolePermissionDto> UpdateRolePermissionsAsync(
        Guid roleId,
        IReadOnlyList<string> permissionCodes,
        CancellationToken ct = default)
    {
        RolePermissionCodes[roleId] = permissionCodes.ToList();
        return GetRolePermissionsAsync(roleId, ct);
    }

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
            Description = request.Description,
            PbsRootNodeId = request.PbsRootNodeId,
            PbsRootNodeName = PbsNodes.FirstOrDefault(x => x.Id == request.PbsRootNodeId) is { } root
                ? root.Code + " " + root.Name
                : null
        };
        return Task.FromResult(Project);
    }

    public Task ArchiveProjectAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProjectMemberDto>> GetProjectMembersAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProjectMemberDto>>(ProjectMembers.ToList());

    public Task<ProjectMemberDto> AddProjectMemberAsync(
        Guid projectId,
        UpsertProjectMemberRequest request,
        CancellationToken ct = default)
    {
        var user = Users.Single(x => x.Id == request.UserId);
        var member = new ProjectMemberDto(
            user.Id, user.UserName, user.DisplayName, request.RoleInProject, user.IsActive);
        ProjectMembers.Add(member);
        return Task.FromResult(member);
    }

    public Task<ProjectMemberDto> UpdateProjectMemberAsync(
        Guid projectId,
        Guid userId,
        UpsertProjectMemberRequest request,
        CancellationToken ct = default)
    {
        var index = ProjectMembers.FindIndex(x => x.UserId == userId);
        if (index < 0)
        {
            throw new ApiException("该用户不是项目成员");
        }

        var updated = ProjectMembers[index] with { RoleInProject = request.RoleInProject };
        ProjectMembers[index] = updated;
        return Task.FromResult(updated);
    }

    public Task RemoveProjectMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        ProjectMembers.RemoveAll(x => x.UserId == userId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectMemberDto>> BulkGrantProjectMembersAsync(
        Guid projectId,
        BulkGrantProjectMembersRequest request,
        CancellationToken ct = default)
    {
        foreach (var userId in request.UserIds.Distinct())
        {
            var user = Users.Single(x => x.Id == userId);
            var index = ProjectMembers.FindIndex(x => x.UserId == userId);
            var member = new ProjectMemberDto(
                user.Id, user.UserName, user.DisplayName, request.RoleInProject, user.IsActive);
            if (index < 0)
            {
                ProjectMembers.Add(member);
            }
            else
            {
                ProjectMembers[index] = member;
            }
        }

        return Task.FromResult<IReadOnlyList<ProjectMemberDto>>(ProjectMembers.ToList());
    }

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

    /// <summary>设计端在装置下新增回路 / 建筑物的调用记录。</summary>
    public List<(Guid UnitId, CreateUnitChildRequest Request)> UnitChildren { get; } = new();

    public Task<PbsNodeDto> CreateUnitChildAsync(
        Guid unitId,
        CreateUnitChildRequest request,
        CancellationToken ct = default)
    {
        var unit = PbsNodes.Single(x => x.Id == unitId);
        var dto = new PbsNodeDto(
            Guid.NewGuid(), unit.PlantId, unitId, request.Code, request.Name,
            request.NodeType, 99, request.Description, 0, 0);
        PbsNodes.Add(dto);
        UnitChildren.Add((unitId, request));
        return Task.FromResult(dto);
    }

    public Task<PagedResult<EquipmentDto>> GetEquipmentsAsync(
        Guid projectId,
        string? search = null,
        string? discipline = null,
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
        if (pbsNodeId is Guid nodeId)
        {
            query = query.Where(x => x.PbsNodeId == nodeId);
        }

        var items = query.ToList();
        return Task.FromResult(new PagedResult<EquipmentDto>(items, items.Count, 1, 200));
    }

    public Task<EquipmentDto> CreateEquipmentAsync(Guid projectId, EquipmentUpsertRequest request, CancellationToken ct = default)
    {
        // 与服务端保持一致：位号在工厂内不区分大小写唯一。
        if (Equipments.Any(x => x.PlantId == Project.PlantId &&
                                x.TagNo.Equals(request.TagNo, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException("该工厂下设备位号已存在");
        }

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

    public Task<PagedResult<EquipmentSpecDto>> GetEquipmentSpecsAsync(
        Guid projectId,
        string? search = null,
        Guid? equipmentId = null,
        int pageSize = 500,
        CancellationToken ct = default)
    {
        var equipmentIds = Equipments
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Id)
            .ToHashSet();

        IEnumerable<EquipmentSpecDto> query = EquipmentSpecs.Where(x => equipmentIds.Contains(x.EquipmentId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.SpecCode.Contains(search) ||
                                     x.SpecName.Contains(search) ||
                                     x.EquipmentTagNo.Contains(search) ||
                                     x.EquipmentName.Contains(search));
        }
        if (equipmentId is Guid filterEquipmentId)
        {
            query = query.Where(x => x.EquipmentId == filterEquipmentId);
        }

        var items = query.ToList();
        return Task.FromResult(new PagedResult<EquipmentSpecDto>(items, items.Count, 1, 200));
    }

    public Task<EquipmentSpecDto> CreateEquipmentSpecAsync(
        Guid equipmentId,
        EquipmentSpecUpsertRequest request,
        CancellationToken ct = default)
    {
        var equipment = Equipments.FirstOrDefault(x => x.Id == equipmentId);
        var dto = new EquipmentSpecDto
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipmentId,
            EquipmentTagNo = equipment?.TagNo ?? string.Empty,
            EquipmentName = equipment?.Name ?? string.Empty,
            SpecCode = request.SpecCode,
            SpecName = request.SpecName,
            Parameters = request.Parameters ?? Array.Empty<SpecParameterDto>(),
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        EquipmentSpecs.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<EquipmentSpecDto> UpdateEquipmentSpecAsync(
        Guid id,
        EquipmentSpecUpsertRequest request,
        CancellationToken ct = default)
    {
        var index = EquipmentSpecs.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("规格书不存在");
        }

        var dto = EquipmentSpecs[index] with
        {
            SpecCode = request.SpecCode,
            SpecName = request.SpecName,
            Parameters = request.Parameters ?? Array.Empty<SpecParameterDto>(),
            UpdatedAt = DateTime.UtcNow
        };
        EquipmentSpecs[index] = dto;
        return Task.FromResult(dto);
    }

    public Task DeleteEquipmentSpecAsync(Guid id, CancellationToken ct = default)
    {
        EquipmentSpecs.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<PagedResult<MaterialItemDto>> GetMaterialsAsync(
        Guid projectId,
        string? search = null,
        Guid? equipmentId = null,
        int pageSize = 500,
        CancellationToken ct = default)
    {
        IEnumerable<MaterialItemDto> query = Materials.Where(x => x.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Code.Contains(search) ||
                                     x.Name.Contains(search) ||
                                     (x.Spec != null && x.Spec.Contains(search)) ||
                                     (x.Material != null && x.Material.Contains(search)) ||
                                     (x.EquipmentTagNo != null && x.EquipmentTagNo.Contains(search)));
        }
        if (equipmentId is Guid filterEquipmentId)
        {
            query = query.Where(x => x.EquipmentId == filterEquipmentId);
        }

        var items = query.ToList();
        return Task.FromResult(new PagedResult<MaterialItemDto>(items, items.Count, 1, 200));
    }

    public Task<MaterialItemDto> CreateMaterialAsync(
        Guid projectId,
        MaterialItemUpsertRequest request,
        CancellationToken ct = default)
    {
        var equipment = Equipments.FirstOrDefault(x => x.Id == request.EquipmentId);
        var dto = new MaterialItemDto
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            EquipmentId = request.EquipmentId,
            EquipmentTagNo = equipment?.TagNo,
            EquipmentName = equipment?.Name,
            Code = request.Code,
            Name = request.Name,
            Spec = request.Spec,
            Material = request.Material,
            Unit = request.Unit,
            Quantity = request.Quantity,
            Remark = request.Remark,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Materials.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<MaterialItemDto> UpdateMaterialAsync(
        Guid id,
        MaterialItemUpsertRequest request,
        CancellationToken ct = default)
    {
        var index = Materials.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("材料/附件不存在");
        }

        var equipment = Equipments.FirstOrDefault(x => x.Id == request.EquipmentId);
        var dto = Materials[index] with
        {
            EquipmentId = request.EquipmentId,
            EquipmentTagNo = equipment?.TagNo,
            EquipmentName = equipment?.Name,
            Code = request.Code,
            Name = request.Name,
            Spec = request.Spec,
            Material = request.Material,
            Unit = request.Unit,
            Quantity = request.Quantity,
            Remark = request.Remark,
            UpdatedAt = DateTime.UtcNow
        };
        Materials[index] = dto;
        return Task.FromResult(dto);
    }

    public Task DeleteMaterialAsync(Guid id, CancellationToken ct = default)
    {
        Materials.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SwitchModelDto>> GetSwitchModelsAsync(
        bool activeOnly = false,
        CancellationToken ct = default)
    {
        var items = SwitchModels
            .Where(x => !activeOnly || x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToList();
        return Task.FromResult<IReadOnlyList<SwitchModelDto>>(items);
    }

    public Task<SwitchModelDto> CreateSwitchModelAsync(
        SwitchModelUpsertRequest request,
        CancellationToken ct = default)
    {
        var dto = new SwitchModelDto(
            Guid.NewGuid(), request.Code, request.Name, request.SwitchType,
            request.ElectricalPorts, request.UplinkPorts, request.IsActive,
            request.SortOrder, request.Remark);
        SwitchModels.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<SwitchModelDto> UpdateSwitchModelAsync(
        Guid id,
        SwitchModelUpsertRequest request,
        CancellationToken ct = default)
    {
        var index = SwitchModels.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("交换机样本不存在");
        }

        var dto = SwitchModels[index] with
        {
            Code = request.Code,
            Name = request.Name,
            SwitchType = request.SwitchType,
            ElectricalPorts = request.ElectricalPorts,
            UplinkPorts = request.UplinkPorts,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            Remark = request.Remark
        };
        SwitchModels[index] = dto;
        return Task.FromResult(dto);
    }

    public Task<SwitchModelDto> ToggleSwitchModelActiveAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var index = SwitchModels.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("设备样本不存在");
        }

        var dto = SwitchModels[index] with { IsActive = !SwitchModels[index].IsActive };
        SwitchModels[index] = dto;
        return Task.FromResult(dto);
    }

    public Task DeleteSwitchModelAsync(Guid id, CancellationToken ct = default)
    {
        SwitchModels.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelecomRoomDto>> GetTelecomRoomsAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var items = TelecomRooms
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.BuildingCode)
            .ThenBy(x => x.Floor)
            .ThenBy(x => x.Code)
            .ToList();
        return Task.FromResult<IReadOnlyList<TelecomRoomDto>>(items);
    }

    public Task<TelecomRoomDto> CreateTelecomRoomAsync(
        Guid projectId,
        TelecomRoomUpsertRequest request,
        CancellationToken ct = default)
    {
        var dto = BuildTelecomRoomDto(Guid.NewGuid(), projectId, request);
        TelecomRooms.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<TelecomRoomDto> UpdateTelecomRoomAsync(
        Guid id,
        TelecomRoomUpsertRequest request,
        CancellationToken ct = default)
    {
        var index = TelecomRooms.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new ApiException("电信房间记录不存在");
        }

        var dto = BuildTelecomRoomDto(id, TelecomRooms[index].ProjectId, request);
        TelecomRooms[index] = dto;
        return Task.FromResult(dto);
    }

    public Task DeleteTelecomRoomAsync(Guid id, CancellationToken ct = default)
    {
        TelecomRooms.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<NumberingRuleDto> GetNumberingRuleAsync(
        Guid projectId,
        CancellationToken ct = default) =>
        Task.FromResult(NumberingRules.TryGetValue(projectId, out var rule)
            ? rule
            : new NumberingRuleDto(
                Guid.Empty, projectId,
                string.Empty, string.Empty, EquipmentTagFormat.DefaultPrefixLength,
                string.Empty, string.Empty, EquipmentTagFormat.DefaultTypeLength,
                EquipmentTagFormat.DefaultSequenceLength, string.Empty,
                EquipmentTagFormat.DefaultSuffixLength));

    public Task<NumberingRuleDto> SaveNumberingRuleAsync(
        Guid projectId,
        UpsertNumberingRuleRequest request,
        CancellationToken ct = default)
    {
        var existingId = NumberingRules.TryGetValue(projectId, out var existing) && existing.Id != Guid.Empty
            ? existing.Id
            : Guid.NewGuid();
        var rule = new NumberingRuleDto(
            existingId,
            projectId,
            request.PrefixField,
            request.PrefixSeparator,
            request.PrefixLength,
            request.EquipmentTypeField,
            request.TypeSeparator,
            request.TypeLength,
            request.SequenceLength,
            request.Suffix,
            request.SuffixLength);
        NumberingRules[projectId] = rule;
        return Task.FromResult(rule);
    }

    private TelecomRoomDto BuildTelecomRoomDto(
        Guid id,
        Guid projectId,
        TelecomRoomUpsertRequest request)
    {
        var building = PbsNodes.FirstOrDefault(x => x.Id == request.PbsNodeId);
        return new TelecomRoomDto(
            id, projectId, request.PbsNodeId,
            building?.Code ?? string.Empty,
            building?.Name ?? string.Empty,
            request.Floor, request.Code, request.Name,
            request.DataPointCount, request.PhonePointCount, request.ApCount,
            request.Remark, request.SortOrder);
    }
}
