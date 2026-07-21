using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinRadial.Core;

namespace WinRadial.UI;

/// <summary>
/// Provides icons for wheel actions using Segoe MDL2 Assets glyphs and
/// SHGetFileInfo for executable file icons. All sources are local-only.
/// Caches extracted icons for performance.
/// </summary>
public sealed class IconProvider
{
    private readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new();

    /// <summary>
    /// Maps well-known icon key names to Segoe MDL2 Assets Unicode glyphs.
    /// </summary>
    private static readonly Dictionary<string, string> GlyphMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // System actions
        ["lock"]       = "\uE72E",
        ["sleep"]      = "\uE708",
        ["darkmode"]   = "\uE793",
        ["recycle"]    = "\uE74D",
        ["screenshot"] = "\uE722",
        ["settings"]   = "\uE713",
        ["power"]      = "\uE7E8",
        ["restart"]    = "\uE72C",

        // Apps
        ["app"]        = "\uE737",
        ["folder"]     = "\uE838",
        ["terminal"]   = "\uE756",
        ["browser"]    = "\uE774",
        ["code"]       = "\uE943",
        ["calculator"] = "\uE8EF",
        ["notepad"]    = "\uE70F",
        ["mail"]       = "\uE715",
        ["music"]      = "\uE8D6",
        ["video"]      = "\uE714",
        ["photo"]      = "\uE71D",
        ["search"]     = "\uE721",
        ["download"]   = "\uE896",
        ["upload"]     = "\uE898",
        ["cloud"]      = "\uE753",
        ["network"]    = "\uE968",
        ["bluetooth"]  = "\uE702",
        ["wifi"]       = "\uE701",
        ["display"]    = "\uE7F4",
        ["audio"]      = "\uE767",
        ["keyboard"]   = "\uE765",
        ["mouse"]      = "\uE962",
        ["printer"]    = "\uE749",
        ["usb"]        = "\uE88E",
        ["disk"]       = "\uEDA2",
        ["info"]       = "\uE946",
        ["warning"]    = "\uE7BA",
        ["error"]      = "\uE783",
        ["check"]      = "\uE73E",
        ["close"]      = "\uE711",
        ["add"]        = "\uE710",
        ["remove"]     = "\uE738",
        ["edit"]       = "\uE70F",
        ["star"]       = "\uE734",
        ["heart"]      = "\uE006",
        ["home"]       = "\uE80F",
        ["user"]       = "\uE77B",
        ["people"]     = "\uE716",
        ["calendar"]   = "\uE787",
        ["clock"]      = "\uE823",
        ["map"]        = "\uE707",
        ["pin"]        = "\uE718",
        ["flag"]       = "\uE129",
        ["link"]       = "\uE71B",
        ["share"]      = "\uE72D",
        ["copy"]       = "\uE8C8",
        ["paste"]      = "\uE77F",
        ["cut"]        = "\uE8C6",
        ["undo"]       = "\uE7A7",
        ["redo"]       = "\uE7A6",
        ["refresh"]    = "\uE72C",
        ["sync"]       = "\uE895",

        // Navigation
        ["left"]       = "\uE76B",
        ["right"]      = "\uE76C",
        ["up"]         = "\uE70E",
        ["down"]       = "\uE70D",
        ["back"]       = "\uE72B",
        ["forward"]    = "\uE72A",
        ["more"]       = "\uE712",
        ["menu"]       = "\uE700",
        ["expand"]     = "\uE70D",
        ["collapse"]   = "\uE70E",

        // Category defaults
        ["gear"]       = "\uE713",
        ["apps"]       = "\uE74C",
        ["system"]     = "\uE770",
        ["tools"]      = "\uE90F",
        ["windows"]    = "\uE782",
    };

    /// <summary>
    /// Resolves an icon key to a glyph string. If the key is already a Unicode glyph
    /// (starts with \uE or is a single char), returns it directly.
    /// </summary>
    public static string ResolveGlyph(string? iconKey)
    {
        if (string.IsNullOrEmpty(iconKey))
            return "\uE737"; // Default app icon

        // If it's already a Unicode glyph character
        if (iconKey.Length <= 2 && iconKey[0] >= 0xE000)
            return iconKey;

        // Look up in glyph map
        if (GlyphMap.TryGetValue(iconKey, out var glyph))
            return glyph;

        // Return as-is (might be a direct glyph string from config)
        return iconKey;
    }

    /// <summary>
    /// Extracts the icon from an executable file using SHGetFileInfo.
    /// Returns null if extraction fails. Results are cached.
    /// </summary>
    public ImageSource? GetFileIcon(string filePath)
    {
        return _iconCache.GetOrAdd(filePath, path =>
        {
            try
            {
                var shinfo = new WindowInterop.SHFILEINFO();
                var result = WindowInterop.SHGetFileInfo(
                    path, 0, ref shinfo,
                    (uint)Marshal.SizeOf<WindowInterop.SHFILEINFO>(),
                    WindowInterop.SHGFI_ICON | WindowInterop.SHGFI_LARGEICON);

                if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                    return null;

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze(); // Make cross-thread safe
                    return source;
                }
                finally
                {
                    WindowInterop.DestroyIcon(shinfo.hIcon);
                }
            }
            catch
            {
                return null;
            }
        });
    }
}
