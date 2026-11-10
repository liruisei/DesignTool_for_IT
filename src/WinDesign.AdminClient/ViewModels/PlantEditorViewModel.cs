using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Contracts.Plants;

namespace WinDesign.AdminClient.ViewModels;

public sealed record PlantEditorContext(PlantDto? Plant);

public sealed record PlantEditorResult(
    string Code,
    string Name,
    string? ClientName,
    string? Location,
    string? Description,
    bool IsActive);

public partial class PlantEditorViewModel : ObservableObject
{
    public bool IsCreating { get; }

    [ObservableProperty]
    private string title = "新建工厂";

    [ObservableProperty]
    private string editCode = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editClient = string.Empty;

    [ObservableProperty]
    private string editLocation = string.Empty;

    [ObservableProperty]
    private string editDescription = string.Empty;

    [ObservableProperty]
    private bool editIsActive = true;

    [ObservableProperty]
    private string? errorMessage;

    public PlantEditorViewModel(PlantEditorContext context)
    {
        IsCreating = context.Plant is null;
        if (context.Plant is { } plant)
        {
            Title = "编辑工厂：" + plant.Code;
            EditCode = plant.Code;
            EditName = plant.Name;
            EditClient = plant.ClientName ?? string.Empty;
            EditLocation = plant.Location ?? string.Empty;
            EditDescription = plant.Description ?? string.Empty;
            EditIsActive = plant.IsActive;
        }
        else
        {
            Title = "新建工厂";
        }
    }

    public bool TryBuild(out PlantEditorResult? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
        {
            error = "工厂编码和名称不能为空";
            return false;
        }

        result = new PlantEditorResult(
            EditCode.Trim(),
            EditName.Trim(),
            NullIfEmpty(EditClient),
            NullIfEmpty(EditLocation),
            NullIfEmpty(EditDescription),
            EditIsActive);
        return true;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
