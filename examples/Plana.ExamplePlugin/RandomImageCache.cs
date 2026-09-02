using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

internal sealed class RandomImageCache : IAsyncDisposable
{
    private const int TargetCount = 5;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://pln.yuelili.com"), Timeout = TimeSpan.FromSeconds(15) };
    private readonly Channel<string> _ready = Channel.CreateBounded<string>(new BoundedChannelOptions(TargetCount) { SingleReader = true, SingleWriter = true });
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _refill = new(0, int.MaxValue);
    private readonly CancellationTokenSource _stop = new();
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanaDesktop", "plugin-cache", "plana.random-images");
    private Task? _worker;

    public void Start()
    {
        Directory.CreateDirectory(_directory);
        if (_worker is not null) return;
        foreach (var file in new DirectoryInfo(_directory).EnumerateFiles()
                     .Where(file => file.Extension.ToLowerInvariant() is ".webp" or ".png" or ".jpg" or ".jpeg")
                     .OrderByDescending(file => file.LastWriteTimeUtc)
                     .Take(TargetCount))
            _ready.Writer.TryWrite(file.FullName);
        _worker = Task.Run(() => FillLoopAsync(_stop.Token));
    }

    public async Task<string> TakeAsync(CancellationToken token = default)
    {
        Start();
        var started = DateTime.UtcNow;
        var path = await _ready.Reader.ReadAsync(token);
        _refill.Release();
        var remaining = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - started);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token);
        return path;
    }

    private async Task FillLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            while (_ready.Reader.Count < TargetCount && !token.IsCancellationRequested)
            {
                var urls = await FetchUrlsAsync(token);
                foreach (var url in urls)
                {
                    if (_ready.Reader.Count >= TargetCount) break;
                    if (!_seen.TryAdd(url, 0)) continue;
                    try { await _ready.Writer.WriteAsync(await DownloadAsync(url, token), token); }
                    catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException) { }
                }
                if (urls.Count == 0) await Task.Delay(1000, token);
            }
            await _refill.WaitAsync(token);
        }
    }

    private async Task<IReadOnlyList<string>> FetchUrlsAsync(CancellationToken token)
    {
        try
        {
            using var response = await _http.GetAsync("/api/v1/artworks/random?limit=8", token);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
            return document.RootElement.GetProperty("data").EnumerateArray()
                .Select(item => item.GetProperty("preview_url").GetString()).Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new Uri(_http.BaseAddress!, path!).AbsoluteUri).ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException) { return []; }
    }

    private async Task<string> DownloadAsync(string url, CancellationToken token)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        var existing = Directory.EnumerateFiles(_directory, $"{key}.*").FirstOrDefault();
        if (existing is not null) return existing;
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var extension = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() switch { "image/webp" => ".webp", "image/png" => ".png", "image/jpeg" => ".jpg", _ => throw new IOException("Unsupported image type.") };
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        if (bytes.Length > 15 * 1024 * 1024) throw new IOException("Image exceeds 15 MiB.");
        var path = Path.Combine(_directory, key + extension);
        await File.WriteAllBytesAsync(path, bytes, token);
        foreach (var file in new DirectoryInfo(_directory).EnumerateFiles().OrderByDescending(file => file.LastWriteTimeUtc).Skip(32)) try { file.Delete(); } catch (IOException) { }
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_worker is not null) try { await _worker; } catch (OperationCanceledException) { }
        _http.Dispose(); _refill.Dispose(); _stop.Dispose();
    }
}
