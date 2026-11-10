using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Services;

public interface IEquipmentSpecDialogService
{
    EquipmentSpecEditResult? EditSpec(EquipmentSpecEditorContext context);
    bool Confirm(string message);
}
