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

[Route("api")]
public sealed class PbsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public PbsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("projects/{projectId:guid}/pbs")]
    public async Task<ActionResult<IReadOnlyList<PbsNodeDto>>> Get(Guid projectId, CancellationToken ct)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId, ct))
        {
            return NotFound(new ApiError("项目不存在"));
        }

        var nodes = await _db.PbsNodes
            .Where(x => x.ProjectId == projectId)
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);

        var equipNodeIds = await _db.Equipments
            .Where(x => x.ProjectId == projectId && x.PbsNodeId != null)
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

    [HttpPost("projects/{projectId:guid}/pbs")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<PbsNodeDto>> Create(
        Guid projectId,
        [FromBody] CreatePbsNodeRequest request,
        CancellationToken ct)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId, ct))
        {
            return NotFound(new ApiError("项目不存在"));
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("节点编码和名称不能为空"));
        }
        if (await _db.PbsNodes.AnyAsync(x => x.ProjectId == projectId && x.Code == code, ct))
        {
            return Conflict(new ApiError("节点编码已存在"));
        }
        if (!Enum.TryParse<PbsNodeType>(request.NodeType, true, out var nodeType))
        {
            return BadRequest(new ApiError("节点类型无效"));
        }

        if (request.ParentId is null)
        {
            if (await _db.PbsNodes.AnyAsync(x => x.ProjectId == projectId && x.ParentId == null, ct))
            {
                return BadRequest(new ApiError("项目已存在根节点"));
            }
            nodeType = PbsNodeType.Root;
        }
        else
        {
            var parentExists = await _db.PbsNodes
                .AnyAsync(x => x.Id == request.ParentId && x.ProjectId == projectId, ct);
            if (!parentExists)
            {
                return BadRequest(new ApiError("上级节点不存在或不属于当前项目"));
            }
            if (nodeType == PbsNodeType.Root)
            {
                return BadRequest(new ApiError("根节点不能再指定上级节点"));
            }
        }

        var node = new PbsNode
        {
            ProjectId = projectId,
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

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("节点编码和名称不能为空"));
        }
        if (await _db.PbsNodes.AnyAsync(x => x.ProjectId == node.ProjectId && x.Code == code && x.Id != id, ct))
        {
            return Conflict(new ApiError("节点编码已存在"));
        }
        if (!Enum.TryParse<PbsNodeType>(request.NodeType, true, out var nodeType))
        {
            return BadRequest(new ApiError("节点类型无效"));
        }

        if (request.ParentId is Guid parentId)
        {
            if (parentId == id)
            {
                return BadRequest(new ApiError("上级节点不能是自己"));
            }
            var parentExists = await _db.PbsNodes
                .AnyAsync(x => x.Id == parentId && x.ProjectId == node.ProjectId, ct);
            if (!parentExists)
            {
                return BadRequest(new ApiError("上级节点不存在或不属于当前项目"));
            }
            if (nodeType == PbsNodeType.Root)
            {
                return BadRequest(new ApiError("根节点不能再指定上级节点"));
            }
            if (await WouldCreateCycleAsync(id, parentId, ct))
            {
                return BadRequest(new ApiError("上级节点不能是当前节点的下级"));
            }
        }
        else
        {
            if (await _db.PbsNodes.AnyAsync(x => x.ProjectId == node.ProjectId && x.ParentId == null && x.Id != id, ct))
            {
                return BadRequest(new ApiError("项目已存在根节点"));
            }
            nodeType = PbsNodeType.Root;
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
    [Authorize(Roles = "Admin,Designer")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var node = await _db.PbsNodes.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (node is null)
        {
            return NotFound(new ApiError("PBS 节点不存在"));
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
        node.ProjectId,
        node.ParentId,
        node.Code,
        node.Name,
        node.NodeType.ToString(),
        node.SortOrder,
        node.Description,
        childrenCount,
        equipmentCount);
}
