using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.Services;

public interface IEquipmentDialogService
{
    EquipmentUpsertRequest? EditEquipment(EquipmentEditorContext context);
    bool Confirm(string message);
}
