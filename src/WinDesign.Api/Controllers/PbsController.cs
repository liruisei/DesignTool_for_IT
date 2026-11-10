using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Pbs;
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

    [HttpPut("pbs/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
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
