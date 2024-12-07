using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using MS.WindowsAPICodePack.Internal;
using Sentry;
using Sentry.Protocol;

namespace CM_Launcher
{
    internal static class Program
    {
        private static readonly string CmExeName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromapper", "ChroMapper.exe");
        private static readonly PropertyKey AppUserModelId = new PropertyKey(new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}"), 5);

        [DllImport("shell32.dll")]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        private static void UpdatePinnedTaskBarElements()
        {
            var pinnedTaskBarItemsPath = Environment.ExpandEnvironmentVariables(@"%AppData%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
            var pinnedTaskBarFiles = Directory.GetFiles(pinnedTaskBarItemsPath);

            var currentExe = typeof(Program).Assembly.Location;
            foreach (var pinnedPath in pinnedTaskBarFiles)
            {
                try
                {
                    var newShortcut = (IShellLinkW) new CShellLink();
                    var newShortcutSave = (IPersistFile) newShortcut;
                    ErrorHelper.VerifySucceeded(newShortcutSave.Load(pinnedPath, STGM.STGM_READWRITE));

                    var sb = new StringBuilder(1024);
                    ErrorHelper.VerifySucceeded(newShortcut.GetPath(sb, 1024, IntPtr.Zero, 4));

                    // If CM is pinned, make it the launcher
                    if (sb.ToString().Equals(CmExeName))
                    {
                        ErrorHelper.VerifySucceeded(newShortcut.SetArguments(""));
                        ErrorHelper.VerifySucceeded(newShortcut.SetPath(currentExe));
                    } else if (sb.ToString() != currentExe) continue;

                    var newShortcutProperties = (IPropertyStore) newShortcut;
                    using (var appId = new PropVariant(CmExeName))
                    {
                        ErrorHelper.VerifySucceeded(newShortcutProperties.SetValue(AppUserModelId, appId));
                        ErrorHelper.VerifySucceeded(newShortcutProperties.Commit());
                    }

                    ErrorHelper.VerifySucceeded(newShortcutSave.Save(pinnedPath, true));
                }
                catch (Exception)
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            using (SentrySdk.Init("https://76dcf1f2484f4839a78b3713420b5147@o462013.ingest.sentry.io/5556322"))
            {
                var identity = WindowsIdentity.GetCurrent();
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.User = new User
                    {
                        Username = identity.Name
                    };
                });

                using (new Mutex(true, "CMLauncher", out var createdNew))
                {
                    var isAdmin = WindowsSpecific.IsAdministrator();
                    if (isAdmin)
                    {
                        if (createdNew)
                        {
                            MessageBox.Show("Don't run CML as admin");
                            return;
                        }

                        LaunchApp(args, true);
                    }
                    else
                    {
                        if (!createdNew) return;

                        SetCurrentProcessExplicitAppUserModelID(CmExeName);
                        UpdatePinnedTaskBarElements();

                        LaunchApp(args, false);
                    }
                }
            }
        }

        private static void LaunchApp(string[] args, bool isAdmin)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Updater(args, isAdmin));
        }
    }
}
