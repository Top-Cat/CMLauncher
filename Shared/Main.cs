using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BsDiff;
using Sentry;
using SharpCompress.Readers;

public class Main : IProgress<float>
{
    private readonly ReleaseChannel useChannel;
    private readonly IPlatformSpecific platformSpecific;
    private readonly string cdnUrl;

    public Main(IPlatformSpecific platformSpecific)
    {
        this.platformSpecific = platformSpecific;

        var mainNode = platformSpecific.GetCMConfig();
        useChannel = mainNode["ReleaseChannel"].Value == "1" ? ReleaseChannel.Dev : ReleaseChannel.Stable;
        cdnUrl = mainNode.HasKey("ReleaseServer") ? mainNode["ReleaseServer"].Value : Config.CDN_URL;

        new Thread(DoUpdate).Start();
    }

    private async Task<int> GetLatestBuildNumber(ReleaseChannel releaseChannel)
    {
        using (var client = new HttpClient().Setup())
        {
            var channel = releaseChannel == ReleaseChannel.Stable ? "stable" : "dev";

            using (var response = await client.GetAsync($"{cdnUrl}/{channel}"))
            {
                using (var content = response.Content)
                {
                    return int.Parse(await content.ReadAsStringAsync());
                }
            }
        }
    }

    private void SetVersion(int version)
    {
        platformSpecific.GetVersion().Update(version, cdnUrl);
    }

    private async void DoUpdate()
    {
        try
        {
            platformSpecific.CleanupUpdate();
            await new UpdateManager(platformSpecific).CheckForUpdates();
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
        }

        try {
            var version = platformSpecific.GetVersion();
            var current = version.VersionNumber;
            var desired = await GetLatestBuildNumber(useChannel);

            /*
             * Update if:
             *  - Our version server does not match (possibly because we have no current version)
             *  - We have an old version
             *  - We have a newer version but we want to be on the stable build
             */
            if (version.VersionServer != cdnUrl || current < desired || (current > desired && useChannel == ReleaseChannel.Stable))
            {
                PerformUpdate(current, desired);
                return;
            }
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
        }

        platformSpecific.Exit();
    }

    private async void PerformUpdate(int current, int desired)
    {
        var stable = await GetLatestBuildNumber(ReleaseChannel.Stable);
        var version = platformSpecific.GetVersion();

        // Downgrade or first run
        if (version.VersionServer != cdnUrl || current > desired)
        {
            current = await UpdateUsingZip(stable);
        }

        // We need to update
        if (current < desired)
        {
            var patches = await FindPath(platformSpecific.GetCDNPrefix(), current, desired);

            // That's a lot of patches
            if (current < stable && (patches == null || patches.Count > Config.PATCH_SKIP_LIMIT))
            {
                if (stable == desired)
                {
                    // Too many patches just download it, triggers the zip update code path below
                    patches = null;
                }
                else
                {
                    // Check how many patches we would save
                    var currentPatches = await FindPath(platformSpecific.GetCDNPrefix(), stable, desired);
                    if (patches == null || patches.Count - currentPatches.Count > Config.PATCH_SKIP_LIMIT)
                    {
                        // We'll save having to do many patches if we download the stable zip
                        current = await UpdateUsingZip(stable);
                        patches = currentPatches;
                    }
                }
            }

            if (patches == null)
            {
                // We can't patch to the desired version
                // If we're not on stable go there
                if (current != stable)
                {
                    current = await UpdateUsingZip(stable);
                }

                // Abandon ship!
                // Hopefully someone creates an update path for us next time around
                platformSpecific.Exit();
                return;
            }

            try
            {
                foreach (var patch in patches)
                {
                    current = await UpdateUsingPatch(current, patch);
                }
            }
            catch (AggregateException e)
            {
                SentrySdk.CaptureException(e);
                // Files are almost certainly between builds so
                // require an update before running again
                SetVersion(0);

                // Bail back to stable
                current = await UpdateUsingZip(stable);
            }
        }

        platformSpecific.Exit();
    }

    private async Task<List<int>> FindPath(string prefix, int current, int desired)
    {
        var regex = new Regex(prefix + @"[0-9]+/([0-9]+).patch");
        var patches = new List<int>();

        using (var client = new HttpClient().Setup())
        {
            using (var response = await client.GetAsync($"{cdnUrl}?prefix={prefix}{desired}/"))
            {
                using (var content = response.Content)
                {
                    var stream = await content.ReadAsStreamAsync();
                    var xReader = XmlReader.Create(stream, new XmlReaderSettings
                    {
                        Async = true
                    });

                    while (xReader.ReadToFollowing("Contents"))
                    {
                        xReader.ReadToFollowing("Key");
                        var str = await xReader.ReadElementContentAsStringAsync();
                        var matches = regex.Matches(str);

                        if (matches.Count > 0)
                        {
                            var possible = int.Parse(matches[0].Groups[1].Value);
                            patches.Add(possible);
                        }
                    }
                }
            }
        }

        if (patches.Contains(current))
        {
            // We can patch from here!
            var ret = new List<int>
                {
                    desired
                };
            return ret;
        }
        else if (patches.Count > 50)
        {
            // You're just asking for a stack overflow exception
            return null;
        }

        try
        {
            var oldest = patches.Where(a => a > current).Min();
            var ret2 = await FindPath(prefix, current, oldest);

            ret2?.Add(desired);

            return ret2;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<int> UpdateUsingZip(int version)
    {
        platformSpecific.UpdateLabel("Downloading update...");
        var etag = platformSpecific.UseCDN() ? EtagValidation.HexMd5 : EtagValidation.None;
        string downloadUrl = platformSpecific.UseCDN() ? $"{cdnUrl}/{platformSpecific.GetCDNPrefix()}{version}/{platformSpecific.GetCDNFilename()}" :
            $"https://jenkins.kirkstall.top-cat.me/job/ChroMapper/{version}/artifact/{platformSpecific.GetJenkinsFilename()}";

        using (var tmp = new TempFile())
        {
            using (var client = new HttpClient().Setup())
            {
                using (var file = new FileStream(tmp.Path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync(downloadUrl, file, this, etagValidation: etag);
                }
            }

            ExtractZip(tmp.Path);

            SetVersion(version);

            return version;
        }
    }

    private void ExtractZip(string filename)
    {
        platformSpecific.UpdateLabel("Extracting zip");
        Report(0);

        string destinationDirectoryFullPath = platformSpecific.GetDownloadFolder();

        using (Stream stream = File.OpenRead(filename))
        {
            var reader = ReaderFactory.Open(stream);
            reader.Patches().AsParallel().Select(patch =>
            {
                var keyFilename = patch.FileName;

                var completeFileName = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, keyFilename));

                if (!completeFileName.StartsWith(destinationDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Lets just ignore it
                    //throw new IOException("Trying to extract file outside of destination directory");
                }
                else if (keyFilename == "")
                {
                    // Probably a directory
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));
                    File.WriteAllBytes(completeFileName, patch.Stream.ToArray());
                }

                patch.FileName = patch.FileName.Replace("chromapper/", "").Replace("ChroMapper.app/", "");
                return patch;
            }).WithProgressReporting(stream.Length, this, "Extracting", platformSpecific.UpdateLabel).ForAll(p => { });
        }
    }

    private async Task<int> UpdateUsingPatch(int source, int dest)
    {
        platformSpecific.UpdateLabel($"Downloading patch for {dest}");
        var etag = platformSpecific.UseCDN() ? EtagValidation.HexMd5 : EtagValidation.None;
        string downloadUrl = $"{cdnUrl}/{platformSpecific.GetCDNPrefix()}{dest}/{source}.patch";

        using (var tmp = new TempFile())
        {
            using (var client = new HttpClient().Setup())
            {
                using (var file = new FileStream(tmp.Path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync(downloadUrl, file, this, etagValidation: etag);
                }
            }

            ApplyPatch(tmp.Path);
            SetVersion(dest);

            return dest;
        }
    }

    private void ApplyPatch(string filename)
    {
        Report(0);
        using (Stream stream = File.OpenRead(filename))
        {
            var reader = ReaderFactory.Open(stream);
            reader.Patches().AsParallel().Select(patch =>
            {
                var memStream = patch.Stream;
                var keyFilename = patch.FileName;

                var patchFilename = keyFilename;
                var compressionType = keyFilename.Substring(0, keyFilename.IndexOf("/"));

                if (compressionType == "xdelta" || compressionType == "bsdiff")
                {
                    patch.FileName = keyFilename = keyFilename.Substring(compressionType.Length + 1);
                    patchFilename = patchFilename.Replace(compressionType, platformSpecific.LocalFolderName()).TrimStart('/');
                }
                else
                {
                    compressionType = "";
                    patchFilename = Path.Combine(platformSpecific.LocalFolderName(), patchFilename);
                }

                patchFilename = Path.Combine(platformSpecific.GetDownloadFolder(), patchFilename);

                byte[] newFile = memStream.ToArray();

                if (compressionType == "xdelta")
                {
                    byte[] oldFile = File.Exists(patchFilename) ? File.ReadAllBytes(patchFilename) : new byte[] { };
                    newFile = xdelta3.ApplyPatch(keyFilename, memStream.ToArray(), oldFile);
                }
                else if (compressionType == "bsdiff")
                {
                    using (var input = new FileStream(patchFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var memStream2 = new MemoryStream(100))
                    {
                        BinaryPatchUtility.Apply(input, () => new MemoryStream(newFile), memStream2);
                        newFile = memStream2.ToArray();
                    }
                }
                if (!Directory.Exists(patchFilename))
                    Directory.CreateDirectory(Path.GetDirectoryName(patchFilename));
                File.WriteAllBytes(patchFilename, newFile);

                return patch;
            }).WithProgressReporting(stream.Length, this, "Patching", platformSpecific.UpdateLabel).ForAll(p => { });
        }
    }

    public void Report(float value)
    {
        platformSpecific.UpdateProgress(value);
    }
}