using System;

namespace StarGen.Inspector;

/// <summary>In-place terminal animation: each Frame() overwrites the
/// previous one (ANSI cursor-up, erase-to-end-of-line per line) so a
/// stepping sim reads as one persistent map updating — frames of an
/// animation, never scrollback. Variable frame heights are handled by
/// padding up to the tallest frame seen. Falls back to plain sequential
/// printing when output is redirected, so piped smoke tests keep working;
/// callers should sample frames sparsely in that mode (check InPlace).</summary>
public sealed class FrameAnimator
{
    private readonly int _frameDelayMs;
    private int _lastHeight;
    private bool _done;

    public bool InPlace { get; }

    public FrameAnimator(int frameDelayMs)
    {
        _frameDelayMs = frameDelayMs;
        InPlace = !Console.IsOutputRedirected;
        if (InPlace)
        {
            EnableVirtualTerminal();
            Console.Write("\x1b[?25l");   // hide the cursor while animating
        }
    }

    public void Frame(string text)
    {
        var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        if (InPlace && _lastHeight > 0)
            Console.Write($"\x1b[{_lastHeight}A");
        int height = Math.Max(lines.Length, _lastHeight);
        for (int i = 0; i < height; i++)
        {
            string line = i < lines.Length ? lines[i] : "";
            Console.Write(InPlace ? line + "\x1b[K\n" : line + "\n");
        }
        _lastHeight = height;
        if (InPlace && _frameDelayMs > 0)
            System.Threading.Thread.Sleep(_frameDelayMs);
    }

    /// <summary>Leave the last frame on screen and restore the cursor.
    /// Idempotent (safe in finally blocks).</summary>
    public void Done()
    {
        if (_done) return;
        _done = true;
        if (InPlace) Console.Write("\x1b[?25h");
    }

    /// <summary>Classic conhost needs virtual-terminal processing switched
    /// on; Windows Terminal and VS Code handle VT regardless, so failure
    /// here is harmless.</summary>
    private static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            IntPtr handle = GetStdHandle(-11);   // STD_OUTPUT_HANDLE
            if (GetConsoleMode(handle, out uint mode))
                SetConsoleMode(handle, mode | 0x0004);   // ENABLE_VIRTUAL_TERMINAL_PROCESSING
        }
        catch (Exception)
        {
            // best effort — the escape codes are simply ignored otherwise
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);
}
