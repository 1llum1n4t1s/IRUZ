using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using IRUZ.Services;
using IRUZ.ViewModels;
using IRUZ.Views;

namespace IRUZ;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private TrayMenuController? _trayMenu;
    private IDisposable? _windowStateSubscription;

    /// <summary>
    /// 明示的な終了操作（「終了」ボタン / トレイの「終了」/ OS のシャットダウン要求）が始まっているか。
    /// これが false のあいだ、ウィンドウを閉じる操作はトレイ格納として扱う。
    /// </summary>
    private bool _isExiting;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            // ウィンドウが表示される前に最小化することを指示
            mainWindow.SetStartMinimized();
            desktop.MainWindow = mainWindow;

            // トレイ常駐アプリなので、ウィンドウが閉じただけでは終了しない。
            // 終了するのは「終了」操作と OS のシャットダウン要求だけ。
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (_, _) => _isExiting = true;
            mainWindow.Closing += (_, e) =>
            {
                if (_isExiting)
                    return;

                // ✕ は終了ではなくトレイ格納として扱う（ジグルは動き続ける）
                e.Cancel = true;
                mainWindow.WindowState = WindowState.Minimized;
            };

            // ウィンドウの「最小化」ボタンから最小化（＝トレイ格納）へつなぐ
            viewModel.MinimizeRequested += (_, _) => mainWindow.WindowState = WindowState.Minimized;
            // ウィンドウの「終了」ボタンからアプリ終了へつなぐ
            viewModel.ExitRequested += (_, _) => ExitApplication(desktop);
            // 終了時にタイマーを止める（トレイの「終了」が呼ぶ Shutdown() でも Exit は発火する）
            desktop.Exit += (_, _) =>
            {
                _trayMenu?.Dispose();
                viewModel.Dispose();
            };
            SetupTrayIcon(desktop, mainWindow, viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// タスクトレイアイコンを用意する。アイコンはウィンドウの表示状態にかかわらず常駐させ、
    /// 右クリックメニューからメインウィンドウと同等の操作ができるようにする。
    /// </summary>
    private void SetupTrayIcon(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow,
        MainWindowViewModel viewModel)
    {
        WindowIcon trayIconImage;
        using (var iconStream = AssetLoader.Open(new Uri("avares://IRUZ/icon/app.ico")))
        {
            using var memoryStream = new MemoryStream();
            iconStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            trayIconImage = new WindowIcon(memoryStream);
        }

        _trayIcon = new TrayIcon
        {
            Icon = trayIconImage,
            ToolTipText = "IRUZ",
            IsVisible = true,
        };

        _trayMenu = new TrayMenuController(
            viewModel,
            () => RestoreFromTray(mainWindow),
            () => ExitApplication(desktop),
            toolTip =>
            {
                if (_trayIcon is { } icon)
                    icon.ToolTipText = toolTip;
            });
        _trayIcon.Menu = _trayMenu.Menu;
        _trayIcon.ToolTipText = _trayMenu.ToolTipText;

        _trayIcon.Clicked += (_, _) => RestoreFromTray(mainWindow);

        var trayIcons = new TrayIcons { _trayIcon };
        TrayIcon.SetIcons(this, trayIcons);

        // 二重起動時の復帰は Loaded 後にだけ有効化する。
        // Loaded より前にアクションを登録すると、そこへ届いた復帰要求が Normal 優先度で先に走り、
        // 後から動く Loaded（DispatcherPriority.Loaded は Normal より低い）の起動時最小化に
        // 打ち消されてウィンドウが出ない。Loaded 前の要求は PendingRestore 経由でここが拾う。
        // このハンドラは MainWindow のコンストラクタが登録した起動時最小化の後に実行される。
        mainWindow.Loaded += (_, _) =>
        {
            Program.RestoreFromTray = () => RestoreFromTray(mainWindow);

            if (Program.PendingRestore)
            {
                Program.PendingRestore = false;
                RestoreFromTray(mainWindow);
            }
        };

        _windowStateSubscription = mainWindow.GetObservable(Window.WindowStateProperty).Subscribe(new WindowStateObserver(state =>
        {
            if (state == WindowState.Minimized)
            {
                mainWindow.ShowInTaskbar = false;
                mainWindow.Hide();
            }
        }));
    }

    /// <summary>
    /// WindowState の変更を Action で受け取る IObserver 実装。
    /// </summary>
    private sealed class WindowStateObserver : IObserver<WindowState>
    {
        private readonly Action<WindowState> _onNext;

        internal WindowStateObserver(Action<WindowState> onNext) => _onNext = onNext;

        public void OnNext(WindowState value) => _onNext(value);

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }

    /// <summary>
    /// アプリを終了する。Closing のキャンセル（トレイ格納）を解除してから Shutdown する。
    /// </summary>
    /// <param name="desktop">デスクトップ用のアプリケーションライフタイム。</param>
    private void ExitApplication(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _isExiting = true;
        desktop.Shutdown();
    }

    /// <summary>
    /// トレイからウィンドウを復帰表示する。トレイアイコンは常駐させたままにする。
    /// </summary>
    private static void RestoreFromTray(MainWindow mainWindow)
    {
        mainWindow.ShowInTaskbar = true;
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Show();
        mainWindow.Activate();
    }
}
