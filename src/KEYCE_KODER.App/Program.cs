namespace KEYCE_KODER.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var application = new App();
        application.Run(new MainWindow());
    }
}
