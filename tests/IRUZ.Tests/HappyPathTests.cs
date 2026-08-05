using System;
using System.Collections.Generic;
using System.ComponentModel;
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
}
