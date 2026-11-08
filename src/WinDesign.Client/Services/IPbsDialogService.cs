using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Services;

public interface IPbsDialogService
{
    PbsNodeEditorResult? EditPbsNode(PbsNodeEditorContext context);
    PlantEditorResult? EditPlant(PlantEditorContext context);
    bool Confirm(string message);
}
