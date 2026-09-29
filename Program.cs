using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
    private readonly TextBox vortexPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox browserPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox closePath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly NumericUpDown fixedSeconds = Seconds(1.0m);
    private readonly NumericUpDown minimumSeconds = Seconds(1.0m);
    private readonly NumericUpDown maximumSeconds = Seconds(2.0m);
    private readonly NumericUpDown closeSeconds = Seconds(5.2m);
    private readonly CheckBox randomTiming = new() { Text = "Random interval", AutoSize = true };
    private readonly Button startButton = new() { Text = "Start", Width = 120, Height = 36 };
    private readonly Button stopButton = new() { Text = "Stop", Width = 120, Height = 36, Enabled = false };
    private readonly Label status = new() { Text = "Ready. Select all three images to begin.", AutoSize = true };
    private CancellationTokenSource? runCancellation;

    public MainForm()
    {
        Text = "Nexus Collection Downloader";
        ClientSize = new Size(720, 550);
        MinimumSize = new Size(650, 550);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(248, 249, 251);
        ForeColor = Color.FromArgb(30, 38, 48);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 165));
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
            Text = "Select cropped images from your current screen. The app clicks Vortex, then the browser Download button, then closes that browser window.",
            Dock = DockStyle.Fill, AutoSize = false
        }, 0, 1);

        var files = new GroupBox { Text = "Button images", Dock = DockStyle.Fill, Padding = new Padding(12, 17, 12, 10) };
        var fileGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        for (var i = 0; i < 3; i++) fileGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        AddFileRow(fileGrid, 0, "Vortex Download", vortexPath);
        AddFileRow(fileGrid, 1, "Browser Download", browserPath);
        AddFileRow(fileGrid, 2, "Browser Close", closePath);
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
        timingGrid.Controls.Add(Label("Browser close delay"), 0, 2);
        timingGrid.Controls.Add(closeSeconds, 1, 2);
        timingGrid.Controls.Add(new Label { Text = "Default: 5.2 s after browser Download", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray }, 2, 2);
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

    private static void AddFileRow(TableLayoutPanel grid, int row, string title, TextBox path)
    {
        grid.Controls.Add(Label(title), 0, row);
        path.Margin = new Padding(3, 7, 3, 7);
        grid.Controls.Add(path, 1, row);
        var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill, Margin = new Padding(6, 5, 0, 5) };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = $"Choose {title} image", Filter = "Image files|*.png;*.bmp;*.jpg;*.jpeg|All files|*.*"
            };
            if (dialog.ShowDialog() == DialogResult.OK) path.Text = dialog.FileName;
        };
        grid.Controls.Add(browse, 2, row);
    }

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

        ImageTemplate? vortex = null, browser = null, close = null;
        try
        {
            vortex = ImageTemplate.Load(vortexPath.Text, "Vortex Download");
            browser = ImageTemplate.Load(browserPath.Text, "Browser Download");
            close = ImageTemplate.Load(closePath.Text, "Browser Close");
        }
        catch (Exception ex)
        {
            vortex?.Dispose(); browser?.Dispose(); close?.Dispose();
            MessageBox.Show(this, ex.Message, "Check images", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        runCancellation = new CancellationTokenSource();
        startButton.Enabled = false;
        stopButton.Enabled = true;
        status.Text = "Running. Restore this window from the taskbar to stop.";
        WindowState = FormWindowState.Minimized;

        try
        {
            var options = new RunOptions((double)fixedSeconds.Value, (double)minimumSeconds.Value,
                (double)maximumSeconds.Value, randomTiming.Checked, (double)closeSeconds.Value);
            await Task.Run(() => RunAsync(vortex!, browser!, close!, options, runCancellation.Token));
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
            vortex?.Dispose(); browser?.Dispose(); close?.Dispose();
            runCancellation.Dispose();
            runCancellation = null;
            startButton.Enabled = true;
            stopButton.Enabled = false;
        }
    }

    private async Task RunAsync(ImageTemplate vortex, ImageTemplate browser, ImageTemplate close, RunOptions options, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var browserHit = ScreenMatcher.Find(browser);
            if (browserHit is not null)
            {
                await HandleBrowserAsync(browserHit.Value, close, options, token);
                await DelayClickAsync(options, token);
                continue;
            }

            var vortexHit = ScreenMatcher.Find(vortex);
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
                browserHit = ScreenMatcher.Find(browser);
                if (browserHit is not null) break;
                if (DateTime.UtcNow >= deadline)
                    throw new InvalidOperationException("Browser Download was not found within 60 seconds. Check its image and window visibility.");
                await Task.Delay(300, token);
            }
            await HandleBrowserAsync(browserHit.Value, close, options, token);
            await DelayClickAsync(options, token);
        }
    }

    private async Task HandleBrowserAsync(ScreenHit hit, ImageTemplate close, RunOptions options, CancellationToken token)
    {
        Report("Clicked browser Download; waiting to close its window…");
        ScreenMatcher.Click(hit.Point);
        await Task.Delay(TimeSpan.FromSeconds(options.CloseDelay), token);
        if (!ScreenMatcher.IsWindowOpen(hit.Window))
        {
            Report("Browser window already closed.");
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!ScreenMatcher.IsWindowOpen(hit.Window)) return;
            var closeHit = ScreenMatcher.Find(close, hit.Window);
            if (closeHit is not null)
            {
                ScreenMatcher.Click(closeHit.Value.Point);
                await Task.Delay(800, token);
                if (ScreenMatcher.IsWindowOpen(hit.Window))
                    throw new InvalidOperationException("The browser window stayed open after clicking Close. Close it manually before restarting.");
                Report("Browser window closed. Waiting for the next Vortex button…");
                return;
            }
            if (DateTime.UtcNow >= deadline)
                throw new InvalidOperationException("Browser Close image was not found in the same window. Close it manually and check the image.");
            await Task.Delay(150, token);
        }
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

    private ImageTemplate(Bitmap bitmap) => Bitmap = bitmap;

    public static ImageTemplate Load(string path, string title)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException($"Choose an image for {title}.");
        using var source = new Bitmap(path);
        if (source.Width < 8 || source.Height < 8 || source.Width > 600 || source.Height > 300)
            throw new InvalidOperationException($"{title} image should be a tight crop between 8×8 and 600×300 pixels.");
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        return new ImageTemplate(bitmap);
    }

    public void Dispose() => Bitmap.Dispose();
}
