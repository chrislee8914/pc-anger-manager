namespace PcAngerManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // 중복 실행 방지 (SPEC 5.4): 이미 실행 중이면 바로 종료
        using var mutex = new Mutex(true, "PcAngerManager.SingleInstance", out bool isFirst);
        if (!isFirst) return;

        ApplicationConfiguration.Initialize();

        bool showPreview = args.Contains("--preview");
        Application.Run(new TrayApp(showPreview));
    }
}
