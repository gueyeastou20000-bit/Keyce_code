using System;
using System.Windows;

namespace Keyce_code.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        app.Run(new MainWindow());
    }
}