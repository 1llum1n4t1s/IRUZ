using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using IRUZ.Services;
using IRUZ.ViewModels;
using Xunit;

namespace IRUZ.Tests;

/// <summary>
/// 🌱 正常系テスト（Happy Path）。
/// 典型的な使用シナリオで期待どおり動作することを確認する。
/// </summary>
[Collection(IruzTestCollection.Name)]
public class HappyPathTests
{
    // ─────────────────────────────────────────────
    // MainWindowViewModel: 初期状態と開始・停止
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath ジグル間隔の選択肢は仕様どおり 30/60/120/300 秒の4件で固定されている。
    /// </summary>
    [Fact]
    public void 既定状態で構築したときIntervalOptionsが30と60と120と300であること()
    {
        using var vm = new MainWindowViewModel();

        Assert.Equal(new[] { 30, 60, 120, 300 }, vm.IntervalOptions);
    }

    /// <summary>
    /// @happypath SelectedIntervalSeconds の初期値は既定値 60 秒。
    /// </summary>
    [Fact]
    public void 構築直後は既定間隔60秒が選択されていること()
    {
        using var vm = new MainWindowViewModel();

        Assert.Equal(60, vm.SelectedIntervalSeconds);
    }

    /// <summary>
    /// @happypath コンストラクタが StartJiggle を呼ぶため、開始状態で始まる。
    /// </summary>
    [Fact]
    public void 構築直後はジグルが自動開始され状態文言が60秒ごとになること()
    {
        using var vm = new MainWindowViewModel();

        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
        Assert.Equal("停止", vm.ToggleButtonText);
    }

    /// <summary>
    /// @happypath 実行中の Toggle は停止経路に入る。
    /// </summary>
    [Fact]
    public void 動作中にToggleを実行したとき停止して停止中と表示されること()
    {
        using var vm = new MainWindowViewModel();

        vm.ToggleCommand.Execute(null);

        Assert.False(vm.IsRunning);
        Assert.Equal("停止中", vm.StatusText);
        Assert.Equal("開始", vm.ToggleButtonText);
    }

    /// <summary>
    /// @happypath 停止 → 再開の往復で初期状態と同じ観測値へ戻る。
    /// </summary>
    [Fact]
    public void 停止中にToggleを実行したとき再開してジグル中と表示されること()
    {
        using var vm = new MainWindowViewModel();
        vm.ToggleCommand.Execute(null);

        vm.ToggleCommand.Execute(null);

        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
        Assert.Equal("停止", vm.ToggleButtonText);
    }

    /// <summary>
    /// @happypath IntervalOptions の各値を選んで再開すると StatusText に反映される。
    /// </summary>
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(300)]
    public void 選択肢の間隔へ変更して再開したとき状態文言にその秒数が出ること(int seconds)
    {
        using var vm = new MainWindowViewModel();

        vm.SelectedIntervalSeconds = seconds;
        vm.ToggleCommand.Execute(null); // 停止
        vm.ToggleCommand.Execute(null); // 再開

        Assert.Equal(seconds, vm.SelectedIntervalSeconds);
        Assert.True(vm.IsRunning);
        Assert.Equal($"ジグル中（{seconds}秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @happypath CanExecute の条件を持たないため常に実行可能。
    /// </summary>
    [Fact]
    public void ToggleCommandが開始中でも停止中でも実行可能であること()
    {
        using var vm = new MainWindowViewModel();

        Assert.True(vm.ToggleCommand.CanExecute(null));
        vm.ToggleCommand.Execute(null);
        Assert.True(vm.ToggleCommand.CanExecute(null));
    }

    /// <summary>
    /// @happypath Dispose は停止と同じ観測結果になる。
    /// </summary>
    [Fact]
    public void Disposeしたときジグルが停止して停止中になること()
    {
        var vm = new MainWindowViewModel();

        vm.Dispose();

        Assert.False(vm.IsRunning);
        Assert.Equal("停止中", vm.StatusText);
        Assert.Equal("開始", vm.ToggleButtonText);
    }

    /// <summary>
    /// @happypath 静的状態を共有しないことを2インスタンスの独立操作で確認する。
    /// </summary>
    [Fact]
    public void ViewModelを複数生成したときそれぞれ独立に開始と停止ができること()
    {
        using var first = new MainWindowViewModel();
        using var second = new MainWindowViewModel();

        first.ToggleCommand.Execute(null);

        Assert.False(first.IsRunning);
        Assert.True(second.IsRunning);
        Assert.Equal("停止中", first.StatusText);
        Assert.Equal("ジグル中（60秒ごと）", second.StatusText);
    }

    /// <summary>
    /// @happypath 変更通知と Dispose の公開契約を型レベルで固定する。
    /// </summary>
    [Fact]
    public void MainWindowViewModelがIDisposableとINotifyPropertyChangedを実装していること()
    {
        using var vm = new MainWindowViewModel();

        Assert.IsAssignableFrom<IDisposable>(vm);
        Assert.IsAssignableFrom<INotifyPropertyChanged>(vm);
        Assert.IsAssignableFrom<ViewModelBase>(vm);
    }

    // ─────────────────────────────────────────────
    // MainWindowViewModel: 変更通知
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath NotifyPropertyChangedFor により IsRunning と ToggleButtonText が両方発火する。
    /// </summary>
    [Fact]
    public void IsRunningが変化したときToggleButtonTextの変更通知も発生すること()
    {
        using var vm = new MainWindowViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ToggleCommand.Execute(null);

        Assert.Contains(nameof(MainWindowViewModel.IsRunning), changed);
        Assert.Contains(nameof(MainWindowViewModel.ToggleButtonText), changed);
    }

    /// <summary>
    /// @happypath ObservableProperty による PropertyChanged が発火する。
    /// </summary>
    [Fact]
    public void SelectedIntervalSecondsを変更したとき変更通知が発生すること()
    {
        using var vm = new MainWindowViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SelectedIntervalSeconds = 120;

        Assert.Contains(nameof(MainWindowViewModel.SelectedIntervalSeconds), changed);
    }

    /// <summary>
    /// @happypath 停止操作に伴う StatusText の PropertyChanged が観測できる。
    /// </summary>
    [Fact]
    public void StatusTextが更新されたとき変更通知が発生すること()
    {
        using var vm = new MainWindowViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ToggleCommand.Execute(null);

        Assert.Contains(nameof(MainWindowViewModel.StatusText), changed);
        Assert.Equal("停止中", vm.StatusText);
    }

    // ─────────────────────────────────────────────
    // MainWindowViewModel: AppVersion
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath 表示バージョンはアセンブリバージョンから導かれる。
    /// </summary>
    [Fact]
    public void AppVersionが実行アセンブリのバージョンと一致すること()
    {
        using var vm = new MainWindowViewModel();
        var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
        var expected = version is null ? "IRUZ" : $"IRUZ v{version.Major}.{version.Minor}.{version.Build}";

        Assert.Equal(expected, vm.AppVersion);
    }

    /// <summary>
    /// @happypath リビジョンを含まない "IRUZ vX.Y.Z" 形式であること。
    /// </summary>
    [Fact]
    public void AppVersionがIRUZ接頭辞のメジャーマイナーパッチ形式であること()
    {
        using var vm = new MainWindowViewModel();

        Assert.Matches(@"^IRUZ v\d+\.\d+\.\d+$", vm.AppVersion);
    }

    // ─────────────────────────────────────────────
    // StartupRegistration
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath 未登録状態では false を返す。
    /// </summary>
    [Fact]
    public void スタートアップ未登録のときIsEnabledがfalseを返すこと()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();

        Assert.False(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @happypath 登録の書き込みが成功し、読み出しでも観測できる。
    /// </summary>
    [Fact]
    public void SetEnabledでtrueにしたときIsEnabledがtrueを返すこと()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();

        var result = StartupRegistration.SetEnabled(true);

        Assert.True(result);
        Assert.True(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @happypath 登録 → 解除の往復で状態が戻る。
    /// </summary>
    [Fact]
    public void 登録済みからSetEnabledでfalseにしたときIsEnabledがfalseに戻ること()
    {
        using var guard = new RunValueGuard();
        Assert.True(StartupRegistration.SetEnabled(true));

        var result = StartupRegistration.SetEnabled(false);

        Assert.True(result);
        Assert.False(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @happypath 読み取り専用であることを、値の一致と生値の不変で確認する。
    /// </summary>
    [Fact]
    public void IsEnabledを複数回呼んでも同じ結果を返し副作用がないこと()
    {
        using var guard = new RunValueGuard();
        StartupRegistration.SetEnabled(true);
        var before = RunValueGuard.Read();

        var first = StartupRegistration.IsEnabled();
        var second = StartupRegistration.IsEnabled();

        Assert.True(first);
        Assert.Equal(first, second);
        Assert.Equal(before, RunValueGuard.Read());
    }

    // ─────────────────────────────────────────────
    // MainWindowViewModel: スタートアップ登録の連携
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath コンストラクタは通知を出さずに IsEnabled() を反映する。
    /// </summary>
    [Fact]
    public void 構築したViewModelのIsStartupEnabledが実際の登録状態と一致すること()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();

        using var vm = new MainWindowViewModel();

        Assert.False(vm.IsStartupEnabled);
        Assert.Equal(StartupRegistration.IsEnabled(), vm.IsStartupEnabled);
    }

    /// <summary>
    /// @happypath チェック操作がレジストリ登録へ伝わり、状態文言はエラーへ変わらない。
    /// </summary>
    [Fact]
    public void IsStartupEnabledをtrueにしたときスタートアップへ登録されること()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();
        using var vm = new MainWindowViewModel();

        vm.IsStartupEnabled = true;

        Assert.True(vm.IsStartupEnabled);
        Assert.True(StartupRegistration.IsEnabled());
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @happypath チェックの往復でレジストリ状態も往復する。
    /// </summary>
    [Fact]
    public void IsStartupEnabledをtrueからfalseに戻したとき登録が解除されること()
    {
        using var guard = new RunValueGuard();
        using var vm = new MainWindowViewModel();
        vm.IsStartupEnabled = true;

        vm.IsStartupEnabled = false;

        Assert.False(vm.IsStartupEnabled);
        Assert.False(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @happypath チェックボックスのバインドに必要な PropertyChanged が飛ぶ。
    /// </summary>
    [Fact]
    public void IsStartupEnabledを変更したとき変更通知が発生すること()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();
        using var vm = new MainWindowViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsStartupEnabled = true;

        Assert.Contains(nameof(MainWindowViewModel.IsStartupEnabled), changed);
    }

    // ─────────────────────────────────────────────
    // MouseJiggleHelper
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath SendInput の戻り値は環境依存のため、例外を投げない契約のみ固定する。
    /// </summary>
    [Fact]
    public void Jiggleを呼んだとき例外を投げずに完了すること()
    {
        var exception = Record.Exception(() => MouseJiggleHelper.Jiggle());

        Assert.Null(exception);
    }

    /// <summary>
    /// @happypath +1px と -1px を1回で送るため、呼び出し前後でカーソル座標が変わらない。
    /// </summary>
    [Fact]
    public void Jiggleを呼んでもカーソル位置が変わらないこと()
    {
        if (!CursorPositionProbe.TryGet(out var before))
            return; // カーソル位置を取得できない環境では検証をスキップする

        MouseJiggleHelper.Jiggle();

        Assert.True(CursorPositionProbe.TryGet(out var after));
        Assert.Equal(before.X, after.X);
        Assert.Equal(before.Y, after.Y);
    }

    // ─────────────────────────────────────────────
    // 自動解除タイマー
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath 自動解除の選択肢は「なし」＋ 1/2/3/4/5/6/8 時間後の8件。
    /// </summary>
    [Fact]
    public void 既定状態で構築したときAutoStopOptionsがなしと1から8時間後であること()
    {
        using var vm = new MainWindowViewModel();

        Assert.Equal(
            new[] { "なし", "1時間後", "2時間後", "3時間後", "4時間後", "5時間後", "6時間後", "8時間後" },
            vm.AutoStopOptions.Select(o => o.ToString()));
    }

    /// <summary>
    /// @happypath 既定は自動解除なし。従来どおり止めるまでジグルし続ける。
    /// </summary>
    [Fact]
    public void 構築直後は自動解除がなしで予定時刻が設定されないこと()
    {
        using var vm = new MainWindowViewModel();

        Assert.Equal(AutoStopOption.None, vm.SelectedAutoStopOption);
        Assert.Null(vm.AutoStopAt);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @happypath 15時に「3時間後」を選ぶと、18時ごろの解除予定が立つ（想定シナリオそのもの）。
    /// </summary>
    [Fact]
    public void ジグル中に3時間後を選ぶとおよそ3時間後の解除予定になること()
    {
        using var vm = new MainWindowViewModel();
        var before = DateTimeOffset.Now;

        vm.SelectedAutoStopOption = new AutoStopOption(3);

        var after = DateTimeOffset.Now;
        Assert.NotNull(vm.AutoStopAt);
        Assert.InRange(vm.AutoStopAt!.Value, before.AddHours(3), after.AddHours(3));
    }

    /// <summary>
    /// @happypath 自動解除を設定すると、残り時間と解除予定時刻が独立した行に出る。
    /// </summary>
    [Fact]
    public void 自動解除を設定するとAutoStopTextに残り時間と予定時刻が出ること()
    {
        using var vm = new MainWindowViewModel();

        vm.SelectedAutoStopOption = new AutoStopOption(3);

        Assert.True(vm.HasAutoStop);
        Assert.StartsWith("自動解除まで 3:00:00（", vm.AutoStopText);
        Assert.EndsWith(" に解除）", vm.AutoStopText);
        Assert.Contains(vm.AutoStopAt!.Value.ToString("HH:mm", CultureInfo.InvariantCulture), vm.AutoStopText);
        // カウントダウンは別行なので状態表示は簡潔なまま
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @happypath 残り時間は基準時刻に応じて減っていく。
    /// </summary>
    [Fact]
    public void 予定時刻の1分前を基準にすると残り1分と表示されること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);

        vm.UpdateAutoStop(vm.AutoStopAt!.Value.AddMinutes(-1));

        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
        Assert.StartsWith("自動解除まで 0:01:00（", vm.AutoStopText);
        Assert.True(vm.IsRunning);
    }

    /// <summary>
    /// @happypath 予定時刻に達するとジグルが止まり、離席と判定される状態へ戻る。
    /// </summary>
    [Fact]
    public void 予定時刻に達すると自動解除されて停止すること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);

        vm.UpdateAutoStop(vm.AutoStopAt!.Value);

        Assert.False(vm.IsRunning);
        Assert.Null(vm.AutoStopAt);
        Assert.Equal("自動解除しました（3時間経過）", vm.StatusText);
        Assert.Equal("開始", vm.ToggleButtonText);
    }

    /// <summary>
    /// @happypath 自動解除後に再開すると、同じ設定でもう一度カウントし直す。
    /// </summary>
    [Fact]
    public void 自動解除後に再開すると同じ時間で再武装されること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(1);
        vm.UpdateAutoStop(vm.AutoStopAt!.Value);
        Assert.False(vm.IsRunning);

        var before = DateTimeOffset.Now;
        vm.ToggleCommand.Execute(null); // 再開

        Assert.True(vm.IsRunning);
        Assert.NotNull(vm.AutoStopAt);
        Assert.InRange(vm.AutoStopAt!.Value, before.AddHours(1), DateTimeOffset.Now.AddHours(1));
    }

    /// <summary>
    /// @happypath 手動で停止したときも解除予定は破棄され、カウントダウン表示も消える。
    /// </summary>
    [Fact]
    public void 手動停止すると解除予定が破棄されること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);

        vm.ToggleCommand.Execute(null); // 停止

        Assert.Null(vm.AutoStopAt);
        Assert.False(vm.HasAutoStop);
        Assert.Equal(string.Empty, vm.AutoStopText);
        Assert.Equal("停止中", vm.StatusText);
    }

    /// <summary>
    /// @happypath 自動解除なしのときはカウントダウン行を出さない。
    /// </summary>
    [Fact]
    public void 自動解除なしのときはカウントダウンを表示しないこと()
    {
        using var vm = new MainWindowViewModel();

        Assert.False(vm.HasAutoStop);
        Assert.Equal(string.Empty, vm.AutoStopText);
    }

    /// <summary>
    /// @happypath カウントダウンの更新で変更通知が飛び、画面が追従する。
    /// </summary>
    [Fact]
    public void カウントダウン更新でAutoStopTextの変更通知が発生すること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UpdateAutoStop(vm.AutoStopAt!.Value.AddMinutes(-30));

        Assert.Contains(nameof(MainWindowViewModel.AutoStopText), changed);
        Assert.StartsWith("自動解除まで 0:30:00（", vm.AutoStopText);
    }

    // ─────────────────────────────────────────────
    // MainWindowViewModel: 最小化・終了
    // ─────────────────────────────────────────────

    /// <summary>
    /// @happypath 「最小化」ボタンは最小化を要求するだけで、実際の操作は App 側が行う。
    /// </summary>
    [Fact]
    public void MinimizeCommandがMinimizeRequestedを発生させること()
    {
        using var vm = new MainWindowViewModel();
        var raised = 0;
        vm.MinimizeRequested += (_, _) => raised++;

        vm.MinimizeCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    /// <summary>
    /// @happypath 最小化してもジグルは動き続ける（トレイへ隠れるだけ）。
    /// </summary>
    [Fact]
    public void MinimizeCommandはジグルの状態を変えないこと()
    {
        using var vm = new MainWindowViewModel();

        vm.MinimizeCommand.Execute(null);

        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @happypath 「終了」ボタンはアプリ終了を要求するだけで、実際の終了は App 側が行う。
    /// </summary>
    [Fact]
    public void ExitCommandがExitRequestedを発生させること()
    {
        using var vm = new MainWindowViewModel();
        var raised = 0;
        vm.ExitRequested += (_, _) => raised++;

        vm.ExitCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    /// <summary>
    /// @happypath 「終了」は「停止」とは別物で、ジグルの状態を変えない。
    /// </summary>
    [Fact]
    public void ExitCommandはジグルの状態を変えないこと()
    {
        using var vm = new MainWindowViewModel();

        vm.ExitCommand.Execute(null);

        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
    }
}
