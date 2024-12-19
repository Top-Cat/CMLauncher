using System;
using Foundation;
using Xamarin.Essentials;

namespace OSX
{
    class Version : IVersion
    {
        public int VersionNumber { get; private set; } = 0;
        public string VersionServer { get; private set; } = "";

        private readonly static string VERSION_NUMBER_KEY = "cm_version";
        private readonly static string VERSION_SERVER_KEY = "cm_version_server";
        private static Version version = null;

        public static Version GetVersion()
        {
            if (version == null)
            {
                version = new Version();
            }
            return version;
        }

        private Version()
        {
            VersionNumber = Preferences.Get(VERSION_NUMBER_KEY, 0);
            VersionServer = Preferences.Get(VERSION_SERVER_KEY, "");
        }

        void IVersion.Update(int version, string server)
        {
            Preferences.Set(VERSION_NUMBER_KEY, version);
            Preferences.Set(VERSION_SERVER_KEY, server);

            VersionNumber = version;
            VersionServer = server;
        }
    }
}