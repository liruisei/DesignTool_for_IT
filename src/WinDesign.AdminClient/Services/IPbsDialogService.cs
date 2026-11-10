using WinDesign.AdminClient.ViewModels;

namespace WinDesign.AdminClient.Services;

public interface IPbsDialogService
{
    PbsNodeEditorResult? EditPbsNode(PbsNodeEditorContext context);
    PlantEditorResult? EditPlant(PlantEditorContext context);
    bool Confirm(string message);
}
