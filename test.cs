using System.Diagnostics;
class Program {
    static void Main() {
        var psi = new ProcessStartInfo {
            FileName = "code",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi);
    }
}
