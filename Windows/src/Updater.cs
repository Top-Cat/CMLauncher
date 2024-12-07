using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CM_Launcher
{
    public partial class Updater : Form
    {
        private readonly SynchronizationContext synchronizationContext;
        public static FormWindowState OriginalWindowState;

        private static bool FilesLocked(IPlatformSpecific specific, bool isAdmin)
        {
            var folder = Path.Combine(specific.GetDownloadFolder(), "chromapper");
            var file = Path.Combine(folder, "ChroMapper.exe");
            try
            {
                if (!File.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (File.Exists(file))
                {
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        stream.Close();
                    }
                }
                else
                {
                    specific.GetVersion().Update(0, "");
                    File.WriteAllBytes(file, Array.Empty<byte>());
                    File.Delete(file);
                }
            }
            catch (Exception e)
            {
                if (isAdmin) MessageBox.Show(e.Message);
                return true;
            }

            return false;
        }

        protected override void OnLoad(EventArgs e)
        {
            OriginalWindowState = WindowState;
            WindowState = FormWindowState.Normal;
            base.OnLoad(e);
        }

        public Updater(string[] args, bool isAdmin)
        {
            InitializeComponent();
            synchronizationContext = SynchronizationContext.Current;

            var specific = new WindowsSpecific(this, args);

            if (!FilesLocked(specific, isAdmin))
            {
                new Main(specific);
                return;
            }

            if (!isAdmin)
            {
                specific.StartAsAdmin();
            }

            new Thread(() =>
            {
                specific.Exit();
            }).Start();
        }

        public void UpdateLabel(string text)
        {
            synchronizationContext.Post(o =>
            {
                label1.Text = $"{o}";
            }, text);
        }

        public void Report(float value)
        {
            synchronizationContext.Post(o =>
            {
                progressBar1.Value = Math.Min((int) o, 1000);
            }, (int)(value * 1000));
        }
    }
}
