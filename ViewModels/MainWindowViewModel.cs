using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IRUZ.Services;

namespace IRUZ.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。マウスジグルの開始/停止と間隔、自動解除タイマーを管理する。
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

    /// <summary>
    /// 入力送信の可否を開始/停止と排他するためのロック。
    /// System.Timers.Timer は Stop/Dispose 済みでも進行中の Elapsed を止めないため、
    /// 停止後に SendInput が漏れて離席判定がリセットされるのを防ぐ。
    /// </summary>
    private readonly object _jiggleGate = new();

    /// <summary>
    /// 入力送信を許可している状態か（<see cref="_jiggleGate"/> 配下）。
    /// UI スレッド専用の <see cref="IsRunning"/> とは別に、タイマースレッドから安全に見るために持つ。
    /// </summary>
    private bool _jiggleEnabled;
    private System.Timers.Timer? _countdownTimer;
    private bool _suppressStartupUpdate;
    private int _consecutiveJiggleFailures;

    /// <summary>
    /// ジグル間隔の選択肢（秒）。
    /// </summary>
    public ObservableCollection<int> IntervalOptions { get; } = [30, 60, 120, 300];

    /// <summary>
    /// 自動解除までの時間の選択肢。
    /// </summary>
    public ObservableCollection<AutoStopOption> AutoStopOptions { get; } =
        [AutoStopOption.None, new(1), new(2), new(3), new(4), new(5), new(6), new(8)];

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

    /// <summary>
    /// 自動解除までに指定できる時間の上限（時）。DateTimeOffset の加算が破綻する値を防ぐ。
    /// </summary>
    private const int MaxAutoStopHours = 24;

    /// <summary>
    /// 残り時間表示と自動解除判定を行う周期（ミリ秒）。
    /// </summary>
    private const double CountdownIntervalMilliseconds = 1000;

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
    private AutoStopOption _selectedAutoStopOption = AutoStopOption.None;

    /// <summary>
    /// 実際に使う自動解除までの時間（時）。0 なら自動解除しない。
    /// </summary>
    private int EffectiveAutoStopHours =>
        SelectedAutoStopOption is { Hours: > 0 } option ? Math.Min(option.Hours, MaxAutoStopHours) : 0;

    private DateTimeOffset? _autoStopAt;

    /// <summary>
    /// 自動解除が発動する予定時刻。自動解除しない設定または停止中は null。
    /// </summary>
    public DateTimeOffset? AutoStopAt
    {
        get => _autoStopAt;
        private set
        {
            if (SetProperty(ref _autoStopAt, value))
                OnPropertyChanged(nameof(HasAutoStop));
        }
    }

    /// <summary>
    /// 自動解除の予定が立っているか。カウントダウン表示の出し分けに使う。
    /// </summary>
    public bool HasAutoStop => AutoStopAt is not null;

    private string _autoStopText = string.Empty;

    /// <summary>
    /// 自動解除までの残り時間と解除予定時刻。予定が無いときは空文字。
    /// </summary>
    public string AutoStopText
    {
        get => _autoStopText;
        private set => SetProperty(ref _autoStopText, value);
    }

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
    /// 自動解除の設定変更を即座に反映する。
    /// ジグル中でも停止させずに再設定できるよう、変更時点から数え直す。
    /// </summary>
    partial void OnSelectedAutoStopOptionChanged(AutoStopOption value)
    {
        // ComboBox の選択解除などで null が入っても「なし」として扱う
        if (value is null)
        {
            SelectedAutoStopOption = AutoStopOption.None;
            return;
        }

        ArmAutoStop(DateTimeOffset.Now);
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
    /// 「最小化」が要求されたときに発生する。実際のウィンドウ操作は App 側が行う。
    /// </summary>
    public event EventHandler? MinimizeRequested;

    /// <summary>
    /// ウィンドウの最小化（タスクトレイへの格納）を要求する。ジグルは動き続ける。
    /// </summary>
    [RelayCommand]
    private void Minimize() => MinimizeRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 「終了」が要求されたときに発生する。実際のアプリ終了は App 側が行う。
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// アプリの終了を要求する（ジグルの停止ではなく常駐そのものの終了）。
    /// </summary>
    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// ジグル動作中に表示する通常の状態文言。
    /// </summary>
    private string RunningStatusText => $"ジグル中（{EffectiveIntervalSeconds}秒ごと）";

    /// <summary>
    /// 自動解除のカウントダウン文言を組み立てる。残り時間と解除予定の時刻を併記する。
    /// </summary>
    /// <param name="deadline">自動解除の予定時刻。</param>
    /// <param name="now">残り時間の計算に使う基準時刻。</param>
    /// <returns>「自動解除まで 2:59:31（18:00 に解除）」形式の文言。</returns>
    private static string BuildAutoStopText(DateTimeOffset deadline, DateTimeOffset now) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"自動解除まで {FormatRemaining(deadline - now)}（{deadline:HH:mm} に解除）");

    /// <summary>
    /// 残り時間を h:mm:ss へ整形する。秒未満は切り上げ、開始直後に指定時間ちょうどを表示する。
    /// </summary>
    private static string FormatRemaining(TimeSpan remaining)
    {
        var totalSeconds = remaining > TimeSpan.Zero ? (long)Math.Ceiling(remaining.TotalSeconds) : 0L;
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{seconds:00}");
    }

    private void StartJiggle()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _consecutiveJiggleFailures = 0;
        lock (_jiggleGate)
            _jiggleEnabled = true;
        _timer = new System.Timers.Timer(EffectiveIntervalSeconds * 1000.0);
        _timer.Elapsed += (_, _) => OnJiggleElapsed();
        _timer.Start();
        IsRunning = true;
        ArmAutoStop(DateTimeOffset.Now);
        StatusText = RunningStatusText;
    }

    /// <summary>
    /// 現在の設定に合わせて自動解除の予定時刻とカウントダウンタイマーを張り直す。
    /// ジグル停止中や「なし」のときは解除する。
    /// </summary>
    /// <param name="now">予定時刻の起点となる時刻。</param>
    private void ArmAutoStop(DateTimeOffset now)
    {
        var hours = EffectiveAutoStopHours;
        if (!IsRunning || hours <= 0)
        {
            DisarmAutoStop();
            return;
        }

        var deadline = now.AddHours(hours);
        AutoStopAt = deadline;
        AutoStopText = BuildAutoStopText(deadline, now);

        _countdownTimer?.Stop();
        _countdownTimer?.Dispose();
        _countdownTimer = new System.Timers.Timer(CountdownIntervalMilliseconds);
        // Elapsed は別スレッドで発火するため、UI に触れる更新は UI スレッドへ回す
        _countdownTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(() => UpdateAutoStop(DateTimeOffset.Now));
        _countdownTimer.Start();
    }

    /// <summary>
    /// 自動解除の予定を取り消し、カウントダウンタイマーを破棄する。
    /// </summary>
    private void DisarmAutoStop()
    {
        _countdownTimer?.Stop();
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        AutoStopAt = null;
        AutoStopText = string.Empty;
    }

    /// <summary>
    /// 指定時刻を基準に残り時間表示を更新し、予定時刻へ達していればジグルを自動解除（停止）する。
    /// 予定時刻は絶対時刻で保持しているため、スリープなどでタイマー発火が遅れても取りこぼさない。
    /// </summary>
    /// <param name="now">判定の基準時刻。</param>
    public void UpdateAutoStop(DateTimeOffset now)
    {
        if (!IsRunning || AutoStopAt is not { } deadline)
            return;

        if (now >= deadline)
        {
            var hours = EffectiveAutoStopHours;
            StopJiggle();
            StatusText = $"自動解除しました（{hours}時間経過）";
            return;
        }

        // カウントダウンは StatusText とは別行なので、ジグル失敗の警告表示を消さない
        AutoStopText = BuildAutoStopText(deadline, now);
    }

    /// <summary>
    /// 動作中のときだけマウス入力を送る。開始/停止と同じロックの中で判定するため、
    /// 停止直後に発火した進行中のタイマーから入力が漏れることがない。
    /// </summary>
    /// <returns>送信した場合はその成否。停止済みで送信しなかった場合は null。</returns>
    public bool? JiggleIfRunning()
    {
        lock (_jiggleGate)
        {
            if (!_jiggleEnabled)
                return null;

            return MouseJiggleHelper.Jiggle();
        }
    }

    /// <summary>
    /// タイマー発火ごとにジグルし、SendInput の失敗を状態表示へ反映する。
    /// 画面ロック中などの失敗は解除で復帰するため、失敗を理由に停止はしない。
    /// </summary>
    private void OnJiggleElapsed()
    {
        if (JiggleIfRunning() is not { } succeeded)
            return; // 停止済み。進行中の Elapsed だったので何も送らず何も表示しない

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
        // タイマー破棄より先に入力を止める（進行中の Elapsed をここでブロックして弾く）
        lock (_jiggleGate)
            _jiggleEnabled = false;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _consecutiveJiggleFailures = 0;
        IsRunning = false;
        DisarmAutoStop();
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
