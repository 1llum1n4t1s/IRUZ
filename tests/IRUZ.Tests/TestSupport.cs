using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using IRUZ.ViewModels;
using Microsoft.Win32;
using Xunit;

namespace IRUZ.Tests;

/// <summary>
/// 全テストを直列実行させるためのコレクション定義。
/// レジストリの HKCU\...\Run!IRUZ 値を共有し、MainWindowViewModel の
/// コンストラクタもその値を読むため、並列実行すると互いに干渉する。
/// </summary>
[CollectionDefinition(IruzTestCollection.Name, DisableParallelization = true)]
public sealed class IruzTestCollection
{
    /// <summary>コレクション名。</summary>
    public const string Name = "IRUZ";
}

/// <summary>
/// HKCU の Run キーにある IRUZ 値だけを退避し、Dispose で必ず元の状態へ復元するガード。
/// 他のキー・値・HKLM には一切触れない。
/// </summary>
internal sealed class RunValueGuard : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "IRUZ";

    private readonly object? _original;
    private readonly RegistryValueKind _originalKind;
    private readonly bool _existed;

    /// <summary>
    /// 現在の登録値を退避する。
    /// </summary>
    public RunValueGuard()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        _original = key?.GetValue(RunValueName);
        _existed = _original is not null;
        _originalKind = _existed ? key!.GetValueKind(RunValueName) : RegistryValueKind.String;
    }

    /// <summary>
    /// 任意の型で値を書き込む。
    /// </summary>
    public static void Write(object value, RegistryValueKind kind)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        Assert.NotNull(key);
        key!.SetValue(RunValueName, value, kind);
    }

    /// <summary>
    /// 文字列値を書き込む。
    /// </summary>
    public static void WriteString(string value) => Write(value, RegistryValueKind.String);

    /// <summary>
    /// 生の値を読み出す。未登録なら null。
    /// </summary>
    public static object? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName);
    }

    /// <summary>
    /// 値の種別を読み出す。未登録なら None。
    /// </summary>
    public static RegistryValueKind ReadKind()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is null
            ? RegistryValueKind.None
            : key.GetValueKind(RunValueName);
    }

    /// <summary>
    /// 値を削除して未登録へ戻す。
    /// </summary>
    public static void Delete()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// 退避した状態へ復元する。
    /// </summary>
    public void Dispose()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
            return;

        if (_existed)
            key.SetValue(RunValueName, _original!, _originalKind);
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }
}

/// <summary>
/// テスト共通のヘルパー。
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// 指定した間隔を設定してから停止→開始し、丸め後の値が反映された StatusText を返す。
    /// StatusText は StartJiggle 時にのみ更新されるため、再起動して反映させる。
    /// </summary>
    public static string RestartWithInterval(int seconds)
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedIntervalSeconds = seconds;
        vm.ToggleCommand.Execute(null); // 停止
        vm.ToggleCommand.Execute(null); // 丸めた間隔で再開始
        return vm.StatusText;
    }

    /// <summary>
    /// 2つの処理をバリアで同時解放し、発生した例外を返す（sleep に依存しない決定的な競合再現）。
    /// </summary>
    public static List<Exception> RunConcurrently(Action first, Action second)
    {
        var errors = new List<Exception>();
        using var gate = new ManualResetEventSlim(false);
        using var ready = new CountdownEvent(2);

        void Run(Action action)
        {
            ready.Signal();
            gate.Wait();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                lock (errors)
                    errors.Add(ex);
            }
        }

        var first_ = new Thread(() => Run(first)) { IsBackground = true };
        var second_ = new Thread(() => Run(second)) { IsBackground = true };
        first_.Start();
        second_.Start();

        ready.Wait();   // 両スレッドが起動線に並ぶまで待つ
        gate.Set();     // 同時解放
        first_.Join();
        second_.Join();

        return errors;
    }
}

/// <summary>
/// カーソル座標の取得。ジグルが位置を変えないことの確認に使う。
/// </summary>
internal static class CursorPositionProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// 現在のカーソル座標を取得する。取得できない環境では false。
    /// </summary>
    public static bool TryGet(out (int X, int Y) position)
    {
        if (GetCursorPos(out var p))
        {
            position = (p.X, p.Y);
            return true;
        }

        position = default;
        return false;
    }
}
