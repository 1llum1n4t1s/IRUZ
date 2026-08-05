using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IRUZ.Services;

namespace IRUZ.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。マウスジグルの開始/停止と間隔を管理する。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// MainWindowViewModel のコンストラクタ。
    /// </summary>
    public MainWindowViewModel()
    {
        // 更新でインストール先が変わっている場合に備えて登録内容を追従させる
        StartupRegistration.RefreshIfEnabled();
        // 通知を発生させずに現在の登録状態を反映する（レジストリへの書き戻しを避ける）
        _isStartupEnabled = StartupRegistration.IsEnabled();

        // 起動時にデフォルト値でスタートさせる
        StartJiggle();
    }

    private System.Timers.Timer? _timer;
    private bool _suppressStartupUpdate;
    private int _consecutiveJiggleFailures;

    /// <summary>
    /// ジグル間隔の選択肢（秒）。
    /// </summary>
    public ObservableCollection<int> IntervalOptions { get; } = [30, 60, 120, 300];

    /// <summary>
    /// タイマーへ渡せる間隔の下限（秒）。
    /// </summary>
    private const int MinIntervalSeconds = 1;

    /// <summary>
    /// タイマーへ渡せる間隔の上限（秒）。System.Timers.Timer の上限を超えないようにする。
    /// </summary>
    private const int MaxIntervalSeconds = 86400;

    /// <summary>
    /// 間隔の既定値（秒）。
    /// </summary>
    private const int DefaultIntervalSeconds = 60;

    [ObservableProperty]
    private int _selectedIntervalSeconds = DefaultIntervalSeconds;

    /// <summary>
    /// 実際にタイマーへ渡す間隔（秒）。範囲外の値は既定値へ丸め、Timer 生成の例外を防ぐ。
    /// </summary>
    private int EffectiveIntervalSeconds =>
        SelectedIntervalSeconds is >= MinIntervalSeconds and <= MaxIntervalSeconds
            ? SelectedIntervalSeconds
            : DefaultIntervalSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "停止中";

    [ObservableProperty]
    private bool _isStartupEnabled;

    /// <summary>
    /// チェックボックスの操作に合わせてスタートアップ登録を更新する。
    /// </summary>
    partial void OnIsStartupEnabledChanged(bool value)
    {
        if (_suppressStartupUpdate)
            return;

        if (StartupRegistration.SetEnabled(value))
            return;

        // 失敗した場合はチェック状態を実際の登録状態へ戻す
        _suppressStartupUpdate = true;
        IsStartupEnabled = StartupRegistration.IsEnabled();
        _suppressStartupUpdate = false;
        StatusText = "スタートアップ登録の変更に失敗しました";
    }

    /// <summary>
    /// トグルボタンに表示する文言（開始/停止）。
    /// </summary>
    public string ToggleButtonText => IsRunning ? "停止" : "開始";

    /// <summary>
    /// タイトルバーに表示するアプリ名とバージョン。
    /// </summary>
    public string AppVersion =>
        typeof(MainWindowViewModel).Assembly.GetName().Version is { } v
            ? $"IRUZ v{v.Major}.{v.Minor}.{v.Build}"
            : "IRUZ";

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

    /// <summary>
    /// ジグル動作中に表示する通常の状態文言。
    /// </summary>
    private string RunningStatusText => $"ジグル中（{EffectiveIntervalSeconds}秒ごと）";

    private void StartJiggle()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _consecutiveJiggleFailures = 0;
        _timer = new System.Timers.Timer(EffectiveIntervalSeconds * 1000.0);
        _timer.Elapsed += (_, _) => OnJiggleElapsed();
        _timer.Start();
        IsRunning = true;
        StatusText = RunningStatusText;
    }

    /// <summary>
    /// タイマー発火ごとにジグルし、SendInput の失敗を状態表示へ反映する。
    /// 画面ロック中などの失敗は解除で復帰するため、失敗を理由に停止はしない。
    /// </summary>
    private void OnJiggleElapsed()
    {
        var succeeded = MouseJiggleHelper.Jiggle();

        // Elapsed は別スレッドで発火するため、UI に触れる更新は UI スレッドへ回す
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsRunning)
                return;

            if (succeeded)
            {
                if (_consecutiveJiggleFailures > 0)
                {
                    _consecutiveJiggleFailures = 0;
                    StatusText = RunningStatusText;
                }
                return;
            }

            _consecutiveJiggleFailures++;
            StatusText = $"ジグル失敗（{_consecutiveJiggleFailures}回連続・画面ロック中の可能性）";
        });
    }

    private void StopJiggle()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _consecutiveJiggleFailures = 0;
        IsRunning = false;
        StatusText = "停止中";
    }

    /// <summary>
    /// リソースを解放する。
    /// </summary>
    public void Dispose()
    {
        StopJiggle();
    }
}
