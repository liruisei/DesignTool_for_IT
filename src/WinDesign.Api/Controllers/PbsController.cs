using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Permissions;
using WinDesign.Domain;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 工厂 PBS。根节点是工厂本身，因此节点接口挂在工厂下，而不是项目下。
/// </summary>
[Route("api")]
public sealed class PbsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public PbsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("plants/{plantId:guid}/pbs")]
    [Authorize(Policy = AuthorizationPolicies.ClientAccess)]
    [RequirePermission("pbs.read")]
    public async Task<ActionResult<IReadOnlyList<PbsNodeDto>>> Get(Guid plantId, CancellationToken ct)
    {
        if (EnsureWorkspacePlant(plantId) is ActionResult denied)
        {
            return denied;
        }
        if (!await _db.Plants.AnyAsync(x => x.Id == plantId, ct))
        {
            return NotFound(new ApiError("工厂不存在"));
        }

        var nodes = await _db.PbsNodes
            .Where(x => x.PlantId == plantId)
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);

        var equipNodeIds = await _db.Equipments
            .Where(x => x.PlantId == plantId && x.PbsNodeId != null)
            .Select(x => x.PbsNodeId!.Value)
            .ToListAsync(ct);

        var directCounts = equipNodeIds
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        var childrenMap = nodes
            .Where(x => x.ParentId != null)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var subtreeCounts = new Dictionary<Guid, int>();
        int CountSubtree(Guid nodeId)
        {
            if (subtreeCounts.TryGetValue(nodeId, out var cached))
            {
                return cached;
            }

            var count = directCounts.GetValueOrDefault(nodeId);
            if (childrenMap.TryGetValue(nodeId, out var children))
            {
                foreach (var child in children)
                {
                    count += CountSubtree(child.Id);
                }
            }
            subtreeCounts[nodeId] = count;
            return count;
        }

        var result = nodes
            .Select(x => ToDto(
                x,
                childrenMap.GetValueOrDefault(x.Id)?.Count ?? 0,
                CountSubtree(x.Id)))
            .ToList();

        return Ok(result);
    }

    [HttpPost("plants/{plantId:guid}/pbs")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("pbs.create")]
    public async Task<ActionResult<PbsNodeDto>> Create(
        Guid plantId,
        [FromBody] CreatePbsNodeRequest request,
        CancellationToken ct)
    {
        if (EnsureWorkspacePlant(plantId) is ActionResult denied)
        {
            return denied;
        }
        if (!await _db.Plants.AnyAsync(x => x.Id == plantId, ct))
        {
            return NotFound(new ApiError("工厂不存在"));
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("节点编码和名称不能为空"));
        }
        if (await _db.PbsNodes.AnyAsync(x => x.PlantId == plantId && x.Code == code, ct))
        {
            return Conflict(new ApiError("节点编码已存在"));
        }
        if (!Enum.TryParse<PbsNodeType>(request.NodeType, true, out var nodeType) ||
            nodeType == PbsNodeType.Root)
        {
            return BadRequest(new ApiError("节点类型无效；工厂已是根节点，请选择区域/装置/回路等层级"));
        }

        if (request.ParentId is Guid parentId)
        {
            var parentExists = await _db.PbsNodes
                .AnyAsync(x => x.Id == parentId && x.PlantId == plantId, ct);
            if (!parentExists)
            {
                return BadRequest(new ApiError("上级节点不存在或不属于当前工厂"));
            }
        }

        var node = new PbsNode
        {
            PlantId = plantId,
            ParentId = request.ParentId,
            Code = code,
            Name = request.Name.Trim(),
            NodeType = nodeType,
            SortOrder = request.SortOrder,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        _db.PbsNodes.Add(node);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "PbsNode", node.Id.ToString(), $"{code} {node.Name}");
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(node, 0, 0));
    }

    /// <summary>
    /// 设计端在装置的「回路」「建筑物」分类文件夹上右键新增。
    /// 该接口只允许创建 Loop / Building 两类子节点，PBS 的其他维护仍走管理端。
    /// </summary>
    [HttpPost("units/{unitId:guid}/pbs-children")]
    [Authorize(Policy = AuthorizationPolicies.DesignClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("pbs.create")]
    public async Task<ActionResult<PbsNodeDto>> CreateUnitChild(
        Guid unitId,
        [FromBody] CreateUnitChildRequest request,
        CancellationToken ct)
    {
        var unit = await _db.PbsNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == unitId, ct);
        if (unit is null)
        {
            return NotFound(new ApiError("装置不存在"));
        }
        if (EnsureWorkspacePlant(unit.PlantId) is ActionResult denied)
        {
            return denied;
        }
        if (unit.NodeType != PbsNodeType.Unit)
        {
            return BadRequest(new ApiError("只能在装置下新增回路或建筑物"));
        }
        if (!Enum.TryParse<PbsNodeType>(request.NodeType, true, out var nodeType) ||
            nodeType is not (PbsNodeType.Loop or PbsNodeType.Building))
        {
            return BadRequest(new ApiError("只能新增回路或建筑物"));
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("编码和名称不能为空"));
        }
        if (await _db.PbsNodes.AnyAsync(x => x.PlantId == unit.PlantId && x.Code == code, ct))
        {
            return Conflict(new ApiError("编码已存在"));
        }

        var maxSort = await _db.PbsNodes
            .Where(x => x.ParentId == unitId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var node = new PbsNode
        {
            PlantId = unit.PlantId,
            ParentId = unitId,
            Code = code,
            Name = request.Name.Trim(),
            NodeType = nodeType,
            SortOrder = (maxSort ?? 0) + 1,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        _db.PbsNodes.Add(node);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "PbsNode", node.Id.ToString(),
            $"{nodeType} {code} {node.Name}");
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(node, 0, 0));
    }

    /// <summary>
    /// 设计端编辑装置下的回路 / 建筑物节点（当前项目 PBS 范围内）。
    /// </summary>
    [HttpPut("units/{unitId:guid}/pbs-children/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.DesignClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("pbs.update")]
    public async Task<ActionResult<PbsNodeDto>> UpdateUnitChild(
        Guid unitId,
        Guid id,
        [FromBody] UpdateUnitChildRequest request,
        CancellationToken ct)
    {
        var unit = await _db.PbsNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == unitId, ct);
        if (unit is null)
        {
            return NotFound(new ApiError("装置不存在"));
        }
        if (EnsureWorkspacePlant(unit.PlantId) is ActionResult denied)
        {
            return denied;
        }
        if (unit.NodeType != PbsNodeType.Unit)
        {
            return BadRequest(new ApiError("只能在装置下编辑回路或建筑物"));
        }

        var node = await _db.PbsNodes.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (node is null)
        {
            return NotFound(new ApiError("节点不存在"));
        }
        if (node.ParentId != unitId)
        {
            return BadRequest(new ApiError("节点不属于该装置"));
        }
        if (node.NodeType is not (PbsNodeType.Loop or PbsNodeType.Building))
        {
            return BadRequest(new ApiError("只能编辑回路或建筑物"));
        }
        if (!await IsInCurrentProjectScopeAsync(unit, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("节点不在当前项目的 PBS 范围内"));
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("编码和名称不能为空"));
        }
        if (await _db.PbsNodes.AnyAsync(
                x => x.PlantId == unit.PlantId && x.Code == code && x.Id != id, ct))
        {
            return Conflict(new ApiError("编码已存在"));
        }

        node.Code = code;
        node.Name = request.Name.Trim();
        node.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        node.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "PbsNode", node.Id.ToString(),
            $"{node.NodeType} {node.Code} {node.Name}");
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(node, 0, 0));
    }

    private async Task<bool> IsInCurrentProjectScopeAsync(PbsNode node, CancellationToken ct)
    {
        if (CurrentProjectId is not Guid projectId)
        {
            return false;
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null)
        {
            return false;
        }
        if (project.PbsRootNodeId is not Guid rootId)
        {
            return true;
        }

        var nodes = await _db.PbsNodes
            .Where(x => x.PlantId == node.PlantId)
            .AsNoTracking()
            .ToListAsync(ct);
        var byId = nodes.ToDictionary(x => x.Id);

        var current = byId[node.Id];
        var visited = new HashSet<Guid>();
        while (visited.Add(current.Id))
        {
            if (current.Id == rootId)
            {
                return true;
            }
            if (current.ParentId is Guid parentId && byId.TryGetValue(parentId, out var parent))
            {
                current = parent;
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    [HttpPut("pbs/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("pbs.update")]
    public async Task<ActionResult<PbsNodeDto>> Update(
        Guid id,
        [FromBody] UpdatePbsNodeRequest request,
        CancellationToken ct)
    {
        var node = await _db.PbsNodes.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (node is null)
        {
            return NotFound(new ApiError("PBS 节点不存在"));
        }
        if (EnsureWorkspacePlant(node.PlantId) is ActionResult denied)
        {
            return denied;
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("节点编码和名称不能为空"));
        }
        if (await _db.PbsNodes.AnyAsync(x => x.PlantId == node.PlantId && x.Code == code && x.Id != id, ct))
        {
            return Conflict(new ApiError("节点编码已存在"));
        }
        if (!Enum.TryParse<PbsNodeType>(request.NodeType, true, out var nodeType) ||
            nodeType == PbsNodeType.Root)
        {
            return BadRequest(new ApiError("节点类型无效；工厂已是根节点"));
        }
        if (request.ParentId is not null &&
            await _db.Projects.AnyAsync(x => x.PbsRootNodeId == id, ct))
        {
            return BadRequest(new ApiError("该节点是项目的 PBS 根节点，不能移动到其他节点下"));
        }

        if (request.ParentId is Guid parentId)
        {
            if (parentId == id)
            {
                return BadRequest(new ApiError("上级节点不能是自己"));
            }
            var parentExists = await _db.PbsNodes
                .AnyAsync(x => x.Id == parentId && x.PlantId == node.PlantId, ct);
            if (!parentExists)
            {
                return BadRequest(new ApiError("上级节点不存在或不属于当前工厂"));
            }
            if (await WouldCreateCycleAsync(id, parentId, ct))
            {
                return BadRequest(new ApiError("上级节点不能是当前节点的下级"));
            }
        }

        node.ParentId = request.ParentId;
        node.Code = code;
        node.Name = request.Name.Trim();
        node.NodeType = nodeType;
        node.SortOrder = request.SortOrder;
        node.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        node.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "PbsNode", node.Id.ToString(), $"{code} {node.Name}");
        await _db.SaveChangesAsync(ct);

        var childCount = await _db.PbsNodes.CountAsync(x => x.ParentId == node.Id, ct);
        var equipCount = await _db.Equipments.CountAsync(x => x.PbsNodeId == node.Id, ct);
        return Ok(ToDto(node, childCount, equipCount));
    }

    [HttpDelete("pbs/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("pbs.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var node = await _db.PbsNodes.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (node is null)
        {
            return NotFound(new ApiError("PBS 节点不存在"));
        }
        if (EnsureWorkspacePlant(node.PlantId) is ActionResult denied)
        {
            return denied;
        }
        if (await _db.Projects.AnyAsync(x => x.PbsRootNodeId == id, ct))
        {
            return BadRequest(new ApiError("该节点是项目的 PBS 根节点，不能删除"));
        }
        if (await _db.PbsNodes.AnyAsync(x => x.ParentId == id, ct))
        {
            return BadRequest(new ApiError("请先删除下级节点"));
        }
        if (await _db.Equipments.AnyAsync(x => x.PbsNodeId == id, ct))
        {
            return BadRequest(new ApiError("节点下还有设备，不能删除"));
        }

        _db.PbsNodes.Remove(node);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "PbsNode", node.Id.ToString(), node.Code);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> WouldCreateCycleAsync(Guid nodeId, Guid newParentId, CancellationToken ct)
    {
        var current = (Guid?)newParentId;
        var guard = 0;
        while (current is not null && guard++ < 200)
        {
            if (current == nodeId)
            {
                return true;
            }
            current = await _db.PbsNodes
                .Where(x => x.Id == current)
                .Select(x => x.ParentId)
                .SingleOrDefaultAsync(ct);
        }
        return false;
    }

    private static PbsNodeDto ToDto(PbsNode node, int childrenCount, int equipmentCount) => new(
        node.Id,
        node.PlantId,
        node.ParentId,
        node.Code,
        node.Name,
        node.NodeType.ToString(),
        node.SortOrder,
        node.Description,
        childrenCount,
        equipmentCount);
}
