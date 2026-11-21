using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using OpenTK.GLControl;
using OpenTK.Windowing.Common;
using Overbryce.App.Rendering;
using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.App.UI;

/// <summary>
/// Main window (original TAniMain): element tree + property grid on the left, OpenGL view with the
/// move/rotate tool bar on the right, key-state timeline below.
/// </summary>
internal sealed class MainForm : Form
{
    private const string AppTitle = "Overbryce Animator";

    // ------------------------------------------------------------------ document state
    private Animation _animation = Animation.CreateNew();
    private string? _filePath;
    private bool _dirty;
    private Scene _currentScene;
    private Element? _selected;

    // ------------------------------------------------------------------ view state
    private readonly EditorCamera _editor = new();
    private ViewMode _viewMode = ViewMode.Front;
    private bool _wireframe;
    private bool _modernView;
    private float? _previewTime;
    private readonly RenderSettings _settings = new();
    private readonly Dictionary<string, string> _textureLookup = new(StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ playback
    private readonly System.Windows.Forms.Timer _playTimer = new() { Interval = 10 };
    private readonly Stopwatch _playClock = new();
    private float _playStart;
    private bool _exportCancel;

    // ------------------------------------------------------------------ controls
    private readonly GLControl _gl;
    private SceneRenderer? _renderer;
    private readonly TreeView _tree = new();
    private readonly PropertyGrid _grid = new();
    private readonly ImageList _treeImages = new();
    private readonly Dictionary<object, TreeNode> _nodes = new(ReferenceEqualityComparer.Instance);
    private bool _suppressTreeSelect;

    private readonly TrackBar _timeline = new();
    private readonly Label _timeLabel = new();
    private readonly Button _playButton = new();
    private readonly CheckBox _loopCheck = new();
    private readonly NumericUpDown _moveStep = new();
    private readonly NumericUpDown _rotateStep = new();

    private readonly ToolStripStatusLabel _stateStatus = new();
    private readonly ToolStripStatusLabel _viewStatus = new();
    private readonly ToolStripStatusLabel _selectionStatus = new();
    private readonly ToolStripStatusLabel _warningStatus = new();

    private ToolStripMenuItem _wireItem = null!;
    private ToolStripMenuItem _modernItem = null!;
    private readonly Dictionary<ViewMode, ToolStripMenuItem> _viewItems = new();
    private readonly Dictionary<string, Image?> _toolImages = new(StringComparer.OrdinalIgnoreCase);

    private Point _lastMouse;
    private Point _downPoint;
    private bool _dragged;
    private bool _pressed;

    public MainForm(string? fileToOpen, SnapshotRequest? snapshot = null)
    {
        _currentScene = _animation.Scenes[0];

        Text = AppTitle;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1280, 800);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _gl = new GLControl(new GLControlSettings
        {
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            NumberOfSamples = 4,
            DepthBits = 24,
        })
        {
            Dock = DockStyle.Fill,
        };

        BuildUi();
        _editor.Reset(ViewMode.Front);
        UpdateViewChecks();
        RebuildTree();
        UpdateTitle();
        UpdateTimeline();

        _playTimer.Tick += (_, _) => PlayTick();

        Shown += (_, _) =>
        {
            if (fileToOpen != null) OpenFile(fileToOpen);
            if (snapshot != null)
            {
                Environment.ExitCode = WriteSnapshot(snapshot) ? 0 : 1;
                _dirty = false;
                Close();
            }
        };
    }

    /// <summary>
    /// Renders off-screen and exits (command line): one picture (--snapshot) or every frame of the
    /// animation as name000000.png, ... into a folder (--frames).
    /// </summary>
    private bool WriteSnapshot(SnapshotRequest req)
    {
        try
        {
            _gl.MakeCurrent();
            _renderer ??= new SceneRenderer();
            var editor = new EditorCamera();
            editor.Reset(req.View);
            using var target = new OffscreenTarget(req.Size.Width, req.Size.Height, 8);

            void RenderTo(Scene? scene, string path)
            {
                var (view, proj) = ViewSetup.Compute(scene, req.View, editor, req.Size.Width, req.Size.Height, out _);
                target.Bind();
                _renderer.Render(scene, new RenderOptions
                {
                    Wireframe = req.Wireframe,
                    AlphaBlend = _settings.AlphaBlend,
                    View = view,
                    Projection = proj,
                    ResolveTexture = ResolveTexture,
                    Modern = (req.Modern ?? _animation.Modern.Enabled) ? _animation.Modern : null,
                    Samples = 8,
                }, req.Size.Width, req.Size.Height);
                using var bmp = target.ReadBitmap();
                bmp.Save(path, path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Bmp : ImageFormat.Png);
            }

            if (!req.Frames)
            {
                RenderTo(req.Time is float t ? _animation.Evaluate(t) : _currentScene, req.Output);
                return true;
            }

            Directory.CreateDirectory(req.Output);
            var times = _animation.FrameTimes().ToList();
            for (int i = 0; i < times.Count; i++)
            {
                if (times[i] < (req.From ?? float.MinValue) - 1e-4f || times[i] > (req.To ?? float.MaxValue) + 1e-4f) continue;
                RenderTo(_animation.Evaluate(times[i]), Path.Combine(req.Output, $"{_animation.Name}{i:000000}.png"));
            }
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            File.WriteAllText(req.Output + ".error.txt", ex.ToString());
            return false;
        }
    }

    // ==================================================================================== UI build

    private Image? Tool(string name)
    {
        if (_toolImages.TryGetValue(name, out var cached)) return cached;
        Image? result = null;
        var asm = typeof(MainForm).Assembly;
        string? res = asm.GetManifestResourceNames().FirstOrDefault(n => string.Equals(n, $"Tools.{name}.bmp", StringComparison.OrdinalIgnoreCase));
        if (res != null)
        {
            using var s = asm.GetManifestResourceStream(res)!;
            using var src = new Bitmap(s);
            var bmp = src.Clone(new Rectangle(0, 0, src.Width, src.Height), PixelFormat.Format32bppArgb);
            // C++ Builder speed-button glyphs use the bottom-left pixel as the transparent colour
            bmp.MakeTransparent(bmp.GetPixel(0, bmp.Height - 1));
            result = bmp;
        }
        _toolImages[name] = result;
        return result;
    }

    private void BuildUi()
    {
        SuspendLayout();

        // ---------------------------------------------------------------- menu
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(Item("&New", Keys.Control | Keys.N, (_, _) => NewAnimation()));
        file.DropDownItems.Add(Item("&Open...", Keys.Control | Keys.O, (_, _) => OpenWithDialog()));
        file.DropDownItems.Add(Item("&Save", Keys.Control | Keys.S, (_, _) => Save(false)));
        file.DropDownItems.Add(Item("Save &As...", Keys.Control | Keys.Shift | Keys.S, (_, _) => Save(true)));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("&Export frames...", Keys.Control | Keys.E, (_, _) => ExportFrames()));
        file.DropDownItems.Add(Item("Rendering se&ttings...", Keys.None, (_, _) => { if (SettingsDialog.Show(this, _settings)) _gl.Invalidate(); }));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("E&xit", Keys.Alt | Keys.F4, (_, _) => Close()));

        var edit = new ToolStripMenuItem("&Edit");
        var insert = new ToolStripMenuItem("&Insert");
        foreach (var (label, img, factory) in InsertableElements())
            insert.DropDownItems.Add(Item(label, Keys.None, (_, _) => InsertElement(factory()), Tool(img)));
        insert.DropDownItems.Add(new ToolStripSeparator());
        for (int i = 1; i <= 4; i++)
        {
            int preset = i;
            insert.DropDownItems.Add(Item($"Bezier mesh preset {i}", Keys.None,
                (_, _) => InsertElement(new MeshElement("NovyMesh", MeshPresets.Get(preset), MeshPresets.Size, MeshPresets.Size)), Tool("mesh" + i)));
        }
        edit.DropDownItems.Add(insert);
        edit.DropDownItems.Add(Item("&Delete", Keys.Delete, (_, _) => DeleteSelected()));
        edit.DropDownItems.Add(Item("Re&name", Keys.F2, (_, _) => _tree.SelectedNode?.BeginEdit()));
        edit.DropDownItems.Add(Item("Set as active &camera", Keys.Control | Keys.K, (_, _) => SetActiveCamera()));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Item("D&uplicate key state", Keys.Control | Keys.D, (_, _) => DuplicateState()));
        edit.DropDownItems.Add(Item("Delete key s&tate", Keys.Control | Keys.Delete, (_, _) => DeleteState()));

        var view = new ToolStripMenuItem("&View");
        _wireItem = Item("&Wire frame", Keys.Control | Keys.W, (_, _) => { _wireframe = !_wireframe; UpdateViewChecks(); _gl.Invalidate(); });
        view.DropDownItems.Add(_wireItem);
        _modernItem = Item("&Modern rendering", Keys.Control | Keys.M, (_, _) => { _modernView = !_modernView; UpdateViewChecks(); UpdateStatus(); _gl.Invalidate(); });
        view.DropDownItems.Add(_modernItem);
        view.DropDownItems.Add(new ToolStripSeparator());
        AddViewItem(view, "&Camera", ViewMode.Camera, Keys.Control | Keys.D1);
        AddViewItem(view, "&Front", ViewMode.Front, Keys.Control | Keys.D2);
        AddViewItem(view, "&Side", ViewMode.Side, Keys.Control | Keys.D3);
        AddViewItem(view, "&Top", ViewMode.Top, Keys.Control | Keys.D4);
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownItems.Add(Item("F&irst key state", Keys.Control | Keys.Home, (_, _) => GoToState(0)));
        view.DropDownItems.Add(Item("&Previous key state", Keys.PageUp, (_, _) => StepState(-1)));
        view.DropDownItems.Add(Item("&Next key state (new at end)", Keys.PageDown, (_, _) => StepState(+1)));
        view.DropDownItems.Add(Item("&Last key state", Keys.Control | Keys.End, (_, _) => GoToState(_animation.Scenes.Count - 1)));

        var anim = new ToolStripMenuItem("&Animation");
        anim.DropDownItems.Add(Item("&Settings...", Keys.None, (_, _) => EditAnimationSettings()));
        anim.DropDownItems.Add(Item("&Modern rendering settings...", Keys.None, (_, _) => EditModernSettings()));
        anim.DropDownItems.Add(Item("&Play / Stop", Keys.F5, (_, _) => TogglePlay(), Tool("render")));
        anim.DropDownItems.Add(Item("&Compute frames (export)...", Keys.None, (_, _) => ExportFrames()));

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(Item("&Controls", Keys.F1, (_, _) => ShowHelp()));
        help.DropDownItems.Add(Item("&About", Keys.None, (_, _) => AboutDialog.Show(this)));

        menu.Items.AddRange([file, edit, view, anim, help]);

        // ---------------------------------------------------------------- tool bar (original speed buttons)
        var tools = new ToolStrip { ImageScalingSize = new Size(25, 25), GripStyle = ToolStripGripStyle.Hidden };
        foreach (var (label, img, factory) in InsertableElements())
            tools.Items.Add(ToolButton(label, img, () => InsertElement(factory())));
        tools.Items.Add(new ToolStripSeparator());
        var meshes = new ToolStripDropDownButton("Mesh presets", Tool("mesh")) { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "Insert Bezier mesh preset" };
        for (int i = 1; i <= 4; i++)
        {
            int preset = i;
            meshes.DropDownItems.Add(new ToolStripMenuItem($"Preset {i}", Tool("mesh" + i),
                (_, _) => InsertElement(new MeshElement("NovyMesh", MeshPresets.Get(preset), MeshPresets.Size, MeshPresets.Size)))
            { ImageScaling = ToolStripItemImageScaling.None });
        }
        tools.Items.Add(meshes);
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add(ToolButton("Play / stop animation (F5)", "render", TogglePlay));

        // ---------------------------------------------------------------- left side: tree + properties
        _treeImages.ColorDepth = ColorDepth.Depth32Bit;
        _treeImages.ImageSize = new Size(LogicalToDeviceUnits(18), LogicalToDeviceUnits(18));
        foreach (var (key, img) in TreeIcons())
            if (Tool(img) is { } image) _treeImages.Images.Add(key, image);

        _tree.Dock = DockStyle.Fill;
        _tree.HideSelection = false;
        _tree.LabelEdit = true;
        _tree.AllowDrop = true;
        _tree.ImageList = _treeImages;
        _tree.AfterSelect += Tree_AfterSelect;
        _tree.BeforeLabelEdit += (_, e) => { if (e.Node?.Tag == null) e.CancelEdit = true; };
        _tree.AfterLabelEdit += Tree_AfterLabelEdit;
        _tree.ItemDrag += (_, e) => { if (e.Item is TreeNode { Tag: Element and not Scene } n) _tree.DoDragDrop(n, DragDropEffects.Move | DragDropEffects.Copy); };
        _tree.DragOver += Tree_DragOver;
        _tree.DragDrop += Tree_DragDrop;
        _tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };
        _tree.ContextMenuStrip = BuildTreeMenu();

        _grid.Dock = DockStyle.Fill;
        _grid.ToolbarVisible = false;
        _grid.PropertySort = PropertySort.Categorized;
        _grid.PropertyValueChanged += Grid_PropertyValueChanged;

        var leftSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        leftSplit.Panel1.Controls.Add(_tree);
        leftSplit.Panel2.Controls.Add(_grid);

        // ---------------------------------------------------------------- right side: view + tools + timeline
        _gl.Paint += (_, _) => RenderView();
        _gl.Resize += (_, _) => _gl.Invalidate();
        _gl.MouseDown += (_, e) =>
        {
            _lastMouse = _downPoint = e.Location;
            _pressed = e.Button == MouseButtons.Left;
            _dragged = false;
            _gl.Focus();
        };
        _gl.MouseMove += Gl_MouseMove;
        _gl.MouseUp += (_, e) =>
        {
            // a click that only activated the window may arrive without its MouseDown: still pick
            if (e.Button == MouseButtons.Left && !(_pressed && _dragged))
                PickAt(e.Location, (ModifierKeys & Keys.Alt) != 0);
            _pressed = _dragged = false;
        };
        _gl.MouseWheel += (_, e) =>
        {
            if (_viewMode == ViewMode.Camera) return;
            _editor.Zoom(e.Delta > 0 ? 1 / 1.1f : 1.1f);
            _gl.Invalidate();
        };

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(_gl);
        right.Controls.Add(BuildToolPanel());
        right.Controls.Add(BuildTimeline());

        var mainSplit = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        mainSplit.Panel1.Controls.Add(leftSplit);
        mainSplit.Panel2.Controls.Add(right);

        // ---------------------------------------------------------------- status bar
        var status = new StatusStrip();
        _stateStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _viewStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _selectionStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _warningStatus.Spring = true;
        _warningStatus.TextAlign = ContentAlignment.MiddleLeft;
        _warningStatus.ForeColor = Color.DarkRed;
        status.Items.AddRange([_stateStatus, _viewStatus, _selectionStatus, _warningStatus]);

        Controls.Add(mainSplit);
        Controls.Add(tools);
        Controls.Add(menu);
        Controls.Add(status);
        MainMenuStrip = menu;

        ResumeLayout(true);

        Load += (_, _) =>
        {
            mainSplit.SplitterDistance = LogicalToDeviceUnits(330);
            leftSplit.SplitterDistance = leftSplit.Height * 2 / 5;
        };
    }

    private static ToolStripMenuItem Item(string text, Keys keys, EventHandler onClick, Image? image = null)
    {
        var item = new ToolStripMenuItem(text, image, onClick);
        if (keys != Keys.None)
        {
            if (keys == Keys.Delete || keys == Keys.F2 || keys == Keys.PageUp || keys == Keys.PageDown)
                item.ShortcutKeyDisplayString = keys switch { Keys.Delete => "Del", Keys.PageUp => "PgUp", Keys.PageDown => "PgDn", _ => keys.ToString() };
            else
                item.ShortcutKeys = keys;
        }
        return item;
    }

    private ToolStripButton ToolButton(string text, string image, Action onClick)
    {
        var b = new ToolStripButton(text, Tool(image), (_, _) => onClick())
        {
            DisplayStyle = Tool(image) != null ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.Text,
            ToolTipText = text,
        };
        return b;
    }

    private void AddViewItem(ToolStripMenuItem menu, string text, ViewMode mode, Keys keys)
    {
        var item = Item(text, keys, (_, _) => SetViewMode(mode));
        _viewItems[mode] = item;
        menu.DropDownItems.Add(item);
    }

    private static IEnumerable<(string Label, string Image, Func<Element> Factory)> InsertableElements() =>
    [
        ("Sphere", "gula", () => new SphereElement()),
        ("Disk", "disk", () => new DiskElement()),
        ("Truncated cone", "zrezkuze", () => new ConeElement()),
        ("Bezier mesh", "mesh", () => new MeshElement()),
        ("Box", "kvader", () => new BoxElement()),
        ("Rectangle", "obdlznik", () => new RectangleElement()),
        ("Triangle", "trojuhol", () => new TriangleElement()),
        ("Torus", "torus", () => new TorusElement()),
        ("Light", "svetlo", () => new LightElement()),
        ("Camera", "kamera", () => new CameraElement()),
        ("Group", "grupa", () => new GroupElement()),
    ];

    private static IEnumerable<(string Key, string Image)> TreeIcons() =>
    [
        (nameof(ElementType.Sphere), "gula"), (nameof(ElementType.Disk), "disk"), (nameof(ElementType.Cone), "zrezkuze"),
        (nameof(ElementType.Mesh), "mesh"), (nameof(ElementType.Box), "kvader"), (nameof(ElementType.Rectangle), "obdlznik"),
        (nameof(ElementType.Triangle), "trojuhol"), (nameof(ElementType.Torus), "torus"), (nameof(ElementType.Light), "svetlo"),
        (nameof(ElementType.Camera), "kamera"), (nameof(ElementType.Group), "grupa"), (nameof(ElementType.Scene), "render"),
        ("Animation", "render"),
    ];

    private ContextMenuStrip BuildTreeMenu()
    {
        var m = new ContextMenuStrip();
        var insert = new ToolStripMenuItem("Insert");
        foreach (var (label, img, factory) in InsertableElements())
            insert.DropDownItems.Add(Item(label, Keys.None, (_, _) => InsertElement(factory()), Tool(img)));
        m.Items.Add(insert);
        m.Items.Add(Item("Rename", Keys.None, (_, _) => _tree.SelectedNode?.BeginEdit()));
        m.Items.Add(Item("Delete", Keys.None, (_, _) => DeleteSelected()));
        m.Items.Add(Item("Set as active camera", Keys.None, (_, _) => SetActiveCamera()));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(Item("Duplicate key state", Keys.None, (_, _) => DuplicateState()));
        m.Items.Add(Item("Delete key state", Keys.None, (_, _) => DeleteState()));
        return m;
    }

    /// <summary>The original move/rotate speed buttons (left/right/up/down/z, xcw/xccw ...).</summary>
    private Control BuildToolPanel()
    {
        var strip = new ToolStrip
        {
            Dock = DockStyle.Right,
            LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow,
            ImageScalingSize = new Size(25, 25),
            GripStyle = ToolStripGripStyle.Hidden,
        };

        strip.Items.Add(new ToolStripLabel("Move"));
        _moveStep.DecimalPlaces = 2;
        _moveStep.Increment = 0.1m;
        _moveStep.Minimum = 0.01m;
        _moveStep.Maximum = 100;
        _moveStep.Value = 1;
        _moveStep.Width = LogicalToDeviceUnits(60);
        strip.Items.Add(new ToolStripControlHost(_moveStep) { ToolTipText = "Move step" });
        strip.Items.Add(ToolButton("Move -X", "left", () => MoveSelected(-1, 0, 0)));
        strip.Items.Add(ToolButton("Move +X", "right", () => MoveSelected(1, 0, 0)));
        strip.Items.Add(ToolButton("Move +Y", "up", () => MoveSelected(0, 1, 0)));
        strip.Items.Add(ToolButton("Move -Y", "down", () => MoveSelected(0, -1, 0)));
        strip.Items.Add(ToolButton("Move +Z (towards the viewer)", "zksebe", () => MoveSelected(0, 0, 1)));
        strip.Items.Add(ToolButton("Move -Z (away from the viewer)", "zodseba", () => MoveSelected(0, 0, -1)));
        strip.Items.Add(new ToolStripSeparator());

        strip.Items.Add(new ToolStripLabel("Rotate"));
        _rotateStep.DecimalPlaces = 1;
        _rotateStep.Minimum = 0.1m;
        _rotateStep.Maximum = 180;
        _rotateStep.Value = 15;
        _rotateStep.Width = LogicalToDeviceUnits(60);
        strip.Items.Add(new ToolStripControlHost(_rotateStep) { ToolTipText = "Rotation step in degrees" });
        strip.Items.Add(ToolButton("Rotate about X (counter-clockwise)", "xccw", () => RotateSelected(0, 1)));
        strip.Items.Add(ToolButton("Rotate about X (clockwise)", "xcw", () => RotateSelected(0, -1)));
        strip.Items.Add(ToolButton("Rotate about Y (counter-clockwise)", "yccw", () => RotateSelected(1, 1)));
        strip.Items.Add(ToolButton("Rotate about Y (clockwise)", "ycw", () => RotateSelected(1, -1)));
        strip.Items.Add(ToolButton("Rotate about Z (counter-clockwise)", "zccw", () => RotateSelected(2, 1)));
        strip.Items.Add(ToolButton("Rotate about Z (clockwise)", "zcw", () => RotateSelected(2, -1)));
        strip.Items.Add(new ToolStripSeparator());

        strip.Items.Add(new ToolStripLabel("Size"));
        strip.Items.Add(new ToolStripButton("Grow", null, (_, _) => ResizeSelected(1.1f)) { ToolTipText = "Enlarge by 10 %" });
        strip.Items.Add(new ToolStripButton("Shrink", null, (_, _) => ResizeSelected(1 / 1.1f)) { ToolTipText = "Shrink by 10 %" });
        return strip;
    }

    private Control BuildTimeline()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 4, RowCount = 1, AutoSize = true, Padding = new Padding(4) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _playButton.Text = "Play";
        _playButton.AutoSize = true;
        _playButton.Click += (_, _) => TogglePlay();
        _loopCheck.Text = "Loop";
        _loopCheck.AutoSize = true;
        _loopCheck.Anchor = AnchorStyles.Left;
        _timeline.Minimum = 0;
        _timeline.Maximum = 1000;
        _timeline.TickStyle = TickStyle.None;
        _timeline.Dock = DockStyle.Fill;
        _timeline.Scroll += (_, _) =>
        {
            StopPlay();
            _previewTime = TimelineToTime(_timeline.Value);
            UpdateTimeLabel();
            UpdateStatus();
            _gl.Invalidate();
        };
        _timeLabel.AutoSize = true;
        _timeLabel.Anchor = AnchorStyles.Left;
        _timeLabel.MinimumSize = new Size(LogicalToDeviceUnits(190), 0);

        panel.Controls.Add(_playButton, 0, 0);
        panel.Controls.Add(_loopCheck, 1, 0);
        panel.Controls.Add(_timeline, 2, 0);
        panel.Controls.Add(_timeLabel, 3, 0);
        return panel;
    }

    // ==================================================================================== rendering

    private void RenderView()
    {
        try
        {
            _gl.MakeCurrent();
            _renderer ??= new SceneRenderer();

            Scene? scene = _previewTime is float t ? _animation.Evaluate(t) : _currentScene;
            int w = _gl.ClientSize.Width, h = _gl.ClientSize.Height;
            var (view, proj) = ViewSetup.Compute(scene, _viewMode, _editor, w, h, out string? warning);
            _renderer.Render(scene, new RenderOptions
            {
                Wireframe = _wireframe,
                AlphaBlend = _settings.AlphaBlend,
                Selected = _previewTime == null ? _selected : null,
                View = view,
                Projection = proj,
                ResolveTexture = ResolveTexture,
                Modern = _modernView ? _animation.Modern : null,
                Samples = 4,
            }, w, h);
            _gl.SwapBuffers();

            var missing = _renderer.Textures.Missing;
            if (missing.Count > 0)
            {
                string tex = "Texture not found: " + string.Join(", ", missing.Take(3).Select(Path.GetFileName));
                warning = warning == null ? tex : warning + "  " + tex;
            }
            _warningStatus.Text = warning ?? "";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _warningStatus.Text = "Rendering failed: " + ex.Message;
        }
    }

    /// <summary>
    /// Resolves a texture name as stored in the model: absolute paths as they are, bare names next
    /// to the animation file (like the original), then in the bundled Scenes folder.
    /// </summary>
    private string? ResolveTexture(string name)
    {
        if (_textureLookup.TryGetValue(name, out var known) && File.Exists(known)) return known;
        var candidates = new List<string>();
        if (Path.IsPathRooted(name)) candidates.Add(name);
        string fileName = Path.GetFileName(name);
        if (_filePath != null) candidates.Add(Path.Combine(Path.GetDirectoryName(_filePath)!, fileName));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "Scenes", fileName));
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                _textureLookup[name] = c;
                return c;
            }
        }
        return null;
    }

    // ==================================================================================== mouse

    private void Gl_MouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_pressed) return;
        int slop = SystemInformation.DragSize.Width / 2;
        if (!_dragged && Math.Abs(e.X - _downPoint.X) <= slop && Math.Abs(e.Y - _downPoint.Y) <= slop) return;
        _dragged = true;
        if (_viewMode == ViewMode.Camera) return;
        int dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
        _lastMouse = e.Location;
        _editor.Orbit(-dx * 0.4f, -dy * 0.4f);
        _gl.Invalidate();
    }

    /// <summary>
    /// Selects the element under the mouse in the view (new in the port; the original could only
    /// select in the Object List). Alt+click selects the enclosing group, repeated Alt+clicks go
    /// further up. Clicking the background selects the key state itself.
    /// </summary>
    private void PickAt(Point location, bool selectGroup)
    {
        if (_renderer == null) return;
        Element? hit;
        Scene key = _currentScene;
        try
        {
            _gl.MakeCurrent();
            Scene? shown = _previewTime is float t ? _animation.Evaluate(t) : _currentScene;
            int w = _gl.ClientSize.Width, h = _gl.ClientSize.Height;
            var (view, proj) = ViewSetup.Compute(shown, _viewMode, _editor, w, h, out _);
            hit = _renderer.Pick(shown, new RenderOptions
            {
                Wireframe = _wireframe,
                AlphaBlend = _settings.AlphaBlend,
                View = view,
                Projection = proj,
                ResolveTexture = ResolveTexture,
            }, w, h, location.X, location.Y, LogicalToDeviceUnits(4));

            // while previewing, the picture is an in-between copy of the key state the interval
            // starts at: select the same element (by name path) in that key state
            if (_previewTime is float pt && hit != null)
            {
                key = _animation.Scenes.LastOrDefault(s => s.AniTime <= pt) ?? _animation.Scenes[0];
                hit = FindByPath(key, hit);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _warningStatus.Text = "Picking failed: " + ex.Message;
            return;
        }
        finally
        {
            _gl.Invalidate();
        }

        Element target = hit ?? key;
        if (hit != null && selectGroup)
        {
            bool selectionContainsHit = _selected is not null and not Scene && IsSelfOrDescendant(hit, _selected);
            var start = selectionContainsHit ? _selected! : hit;
            target = start.Parent is { } parent and not Scene ? parent : start;
        }

        if (_nodes.TryGetValue(target, out var node))
        {
            _tree.SelectedNode = node;
            node.EnsureVisible();
        }
    }

    /// <summary>The element of <paramref name="scene"/> at the same name path as <paramref name="element"/> (from another copy of the state).</summary>
    private static Element? FindByPath(Scene scene, Element element)
    {
        var path = new Stack<Element>();
        for (Element? e = element; e != null && e is not Scene; e = e.Parent) path.Push(e);
        GroupElement current = scene;
        Element? found = null;
        while (path.Count > 0)
        {
            var step = path.Pop();
            found = current.FindChild(step.Name);
            if (found == null || found.Type != step.Type) return null;
            if (path.Count > 0)
            {
                if (found is not GroupElement g) return null;
                current = g;
            }
        }
        return found;
    }

    // ==================================================================================== tree

    private static string NodeText(object o) => o switch
    {
        Animation a => a.Name,
        Scene s => $"{s.Name}  @ {s.AniTime:0.###} s",
        Element e => e.Name,
        _ => o.ToString() ?? "",
    };

    private static string ImageKey(object o) => o switch
    {
        Animation => "Animation",
        Element e => e.Type.ToString(),
        _ => "",
    };

    private TreeNode CreateNode(object o)
    {
        var node = new TreeNode(NodeText(o)) { Tag = o, ImageKey = ImageKey(o), SelectedImageKey = ImageKey(o) };
        _nodes[o] = node;
        if (o is GroupElement g)
            foreach (var c in g.Children)
                node.Nodes.Add(CreateNode(c));
        return node;
    }

    private void RebuildTree()
    {
        _suppressTreeSelect = true;
        _tree.BeginUpdate();
        try
        {
            _nodes.Clear();
            _tree.Nodes.Clear();
            var root = CreateNode(_animation);
            foreach (var s in _animation.Scenes)
                root.Nodes.Add(CreateNode(s));
            _tree.Nodes.Add(root);
            root.Expand();
            if (_nodes.TryGetValue(_currentScene, out var sn)) sn.ExpandAll();

            object target = (object?)_selected ?? _currentScene;
            if (_nodes.TryGetValue(target, out var sel))
            {
                _tree.SelectedNode = sel;
                sel.EnsureVisible();
            }
        }
        finally
        {
            _tree.EndUpdate();
            _suppressTreeSelect = false;
        }
        ShowProperties();
        UpdateStatus();
    }

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (_suppressTreeSelect || e.Node == null) return;
        switch (e.Node.Tag)
        {
            case Animation:
                _selected = null;
                break;
            case Scene s:
                _currentScene = s;
                _selected = s;
                break;
            case Element el:
                _currentScene = SceneOf(el) ?? _currentScene;
                _selected = el;
                break;
        }
        StopPlay();
        _previewTime = null;
        ShowProperties();
        UpdateTimeline();
        UpdateStatus();
        _gl.Invalidate();
    }

    private void Tree_AfterLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        e.CancelEdit = true; // we set the (sanitised) text ourselves
        if (e.Label == null || e.Node == null) return;
        string label = e.Label.Trim();
        if (label.Length == 0) return;
        var node = e.Node;
        switch (node.Tag)
        {
            case Animation a:
                a.Name = label;
                break;
            case Scene s:
                s.Name = label.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                break;
            case Element el:
                el.Name = label;
                break;
            default:
                return;
        }
        MarkDirty();
        BeginInvoke(() =>
        {
            node.Text = NodeText(node.Tag!);
            _grid.Refresh();
        });
    }

    private void Tree_DragOver(object? sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;
        if (e.Data?.GetData(typeof(TreeNode)) is not TreeNode { Tag: Element source }) return;
        var target = _tree.GetNodeAt(_tree.PointToClient(new Point(e.X, e.Y)));
        if (target?.Tag is not Element dest) return;
        if (IsSelfOrDescendant(dest, source)) return;
        e.Effect = (e.KeyState & 8) != 0 || (e.KeyState & 4) != 0 ? DragDropEffects.Copy : DragDropEffects.Move;
        target.EnsureVisible();
    }

    private void Tree_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(TreeNode)) is not TreeNode { Tag: Element source }) return;
        var targetNode = _tree.GetNodeAt(_tree.PointToClient(new Point(e.X, e.Y)));
        if (targetNode?.Tag is not Element dest || IsSelfOrDescendant(dest, source)) return;
        bool copy = e.Effect == DragDropEffects.Copy;

        GroupElement container;
        int index;
        if (dest is GroupElement g)
        {
            container = g;
            index = g.Children.Count;
        }
        else
        {
            container = dest.Parent!;
            index = container.IndexOf(dest);
        }

        var destScene = container as Scene ?? SceneOf(container);
        if (destScene == null) return;
        bool crossScene = !ReferenceEquals(destScene, SceneOf(source));
        if ((copy || crossScene) && !CheckLimits(destScene, source)) return;

        var element = copy ? source.Clone() : source;
        if (!copy && ReferenceEquals(source.Parent, container) && container.IndexOf(source) < index) index--;
        container.Insert(index, element);
        // names must stay unique among siblings (they identify the element between key states)
        if (container.Children.Any(c => !ReferenceEquals(c, element) && string.Equals(c.Name, element.Name, StringComparison.OrdinalIgnoreCase)))
            element.Name = UniqueName(container, element.Name);

        _currentScene = destScene;
        _selected = element;
        MarkDirty();
        RebuildTree();
        _gl.Invalidate();
    }

    private static bool IsSelfOrDescendant(Element candidate, Element ancestor)
    {
        for (Element? e = candidate; e != null; e = e.Parent)
            if (ReferenceEquals(e, ancestor)) return true;
        return false;
    }

    private static Scene? SceneOf(Element e)
    {
        for (Element? x = e; x != null; x = x.Parent)
            if (x is Scene s) return s;
        return null;
    }

    // ==================================================================================== properties

    private void ShowProperties()
    {
        _grid.SelectedObject = _tree.SelectedNode?.Tag switch
        {
            Animation a => a,
            Element e => new ElementView(e),
            _ => null,
        };
    }

    private void Grid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
    {
        string? prop = e.ChangedItem?.PropertyDescriptor?.Name;
        MarkDirty();

        if (_selected is CameraElement cam && prop == nameof(CameraElement.Active) && cam.Active)
            SceneOf(cam)?.SetActiveCamera(cam);

        if (prop == nameof(Scene.AniTime) || prop == nameof(Animation.Name) || prop == nameof(Element.Name))
        {
            if (prop == nameof(Scene.AniTime)) SortStates();
            RebuildTree();
        }
        else
        {
            _grid.Refresh();
        }
        UpdateTimeline();
        UpdateStatus();
        _gl.Invalidate();
    }

    /// <summary>Key states are always kept in time order.</summary>
    private void SortStates()
    {
        var sorted = _animation.Scenes.OrderBy(sc => sc.AniTime).ToList();
        _animation.Scenes.Clear();
        _animation.Scenes.AddRange(sorted);
    }

    // ==================================================================================== editing

    private GroupElement InsertTarget()
    {
        return _selected switch
        {
            GroupElement g => g,
            { Parent: { } p } => p,
            _ => _currentScene,
        };
    }

    private void InsertElement(Element element)
    {
        if (_previewTime != null) ExitPreview();
        var container = InsertTarget();
        var scene = container as Scene ?? SceneOf(container) ?? _currentScene;
        if (!CheckLimits(scene, element)) return;

        element.Name = UniqueName(scene, element.Name);
        container.Add(element);
        if (element is CameraElement cam && scene.ActiveCamera == null)
            cam.Active = true;

        _currentScene = scene;
        _selected = element;
        MarkDirty();
        RebuildTree();
        _gl.Invalidate();
    }

    /// <summary>Original limit: GL_LIGHT0..7 and eight cameras per key state.</summary>
    private bool CheckLimits(Scene scene, Element adding)
    {
        int lights = adding is GroupElement ag ? ag.Descendants().OfType<LightElement>().Count() : adding is LightElement ? 1 : 0;
        int cameras = adding is GroupElement cg ? cg.Descendants().OfType<CameraElement>().Count() : adding is CameraElement ? 1 : 0;
        if (lights > 0 && scene.Lights.Count() + lights > LightElement.MaxPerScene)
        {
            MessageBox.Show(this, $"A key state can contain at most {LightElement.MaxPerScene} lights.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (cameras > 0 && scene.Cameras.Count() + cameras > CameraElement.MaxPerScene)
        {
            MessageBox.Show(this, $"A key state can contain at most {CameraElement.MaxPerScene} cameras.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private static string UniqueName(GroupElement scope, string baseName)
    {
        var used = new HashSet<string>(scope.Descendants().Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(baseName)) return baseName;
        for (int i = 1; ; i++)
        {
            string suffix = i.ToString();
            string stem = baseName.Length + suffix.Length > Element.MaxNameLength ? baseName[..(Element.MaxNameLength - suffix.Length)] : baseName;
            if (!used.Contains(stem + suffix)) return stem + suffix;
        }
    }

    private void DeleteSelected()
    {
        if (_tree.SelectedNode is { IsEditing: true }) return;
        if (_selected is Scene)
        {
            DeleteState();
            return;
        }
        if (_selected?.Parent is not { } parent) return;
        int index = parent.IndexOf(_selected);
        parent.Remove(_selected);
        _selected = index < parent.Children.Count ? parent.Children[index] : index > 0 ? parent.Children[index - 1] : parent;
        MarkDirty();
        RebuildTree();
        _gl.Invalidate();
    }

    private void SetActiveCamera()
    {
        if (_selected is not CameraElement cam || SceneOf(cam) is not { } scene) return;
        scene.SetActiveCamera(cam);
        MarkDirty();
        _grid.Refresh();
        _gl.Invalidate();
    }

    private void MoveSelected(int dx, int dy, int dz)
    {
        if (_selected == null) return;
        float step = (float)_moveStep.Value;
        _selected.X += dx * step;
        _selected.Y += dy * step;
        _selected.Z += dz * step;
        AfterToolEdit();
    }

    private void RotateSelected(int axis, int sign)
    {
        if (_selected == null) return;
        float deg = sign * (float)_rotateStep.Value;
        if (_selected is LightElement light)
        {
            // for lights alpha/beta/gamma are the spot direction vector: rotate the vector
            float rad = deg * MathF.PI / 180f;
            var m = axis switch { 0 => Matrix4x4.CreateRotationX(rad), 1 => Matrix4x4.CreateRotationY(rad), _ => Matrix4x4.CreateRotationZ(rad) };
            var v = Vector3.TransformNormal(new Vector3(light.Alpha, light.Beta, light.Gamma), m);
            (light.Alpha, light.Beta, light.Gamma) = (v.X, v.Y, v.Z);
        }
        else
        {
            switch (axis)
            {
                case 0: _selected.Alpha += deg; break;
                case 1: _selected.Beta += deg; break;
                default: _selected.Gamma += deg; break;
            }
        }
        AfterToolEdit();
    }

    private void ResizeSelected(float factor)
    {
        if (_selected == null) return;
        _selected.Resize(factor);
        AfterToolEdit();
    }

    private void AfterToolEdit()
    {
        if (_previewTime != null) ExitPreview();
        MarkDirty();
        _grid.Refresh();
        _gl.Invalidate();
    }

    // ==================================================================================== key states

    private void GoToState(int index)
    {
        if (_animation.Scenes.Count == 0) return;
        index = Math.Clamp(index, 0, _animation.Scenes.Count - 1);
        _currentScene = _animation.Scenes[index];
        _selected = _currentScene;
        _previewTime = null;
        StopPlay();
        RebuildTree();
        UpdateTimeline();
        _gl.Invalidate();
    }

    /// <summary>Original Next/Previous buttons; "Next" on the last state creates a copy 5 s later.</summary>
    private void StepState(int delta)
    {
        int index = _animation.Scenes.IndexOf(_currentScene);
        if (delta > 0 && index == _animation.Scenes.Count - 1)
        {
            _animation.InsertCopyAfter(_currentScene, _currentScene);
            MarkDirty();
        }
        GoToState(index + delta);
    }

    private void DuplicateState()
    {
        var copy = _animation.InsertCopyAfter(_currentScene, _currentScene);
        MarkDirty();
        GoToState(_animation.Scenes.IndexOf(copy));
    }

    private void DeleteState()
    {
        if (_animation.Scenes.Count <= 1)
        {
            MessageBox.Show(this, "The animation needs at least one key state.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, $"Delete key state '{_currentScene.Name}' at {_currentScene.AniTime:0.###} s?", AppTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        int index = _animation.Scenes.IndexOf(_currentScene);
        _animation.Scenes.RemoveAt(index);
        MarkDirty();
        GoToState(Math.Min(index, _animation.Scenes.Count - 1));
    }

    // ==================================================================================== view

    private void SetViewMode(ViewMode mode)
    {
        _viewMode = mode;
        if (mode != ViewMode.Camera) _editor.Reset(mode);
        UpdateViewChecks();
        UpdateStatus();
        _gl.Invalidate();
    }

    private void UpdateViewChecks()
    {
        foreach (var (mode, item) in _viewItems) item.Checked = mode == _viewMode;
        _wireItem.Checked = _wireframe;
        _modernItem.Checked = _modernView;
    }

    // ==================================================================================== timeline & playback

    private float TimelineToTime(int value)
    {
        float start = _animation.StartTime, end = _animation.EndTime;
        return start + (end - start) * value / _timeline.Maximum;
    }

    private int TimeToTimeline(float t)
    {
        float start = _animation.StartTime, end = _animation.EndTime;
        if (end <= start) return 0;
        return (int)Math.Round(Math.Clamp((t - start) / (end - start), 0, 1) * _timeline.Maximum);
    }

    private void UpdateTimeline()
    {
        float t = _previewTime ?? _currentScene.AniTime;
        _timeline.Value = TimeToTimeline(t);
        _timeline.Enabled = _animation.Scenes.Count > 1;
        UpdateTimeLabel();
    }

    private void UpdateTimeLabel()
    {
        float t = _previewTime ?? _currentScene.AniTime;
        string mode = _previewTime != null ? "preview" : "key state";
        _timeLabel.Text = $"t = {t:0.00} s / {_animation.EndTime:0.00} s ({mode})";
    }

    private void ExitPreview()
    {
        StopPlay();
        _previewTime = null;
        UpdateTimeline();
        UpdateStatus();
    }

    private void TogglePlay()
    {
        if (_playTimer.Enabled)
        {
            StopPlay();
            return;
        }
        if (_animation.Scenes.Count < 2)
        {
            MessageBox.Show(this, "The animation needs at least two key states to play. Use View > Next key state (PgDn) to add one.",
                AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        float t = _previewTime ?? _animation.StartTime;
        if (t >= _animation.EndTime) t = _animation.StartTime;
        _playStart = t;
        _playClock.Restart();
        _playTimer.Start();
        _playButton.Text = "Stop";
    }

    private void StopPlay()
    {
        if (!_playTimer.Enabled) return;
        _playTimer.Stop();
        _playClock.Stop();
        _playButton.Text = "Play";
    }

    private void PlayTick()
    {
        float t = _playStart + (float)_playClock.Elapsed.TotalSeconds;
        if (t > _animation.EndTime)
        {
            if (_loopCheck.Checked)
            {
                _playStart = _animation.StartTime;
                _playClock.Restart();
                t = _playStart;
            }
            else
            {
                t = _animation.EndTime;
                StopPlay();
            }
        }
        _previewTime = t;
        _timeline.Value = TimeToTimeline(t);
        UpdateTimeLabel();
        UpdateStatus();
        _gl.Invalidate();
    }

    // ==================================================================================== files

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        var r = MessageBox.Show(this, $"Save changes to '{_animation.Name}'?", AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return r switch
        {
            DialogResult.Yes => Save(false),
            DialogResult.No => true,
            _ => false,
        };
    }

    private void NewAnimation()
    {
        if (!ConfirmDiscard()) return;
        SetDocument(Animation.CreateNew(), null);
    }

    private void OpenWithDialog()
    {
        if (!ConfirmDiscard()) return;
        using var dlg = new OpenFileDialog
        {
            Filter = "Overbryce animations (*.ani)|*.ani|All files|*.*",
            InitialDirectory = _filePath != null ? Path.GetDirectoryName(_filePath) : Path.Combine(AppContext.BaseDirectory, "Scenes"),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) OpenFile(dlg.FileName);
    }

    private void OpenFile(string path)
    {
        try
        {
            var animation = AniFile.Load(path);
            if (animation.Scenes.Count == 0) animation.Scenes.Add(new Scene("Scena"));
            SetDocument(animation, Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or AniFormatException or FormatException)
        {
            MessageBox.Show(this, $"Cannot open '{path}':\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetDocument(Animation animation, string? path)
    {
        StopPlay();
        _animation = animation;
        _filePath = path;
        _dirty = false;
        _previewTime = null;
        _currentScene = animation.Scenes[0];
        _selected = _currentScene;
        _textureLookup.Clear();
        _modernView = animation.Modern.Enabled;
        UpdateViewChecks();
        if (_renderer != null)
        {
            _gl.MakeCurrent();
            _renderer.Textures.Clear();
        }
        TextureFileEditor.BaseDirectory = path != null ? Path.GetDirectoryName(path) : null;

        // like the original, show the state through its camera when it has an active one
        SetViewMode(_currentScene.ActiveCamera != null ? ViewMode.Camera : ViewMode.Front);
        RebuildTree();
        UpdateTimeline();
        UpdateTitle();
        _gl.Invalidate();
    }

    private bool Save(bool saveAs)
    {
        string? path = _filePath;
        if (saveAs || path == null)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Overbryce animations (*.ani)|*.ani",
                FileName = path != null ? Path.GetFileName(path) : _animation.Name + ".ani",
                InitialDirectory = path != null ? Path.GetDirectoryName(path) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;
            path = dlg.FileName;
        }

        try
        {
            CopyTexturesTo(Path.GetDirectoryName(Path.GetFullPath(path))!);
            AniFile.Save(_animation, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Cannot save '{path}':\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _filePath = Path.GetFullPath(path);
        TextureFileEditor.BaseDirectory = Path.GetDirectoryName(_filePath);
        _dirty = false;
        UpdateTitle();
        _grid.Refresh();
        return true;
    }

    /// <summary>
    /// The .ani format stores bare texture file names that are looked up next to the animation, so
    /// textures picked from elsewhere are copied into the target directory when saving.
    /// </summary>
    private void CopyTexturesTo(string directory)
    {
        var all = _animation.Scenes.SelectMany(s => s.Descendants()).Where(e => e.HasTexture).ToList();
        foreach (var e in all)
        {
            string? source = ResolveTexture(e.Texture!);
            string fileName = Path.GetFileName(e.Texture!).Replace(' ', '_');
            string dest = Path.Combine(directory, fileName);
            if (source != null && !File.Exists(dest))
                File.Copy(source, dest);
            if (File.Exists(dest))
            {
                _textureLookup[fileName] = dest;
                e.Texture = fileName;
            }
        }
    }

    private void EditAnimationSettings()
    {
        if (!AnimationSettingsDialog.Show(this, _animation)) return;
        MarkDirty();
        RebuildTree();
    }

    /// <summary>The modern renderer's settings of this animation; the view switches to modern rendering to show them.</summary>
    private void EditModernSettings()
    {
        if (!ModernSettingsDialog.Show(this, _animation.Modern, () => { _modernView = true; UpdateViewChecks(); UpdateStatus(); RenderView(); }))
        {
            _gl.Invalidate();
            return;
        }
        _modernView = true;
        UpdateViewChecks();
        UpdateStatus();
        MarkDirty();
        _gl.Invalidate();
    }

    // ==================================================================================== export

    /// <summary>
    /// Original "Compute animation": renders every frame through the active camera and saves it as
    /// name000000.bmp, name000001.bmp, ... plus list.lst with the file names.
    /// </summary>
    private void ExportFrames()
    {
        if (_animation.Scenes.Count < 2)
        {
            MessageBox.Show(this, "The animation needs at least two key states.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string ext = _settings.ExportPng ? ".png" : ".bmp";
        using var dlg = new SaveFileDialog
        {
            Title = "Export frames - choose the base file name",
            Filter = _settings.ExportPng ? "PNG images (*.png)|*.png" : "Bitmaps (*.bmp)|*.bmp",
            FileName = _animation.Name + ext,
            InitialDirectory = _filePath != null ? Path.GetDirectoryName(_filePath) : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string dir = Path.GetDirectoryName(dlg.FileName)!;
        string stem = Path.GetFileNameWithoutExtension(dlg.FileName);
        var times = _animation.FrameTimes().ToList();
        var names = new List<string>(times.Count);
        var format = _settings.ExportPng ? ImageFormat.Png : ImageFormat.Bmp;
        var size = _settings.ExportSize;

        StopPlay();
        _exportCancel = false;
        Enabled = false;
        var cancelHook = new KeyEventHandler((_, e) => { if (e.KeyCode == Keys.Escape) _exportCancel = true; });
        KeyDown += cancelHook;
        try
        {
            _gl.MakeCurrent();
            _renderer ??= new SceneRenderer();
            using var target = new OffscreenTarget(size.Width, size.Height, _settings.Antialias ? 8 : 0);
            for (int i = 0; i < times.Count && !_exportCancel; i++)
            {
                var scene = _animation.Evaluate(times[i]);
                var (view, proj) = ViewSetup.Compute(scene, ViewMode.Camera, _editor, size.Width, size.Height, out _);
                target.Bind();
                _renderer.Render(scene, new RenderOptions
                {
                    Wireframe = _wireframe,
                    AlphaBlend = _settings.AlphaBlend,
                    View = view,
                    Projection = proj,
                    ResolveTexture = ResolveTexture,
                    Modern = _modernView ? _animation.Modern : null,
                    Samples = _settings.Antialias ? 8 : 0,
                }, size.Width, size.Height);
                using var bmp = target.ReadBitmap();
                string name = $"{stem}{i:000000}{ext}";
                bmp.Save(Path.Combine(dir, name), format);
                names.Add(name);

                _warningStatus.Text = $"Exporting frame {i + 1} / {times.Count} (Esc cancels)...";
                Application.DoEvents();
            }
            File.WriteAllLines(Path.Combine(dir, "list.lst"), names);
            _warningStatus.Text = (_exportCancel ? "Export cancelled after " : "Exported ") + $"{names.Count} frames to {dir}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            MessageBox.Show(this, "Export failed:\n" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            KeyDown -= cancelHook;
            Enabled = true;
            OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, 0);
            _gl.Invalidate();
        }
    }

    // ==================================================================================== misc

    private void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string name = _filePath != null ? Path.GetFileName(_filePath) : "(untitled)";
        Text = $"{name}{(_dirty ? " *" : "")} - {AppTitle}";
    }

    private void UpdateStatus()
    {
        int index = _animation.Scenes.IndexOf(_currentScene);
        _stateStatus.Text = _previewTime is float t
            ? $"Preview at {t:0.00} s"
            : $"Key state {index + 1} / {_animation.Scenes.Count}  ({_currentScene.Name}, {_currentScene.AniTime:0.###} s)";
        _viewStatus.Text = (_wireframe ? "Wire frame, " : _modernView ? "Modern, " : "") + _viewMode switch
        {
            ViewMode.Camera => "camera view",
            ViewMode.Front => "front view",
            ViewMode.Side => "side view",
            _ => "top view",
        };
        _selectionStatus.Text = _selected is null or Scene
            ? "Click an object to select it"
            : _selected.Parent is null or Scene
                ? $"Selected: {_selected.Name}"
                : $"Selected: {_selected.Name} in {_selected.Parent.Name} (Alt+click: group)";
    }

    private void ShowHelp()
    {
        MessageBox.Show(this,
            "Tree: select key states and elements, F2 renames, Del deletes, drag to move into another group/state (Ctrl+drag copies).\r\n" +
            "Elements with the same name in neighbouring key states are interpolated (Catmull-Rom spline over the key-state times).\r\n\r\n" +
            "View: click an object to select it, Alt+click selects its group (repeat to go further up), click the background to select the key state.\r\n" +
            "Left-drag orbits the editor camera, mouse wheel zooms (front/side/top views). Ctrl+1..4 switch views, Ctrl+W toggles wire frame, Ctrl+M modern rendering.\r\n" +
            "In the wire frame view lights (yellow) and cameras (green, active cyan) are shown and can be clicked too.\r\n" +
            "'Stop at this state' (Final) makes an element come to rest at that key state; otherwise it moves smoothly through it.\r\n" +
            "PgDn on the last key state creates a copy 5 s later; edit it to define the motion.\r\n\r\n" +
            "Timeline: drag to preview the interpolated animation, F5 plays it. File > Export frames renders BMP/PNG frames through the active camera.",
            "Controls", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // shortcuts that must not steal keys from text editing in the grid / tree label editor
        bool editing = _tree.SelectedNode is { IsEditing: true } || _grid.ContainsFocus;
        if (!editing)
        {
            switch (keyData)
            {
                case Keys.Delete: DeleteSelected(); return true;
                case Keys.F2: _tree.SelectedNode?.BeginEdit(); return true;
                case Keys.PageUp: StepState(-1); return true;
                case Keys.PageDown: StepState(+1); return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        StopPlay();
        if (_renderer != null)
        {
            _gl.MakeCurrent();
            _renderer.Dispose();
            _renderer = null;
        }
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _playTimer.Dispose();
            foreach (var img in _toolImages.Values) img?.Dispose();
        }
        base.Dispose(disposing);
    }
}
