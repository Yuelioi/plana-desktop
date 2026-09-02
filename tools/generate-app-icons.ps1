param(
    [Parameter(Mandatory)][string]$Source,
    [string]$ControlCenterAssets = "$PSScriptRoot\..\src\Plana.ControlCenter\Assets",
    [string]$BrandAssets = "$PSScriptRoot\..\src\Plana.Brand",
    [switch]$PreserveBackground
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$drawingAssembly = [System.Drawing.Bitmap].Assembly.Location
$drawingPrimitivesAssembly = [System.Drawing.Color].Assembly.Location
$gdiPlusAssembly = Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'
$windowsCoreAssembly = Join-Path $PSHOME 'System.Private.Windows.Core.dll'
$collectionsAssembly = Join-Path $PSHOME 'System.Collections.dll'
$coreAssembly = [object].Assembly.Location
Add-Type -ReferencedAssemblies $drawingAssembly,$drawingPrimitivesAssembly,$gdiPlusAssembly,$windowsCoreAssembly,$collectionsAssembly,$coreAssembly -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class PlanaIconPipeline {
    public static void Generate(string source, string control, string brand, bool preserveBackground) {
        Directory.CreateDirectory(control);
        Directory.CreateDirectory(brand);
        using var original = new Bitmap(source);
        using var cleaned = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(cleaned)) graphics.DrawImageUnscaled(original, 0, 0);
        if (!preserveBackground) ClearConnectedCheckerboard(cleaned);
        using var master = Resize(cleaned, 1024, 1024);
        master.Save(Path.Combine(brand, "AppIcon.png"), ImageFormat.Png);

        Save(master, Path.Combine(control, "Square150x150Logo.scale-200.png"), 300, 300);
        Save(master, Path.Combine(control, "Square44x44Logo.scale-200.png"), 88, 88);
        Save(master, Path.Combine(control, "Square44x44Logo.targetsize-24_altform-unplated.png"), 24, 24);
        Save(master, Path.Combine(control, "Square44x44Logo.targetsize-48_altform-lightunplated.png"), 48, 48);
        Save(master, Path.Combine(control, "StoreLogo.png"), 50, 50);
        Save(master, Path.Combine(control, "LockScreenLogo.scale-200.png"), 48, 48);
        SaveCanvas(master, Path.Combine(control, "Wide310x150Logo.scale-200.png"), 620, 300, 220);
        SaveCanvas(master, Path.Combine(control, "SplashScreen.scale-200.png"), 1240, 600, 300);

        var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        SaveIco(master, Path.Combine(control, "AppIcon.ico"), sizes);
        SaveIco(master, Path.Combine(brand, "AppIcon.ico"), sizes);
    }

    private static bool IsBackground(Color color) {
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        var min = Math.Min(color.R, Math.Min(color.G, color.B));
        return color.A > 0 && min > 215 && max - min < 18;
    }

    private static void ClearConnectedCheckerboard(Bitmap image) {
        var width = image.Width;
        var height = image.Height;
        var seen = new bool[width * height];
        var queue = new int[width * height];
        var head = 0;
        var tail = 0;
        void Add(int x, int y) { var index = y * width + x; if (!seen[index] && IsBackground(image.GetPixel(x, y))) { seen[index] = true; queue[tail++] = index; } }
        for (var x = 0; x < width; x++) { Add(x, 0); Add(x, height - 1); }
        for (var y = 0; y < height; y++) { Add(0, y); Add(width - 1, y); }
        while (head < tail) {
            var index = queue[head++];
            var x = index % width;
            var y = index / width;
            image.SetPixel(x, y, Color.Transparent);
            if (x > 0) Add(x - 1, y); if (x + 1 < width) Add(x + 1, y);
            if (y > 0) Add(x, y - 1); if (y + 1 < height) Add(x, y + 1);
        }
    }

    private static Bitmap Resize(Image source, int width, int height) {
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        return result;
    }

    private static void Save(Image source, string path, int width, int height) {
        using var result = Resize(source, width, height);
        result.Save(path, ImageFormat.Png);
    }

    private static void SaveCanvas(Image source, string path, int width, int height, int iconSize) {
        using var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var x = (width - iconSize) / 2;
        var y = (height - iconSize) / 2;
        graphics.DrawImage(source, new Rectangle(x, y, iconSize, iconSize));
        result.Save(path, ImageFormat.Png);
    }

    private static void SaveIco(Image source, string path, int[] sizes) {
        var frames = new byte[sizes.Length][];
        for (var frameIndex = 0; frameIndex < sizes.Length; frameIndex++) {
            var size = sizes[frameIndex];
            using var image = Resize(source, size, size);
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            frames[frameIndex] = stream.ToArray();
        }
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)frames.Length);
        var offset = 6 + frames.Length * 16;
        for (var index = 0; index < frames.Length; index++) {
            var size = sizes[index];
            writer.Write((byte)(size >= 256 ? 0 : size)); writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(frames[index].Length); writer.Write(offset); offset += frames[index].Length;
        }
        foreach (var frame in frames) writer.Write(frame);
    }
}
'@

[PlanaIconPipeline]::Generate(
    [IO.Path]::GetFullPath($Source),
    [IO.Path]::GetFullPath($ControlCenterAssets),
    [IO.Path]::GetFullPath($BrandAssets),
    $PreserveBackground.IsPresent)
