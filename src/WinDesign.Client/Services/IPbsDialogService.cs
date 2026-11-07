using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Services;

public interface IPbsDialogService
{
    PbsNodeEditorResult? EditPbsNode(PbsNodeEditorContext context);
    bool Confirm(string message);
}
