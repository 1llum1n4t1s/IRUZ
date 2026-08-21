using System;
using System.Linq;
using Avalonia.Controls;
using IRUZ.Services;
using IRUZ.ViewModels;
using Xunit;

namespace IRUZ.Tests;

/// <summary>
/// 🧺 タスクトレイの右クリックメニューのテスト。
/// メインウィンドウと同等の操作ができ、ViewModel の状態と同期し続けることを確認する。
/// </summary>
[Collection(IruzTestCollection.Name)]
public class TrayMenuTests
{
    /// <summary>
    /// テスト用に ViewModel とコントローラを組み立てる。
    /// </summary>
    private static (MainWindowViewModel Vm, TrayMenuController Menu, TrayMenuProbe Probe) Create()
    {
        var vm = new MainWindowViewModel();
        var probe = new TrayMenuProbe();
        var menu = new TrayMenuController(vm, () => probe.ShowCount++, () => probe.ExitCount++, t => probe.ToolTip = t);
        return (vm, menu, probe);
    }

    /// <summary>
    /// 呼び出し回数とツールチップを記録する観測用ホルダ。
    /// </summary>
    internal sealed class TrayMenuProbe
    {
        public int ShowCount { get; set; }
        public int ExitCount { get; set; }
        public string? ToolTip { get; set; }
    }

    /// <summary>
    /// ヘッダー文言から項目を取り出す。
    /// </summary>
    private static NativeMenuItem Item(NativeMenu menu, string header) =>
        menu.Items.OfType<NativeMenuItem>().Single(i => i.Header == header);

    private static NativeMenu SubMenu(NativeMenu menu, string header) =>
        Item(menu, header).Menu ?? throw new InvalidOperationException($"{header} にサブメニューがありません");

    // ─────────────────────────────────────────────
    // メニュー構成
    // ─────────────────────────────────────────────

    /// <summary>
    /// ウィンドウにある操作がすべてトレイメニューからも辿れること。
    /// </summary>
    [Fact]
    public void トレイメニューにウィンドウと同等の操作が揃っていること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        var headers = menu.Menu.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToArray();

        Assert.Contains("停止", headers);                       // 開始/停止トグル
        Assert.Contains("間隔", headers);
        Assert.Contains("自動解除", headers);
        Assert.Contains("スタートアップに登録する", headers);
        Assert.Contains("表示", headers);
        Assert.Contains("終了", headers);
    }

    /// <summary>
    /// 間隔のサブメニューは ViewModel の選択肢と一致する。
    /// </summary>
    [Fact]
    public void 間隔サブメニューがIntervalOptionsと一致すること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        var headers = SubMenu(menu.Menu, "間隔").Items.OfType<NativeMenuItem>().Select(i => i.Header);

        Assert.Equal(new[] { "30秒", "60秒", "120秒", "300秒" }, headers);
    }

    /// <summary>
    /// 自動解除のサブメニューは ViewModel の選択肢と一致する。
    /// </summary>
    [Fact]
    public void 自動解除サブメニューがAutoStopOptionsと一致すること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        var headers = SubMenu(menu.Menu, "自動解除").Items.OfType<NativeMenuItem>().Select(i => i.Header);

        Assert.Equal(
            new[] { "なし", "1時間後", "2時間後", "3時間後", "4時間後", "5時間後", "6時間後", "8時間後" },
            headers);
    }

    /// <summary>
    /// 先頭に現在の状態が（操作不可の見出しとして）出る。
    /// </summary>
    [Fact]
    public void 先頭項目が現在の状態を示す操作不可の見出しであること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        var first = (NativeMenuItem)menu.Menu.Items[0];

        Assert.Equal("ジグル中（60秒ごと）", first.Header);
        Assert.False(first.IsEnabled);
    }

    // ─────────────────────────────────────────────
    // ViewModel との同期
    // ─────────────────────────────────────────────

    /// <summary>
    /// 現在選択されている間隔・自動解除にチェックが付く。
    /// </summary>
    [Fact]
    public void 現在の選択にチェックが付くこと()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        Assert.True(Item(SubMenu(menu.Menu, "間隔"), "60秒").IsChecked);
        Assert.True(Item(SubMenu(menu.Menu, "自動解除"), "なし").IsChecked);

        vm.SelectedAutoStopOption = new AutoStopOption(3);

        Assert.True(Item(SubMenu(menu.Menu, "自動解除"), "3時間後").IsChecked);
        Assert.False(Item(SubMenu(menu.Menu, "自動解除"), "なし").IsChecked);
    }

    /// <summary>
    /// ウィンドウ側の状態変化がメニュー表示へ追従する。
    /// </summary>
    [Fact]
    public void 停止するとトグル項目と状態見出しが更新されること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        vm.ToggleCommand.Execute(null); // 停止

        Assert.Equal("開始", ((NativeMenuItem)menu.Menu.Items[2]).Header);
        Assert.Equal("停止中", ((NativeMenuItem)menu.Menu.Items[0]).Header);
    }

    /// <summary>
    /// 間隔はウィンドウの ComboBox と同じくジグル中は変更させない。
    /// </summary>
    [Fact]
    public void 間隔はジグル中に選べず停止中に選べること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        Assert.False(Item(menu.Menu, "間隔").IsEnabled);
        Assert.All(SubMenu(menu.Menu, "間隔").Items.OfType<NativeMenuItem>(), i => Assert.False(i.IsEnabled));

        vm.ToggleCommand.Execute(null); // 停止

        Assert.True(Item(menu.Menu, "間隔").IsEnabled);
        Assert.All(SubMenu(menu.Menu, "間隔").Items.OfType<NativeMenuItem>(), i => Assert.True(i.IsEnabled));
    }

    /// <summary>
    /// ツールチップに現在の状態が載り、状態変化で更新される。
    /// </summary>
    [Fact]
    public void ツールチップが現在の状態を反映して更新されること()
    {
        var (vm, menu, probe) = Create();
        using var _vm = vm;
        using var _menu = menu;

        Assert.Equal("IRUZ - ジグル中（60秒ごと）", menu.ToolTipText);

        vm.ToggleCommand.Execute(null); // 停止

        Assert.Equal("IRUZ - 停止中", menu.ToolTipText);
        Assert.Equal("IRUZ - 停止中", probe.ToolTip);
    }

    // ─────────────────────────────────────────────
    // 自動解除のカウントダウン
    // ─────────────────────────────────────────────

    /// <summary>
    /// 自動解除を設定するとカウントダウン行が状態見出しの直下に現れる。
    /// </summary>
    [Fact]
    public void 自動解除を設定するとカウントダウン行が現れること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        var before = menu.Menu.Items.Count;

        vm.SelectedAutoStopOption = new AutoStopOption(3);

        Assert.Equal(before + 1, menu.Menu.Items.Count);
        Assert.StartsWith("自動解除まで 3:00:00（", ((NativeMenuItem)menu.Menu.Items[1]).Header);
        Assert.False(((NativeMenuItem)menu.Menu.Items[1]).IsEnabled);
    }

    /// <summary>
    /// カウントダウン行は残り時間の更新に追従する。
    /// </summary>
    [Fact]
    public void カウントダウン行が残り時間の更新に追従すること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;
        vm.SelectedAutoStopOption = new AutoStopOption(3);

        vm.UpdateAutoStop(vm.AutoStopAt!.Value.AddMinutes(-5));

        Assert.StartsWith("自動解除まで 0:05:00（", ((NativeMenuItem)menu.Menu.Items[1]).Header);
    }

    /// <summary>
    /// 「なし」へ戻すとカウントダウン行が消え、項目が二重に増えない。
    /// </summary>
    [Fact]
    public void なしへ戻すとカウントダウン行が消えて項目が重複しないこと()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;
        var baseline = menu.Menu.Items.Count;

        vm.SelectedAutoStopOption = new AutoStopOption(3);
        vm.SelectedAutoStopOption = new AutoStopOption(6); // 付け替えても増えない
        Assert.Equal(baseline + 1, menu.Menu.Items.Count);

        vm.SelectedAutoStopOption = AutoStopOption.None;

        Assert.Equal(baseline, menu.Menu.Items.Count);
        Assert.Equal("ジグル中（60秒ごと）", ((NativeMenuItem)menu.Menu.Items[0]).Header);
    }

    /// <summary>
    /// 自動解除中はツールチップにも残り時間が載る。
    /// </summary>
    [Fact]
    public void 自動解除中はツールチップに残り時間が載ること()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        vm.SelectedAutoStopOption = new AutoStopOption(3);

        Assert.StartsWith("IRUZ - ジグル中（60秒ごと） / 自動解除まで 3:00:00（", menu.ToolTipText);
    }

    /// <summary>
    /// スタートアップのチェックが登録状態と一致する。
    /// </summary>
    [Fact]
    public void スタートアップ項目のチェックが登録状態と一致すること()
    {
        using var guard = new RunValueGuard();
        RunValueGuard.Delete();

        var (vm, menu, _) = Create();
        using var _vm = vm;
        using var _menu = menu;

        Assert.False(Item(menu.Menu, "スタートアップに登録する").IsChecked);

        vm.IsStartupEnabled = true;

        Assert.True(Item(menu.Menu, "スタートアップに登録する").IsChecked);
    }

    // ─────────────────────────────────────────────
    // 破棄
    // ─────────────────────────────────────────────

    /// <summary>
    /// Dispose 後は ViewModel の変更を追わない（購読が外れている）。
    /// </summary>
    [Fact]
    public void Dispose後はViewModelの変更を追わないこと()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;

        menu.Dispose();
        vm.ToggleCommand.Execute(null); // 停止

        Assert.Equal("停止", ((NativeMenuItem)menu.Menu.Items[2]).Header);
    }

    /// <summary>
    /// Dispose を複数回呼んでも例外にならない。
    /// </summary>
    [Fact]
    public void Disposeを複数回呼んでも例外にならないこと()
    {
        var (vm, menu, _) = Create();
        using var _vm = vm;

        var exception = Record.Exception(() =>
        {
            menu.Dispose();
            menu.Dispose();
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// 必須の引数が null なら構築時点で弾く。
    /// </summary>
    [Fact]
    public void 必須引数がnullならArgumentNullExceptionになること()
    {
        using var vm = new MainWindowViewModel();

        Assert.Throws<ArgumentNullException>(() => new TrayMenuController(null!, () => { }, () => { }));
        Assert.Throws<ArgumentNullException>(() => new TrayMenuController(vm, null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => new TrayMenuController(vm, () => { }, null!));
    }
}
