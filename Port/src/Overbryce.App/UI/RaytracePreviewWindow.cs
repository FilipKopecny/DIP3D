using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Overbryce.Raytrace;

namespace Overbryce.App.UI;

/// <summary>
/// The view ray traced (View > Ray trace this view, F9): the picture is refined pass by pass (1, 2, 4 …
/// samples per pixel) until the animation's sample count is reached. It is a snapshot of the view at the
/// moment F9 was pressed; F9 again traces the current view into the same window.
/// </summary>
internal sealed class RaytracePreviewWindow : Window
{
    private readonly Image _picture = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _save = new() { Content = "Save picture...", IsEnabled = false, Margin = new Thickness(8, 0, 0, 0) };
    private CancellationTokenSource? _cancel;
    private BitmapSource? _shown;
    private int _run;

    public RaytracePreviewWindow()
    {
        Title = "Ray traced view";
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty, typeof(Window));
        RenderOptions.SetBitmapScalingMode(_picture, BitmapScalingMode.HighQuality);

        DockPanel.SetDock(_save, Dock.Right);
        var bar = new DockPanel { Margin = new Thickness(12, 8, 12, 8), Children = { _save, _status } };
        DockPanel.SetDock(bar, Dock.Bottom);
        Content = new DockPanel
        {
            Children =
            {
                bar,
                new Border { Background = Brushes.Black, Child = _picture },
            },
        };
        _save.Click += (_, _) => SavePicture();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Closed += (_, _) => _cancel?.Cancel();
    }

    /// <summary>Traces <paramref name="job"/> up to <paramref name="samples"/> samples per pixel, stopping any earlier picture.</summary>
    public void Start(RaytraceJob job, int samples, string caption)
    {
        _cancel?.Cancel();
        var cancel = _cancel = new CancellationTokenSource();
        int run = ++_run;
        Title = "Ray traced view - " + caption;

        // the picture's pixels at their size on screen, unless the screen is too small
        var dpi = VisualTreeHelper.GetDpi(Owner ?? this);
        var area = SystemParameters.WorkArea;
        double w = job.Width / dpi.DpiScaleX, h = job.Height / dpi.DpiScaleY;
        double fit = Math.Min(1, Math.Min(area.Width * 0.85 / w, area.Height * 0.85 / (h + 60)));
        // the window takes its size from the picture once, then the picture follows the window
        _picture.Width = w * fit;
        _picture.Height = h * fit;
        SizeToContent = SizeToContent.WidthAndHeight;
        Dispatcher.InvokeAsync(() =>
        {
            SizeToContent = SizeToContent.Manual;
            _picture.ClearValue(WidthProperty);
            _picture.ClearValue(HeightProperty);
        }, DispatcherPriority.ContextIdle);

        var progress = new LatestProgress();
        var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            if (run != _run || !IsVisible)
            {
                timer.Stop();
                return;
            }
            if (job.Samples < samples)
                _status.Text = $"Tracing: {job.Samples} of {samples} samples per pixel, pass {progress.Value:0%}, {watch.Elapsed.TotalSeconds:0} s (Esc closes)";
        };
        timer.Start();

        Task.Run(() =>
        {
            int pass = 1;
            while (job.Samples < samples && !cancel.IsCancellationRequested)
            {
                job.Trace(Math.Min(pass, samples - job.Samples), progress, cancel.Token);
                var picture = ToBitmapSource(job.Resolve());
                var stats = job.Stats;
                string text = job.Samples >= samples
                    ? $"Done: {stats.Samples} samples per pixel, {stats.Elements} elements, {stats.Triangles} triangles, " +
                      $"{(stats.Prepare + stats.Trace).TotalSeconds:0.0} s"
                    : $"Tracing: {job.Samples} of {samples} samples per pixel, {watch.Elapsed.TotalSeconds:0} s (Esc closes)";
                if (!Post(run, cancel, picture, text)) return;
                pass = Math.Min(pass * 2, 64);
            }
        }, cancel.Token).ContinueWith(t =>
        {
            if (t.Exception is { } ex && !cancel.IsCancellationRequested)
                Post(run, cancel, null, "Ray tracing failed: " + ex.GetBaseException().Message);
        }, TaskScheduler.Default);
    }

    /// <summary>A traced picture for WPF (frozen: made on a worker thread, shown on the UI thread).</summary>
    public static BitmapSource ToBitmapSource(RaytraceImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, image.Bgra, image.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Shows a finished pass on the UI thread; false when the window is gone or a newer picture started.</summary>
    private bool Post(int run, CancellationTokenSource cancel, BitmapSource? picture, string text)
    {
        if (cancel.IsCancellationRequested || run != Volatile.Read(ref _run)) return false;
        Dispatcher.InvokeAsync(() =>
        {
            if (cancel.IsCancellationRequested || run != _run) return;
            if (picture != null)
            {
                _picture.Source = _shown = picture;
                _save.IsEnabled = true;
            }
            _status.Text = text;
        });
        return true;
    }

    private void SavePicture()
    {
        if (_shown == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save the ray traced picture",
            Filter = "PNG images (*.png)|*.png|Bitmaps (*.bmp)|*.bmp",
            FileName = "raytraced.png",
        };
        if (dlg.ShowDialog(this) != true) return;
        BitmapEncoder encoder = dlg.FileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ? new BmpBitmapEncoder() : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(_shown));
        try
        {
            using var file = File.Create(dlg.FileName);
            encoder.Save(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message.Show(this, $"Cannot save '{dlg.FileName}':\n{ex.Message}", MessageKind.Error);
        }
    }
}

/// <summary>Keeps the last reported value (read by a UI timer; no message per tile).</summary>
internal sealed class LatestProgress : IProgress<float>
{
    private float _value;

    public float Value => Volatile.Read(ref _value);

    public void Report(float value) => Volatile.Write(ref _value, value);
}
