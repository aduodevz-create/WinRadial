using System;
using Velopack;
using WinRadial;

namespace WinRadial;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Run Velopack installer integration FIRST!
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
