using System.Numerics;
using Engine.Input;
using Engine.Render;
using Engine.Spatial;
using Engine.Ui;
using EntityARestaurantGame.Scenes;
using Game.Contracts;
using Silk.NET.Input; // CreateInput() extension on IWindow
using Silk.NET.OpenGL;
using Shader = Engine.Render.Shader; // Silk.NET.OpenGL also exports a Shader type

namespace EntityARestaurantGame.Render3D;

/// <summary>Actions the 3D view understands; bindings.json maps devices onto these.</summary>
public enum GameAction
{
    OrbitDrag,
    PanLeft,
    PanRight,
    PanForward,
    PanBack,
    MenuUp,
    MenuDown,
    Confirm,
    Cancel,
    Select,
    ToggleDebug,
    PlayerMenu,
    FocusPawn,
    NextMember,
}

/// <summary>
/// Composition point for the 3D view. Owns the GameWindow, drives the sim
/// clock via <see cref="EntityController.Pump"/>, and — per the rendering
/// boundary in ARCHITECTURE.md — only ever READS sim truth.
///
/// Axis mapping: sim is X=left/right, Y=front/back, Z=floor-layer; render is
/// X=right, Y=up, Z=toward-camera. So renderPos = (sim.X, sim.Z, sim.Y).
/// </summary>
public sealed class Render3DHost
{
    private readonly EntityController _controller;
    private readonly LocationRegistry _locations;
    private readonly GameWindow _window;
    private readonly OrbitCamera _camera = new();

    private Shader? _shader;
    private RenderTarget? _target;
    private GpuMesh? _grid;
    private GpuMesh? _axes;
    private GpuMesh? _pawnMesh;
    private readonly List<(GpuMesh Mesh, Matrix4x4 Model)> _floors = [];
    private readonly List<(GpuMesh Mesh, Matrix4x4 Model)> _props = [];
    private SilkInputAdapter<GameAction>? _input;
    private IInputContext? _inputContext;
    private Renderer2D? _renderer2d;
    private SpriteFont? _font;
    private SpriteFont? _headingFont;
    private SpriteFont? _debugFont;
    private SceneManager? _scenes;
    private readonly DebugHud _hud = new();
    private readonly PartyHud _partyHud = new();

    /// <summary>
    /// Bottom-edge space the party strip owns, for scenes docked to the left.
    /// Zero before the fonts load, which is the honest answer — nothing is
    /// drawn down there yet either.
    /// </summary>
    private float PartyReserve =>
        _font is null || _debugFont is null ? 0f : PartyHud.Footprint(_font, _debugFont);
    private readonly SpeechBubbles _bubbles = new();
    private double _fps;

    /// <summary>Smoothed visual positions so per-decision jumps read as walking.</summary>
    private readonly Dictionary<string, Vector3> _visualPos = [];

    private static readonly Dictionary<string, Vector4> TypeColors = new()
    {
        ["Warrior"]  = new(0.75f, 0.22f, 0.18f, 1f),
        ["Guard"]    = new(0.25f, 0.45f, 0.80f, 1f),
        ["Merchant"] = new(0.85f, 0.70f, 0.25f, 1f),
        ["Nobody"]   = new(0.55f, 0.55f, 0.55f, 1f),
    };

    /// <summary>Known types keep their hand-picked colors; generated types
    /// (Cook, Patron, …) get a stable hue from an FNV hash of the name —
    /// string.GetHashCode is randomized per process, so not that.</summary>
    private static Vector4 ColorFor(string entityType)
    {
        if (TypeColors.TryGetValue(entityType, out var known)) return known;

        uint hash = 2166136261;
        foreach (char c in entityType) hash = (hash ^ c) * 16777619;
        float hue = (hash % 360) / 60f;
        float x = 1f - MathF.Abs(hue % 2f - 1f);
        var (r, g, b) = (int)hue switch
        {
            0 => (1f, x, 0f),
            1 => (x, 1f, 0f),
            2 => (0f, 1f, x),
            3 => (0f, x, 1f),
            4 => (x, 0f, 1f),
            _ => (1f, 0f, x),
        };
        // Desaturate toward grey so hashed colors sit in the same muted family.
        const float sat = 0.6f, val = 0.78f;
        return new Vector4(
            val * (1f - sat + sat * r),
            val * (1f - sat + sat * g),
            val * (1f - sat + sat * b), 1f);
    }

    private static readonly Vector4[] FloorColors =
    [
        new(0.16f, 0.19f, 0.22f, 1f),
        new(0.30f, 0.24f, 0.18f, 1f),
        new(0.20f, 0.27f, 0.24f, 1f),
        new(0.26f, 0.22f, 0.28f, 1f),
    ];

    private const int CanvasWidth = 480;
    private const int CanvasHeight = 270;

    /// <summary>Relation-outline hull thickness, world units (~1 canvas pixel at rest zoom).</summary>
    private const float OutlineWidth = 0.08f;

    private double _time;
    private float _aspect = 1280f / 720f;
    private readonly bool _smoke;
    private readonly double _smokeCloseAfter;
    private bool _smokeInspectShown;
    private bool _screenshotTaken;

    public Render3DHost(EntityController controller, LocationRegistry locations, double autoCloseSeconds = 0)
    {
        _controller = controller;
        _locations = locations;
        _smoke = autoCloseSeconds > 0;
        _smokeCloseAfter = autoCloseSeconds;
        // Smoke closing is handled here, not by GameWindow's timer: the close
        // must never race ahead of the screenshot (a single hitchy frame can
        // jump update-time past both thresholds at once).
        _window = new GameWindow("Entity A Restaurant — 3D", 1280, 720);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Resize += size => _aspect = size.X / (float)Math.Max(1, size.Y);
    }

    private static Vector3 SimToRender(Vector3 sim) => new(sim.X, sim.Z, sim.Y);

    private void OnLoad()
    {
        var gl = _window.Gl;
        gl.ClearColor(0.09f, 0.13f, 0.16f, 1f);
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _shader = Shader.FromFiles(gl,
            Path.Combine(baseDir, "data", "shaders", "basic.vert"),
            Path.Combine(baseDir, "data", "shaders", "basic.frag"));

        // PS2-flavored: the whole scene renders at 480x270 and upscales with
        // nearest filtering. Chunky pixels are the point. UI draws into the
        // same target, so menus and text inherit the look.
        _target = new RenderTarget(gl, CanvasWidth, CanvasHeight);

        _grid = new GpuMesh(gl, Primitives.Grid(14, 2f, new Vector4(0.20f, 0.26f, 0.30f, 1f)));
        _axes = new GpuMesh(gl, Primitives.Axes(2f));
        _pawnMesh = new GpuMesh(gl, Primitives.Cube(new Vector3(0.7f, 1.7f, 0.7f), new Vector4(1f, 1f, 1f, 1f)));

        // One floor pane per location. Pane() builds in XY facing +Z; rotating
        // -90° about X lays it flat facing +Y. Overlapping zones (kitchen sits
        // inside restaurant) get a tiny per-index lift to dodge z-fighting.
        int i = 0;
        foreach (var loc in _locations.All)
        {
            float w = loc.Max.X - loc.Min.X;
            float d = loc.Max.Y - loc.Min.Y; // sim Y = depth
            var centerSim = (loc.Min + loc.Max) / 2f;
            var color = FloorColors[i % FloorColors.Length];
            var mesh = new GpuMesh(gl, Primitives.Pane(w, d, color));
            var model = Matrix4x4.CreateRotationX(-MathF.PI / 2f)
                      * Matrix4x4.CreateTranslation(centerSim.X, 0.005f + i * 0.01f, centerSim.Y);
            _floors.Add((mesh, model));
            i++;
        }

        // Imported-model proof: OBJ pyramids as stand-in decor on the dining floor.
        var pyramid = new GpuMesh(gl, ObjLoader.Load(
            Path.Combine(baseDir, "data", "models", "pyramid.obj"),
            new Vector4(0.62f, 0.44f, 0.28f, 1f)));
        foreach (var (px, py) in new[] { (4f, 12f), (16f, 12f), (10f, 17f) })
        {
            _props.Add((pyramid, Matrix4x4.CreateScale(1.6f, 1.1f, 1.6f)
                               * Matrix4x4.CreateTranslation(px, 0f, py)));
        }

        // Start the camera over the middle of the largest location.
        var world = _locations.All.OrderByDescending(l => l.Volume).FirstOrDefault();
        if (world is not null)
        {
            var c = (world.Min + world.Max) / 2f;
            _camera.Target = new Vector3(c.X, 0, c.Y);
            _camera.Distance = 26f;
        }

        _inputContext = _window.Window.CreateInput();
        _input = new SilkInputAdapter<GameAction>(
            _inputContext,
            BindingSet<GameAction>.FromFile(Path.Combine(baseDir, "data", "input", "bindings.json")));

        // ── 2D interface: renderer, baked font, scene controller ──────────
        _renderer2d = new Renderer2D(gl);
        byte[] ttf = GameFonts.LoadTtf();
        _font = new SpriteFont(gl, ttf, 13);
        _headingFont = new SpriteFont(gl, ttf, 26);
        _debugFont = new SpriteFont(gl, ttf, 10);
        var uiContext = new UiContext(gl, _renderer2d, _font, _headingFont,
            CanvasWidth, CanvasHeight, Path.Combine(baseDir, "data", "ui"));
        _scenes = new SceneManager(uiContext);
        _scenes.QuitRequested += _window.Close;

        // The intro: settle behind a pawn's eyes before the world moves.
        // Smoke runs auto-pick instead so the screenshot proves the player
        // layer (HUD + relation outlines) over a live sim.
        if (_smoke)
            _controller.FavoredEntityId ??= _controller.ActiveEntities.FirstOrDefault()?.ID;
        else if (_controller.FavoredEntityId is null)
            _scenes.Push(new PawnSelectScene(_controller));

        // Smoke sequence: sim runs one real second (entity logs start filling),
        // the inspect panel auto-opens on a pawn (see OnUpdate), screenshot at
        // 2s — one frame proving speech bubbles, the dossier panel, and a live
        // sim. Toggle the full debug HUD manually with F3.

        Console.WriteLine($"[3D] GL ready: {gl.GetStringS(StringName.Version)} | {_locations.All.Count} locations, {_controller.ActiveEntities.Count} entities");
    }

    private void OnUpdate(double dt)
    {
        _time += dt;

        // The render loop is the master clock; the sim pumps off real time —
        // unless a sim-blocking scene (pause menu) sits on the stack.
        if (_scenes is null || !_scenes.SimPaused)
            _controller.Pump(dt);

        if (_input is null || _scenes is null) return;
        var input = _input.State;

        // The debug HUD is a passive readout — toggles in any mode.
        if (input.WasPressed(GameAction.ToggleDebug)) _hud.Visible = !_hud.Visible;

        // Smoke sequence: open the inspect panel on a live pawn so the
        // screenshot proves bubbles + dossier over a running sim, and the
        // whisper wheel on top of it so the screenshot proves that too. The
        // wheel is an overlay and does not block, so both are visible at once
        // and the frame shows the whole player surface in one picture.
        if (_smoke && !_smokeInspectShown && _time > 1.0)
        {
            _smokeInspectShown = true;
            var pick = _controller.ActiveEntities.FirstOrDefault();
            if (pick is not null) _scenes.Push(new EntityInspectScene(_controller, pick));
            if (_controller.FavoredEntity is not null)
                _scenes.Push(new InfluenceScene(_controller, PartyReserve));
        }

        // Smoke close: only after the screenshot exists (10s hard cap in case
        // renders never produce one).
        if (_smoke && _time > _smokeCloseAfter && (_screenshotTaken || _time > 10))
            _window.Close();

        // Losing the pawn reopens the intro — the narrator needs new eyes.
        // (Skipped in smoke; the guard on Top keeps this from re-pushing.)
        if (!_smoke && _controller.FavoredEntityId != null && _controller.FavoredEntity is null
            && _scenes.Top is not PawnSelectScene)
        {
            _controller.FavoredEntityId = null;
            _scenes.Push(new PawnSelectScene(_controller));
        }

        // The inspect panel and whisper menu are non-blocking sidecars: the
        // world (camera, clicking other pawns) stays live underneath them.
        bool worldLive = _scenes.IsEmpty || _scenes.Top is EntityInspectScene or InfluenceScene;
        if (worldLive)
        {
            if (input.WasPressed(GameAction.Cancel) && _scenes.IsEmpty) _scenes.Push(new PauseScene());
            if (input.IsHeld(GameAction.OrbitDrag)) _camera.Orbit(input.MouseDelta);
            if (input.Scroll != 0) _camera.Zoom(input.Scroll);

            // A click on the party strip swaps who we speak through; only if
            // it missed the strip does it fall through to picking a pawn in
            // the world, so the HUD never acts as a hole in the interface.
            if (input.WasPressed(GameAction.Select))
            {
                if (_partyHud.MemberAt(ToCanvas(input.MousePosition)) is { } memberId)
                    _controller.FavoredEntityId = memberId;
                else
                    TryPickEntity(input.MousePosition);
            }

            // Keyboard mirror of clicking a tab: cycle to the next member.
            if (input.WasPressed(GameAction.NextMember))
            {
                var members = _controller.PlayerPartyMembers;
                if (members.Count > 1)
                {
                    int current = members.ToList().FindIndex(m => m.ID == _controller.FavoredEntityId);
                    _controller.FavoredEntityId = members[(current + 1) % members.Count].ID;
                }
            }

            if (input.WasPressed(GameAction.PlayerMenu) && _scenes.Top is not InfluenceScene
                && _controller.FavoredEntity is { IsAlive: true })
                _scenes.Push(new InfluenceScene(_controller, PartyReserve));

            if (input.WasPressed(GameAction.FocusPawn) && _controller.FavoredEntity is { } fav
                && _visualPos.TryGetValue(fav.ID, out var favPos))
                _camera.Target = favPos;

            var pan = input.DigitalAxis2(GameAction.PanLeft, GameAction.PanRight, GameAction.PanBack, GameAction.PanForward);
            if (pan != Vector2.Zero) _camera.Pan(pan, (float)dt);
        }

        if (!_scenes.IsEmpty)
        {
            // The top scene owns menu input (for the inspect panel this rides
            // alongside the live camera controls above).
            _scenes.Update(dt, new UiInput(
                input.WasPressed(GameAction.MenuUp),
                input.WasPressed(GameAction.MenuDown),
                input.WasPressed(GameAction.Confirm),
                input.WasPressed(GameAction.Cancel),
                ToCanvas(input.MousePosition),
                input.WasPressed(GameAction.Select),
                input.IsHeld(GameAction.Select),
                input.WasReleased(GameAction.Select)));
        }

        // Ease visual pawn positions toward sim truth so decision-step moves read as walking.
        float ease = 1f - MathF.Exp(-(float)dt * 5f);
        foreach (var e in _controller.ActiveEntities)
        {
            var target = SimToRender(e.Position);
            _visualPos[e.ID] = _visualPos.TryGetValue(e.ID, out var current)
                ? Vector3.Lerp(current, target, ease)
                : target;
        }

        input.NextFrame();
    }

    private Matrix4x4 ComputeViewProj() =>
        _camera.View() * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, _aspect, 0.1f, 200f);

    /// <summary>
    /// Screen-space picking: project every pawn's center onto the low-res
    /// canvas and take the nearest one within a small radius of the click.
    /// At 480x270 a ray-cast is overkill — pawns are ~6px wide.
    /// </summary>
    /// <summary>Window pixels to canvas pixels. The interface lays itself out
    /// in canvas space, so every hit-test has to arrive in those terms.</summary>
    private Vector2 ToCanvas(Vector2 windowPoint)
    {
        var winSize = _window.Window.Size;
        if (winSize.X <= 0 || winSize.Y <= 0) return new Vector2(-1, -1);
        return new Vector2(
            windowPoint.X / winSize.X * CanvasWidth,
            windowPoint.Y / winSize.Y * CanvasHeight);
    }

    private void TryPickEntity(Vector2 mouseWin)
    {
        var click = ToCanvas(mouseWin);
        if (click.X < 0) return;

        var viewProj = ComputeViewProj();
        Entity? best = null;
        float bestDist = 12f; // canvas pixels — forgiving at this resolution

        foreach (var e in _controller.ActiveEntities)
        {
            if (!_visualPos.TryGetValue(e.ID, out var pos)) continue;
            var clip = Vector4.Transform(new Vector4(pos + new Vector3(0f, 0.85f, 0f), 1f), viewProj);
            if (clip.W <= 0.05f) continue;
            float sx = (clip.X / clip.W * 0.5f + 0.5f) * CanvasWidth;
            float sy = (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * CanvasHeight;
            float dist = Vector2.Distance(click, new Vector2(sx, sy));
            if (dist < bestDist)
            {
                bestDist = dist;
                best = e;
            }
        }

        if (best is null) return;
        if (_scenes!.Top is EntityInspectScene inspect) inspect.Retarget(best);
        else _scenes.Push(new EntityInspectScene(_controller, best));
    }

    private void OnRender(double dt)
    {
        var gl = _window.Gl;

        _target!.Bind();
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        var viewProj = ComputeViewProj();

        _shader!.Use();
        _shader.Set("uTint", Vector4.One); // uniforms default to zero — set before ANY draw
        _shader.Set("uSolid", 0f);
        _shader.Set("uOutline", 0f);

        // Grid and axes: unlit debug geometry at the world origin.
        _shader.Set("uMvp", viewProj);
        _shader.Set("uModel", Matrix4x4.Identity);
        _shader.Set("uLit", 0f);
        _grid!.Draw();
        _axes!.Draw();

        _shader.Set("uLit", 1f);
        foreach (var (mesh, model) in _floors)
        {
            _shader.Set("uMvp", model * viewProj);
            _shader.Set("uModel", model);
            mesh.Draw();
        }

        foreach (var (mesh, model) in _props)
        {
            _shader.Set("uMvp", model * viewProj);
            _shader.Set("uModel", model);
            mesh.Draw();
        }

        foreach (var e in _controller.ActiveEntities)
        {
            if (!_visualPos.TryGetValue(e.ID, out var pos)) continue;
            var model = Matrix4x4.CreateTranslation(pos + new Vector3(0f, 0.85f, 0f));
            _shader.Set("uMvp", model * viewProj);
            _shader.Set("uModel", model);
            _shader.Set("uTint", ColorFor(e.EntityType));
            _pawnMesh!.Draw();
        }

        // Relation outlines: the same mesh re-drawn as an inverted hull —
        // vertices pushed out along their normals, front faces culled, flat
        // relation color. Generic to any GpuMesh with normals, so props and
        // beasts inherit the treatment the day they get real models.
        if (_controller.FavoredEntityId is { } favoredId)
        {
            gl.CullFace(GLEnum.Front);
            _shader.Set("uSolid", 1f);
            _shader.Set("uOutline", OutlineWidth);
            foreach (var e in _controller.ActiveEntities)
            {
                if (!_visualPos.TryGetValue(e.ID, out var pos)) continue;
                var model = Matrix4x4.CreateTranslation(pos + new Vector3(0f, 0.85f, 0f));
                _shader.Set("uMvp", model * viewProj);
                _shader.Set("uModel", model);
                _shader.Set("uTint", RelationTint.For(e, favoredId));
                _pawnMesh!.Draw();
            }
            gl.CullFace(GLEnum.Back);
            _shader.Set("uSolid", 0f);
            _shader.Set("uOutline", 0f);
        }

        _fps = _fps <= 0 ? 1.0 / Math.Max(dt, 1e-6) : _fps * 0.95 + (1.0 / Math.Max(dt, 1e-6)) * 0.05;

        // Speech bubbles are world-anchored, so they draw beneath the menus.
        // The player strip rides in the same pass: above bubbles, below menus.
        if (_renderer2d is not null && _debugFont is not null && _font is not null)
        {
            _renderer2d.Begin(CanvasWidth, CanvasHeight);
            _bubbles.Draw(_renderer2d, _debugFont, _controller, _visualPos, viewProj, CanvasWidth, CanvasHeight);
            _partyHud.Draw(_renderer2d, _font, _debugFont, _controller, CanvasHeight);
            _renderer2d.End();
        }

        // 2D interface draws into the same low-res target, above the world.
        _scenes?.Draw();

        // The debug HUD sits above even the menus, in its own pass.
        if (_hud.Visible && _renderer2d is not null && _debugFont is not null && _scenes is not null)
        {
            _renderer2d.Begin(CanvasWidth, CanvasHeight);
            _hud.Draw(_renderer2d, _debugFont, _controller, _fps, _scenes.SimPaused, CanvasWidth, CanvasHeight);
            _renderer2d.End();
        }

        var winSize = _window.Window.FramebufferSize;
        _target.BlitToScreen(winSize.X, winSize.Y);

        if (_smoke && !_screenshotTaken && _time > 2.0)
        {
            _screenshotTaken = true;
            var size = _window.Window.FramebufferSize;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke.bmp");
            Screenshot.Capture(gl, size.X, size.Y, path);
            Console.WriteLine($"[3D] smoke screenshot: {path}");
        }
    }

    public void Run()
    {
        _window.Run();
        // The input context owns device threads — leaving it undisposed can
        // keep the process alive long after the window closes.
        _inputContext?.Dispose();
        _window.Dispose();
    }
}
