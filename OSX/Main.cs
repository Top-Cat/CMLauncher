using System.Linq;
using AppKit;
using Sentry;

namespace OSX
{
    static class MainClass
    {
        public static bool CleanDownload = false;

        static void Main(string[] args)
        {
            CleanDownload = args.Any("--clean".Contains);
            using (SentrySdk.Init("https://76dcf1f2484f4839a78b3713420b5147@o462013.ingest.sentry.io/5556322"))
            {
                NSApplication.Init();
                NSApplication.Main(args);
            }
        }
    }
}
