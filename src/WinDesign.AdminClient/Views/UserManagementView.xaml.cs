using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.AdminClient.ViewModels;

namespace WinDesign.AdminClient.Views;

public partial class UserManagementView : UserControl
{
    private GridLength _lastEditorWidth = new(370);

    /// <summary>右键命中的用户行；为空时（表头或空白区域）不弹出菜单。</summary>
    private UserRowItem? _contextMenuUser;

    private UserManagementViewModel? _userViewModel;
    private bool _syncingPassword;
    private bool _passwordVisible;

    public UserManagementView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ---------- 行右键菜单 ----------

    private void OnUsersGridPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuUser = null;
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is DataGridRow row &&
            row.Item is UserRowItem user)
        {
            grid.SelectedItem = user;
            _contextMenuUser = user;
        }
    }

    private void OnUsersGridContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_contextMenuUser is null)
        {
            e.Handled = true;
        }
    }

    private void OnEditUserClick(object sender, RoutedEventArgs e) => ExpandEditor();

    private void OnDeleteUserClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not UserManagementViewModel viewModel || viewModel.SelectedUser is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"确定删除用户「{viewModel.SelectedUser.UserName}」吗？删除后不可恢复；该用户名下的项目负责人将转给当前登录用户。",
            "删除用户",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        viewModel.DeleteUserCommand.Execute(null);
    }

    // ---------- 密码显隐 ----------

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_userViewModel is not null)
        {
            _userViewModel.PropertyChanged -= OnUserViewModelPropertyChanged;
        }

        _userViewModel = DataContext as UserManagementViewModel;
        if (_userViewModel is not null)
        {
            _userViewModel.PropertyChanged += OnUserViewModelPropertyChanged;
        }

        SyncPasswordBoxFromViewModel();
    }

    private void OnUserViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserManagementViewModel.EditPassword))
        {
            SyncPasswordBoxFromViewModel();
        }
    }

    /// <summary>ViewModel 中的密码变化（选择用户 / 保存后清空）同步到隐藏的 PasswordBox。</summary>
    private void SyncPasswordBoxFromViewModel()
    {
        if (_syncingPassword)
        {
            return;
        }

        var password = _userViewModel?.EditPassword ?? string.Empty;
        _syncingPassword = true;
        try
        {
            if (PasswordHidden.Password != password)
            {
                PasswordHidden.Password = password;
            }
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword || _userViewModel is null)
        {
            return;
        }

        _syncingPassword = true;
        try
        {
            _userViewModel.EditPassword = PasswordHidden.Password;
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    private void OnTogglePasswordVisibility(object sender, RoutedEventArgs e)
        => SetPasswordVisible(!_passwordVisible);

    private void SetPasswordVisible(bool visible)
    {
        _passwordVisible = visible;
        _syncingPassword = true;
        try
        {
            if (visible)
            {
                PasswordVisible.Text = PasswordHidden.Password;
                if (_userViewModel is not null)
                {
                    _userViewModel.EditPassword = PasswordHidden.Password;
                }

                PasswordHidden.Visibility = Visibility.Collapsed;
                PasswordVisible.Visibility = Visibility.Visible;
                PasswordEyeButton.ToolTip = "隐藏密码";
                PasswordEyeButton.Foreground = (Brush)FindResource("AccentBrush");
                PasswordVisible.Focus();
                PasswordVisible.CaretIndex = PasswordVisible.Text.Length;
            }
            else
            {
                PasswordHidden.Password = PasswordVisible.Text;
                if (_userViewModel is not null)
                {
                    _userViewModel.EditPassword = PasswordVisible.Text;
                }

                PasswordVisible.Visibility = Visibility.Collapsed;
                PasswordHidden.Visibility = Visibility.Visible;
                PasswordEyeButton.ToolTip = "显示密码";
                PasswordEyeButton.Foreground = (Brush)FindResource("TextSecondaryBrush");
                PasswordHidden.Focus();
            }
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    private void OnToggleEditorClick(object sender, RoutedEventArgs e)
    {
        if (EditorColumn.Width.Value > 1)
        {
            CollapseEditor();
        }
        else
        {
            ExpandEditor();
        }

        UpdateToggleText();
    }

    private void OnNewUserClick(object sender, RoutedEventArgs e) => ExpandEditor();

    private void CollapseEditor()
    {
        if (EditorColumn.Width.Value > 1)
        {
            _lastEditorWidth = EditorColumn.Width;
        }

        EditorColumn.Width = new GridLength(0);
        EditorPanel.Visibility = Visibility.Collapsed;
    }

    private void ExpandEditor()
    {
        if (EditorColumn.Width.Value <= 1)
        {
            EditorColumn.Width = _lastEditorWidth.Value > 1
                ? _lastEditorWidth
                : new GridLength(370);
        }

        EditorPanel.Visibility = Visibility.Visible;
        UpdateToggleText();
    }

    /// <summary>宽度变为 0 时（包括拖动分隔条到边界）同步收起面板；展开时同步显示。</summary>
    private void OnEditorPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var target = EditorColumn.Width.Value > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (EditorPanel.Visibility != target)
        {
            EditorPanel.Visibility = target;
        }

        UpdateToggleText();
    }

    private void OnEditorLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        => EditorAutoSave.OnLostKeyboardFocus(sender, e);

    private void UpdateToggleText()
    {
        if (ToggleEditorButton is null)
        {
            return;
        }

        ToggleEditorButton.Content = EditorColumn.Width.Value > 1 ? "收起编辑" : "展开编辑";
    }
}
