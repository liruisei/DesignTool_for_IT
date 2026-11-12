using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Dictionaries;

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
}
