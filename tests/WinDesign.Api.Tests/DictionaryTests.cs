using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Api.Tests;

public class DictionaryTests : ApiTestBase
{
    public DictionaryTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UpdateEquipmentType_WithDiscipline_PersistsInExtraJson()
    {
        await LoginManagementAsync();

        var types = await Client.GetFromJsonAsync<List<DictionaryTypeDto>>("/api/dictionaries/types");
        var equipmentType = types!.Single(x => x.Code == "EQUIPMENT_TYPE");
        var items = await Client.GetFromJsonAsync<List<DictionaryItemDto>>(
            "/api/dictionaries/types/EQUIPMENT_TYPE/items");
        var pressureTransmitter = items!.Single(x => x.Code == "PT");

        var response = await Client.PutAsJsonAsync(
            $"/api/dictionaries/items/{pressureTransmitter.Id}",
            new
            {
                name = pressureTransmitter.Name,
                sortOrder = pressureTransmitter.SortOrder,
                isActive = true,
                extraJson = pressureTransmitter.ExtraJson,
                discipline = "Instrument"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<DictionaryItemDto>();
        Assert.Equal("Instrument", saved!.Discipline);
        Assert.Contains("discipline", saved.ExtraJson);

        var reloaded = await Client.GetFromJsonAsync<List<DictionaryItemDto>>(
            $"/api/dictionaries/types/{equipmentType.Code}/items");
        Assert.Equal("Instrument", reloaded!.Single(x => x.Id == saved.Id).Discipline);
    }

    [Fact]
    public async Task DeleteEquipmentType_BlockedWhileReferencedByEquipment()
    {
        await LoginManagementAsync();

        var code = "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var createItem = await Client.PostAsJsonAsync(
            "/api/dictionaries/types/EQUIPMENT_TYPE/items",
            new
            {
                code,
                name = "临时设备类型",
                sortOrder = 99,
                extraJson = (string?)null,
                discipline = "Instrument"
            });
        createItem.EnsureSuccessStatusCode();
        var item = (await createItem.Content.ReadFromJsonAsync<DictionaryItemDto>())!;

        // 设计端用该类型实例化一台设备。
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var tagNo = "TEST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var createEquipment = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/equipments", new
        {
            pbsNodeId = (Guid?)null,
            tagNo,
            name = "引用临时设备类型的设备",
            typeCode = code,
            discipline = "Instrument",
            location = "",
            description = ""
        });
        createEquipment.EnsureSuccessStatusCode();
        var equipment = (await createEquipment.Content.ReadFromJsonAsync<EquipmentDto>())!;

        try
        {
            await LoginManagementAsync();
            var deleteInUse = await Client.DeleteAsync($"/api/dictionaries/items/{item.Id}");
            Assert.Equal(HttpStatusCode.BadRequest, deleteInUse.StatusCode);

            var stillThere = await Client.GetFromJsonAsync<List<DictionaryItemDto>>(
                "/api/dictionaries/types/EQUIPMENT_TYPE/items");
            Assert.Contains(stillThere!, x => x.Id == item.Id);
        }
        finally
        {
            // 设备移除后，类型不再被引用，应恢复可删除。
            await LoginAsync();
            var deleteEquipment = await Client.DeleteAsync($"/api/equipments/{equipment.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteEquipment.StatusCode);

            await LoginManagementAsync();
            var deleteUnused = await Client.DeleteAsync($"/api/dictionaries/items/{item.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteUnused.StatusCode);
        }
    }

    [Fact]
    public async Task UpdateEquipmentTypeCode_SyncsTypeReferenceButKeepsEquipmentTag()
    {
        await LoginManagementAsync();

        var oldCode = "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var newCode = oldCode + "N";
        var createItem = await Client.PostAsJsonAsync(
            "/api/dictionaries/types/EQUIPMENT_TYPE/items",
            new
            {
                code = oldCode,
                name = "改名设备类型",
                sortOrder = 98,
                extraJson = (string?)null,
                discipline = "Instrument"
            });
        createItem.EnsureSuccessStatusCode();
        var item = (await createItem.Content.ReadFromJsonAsync<DictionaryItemDto>())!;

        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var tagNo = "TEST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var createEquipment = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/equipments", new
        {
            pbsNodeId = (Guid?)null,
            tagNo,
            name = "改编码前的设备",
            typeCode = oldCode,
            discipline = "Instrument",
            location = "",
            description = ""
        });
        createEquipment.EnsureSuccessStatusCode();
        var equipment = (await createEquipment.Content.ReadFromJsonAsync<EquipmentDto>())!;

        try
        {
            await LoginManagementAsync();
            var update = await Client.PutAsJsonAsync($"/api/dictionaries/items/{item.Id}", new
            {
                name = item.Name,
                sortOrder = item.SortOrder,
                isActive = true,
                extraJson = item.ExtraJson,
                discipline = "Instrument",
                code = newCode
            });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var saved = (await update.Content.ReadFromJsonAsync<DictionaryItemDto>())!;
            Assert.Equal(newCode, saved.Code);

            // 位号是实例化时的快照，不随类型编码修改；只有类型引用同步到新编码。
            await LoginAsync();
            var reloaded = await Client.GetFromJsonAsync<EquipmentDto>($"/api/equipments/{equipment.Id}");
            Assert.Equal(tagNo, reloaded!.TagNo);
            Assert.Equal(newCode, reloaded.TypeCode);
        }
        finally
        {
            await LoginAsync();
            await Client.DeleteAsync($"/api/equipments/{equipment.Id}");

            await LoginManagementAsync();
            await Client.DeleteAsync($"/api/dictionaries/items/{item.Id}");
        }
    }
}
