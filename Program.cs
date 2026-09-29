using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Windows.Forms;

namespace NexusCollectionDownloader;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox imageFolder = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly NumericUpDown fixedSeconds = Seconds(1.0m);
    private readonly NumericUpDown minimumSeconds = Seconds(1.0m);
    private readonly NumericUpDown maximumSeconds = Seconds(2.0m);
    private readonly NumericUpDown closeSeconds = Seconds(5.0m);
    private readonly CheckBox randomTiming = new() { Text = "Random interval", AutoSize = true };
    private readonly Button startButton = new() { Text = "Start", Width = 120, Height = 36 };
    private readonly Button stopButton = new() { Text = "Stop", Width = 120, Height = 36, Enabled = false };
    private readonly Label status = new() { Text = "Ready. Select a folder of Download button images.", AutoSize = true };
    private CancellationTokenSource? runCancellation;

    public MainForm()
    {
        Text = "Nexus Collection Downloader";
        ClientSize = new Size(720, 480);
        MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(248, 249, 251);
        ForeColor = Color.FromArgb(30, 38, 48);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 146));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Collection click assistant", Font = new Font("Segoe UI Semibold", 17f),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Choose a folder of Download button crops. The app clicks Vortex, then the browser, then forcefully ends that browser process.",
            Dock = DockStyle.Fill, AutoSize = false
        }, 0, 1);

        var files = new GroupBox { Text = "Download button images", Dock = DockStyle.Fill, Padding = new Padding(12, 17, 12, 10) };
        var fileGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        fileGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fileGrid.Controls.Add(Label("Image folder"), 0, 0);
        imageFolder.Margin = new Padding(3, 7, 3, 7);
        fileGrid.Controls.Add(imageFolder, 1, 0);
        var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill, Margin = new Padding(6, 5, 0, 5) };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Choose a folder containing Download button images" };
            if (dialog.ShowDialog(this) == DialogResult.OK) imageFolder.Text = dialog.SelectedPath;
        };
        fileGrid.Controls.Add(browse, 2, 0);
        files.Controls.Add(fileGrid);
        root.Controls.Add(files, 0, 2);

        var timing = new GroupBox { Text = "Timing (seconds)", Dock = DockStyle.Fill, Padding = new Padding(12, 17, 12, 10) };
        var timingGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) timingGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        timingGrid.Controls.Add(Label("Click interval"), 0, 0);
        timingGrid.Controls.Add(fixedSeconds, 1, 0);
        timingGrid.Controls.Add(randomTiming, 2, 0);
        timingGrid.Controls.Add(Label("Minimum"), 0, 1);
        timingGrid.Controls.Add(minimumSeconds, 1, 1);
        timingGrid.Controls.Add(Label("Maximum"), 2, 1);
        timingGrid.Controls.Add(maximumSeconds, 3, 1);
        timingGrid.Controls.Add(Label("Browser kill delay"), 0, 2);
        timingGrid.Controls.Add(closeSeconds, 1, 2);
        timingGrid.Controls.Add(new Label { Text = "Default: 5.0 s after browser Download", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray }, 2, 2);
        timingGrid.SetColumnSpan(timingGrid.GetControlFromPosition(2, 2)!, 2);
        timing.Controls.Add(timingGrid);
        root.Controls.Add(timing, 0, 3);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        startButton.BackColor = Color.FromArgb(211, 89, 43);
        startButton.ForeColor = Color.White;
        startButton.FlatStyle = FlatStyle.Flat;
        startButton.FlatAppearance.BorderSize = 0;
        stopButton.FlatStyle = FlatStyle.Flat;
        actions.Controls.Add(startButton);
        actions.Controls.Add(stopButton);
        root.Controls.Add(actions, 0, 4);

        var statusPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) };
        status.Dock = DockStyle.Fill;
        statusPanel.Controls.Add(status);
        root.Controls.Add(statusPanel, 0, 5);

        randomTiming.CheckedChanged += (_, _) => UpdateTimingFields();
        UpdateTimingFields();
        startButton.Click += StartClicked;
        stopButton.Click += (_, _) => runCancellation?.Cancel();
        FormClosing += (_, _) => runCancellation?.Cancel();
    }

    private static Label Label(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

    private static NumericUpDown Seconds(decimal value) => new()
    {
        DecimalPlaces = 1, Increment = 0.1m, Minimum = 0.1m, Maximum = 120m,
        Value = value, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Right
    };

    private void UpdateTimingFields()
    {
        fixedSeconds.Enabled = !randomTiming.Checked;
        minimumSeconds.Enabled = maximumSeconds.Enabled = randomTiming.Checked;
    }

    private async void StartClicked(object? sender, EventArgs e)
    {
        if (runCancellation is not null) return;
        if (randomTiming.Checked && minimumSeconds.Value > maximumSeconds.Value)
        {
            MessageBox.Show(this, "Minimum interval must not exceed maximum interval.", "Check timing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var images = new List<ImageTemplate>();
        try
        {
            if (!Directory.Exists(imageFolder.Text))
                throw new InvalidOperationException("Choose a folder containing Download button images.");
            foreach (var path in Directory.EnumerateFiles(imageFolder.Text).Where(path =>
                         new[] { ".png", ".bmp", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
                images.Add(ImageTemplate.Load(path));
            if (images.Count == 0)
                throw new InvalidOperationException("The selected folder has no PNG, BMP, or JPEG images.");
        }
        catch (Exception ex)
        {
            foreach (var image in images) image.Dispose();
            MessageBox.Show(this, ex.Message, "Check images", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        runCancellation = new CancellationTokenSource();
        startButton.Enabled = false;
        stopButton.Enabled = true;
        status.Text = $"Running with {images.Count} images. Restore this window from the taskbar to stop.";
        WindowState = FormWindowState.Minimized;

        try
        {
            var options = new RunOptions((double)fixedSeconds.Value, (double)minimumSeconds.Value,
                (double)maximumSeconds.Value, randomTiming.Checked, (double)closeSeconds.Value);
            await Task.Run(() => RunAsync(images, options, runCancellation.Token));
            status.Text = "Stopped.";
        }
        catch (OperationCanceledException)
        {
            status.Text = "Stopped.";
        }
        catch (Exception ex)
        {
            WindowState = FormWindowState.Normal;
            status.Text = "Paused: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Downloader paused", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            foreach (var image in images) image.Dispose();
            runCancellation.Dispose();
            runCancellation = null;
            startButton.Enabled = true;
            stopButton.Enabled = false;
        }
    }

    private async Task RunAsync(IReadOnlyList<ImageTemplate> images, RunOptions options, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var browserHit = ScreenMatcher.Find(images, WindowKind.Browser);
            if (browserHit is not null)
            {
                await HandleBrowserAsync(browserHit.Value, options, token);
                await DelayClickAsync(options, token);
                continue;
            }

            var vortexHit = ScreenMatcher.Find(images, WindowKind.Vortex);
            if (vortexHit is null)
            {
                Report("Waiting for Vortex Download…");
                await Task.Delay(350, token);
                continue;
            }

            Report("Clicked Vortex Download; waiting for browser…");
            ScreenMatcher.Click(vortexHit.Value.Point);
            await DelayClickAsync(options, token);
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                browserHit = ScreenMatcher.Find(images, WindowKind.Browser);
                if (browserHit is not null) break;
                if (DateTime.UtcNow >= deadline)
                    throw new InvalidOperationException("Browser Download was not found within 60 seconds. Check the image folder and window visibility.");
                await Task.Delay(300, token);
            }
            await HandleBrowserAsync(browserHit.Value, options, token);
            await DelayClickAsync(options, token);
        }
    }

    private async Task HandleBrowserAsync(ScreenHit hit, RunOptions options, CancellationToken token)
    {
        using var browser = Process.GetProcessById(hit.ProcessId);
        if (!ScreenMatcher.IsBrowserProcess(browser.ProcessName))
            throw new InvalidOperationException("The matched window is not a supported browser.");
        Report($"Clicked browser Download; ending {browser.ProcessName} in {options.CloseDelay:0.0} seconds…");
        var clickedAt = Stopwatch.GetTimestamp();
        ScreenMatcher.Click(hit.Point);
        var remaining = TimeSpan.FromSeconds(options.CloseDelay) - Stopwatch.GetElapsedTime(clickedAt);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token);
        token.ThrowIfCancellationRequested();
        if (browser.HasExited)
            throw new InvalidOperationException("The matched browser process ended before cleanup. Check whether the download started.");
        browser.Kill(entireProcessTree: true);
        if (!browser.WaitForExit(5000))
            throw new InvalidOperationException("The browser process did not exit after termination.");
        Report("Browser process ended. Waiting for the next Vortex button…");
    }

    private static Task DelayClickAsync(RunOptions options, CancellationToken token)
    {
        var seconds = options.Random
            ? options.Minimum + Random.Shared.NextDouble() * (options.Maximum - options.Minimum)
            : options.Fixed;
        return Task.Delay(TimeSpan.FromSeconds(seconds), token);
    }

    private void Report(string message)
    {
        if (!IsDisposed && IsHandleCreated) BeginInvoke(() => status.Text = message);
    }
}

internal readonly record struct RunOptions(double Fixed, double Minimum, double Maximum, bool Random, double CloseDelay);

internal sealed class ImageTemplate : IDisposable
{
    public Bitmap Bitmap { get; }
    public int[] Pixels { get; }
    public int Stride { get; }

    private ImageTemplate(Bitmap bitmap)
    {
        Bitmap = bitmap;
        Pixels = ScreenMatcher.CopyPixels(bitmap, out var stride);
        Stride = stride;
    }

    public static ImageTemplate Load(string path)
    {
        using var source = new Bitmap(path);
        if (source.Width < 8 || source.Height < 8 || source.Width > 600 || source.Height > 300)
            throw new InvalidOperationException($"{Path.GetFileName(path)} must be a tight crop between 8×8 and 600×300 pixels.");
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        return new ImageTemplate(bitmap);
    }

    public void Dispose() => Bitmap.Dispose();
}
