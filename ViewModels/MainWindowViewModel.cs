using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IRUZ.Services;

namespace IRUZ.ViewModels;

    /// <summary>
    /// メインウィンドウの ViewModel。マウスジグルの開始/停止と間隔を管理する。
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        /// <summary>
        /// MainWindowViewModel のコンストラクタ。
        /// </summary>
        public MainWindowViewModel()
        {
            // 起動時にデフォルト値でスタートさせる
            StartJiggle();
        }

        private System.Timers.Timer? _timer;

    /// <summary>
    /// ジグル間隔の選択肢（秒）。
    /// </summary>
    public ObservableCollection<int> IntervalOptions { get; } = [30, 60, 120, 300];

    [ObservableProperty]
    private int _selectedIntervalSeconds = 60;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "停止中";

    /// <summary>
    /// トグルボタンに表示する文言（開始/停止）。
    /// </summary>
    public string ToggleButtonText => IsRunning ? "停止" : "開始";

    /// <summary>
    /// ジグルを開始または停止する。
    /// </summary>
    [RelayCommand]
    private void Toggle()
    {
        if (IsRunning)
            StopJiggle();
        else
            StartJiggle();
    }

    private void StartJiggle()
    {
        _timer?.Stop();
        _timer = new System.Timers.Timer(SelectedIntervalSeconds * 1000.0);
        _timer.Elapsed += (_, _) => MouseJiggleHelper.Jiggle();
        _timer.Start();
        IsRunning = true;
        StatusText = $"ジグル中（{SelectedIntervalSeconds}秒ごと）";
    }

    private void StopJiggle()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        IsRunning = false;
        StatusText = "停止中";
    }
}
