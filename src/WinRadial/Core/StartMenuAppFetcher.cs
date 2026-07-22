using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace WinRadial.Core;

public class AppInfo
{
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
}

public static class StartMenuAppFetcher
{
    public static List<AppInfo> GetInstalledApps()
    {
        var apps = new List<AppInfo>();
        var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

        var directories = new[] { userStartMenu, commonStartMenu }.Where(Directory.Exists).Distinct();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in directories)
        {
            try
            {
                var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var appName = Path.GetFileNameWithoutExtension(file);
                    
                    // Skip if we already have an app with this name
                    if (seenNames.Contains(appName))
                        continue;

                    var targetPath = ResolveShortcut(file);
                    if (!string.IsNullOrEmpty(targetPath) && targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(targetPath))
                        {
                            apps.Add(new AppInfo
                            {
                                Name = appName,
                                ExecutablePath = targetPath
                            });
                            seenNames.Add(appName);
                        }
                    }
                }
            }
            catch
            {
                // Ignore access denied etc.
            }
        }

        return apps.OrderBy(a => a.Name).ToList();
    }

    private static string ResolveShortcut(string shortcutPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(shortcutPath, 0);

            var sb = new StringBuilder(512); 
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    internal interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFileName);
    }
}
