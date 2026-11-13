using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Materials;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Users;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Client.Services;

public interface IApiClient
{
    string BaseUrl { get; }
    string? Token { get; }

    void Configure(string baseUrl);
    void SetToken(string? token);

    Task<LoginResponse> LoginAsync(string userName, string password, CancellationToken ct = default);
    Task<IReadOnlyList<WorkspacePlantDto>> GetWorkspacesAsync(CancellationToken ct = default);
    Task<WorkspaceResponse> SelectWorkspaceAsync(Guid plantId, Guid projectId, CancellationToken ct = default);
    Task<ManagementSessionResponse> EnterManagementModeAsync(CancellationToken ct = default);
    Task<CurrentUserDto> GetMeAsync(CancellationToken ct = default);
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);

    Task<PagedResult<UserDto>> GetUsersAsync(string? search, int page = 1, int pageSize = 200, CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default);
    Task DeleteUserAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> ToggleUserActiveAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default);
    Task<RolePermissionDto> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task<RolePermissionDto> UpdateRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissionCodes, CancellationToken ct = default);

    Task<IReadOnlyList<PlantDto>> GetPlantsAsync(CancellationToken ct = default);
    Task<PlantDto> CreatePlantAsync(CreatePlantRequest request, CancellationToken ct = default);
    Task<PlantDto> UpdatePlantAsync(Guid id, UpdatePlantRequest request, CancellationToken ct = default);
    Task DeletePlantAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<ProjectDto>> GetProjectsAsync(string? search = null, string? status = null, CancellationToken ct = default);
    Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task ArchiveProjectAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectMemberDto>> GetProjectMembersAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectMemberDto> AddProjectMemberAsync(Guid projectId, UpsertProjectMemberRequest request, CancellationToken ct = default);
    Task<ProjectMemberDto> UpdateProjectMemberAsync(Guid projectId, Guid userId, UpsertProjectMemberRequest request, CancellationToken ct = default);
    Task RemoveProjectMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMemberDto>> BulkGrantProjectMembersAsync(Guid projectId, BulkGrantProjectMembersRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<DictionaryTypeDto>> GetDictionaryTypesAsync(CancellationToken ct = default);
    Task<DictionaryTypeDto> CreateDictionaryTypeAsync(CreateDictionaryTypeRequest request, CancellationToken ct = default);
    Task<DictionaryTypeDto> UpdateDictionaryTypeAsync(Guid id, UpdateDictionaryTypeRequest request, CancellationToken ct = default);
    Task DeleteDictionaryTypeAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DictionaryItemDto>> GetDictionaryItemsAsync(string typeCode, CancellationToken ct = default);
    Task<DictionaryItemDto> CreateDictionaryItemAsync(string typeCode, CreateDictionaryItemRequest request, CancellationToken ct = default);
    Task<DictionaryItemDto> UpdateDictionaryItemAsync(Guid id, UpdateDictionaryItemRequest request, CancellationToken ct = default);
    Task DeleteDictionaryItemAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PbsNodeDto>> GetPbsNodesAsync(Guid plantId, CancellationToken ct = default);
    Task<PbsNodeDto> CreatePbsNodeAsync(Guid plantId, CreatePbsNodeRequest request, CancellationToken ct = default);
    Task<PbsNodeDto> UpdatePbsNodeAsync(Guid id, UpdatePbsNodeRequest request, CancellationToken ct = default);
    Task DeletePbsNodeAsync(Guid id, CancellationToken ct = default);

    /// <summary>设计端在装置下新增「回路」或「建筑物」分类节点。</summary>
    Task<PbsNodeDto> CreateUnitChildAsync(Guid unitId, CreateUnitChildRequest request, CancellationToken ct = default);

    Task<PagedResult<EquipmentDto>> GetEquipmentsAsync(Guid projectId, string? search = null, string? discipline = null, Guid? pbsNodeId = null, int pageSize = 500, CancellationToken ct = default);
    Task<EquipmentDto> CreateEquipmentAsync(Guid projectId, EquipmentUpsertRequest request, CancellationToken ct = default);
    Task<EquipmentDto> UpdateEquipmentAsync(Guid id, EquipmentUpsertRequest request, CancellationToken ct = default);
    Task DeleteEquipmentAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<EquipmentSpecDto>> GetEquipmentSpecsAsync(Guid projectId, string? search = null, Guid? equipmentId = null, int pageSize = 500, CancellationToken ct = default);
    Task<EquipmentSpecDto> CreateEquipmentSpecAsync(Guid equipmentId, EquipmentSpecUpsertRequest request, CancellationToken ct = default);
    Task<EquipmentSpecDto> UpdateEquipmentSpecAsync(Guid id, EquipmentSpecUpsertRequest request, CancellationToken ct = default);
    Task DeleteEquipmentSpecAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<MaterialItemDto>> GetMaterialsAsync(Guid projectId, string? search = null, Guid? equipmentId = null, int pageSize = 500, CancellationToken ct = default);
    Task<MaterialItemDto> CreateMaterialAsync(Guid projectId, MaterialItemUpsertRequest request, CancellationToken ct = default);
    Task<MaterialItemDto> UpdateMaterialAsync(Guid id, MaterialItemUpsertRequest request, CancellationToken ct = default);
    Task DeleteMaterialAsync(Guid id, CancellationToken ct = default);
}
