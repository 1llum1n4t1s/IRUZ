using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using IRUZ.ViewModels;
using IRUZ.Views;

namespace IRUZ
{
    public partial class App : Application
    {
        private TrayIcon? _trayIcon;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();
                var mainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
                desktop.MainWindow = mainWindow;
                SetupTrayIcon(desktop, mainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// タスクトレイアイコンを用意し、最小化時にトレイへ入るようにする。
        /// </summary>
        private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow)
        {
            WindowIcon trayIconImage;
            using (var iconStream = AssetLoader.Open(new Uri("avares://IRUZ/icon/app.ico")))
            {
                var memoryStream = new MemoryStream();
                iconStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                trayIconImage = new WindowIcon(memoryStream);
            }

            _trayIcon = new TrayIcon
            {
                Icon = trayIconImage,
                ToolTipText = "IRUZ",
                IsVisible = false,
            };

            var showItem = new NativeMenuItem("表示");
            showItem.Click += (_, _) => RestoreFromTray(mainWindow);
            var exitItem = new NativeMenuItem("終了");
            exitItem.Click += (_, _) => desktop.Shutdown();
            _trayIcon.Menu = new NativeMenu();
            _trayIcon.Menu.Items.Add(showItem);
            _trayIcon.Menu.Items.Add(exitItem);

            _trayIcon.Clicked += (_, _) => RestoreFromTray(mainWindow);

            var trayIcons = new TrayIcons { _trayIcon };
            TrayIcon.SetIcons(this, trayIcons);

            mainWindow.GetObservable(Window.WindowStateProperty).Subscribe(new WindowStateObserver(state =>
            {
                if (state == WindowState.Minimized)
                {
                    mainWindow.ShowInTaskbar = false;
                    mainWindow.Hide();
                    if (_trayIcon != null)
                        _trayIcon.IsVisible = true;
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
        /// トレイからウィンドウを復帰表示する。
        /// </summary>
        private void RestoreFromTray(MainWindow mainWindow)
        {
            if (_trayIcon != null)
                _trayIcon.IsVisible = false;
            mainWindow.ShowInTaskbar = true;
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Show();
            mainWindow.Activate();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}