namespace DesktopIconsToggle;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--toggle")
        {
            Console.WriteLine($"DefView    : 0x{IconToggler.FindDefView():X}");
            Console.WriteLine($"Visible    : {IconToggler.AreIconsVisible()}");
            Console.WriteLine($"Toggle sent: {IconToggler.Toggle()}");
            Thread.Sleep(300);
            Console.WriteLine($"Visible now: {IconToggler.AreIconsVisible()}");
            return 0;
        }

        if (args.Length > 0 && args[0] == "--watch")
        {
            using var watcher = new DesktopWatcher();
            watcher.Changed += isDesktop =>
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] desktop foreground = {isDesktop}");
            watcher.Start();

            Console.WriteLine("Alt-Tab between the desktop and a window. Ctrl+C to stop.");
            while (Native.Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                Native.Win32.TranslateMessage(ref msg);
                Native.Win32.DispatchMessageW(ref msg);
            }

            return 0;
        }

        Console.WriteLine("DesktopIconsToggle scaffold OK");
        return 0;
    }
}
