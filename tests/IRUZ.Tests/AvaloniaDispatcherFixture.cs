using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using Avalonia;
using Avalonia.Headless;

namespace IRUZ.Tests;

/// <summary>
/// Avalonia の Headless 環境を専用 STA スレッドで初期化し、UI スレッド上でテスト処理を実行する。
/// </summary>
public sealed class AvaloniaDispatcherFixture : IDisposable
{
    private readonly BlockingCollection<Action> _actions = [];
    private readonly object _lifetimeGate = new();
    private readonly Thread _uiThread;
    private bool _disposed;

    /// <summary>
    /// Headless 環境と専用 UI スレッドを起動する。
    /// </summary>
    public AvaloniaDispatcherFixture()
    {
        using var ready = new ManualResetEventSlim(false);
        Exception? startupException = null;

        _uiThread = new Thread(() =>
        {
            try
            {
                AppBuilder.Configure<HeadlessTestApp>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }
            catch (Exception ex)
            {
                startupException = ex;
            }
            finally
            {
                ready.Set();
            }

            if (startupException is not null)
                return;

            foreach (var action in _actions.GetConsumingEnumerable())
                action();
        })
        {
            IsBackground = true,
            Name = "IRUZ.Tests.AvaloniaUI",
        };

        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        ready.Wait();

        if (startupException is null)
            return;

        _actions.CompleteAdding();
        _uiThread.Join();
        _actions.Dispose();
        ExceptionDispatchInfo.Capture(startupException).Throw();
    }

    /// <summary>
    /// 処理を Avalonia の UI スレッド上で同期実行する。
    /// </summary>
    /// <param name="action">実行する処理。</param>
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var completed = new ManualResetEventSlim(false);
        Exception? actionException = null;

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _actions.Add(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    actionException = ex;
                }
                finally
                {
                    completed.Set();
                }
            });
        }

        completed.Wait();
        if (actionException is not null)
            ExceptionDispatchInfo.Capture(actionException).Throw();
    }

    /// <summary>
    /// UI スレッドを終了し、待機中のリソースを解放する。
    /// </summary>
    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _actions.CompleteAdding();
        }

        _uiThread.Join();
        _actions.Dispose();
    }
}

/// <summary>
/// 製品のウィンドウやトレイを生成しない Headless テスト用 Avalonia アプリ。
/// </summary>
public sealed class HeadlessTestApp : Application
{
}
