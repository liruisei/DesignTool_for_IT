using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Telecom;
using WinDesign.Domain;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 设计端电信统计：房间记录按当前项目隔离，建筑物必须来自本工厂且在当前项目 PBS 范围内的建筑物节点。
/// 电话点只参与统计，不占用交换机电口。
/// </summary>
[Route("api")]
[Authorize(Policy = AuthorizationPolicies.DesignClient)]
public sealed class TelecomController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public TelecomController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("projects/{projectId:guid}/telecom-rooms")]
    [RequirePermission("telecom.read")]
    public async Task<ActionResult<IReadOnlyList<TelecomRoomDto>>> Get(
        Guid projectId,
        CancellationToken ct)
    {
        if (EnsureWorkspaceProject(projectId) is ActionResult denied)
        {
            return denied;
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessProjectAsync(projectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var rooms = await QueryRoomsAsync(projectId, ct);
        return Ok(rooms.Select(ToDto).ToList());
    }

    [HttpPost("projects/{projectId:guid}/telecom-rooms")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("telecom.create")]
    public async Task<ActionResult<TelecomRoomDto>> Create(
        Guid projectId,
        [FromBody] TelecomRoomUpsertRequest request,
        CancellationToken ct)
    {
        if (EnsureWorkspaceProject(projectId) is ActionResult denied)
        {
            return denied;
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessProjectAsync(projectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var validation = await ValidateRequestAsync(project, request, ct);
        if (validation is not null)
        {
            return validation;
        }

        var room = new TelecomRoom
        {
            ProjectId = projectId,
            PbsNodeId = request.PbsNodeId,
            Floor = request.Floor.Trim(),
            Code = request.Code.Trim(),
            Name = request.Name?.Trim() ?? string.Empty,
            DataPointCount = request.DataPointCount,
            PhonePointCount = request.PhonePointCount,
            ApCount = request.ApCount,
            SortOrder = request.SortOrder,
            Remark = Clean(request.Remark)
        };
        _db.TelecomRooms.Add(room);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "TelecomRoom",
            room.Id.ToString(), room.Code);
        await _db.SaveChangesAsync(ct);

        var created = await _db.TelecomRooms.Include(x => x.PbsNode).AsNoTracking()
            .SingleAsync(x => x.Id == room.Id, ct);
        return Ok(ToDto(created));
    }

    [HttpPut("telecom-rooms/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("telecom.update")]
    public async Task<ActionResult<TelecomRoomDto>> Update(
        Guid id,
        [FromBody] TelecomRoomUpsertRequest request,
        CancellationToken ct)
    {
        var room = await _db.TelecomRooms.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (room is null)
        {
            return NotFound(new ApiError("电信房间记录不存在"));
        }
        if (CurrentProjectId is not Guid currentProjectId || room.ProjectId != currentProjectId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能修改当前工作区项目下的统计记录"));
        }
        if (!await CanAccessProjectAsync(room.ProjectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleAsync(x => x.Id == room.ProjectId, ct);
        var validation = await ValidateRequestAsync(project, request, ct);
        if (validation is not null)
        {
            return validation;
        }

        room.PbsNodeId = request.PbsNodeId;
        room.Floor = request.Floor.Trim();
        room.Code = request.Code.Trim();
        room.Name = request.Name?.Trim() ?? string.Empty;
        room.DataPointCount = request.DataPointCount;
        room.PhonePointCount = request.PhonePointCount;
        room.ApCount = request.ApCount;
        room.SortOrder = request.SortOrder;
        room.Remark = Clean(request.Remark);
        room.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "TelecomRoom",
            room.Id.ToString(), room.Code);
        await _db.SaveChangesAsync(ct);

        var updated = await _db.TelecomRooms.Include(x => x.PbsNode).AsNoTracking()
            .SingleAsync(x => x.Id == room.Id, ct);
        return Ok(ToDto(updated));
    }

    [HttpDelete("telecom-rooms/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("telecom.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var room = await _db.TelecomRooms.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (room is null)
        {
            return NotFound(new ApiError("电信房间记录不存在"));
        }
        if (CurrentProjectId is not Guid currentProjectId || room.ProjectId != currentProjectId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能删除当前工作区项目下的统计记录"));
        }
        if (!await CanAccessProjectAsync(room.ProjectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        _db.TelecomRooms.Remove(room);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "TelecomRoom",
            room.Id.ToString(), room.Code);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<List<TelecomRoom>> QueryRoomsAsync(Guid projectId, CancellationToken ct) =>
        _db.TelecomRooms
            .Include(x => x.PbsNode)
            .Where(x => x.ProjectId == projectId)
            .AsNoTracking()
            .OrderBy(x => x.PbsNode.SortOrder)
            .ThenBy(x => x.PbsNode.Code)
            .ThenBy(x => x.Floor)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);

    private async Task<ActionResult?> ValidateRequestAsync(
        Project project,
        TelecomRoomUpsertRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Floor) ||
            string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new ApiError("楼层和房间编码不能为空"));
        }
        if (request.DataPointCount < 0 || request.PhonePointCount < 0 || request.ApCount < 0)
        {
            return BadRequest(new ApiError("信息点、电话点和无线 AP 数量不能为负数"));
        }

        var building = await _db.PbsNodes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.PbsNodeId, ct);
        if (building is null || building.PlantId != project.PlantId)
        {
            return BadRequest(new ApiError("建筑物 PBS 节点不存在或不属于当前工厂"));
        }
        if (building.NodeType != PbsNodeType.Building)
        {
            return BadRequest(new ApiError("只能关联 PBS 中的建筑物节点"));
        }
        if (!await IsInProjectScopeAsync(project, building.Id, ct))
        {
            return BadRequest(new ApiError("建筑物不在当前项目的 PBS 范围内"));
        }

        return null;
    }

    private async Task<bool> IsInProjectScopeAsync(
        Project project,
        Guid buildingNodeId,
        CancellationToken ct)
    {
        if (project.PbsRootNodeId is not Guid rootId)
        {
            return true;
        }

        var nodes = await _db.PbsNodes
            .Where(x => x.PlantId == project.PlantId)
            .AsNoTracking()
            .ToListAsync(ct);
        var byId = nodes.ToDictionary(x => x.Id);

        var current = byId[buildingNodeId];
        var visited = new HashSet<Guid>();
        while (visited.Add(current.Id))
        {
            if (current.Id == rootId)
            {
                return true;
            }
            if (current.ParentId is not Guid parentId || !byId.TryGetValue(parentId, out var parent))
            {
                return false;
            }
            current = parent;
        }

        return false;
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct) =>
        User.IsInRole("Admin") ||
        await _db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == CurrentUserId, ct);

    private static TelecomRoomDto ToDto(TelecomRoom room) => new(
        room.Id,
        room.ProjectId,
        room.PbsNodeId,
        room.PbsNode.Code,
        room.PbsNode.Name,
        room.Floor,
        room.Code,
        room.Name,
        room.DataPointCount,
        room.PhonePointCount,
        room.ApCount,
        room.Remark,
        room.SortOrder);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
