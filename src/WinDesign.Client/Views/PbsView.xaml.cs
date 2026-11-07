using System.Windows;
using System.Windows.Controls;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

public partial class PbsView : UserControl
{
    public PbsView()
    {
        InitializeComponent();
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is PbsViewModel viewModel && e.NewValue is PbsNodeItem item)
        {
            viewModel.SelectedNode = item;
        }
    }
}
