using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Services;

public interface IMaterialDialogService
{
    MaterialEditResult? EditMaterial(MaterialEditorContext context);
    bool Confirm(string message);
}
