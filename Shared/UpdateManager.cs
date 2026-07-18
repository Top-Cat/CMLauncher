using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public class UpdateManager
{
    private readonly IPlatformSpecific platform;

    public UpdateManager(IPlatformSpecific platform)
    {
        this.platform = platform;
    }

    public async Task CheckForUpdates()
    {
        using (var client = new HttpClient().Setup())
        {
            using (var response = await client.GetAsync($"{Config.CDN_URL}/cml"))
            {
                using (var content = response.Content)
                {
                    var cmlVer = int.Parse(await content.ReadAsStringAsync());
                    if (cmlVer > Config.APP_VERSION)
                    {
                        await DownloadUpdate();
                    }
                }
            }
        }
    }

    private async Task DownloadUpdate()
    {
        using (var tmp = new TempFile())
        {
            using (var client = new HttpClient().Setup())
            {
                using (var file = new FileStream(tmp.Path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync($"{Config.CDN_URL}/{platform.GetCMLFilename()}", file, EtagValidation.HexMd5);
                }
            }

            platform.Restart(tmp.Path);
        }
    }
}