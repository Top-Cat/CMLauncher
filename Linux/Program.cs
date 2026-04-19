using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Sentry;

namespace Linux
{
    internal class Program
    {
        private static bool FilesLocked(IPlatformSpecific specific)
        {
            var file = Path.Combine(specific.GetDownloadFolder(), "chromapper", "ChroMapper");
            try
            {
                if (File.Exists(file))
                {
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        stream.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return true;
            }

            return false;
        }

        public static void WriteResourceToFile(string resourceName, string fileName)
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    resource.CopyTo(file);
                }
            }
        }

        public static void Main(string[] args)
        {
            using (SentrySdk.Init("https://76dcf1f2484f4839a78b3713420b5147@o462013.ingest.sentry.io/5556322"))
            {
                using (new Mutex(true, "CMLauncher", out var createdNew))
                {
                    if (!createdNew) return;

                    Console.WriteLine("CML Linux");

                    var specific = new LinuxSpecific(args);

                    if (!File.Exists(Path.Combine(specific.GetDownloadFolder(), "xdelta3.so")))
                    {
                        Console.WriteLine("Extracting xdelta3");
                        WriteResourceToFile("Linux.Costura32.xdelta3.so", Path.Combine(specific.GetDownloadFolder(), "xdelta3.so"));
                    }

                    int tries = 0;
                    while (tries++ < 3)
                    {
                        if (!FilesLocked(specific))
                        {
                            new Main(specific);

                            while (true)
                                Thread.Sleep(1000);

                            return;
                        }
                        Thread.Sleep(1000 * tries);
                    }

                    specific.Exit();
                }
            }
        }
    }
}
