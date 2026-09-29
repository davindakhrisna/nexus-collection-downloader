using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NexusCollectionDownloader;

internal readonly record struct ScreenHit(Point Point, nint Window);

internal static class ScreenMatcher
{
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint RootWindow = 2;

    public static ScreenHit? Find(ImageTemplate template, nint requiredWindow = 0)
    {
        Rectangle? windowBounds = null;
        if (requiredWindow != 0)
        {
            if (!IsWindowOpen(requiredWindow) || !GetWindowRect(requiredWindow, out var rect)) return null;
            windowBounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        foreach (var display in Screen.AllScreens)
        {
            var bounds = windowBounds is null ? display.Bounds : Rectangle.Intersect(display.Bounds, windowBounds.Value);
            if (bounds.Width < template.Bitmap.Width || bounds.Height < template.Bitmap.Height) continue;

            using var shot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(shot))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            var point = Match(shot, template.Bitmap, candidate =>
            {
                if (requiredWindow == 0) return true;
                var absolute = new Point(bounds.Left + candidate.X + template.Bitmap.Width / 2,
                    bounds.Top + candidate.Y + template.Bitmap.Height / 2);
                return GetAncestor(WindowFromPoint(absolute), RootWindow) == requiredWindow;
            });
            if (point is null) continue;

            var absolute = new Point(bounds.Left + point.Value.X + template.Bitmap.Width / 2,
                bounds.Top + point.Value.Y + template.Bitmap.Height / 2);
            var window = GetAncestor(WindowFromPoint(absolute), RootWindow);
            return new ScreenHit(absolute, window);
        }
        return null;
    }

    public static void Click(Point point)
    {
        if (!SetCursorPos(point.X, point.Y))
            throw new InvalidOperationException("Could not move the mouse to the matched button.");
        mouse_event(MouseLeftDown, 0, 0, 0, 0);
        Thread.Sleep(45);
        mouse_event(MouseLeftUp, 0, 0, 0, 0);
    }

    public static bool IsWindowOpen(nint window) => window != 0 && IsWindow(window);

    private static Point? Match(Bitmap screen, Bitmap template, Func<Point, bool> accepts)
    {
        var pixels = CopyPixels(screen, out var screenStride);
        var target = CopyPixels(template, out var targetStride);
        var width = template.Width;
        var height = template.Height;
        var anchors = new (int X, int Y)[]
        {
            (width / 2, height / 2), (width / 4, height / 4),
            (width * 3 / 4, height / 4), (width / 4, height * 3 / 4),
            (width * 3 / 4, height * 3 / 4)
        };

        for (var y = 0; y <= screen.Height - height; y++)
        for (var x = 0; x <= screen.Width - width; x++)
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

    private static int[] CopyPixels(Bitmap bitmap, out int stride)
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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extraInfo);
}
