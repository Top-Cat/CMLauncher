using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public static class HttpClientExtensions
{
    public static HttpClient Setup(this HttpClient client)
    {
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.Add("User-Agent", $"CML v{Config.APP_VERSION}");
        return client;
    }

    public static async Task DownloadAsync(
        this HttpClient client, 
        string requestUri, 
        Stream destination,
        EtagValidation etagValidation,
        IProgress<float> progress = null,
        CancellationToken cancellationToken = default)
    {
        // Get the http headers first to examine the content length
        using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            var contentLength = response.Content.Headers.ContentLength;
            var etag = response.Headers.ETag;

            using (var download = await response.Content.ReadAsStreamAsync())
            {
                // Reroute download stream for etag validation
                Stream source = etag != null && !etag.IsWeak && etag.Tag.Length != 0 ?
                        new EtagValidatingStream(download, etagValidation, etag.Tag) : download;

                // Ignore progress reporting when no progress reporter was 
                // passed or when the content length is unknown
                if (progress == null || !contentLength.HasValue)
                {
                    await source.CopyToAsync(destination);
                    return;
                }

                // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
                // Use extension method to report progress while downloading
                await source.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
                progress.Report(1);
            }
        }
    }
}