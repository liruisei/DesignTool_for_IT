using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Numbering;
using WinDesign.Contracts.Projects;

namespace WinDesign.Api.Tests;

public class NumberingRuleTests : ApiTestBase
{
    public NumberingRuleTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task NumberingRule_SaveAndGet_IsPerProject()
    {
        await LoginManagementAsync();
        var projects = await GetProjectsAsync();
        var project = projects.Single(x => x.Code == "DEMO-001");
        var other = projects.Single(x => x.Code == "DEMO-002");

        var save = await Client.PutAsJsonAsync($"/api/projects/{project.Id}/numbering-rule", new
        {
            prefixField = NumberingFields.UnitCode,
            prefixSeparator = "-",
            prefixLength = 4,
            equipmentTypeField = NumberingFields.EquipmentTypeCode,
            typeSeparator = "-",
            typeLength = 6,
            sequenceLength = 5,
            suffix = "A",
            suffixLength = 2
        });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<NumberingRuleDto>();
        Assert.NotNull(saved);
        Assert.Equal(project.Id, saved!.ProjectId);
        Assert.Equal(NumberingFields.UnitCode, saved.PrefixField);
        Assert.Equal(6, saved.TypeLength);
        Assert.Equal(5, saved.SequenceLength);
        Assert.Equal(2, saved.SuffixLength);

        var fetched = await Client.GetFromJsonAsync<NumberingRuleDto>(
            $"/api/projects/{project.Id}/numbering-rule");
        Assert.Equal(saved.Id, fetched!.Id);
        Assert.Equal(NumberingFields.EquipmentTypeCode, fetched.EquipmentTypeField);

        // 另一个项目不应读到这条规定。
        var otherRule = await Client.GetFromJsonAsync<NumberingRuleDto>(
            $"/api/projects/{other.Id}/numbering-rule");
        Assert.Equal(Guid.Empty, otherRule!.Id);
        Assert.Equal(string.Empty, otherRule.PrefixField);
    }

    [Fact]
    public async Task DesignClient_CanReadOnlyItsWorkspaceProjectRule()
    {
        await LoginManagementAsync();
        var projects = await GetProjectsAsync();
        var project = projects.Single(x => x.Code == "DEMO-001");
        var other = projects.Single(x => x.Code == "DEMO-002");

        var save = await Client.PutAsJsonAsync($"/api/projects/{project.Id}/numbering-rule", new
        {
            prefixField = NumberingFields.UnitCode,
            prefixSeparator = "-",
            prefixLength = 4,
            equipmentTypeField = NumberingFields.EquipmentTypeCode,
            typeSeparator = "-",
            typeLength = 5,
            sequenceLength = 4,
            suffix = string.Empty,
            suffixLength = 1
        });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        await LoginAsync();
        var fetched = await Client.GetFromJsonAsync<NumberingRuleDto>(
            $"/api/projects/{project.Id}/numbering-rule");
        Assert.Equal(project.Id, fetched!.ProjectId);

        var forbidden = await Client.GetAsync($"/api/projects/{other.Id}/numbering-rule");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task NumberingRule_InvalidFieldOrSeparator_ReturnsBadRequest()
    {
        await LoginManagementAsync();
        var project = (await GetProjectsAsync()).Single(x => x.Code == "DEMO-001");

        var invalidField = await Client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/numbering-rule", new
            {
                prefixField = "NotAField",
                prefixSeparator = "-",
                prefixLength = 4,
                equipmentTypeField = string.Empty,
                typeSeparator = string.Empty,
                typeLength = 5,
                sequenceLength = 4,
                suffix = string.Empty,
                suffixLength = 1
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidField.StatusCode);

        var invalidSeparator = await Client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/numbering-rule", new
            {
                prefixField = string.Empty,
                prefixSeparator = "_",
                prefixLength = 4,
                equipmentTypeField = string.Empty,
                typeSeparator = string.Empty,
                typeLength = 5,
                sequenceLength = 4,
                suffix = string.Empty,
                suffixLength = 1
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidSeparator.StatusCode);
    }

    private async Task<List<ProjectDto>> GetProjectsAsync()
    {
        var result = await Client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects");
        return result!.Items.ToList();
    }
}
