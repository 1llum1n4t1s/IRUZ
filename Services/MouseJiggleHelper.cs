using System.Runtime.InteropServices;

namespace IRU.Services;

/// <summary>
/// Windows でマウス入力をシミュレートし、GetLastInputInfo を更新して退席中を防ぐヘルパー。
/// SetCursorPos は「最後の入力」を更新しないため、SendInput で相対マウス移動を行う。
/// </summary>
public static class MouseJiggleHelper
{
    private const int INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)]
        public int type;
        [FieldOffset(8)]
        public MOUSEINPUT mi;
    }

    /// <summary>
    /// 相対マウス移動を送り、OS の「最後の入力」を更新する（ジグル）。
    /// Teams 等の退席判定（GetLastInputInfo 等）をリセットする。
    /// </summary>
    /// <returns>成功した場合 true。</returns>
    public static bool Jiggle()
    {
        var size = Marshal.SizeOf<INPUT>();
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE;
        inputs[0].mi.dx = 1;
        inputs[0].mi.dy = 0;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_MOVE;
        inputs[1].mi.dx = -1;
        inputs[1].mi.dy = 0;
        return SendInput(2, inputs, size) == 2;
    }
}
