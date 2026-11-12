using System.Windows;
using Wpf.Ui.Controls;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

public partial class LoginWindow : FluentWindow
{
    private LoginViewModel? _viewModel;

    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.LoginSucceeded -= OnLoginSucceeded;
        }

        _viewModel = DataContext as LoginViewModel;
        if (_viewModel is not null)
        {
            _viewModel.LoginSucceeded += OnLoginSucceeded;
        }
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnLoginClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordInput.Password;
            viewModel.LoginCommand.Execute(null);
        }
    }
}
