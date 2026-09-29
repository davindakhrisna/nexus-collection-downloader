using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NexusCollectionDownloader;

internal enum WindowKind { Vortex, Browser }
internal readonly record struct ScreenHit(Point Point, int ProcessId);

internal static class ScreenMatcher
{
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint RootWindow = 2;
    private static readonly HashSet<string> BrowserNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "chromium",
        "waterfox", "librewolf", "floorp", "zen", "arc"
    };

    public static bool IsBrowserProcess(string name) => BrowserNames.Contains(name);

    public static ScreenHit? Find(IReadOnlyList<ImageTemplate> templates, WindowKind kind)
    {
        foreach (var display in Screen.AllScreens)
        {
            var bounds = display.Bounds;
            using var shot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(shot))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            var pixels = CopyPixels(shot, out var stride);
            foreach (var template in templates)
            {
                if (bounds.Width < template.Bitmap.Width || bounds.Height < template.Bitmap.Height) continue;
                ScreenHit? hit = null;
                Match(pixels, stride, bounds.Width, bounds.Height, template, candidate =>
                {
                    var absolute = new Point(bounds.Left + candidate.X + template.Bitmap.Width / 2,
                        bounds.Top + candidate.Y + template.Bitmap.Height / 2);
                    var window = GetAncestor(WindowFromPoint(absolute), RootWindow);
                    if (window == 0) return false;
                    GetWindowThreadProcessId(window, out var processId);
                    if (!IsTargetProcess(processId, kind)) return false;
                    hit = new ScreenHit(absolute, (int)processId);
                    return true;
                });
                if (hit is not null) return hit;
            }
        }
        return null;
    }

    private static bool IsTargetProcess(uint processId, WindowKind kind)
    {
        if (processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return kind == WindowKind.Vortex
                ? process.ProcessName.Equals("Vortex", StringComparison.OrdinalIgnoreCase)
                : IsBrowserProcess(process.ProcessName);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    public static void Click(Point point)
    {
        if (!SetCursorPos(point.X, point.Y))
            throw new InvalidOperationException("Could not move the mouse to the matched button.");
        mouse_event(MouseLeftDown, 0, 0, 0, 0);
        Thread.Sleep(45);
        mouse_event(MouseLeftUp, 0, 0, 0, 0);
    }

    private static Point? Match(int[] pixels, int screenStride, int screenWidth, int screenHeight,
        ImageTemplate template, Func<Point, bool> accepts)
    {
        var target = template.Pixels;
        var targetStride = template.Stride;
        var width = template.Bitmap.Width;
        var height = template.Bitmap.Height;
        var anchors = new (int X, int Y)[]
        {
            (width / 2, height / 2), (width / 4, height / 4),
            (width * 3 / 4, height / 4), (width / 4, height * 3 / 4),
            (width * 3 / 4, height * 3 / 4)
        };

        for (var y = 0; y <= screenHeight - height; y++)
        for (var x = 0; x <= screenWidth - width; x++)
        {
            var start = y * screenStride + x;
            var anchored = true;
            foreach (var (ax, ay) in anchors)
            {
                if (Near(pixels[start + ay * screenStride + ax], target[ay * targetStride + ax])) continue;
                anchored = false;
                break;
            }
            if (!anchored) continue;

            var checkedPixels = 0;
            var misses = 0;
            for (var ty = 0; ty < height; ty += 2)
            {
                for (var tx = 0; tx < width; tx += 2)
                {
                    checkedPixels++;
                    if (!Near(pixels[start + ty * screenStride + tx], target[ty * targetStride + tx])) misses++;
                }
                if (misses > checkedPixels * 0.04 + 3) break;
            }
            if (misses <= checkedPixels * 0.04 + 3 && accepts(new Point(x, y))) return new Point(x, y);
        }
        return null;
    }

    internal static int[] CopyPixels(Bitmap bitmap, out int stride)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            stride = data.Stride / 4;
            var pixels = new int[stride * bitmap.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static bool Near(int a, int b) =>
        Math.Abs((a & 255) - (b & 255)) <= 28 &&
        Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255)) <= 28 &&
        Math.Abs(((a >> 16) & 255) - ((b >> 16) & 255)) <= 28;

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extraInfo);
}
