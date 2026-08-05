using Avalonia;
using Avalonia.Headless;
using IRUZ.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace IRUZ.Tests;

/// <summary>
/// Headless テスト用の最小 Avalonia アプリ。
/// 製品の App はトレイアイコンとウィンドウを生成するため、テストでは使わない。
/// </summary>
public sealed class HeadlessTestApp : Application
{
}

/// <summary>
/// Avalonia.Headless.XUnit が使用する AppBuilder の供給元。
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    /// Headless 環境の AppBuilder を構築する。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
