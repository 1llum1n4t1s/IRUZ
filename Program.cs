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

    /// <summary>
    /// 更新チェックの待ち時間上限。応答の無い配信元で起動が止まり続けないようにする。
    /// </summary>
    private static readonly TimeSpan UpdateCheckTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 終了時に単一インスタンス監視タスクの終了を待つ上限。
    /// </summary>
    private static readonly TimeSpan ListenerShutdownTimeout = TimeSpan.FromSeconds(2);

    internal static volatile Action? RestoreFromTray;

    /// <summary>
    /// トレイ登録前に復帰要求が届いた場合の保留フラグ。
    /// </summary>
    internal static volatile bool PendingRestore;

    [STAThread]
    public static void Main(string[] args)
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
        var showWindowListener = Task.Run(() => ListenForShowWindow(showWindowEvent, cts.Token));

        // await を挟むと継続がスレッドプール（MTA）へ移り、Avalonia の UI スレッドが STA でなくなる。
        // STA を保ったまま起動するため、更新処理はこのスレッド上で同期的に完了させる。
        TryForceUpdate(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        cts.Cancel();
        // WaitHandle を破棄する前に監視タスクと合流する（WaitAny 実行中の Dispose を避ける）
        try { showWindowListener.Wait(ListenerShutdownTimeout); }
        catch { /* 監視タスクの後始末に失敗してもプロセス終了は妨げない */ }
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

    private static void TryForceUpdate(string[] args)
    {
        try
        {
            var source = new SimpleWebSource(UpdateBaseUrl);
            var options = new UpdateOptions { ExplicitChannel = "win" };
            var mgr = new UpdateManager(source, options);

            // CheckForUpdatesAsync は CancellationToken を受け取らないため、待ち時間側を打ち切る。
            // 打ち切った場合は今回の更新を諦め、次回起動で改めて確認する。
            var newVersion = mgr.CheckForUpdatesAsync().WaitAsync(UpdateCheckTimeout).GetAwaiter().GetResult();
            if (newVersion == null)
                return;

            mgr.DownloadUpdatesAsync(newVersion).GetAwaiter().GetResult();
            mgr.ApplyUpdatesAndRestart(newVersion, args);
        }
        catch { /* 更新できなくても現行バージョンで起動を続ける */ }
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
