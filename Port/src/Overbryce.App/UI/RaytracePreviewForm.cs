using System.Diagnostics;
using System.Drawing.Imaging;
using Overbryce.App.Rendering;
using Overbryce.Raytrace;

namespace Overbryce.App.UI;

/// <summary>
/// The view ray traced (View > Ray trace this view, F9): the picture is refined pass by pass (1, 2, 4 …
/// samples per pixel) until the animation's sample count is reached. It is a snapshot of the view at the
/// moment F9 was pressed; F9 again traces the current view into the same window.
/// </summary>
internal sealed class RaytracePreviewForm : Form
{
    private readonly PictureBox _picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripButton _save = new("Save picture...") { Enabled = false };
    private CancellationTokenSource? _cancel;
    private Bitmap? _shown;
    private int _run;

    public RaytracePreviewForm()
    {
        Text = "Ray traced view";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        KeyPreview = true;
        var bar = new StatusStrip();
        bar.Items.Add(_status);
        bar.Items.Add(_save);
        _save.Click += (_, _) => SavePicture();
        Controls.Add(_picture);
        Controls.Add(bar);
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };
        FormClosed += (_, _) =>
        {
            _cancel?.Cancel();
            _shown?.Dispose();
        };
    }

    /// <summary>Traces <paramref name="job"/> up to <paramref name="samples"/> samples per pixel, stopping any earlier picture.</summary>
    public void Start(RaytraceJob job, int samples, string caption)
    {
        _cancel?.Cancel();
        var cancel = _cancel = new CancellationTokenSource();
        int run = ++_run;
        Text = "Ray traced view - " + caption;

        var area = Screen.FromControl(this).WorkingArea;
        float fit = Math.Min(1f, Math.Min(area.Width * 0.85f / job.Width, area.Height * 0.85f / job.Height));
        ClientSize = new Size((int)(job.Width * fit), (int)(job.Height * fit) + 24);

        var progress = new LatestProgress();
        var watch = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) =>
        {
            if (run != _run || IsDisposed)
            {
                timer.Dispose();
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
                var bitmap = RaytraceBridge.ToBitmap(job.Resolve());
                var stats = job.Stats;
                string text = job.Samples >= samples
                    ? $"Done: {stats.Samples} samples per pixel, {stats.Elements} elements, {stats.Triangles} triangles, " +
                      $"{(stats.Prepare + stats.Trace).TotalSeconds:0.0} s"
                    : $"Tracing: {job.Samples} of {samples} samples per pixel, {watch.Elapsed.TotalSeconds:0} s (Esc closes)";
                if (!Post(run, bitmap, text)) return;
                pass = Math.Min(pass * 2, 64);
            }
        }, cancel.Token).ContinueWith(t =>
        {
            if (t.Exception is { } ex && !cancel.IsCancellationRequested)
                Post(run, null, "Ray tracing failed: " + ex.GetBaseException().Message);
        }, TaskScheduler.Default);
    }

    /// <summary>Shows a finished pass on the UI thread; false when the window is gone or a newer picture started.</summary>
    private bool Post(int run, Bitmap? bitmap, string text)
    {
        if (IsDisposed || run != _run)
        {
            bitmap?.Dispose();
            return false;
        }
        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed || run != _run)
                {
                    bitmap?.Dispose();
                    return;
                }
                if (bitmap != null)
                {
                    _picture.Image = bitmap;
                    _shown?.Dispose();
                    _shown = bitmap;
                    _save.Enabled = true;
                }
                _status.Text = text;
            });
            return true;
        }
        catch (InvalidOperationException)
        {
            bitmap?.Dispose();
            return false;
        }
    }

    private void SavePicture()
    {
        if (_shown == null) return;
        using var dlg = new SaveFileDialog
        {
            Title = "Save the ray traced picture",
            Filter = "PNG images (*.png)|*.png|Bitmaps (*.bmp)|*.bmp",
            FileName = "raytraced.png",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _shown.Save(dlg.FileName, dlg.FileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Bmp : ImageFormat.Png);
    }
}

/// <summary>Keeps the last reported value (read by a UI timer; no message per tile).</summary>
internal sealed class LatestProgress : IProgress<float>
{
    private float _value;

    public float Value => Volatile.Read(ref _value);

    public void Report(float value) => Volatile.Write(ref _value, value);
}
