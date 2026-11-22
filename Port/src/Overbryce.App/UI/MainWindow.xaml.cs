using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenTK.GLControl;
using OpenTK.Windowing.Common;
using Overbryce.App.Rendering;
using Overbryce.Core.IO;
using Overbryce.Core.Model;
using Forms = System.Windows.Forms;
using RenderOptions = Overbryce.App.Rendering.RenderOptions;

namespace Overbryce.App.UI;

/// <summary>
/// Main window (original TAniMain): element tree + properties on the left, OpenGL view with the
/// move/rotate tools on the right, key-state timeline below. The layout is in MainWindow.xaml.
/// </summary>
internal sealed partial class MainWindow : Window
{
    public const string AppTitle = "Overbryce Animator";

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

    // ------------------------------------------------------------------ playback and export
    private readonly DispatcherTimer _playTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(10) };
    private readonly Stopwatch _playClock = new();
    private float _playStart;
    private bool _exporting;
    private bool _exportCancel;

    // ------------------------------------------------------------------ controls
    private readonly GLControl _gl;
    private SceneRenderer? _renderer;
    private readonly Dictionary<object, TreeViewItem> _nodes = new(ReferenceEqualityComparer.Instance);
    private bool _suppressTreeSelect;
    private bool _settingTimeline;
    private TreeViewItem? _renaming;
    private readonly ContextMenu _meshMenu = new();
    private RaytracePreviewWindow? _raytraced;

    /// <summary>Menu shortcuts, read from the menu items' InputGestureText.</summary>
    private readonly List<(Key Key, ModifierKeys Modifiers, MenuItem Item)> _shortcuts = new();

    // mouse in the view (pixels of the OpenGL control)
    private System.Drawing.Point _lastMouse;
    private System.Drawing.Point _downPoint;
    private bool _dragged;
    private bool _pressed;

    // dragging in the tree
    private Point _treeDragStart;
    private TreeViewItem? _treeDragItem;

    public MainWindow(string? fileToOpen, SnapshotRequest? snapshot = null)
    {
        InitializeComponent();
        _currentScene = _animation.Scenes[0];

        _gl = new GLControl(new GLControlSettings
        {
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            NumberOfSamples = 4,
            DepthBits = 24,
        })
        {
            Dock = Forms.DockStyle.Fill,
        };
        ViewSlot.Child = new WindowsFormsHost { Child = _gl };

        BuildUi();
        _editor.Reset(ViewMode.Front);
        UpdateViewChecks();
        RebuildTree();
        UpdateTitle();
        UpdateTimeline();

        _playTimer.Tick += (_, _) => PlayTick();
        Theme.Changed += UpdateViewChecks;

        ContentRendered += (_, _) =>
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
            RaytraceBridge? bridge = null;
            var traceSettings = _animation.Raytrace.Clone();
            if (req.Samples is int samples) traceSettings.Samples = samples;
            if (req.Bounces is int bounces) traceSettings.Bounces = bounces;
            traceSettings.Normalize();

            void RenderTo(Scene? scene, float? time, int frame, string path)
            {
                if (req.Raytrace && !req.Wireframe && scene != null)
                {
                    bridge ??= new RaytraceBridge(ResolveTexture);
                    var job = bridge.Prepare(_animation, scene, time, req.View, editor, req.Size.Width, req.Size.Height,
                        _settings.AlphaBlend, classic: req.Modern == false, traceSettings, (uint)frame);
                    job.Trace(traceSettings.Samples);
                    using (var traced = RaytraceBridge.ToBitmap(job.Resolve()))
                        traced.Save(path, path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Bmp : ImageFormat.Png);
                    var st = job.Stats;
                    req.Log?.Invoke($"{Path.GetFileName(path)}: {st.Elements} elements, {st.Triangles} triangles, " +
                                    $"{st.Samples} samples, {(st.Prepare + st.Trace).TotalSeconds:0.0} s");
                    return;
                }

                var (view, proj) = ViewSetup.Compute(scene, req.View, editor, req.Size.Width, req.Size.Height, out _);
                target.Bind();
                _renderer.Render(scene, new RenderOptions
                {
                    Wireframe = req.Wireframe,
                    AlphaBlend = _settings.AlphaBlend,
                    View = view,
                    Projection = proj,
                    ResolveTexture = ResolveTexture,
                TextureSize = _animation.TextureSize,
                    Modern = (req.Modern ?? _animation.Modern.Enabled) ? _animation.Modern : null,
                    Samples = 8,
                }, req.Size.Width, req.Size.Height);
                using var bmp = target.ReadBitmap();
                bmp.Save(path, path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Bmp : ImageFormat.Png);
            }

            if (!req.Frames)
            {
                RenderTo(req.Time is float t ? _animation.Evaluate(t) : _currentScene, req.Time, 0, req.Output);
                return true;
            }

            Directory.CreateDirectory(req.Output);
            var times = _animation.FrameTimes().ToList();
            for (int i = 0; i < times.Count; i++)
            {
                if (times[i] < (req.From ?? float.MinValue) - 1e-4f || times[i] > (req.To ?? float.MaxValue) + 1e-4f) continue;
                RenderTo(_animation.Evaluate(times[i]), times[i], i, Path.Combine(req.Output, $"{_animation.Name}{i:000000}.png"));
            }
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            File.WriteAllText(req.Output + ".error.txt", ex.ToString());
            req.Log?.Invoke("Rendering failed: " + ex.Message);
            return false;
        }
    }

    // ==================================================================================== UI build

    private void BuildUi()
    {
        // ---------------------------------------------------------------- inserting elements
        var treeInsert = new MenuItem { Header = "Insert" };
        foreach (var (label, icon, factory) in InsertableElements())
        {
            InsertMenu.Items.Add(Item(label, icon, () => InsertElement(factory())));
            treeInsert.Items.Add(Item(label, icon, () => InsertElement(factory())));
            var button = new Button { Content = Icons.Shape(icon), ToolTip = "Insert " + label.ToLowerInvariant() };
            button.SetResourceReference(StyleProperty, "ToolButton");
            button.Click += (_, _) => InsertElement(factory());
            InsertButtons.Children.Add(button);
        }
        InsertMenu.Items.Add(new Separator());
        for (int i = 1; i <= 4; i++)
        {
            int preset = i;
            Element Mesh() => new MeshElement("NovyMesh", MeshPresets.Get(preset), MeshPresets.Size, MeshPresets.Size);
            InsertMenu.Items.Add(Item($"Bezier mesh preset {i}", "Mesh", () => InsertElement(Mesh())));
            _meshMenu.Items.Add(Item($"Preset {i}", "Mesh", () => InsertElement(Mesh())));
        }
        _meshMenu.Closed += (_, _) => MeshPresetsButton.IsChecked = false;

        // ---------------------------------------------------------------- tree
        var treeMenu = new ContextMenu();
        treeMenu.Items.Add(treeInsert);
        treeMenu.Items.Add(Item("Rename", null, BeginRename, Icons.Rename));
        treeMenu.Items.Add(Item("Delete", null, DeleteSelected, Icons.Delete));
        treeMenu.Items.Add(Item("Set as active camera", null, SetActiveCamera, Icons.Camera));
        treeMenu.Items.Add(new Separator());
        treeMenu.Items.Add(Item("Duplicate key state", null, DuplicateState, Icons.Copy));
        treeMenu.Items.Add(Item("Delete key state", null, DeleteState));
        Tree.ContextMenu = treeMenu;
        Tree.SelectedItemChanged += (_, _) => Tree_AfterSelect();
        Tree.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (ItemAt(e.OriginalSource) is { } item)
            {
                item.IsSelected = true;
                item.Focus();
            }
        };
        Tree.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _treeDragStart = e.GetPosition(Tree);
            _treeDragItem = ItemAt(e.OriginalSource);
        };
        Tree.PreviewMouseMove += Tree_PreviewMouseMove;
        Tree.DragOver += Tree_DragOver;
        Tree.Drop += Tree_Drop;

        // ---------------------------------------------------------------- properties
        Properties.PropertyValueChanged += Grid_PropertyValueChanged;

        // ---------------------------------------------------------------- view
        _gl.Paint += (_, _) => RenderView();
        _gl.Resize += (_, _) => _gl.Invalidate();
        _gl.MouseDown += (_, e) =>
        {
            _lastMouse = _downPoint = e.Location;
            _pressed = e.Button == Forms.MouseButtons.Left;
            _dragged = false;
            _gl.Focus();
        };
        _gl.MouseMove += Gl_MouseMove;
        _gl.MouseUp += (_, e) =>
        {
            // a click that only activated the window may arrive without its MouseDown: still pick
            if (e.Button == Forms.MouseButtons.Left && !(_pressed && _dragged))
                PickAt(e.Location, (Forms.Control.ModifierKeys & Forms.Keys.Alt) != 0);
            _pressed = _dragged = false;
        };
        _gl.MouseWheel += (_, e) =>
        {
            if (_viewMode == ViewMode.Camera) return;
            _editor.Zoom(e.Delta > 0 ? 1 / 1.1f : 1.1f);
            _gl.Invalidate();
        };
        // keys pressed while the view has the focus reach the window's PreviewKeyDown through the host

        // ---------------------------------------------------------------- tools and timeline
        foreach (var (axis, name) in new[] { (0, 'X'), (1, 'Y'), (2, 'Z') })
        {
            foreach (int sign in new[] { 1, -1 })
            {
                var button = new Button
                {
                    Content = Icons.Rotation(name, sign > 0),
                    ToolTip = $"Rotate about {name} ({(sign > 0 ? "counter-clockwise" : "clockwise")})",
                };
                button.SetResourceReference(StyleProperty, "ToolButton");
                button.Click += (_, _) => RotateSelected(axis, sign);
                RotateButtons.Children.Add(button);
            }
        }
        Timeline.ValueChanged += (_, _) =>
        {
            if (_settingTimeline) return;
            StopPlay();
            _previewTime = TimelineToTime(Timeline.Value);
            UpdateTimeLabel();
            UpdateStatus();
            _gl.Invalidate();
        };

        CollectShortcuts(MainMenu);
        PreviewKeyDown += (_, e) =>
        {
            if (_exporting)
            {
                if (e.Key == Key.Escape) _exportCancel = true;
                e.Handled = true;
                return;
            }
            if (HandleShortcut(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers)) e.Handled = true;
        };
    }

    private static MenuItem Item(string header, string? shape, Action onClick, string? glyph = null)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = shape != null ? Icons.Shape(shape) : glyph != null ? Icons.MenuGlyph(glyph) : null,
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static IEnumerable<(string Label, string Icon, Func<Element> Factory)> InsertableElements() =>
    [
        ("Sphere", "Sphere", () => new SphereElement()),
        ("Disk", "Disk", () => new DiskElement()),
        ("Truncated cone", "Cone", () => new ConeElement()),
        ("Bezier mesh", "Mesh", () => new MeshElement()),
        ("Box", "Box", () => new BoxElement()),
        ("Rectangle", "Rectangle", () => new RectangleElement()),
        ("Triangle", "Triangle", () => new TriangleElement()),
        ("Torus", "Torus", () => new TorusElement()),
        ("Light", "Light", () => new LightElement()),
        ("Camera", "Camera", () => new CameraElement()),
        ("Group", "Group", () => new GroupElement()),
    ];

    // ==================================================================================== shortcuts

    private void CollectShortcuts(ItemsControl menu)
    {
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            // Alt+F4 is the window's own
            if (!string.IsNullOrEmpty(item.InputGestureText) && item.InputGestureText != "Alt+F4" && ParseGesture(item.InputGestureText) is { } g)
                _shortcuts.Add((g.Key, g.Modifiers, item));
            CollectShortcuts(item);
        }
    }

    private static (Key Key, ModifierKeys Modifiers)? ParseGesture(string text)
    {
        var modifiers = ModifierKeys.None;
        var key = Key.None;
        foreach (string part in text.Split('+'))
        {
            switch (part)
            {
                case "Ctrl": modifiers |= ModifierKeys.Control; break;
                case "Shift": modifiers |= ModifierKeys.Shift; break;
                case "Alt": modifiers |= ModifierKeys.Alt; break;
                case "Del": key = Key.Delete; break;
                case "PgUp": key = Key.PageUp; break;
                case "PgDn": key = Key.PageDown; break;
                case [>= '0' and <= '9']: key = Key.D0 + (part[0] - '0'); break;
                default:
                    if (!Enum.TryParse(part, ignoreCase: true, out key)) return null;
                    break;
            }
        }
        return key == Key.None ? null : (key, modifiers);
    }

    /// <summary>Runs the menu item of the shortcut; keys that edit text stay with a text field that has the focus.</summary>
    private bool HandleShortcut(Key key, ModifierKeys modifiers)
    {
        foreach (var (k, m, item) in _shortcuts)
        {
            if (k != key || m != modifiers) continue;
            if ((key is Key.Delete or Key.F2 or Key.PageUp or Key.PageDown or Key.Home or Key.End) && IsEditingText()) return false;
            if (!item.IsEnabled) return false;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
            return true;
        }
        return false;
    }

    private bool IsEditingText() =>
        _renaming != null || Properties.IsKeyboardFocusWithin || Keyboard.FocusedElement is TextBoxBase or ComboBox or ComboBoxItem;

    // ==================================================================================== menu and tool bar

    private void New_Click(object sender, RoutedEventArgs e) => NewAnimation();
    private void Open_Click(object sender, RoutedEventArgs e) => OpenWithDialog();
    private void Save_Click(object sender, RoutedEventArgs e) => Save(false);
    private void SaveAs_Click(object sender, RoutedEventArgs e) => Save(true);
    private void Export_Click(object sender, RoutedEventArgs e) => ExportFrames();
    private void ImportBlender_Click(object sender, RoutedEventArgs e) => ImportBlender();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void RenderSettings_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsDialog.Show(this, _settings)) _gl.Invalidate();
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();
    private void Rename_Click(object sender, RoutedEventArgs e) => BeginRename();
    private void ActiveCamera_Click(object sender, RoutedEventArgs e) => SetActiveCamera();
    private void DuplicateState_Click(object sender, RoutedEventArgs e) => DuplicateState();
    private void DeleteState_Click(object sender, RoutedEventArgs e) => DeleteState();

    private void Wire_Click(object sender, RoutedEventArgs e)
    {
        _wireframe = !_wireframe;
        UpdateViewChecks();
        UpdateStatus();
        _gl.Invalidate();
    }

    private void Modern_Click(object sender, RoutedEventArgs e)
    {
        _modernView = !_modernView;
        UpdateViewChecks();
        UpdateStatus();
        _gl.Invalidate();
    }

    private void RayTrace_Click(object sender, RoutedEventArgs e) => RaytraceView();

    private void ViewMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string mode }) SetViewMode(Enum.Parse<ViewMode>(mode));
        UpdateViewChecks(); // a tool bar toggle must not switch itself off
    }

    private void FirstState_Click(object sender, RoutedEventArgs e) => GoToState(0);
    private void PreviousState_Click(object sender, RoutedEventArgs e) => StepState(-1);
    private void NextState_Click(object sender, RoutedEventArgs e) => StepState(+1);
    private void LastState_Click(object sender, RoutedEventArgs e) => GoToState(_animation.Scenes.Count - 1);

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string choice }) Theme.Set(Enum.Parse<ThemeChoice>(choice));
    }

    private void AnimationSettings_Click(object sender, RoutedEventArgs e) => EditAnimationSettings();
    private void ModernSettings_Click(object sender, RoutedEventArgs e) => EditModernSettings();

    private void RaytraceSettings_Click(object sender, RoutedEventArgs e)
    {
        if (RaytraceSettingsDialog.Show(this, _animation.Raytrace)) MarkDirty();
    }

    private void Play_Click(object sender, RoutedEventArgs e) => TogglePlay();
    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();
    private void About_Click(object sender, RoutedEventArgs e) => AboutDialog.Show(this);

    private void MeshPresets_Click(object sender, RoutedEventArgs e)
    {
        _meshMenu.PlacementTarget = MeshPresetsButton;
        _meshMenu.Placement = PlacementMode.Bottom;
        _meshMenu.IsOpen = true;
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        var d = ((string)((FrameworkElement)sender).Tag).Split(' ').Select(int.Parse).ToArray();
        MoveSelected(d[0], d[1], d[2]);
    }

    private void Resize_Click(object sender, RoutedEventArgs e) =>
        ResizeSelected(float.Parse((string)((FrameworkElement)sender).Tag, System.Globalization.CultureInfo.InvariantCulture));

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
                TextureSize = _animation.TextureSize,
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
            if (!_exporting) WarningStatus.Text = warning ?? "";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            WarningStatus.Text = "Rendering failed: " + ex.Message;
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

    private void Gl_MouseMove(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left || !_pressed) return;
        int slop = Forms.SystemInformation.DragSize.Width / 2;
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
    private void PickAt(System.Drawing.Point location, bool selectGroup)
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
                TextureSize = _animation.TextureSize,
            }, w, h, location.X, location.Y, _gl.LogicalToDeviceUnits(4));

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
            WarningStatus.Text = "Picking failed: " + ex.Message;
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

        if (TryGetNode(target, out var node)) SelectNode(node);
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

    private static string IconName(object o) => o switch
    {
        Animation => "Animation",
        Element e => e.Type.ToString(),
        _ => "Scene",
    };

    /// <summary>
    /// Creates the node of <paramref name="o"/> and its branch. A <paramref name="lazy"/> node gets a
    /// placeholder instead of its children, and builds them when it is opened or something in it is
    /// looked up (<see cref="TryGetNode"/>): an animation may have hundreds of key states with hundreds
    /// of elements each, far too many WPF items to build at once.
    /// </summary>
    private TreeViewItem CreateNode(object o, bool lazy = false)
    {
        var text = new TextBlock { Text = NodeText(o), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Children = { Icons.Shape(IconName(o)), text } };
        var node = new TreeViewItem { Header = header, Tag = o };
        node.Loaded += (_, _) => ShowChevronOpen(node);
        System.Windows.Automation.AutomationProperties.SetName(node, NodeText(o));
        _nodes[o] = node;
        if (o is GroupElement g)
        {
            if (lazy && g.Children.Count > 0)
            {
                node.Items.Add(new TreeViewItem { Tag = PendingBranch });
                node.Expanded += (_, e) =>
                {
                    if (ReferenceEquals(e.OriginalSource, node)) BuildBranch(node);
                };
            }
            else
            {
                foreach (var c in g.Children)
                    node.Items.Add(CreateNode(c));
            }
        }
        return node;
    }

    /// <summary>The placeholder child of a node whose branch is not built yet.</summary>
    private static readonly object PendingBranch = new();

    private void BuildBranch(TreeViewItem node)
    {
        if (node.Items.Count != 1 || node.Items[0] is not TreeViewItem { Tag: var tag } || tag != PendingBranch || node.Tag is not GroupElement g)
            return;
        node.Items.Clear();
        foreach (var c in g.Children)
            node.Items.Add(CreateNode(c));
    }

    /// <summary>The node of <paramref name="o"/>, building the branch of its key state if needed.</summary>
    private bool TryGetNode(object o, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TreeViewItem? node)
    {
        if (_nodes.TryGetValue(o, out node)) return true;
        if (o is Element e && SceneOf(e) is { } scene && _nodes.TryGetValue(scene, out var sceneNode))
        {
            BuildBranch(sceneNode);
            return _nodes.TryGetValue(o, out node);
        }
        return false;
    }

    /// <summary>
    /// Fluent turns a node's chevron by an animation when the node opens, which does not run for a node
    /// that is already open when it appears: turn it here.
    /// </summary>
    private static void ShowChevronOpen(TreeViewItem node)
    {
        if (!node.IsExpanded || node.Template?.FindName("Expander", node) is not ToggleButton toggle) return;
        toggle.ApplyTemplate();
        if (toggle.Template?.FindName("ChevronContainer", toggle) is UIElement { RenderTransform: RotateTransform { IsFrozen: false } turn } && turn.Angle == 0)
            turn.Angle = 90;
    }

    private static TextBlock NodeLabel(TreeViewItem node) => (TextBlock)((StackPanel)node.Header).Children[1];

    private static void ExpandAll(TreeViewItem node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Items.OfType<TreeViewItem>()) ExpandAll(child);
    }

    /// <summary>Selects a node, opening its branch and scrolling it into view.</summary>
    private static void SelectNode(TreeViewItem node)
    {
        for (var p = node.Parent as TreeViewItem; p != null; p = p.Parent as TreeViewItem) p.IsExpanded = true;
        node.IsSelected = true;
        node.Dispatcher.InvokeAsync(node.BringIntoView, DispatcherPriority.Loaded);
    }

    private static TreeViewItem? ItemAt(object? source)
    {
        for (var d = source as DependencyObject; d != null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is TreeViewItem item) return item;
        return null;
    }

    private void RebuildTree()
    {
        _suppressTreeSelect = true;
        try
        {
            _nodes.Clear();
            Tree.Items.Clear();
            var root = CreateNode(_animation);
            foreach (var s in _animation.Scenes)
                root.Items.Add(CreateNode(s, lazy: !ReferenceEquals(s, _currentScene)));
            Tree.Items.Add(root);
            root.IsExpanded = true;
            if (_nodes.TryGetValue(_currentScene, out var sn)) ExpandAll(sn);

            object target = (object?)_selected ?? _currentScene;
            if (TryGetNode(target, out var sel)) SelectNode(sel);
        }
        finally
        {
            _suppressTreeSelect = false;
        }
        ShowProperties();
        UpdateStatus();
    }

    private void Tree_AfterSelect()
    {
        if (_suppressTreeSelect || Tree.SelectedItem is not TreeViewItem node) return;
        switch (node.Tag)
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

    /// <summary>Edits the name of the selected node in place (F2); Enter or leaving the field takes it, Esc cancels.</summary>
    private void BeginRename()
    {
        if (_renaming != null || Tree.SelectedItem is not TreeViewItem { Tag: { } tag } node) return;
        var header = (StackPanel)node.Header;
        var label = NodeLabel(node);
        var box = new TextBox
        {
            Text = tag switch { Animation a => a.Name, Element el => el.Name, _ => label.Text },
            MinWidth = 140,
            MinHeight = 0,
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(4, 0, 0, 0),
        };
        header.Children[1] = box;
        _renaming = node;
        bool done = false;

        void Finish(bool commit)
        {
            if (done) return;
            done = true;
            header.Children[1] = label;
            _renaming = null;
            if (commit) Rename(node, box.Text);
            node.Focus();
        }

        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Finish(true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
        };
        box.LostKeyboardFocus += (_, _) => Finish(true);
        box.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
    }

    private void Rename(TreeViewItem node, string text)
    {
        string label = text.Trim();
        if (label.Length == 0) return;
        switch (node.Tag)
        {
            case Animation a:
                if (a.Name == label) return;
                a.Name = label;
                break;
            case Scene s:
                label = label.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (s.Name == label) return;
                s.Name = label;
                break;
            case Element el:
                if (el.Name == label) return;
                el.Name = label;
                break;
            default:
                return;
        }
        MarkDirty();
        NodeLabel(node).Text = NodeText(node.Tag!);
        System.Windows.Automation.AutomationProperties.SetName(node, NodeText(node.Tag!));
        Properties.RefreshValues();
    }

    private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _renaming != null || _treeDragItem is not { Tag: Element and not Scene } item) return;
        var p = e.GetPosition(Tree);
        if (Math.Abs(p.X - _treeDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _treeDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _treeDragItem = null;
        DragDrop.DoDragDrop(item, new DataObject(DragFormat, item.Tag), DragDropEffects.Move | DragDropEffects.Copy);
    }

    private const string DragFormat = "Overbryce.Element";

    /// <summary>Ctrl (or Shift) while dragging copies.</summary>
    private static bool CopyKey(DragEventArgs e) => (e.KeyStates & (DragDropKeyStates.ControlKey | DragDropKeyStates.ShiftKey)) != 0;

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        if (e.Data.GetData(DragFormat) is not Element source) return;
        if (ItemAt(e.OriginalSource) is not { Tag: Element dest } target || IsSelfOrDescendant(dest, source)) return;
        e.Effects = CopyKey(e) ? DragDropEffects.Copy : DragDropEffects.Move;
        target.BringIntoView();
    }

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DragFormat) is not Element source) return;
        if (ItemAt(e.OriginalSource) is not { Tag: Element dest } || IsSelfOrDescendant(dest, source)) return;
        bool copy = CopyKey(e);

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
        Properties.SelectedObject = (Tree.SelectedItem as TreeViewItem)?.Tag switch
        {
            Animation a => a,
            Element e => new ElementView(e),
            _ => null,
        };
    }

    private void Grid_PropertyValueChanged(string prop)
    {
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
            Properties.RefreshValues();
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
            Message.Show(this, $"A key state can contain at most {LightElement.MaxPerScene} lights.", MessageKind.Warning);
            return false;
        }
        if (cameras > 0 && scene.Cameras.Count() + cameras > CameraElement.MaxPerScene)
        {
            Message.Show(this, $"A key state can contain at most {CameraElement.MaxPerScene} cameras.", MessageKind.Warning);
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
        if (_renaming != null) return;
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
        Properties.RefreshValues();
        _gl.Invalidate();
    }

    private void MoveSelected(int dx, int dy, int dz)
    {
        if (_selected == null) return;
        float step = (float)MoveStep.Value;
        _selected.X += dx * step;
        _selected.Y += dy * step;
        _selected.Z += dz * step;
        AfterToolEdit();
    }

    private void RotateSelected(int axis, int sign)
    {
        if (_selected == null) return;
        float deg = sign * (float)RotateStep.Value;
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
        Properties.RefreshValues();
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
            Message.Show(this, "The animation needs at least one key state.");
            return;
        }
        if (Message.Ask(this, $"Delete key state '{_currentScene.Name}' at {_currentScene.AniTime:0.###} s?",
                MessageKind.Question, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
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
        CameraViewButton.IsChecked = CameraViewItem.IsChecked = _viewMode == ViewMode.Camera;
        FrontViewButton.IsChecked = FrontViewItem.IsChecked = _viewMode == ViewMode.Front;
        SideViewButton.IsChecked = SideViewItem.IsChecked = _viewMode == ViewMode.Side;
        TopViewButton.IsChecked = TopViewItem.IsChecked = _viewMode == ViewMode.Top;
        WireButton.IsChecked = WireItem.IsChecked = _wireframe;
        ModernButton.IsChecked = ModernItem.IsChecked = _modernView;
        ThemeSystemItem.IsChecked = Theme.Choice == ThemeChoice.System;
        ThemeLightItem.IsChecked = Theme.Choice == ThemeChoice.Light;
        ThemeDarkItem.IsChecked = Theme.Choice == ThemeChoice.Dark;
    }

    // ==================================================================================== timeline & playback

    private float TimelineToTime(double value)
    {
        float start = _animation.StartTime, end = _animation.EndTime;
        return start + (end - start) * (float)(value / Timeline.Maximum);
    }

    private double TimeToTimeline(float t)
    {
        float start = _animation.StartTime, end = _animation.EndTime;
        if (end <= start) return 0;
        return Math.Clamp((t - start) / (end - start), 0, 1) * Timeline.Maximum;
    }

    private void SetTimeline(double value)
    {
        _settingTimeline = true;
        try
        {
            Timeline.Value = value;
        }
        finally
        {
            _settingTimeline = false;
        }
    }

    private void UpdateTimeline()
    {
        float t = _previewTime ?? _currentScene.AniTime;
        SetTimeline(TimeToTimeline(t));
        Timeline.IsEnabled = _animation.Scenes.Count > 1;
        UpdateTimeLabel();
    }

    private void UpdateTimeLabel()
    {
        float t = _previewTime ?? _currentScene.AniTime;
        string mode = _previewTime != null ? "preview" : "key state";
        TimeLabel.Text = $"t = {t:0.00} s / {_animation.EndTime:0.00} s ({mode})";
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
        if (_playTimer.IsEnabled)
        {
            StopPlay();
            return;
        }
        if (_animation.Scenes.Count < 2)
        {
            PlayButton.IsChecked = false;
            Message.Show(this, "The animation needs at least two key states to play. Use View > Next key state (PgDn) to add one.");
            return;
        }
        float t = _previewTime ?? _animation.StartTime;
        if (t >= _animation.EndTime) t = _animation.StartTime;
        _playStart = t;
        _playClock.Restart();
        _playTimer.Start();
        PlayButton.IsChecked = true;
        PlayGlyph.Text = Icons.Pause;
    }

    private void StopPlay()
    {
        PlayButton.IsChecked = false;
        PlayGlyph.Text = Icons.Play;
        if (!_playTimer.IsEnabled) return;
        _playTimer.Stop();
        _playClock.Stop();
    }

    private void PlayTick()
    {
        float t = _playStart + (float)_playClock.Elapsed.TotalSeconds;
        if (t > _animation.EndTime)
        {
            if (LoopButton.IsChecked == true)
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
        SetTimeline(TimeToTimeline(t));
        UpdateTimeLabel();
        UpdateStatus();
        _gl.Invalidate();
    }

    // ==================================================================================== files

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        return Message.Ask(this, $"Save changes to '{_animation.Name}'?", MessageKind.Question, MessageBoxButton.YesNoCancel) switch
        {
            MessageBoxResult.Yes => Save(false),
            MessageBoxResult.No => true,
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
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Overbryce animations (*.ani)|*.ani|All files|*.*",
            InitialDirectory = _filePath != null ? Path.GetDirectoryName(_filePath) : Path.Combine(AppContext.BaseDirectory, "Scenes"),
        };
        if (dlg.ShowDialog(this) == true) OpenFile(dlg.FileName);
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
            Message.Show(this, $"Cannot open '{path}':\n{ex.Message}", MessageKind.Error);
        }
    }

    /// <summary>
    /// File > Import > Blender scene: Blender (run in the background) reads the .blend file, the result is saved as a
    /// new .ani file next to its textures, opened, and the report of what was approximated or left out is shown.
    /// </summary>
    private void ImportBlender()
    {
        StopPlay();
        if (!ConfirmDiscard()) return;
        string? blender = Core.Import.BlenderImport.FindBlender();
        if (blender == null)
        {
            Message.Show(this, "Blender was not found. Install Blender (4.2 or newer) or set the BLENDER environment variable to blender.exe.", MessageKind.Error);
            return;
        }
        var open = new Microsoft.Win32.OpenFileDialog { Filter = "Blender scenes (*.blend)|*.blend|All files|*.*", Title = "Import a Blender scene" };
        if (open.ShowDialog(this) != true) return;
        var save = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Overbryce animations (*.ani)|*.ani",
            Title = "Save the imported animation as (its textures go next to it)",
            InitialDirectory = Path.GetDirectoryName(open.FileName),
            FileName = Path.GetFileNameWithoutExtension(open.FileName) + ".ani",
        };
        if (save.ShowDialog(this) != true) return;

        _exporting = true;
        _exportCancel = false;
        Root.IsEnabled = false;
        string last = "Starting Blender ...";
        WarningStatus.Text = last;
        using var cancel = new CancellationTokenSource();
        var options = new Core.Import.BlenderImportOptions();
        var task = Task.Run(() => Core.Import.BlenderImport.Run(open.FileName, save.FileName, options, line => last = line, blender, cancel.Token), cancel.Token);
        try
        {
            while (!task.IsCompleted)
            {
                DoEvents();
                if (_exportCancel) cancel.Cancel();
                WarningStatus.Text = "Importing (Esc cancels): " + last;
                task.Wait(80);
            }
            var result = task.GetAwaiter().GetResult();
            OpenFile(result.AniPath);
            WarningStatus.Text = $"Imported {Path.GetFileName(open.FileName)}: {result.Animation.Scenes.Count} key states, " +
                                 $"{result.Report.Approximated.Count} approximated, {result.Report.Dropped.Count} left out (report: {Path.GetFileName(result.ReportPath)})";
            ShowReport("Blender import report", File.ReadAllText(result.ReportPath));
        }
        catch (Exception) when (cancel.IsCancellationRequested)
        {
            WarningStatus.Text = "Import cancelled.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or System.Text.Json.JsonException or AniFormatException)
        {
            Message.Show(this, "Import failed:\n" + ex.Message, MessageKind.Error);
            WarningStatus.Text = "Import failed.";
        }
        finally
        {
            _exporting = false;
            Root.IsEnabled = true;
        }
    }

    /// <summary>A long text (a report) in a window of its own.</summary>
    private void ShowReport(string title, string text)
    {
        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(8),
        };
        new Window
        {
            Title = title,
            Owner = this,
            Width = 760,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = box,
        }.Show();
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
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Overbryce animations (*.ani)|*.ani",
                FileName = path != null ? Path.GetFileName(path) : _animation.Name + ".ani",
                InitialDirectory = path != null ? Path.GetDirectoryName(path) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (dlg.ShowDialog(this) != true) return false;
            path = dlg.FileName;
        }

        try
        {
            CopyTexturesTo(Path.GetDirectoryName(Path.GetFullPath(path))!);
            AniFile.Save(_animation, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message.Show(this, $"Cannot save '{path}':\n{ex.Message}", MessageKind.Error);
            return false;
        }

        _filePath = Path.GetFullPath(path);
        TextureFileEditor.BaseDirectory = Path.GetDirectoryName(_filePath);
        _dirty = false;
        UpdateTitle();
        Properties.RefreshValues();
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
            Message.Show(this, "The animation needs at least two key states.");
            return;
        }
        string ext = _settings.ExportPng ? ".png" : ".bmp";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export frames - choose the base file name",
            Filter = _settings.ExportPng ? "PNG images (*.png)|*.png" : "Bitmaps (*.bmp)|*.bmp",
            FileName = _animation.Name + ext,
            InitialDirectory = _filePath != null ? Path.GetDirectoryName(_filePath) : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dlg.ShowDialog(this) != true) return;

        string dir = Path.GetDirectoryName(dlg.FileName)!;
        string stem = Path.GetFileNameWithoutExtension(dlg.FileName);
        var times = _animation.FrameTimes().ToList();
        var names = new List<string>(times.Count);
        var format = _settings.ExportPng ? ImageFormat.Png : ImageFormat.Bmp;
        var size = _settings.ExportSize;

        StopPlay();
        _exportCancel = false;
        _exporting = true;
        Root.IsEnabled = false;
        Focus(); // Esc reaches the window while its content is disabled
        bool trace = _settings.Renderer == ExportRenderer.RayTraced && !_wireframe;
        bool modern = _settings.Renderer switch
        {
            ExportRenderer.Classic => false,
            ExportRenderer.Modern => true,
            _ => _modernView,
        };
        var bridge = trace ? new RaytraceBridge(ResolveTexture) : null;
        try
        {
            _gl.MakeCurrent();
            _renderer ??= new SceneRenderer();
            using var target = new OffscreenTarget(size.Width, size.Height, _settings.Antialias ? 8 : 0);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < times.Count && !_exportCancel; i++)
            {
                var scene = _animation.Evaluate(times[i]);
                string name = $"{stem}{i:000000}{ext}";
                if (bridge != null)
                {
                    var job = bridge.Prepare(_animation, scene, times[i], ViewMode.Camera, _editor, size.Width, size.Height,
                        _settings.AlphaBlend, classic: false, _animation.Raytrace, (uint)i);
                    if (!TraceWithEvents(job, _animation.Raytrace.Samples, p =>
                            WarningStatus.Text = $"Ray tracing frame {i + 1} / {times.Count}: {p:0%} (Esc cancels)..."))
                        break;
                    using var traced = RaytraceBridge.ToBitmap(job.Resolve());
                    traced.Save(Path.Combine(dir, name), format);
                }
                else
                {
                    var (view, proj) = ViewSetup.Compute(scene, ViewMode.Camera, _editor, size.Width, size.Height, out _);
                    target.Bind();
                    _renderer.Render(scene, new RenderOptions
                    {
                        Wireframe = _wireframe,
                        AlphaBlend = _settings.AlphaBlend,
                        View = view,
                        Projection = proj,
                        ResolveTexture = ResolveTexture,
                TextureSize = _animation.TextureSize,
                        Modern = modern ? _animation.Modern : null,
                        Samples = _settings.Antialias ? 8 : 0,
                    }, size.Width, size.Height);
                    using var bmp = target.ReadBitmap();
                    bmp.Save(Path.Combine(dir, name), format);
                }
                names.Add(name);

                WarningStatus.Text = $"Exporting frame {i + 1} / {times.Count} (Esc cancels)...";
                DoEvents();
            }
            File.WriteAllLines(Path.Combine(dir, "list.lst"), names);
            WarningStatus.Text = (_exportCancel ? "Export cancelled after " : "Exported ") + $"{names.Count} frames to {dir}" +
                                 (bridge != null ? $" (ray traced in {watch.Elapsed.TotalSeconds:0} s)" : "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            Message.Show(this, "Export failed:\n" + ex.Message, MessageKind.Error);
        }
        finally
        {
            _exporting = false;
            Root.IsEnabled = true;
            OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, 0);
            _gl.Invalidate();
        }
    }

    /// <summary>
    /// Traces on worker threads while the window keeps handling messages (Esc sets the export's cancel
    /// flag); returns false when it was cancelled.
    /// </summary>
    private bool TraceWithEvents(Raytrace.RaytraceJob job, int samples, Action<float> report)
    {
        using var cancel = new CancellationTokenSource();
        var progress = new LatestProgress();
        var task = Task.Run(() => job.Trace(samples, progress, cancel.Token), cancel.Token);
        while (!task.IsCompleted)
        {
            DoEvents();
            if (_exportCancel) cancel.Cancel();
            report(progress.Value);
            task.Wait(40);
        }
        try
        {
            task.GetAwaiter().GetResult();
            return true;
        }
        catch (Exception) when (cancel.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Lets the window handle its pending messages (input, painting) during a long job on the UI thread.</summary>
    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.InvokeAsync(() => frame.Continue = false, DispatcherPriority.Background);
        Dispatcher.PushFrame(frame);
    }

    // ==================================================================================== ray traced view

    /// <summary>View > Ray trace this view (F9): the shown state through the shown camera, in its own window.</summary>
    private void RaytraceView()
    {
        StopPlay();
        Scene? scene = _previewTime is float t ? _animation.Evaluate(t) : _currentScene;
        int w = Math.Max(16, _gl.ClientSize.Width), h = Math.Max(16, _gl.ClientSize.Height);
        Raytrace.RaytraceJob job;
        try
        {
            job = new RaytraceBridge(ResolveTexture).Prepare(_animation, scene, _previewTime, _viewMode, _editor, w, h,
                _settings.AlphaBlend, classic: false, _animation.Raytrace, 0);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            WarningStatus.Text = "Ray tracing failed: " + ex.Message;
            return;
        }

        if (_raytraced == null)
        {
            _raytraced = new RaytracePreviewWindow { Owner = this };
            _raytraced.Closed += (_, _) => _raytraced = null;
            _raytraced.Show();
        }
        else
        {
            _raytraced.Activate();
        }
        string what = _previewTime is float pt ? $"{pt:0.00} s" : $"key state {_animation.Scenes.IndexOf(_currentScene) + 1}";
        _raytraced.Start(job, _animation.Raytrace.Samples, $"{what}, {ViewStatus.Text}");
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
        Title = $"{name}{(_dirty ? " *" : "")} - {AppTitle}";
    }

    private void UpdateStatus()
    {
        int index = _animation.Scenes.IndexOf(_currentScene);
        StateStatus.Text = _previewTime is float t
            ? $"Preview at {t:0.00} s"
            : $"Key state {index + 1} / {_animation.Scenes.Count}  ({_currentScene.Name}, {_currentScene.AniTime:0.###} s)";
        ViewStatus.Text = (_wireframe ? "Wire frame, " : _modernView ? "Modern, " : "") + _viewMode switch
        {
            ViewMode.Camera => "camera view",
            ViewMode.Front => "front view",
            ViewMode.Side => "side view",
            _ => "top view",
        };
        SelectionStatus.Text = _selected is null or Scene
            ? "Click an object to select it"
            : _selected.Parent is null or Scene
                ? $"Selected: {_selected.Name}"
                : $"Selected: {_selected.Name} in {_selected.Parent.Name} (Alt+click: group)";
    }

    private void ShowHelp()
    {
        Message.Show(this,
            "Tree: select key states and elements, F2 renames, Del deletes, drag to move into another group/state (Ctrl+drag copies).\n" +
            "Elements with the same name in neighbouring key states are interpolated (Catmull-Rom spline over the key-state times).\n\n" +
            "View: click an object to select it, Alt+click selects its group (repeat to go further up), click the background to select the key state.\n" +
            "Left-drag orbits the editor camera, mouse wheel zooms (front/side/top views). Ctrl+1..4 switch views, Ctrl+W toggles wire frame, Ctrl+M modern rendering.\n" +
            "F9 ray traces the view in its own window (reflections, glass, soft shadows; Animation > Ray tracing settings sets the quality).\n" +
            "In the wire frame view lights (yellow) and cameras (green, active cyan) are shown and can be clicked too.\n" +
            "'Stop at this state' (Final) makes an element come to rest at that key state; otherwise it moves smoothly through it.\n" +
            "PgDn on the last key state creates a copy 5 s later; edit it to define the motion.\n\n" +
            "Properties: Enter takes a typed value, Esc restores it, Up/Down step a number (Shift: x10, Ctrl: x0.1).\n" +
            "Timeline: drag to preview the interpolated animation, F5 plays it. File > Export frames renders BMP/PNG frames through the active camera " +
            "(File > Rendering settings chooses the renderer: as the view, classic, modern or ray traced).\n" +
            "View > Theme switches between light and dark (or follows Windows).",
            MessageKind.Information, "Controls");
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopPlay();
        Theme.Changed -= UpdateViewChecks;
        if (_renderer != null)
        {
            _gl.MakeCurrent();
            _renderer.Dispose();
            _renderer = null;
        }
        base.OnClosed(e);
    }
}
