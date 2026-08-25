using System;
using System.Threading;

namespace IRUZ.Services;

/// <summary>
/// 二重起動によるウィンドウ復帰要求を、UI の登録前後で取りこぼさず受け渡す。
/// </summary>
internal sealed class WindowRestoreCoordinator
{
    private readonly Lock _gate = new();
    private Action? _restore;
    private bool _pending;

    internal bool HasPendingRestore
    {
        get
        {
            lock (_gate)
                return _pending;
        }
    }

    /// <summary>
    /// 復帰要求を記録し、UI が登録済みなら呼び出すアクションを返す。
    /// </summary>
    internal Action? RequestRestore()
    {
        lock (_gate)
        {
            if (_restore is null)
            {
                _pending = true;
                return null;
            }

            return _restore;
        }
    }

    /// <summary>
    /// UI の復帰処理を登録し、それ以前の保留要求を呼び出し側で消化すべきか返す。
    /// </summary>
    internal bool RegisterRestore(Action restore)
    {
        ArgumentNullException.ThrowIfNull(restore);

        lock (_gate)
        {
            _restore = restore;
            var shouldRestore = _pending;
            _pending = false;
            return shouldRestore;
        }
    }
}
