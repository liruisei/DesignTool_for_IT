using WinDesign.Client.ViewModels;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;

namespace WinDesign.AdminClient.ViewModels;

public partial class DictionaryViewModel : ViewModelBase, IPendingChanges
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

    public bool HasSelectedType => SelectedType is not null;
    public bool HasSelectedItem => SelectedItem is not null;

    public DictionaryViewModel(SessionService session)
    {
        _session = session;
        Title = "编码管理";
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

        // 默认停留在“新建”状态：只有用户主动选中类型时才进入编辑状态
        SelectedType = Types.FirstOrDefault(x => x.Id == currentId);
    });

    /// <summary>先保存编码项，再保存编码类型（类型保存会刷新列表）。新建表单不自动创建。</summary>
    public async Task SavePendingAsync()
    {
        var editingTypeId = IsCreatingType ? (Guid?)null : SelectedType?.Id;

        if (!IsCreatingItem && SelectedItem is not null)
        {
            await SaveItemAsync();
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                throw new ApiException("编码项：" + ErrorMessage);
            }
        }

        // 保存期间用户可能已切换到其他类型，此时不要覆盖其编辑内容。
        if (editingTypeId is not null && SelectedType?.Id == editingTypeId)
        {
            await SaveTypeAsync();
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                throw new ApiException("编码类型：" + ErrorMessage);
            }
        }
    }

    [RelayCommand]
    private void NewType()
    {
        SelectedType = null;
        ResetTypeEditor();
        Items.Clear();
        SelectedItem = null;
        StatusMessage = null;
    }

    [RelayCommand]
    private void CancelTypeEdit()
    {
        if (!IsCreatingType && SelectedType is not null)
        {
            LoadTypeIntoEditor(SelectedType);
            return;
        }

        ResetTypeEditor();
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveTypeAsync() => RunAsync(async () =>
    {
        // 记录保存开始时正在编辑的类型，保存完成后若用户已切换选择则不要抢回。
        var editedTypeId = SelectedType?.Id;
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
        if (SelectedType is null || SelectedType.Id == editedTypeId)
        {
            SelectedType = Types.FirstOrDefault(x => x.Id == saved.Id);
        }
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
        ResetItemEditor();
        StatusMessage = null;
    }

    [RelayCommand]
    private void CancelItemEdit()
    {
        if (!IsCreatingItem && SelectedItem is not null)
        {
            LoadItemIntoEditor(SelectedItem);
            return;
        }

        ResetItemEditor();
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

        // 保存期间用户可能已选中另一条编码项，此时保持其选择。
        var selectionBeforeReload = SelectedItem?.Id;
        await LoadItemsAsync(SelectedType.Code, selectionBeforeReload ?? saved.Id);
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
        OnPropertyChanged(nameof(HasSelectedType));

        if (value is null)
        {
            ResetTypeEditor();
            Items.Clear();
            SelectedItem = null;
            return;
        }

        LoadTypeIntoEditor(value);
        StatusMessage = null;
        _ = LoadItemsAsync(value.Code);
    }

    partial void OnSelectedItemChanged(DictionaryItemDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));

        if (value is null)
        {
            ResetItemEditor();
            return;
        }

        LoadItemIntoEditor(value);
    }

    private Task LoadItemsAsync(string typeCode, Guid? selectId = null) => RunAsync(async () =>
    {
        var items = await _session.Api.GetDictionaryItemsAsync(typeCode);
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        // 未指定保存后的回选项时保持“新建”状态，不自动选中第一行
        SelectedItem = selectId is null ? null : Items.FirstOrDefault(x => x.Id == selectId);
    });

    private void ResetTypeEditor()
    {
        IsCreatingType = true;
        TypeEditorTitle = "新建编码类型";
        EditTypeCode = string.Empty;
        EditTypeName = string.Empty;
        EditTypeDescription = string.Empty;
        EditTypeSort = 0;
    }

    private void LoadTypeIntoEditor(DictionaryTypeDto type)
    {
        IsCreatingType = false;
        TypeEditorTitle = "编辑编码类型：" + type.Code;
        EditTypeCode = type.Code;
        EditTypeName = type.Name;
        EditTypeDescription = type.Description ?? string.Empty;
        EditTypeSort = type.SortOrder;
    }

    private void ResetItemEditor()
    {
        IsCreatingItem = true;
        ItemEditorTitle = "新建编码项";
        EditItemCode = string.Empty;
        EditItemName = string.Empty;
        EditItemSort = 0;
        EditItemActive = true;
    }

    private void LoadItemIntoEditor(DictionaryItemDto item)
    {
        IsCreatingItem = false;
        ItemEditorTitle = "编辑编码项：" + item.Code;
        EditItemCode = item.Code;
        EditItemName = item.Name;
        EditItemSort = item.SortOrder;
        EditItemActive = item.IsActive;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
