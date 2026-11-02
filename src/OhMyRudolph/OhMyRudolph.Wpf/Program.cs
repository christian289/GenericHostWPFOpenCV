namespace OhMyRudolph.Wpf;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        App app = new();
        app.Run();
    }
}
