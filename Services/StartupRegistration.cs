using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace IRUZ.Services;

/// <summary>
/// Windows のログイン時自動起動（HKCU の Run キー）を登録・解除するヘルパー。
/// ショートカット作成（IWshShell）は COM 依存で Native AOT と相性が悪いため、レジストリで登録する。
/// </summary>
[SupportedOSPlatform("windows")]
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IRUZ";

    /// <summary>
    /// IRUZ 本体の実行ファイル名（拡張子なし）。
    /// </summary>
    private const string ExecutableName = "IRUZ";

    /// <summary>
    /// スタートアップに登録されているかを返す。
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// スタートアップ登録を設定または解除する。
    /// </summary>
    /// <param name="enabled">登録する場合 true、解除する場合 false。</param>
    /// <returns>成功した場合 true。</returns>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key == null)
                return false;

            if (enabled)
            {
                var exePath = ResolveExecutablePath();
                if (string.IsNullOrEmpty(exePath))
                    return false;
                key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 登録済みのパスが現在の実行ファイルと異なる場合に、登録内容を最新のパスへ更新する。
    /// </summary>
    public static void RefreshIfEnabled()
    {
        // IRUZ 本体以外（テストホスト等）から呼ばれた場合、ユーザーの登録を
        // 別の実行ファイルのパスで上書きしてしまうため何もしない。
        if (!IsOwnProcess())
            return;

        if (!IsEnabled())
            return;

        try
        {
            var expected = ResolveExecutablePath();
            if (string.IsNullOrEmpty(expected))
                return;

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string current)
                return;

            if (!string.Equals(current.Trim('"'), expected, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, $"\"{expected}\"", RegistryValueKind.String);
        }
        catch { /* 登録の更新に失敗しても起動は続行する */ }
    }

    /// <summary>
    /// 現在のプロセスが IRUZ 本体かどうかを返す。
    /// </summary>
    internal static bool IsOwnProcess()
    {
        var exePath = Environment.ProcessPath;
        return !string.IsNullOrEmpty(exePath)
            && string.Equals(Path.GetFileNameWithoutExtension(exePath), ExecutableName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// スタートアップに登録する実行ファイルのパスを求める。
    /// Velopack のインストール構成では実体が &lt;root&gt;\current\IRUZ.exe に置かれ更新のたびに入れ替わるため、
    /// パスが変わらない &lt;root&gt;\IRUZ.exe（スタブ）があればそちらを優先する。
    /// </summary>
    private static string ResolveExecutablePath()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return string.Empty;

        var directory = Path.GetDirectoryName(exePath);
        if (directory != null && string.Equals(Path.GetFileName(directory), "current", StringComparison.OrdinalIgnoreCase))
        {
            var root = Path.GetDirectoryName(directory);
            if (root != null)
            {
                var stubPath = Path.Combine(root, Path.GetFileName(exePath));
                if (File.Exists(stubPath))
                    return stubPath;
            }
        }

        return exePath;
    }
}
