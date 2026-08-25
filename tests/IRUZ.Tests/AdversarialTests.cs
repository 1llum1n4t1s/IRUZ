using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using IRUZ.Services;
using IRUZ.ViewModels;
using Microsoft.Win32;
using Xunit;

namespace IRUZ.Tests;

/// <summary>
/// 😈 嫌がらせテスト（Adversarial）。
/// 境界値 / 状態遷移 / 並行性 / 環境異常の4カテゴリを扱う。
/// </summary>
[Collection(IruzTestCollection.Name)]
public class AdversarialTests
{
    #region 🗡️ 境界値・極端入力（Boundary Assault）

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// 1 は有効範囲の下限。丸めが発生せずそのまま採用されること。
    /// </summary>
    [Fact]
    public void 間隔が下限1秒のとき丸められずそのまま採用されること()
    {
        Assert.Equal("ジグル中（1秒ごと）", TestHelpers.RestartWithInterval(1));
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// 86400 は有効範囲の上限。Timer 生成が例外にならないこと。
    /// </summary>
    [Fact]
    public void 間隔が上限86400秒のとき丸められずそのまま採用されること()
    {
        Assert.Equal("ジグル中（86400秒ごと）", TestHelpers.RestartWithInterval(86400));
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// 回帰防止の要点。修正前は ArgumentException で落ちていた。
    /// </summary>
    [Fact]
    public void 間隔が0のとき既定60秒へ丸められ例外にならないこと()
    {
        Assert.Equal("ジグル中（60秒ごと）", TestHelpers.RestartWithInterval(0));
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// 上限の直後。範囲外なので既定値へ丸める。
    /// </summary>
    [Fact]
    public void 間隔が86401秒のとき既定60秒へ丸められること()
    {
        Assert.Equal("ジグル中（60秒ごと）", TestHelpers.RestartWithInterval(86401));
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// 下限の直前。範囲外なので既定値へ丸める。
    /// </summary>
    [Fact]
    public void 間隔が負値のとき既定60秒へ丸められること()
    {
        Assert.Equal("ジグル中（60秒ごと）", TestHelpers.RestartWithInterval(-1));
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// int 極値は *1000.0 で double 化されるため、丸め判定が先に効く必要がある。
    /// </summary>
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void 間隔がint極値のとき既定60秒へ丸められオーバーフローしないこと(int seconds)
    {
        Assert.Equal("ジグル中（60秒ごと）", TestHelpers.RestartWithInterval(seconds));
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// 丸めによって StartJiggle が最後まで通り、開始状態になること。
    /// </summary>
    [Fact]
    public void 範囲外の間隔で開始してもIsRunningがtrueになること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedIntervalSeconds = 0;

        vm.ToggleCommand.Execute(null); // 停止
        Assert.False(vm.IsRunning);

        vm.ToggleCommand.Execute(null); // 丸めた間隔で開始
        Assert.True(vm.IsRunning);
        Assert.Equal("停止", vm.ToggleButtonText);
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// REG_SZ ではあるが実質無効な値。IsNullOrWhiteSpace で false になること。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t ")]
    public void 登録値が空文字や空白のみのときIsEnabledがfalseを返すこと(string value)
    {
        using var guard = new RunValueGuard();
        RunValueGuard.WriteString(value);

        Assert.False(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=high
    /// 想定外の型。string へのパターンマッチが外れて false になり、例外が漏れないこと。
    /// </summary>
    [Fact]
    public void 登録値がREG_DWORDのときIsEnabledがfalseを返すこと()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Write(1, RegistryValueKind.DWord);

        Assert.False(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=med
    /// byte[] / string[] も string ではないため false。型違いで例外にならないこと。
    /// </summary>
    [Fact]
    public void 登録値がREG_BINARYやREG_MULTI_SZのときIsEnabledがfalseを返すこと()
    {
        using var guard = new RunValueGuard();

        RunValueGuard.Write(new byte[] { 0x01, 0x02 }, RegistryValueKind.Binary);
        Assert.False(StartupRegistration.IsEnabled());

        RunValueGuard.Write(new[] { "a", "b" }, RegistryValueKind.MultiString);
        Assert.False(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=med
    /// 引用符の有無・前後空白付きのいずれも非空白文字列なので true。
    /// </summary>
    [Theory]
    [InlineData("\"C:\\Program Files\\IRUZ\\IRUZ.exe\"")]
    [InlineData("C:\\IRUZ\\IRUZ.exe")]
    [InlineData("  C:\\IRUZ\\IRUZ.exe  ")]
    [InlineData("x")]
    public void 登録値が引用符付きや前後空白付きでもIsEnabledがtrueを返すこと(string value)
    {
        using var guard = new RunValueGuard();
        RunValueGuard.WriteString(value);

        Assert.True(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @adversarial @category=境界値 @severity=med
    /// 存在しない値の削除は throwOnMissingValue:false のため成功扱いで冪等。
    /// </summary>
    [Fact]
    public void 未登録状態でSetEnabledfalseを呼んでもtrueを返し登録されないこと()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();

        Assert.True(StartupRegistration.SetEnabled(false));
        Assert.True(StartupRegistration.SetEnabled(false));
        Assert.False(StartupRegistration.IsEnabled());
    }

    #endregion

    #region 🔀 状態遷移（State Machine Abuse）

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=med
    /// 多重遷移の耐性。偶数回で開始状態へ戻り、途中で状態不整合が起きないこと。
    /// </summary>
    [Fact]
    public void Toggleを10回連続で呼んでも状態が破綻しないこと()
    {
        using var vm = new MainWindowViewModel();

        for (var i = 0; i < 10; i++)
        {
            vm.ToggleCommand.Execute(null);
            var expectedRunning = i % 2 == 1;
            Assert.Equal(expectedRunning, vm.IsRunning);
            Assert.Equal(expectedRunning ? "ジグル中（60秒ごと）" : "停止中", vm.StatusText);
            Assert.Equal(expectedRunning ? "停止" : "開始", vm.ToggleButtonText);
        }

        Assert.True(vm.IsRunning);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=high
    /// Dispose の idempotency。_timer が null 条件付きで扱われるため多重呼び出しが安全であること。
    /// </summary>
    [Fact]
    public void Disposeを3回呼んでも例外にならず停止中のままになること()
    {
        var vm = new MainWindowViewModel();

        vm.Dispose();
        vm.Dispose();
        vm.Dispose();

        Assert.False(vm.IsRunning);
        Assert.Equal("停止中", vm.StatusText);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=med
    /// 停止済み状態からの Dispose（重複した停止遷移）が状態を壊さないこと。
    /// </summary>
    [Fact]
    public void 停止後にDisposeしても停止中の状態が変化しないこと()
    {
        var vm = new MainWindowViewModel();
        vm.ToggleCommand.Execute(null);
        Assert.False(vm.IsRunning);

        vm.Dispose();

        Assert.False(vm.IsRunning);
        Assert.Equal("停止中", vm.StatusText);
        Assert.Equal("開始", vm.ToggleButtonText);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=high
    /// 終了後操作の現行挙動を固定する。Dispose は破棄フラグを持たないため
    /// Toggle が StartJiggle を再実行し、IsRunning が true に戻る。
    /// 仕様変更（破棄後は無視する等）で真っ先に壊れる箇所なので明示的に固定しておく。
    /// </summary>
    [Fact]
    public void Dispose後にToggleするとジグルが再開してしまうこと()
    {
        using var vm = new MainWindowViewModel();
        vm.Dispose();
        Assert.False(vm.IsRunning);

        vm.ToggleCommand.Execute(null);

        // 現行仕様: Dispose 済みでも再開する（破棄状態を保持しない）
        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
        Assert.Equal("停止", vm.ToggleButtonText);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=med
    /// StatusText は遷移時にしか更新されない。停止中の変更は次の開始で反映される。
    /// </summary>
    [Fact]
    public void 停止中に間隔を変更してもStatusTextは停止中のままで再開時に反映されること()
    {
        using var vm = new MainWindowViewModel();
        vm.ToggleCommand.Execute(null);

        vm.SelectedIntervalSeconds = 300;
        Assert.Equal("停止中", vm.StatusText);

        vm.ToggleCommand.Execute(null);
        Assert.Equal("ジグル中（300秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=med
    /// 実行中の間隔変更は即時反映されない（タイマーも張り替わらない）。
    /// </summary>
    [Fact]
    public void ジグル中に間隔を変更してもStatusTextは再遷移するまで更新されないこと()
    {
        using var vm = new MainWindowViewModel();
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);

        vm.SelectedIntervalSeconds = 30;
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);

        vm.ToggleCommand.Execute(null);
        vm.ToggleCommand.Execute(null);
        Assert.Equal("ジグル中（30秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=med
    /// 遷移1回につき通知が各1回であること（多重発火しない）。
    /// </summary>
    [Fact]
    public void 停止遷移でIsRunningとToggleButtonTextの通知が各1回だけ発火すること()
    {
        using var vm = new MainWindowViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ToggleCommand.Execute(null);

        Assert.Equal(1, raised.Count(n => n == nameof(MainWindowViewModel.IsRunning)));
        Assert.Equal(1, raised.Count(n => n == nameof(MainWindowViewModel.ToggleButtonText)));
        Assert.Contains(nameof(MainWindowViewModel.StatusText), raised);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=med
    /// 重複遷移の抑止。等値なら変更通知も OnChanged も走らないため副作用が起きない。
    /// </summary>
    [Fact]
    public void 同じ値のIsStartupEnabledを再代入しても通知が発火しないこと()
    {
        using var guard = new RunValueGuard();
        using var vm = new MainWindowViewModel();
        var current = vm.IsStartupEnabled;
        var statusBefore = vm.StatusText;
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsStartupEnabled = current;
        vm.IsStartupEnabled = current;

        Assert.DoesNotContain(nameof(MainWindowViewModel.IsStartupEnabled), raised);
        Assert.Equal(statusBefore, vm.StatusText);
        Assert.Equal(current, vm.IsStartupEnabled);
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=low
    /// 重複した登録遷移が既存値を壊さないこと。
    /// </summary>
    [Fact]
    public void スタートアップ登録を連続で2回ONにしても登録状態が保たれること()
    {
        using var guard = new RunValueGuard();

        Assert.True(StartupRegistration.SetEnabled(true));
        Assert.True(StartupRegistration.SetEnabled(true));
        Assert.True(StartupRegistration.IsEnabled());
    }

    #endregion

    #region ⚡ 並行性（Concurrency Chaos）

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// バリアで2スレッドの Execute を同時解放し、タイマー差し替え中の競合で
    /// 例外が出ないこと、後続の単独 Toggle でも遷移が壊れないことを確認する。
    /// </summary>
    [Fact]
    public void 二スレッドから同時にToggleしても例外なく状態遷移が保たれること()
    {
        using var vm = new MainWindowViewModel();

        var errors = TestHelpers.RunConcurrently(
            () => vm.ToggleCommand.Execute(null),
            () => vm.ToggleCommand.Execute(null));

        Assert.Empty(errors);

        // 競合後も状態機械が壊れていないこと（1回の Toggle で必ず反転する）
        var before = vm.IsRunning;
        vm.ToggleCommand.Execute(null);
        Assert.Equal(!before, vm.IsRunning);
        Assert.Equal(vm.IsRunning ? "停止" : "開始", vm.ToggleButtonText);
    }

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// 破棄とタイマー再生成の競合。Dispose 後に Toggle が新しいタイマーを作り得るため、
    /// 再度 Dispose して停止へ収束することを確認する。
    /// </summary>
    [Fact]
    public void DisposeとToggleが同時に走っても最後のDisposeで停止に収束すること()
    {
        var vm = new MainWindowViewModel();
        try
        {
            var errors = TestHelpers.RunConcurrently(
                () => vm.Dispose(),
                () => vm.ToggleCommand.Execute(null));

            Assert.Empty(errors);
        }
        finally
        {
            vm.Dispose();
        }

        Assert.False(vm.IsRunning);
        Assert.Equal("停止中", vm.StatusText);
        Assert.Equal("開始", vm.ToggleButtonText);
    }

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// 1秒間隔でタイマーを回して Elapsed を発火させ、停止後に RunJobs する。
    /// OnJiggleElapsed の Post 内 IsRunning ガードにより、遅延ジョブが
    /// 停止中の表示を上書きしないことを確認する。
    /// </summary>
    [AvaloniaFact]
    public void 停止後に投函済みジョブを実行してもStatusTextが上書きされないこと()
    {
        using var vm = new MainWindowViewModel();

        // 1秒間隔で再スタート（間隔は StartJiggle 時に読まれる）
        vm.SelectedIntervalSeconds = 1;
        vm.ToggleCommand.Execute(null); // 停止
        vm.ToggleCommand.Execute(null); // 1秒間隔で開始
        Assert.Equal("ジグル中（1秒ごと）", vm.StatusText);

        // Elapsed を最低1回発火させる
        Thread.Sleep(1200);

        vm.ToggleCommand.Execute(null); // 停止
        Assert.Equal("停止中", vm.StatusText);

        // 停止前に投函済みのジョブを実行しても上書きされない
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("停止中", vm.StatusText);
        Assert.False(vm.IsRunning);
    }

    /// <summary>
    /// @adversarial 停止後に再開した場合も、前回のタイマーが投函した結果で現在の表示を壊さない。
    /// </summary>
    [Fact]
    public void 停止後に再開しても前回のジグル結果でStatusTextが上書きされないこと()
    {
        using var vm = new MainWindowViewModel();
        var timerField = typeof(MainWindowViewModel).GetField(
            "_timer",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(timerField);
        var staleTimer = Assert.IsType<System.Timers.Timer>(timerField.GetValue(vm));

        vm.ToggleCommand.Execute(null); // 停止
        vm.ToggleCommand.Execute(null); // 新しいタイマーで再開
        var currentTimer = Assert.IsType<System.Timers.Timer>(timerField.GetValue(vm));
        Assert.NotSame(staleTimer, currentTimer);

        var jiggleCurrentTimer = typeof(MainWindowViewModel).GetMethod(
            "JiggleIfCurrentTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(jiggleCurrentTimer);
        Assert.Null(jiggleCurrentTimer.Invoke(vm, [staleTimer]));

        var applyResult = typeof(MainWindowViewModel).GetMethod(
            "ApplyJiggleResult",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyResult);
        applyResult.Invoke(vm, [staleTimer, false]);

        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
        Assert.True(vm.IsRunning);
    }

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// テストホストからの並行呼び出しはプロセスガードで no-op になる。
    /// </summary>
    [Fact]
    public void RefreshIfEnabledを二スレッドから同時に呼んでも登録が変化しないこと()
    {
        using var guard = new RunValueGuard();
        var before = StartupRegistration.IsEnabled();

        var errors = TestHelpers.RunConcurrently(
            StartupRegistration.RefreshIfEnabled,
            StartupRegistration.RefreshIfEnabled);

        Assert.Empty(errors);
        Assert.Equal(before, StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @adversarial @category=並行性 @severity=med
    /// 同一レジストリ値への同時書き込みで例外や戻り値の破綻がないこと。
    /// </summary>
    [Fact]
    public void SetEnabledを二スレッドから同時に呼んでも例外にならないこと()
    {
        using var guard = new RunValueGuard();

        var errors = TestHelpers.RunConcurrently(
            () => StartupRegistration.SetEnabled(true),
            () => StartupRegistration.SetEnabled(false));

        Assert.Empty(errors);

        // 競合後も読み出しは例外なく bool を返す
        var exception = Record.Exception(() => StartupRegistration.IsEnabled());
        Assert.Null(exception);
    }

    #endregion

    #region 🌪️ 環境異常（Environmental Chaos）

    /// <summary>
    /// @adversarial @category=環境異常 @severity=high
    /// Phase 0.5 の最重要仕様。IRUZ 本体以外のプロセス（testhost.exe）から呼んでも
    /// 登録パスが testhost のパスへ上書きされないこと。
    /// </summary>
    [Fact]
    public void テストホストからRefreshIfEnabledを呼んでも既存の登録パスを書き換えないこと()
    {
        using var guard = new RunValueGuard();
        const string dummy = "\"C:\\dummy\\IRUZ.exe\"";
        RunValueGuard.WriteString(dummy);

        StartupRegistration.RefreshIfEnabled();

        Assert.Equal(dummy, RunValueGuard.Read());
        Assert.Equal(RegistryValueKind.String, RunValueGuard.ReadKind());
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=high
    /// 未登録状態でテストホストから呼んでも新規に登録を作らないこと。
    /// </summary>
    [Fact]
    public void 未登録状態でRefreshIfEnabledを呼んでも新規に登録を作らないこと()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();

        StartupRegistration.RefreshIfEnabled();

        Assert.Null(RunValueGuard.Read());
        Assert.False(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=med
    /// 起動経路で繰り返し呼ばれても冪等で、例外を投げないこと。
    /// </summary>
    [Fact]
    public void RefreshIfEnabledを複数回呼んでも例外を投げず登録内容が変化しないこと()
    {
        using var guard = new RunValueGuard();
        const string dummy = "\"C:\\dummy\\sub dir\\IRUZ.exe\"";
        RunValueGuard.WriteString(dummy);

        var exception = Record.Exception(() =>
        {
            for (var i = 0; i < 5; i++)
                StartupRegistration.RefreshIfEnabled();
        });

        Assert.Null(exception);
        Assert.Equal(dummy, RunValueGuard.Read());
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=high
    /// SetEnabled はプロセスガードを持たないため、呼び出し元の実行ファイルパスが
    /// 二重引用符付きで書かれる。RefreshIfEnabled との挙動差を固定する。
    /// </summary>
    [Fact]
    public void SetEnabledtrueは呼び出し元プロセスのパスを引用符付きで登録すること()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();

        Assert.True(StartupRegistration.SetEnabled(true));

        var stored = Assert.IsType<string>(RunValueGuard.Read());
        Assert.StartsWith("\"", stored, StringComparison.Ordinal);
        Assert.EndsWith("\"", stored, StringComparison.Ordinal);
        Assert.Equal(Environment.ProcessPath, stored.Trim('"'));
        Assert.True(StartupRegistration.IsEnabled());
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=high
    /// コンストラクタは RefreshIfEnabled を呼ぶ。テストホストから生成しても
    /// 既存登録が書き換わらず、チェック状態だけが読み取られること。
    /// </summary>
    [Fact]
    public void ダミーパス登録があってもViewModel生成で値を書き換えないこと()
    {
        using var guard = new RunValueGuard();
        const string dummy = "\"C:\\dummy\\IRUZ.exe\"";
        RunValueGuard.WriteString(dummy);

        using (var vm = new MainWindowViewModel())
        {
            Assert.True(vm.IsStartupEnabled);
            Assert.Equal(dummy, RunValueGuard.Read());
        }

        Assert.Equal(dummy, RunValueGuard.Read());
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=high
    /// P/Invoke 定義（構造体サイズ・レイアウト）が壊れていれば例外やクラッシュになる。
    /// 戻り値は画面ロック等で false になり得るため断定しない。
    /// </summary>
    [Fact]
    public void Jiggleを繰り返し呼んでも例外を投げずboolを返すこと()
    {
        var exception = Record.Exception(() =>
        {
            for (var i = 0; i < 10; i++)
                _ = MouseJiggleHelper.Jiggle();
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=med
    /// 実際の呼び出し元はタイマーのワーカースレッド。スレッドアフィニティ依存がないこと。
    /// </summary>
    [Fact]
    public void JiggleをUIスレッド以外から呼んでも例外を投げないこと()
    {
        var exception = Record.Exception(() => Task.Run(() =>
        {
            _ = MouseJiggleHelper.Jiggle();
            _ = MouseJiggleHelper.Jiggle();
        }, TestContext.Current.CancellationToken).GetAwaiter().GetResult());

        Assert.Null(exception);
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=med
    /// de-DE（小数点がカンマ）や tr-TR での数値書式差の影響を受けないこと。
    /// </summary>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("ja-JP")]
    public void 小数点記号が異なるカルチャでもStatusTextの表示が崩れないこと(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

            using var vm = new MainWindowViewModel();

            Assert.Equal(60, vm.SelectedIntervalSeconds);
            Assert.True(vm.IsRunning);
            Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
            Assert.Equal("停止", vm.ToggleButtonText);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>
    /// @adversarial @category=環境異常 @severity=low
    /// バージョン表記が数字置換やカルチャ依存書式の影響を受けないこと。
    /// </summary>
    [Fact]
    public void カルチャを変えてもAppVersionがASCII数字の同一文字列になること()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            using var invariantVm = new MainWindowViewModel();
            var expected = invariantVm.AppVersion;

            Assert.Matches("^IRUZ v[0-9]+\\.[0-9]+\\.[0-9]+$", expected);

            foreach (var name in new[] { "tr-TR", "ar-SA", "de-DE" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                CultureInfo.CurrentUICulture = new CultureInfo(name);
                using var vm = new MainWindowViewModel();
                Assert.Equal(expected, vm.AppVersion);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    #endregion

    #region 🖱️ 停止後の入力漏れ（Elapsed レース）

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// System.Timers.Timer は Stop/Dispose 後も進行中の Elapsed を止めないため、
    /// 停止済みの状態で発火した分は SendInput を送ってはいけない（離席判定が再武装される）。
    /// </summary>
    [Fact]
    public void 停止後にタイマー発火分が走っても入力を送らないこと()
    {
        using var vm = new MainWindowViewModel();
        Assert.NotNull(vm.JiggleIfRunning()); // ジグル中は送る

        vm.ToggleCommand.Execute(null); // 停止

        Assert.Null(vm.JiggleIfRunning()); // 進行中の Elapsed 相当。送ってはいけない
    }

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// 自動解除でも同じ保証が要る。README が約束する「停止後は入力の送信が途絶える」の担保。
    /// </summary>
    [Fact]
    public void 自動解除後にタイマー発火分が走っても入力を送らないこと()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);

        vm.UpdateAutoStop(vm.AutoStopAt!.Value); // 自動解除

        Assert.False(vm.IsRunning);
        Assert.Null(vm.JiggleIfRunning());
    }

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// Dispose 済みでも入力を送らず、例外も出さない。
    /// </summary>
    [Fact]
    public void Dispose後は入力を送らず例外にもならないこと()
    {
        var vm = new MainWindowViewModel();
        vm.Dispose();

        var exception = Record.Exception(() => Assert.Null(vm.JiggleIfRunning()));

        Assert.Null(exception);
    }

    /// <summary>
    /// @adversarial @category=並行性 @severity=high
    /// 停止と発火を同時に解放しても状態が壊れず、停止が完了した後は必ず送らない。
    /// （同時実行そのものの勝敗は非決定なので、収束後の不変条件を固定する）
    /// </summary>
    [Fact]
    public void 停止と発火が競合しても停止完了後は必ず入力を送らないこと()
    {
        using var vm = new MainWindowViewModel();

        var errors = TestHelpers.RunConcurrently(
            () => vm.ToggleCommand.Execute(null),
            () => vm.JiggleIfRunning());

        Assert.Empty(errors);
        Assert.False(vm.IsRunning);
        Assert.Null(vm.JiggleIfRunning());
    }

    /// <summary>
    /// @adversarial @category=状態遷移 @severity=med
    /// 停止から再開すると、また送れるようになる（ゲートが開きっぱなし／閉じっぱなしにならない）。
    /// </summary>
    [Fact]
    public void 停止と再開を繰り返しても入力可否がその都度切り替わること()
    {
        using var vm = new MainWindowViewModel();

        for (var i = 0; i < 3; i++)
        {
            vm.ToggleCommand.Execute(null); // 停止
            Assert.Null(vm.JiggleIfRunning());
            vm.ToggleCommand.Execute(null); // 再開
            Assert.NotNull(vm.JiggleIfRunning());
        }
    }

    #endregion

    #region 自動解除タイマー

    /// <summary>
    /// @adversarial 境界値: 予定時刻の1秒前ではまだ解除されない（オフバイワン確認）。
    /// </summary>
    [Fact]
    public void 予定時刻の1秒前では自動解除されないこと()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);

        vm.UpdateAutoStop(vm.AutoStopAt!.Value.AddSeconds(-1));

        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
        Assert.StartsWith("自動解除まで 0:00:01（", vm.AutoStopText);
    }

    /// <summary>
    /// @adversarial 環境異常: スリープ等で発火が大幅に遅れても、絶対時刻判定なので取りこぼさない。
    /// </summary>
    [Fact]
    public void 予定時刻を大幅に過ぎた時刻で評価しても確実に自動解除されること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);

        vm.UpdateAutoStop(vm.AutoStopAt!.Value.AddDays(2));

        Assert.False(vm.IsRunning);
        Assert.Equal("自動解除しました（3時間経過）", vm.StatusText);
    }

    /// <summary>
    /// @adversarial 状態遷移: 自動解除後に再評価しても状態文言が上書きされない。
    /// </summary>
    [Fact]
    public void 自動解除後にもう一度評価しても状態が変わらないこと()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(2);
        var deadline = vm.AutoStopAt!.Value;
        vm.UpdateAutoStop(deadline);

        vm.UpdateAutoStop(deadline.AddHours(1));

        Assert.False(vm.IsRunning);
        Assert.Equal("自動解除しました（2時間経過）", vm.StatusText);
    }

    /// <summary>
    /// @adversarial 状態遷移: 停止中に評価しても「停止中」表示を壊さない。
    /// </summary>
    [Fact]
    public void 停止中にUpdateAutoStopを呼んでもStatusTextが上書きされないこと()
    {
        using var vm = new MainWindowViewModel();
        vm.ToggleCommand.Execute(null); // 停止

        vm.UpdateAutoStop(DateTimeOffset.Now.AddDays(1));

        Assert.False(vm.IsRunning);
        Assert.Equal("停止中", vm.StatusText);
    }

    /// <summary>
    /// @adversarial 状態遷移: 「なし」へ戻すと予定が取り消され、残り時間表示も消える。
    /// </summary>
    [Fact]
    public void 自動解除をなしへ戻すと予定が取り消されて通常表示へ戻ること()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);
        Assert.NotNull(vm.AutoStopAt);

        vm.SelectedAutoStopOption = AutoStopOption.None;

        Assert.Null(vm.AutoStopAt);
        Assert.False(vm.HasAutoStop);
        Assert.Equal(string.Empty, vm.AutoStopText);
        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @adversarial 状態遷移: 途中で時間を変えると、その時点から数え直す（残りが延びる）。
    /// </summary>
    [Fact]
    public void ジグル中に時間を変更すると変更時点から数え直すこと()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(1);
        var firstDeadline = vm.AutoStopAt!.Value;

        vm.SelectedAutoStopOption = new AutoStopOption(6);

        Assert.True(vm.AutoStopAt!.Value > firstDeadline.AddHours(4));
        Assert.StartsWith("自動解除まで 6:00:00（", vm.AutoStopText);
    }

    /// <summary>
    /// @adversarial 境界値: 0 以下の時間は「なし」扱いにして予定を立てない。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ゼロ以下の時間を指定しても自動解除が設定されないこと(int hours)
    {
        using var vm = new MainWindowViewModel();

        vm.SelectedAutoStopOption = new AutoStopOption(hours);

        Assert.Null(vm.AutoStopAt);
        Assert.True(vm.IsRunning);
        Assert.Equal("ジグル中（60秒ごと）", vm.StatusText);
    }

    /// <summary>
    /// @adversarial 境界値: 極端に大きな時間でも DateTimeOffset を溢れさせず 24 時間へ丸める。
    /// </summary>
    [Theory]
    [InlineData(25)]
    [InlineData(100000)]
    [InlineData(int.MaxValue)]
    public void 過大な時間を指定しても24時間へ丸められて例外にならないこと(int hours)
    {
        using var vm = new MainWindowViewModel();

        var exception = Record.Exception(() => vm.SelectedAutoStopOption = new AutoStopOption(hours));

        Assert.Null(exception);
        Assert.NotNull(vm.AutoStopAt);
        Assert.StartsWith("自動解除まで 24:00:00（", vm.AutoStopText);
        Assert.InRange(vm.AutoStopAt!.Value, DateTimeOffset.Now.AddHours(23), DateTimeOffset.Now.AddHours(25));
    }

    /// <summary>
    /// @adversarial 境界値: ComboBox の選択解除などで null が入っても「なし」へ矯正する。
    /// </summary>
    [Fact]
    public void 自動解除にnullを設定してもなしへ矯正されること()
    {
        using var vm = new MainWindowViewModel();

        vm.SelectedAutoStopOption = null!;

        Assert.Equal(AutoStopOption.None, vm.SelectedAutoStopOption);
        Assert.Null(vm.AutoStopAt);
    }

    /// <summary>
    /// @adversarial 状態遷移: Dispose 済みでも評価呼び出しが例外にならない。
    /// </summary>
    [Fact]
    public void Dispose後にUpdateAutoStopを呼んでも例外にならないこと()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedAutoStopOption = new AutoStopOption(3);
        vm.Dispose();

        var exception = Record.Exception(() => vm.UpdateAutoStop(DateTimeOffset.Now.AddDays(1)));

        Assert.Null(exception);
        Assert.False(vm.IsRunning);
        Assert.Equal("停止中", vm.StatusText);
    }

    /// <summary>
    /// @adversarial 環境異常: 小数点記号や数字体系が異なるカルチャでも残り時間表示が崩れない。
    /// </summary>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("de-DE")]
    public void カルチャを変えても残り時間がASCII数字のhmmss形式になること(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            using var vm = new MainWindowViewModel();

            vm.SelectedAutoStopOption = new AutoStopOption(3);

            Assert.StartsWith("自動解除まで 3:00:00（", vm.AutoStopText);
            // 解除予定時刻も ASCII 数字の HH:mm であること（ar-SA の暦・数字体系に引きずられない）
            Assert.Matches(@"（[0-9]{2}:[0-9]{2} に解除）$", vm.AutoStopText);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    #endregion
}
