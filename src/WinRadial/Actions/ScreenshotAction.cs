using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

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

            // Audio feedback so the user knows it worked
            System.Media.SystemSounds.Asterisk.Play();

            // Copy to clipboard (prioritize this so it works even if file save fails)
            try
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                    Clipboard.SetImage(bitmapSource);
                    _log.Info("Screenshot copied to clipboard.");
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Clipboard copy failed: {ex.Message}");
            }

            // Save to Pictures\WinRadial Screenshots
            try
            {
                var picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                var screenshotDir = Path.Combine(picturesDir, "WinRadial Screenshots");
                Directory.CreateDirectory(screenshotDir);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"Screenshot_{timestamp}.png";
                var filePath = Path.Combine(screenshotDir, fileName);

                bitmap.Save(filePath, ImageFormat.Png);
                _log.Info($"Screenshot saved: {filePath}");
            }
            catch (Exception ex)
            {
                _log.Error($"Screenshot file save failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Screenshot capture failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
