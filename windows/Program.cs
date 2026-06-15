namespace Volumify;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        StartupManager.MigrateLegacy(); // carry "run at startup" over from the old app name
        Application.Run(new TrayAppContext());
    }
}
