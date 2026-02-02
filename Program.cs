using System;
using Avalonia;
using Velopack;

namespace IRU
{
    internal sealed class Program
    {
        /// <summary>
        /// アプリケーションのエントリポイント。Velopack のブートストラップを最初に実行し、続けて Avalonia アプリを起動する。
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
