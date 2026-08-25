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

        Console.WriteLine("DesktopIconsToggle scaffold OK");
        return 0;
    }
}
