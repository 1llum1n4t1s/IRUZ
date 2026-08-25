using System;
using System.Threading;
using IRUZ.Services;
using Xunit;

namespace IRUZ.Tests;

[Collection(IruzTestCollection.Name)]
public class ProgramTests
{
    [Fact]
    public void 更新再起動用の復帰引数が次プロセスの保留要求へ変換されること()
    {
        var beforeRestart = new WindowRestoreCoordinator();
        beforeRestart.RequestRestore();

        var restartArgs = Program.PrepareRestartArguments(["--sample"], beforeRestart);

        Assert.Equal(["--sample", Program.RestoreWindowArgument], restartArgs);

        var afterRestart = new WindowRestoreCoordinator();
        var applicationArgs = Program.PrepareApplicationArguments(restartArgs, afterRestart);

        Assert.Equal(["--sample"], applicationArgs);
        Assert.True(afterRestart.HasPendingRestore);
    }

    [Fact]
    public void 復帰処理の登録後に届いた要求はその処理を返すこと()
    {
        var coordinator = new WindowRestoreCoordinator();
        var calls = 0;
        Action restore = () => calls++;

        Assert.False(coordinator.RegisterRestore(restore));

        var requestedRestore = coordinator.RequestRestore();
        requestedRestore?.Invoke();

        Assert.Same(restore, requestedRestore);
        Assert.Equal(1, calls);
        Assert.False(coordinator.HasPendingRestore);
    }

    [Fact]
    public void 復帰要求と登録が競合しても要求を一度だけ消化できること()
    {
        for (var i = 0; i < 250; i++)
        {
            var coordinator = new WindowRestoreCoordinator();
            var calls = 0;
            Action restore = () => Interlocked.Increment(ref calls);
            Action? requestedRestore = null;
            var restoreAfterRegistration = false;

            var errors = TestHelpers.RunConcurrently(
                () => requestedRestore = coordinator.RequestRestore(),
                () => restoreAfterRegistration = coordinator.RegisterRestore(restore));

            Assert.Empty(errors);
            Assert.True((requestedRestore is not null) ^ restoreAfterRegistration);

            requestedRestore?.Invoke();
            if (restoreAfterRegistration)
                restore();

            Assert.Equal(1, calls);
            Assert.False(coordinator.HasPendingRestore);
        }
    }
}
