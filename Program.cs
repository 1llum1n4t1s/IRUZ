using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Velopack;
using Velopack.Sources;

namespace IRUZ;

internal sealed class Program
{
    private const string AppUserModelId = "velopack.IRUZ";

    // 自動更新の配信元（Cloudflare R2 カスタムドメイン経由の SimpleWebSource）。
    private const string UpdateBaseUrl = "https://iruz.kagayoi.com";
    private const string MutexName = "Local\\IRUZ_SingleInstance_B7A3F1E0";
    private const string ShowWindowEventName = "Local\\IRUZ_ShowWindow_B7A3F1E0";
    internal static volatile Action? RestoreFromTray;

    /// <summary>
    /// トレイ登録前に復帰要求が届いた場合の保留フラグ。
    /// </summary>
    internal static volatile bool PendingRestore;

    [STAThread]
    public static async Task Main(string[] args)
    {
        TrySetCurrentProcessAppUserModelId();

        // Velopack のブートストラップを最初に実行する。
        // インストール・アップデート引数の処理が必要なため、多重起動チェックより前に呼ぶ。
        VelopackApp.Build().Run();

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // 既に起動中のインスタンスにウィンドウ表示を通知して終了
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showEvent.Set();
            }
            catch { }
            return;
        }

        using var showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => ListenForShowWindow(showWindowEvent, cts.Token));

        await TryForceUpdateAsync(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        cts.Cancel();
    }

    private static void ListenForShowWindow(EventWaitHandle showEvent, CancellationToken ct)
    {
        WaitHandle[] handles = [showEvent, ct.WaitHandle];
        while (!ct.IsCancellationRequested)
        {
            if (WaitHandle.WaitAny(handles) == 0)
            {
                if (RestoreFromTray is { } restore)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => restore());
                else
                    PendingRestore = true; // トレイ未登録 → 登録完了後に処理
            }
        }
    }

    private static async Task TryForceUpdateAsync(string[] args)
    {
        try
        {
            var source = new SimpleWebSource(UpdateBaseUrl);
            var options = new UpdateOptions { ExplicitChannel = "win" };
            var mgr = new UpdateManager(source, options);
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion != null)
            {
                await mgr.DownloadUpdatesAsync(newVersion);
                mgr.ApplyUpdatesAndRestart(newVersion, args);
            }
        }
        catch { }
    }

    private static void TrySetCurrentProcessAppUserModelId()
    {
        try { _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId); }
        catch { /* シェル連携の失敗だけで起動を止めない */ }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
