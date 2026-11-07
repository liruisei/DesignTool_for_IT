using WinDesign.Client.Services;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;

namespace WinDesign.Client.Tests;

public sealed class FakeApiClient : IApiClient
{
    public string BaseUrl { get; private set; } = "http://test";
    public string? Token { get; private set; }

    public LoginResponse? LoginResult { get; set; }
    public ApiException? LoginError { get; set; }
    public ProjectDto Project { get; set; } = new()
    {
        Id = Guid.NewGuid(),
        Code = "DEMO-001",
        Name = "演示项目",
        ClientName = "演示客户",
        Location = "演示地点",
        Phase = "详细设计",
        Status = "Active",
        CreatedAt = DateTime.UtcNow
    };

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

    public Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

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

    public Task<IReadOnlyList<PbsNodeDto>> GetPbsNodesAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PbsNodeDto>>(
            PbsNodes.Where(x => x.ProjectId == projectId).ToList());

    public Task<PbsNodeDto> CreatePbsNodeAsync(Guid projectId, CreatePbsNodeRequest request, CancellationToken ct = default)
    {
        var dto = new PbsNodeDto(
            Guid.NewGuid(), projectId, request.ParentId, request.Code, request.Name,
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
