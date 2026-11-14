using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Numbering;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 项目级设备位号编号规定：管理员为每个项目预先定义位号使用哪些字段，
/// 保证该项目内设备位号口径一致。下拉选项是字段名，而不是具体数据值。
/// </summary>
[Route("api")]
public sealed class NumberingRuleController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public NumberingRuleController(WinDesignDbContext db)
    {
        _db = db;
    }

    /// <summary>管理端可读任意项目；设计端只能读当前工作区项目。</summary>
    [HttpGet("projects/{projectId:guid}/numbering-rule")]
    [Authorize(Policy = AuthorizationPolicies.ClientAccess)]
    public async Task<ActionResult<NumberingRuleDto>> Get(Guid projectId, CancellationToken ct)
    {
        if (!IsManagementMode && CurrentProjectId != projectId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能读取当前工作区项目的编号规定"));
        }
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId, ct))
        {
            return NotFound(new ApiError("项目不存在"));
        }

        var rule = await _db.NumberingRules.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);

        return Ok(rule is null
            ? new NumberingRuleDto(
                Guid.Empty, projectId,
                string.Empty, string.Empty, EquipmentTagFormat.DefaultPrefixLength,
                string.Empty, string.Empty, EquipmentTagFormat.DefaultTypeLength,
                EquipmentTagFormat.DefaultSequenceLength, string.Empty,
                EquipmentTagFormat.DefaultSuffixLength)
            : ToDto(rule));
    }

    [HttpPut("projects/{projectId:guid}/numbering-rule")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [RequirePermission("numbering.update")]
    public async Task<ActionResult<NumberingRuleDto>> Save(
        Guid projectId,
        [FromBody] UpsertNumberingRuleRequest request,
        CancellationToken ct)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId, ct))
        {
            return NotFound(new ApiError("项目不存在"));
        }

        var prefixField = Clean(request.PrefixField);
        var equipmentTypeField = Clean(request.EquipmentTypeField);
        var prefixSeparator = Clean(request.PrefixSeparator);
        var typeSeparator = Clean(request.TypeSeparator);

        if (prefixField is not ("" or NumberingFields.UnitCode))
        {
            return BadRequest(new ApiError("前缀字段无效"));
        }
        if (equipmentTypeField is not ("" or NumberingFields.EquipmentTypeCode))
        {
            return BadRequest(new ApiError("设备类型字段无效"));
        }
        if (prefixSeparator is not ("" or "-") || typeSeparator is not ("" or "-"))
        {
            return BadRequest(new ApiError("分隔符只能是「-」或留空"));
        }
        if (request.PrefixLength is < 1 or > EquipmentTagFormat.MaxLength ||
            request.TypeLength is < 1 or > EquipmentTagFormat.MaxLength)
        {
            return BadRequest(new ApiError("前缀/设备类型字段长度必须在 1 到 64 之间"));
        }
        if (request.SequenceLength is < 1 or > 12)
        {
            return BadRequest(new ApiError("顺序号长度必须在 1 到 12 之间"));
        }
        if (request.SuffixLength is < 0 or > 16)
        {
            return BadRequest(new ApiError("后缀长度必须在 0 到 16 之间"));
        }

        var rule = await _db.NumberingRules
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);
        if (rule is null)
        {
            rule = new NumberingRule { ProjectId = projectId };
            _db.NumberingRules.Add(rule);
        }

        rule.PrefixField = prefixField;
        rule.PrefixSeparator = prefixSeparator;
        rule.PrefixLength = request.PrefixLength;
        rule.EquipmentTypeField = equipmentTypeField;
        rule.TypeSeparator = typeSeparator;
        rule.TypeLength = request.TypeLength;
        rule.SequenceLength = request.SequenceLength;
        rule.Suffix = Clean(request.Suffix);
        rule.SuffixLength = request.SuffixLength;
        rule.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "NumberingRule",
            rule.Id.ToString(), "项目编号规定 " + projectId);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(rule));
    }

    private static NumberingRuleDto ToDto(NumberingRule rule) => new(
        rule.Id,
        rule.ProjectId,
        rule.PrefixField,
        rule.PrefixSeparator,
        rule.PrefixLength,
        rule.EquipmentTypeField,
        rule.TypeSeparator,
        rule.TypeLength,
        rule.SequenceLength,
        rule.Suffix,
        rule.SuffixLength);

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
