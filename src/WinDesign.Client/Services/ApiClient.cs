using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;

namespace WinDesign.Client.Services;

public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message)
    {
    }
}

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string BaseUrl { get; private set; } = "http://localhost:5080";
    public string? Token { get; private set; }

    public void Configure(string baseUrl)
    {
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:5080" : baseUrl.Trim().TrimEnd('/');
    }

    public void SetToken(string? token)
    {
        Token = token;
    }

    private Uri Build(string path) => new(new Uri(BaseUrl + "/"), path.TrimStart('/'));

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, Build(path));
        if (!string.IsNullOrWhiteSpace(Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }
        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new ApiException("服务端返回了空响应");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var text = await response.Content.ReadAsStringAsync(ct);
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                var error = JsonSerializer.Deserialize<ApiError>(text, JsonOptions);
                if (!string.IsNullOrWhiteSpace(error?.Message))
                {
                    throw new ApiException(error!.Message);
                }
            }
            catch (JsonException)
            {
                // fall through to raw text
            }
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiException("登录已失效，请重新登录");
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new ApiException("没有权限执行该操作");
        }
        throw new ApiException(string.IsNullOrWhiteSpace(text)
            ? $"请求失败（{(int)response.StatusCode}）"
            : text);
    }

    private async Task SendNoContentAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    // ---------- auth ----------
    public async Task<LoginResponse> LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        var request = Request(HttpMethod.Post, "api/auth/login");
        request.Content = JsonContent.Create(new LoginRequest(userName, password), options: JsonOptions);
        var result = await SendAsync<LoginResponse>(request, ct);
        Token = result.AccessToken;
        return result;
    }

    public Task<CurrentUserDto> GetMeAsync(CancellationToken ct = default) =>
        SendAsync<CurrentUserDto>(Request(HttpMethod.Get, "api/auth/me"), ct);

    // ---------- dashboard ----------
    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default) =>
        SendAsync<DashboardDto>(Request(HttpMethod.Get, "api/dashboard"), ct);

    // ---------- users ----------
    public Task<PagedResult<UserDto>> GetUsersAsync(string? search, int page = 1, int pageSize = 200, CancellationToken ct = default)
    {
        var query = $"api/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += "&search=" + Uri.EscapeDataString(search);
        }
        return SendAsync<PagedResult<UserDto>>(Request(HttpMethod.Get, query), ct);
    }

    public Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Post, "api/users");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<UserDto>(message, ct);
    }

    public Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Put, $"api/users/{id}");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<UserDto>(message, ct);
    }

    public Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Post, $"api/users/{id}/reset-password");
        message.Content = JsonContent.Create(new ResetPasswordRequest(newPassword), options: JsonOptions);
        return SendNoContentAsync(message, ct);
    }

    public Task<UserDto> ToggleUserActiveAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<UserDto>(Request(HttpMethod.Post, $"api/users/{id}/toggle-active"), ct);

    public Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<RoleDto>>(Request(HttpMethod.Get, "api/roles"), ct);

    // ---------- projects ----------
    public Task<PagedResult<ProjectDto>> GetProjectsAsync(string? search = null, string? status = null, CancellationToken ct = default)
    {
        var query = "api/projects?pageSize=200";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += "&search=" + Uri.EscapeDataString(search);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query += "&status=" + Uri.EscapeDataString(status);
        }
        return SendAsync<PagedResult<ProjectDto>>(Request(HttpMethod.Get, query), ct);
    }

    public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Post, "api/projects");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<ProjectDto>(message, ct);
    }

    public Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Put, $"api/projects/{id}");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<ProjectDto>(message, ct);
    }

    public Task ArchiveProjectAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(Request(HttpMethod.Delete, $"api/projects/{id}"), ct);

    // ---------- dictionaries ----------
    public Task<IReadOnlyList<DictionaryTypeDto>> GetDictionaryTypesAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<DictionaryTypeDto>>(Request(HttpMethod.Get, "api/dictionaries/types"), ct);

    public Task<DictionaryTypeDto> CreateDictionaryTypeAsync(CreateDictionaryTypeRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Post, "api/dictionaries/types");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<DictionaryTypeDto>(message, ct);
    }

    public Task<DictionaryTypeDto> UpdateDictionaryTypeAsync(Guid id, UpdateDictionaryTypeRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Put, $"api/dictionaries/types/{id}");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<DictionaryTypeDto>(message, ct);
    }

    public Task DeleteDictionaryTypeAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(Request(HttpMethod.Delete, $"api/dictionaries/types/{id}"), ct);

    public Task<IReadOnlyList<DictionaryItemDto>> GetDictionaryItemsAsync(string typeCode, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<DictionaryItemDto>>(
            Request(HttpMethod.Get, $"api/dictionaries/types/{Uri.EscapeDataString(typeCode)}/items"), ct);

    public Task<DictionaryItemDto> CreateDictionaryItemAsync(string typeCode, CreateDictionaryItemRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Post, $"api/dictionaries/types/{Uri.EscapeDataString(typeCode)}/items");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<DictionaryItemDto>(message, ct);
    }

    public Task<DictionaryItemDto> UpdateDictionaryItemAsync(Guid id, UpdateDictionaryItemRequest request, CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Put, $"api/dictionaries/items/{id}");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<DictionaryItemDto>(message, ct);
    }

    public Task DeleteDictionaryItemAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(Request(HttpMethod.Delete, $"api/dictionaries/items/{id}"), ct);

    // ---------- PBS ----------
    public Task<IReadOnlyList<PbsNodeDto>> GetPbsNodesAsync(Guid projectId, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<PbsNodeDto>>(
            Request(HttpMethod.Get, $"api/projects/{projectId}/pbs"), ct);

    public Task<PbsNodeDto> CreatePbsNodeAsync(
        Guid projectId,
        CreatePbsNodeRequest request,
        CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Post, $"api/projects/{projectId}/pbs");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<PbsNodeDto>(message, ct);
    }

    public Task<PbsNodeDto> UpdatePbsNodeAsync(
        Guid id,
        UpdatePbsNodeRequest request,
        CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Put, $"api/pbs/{id}");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<PbsNodeDto>(message, ct);
    }

    public Task DeletePbsNodeAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(Request(HttpMethod.Delete, $"api/pbs/{id}"), ct);

    // ---------- equipment ----------
    public Task<PagedResult<EquipmentDto>> GetEquipmentsAsync(
        Guid projectId,
        string? search = null,
        string? discipline = null,
        string? status = null,
        Guid? pbsNodeId = null,
        int pageSize = 500,
        CancellationToken ct = default)
    {
        var query = $"api/projects/{projectId}/equipments?pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += "&search=" + Uri.EscapeDataString(search);
        }
        if (!string.IsNullOrWhiteSpace(discipline))
        {
            query += "&discipline=" + Uri.EscapeDataString(discipline);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query += "&status=" + Uri.EscapeDataString(status);
        }
        if (pbsNodeId is Guid nodeId)
        {
            query += "&pbsNodeId=" + nodeId;
        }
        return SendAsync<PagedResult<EquipmentDto>>(Request(HttpMethod.Get, query), ct);
    }

    public Task<EquipmentDto> CreateEquipmentAsync(
        Guid projectId,
        EquipmentUpsertRequest request,
        CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Post, $"api/projects/{projectId}/equipments");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<EquipmentDto>(message, ct);
    }

    public Task<EquipmentDto> UpdateEquipmentAsync(
        Guid id,
        EquipmentUpsertRequest request,
        CancellationToken ct = default)
    {
        var message = Request(HttpMethod.Put, $"api/equipments/{id}");
        message.Content = JsonContent.Create(request, options: JsonOptions);
        return SendAsync<EquipmentDto>(message, ct);
    }

    public Task DeleteEquipmentAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(Request(HttpMethod.Delete, $"api/equipments/{id}"), ct);
}
