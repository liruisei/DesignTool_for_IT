using WinDesign.Client.ViewModels;
namespace WinDesign.DesignClient.ViewModels;

public sealed class ModulePlaceholderViewModel : ViewModelBase
{
    public ModulePlaceholderViewModel(
        string title,
        string phase,
        string description,
        IReadOnlyList<string> features)
    {
        Title = title;
        Phase = phase;
        Description = description;
        Features = features;
    }

    public string Phase { get; }
    public string Description { get; }
    public IReadOnlyList<string> Features { get; }
    public string Hint => "该模块已完成导航与页面骨架，业务接口将在下一阶段接入。";
}
