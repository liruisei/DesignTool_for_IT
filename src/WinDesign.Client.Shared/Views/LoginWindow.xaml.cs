using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

public partial class LoginWindow : FluentWindow
{
    private LoginViewModel? _viewModel;
    private bool _passwordVisible;

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
            viewModel.Password = GetPassword();
            viewModel.LoginCommand.Execute(null);
        }
    }

    /// <summary>当前是否以明文显示密码。</summary>
    private string GetPassword() => _passwordVisible ? PasswordVisible.Text : PasswordInput.Password;

    private void OnTogglePasswordVisibility(object sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;

        if (_passwordVisible)
        {
            PasswordVisible.Text = PasswordInput.Password;
            PasswordInput.Visibility = Visibility.Collapsed;
            PasswordVisible.Visibility = Visibility.Visible;
            PasswordEyeButton.ToolTip = "隐藏密码";
            PasswordEyeButton.Foreground = (Brush)FindResource("AccentBrush");
            PasswordVisible.Focus();
            PasswordVisible.CaretIndex = PasswordVisible.Text.Length;
        }
        else
        {
            PasswordInput.Password = PasswordVisible.Text;
            PasswordVisible.Visibility = Visibility.Collapsed;
            PasswordInput.Visibility = Visibility.Visible;
            PasswordEyeButton.ToolTip = "显示密码";
            PasswordEyeButton.Foreground = (Brush)FindResource("TextSecondaryBrush");
            PasswordInput.Focus();
        }
    }
}
