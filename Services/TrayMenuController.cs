using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using IRUZ.ViewModels;

namespace IRUZ.Services;

/// <summary>
/// タスクトレイの右クリックメニューを組み立て、ViewModel の状態と同期させる。
/// メインウィンドウと同等の操作（開始/停止・間隔・自動解除・スタートアップ登録）を提供する。
/// </summary>
public sealed class TrayMenuController : IDisposable
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Action<string>? _onToolTipTextChanged;

    private readonly NativeMenuItem _statusItem = new();
    private readonly NativeMenuItem _countdownItem = new();
    private readonly NativeMenuItem _toggleItem = new();
    private readonly NativeMenuItem _intervalItem = new("間隔");
    private readonly NativeMenuItem _autoStopItem = new("自動解除");
    private readonly NativeMenuItem _startupItem = new("スタートアップに登録する");
    private readonly List<(NativeMenuItem Item, int Seconds)> _intervalChoices = [];
    private readonly List<(NativeMenuItem Item, AutoStopOption Option)> _autoStopChoices = [];

    private bool _disposed;

    /// <summary>
    /// トレイメニューを構築し、ViewModel の変更に追従させる。
    /// </summary>
    /// <param name="viewModel">メインウィンドウと共有する ViewModel。</param>
    /// <param name="showWindow">「表示」で呼ぶウィンドウ復帰処理。</param>
    /// <param name="exitApplication">「終了」で呼ぶアプリ終了処理。</param>
    /// <param name="onToolTipTextChanged">トレイアイコンのツールチップ更新を受け取るコールバック。</param>
    public TrayMenuController(
        MainWindowViewModel viewModel,
        Action showWindow,
        Action exitApplication,
        Action<string>? onToolTipTextChanged = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(showWindow);
        ArgumentNullException.ThrowIfNull(exitApplication);

        _viewModel = viewModel;
        _onToolTipTextChanged = onToolTipTextChanged;

        // 現在の状態表示（クリック不可の見出し）
        _statusItem.IsEnabled = false;
        // 自動解除のカウントダウン（予定があるときだけ差し込む）
        _countdownItem.IsEnabled = false;

        _toggleItem.Click += (_, _) => _viewModel.ToggleCommand.Execute(null);

        _intervalItem.Menu = BuildIntervalMenu();
        _autoStopItem.Menu = BuildAutoStopMenu();

        _startupItem.ToggleType = MenuItemToggleType.CheckBox;
        _startupItem.Click += (_, _) => _viewModel.IsStartupEnabled = !_viewModel.IsStartupEnabled;

        var showItem = new NativeMenuItem("表示");
        showItem.Click += (_, _) => showWindow();
        var exitItem = new NativeMenuItem("終了");
        exitItem.Click += (_, _) => exitApplication();

        Menu = new NativeMenu();
        Menu.Items.Add(_statusItem);
        Menu.Items.Add(new NativeMenuItemSeparator());
        Menu.Items.Add(_toggleItem);
        Menu.Items.Add(_intervalItem);
        Menu.Items.Add(_autoStopItem);
        Menu.Items.Add(new NativeMenuItemSeparator());
        Menu.Items.Add(_startupItem);
        Menu.Items.Add(new NativeMenuItemSeparator());
        Menu.Items.Add(showItem);
        Menu.Items.Add(exitItem);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Refresh();
    }

    /// <summary>
    /// TrayIcon へ割り当てるメニュー。
    /// </summary>
    public NativeMenu Menu { get; }

    /// <summary>
    /// トレイアイコンのツールチップ文言。現在の状態を含む。
    /// </summary>
    public string ToolTipText { get; private set; } = "IRUZ";

    private NativeMenu BuildIntervalMenu()
    {
        var menu = new NativeMenu();
        foreach (var seconds in _viewModel.IntervalOptions)
        {
            var captured = seconds;
            var item = new NativeMenuItem($"{captured}秒") { ToggleType = MenuItemToggleType.Radio };
            // 間隔はウィンドウ側と同じく停止中だけ変更できる（適用には開始し直しが要るため）
            item.Click += (_, _) => _viewModel.SelectedIntervalSeconds = captured;
            _intervalChoices.Add((item, captured));
            menu.Items.Add(item);
        }

        return menu;
    }

    private NativeMenu BuildAutoStopMenu()
    {
        var menu = new NativeMenu();
        foreach (var option in _viewModel.AutoStopOptions)
        {
            var captured = option;
            var item = new NativeMenuItem(captured.ToString()) { ToggleType = MenuItemToggleType.Radio };
            item.Click += (_, _) => _viewModel.SelectedAutoStopOption = captured;
            _autoStopChoices.Add((item, captured));
            menu.Items.Add(item);
        }

        return menu;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    /// <summary>
    /// ViewModel の現在値をメニュー表示へ反映する。
    /// </summary>
    private void Refresh()
    {
        _statusItem.Header = _viewModel.StatusText;
        _toggleItem.Header = _viewModel.ToggleButtonText;
        SyncCountdownItem();

        // 間隔はウィンドウの ComboBox と同じく、ジグル中は変更させない
        _intervalItem.IsEnabled = !_viewModel.IsRunning;
        foreach (var (item, seconds) in _intervalChoices)
        {
            item.IsEnabled = !_viewModel.IsRunning;
            item.IsChecked = seconds == _viewModel.SelectedIntervalSeconds;
        }

        foreach (var (item, option) in _autoStopChoices)
            item.IsChecked = option.Equals(_viewModel.SelectedAutoStopOption);

        _startupItem.IsChecked = _viewModel.IsStartupEnabled;

        ToolTipText = _viewModel.HasAutoStop
            ? $"IRUZ - {_viewModel.StatusText} / {_viewModel.AutoStopText}"
            : $"IRUZ - {_viewModel.StatusText}";
        _onToolTipTextChanged?.Invoke(ToolTipText);
    }

    /// <summary>
    /// カウントダウン行を、自動解除の予定があるときだけ状態見出しの直下へ差し込む。
    /// NativeMenuItem に表示/非表示の指定が無いため、項目の出し入れで切り替える。
    /// </summary>
    private void SyncCountdownItem()
    {
        var index = Menu.Items.IndexOf(_countdownItem);
        if (_viewModel.HasAutoStop)
        {
            _countdownItem.Header = _viewModel.AutoStopText;
            if (index < 0)
                Menu.Items.Insert(1, _countdownItem);
        }
        else if (index >= 0)
        {
            Menu.Items.RemoveAt(index);
        }
    }

    /// <summary>
    /// ViewModel の購読を解除する。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
