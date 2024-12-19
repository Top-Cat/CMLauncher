using System;
using System.Diagnostics;
using System.IO;
using AppKit;
using Foundation;
using SharpCompress.Archives;
using SharpCompress.Common;
using SimpleJSON;

public class MacSpecific : IPlatformSpecific
{
    private NSTextField progressLabel;
    private NSProgressIndicator progressBar;

    public MacSpecific(NSTextField progressLabel, NSProgressIndicator progressBar)
    {
        this.progressLabel = progressLabel;
        this.progressBar = progressBar;
    }

    public IVersion GetVersion()
    {
        return OSX.Version.GetVersion();
    }

    public JSONNode GetCMConfig()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string cmSettingsPath = Path.Combine(homeDir, "Library", "Application Support", "com.BinaryElement.ChroMapper", "ChroMapperSettings.json");

        if (!File.Exists(cmSettingsPath))
        {
            return new JSONObject();
        }

        using (StreamReader reader = new StreamReader(cmSettingsPath))
        {
            return JSON.Parse(reader.ReadToEnd());
        }
    }

    public string GetCDNPrefix()
    {
        return "osx/";
    }

    public bool UseCDN() => true;

    public void UpdateLabel(string label)
    {
        NSApplication.SharedApplication.InvokeOnMainThread(() =>
        {
            progressLabel.StringValue = label;
        });
    }

    public void UpdateProgress(float progress)
    {
        NSApplication.SharedApplication.InvokeOnMainThread(() =>
        {
            progressBar.Indeterminate = false;
            progressBar.DoubleValue = progress * 100;
        });
    }

    public void Exit()
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"chmod +x {GetDownloadFolder()}/ChroMapper.app/Contents/MacOS/ChroMapper\"",

            CreateNoWindow = true
        };
        Process.Start(startInfo).WaitForExit();

        var startInfo2 = new ProcessStartInfo($"{GetDownloadFolder()}/ChroMapper.app/Contents/MacOS/ChroMapper")
        {
            WorkingDirectory = GetDownloadFolder(),
            Arguments = $"--launcher \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName)}\""
        };

        Process.Start(startInfo2);

        NSApplication.SharedApplication.InvokeOnMainThread(() =>
        {
            NSApplication.SharedApplication.Terminate(NSApplication.SharedApplication);
        });
    }

    public string GetJenkinsFilename()
    {
        return "build/ChroMapper-MacOS.tar.gz";
    }

    public string GetCDNFilename()
    {
        return "MacOS.tar.gz";
    }

    public string GetDownloadFolder()
    {
        var homeDir = Environment.GetEnvironmentVariable("HOME");
        return $"{homeDir}/Applications";
    }

    public string LocalFolderName()
    {
        return "";
    }

    public string GetCMLFilename()
    {
        return "CML.app.zip";
    }

    public void Restart(string tmpFile)
    {
        var newApp = GetCMLPath();
        var oldApp = GetTempCMLPath();
        var dir = Directory.GetParent(NSBundle.MainBundle.BundlePath).FullName;

        var tempApp = Path.Combine(Path.GetTempPath(), "CML.app");
        DeleteFSObject(tempApp);
        var archive = ArchiveFactory.Open(tmpFile);
        archive.WriteToDirectory(Path.GetTempPath(), new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });

        Directory.Move(newApp, oldApp);
        Directory.Move(tempApp, newApp);

        var startInfo = new ProcessStartInfo()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"chmod +x {newApp.Replace(" ", "\\ ")}/Contents/MacOS/CM\\ Launcher\"",

            CreateNoWindow = true
        };
        Process.Start(startInfo).WaitForExit();

        var startInfo2 = new ProcessStartInfo(Path.Combine(newApp, "Contents", "MacOS", "CM Launcher"))
        {
            WorkingDirectory = dir
        };

        Process.Start(startInfo2);

        NSApplication.SharedApplication.InvokeOnMainThread(() =>
        {
            NSApplication.SharedApplication.Terminate(NSApplication.SharedApplication);
        });
    }

    public void CleanupUpdate()
    {
        try
        {
            var oldExe = GetTempCMLPath();
            if (Directory.Exists(oldExe))
            {
                Directory.Delete(oldExe, true);
            }
            if (File.Exists(oldExe))
            {
                File.Delete(oldExe);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public void DeleteFSObject(string fsPath)
    {
        try
        {
            if (Directory.Exists(fsPath))
            {
                Directory.Delete(fsPath, true);
            }
            if (File.Exists(fsPath))
            {
                File.Delete(fsPath);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private string GetTempCMLPath()
    {
        return Path.Combine(Directory.GetParent(NSBundle.MainBundle.BundlePath).FullName, "CML.old.app");
    }

    private string GetCMLPath()
    {
        return NSBundle.MainBundle.BundlePath;
    }
}
