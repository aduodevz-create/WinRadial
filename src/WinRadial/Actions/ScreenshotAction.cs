using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Takes a full-screen screenshot, saves to Pictures folder, and copies to clipboard.
/// Uses System.Drawing for screen capture (WPF doesn't have CopyFromScreen).
/// </summary>
public sealed class ScreenshotAction : IWheelAction
{
    private readonly LogService _log;

    public string Id => "screenshot";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    public ScreenshotAction(string label, string iconKey, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _log = log;
    }

    public Task ExecuteAsync()
    {
        try
        {
            // Get virtual screen bounds (spans all monitors)
            var left = (int)SystemParameters.VirtualScreenLeft;
            var top = (int)SystemParameters.VirtualScreenTop;
            var width = (int)SystemParameters.VirtualScreenWidth;
            var height = (int)SystemParameters.VirtualScreenHeight;

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
            }

            // Save to Pictures\WinRadial Screenshots
            var picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var screenshotDir = Path.Combine(picturesDir, "WinRadial Screenshots");
            Directory.CreateDirectory(screenshotDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var fileName = $"Screenshot_{timestamp}.png";
            var filePath = Path.Combine(screenshotDir, fileName);

            bitmap.Save(filePath, ImageFormat.Png);
            _log.Info($"Screenshot saved: {filePath}");

            // Copy to clipboard
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            Clipboard.SetImage(bitmapSource);
            _log.Info("Screenshot copied to clipboard.");
        }
        catch (Exception ex)
        {
            _log.Error($"Screenshot failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
