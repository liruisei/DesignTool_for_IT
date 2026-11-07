using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;

namespace WinDesign.Client.ViewModels;

public partial class DictionaryViewModel : ViewModelBase
{
    private readonly SessionService _session;

    public ObservableCollection<DictionaryTypeDto> Types { get; } = new();
    public ObservableCollection<DictionaryItemDto> Items { get; } = new();

    [ObservableProperty]
    private DictionaryTypeDto? selectedType;

    [ObservableProperty]
    private DictionaryItemDto? selectedItem;

    [ObservableProperty]
    private string typeEditorTitle = "新建编码类型";

    [ObservableProperty]
    private bool isCreatingType = true;

    [ObservableProperty]
    private string editTypeCode = string.Empty;

    [ObservableProperty]
    private string editTypeName = string.Empty;

    [ObservableProperty]
    private string editTypeDescription = string.Empty;

    [ObservableProperty]
    private int editTypeSort;

    [ObservableProperty]
    private string itemEditorTitle = "新建编码项";

    [ObservableProperty]
    private bool isCreatingItem = true;

    [ObservableProperty]
    private string editItemCode = string.Empty;

    [ObservableProperty]
    private string editItemName = string.Empty;

    [ObservableProperty]
    private int editItemSort;

    [ObservableProperty]
    private bool editItemActive = true;

    [ObservableProperty]
    private string? statusMessage;

    public DictionaryViewModel(SessionService session)
    {
        _session = session;
        Title = "编码定义";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var types = await _session.Api.GetDictionaryTypesAsync();
        var currentId = SelectedType?.Id;
        Types.Clear();
        foreach (var type in types)
        {
            Types.Add(type);
        }

        SelectedType = Types.FirstOrDefault(x => x.Id == currentId) ?? Types.FirstOrDefault();
    });

    [RelayCommand]
    private void NewType()
    {
        SelectedType = null;
        IsCreatingType = true;
        TypeEditorTitle = "新建编码类型";
        EditTypeCode = string.Empty;
        EditTypeName = string.Empty;
        EditTypeDescription = string.Empty;
        EditTypeSort = 0;
        Items.Clear();
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveTypeAsync() => RunAsync(async () =>
    {
        DictionaryTypeDto saved;
        if (IsCreatingType)
        {
            if (string.IsNullOrWhiteSpace(EditTypeCode) || string.IsNullOrWhiteSpace(EditTypeName))
            {
                throw new ApiException("编码和名称不能为空");
            }

            saved = await _session.Api.CreateDictionaryTypeAsync(new CreateDictionaryTypeRequest(
                EditTypeCode.Trim(),
                EditTypeName.Trim(),
                NullIfEmpty(EditTypeDescription),
                EditTypeSort));
            StatusMessage = "编码类型已创建";
        }
        else
        {
            if (SelectedType is null)
            {
                throw new ApiException("请选择编码类型");
            }

            saved = await _session.Api.UpdateDictionaryTypeAsync(SelectedType.Id, new UpdateDictionaryTypeRequest(
                EditTypeName.Trim(),
                NullIfEmpty(EditTypeDescription),
                EditTypeSort));
            StatusMessage = "编码类型已保存";
        }

        await RefreshAsync();
        SelectedType = Types.FirstOrDefault(x => x.Id == saved.Id);
    });

    [RelayCommand]
    private Task DeleteTypeAsync() => RunAsync(async () =>
    {
        if (SelectedType is null)
        {
            throw new ApiException("请选择编码类型");
        }
        if (SelectedType.IsSystem)
        {
            throw new ApiException("系统内置编码类型不能删除");
        }

        await _session.Api.DeleteDictionaryTypeAsync(SelectedType.Id);
        StatusMessage = "编码类型已删除";
        SelectedType = null;
        await RefreshAsync();
    });

    [RelayCommand]
    private void NewItem()
    {
        if (SelectedType is null)
        {
            return;
        }

        SelectedItem = null;
        IsCreatingItem = true;
        ItemEditorTitle = "新建编码项";
        EditItemCode = string.Empty;
        EditItemName = string.Empty;
        EditItemSort = 0;
        EditItemActive = true;
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveItemAsync() => RunAsync(async () =>
    {
        if (SelectedType is null)
        {
            throw new ApiException("请先选择编码类型");
        }

        DictionaryItemDto saved;
        if (IsCreatingItem)
        {
            if (string.IsNullOrWhiteSpace(EditItemCode) || string.IsNullOrWhiteSpace(EditItemName))
            {
                throw new ApiException("编码和名称不能为空");
            }

            saved = await _session.Api.CreateDictionaryItemAsync(SelectedType.Code, new CreateDictionaryItemRequest(
                EditItemCode.Trim(),
                EditItemName.Trim(),
                EditItemSort,
                null));
            StatusMessage = "编码项已创建";
        }
        else
        {
            if (SelectedItem is null)
            {
                throw new ApiException("请选择编码项");
            }

            saved = await _session.Api.UpdateDictionaryItemAsync(SelectedItem.Id, new UpdateDictionaryItemRequest(
                EditItemName.Trim(),
                EditItemSort,
                EditItemActive,
                SelectedItem.ExtraJson));
            StatusMessage = "编码项已保存";
        }

        await LoadItemsAsync(SelectedType.Code, saved.Id);
    });

    [RelayCommand]
    private Task DeleteItemAsync() => RunAsync(async () =>
    {
        if (SelectedItem is null || SelectedType is null)
        {
            throw new ApiException("请选择编码项");
        }

        await _session.Api.DeleteDictionaryItemAsync(SelectedItem.Id);
        StatusMessage = "编码项已删除";
        await LoadItemsAsync(SelectedType.Code);
    });

    partial void OnSelectedTypeChanged(DictionaryTypeDto? value)
    {
        if (value is null)
        {
            Items.Clear();
            return;
        }

        IsCreatingType = false;
        TypeEditorTitle = "编辑编码类型：" + value.Code;
        EditTypeCode = value.Code;
        EditTypeName = value.Name;
        EditTypeDescription = value.Description ?? string.Empty;
        EditTypeSort = value.SortOrder;
        StatusMessage = null;
        _ = LoadItemsAsync(value.Code);
    }

    partial void OnSelectedItemChanged(DictionaryItemDto? value)
    {
        if (value is null)
        {
            return;
        }

        IsCreatingItem = false;
        ItemEditorTitle = "编辑编码项：" + value.Code;
        EditItemCode = value.Code;
        EditItemName = value.Name;
        EditItemSort = value.SortOrder;
        EditItemActive = value.IsActive;
    }

    private Task LoadItemsAsync(string typeCode, Guid? selectId = null) => RunAsync(async () =>
    {
        var items = await _session.Api.GetDictionaryItemsAsync(typeCode);
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        SelectedItem = (selectId is null ? null : Items.FirstOrDefault(x => x.Id == selectId))
                       ?? Items.FirstOrDefault();
    });

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
