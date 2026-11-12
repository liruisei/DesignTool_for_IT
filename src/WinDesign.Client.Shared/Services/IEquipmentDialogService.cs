using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.Services;

public interface IEquipmentDialogService
{
    /// <summary>
    /// 打开设备编辑弹窗。<paramref name="saveAsync"/> 不为空时，弹窗内点「确定」会先执行保存；
    /// 返回错误信息时弹窗保持打开并显示错误。
    /// </summary>
    EquipmentUpsertRequest? EditEquipment(
        EquipmentEditorContext context,
        Func<EquipmentUpsertRequest, Task<string?>>? saveAsync = null);

    bool Confirm(string message);
}
