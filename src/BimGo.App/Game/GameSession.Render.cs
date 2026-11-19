using System.Numerics;
using BimGo.Audio;
using BimGo.Format;
using BimGo.Game.Guns;
using BimGo.Platform;
using BimGo.Rendering;
using BimGo.Scene;
using Gl = BimGo.Native.Gl;

// The class belongs to the Game namespace
namespace BimGo.Game
{
    /// <summary>
    /// Rendering: 3D passes, minimap and HUD.
    /// </summary>
    internal sealed partial class GameSession
    {
        #region Fields

        private readonly Vector4[] _mapPlanes = new Vector4[6];
        private readonly List<Highlight> _highlights = new();

        // Help panel column widths (measured from the text; recomputed when the UI scale or the rows change)
        private float _helpScale = -1f, _helpKeyWidth, _helpActionWidth;

        // F1 help rows (polish round P-B: generated from the catalogue for the current situation), rebuilt when a binding,
        // the situation changes. Row 0 is the situation's title; the last row always shows.
        private (string Key, string Action)[] _helpRows;
        private int _helpRowsVersion = int.MinValue;
        private string _helpRowsSituation;

        /// <summary>
        /// The F1 sheet for what you are doing (polish round P-B): the situation's title, its rows from
        /// <see cref="InputSituation.Help"/> with the keys bound now (rows with nothing bound are left out), then
        /// "MORE → CONTROLS" and the F1 row.
        /// </summary>
        private (string Key, string Action)[] HelpRows()
        {
            string name = CurrentSituation();
            if (_helpRows != null && _helpRowsVersion == Hotkeys.Version && ReferenceEquals(_helpRowsSituation, name)) { return _helpRows; }
            KeyMap m = Hotkeys.Map;
            InputSituation situation = InputCatalog.Situation(name) ?? InputCatalog.Situation(InputCatalog.SIT_WALKING);

            var rows = new List<(string Key, string Action)>(situation.Help.Count + 3)
            {
                (string.Empty, situation.Name.ToUpperInvariant())
            };
            foreach (HelpEntry entry in situation.Help)
            {
                if (entry.Actions.Any(action => !InputCatalog.ReviewAction(action))) { continue; }
                string keys = m.HelpKeys(entry);
                if (keys.Length == 0) { continue; } // nothing bound
                rows.Add((keys, entry.Label));
            }
            rows.Add(("MORE → CONTROLS",
                "Every key and changing them"));
            rows.Add((m.Label(InputAction.Help).ToUpperInvariant(),
                "Hide help · BimGo " + Program.Version + " · Revit® is a trademark of Autodesk, Inc."));

            _helpRows = rows.ToArray();
            _helpRowsSituation = name;
            _helpRowsVersion = Hotkeys.Version;
            _helpScale = -1f; // re-measure the columns
            return _helpRows;
        }

        #endregion

        /// <summary>
        /// Renders one frame (scene into the off-screen target, then minimap and UI on the window).
        /// </summary>
        private void Render()
        {
            int width = _window.Width, height = _window.Height;

            // ---- Clash pictures (C-B2): row pictures, TO COMMENT / TO COMMENTS, off-screen (state restored afterwards)
            RenderClashPictures();

            RenderScene(width, height, _target, _msaa, photo: false);

            _target.BlitToWindow();
            if (ThumbnailDue) { CaptureThumbnail(width, height); }
            if (_screenshotRequested)
            {
                // Colour round B: the profile's legend in the screenshot when ticked
                if (ProfileLegendInShots)
                {
                    Gl.Viewport(0, 0, width, height);
                    QueueProfileLegendForShot(width, height);
                    _ui.Flush(width, height);
                }
                CaptureScreenshot(width, height);
            }

            // ---- Window pass: minimap 3D, then all 2D UI in one batch
            Gl.Viewport(0, 0, width, height);
            float mapX = width - S(20) - S(220), mapY = S(20);
            _dockOverMap = _dockOverMapNext; // P-A2: last frame's dock sat over the minimap (a short window): it hides
            _dockOverMapNext = false;
            if (_showMap && !_paused && !_uiHidden && !_clashModeOn && !_dockOverMap) { DrawMinimapPlan(mapX + S(8), mapY + S(30), S(204), S(170)); }

            if (_paused)
            {
                // A text box opened from a panel (reply, assignee) sits over the menu and takes the clicks
                if (IsEditingComment) { _window.Input.ConsumeClicks(); }
                BuildPauseMenu();
                if (IsEditingComment) { BuildCommentEditor(); }
            }
            else
            {
                // A text box (comment or bookmark name) takes the clicks too: panels behind it stay still
                if (IsEditingComment) { _window.Input.ConsumeClicks(); }
                if (_uiHidden) { BuildHiddenHud(width); }
                else { BuildHud(mapX, mapY); }
                if (IsEditingComment) { BuildCommentEditor(); }
            }
            _ui.ResetClip(); // polish round P-A1: a scroll region left open by an early return never outlives the frame
            _ui.Flush(width, height);
        }

        /// <summary>
        /// Round X-C1: the sharp reflections' quality this frame (0 = off): stills only (photos, videos, batch stills and
        /// photo mode's preview; Gavin, after testing them live: panes popped between views), Realistic mode with reflections
        /// on, not in the clash view; colour by SSR turns them on live too (medium at least). Off frees the history.
        /// </summary>
        private int ScreenReflectionQuality(bool clashView, bool photo)
        {
            bool realistic = _realistic && !_whitecard && _renderer.HasMaterials;
            bool still = photo && !_sharpOff;
            int quality = !realistic || clashView ? 0 : _reflectDebug == 3 ? Math.Max(_ssrQuality, 2) : _reflections && _reflectDebug == 0 && still ? _ssrQuality : 0;
            if (quality == 0 && !clashView && !_clashShot) { _renderer.ReleaseReflectionHistory(); }
            return quality;
        }

        /// <summary>
        /// The 3D passes into a target: shadows, lights, AO and glow, the scene, caps, ground, highlights, glass, bloom,
        /// photo exposure, then (not for photos) markers and the section gizmo. Uses <see cref="Camera"/> as it stands.
        /// </summary>
        /// <param name="width">Target width (pixels).</param>
        /// <param name="height">Target height.</param>
        /// <param name="target">The target (sized here).</param>
        /// <param name="samples">MSAA samples.</param>
        /// <param name="photo">A photo: no probe baking, no highlights, markers or gizmos.</param>
        private void RenderScene(int width, int height, RenderTarget target, int samples, bool photo)
        {
            // ---- Round Y-D1: GPU time per stage (Performance details; the main view only)
            GpuTimers timers = _renderer.Timers;
            if (!photo)
            {
                timers.Enabled = _showPerf;
                timers.BeginFrame();
            }

            // ---- Colour round A: the active colour profile (evaluated again only when something changed)
            UpdateProfile();

            // ---- Sun lighting and shadow maps (only changed cascades re-render). Clash pictures (C-B2) other than the real
            // model view draw without the sun, so they leave the cascades as the main view needs them
            _renderer.Lighting = CurrentLighting();
            if (!photo) { timers.Stage(GpuTimers.SHADOWS); }
            bool clashShotUnlit = _clashShot && _clashView != CLASH_VIEW_REAL;
            string shadowError = clashShotUnlit ? null : _renderer.UpdateShadows(Camera, Scene.Bounds, ShadowSceneKey(), _groupVisible, Dynamics, _whitecard,
                ShadowMaps.PresetFor(_shadowQuality));
            if (shadowError != null)
            {
                OnShadowFailure(shadowError);
                _renderer.Lighting = CurrentLighting();
            }

            // ---- Artificial lights for this frame (after the sun: daylight dims them), and their cached shadow maps
            // (unlit clash pictures keep the main view's picks, so no light shadow is re-rendered for them)
            if (!clashShotUnlit)
            {
                UpdateArtificialLights(photo || _allLightsLive); // X-C1: off-screen views light every fixture
                string lightShadowError = _renderer.UpdateLightShadows(_groupVisible, Dynamics, ShadowSceneKey());
                if (lightShadowError != null) { Toast(lightShadowError, 6f, important: true); }
            }

            // ---- Ambient occlusion and glow: half-resolution geometry pre-pass, AO + blur, bloom source + blur
            bool glow = _renderer.Artificial.Bloom > 0f;
            // Clash pictures (C-B2) draw the clash view in the photo pass, without AO or glow (their half-resolution
            // targets stay sized for the window instead of being remade for every picture)
            bool clashView = ClashViewActive && (!photo || _clashShot);
            bool clashNoAo = clashView && _clashView != CLASH_VIEW_REAL; // the real model view keeps AO
            int ssr = ScreenReflectionQuality(clashView, photo); // Round X-C1

            // ---- Round Y-D1: occlusion culling (the main view only: last frame's answers decide what the passes skip)
            bool cull = !photo && !clashView && !_clashShot;
            _renderer.OcclusionMode = _occlusionMode;
            _renderer.BeginOcclusionFrame(Camera, cull);
            if (!photo) { timers.Stage(GpuTimers.EFFECTS); }
            if (_clashShot)
            {
                _renderer.SkipScreenEffects();
            }
            else
            {
                string effectsError = _renderer.UpdateScreenEffects(Camera, width, height, _groupVisible, Dynamics, _groundZ, _ambientOcclusion && !clashNoAo, glow, ssr > 0);
                if (effectsError != null) { OnScreenEffectsFailure(effectsError); }
            }

            // ---- Reflection probes: a couple of faces per frame until baked, re-baked a moment after things change
            if (!photo) { timers.Stage(GpuTimers.REFLECTIONS); }
            if (!photo) { UpdateReflectionProbes(); }

            var p = new SceneDrawParams
            {
                ViewProjection = Camera.ViewProjection,
                Planes = Camera.Planes,
                Eye = Camera.Position,
                Whitecard = _whitecard,
                Realistic = _realistic,
                Reflections = _reflections,
                ReflectThreshold = _reflectThreshold / 100f,
                ReflectGain = _reflectStrength,
                ReflectDebug = _reflectDebug,
                Probes = _reflectProbes,
                Time = _clock,
                Tint = _tintMode,
                Plan = false,
                ClipZ = new Vector2(-1e7f, 1e7f),
                FogDensity = 0.0022f,
                Sun = true,
                Section = true,
                ScreenReflections = ssr // Round X-C1
            };

            // ---- Round X-C1: mirrored views of the biggest glass / mirror planes in view (stills; not in the clash view or
            // its pictures)
            if (!_clashShot)
            {
                // Stills: Low 2 planes at half resolution, Medium 4 at full, High 8 at full (none in a warm-up pass)
                int planarPlanes = clashView || ssr == 0 ? 0 : ssr == 1 ? 2 : ssr == 2 ? 4 : 8;
                float planarScale = ssr >= 2 ? 1f : 0.5f;
                if (!photo && planarPlanes > 0)
                {
                    // Live view: cap reflective planes to keep interactive rendering responsive
                    planarPlanes = Math.Min(planarPlanes, 2);
                    planarScale = 0.5f;
                }
                _renderer.UpdatePlanarReflections(p, width, height, planarPlanes, planarScale, _groupVisible, Dynamics, _groundZ);
            }
            p.Planar = !clashView && !_clashShot && _renderer.PlanarCount > 0;
            p.Occlusion = cull; // Y-D1: after the planar views (they draw everything)
            if (!photo) { timers.Stage(GpuTimers.OPAQUE); }

            // ---- 3D scene into the (optionally multisampled) target
            target.Ensure(width, height, samples);
            target.Bind();
            Vector3 fog = _renderer.FogColour;
            Gl.ClearColor(fog.X, fog.Y, fog.Z, 1f);
            Gl.Clear(Gl.COLOR_BUFFER_BIT | Gl.DEPTH_BUFFER_BIT);
            Gl.Enable(Gl.DEPTH_TEST);
            Gl.DepthFunc(Gl.LEQUAL);
            Gl.Disable(Gl.CULL_FACE);
            Gl.Disable(Gl.BLEND);

            _renderer.DrawSky(Camera);
            UpdateGrass(); // grass round: builds / uploads the grass field when needed

            if (clashView)
            {
                // Clash view (Round C-B): the pair in red / green, the rest ghosted / wireframe / real / hidden (G)
                DrawClashView(p);
            }
            else
            {
                // Round Y-D2: depth first, so the lit opaque draws shade each pixel once
                if (_depthPrepass) { _renderer.BeginDepthPrepass(p, _groupVisible, Dynamics); }
                _renderer.DrawStatic(p, _groupVisible, transparent: false);
                _renderer.DrawDynamic(p, Dynamics, transparent: false);
                _renderer.EndDepthPrepass();
                if (_sectionCount > 0)
                {
                    // Fill the cut solids (stencil caps) before the ground
                    float extent = Scene.Bounds.Size.Length() + 50f;
                    _renderer.DrawSectionCaps(Camera.ViewProjection, Camera.Planes, Camera.Position, _groupVisible, Dynamics, _capColour, extent);
                }
                _renderer.DrawGround(Camera, _groundZ);

                // Round Y-D1: query the chunks' boxes against the depth so far (answers decide next frame)
                if (cull)
                {
                    if (!photo) { timers.Stage(GpuTimers.OCCLUSION); }
                    _renderer.QueryOcclusion(Camera.ViewProjection, Camera.Planes, Camera.Position, _groupVisible);
                }
                if (!photo) { timers.Stage(GpuTimers.REST); }
                DrawGrass(p); // grass round: blades over the grass materials (Realistic)

                // Gun highlights (scan target, primed demolitions, gizmo target…)
                Gun active = _guns[_activeGun];
                _highlights.Clear();
                // Hidden UI: no tints either, unless a gun is in the middle of something (Gizmo / Clone holding an element)
                if (!_unarmed && !photo && !_paused && (!_uiHidden || active.CapturesInput)) { active.CollectHighlights(_highlights); }
                if (!photo) { AddFixtureHighlight(_highlights); } // Round Y-C2: the fixture dock's fixture
                if (_highlights.Count > 0)
                {
                    Gl.Enable(Gl.BLEND);
                    Gl.BlendFunc(Gl.SRC_ALPHA, Gl.ONE_MINUS_SRC_ALPHA);
                    Gl.Enable(Gl.POLYGON_OFFSET_FILL);
                    Gl.PolygonOffset(-1f, -2f);
                    foreach (Highlight highlight in _highlights) { DrawHighlight(p, highlight); }
                    Gl.Disable(Gl.POLYGON_OFFSET_FILL);
                    Gl.Disable(Gl.BLEND);
                }

                // Transparent pass (glass etc.), after the colour profile's ghosted elements (colour round A)
                Gl.Enable(Gl.BLEND);
                Gl.BlendFunc(Gl.SRC_ALPHA, Gl.ONE_MINUS_SRC_ALPHA);
                Gl.DepthMask(false);
                _renderer.DrawProfileGhosts(p, _groupVisible, Dynamics);
                _renderer.DrawStatic(p, _groupVisible, transparent: true);
                _renderer.DrawDynamic(p, Dynamics, transparent: true);
                Gl.DepthMask(true);
                Gl.Disable(Gl.BLEND);


                // Round X-C1: this frame's colour (before bloom, markers and UI) for next frame's screen-space reflections
                if (ssr > 0 && !_clashShot) { _renderer.CaptureReflectionHistory(target, Camera.ViewProjection, Camera.Position); }
            }

            // Bloom from glowing surfaces over everything (glass included)
            _renderer.CompositeGlow();

            // Round W-C: a lightning flash in the classic light (with sun lighting it lifted the sky light instead); drops on
            // the lens (the main view only: never photos, videos, thumbnails or screenshots)

            // Markers: depth-tested, then a faint x-ray copy so markers behind walls stay discoverable (none while the
            // UI is hidden or in photo mode: clean views)
            if (!_uiHidden && !photo)
            {
                DrawRoomTint(); // Round Z-A: the check's floor tint (depth-tested only), under the markers
                _overlay.Begin(Camera);
                for (int i = 0; i < _guns.Length; i++) { _guns[i].DrawWorld(_overlay, i == _activeGun && !_unarmed); }
                DrawClashMarkers(_overlay);
                DrawGrids(); // grids round A: the lines under the markers
                _overlay.Draw(Camera, depthTest: true, alpha: 1f, additive: false);
                _overlay.Draw(Camera, depthTest: false, alpha: 0.16f, additive: false);
                DrawClashToolOverlay(); // C-C2: a grouping tool's dots (brighter through walls), the cluster sphere
            }

            // Section box frame and handles (editor only)
            if (!photo) { DrawSectionGizmo(); }
            if (!photo) { timers.EndFrame(); } // Y-D1
        }

        /// <summary>
        /// AO and glow could not be set up on this GPU: switch AO off (the setting is saved off too) and the bloom off
        /// for this session (surfaces still glow, lights still light), and say why.
        /// </summary>
        private void OnScreenEffectsFailure(string reason)
        {
            _ambientOcclusion = false;
            _bloomFailed = true;
            _renderer.Artificial.Bloom = 0f;
            _renderer.DisableScreenEffects();
            Sound.Play(SoundId.Error);
            Toast(reason, 6f, important: true);
        }

        /// <summary>
        /// Tints one static element or dynamic instance.
        /// </summary>
        private void DrawHighlight(in SceneDrawParams p, Highlight highlight)
        {
            Vector4 colour = Rgba.ToVector(highlight.Colour);
            colour.W = highlight.Strength;

            if (highlight.DynamicId > 0)
            {
                Physics.DynamicInstance instance = Dynamics.Find(highlight.DynamicId);
                if (instance != null && Dynamics.IsActive(instance)) { _renderer.DrawDynamicHighlight(p, instance, colour); }
            }
            else if (highlight.Element >= 0 && _pickMask[highlight.Element])
            {
                _renderer.DrawElementHighlight(p, highlight.Element, colour);
            }
        }

        #region Minimap

        /// <summary>
        /// Draws the plan view (cut at 1.2 m above the current level) into a scissored viewport.
        /// </summary>
        /// <param name="surfaceWidth">The surface being drawn to (0 = the window): the video draws it into its frame.</param>
        /// <param name="surfaceHeight">Its height (0 = the window).</param>
        private void DrawMinimapPlan(float x, float y, float w, float h, int surfaceWidth = 0, int surfaceHeight = 0)
        {
            int height = surfaceHeight > 0 ? surfaceHeight : _window.Height;
            int width = surfaceWidth > 0 ? surfaceWidth : _window.Width;
            int vx = (int)x, vy = height - (int)(y + h), vw = Math.Max(1, (int)w), vh = Math.Max(1, (int)h);

            Gl.Enable(Gl.SCISSOR_TEST);
            Gl.Scissor(vx, vy, vw, vh);
            Gl.Viewport(vx, vy, vw, vh);
            Vector4 background = Rgba.ToVector(UiTheme.MAP_BACKGROUND);
            Gl.ClearColor(background.X, background.Y, background.Z, 1f);
            Gl.Clear(Gl.COLOR_BUFFER_BIT | Gl.DEPTH_BUFFER_BIT);

            float elevation = Scene.Levels.Length > 0 ? Scene.Levels[LevelIndexAt(_player.Feet.Z)].Elevation : _player.Feet.Z;
            float metresAcross = MapMetresAcross;
            float metresHigh = metresAcross * h / w;

            var eye = new Vector3(_player.Feet.X, _player.Feet.Y, elevation + 60f);
            Matrix4x4 view = Matrix4x4.CreateLookAt(eye, eye - Vector3.UnitZ, Vector3.UnitY);
            Matrix4x4 projection = FpsCamera.Orthographic(metresAcross, metresHigh, 1f, 200f);
            Matrix4x4 viewProjection = view * projection;
            FpsCamera.ExtractPlanes(viewProjection, _mapPlanes);

            var p = new SceneDrawParams
            {
                ViewProjection = viewProjection,
                Planes = _mapPlanes,
                Eye = eye,
                Whitecard = _whitecard,
                Realistic = _realistic,
                Tint = _tintMode,
                Plan = true,
                ClipZ = new Vector2(elevation - 0.3f, elevation + 1.2f),
                FogDensity = 0f
            };
            Gl.Enable(Gl.DEPTH_TEST);
            _renderer.DrawStatic(p, _groupVisible, transparent: false);
            _renderer.DrawDynamic(p, Dynamics, transparent: false);

            Gl.Disable(Gl.SCISSOR_TEST);
            Gl.Viewport(0, 0, width, height);
        }

        private const float MapMetresAcross = 34f;

        /// <summary>
        /// Draws the minimap frame, header and markers (UI layer).
        /// </summary>
        private void DrawMinimapOverlay(float x, float y)
        {
            FontAtlas f = _ui.Atlas;
            float w = S(220), h = S(208);
            float mapX = x + S(8), mapY = y + S(30), mapW = S(204), mapH = S(170);

            // Frame: header strip and border only (the plan itself was drawn in 3D)
            _ui.Rect(x, y, w, S(30), UiTheme.PANEL);
            _ui.Rect(x, y + S(30), S(8), h - S(30), UiTheme.PANEL);
            _ui.Rect(x + w - S(8), y + S(30), S(8), h - S(30), UiTheme.PANEL);
            _ui.Rect(x + S(8), y + h - S(8), w - S(16), S(8), UiTheme.PANEL);
            _ui.Outline(x, y, w, h, MathF.Max(1f, MathF.Round(UiScale)), UiTheme.PANEL_BORDER);

            Text.Clear().Append("MAP · ").Append(_levelNamesUpper.Length > 0 ? _levelNamesUpper[LevelIndexAt(_player.Feet.Z)] : "\u2014");
            _ui.Text(f.Small, x + S(8), y + S(9), Text.Span, UiTheme.TEXT_MUTED, S(1f));
            _ui.TextRight(f.Small, x + w - S(8), y + S(9), HINT_MAP_KEY.Text, UiTheme.TEXT_MUTED, S(1f));

            float metresPerPixel = MapMetresAcross / mapW;
            float cx = mapX + mapW * 0.5f, cy = mapY + mapH * 0.5f;
            float levelZ = _player.Feet.Z;
            int levelIndex = LevelIndexAt(levelZ);

            DrawMinimapGrids(f, mapX, mapY, mapW, mapH, cx, cy, metresPerPixel); // grids round B: under the markers

            // Comments on this level
            foreach (CommentRecord record in Comments.Comments)
            {
                if (LevelIndexAt(record.Local.Z - 0.5f) != levelIndex) { continue; }
                MapDot(record.Local, UiTheme.COMMENT, S(3.5f));
            }
            foreach (BookmarkRecord bookmark in Bookmarks.Bookmarks)
            {
                if (LevelIndexAt(bookmark.Local.Z) != levelIndex) { continue; }
                MapDot(bookmark.Local, UiTheme.BOOKMARK, S(3f));
            }

            // View cone and player arrow (north up, screen Y down)
            float angle = -_player.Yaw;
            float halfFov = _fov * MathF.PI / 360f;
            _ui.Wedge(cx, cy, S(56), angle - halfFov, angle + halfFov, Rgba.WithAlpha(UiTheme.ACCENT, 0.13f));
            var forward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var side = new Vector2(-forward.Y, forward.X);
            Vector2 tip = new Vector2(cx, cy) + forward * S(10);
            Vector2 left = new Vector2(cx, cy) - forward * S(6) + side * S(7);
            Vector2 right = new Vector2(cx, cy) - forward * S(6) - side * S(7);
            Vector2 notch = new Vector2(cx, cy) - forward * S(2);
            _ui.Triangle(tip.X, tip.Y, left.X, left.Y, notch.X, notch.Y, UiTheme.ACCENT);
            _ui.Triangle(tip.X, tip.Y, notch.X, notch.Y, right.X, right.Y, UiTheme.ACCENT);

            void MapDot(Vector3 world, uint colour, float radius)
            {
                float mx = cx + (world.X - _player.Feet.X) / metresPerPixel;
                float my = cy - (world.Y - _player.Feet.Y) / metresPerPixel;
                if (mx < mapX + radius || mx > mapX + mapW - radius || my < mapY + radius || my > mapY + mapH - radius) { return; }
                _ui.Circle(mx, my, radius, colour, 12);
            }
        }

        #endregion

        #region HUD

        /// <summary>
        /// Builds the in-game HUD.
        /// </summary>
        private void BuildHud(float mapX, float mapY)
        {
            FontAtlas f = _ui.Atlas;
            int width = _window.Width, height = _window.Height;
            Gun active = _guns[_activeGun];

            // Clash mode (Round C-B): its own HUD (docked list, no crosshair or guns)
            if (_clashModeOn)
            {
                BuildReviewPanels();
                BuildClashHud(f, width, height);
                return;
            }

            // Round K-B: the path editor's HUD (dock, scrubber; no crosshair or guns)
            // Screen flash (portals)
            if (_clock < _flashUntil)
            {
                float t = (_flashUntil - _clock) / MathF.Max(_flashLength, 0.01f);
                _ui.Rect(0, 0, width, height, Rgba.WithAlpha(_flashColour, 0.35f * t));
            }

            // World-anchored labels (grids round A: grid bubbles and level tags first, under the rest)
            DrawGridLabels(f);
            DrawRoomTags(f); // Round Z-A: floating room tags, under the guns' labels
            for (int i = 0; i < _guns.Length; i++) { _guns[i].DrawLabels(_ui, i == _activeGun && !_unarmed); }
            DrawGridHoverLabel(f); // grids round B: the hovered node, over the other labels

            // Polish round P-A2: the right-hand column (tools): the minimap (hidden while a dock sits over it on a short
            // window), then the gun's context panel or the open dock beneath it
            if (_showMap && !_dockOverMap) { DrawMinimapOverlay(mapX, mapY); }
            if (!AnyDockOpen && !_unarmed)
            {
                float panelTop = _showMap ? mapY + S(UiTheme.MINIMAP_HEIGHT) + S(UiTheme.COLUMN_GAP) : S(UiTheme.COLUMN_MARGIN);
                float panelWidth = S(260), panelX = width - S(UiTheme.COLUMN_MARGIN) - panelWidth;
                float panelHeight = S(active.PanelHeight) + S(24);
                _ui.Panel(panelX, panelTop, panelWidth, panelHeight, UiTheme.PANEL, UiTheme.PANEL_BORDER);
                active.DrawPanel(_ui, panelX + S(14), panelTop + S(12), panelWidth - S(28));
            }

            if (_sunPanelOpen) { BuildSunPanel(f, _window.Input); }
            BuildGridsPanel(f, _window.Input); // grids round A
            BuildFixtureDock(f, _window.Input); // Round Y-C2
            BuildSectionPanel(f, _window.Input);

            // P-A2: the left-hand column (information): status, coordinates, the legends and the F1 sheet
            BuildInfoColumn(f, height, profileLegend: true, help: true);
            BuildRoomBanner(f, width);
            BuildLiveBanner(f, width, height);
            BuildReviewPanels();
            BuildToast(f, width);
            BuildViewerToolbar(f, width, height);
        }

        private bool ViewerToolbarVisible => !_paused && !_uiHidden
            && !IsEditingComment && !_markupOpen && !_clashModeOn && !OverlayOpen;

        private void BuildViewerToolbar(FontAtlas f, int width, int height)
        {
            if (!ViewerToolbarVisible) { return; }
            InputState input = _window.Input;
            var g = ViewerToolbarGeometry(width, height);

            string settingsLabel = g.W < S(70) ? "SET" : "SETTINGS";
            string commentsLabel = g.W < S(70) ? "ISSUES" : "COMMENTS";
            string bookmarksLabel = g.W < S(70) ? "BOOKS" : "BOOKMARKS";
            MenuButton(f, g.X, g.Y, g.W, settingsLabel, false, false, height: g.H, compact: true);
            MenuButton(f, g.X + (g.W + g.Gap), g.Y, g.W, IsDirty ? "SAVE *" : "SAVE", false, false, height: g.H, compact: true);
            MenuButton(f, g.X + (g.W + g.Gap) * 2, g.Y, g.W, commentsLabel, false, false, height: g.H, compact: true);
            MenuButton(f, g.X + (g.W + g.Gap) * 3, g.Y, g.W, bookmarksLabel, false, false, height: g.H, compact: true);
            MenuButton(f, g.X + (g.W + g.Gap) * 4, g.Y, g.W, _viewerCommandsOpen ? "CLOSE" : "MORE", false, false, height: g.H, compact: true);
            if (!_viewerCommandsOpen) { return; }

            float buttonW = g.MenuButtonW, buttonH = g.MenuButtonH;
            float bx = g.MenuX + S(8), by = g.MenuY + S(8);
            _ui.Panel(g.MenuX, g.MenuY, g.MenuW, S(16) + 6 * buttonH + 5 * g.Gap, UiTheme.PANEL_STRONG, UiTheme.PANEL_BORDER);
            MenuButton(f, bx, by, buttonW, "CONTROLS", false, false, height: buttonH, compact: true);
            MenuButton(f, bx + buttonW + g.Gap, by, buttonW, "RETURN HOME", false, false, height: buttonH, compact: true);
            by += buttonH + g.Gap;
            MenuButton(f, bx, by, buttonW, _clock < _homeSetUntil ? "HOME SAVED" : "SET HOME", false, false, height: buttonH, compact: true);
            MenuButton(f, bx + buttonW + g.Gap, by, buttonW, "FIND ROOM", false, false, enabled: Scene.Rooms.Length > 0, height: buttonH, compact: true);
            by += buttonH + g.Gap;
            MenuButton(f, bx, by, buttonW, "CLASHES", false, false, height: buttonH, compact: true);
            MenuButton(f, bx + buttonW + g.Gap, by, buttonW, GeometryMenuLabel(), false, false, height: buttonH, compact: true);
            by += buttonH + g.Gap;
            int hidden = HiddenThingsCount();
            MenuButton(f, bx, by, buttonW, hidden > 0 ? $"SHOW ALL ({hidden})" : "SHOW ALL", false, false, enabled: hidden > 0, height: buttonH, compact: true);
            MenuButton(f, bx + buttonW + g.Gap, by, buttonW, "SAVE AS…", false, false, height: buttonH, compact: true);
            by += buttonH + g.Gap;
            MenuButton(f, bx, by, g.MenuW - S(16), "FIT VIEW", false, false, height: buttonH, compact: true);
            by += buttonH + g.Gap;
            MenuButton(f, bx, by, g.MenuW - S(16), _live != null ? "LEAVE SESSION" : _options.InApp ? "CLOSE MODEL" : "END SESSION", false, true, height: buttonH, compact: true);
        }

        private (float X, float Y, float W, float H, float Gap, float MenuX, float MenuY, float MenuW, float MenuButtonW, float MenuButtonH) ViewerToolbarGeometry(int width, int height)
        {
            float margin = S(UiTheme.COLUMN_MARGIN), gap = S(6);
            float desiredLeft = S(UiTheme.COLUMN_MARGIN + 250 + UiTheme.COLUMN_GAP + REVIEW_PANEL_WIDTH + 20);
            float minimum = S(5 * 44 + 4 * 6);
            float x = MathF.Min(desiredLeft, MathF.Max(margin, width - minimum - margin));
            float usable = MathF.Max(1f, width - x - margin - gap * 4);
            float buttonW = MathF.Min(S(94), usable / 5f);
            float buttonH = S(30), y = margin;
            float menuW = MathF.Max(1f, MathF.Min(S(280), width - margin * 2));
            float moreX = x + 4 * (buttonW + gap);
            float menuXMax = MathF.Max(margin, width - menuW - margin);
            float menuX = Math.Clamp(moreX + buttonW - menuW, margin, menuXMax);
            float menuY = y + buttonH + gap;
            float menuButtonW = MathF.Max(1f, (menuW - S(24)) * 0.5f);
            return (x, y, buttonW, buttonH, gap, menuX, menuY, menuW, menuButtonW, S(32));
        }

        private bool UpdateViewerToolbarInput(InputState input)
        {
            if (!ViewerToolbarVisible)
            {
                _viewerCommandsOpen = false;
                return false;
            }
            var g = ViewerToolbarGeometry(_window.Width, _window.Height);
            bool leftClick = input.LeftPressed;
            bool rightClick = input.RightPressed;
            float menuH = S(16) + 6 * g.MenuButtonH + 5 * g.Gap;

            bool Hit(float x, float y, float w, float h)
            {
                Vector2 mouse = input.MousePosition;
                return mouse.X >= x && mouse.X < x + w && mouse.Y >= y && mouse.Y < y + h;
            }

            void RunToolbar(int index)
            {
                if (index == 4)
                {
                    _viewerCommandsOpen = !_viewerCommandsOpen;
                    Sound.Play(SoundId.UiClick);
                    return;
                }
                _viewerCommandsOpen = false;
                Sound.Play(SoundId.UiClick);
                switch (index)
                {
                    case 0: OpenSettingsPanel(); break;
                    case 1: Save(saveAs: !IsFileMode); break;
                    case 2: SetPaused(true); OpenComments(); break;
                    case 3: SetPaused(true); OpenBookmarks(); break;
                }
            }

            if (leftClick || rightClick)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = g.X + i * (g.W + g.Gap);
                    if (!Hit(x, g.Y, g.W, g.H)) { continue; }
                    input.ConsumeClicks();
                    if (leftClick) { RunToolbar(i); }
                    return true;
                }
            }

            if (!_viewerCommandsOpen) { return false; }
            if (input.Pressed(InputAction.Back))
            {
                _viewerCommandsOpen = false;
                input.ConsumeFrame();
                return true;
            }

            bool overMenu = Hit(g.MenuX, g.MenuY, g.MenuW, menuH);
            if (input.Wheel != 0) { input.ConsumeWheel(); }
            if (leftClick)
            {
                float bx = g.MenuX + S(8), by = g.MenuY + S(8), bw = g.MenuButtonW, bh = g.MenuButtonH;
                int command = -1;
                for (int row = 0; row < 4; row++)
                {
                    float rowY = by + row * (bh + g.Gap);
                    if (Hit(bx, rowY, bw, bh)) { command = row * 2; break; }
                    if (Hit(bx + bw + g.Gap, rowY, bw, bh)) { command = row * 2 + 1; break; }
                }
                if (command < 0 && Hit(bx, by + 4 * (bh + g.Gap), g.MenuW - S(16), bh)) { command = 8; }
                if (command < 0 && Hit(bx, by + 5 * (bh + g.Gap), g.MenuW - S(16), bh)) { command = 9; }
                if (command >= 0)
                {
                    input.ConsumeClicks();
                    if ((command == 3 && Scene.Rooms.Length == 0) || (command == 6 && HiddenThingsCount() == 0)) { return true; }
                    _viewerCommandsOpen = false;
                    Sound.Play(SoundId.UiClick);
                    switch (command)
                    {
                        case 0: OpenControls(); break;
                        case 1: _player.GoHome(); break;
                        case 2: SetHomeHere(); break;
                        case 3: if (Scene.Rooms.Length > 0) { OpenRooms(); } break;
                        case 4: OpenClashes(); break;
                        case 5: OpenGeometry(); break;
                        case 6: if (HiddenThingsCount() > 0) { ShowAll(); } break;
                        case 7: Save(saveAs: true); break;
                        case 8: FitReviewView(); break;
                        case 9: _endRequested = true; break;
                    }
                    return true;
                }
                _viewerCommandsOpen = false;
                input.ConsumeClicks();
                return true;
            }
            if (rightClick) { input.ConsumeClicks(); return true; }
            return true;
        }

        /// <summary>
        /// The HUD in hide-UI mode (U): only the portal flash and important toasts (errors). The comment text box is
        /// drawn by the caller as usual.
        /// </summary>
        private void BuildHiddenHud(int width)
        {
            if (_clock < _flashUntil)
            {
                float t = (_flashUntil - _clock) / MathF.Max(_flashLength, 0.01f);
                _ui.Rect(0, 0, width, _window.Height, Rgba.WithAlpha(_flashColour, 0.35f * t));
            }
            if (_toastImportant) { BuildToast(_ui.Atlas, width); }
        }

        /// <summary>
        /// Top-left status: title, FPS, mode, level, ground, view.
        /// </summary>
        private void BuildStatusPanel(FontAtlas f)
        {
            float x = S(20), y = S(20), w = S(250), h = S(146);
            _ui.Panel(x, y, w, h, UiTheme.PANEL, UiTheme.PANEL_BORDER);
            float titleWidth = _ui.Text(f.Bold, x + S(14), y + S(11), "BIMGO", UiTheme.TEXT, S(2f));

            // Where edits go: LIVE (Revit session; OFFLINE when contact is lost) or FILE (standalone, * when unsaved)
            string badge;
            uint badgeColour;
            if (Source.IsRevit)
            {
                bool connected = _live == null || _live.Connected;
                badge = connected ? "LIVE" : "OFFLINE";
                badgeColour = connected ? UiTheme.ACCENT : UiTheme.DANGER;
            }
            else
            {
                badge = IsDirty ? "FILE*" : "FILE";
                badgeColour = IsDirty ? UiTheme.MEASURE_LABEL : UiTheme.GOOD;
            }
            _ui.Text(f.Small, x + S(14) + titleWidth + S(8), y + S(15), badge, badgeColour, S(1f));

            if (_showFps)
            {
                Text.Clear().Append(_fps, 0).Append(" fps · ").Append(_frameMs, 1).Append(" ms");
                _ui.TextRight(f.Mono, x + w - S(14), y + S(14), Text.Span, UiTheme.GOOD);
            }

            float labelX = x + S(14), valueX = x + S(14) + S(70);
            float rowY = y + S(40);
            float row = S(20);

            _ui.Text(f.Body, labelX, rowY, "MODE", UiTheme.TEXT_MUTED);
            _ui.Text(f.Body, valueX, rowY, ModeLabel(), UiTheme.TEXT);
            rowY += row;

            _ui.Text(f.Body, labelX, rowY, "LEVEL", UiTheme.TEXT_MUTED);
            if (Scene.Levels.Length > 0)
            {
                LevelInfo level = Scene.Levels[LevelIndexAt(_player.Feet.Z)];
                float used = _ui.Text(f.Body, valueX, rowY, level.Name, UiTheme.TEXT);
                Text.Clear().AppendLength(level.Elevation, 3, plusSign: true);
                _ui.Text(f.Mono, valueX + used + S(6), rowY + S(1), Text.Span, UiTheme.TEXT_SOFT);
            }
            else
            {
                _ui.Text(f.Body, valueX, rowY, "—", UiTheme.TEXT);
            }
            rowY += row;

            _ui.Text(f.Body, labelX, rowY, "ROOM", UiTheme.TEXT_MUTED);
            if (CurrentRoom is RoomInfo room)
            {
                float used = _ui.Text(f.Mono, valueX, rowY + S(1), room.Number, UiTheme.ACCENT);
                _ui.TextWrapped(f.Body, valueX + used + S(8), rowY, w - (valueX - x) - used - S(22), room.Name, UiTheme.TEXT, maxLines: 1);
            }
            else
            {
                _ui.Text(f.Body, valueX, rowY, Scene.Rooms.Length == 0 ? "No rooms in this model" : "—", UiTheme.TEXT_MUTED);
            }
            rowY += row;

            _ui.Text(f.Body, labelX, rowY, "GROUND", UiTheme.TEXT_MUTED);
            Text.Clear().AppendLength(_groundZ, 3);
            _ui.Text(f.Mono, valueX, rowY + S(1), Text.Span, UiTheme.TEXT);
            rowY += row;

            _ui.Text(f.Body, labelX, rowY, "VIEW", UiTheme.TEXT_MUTED);
            _ui.Text(f.Body, valueX, rowY, ColourModeLabel(), UiTheme.TEXT);
        }

        /// <summary>True while one of the docks (sun, section box, grids) is open.</summary>
        private bool AnyDockOpen => _sunPanelOpen || _sectionOpen || _gridsPanelOpen || _fixtureDockOpen;

        /// <summary>
        /// Polish round P-A2: the left-hand column (information), stacked from the top: the status panel and the
        /// coordinates; from the bottom: the F1 note or sheet; the colour legend fills what is left.
        /// While the F1 sheet is open, or when the column runs out of room, the legends collapse to their title rows.
        /// </summary>
        /// <param name="f">Fonts.</param>
        /// <param name="height">The target's height (the window, or a video frame).</param>
        /// <param name="profileLegend">Show the colour profile's legend (when one shows).</param>
        /// <param name="help">Show the F1 note or sheet.</param>
        private void BuildInfoColumn(FontAtlas f, int height, bool profileLegend, bool help)
        {
            float x = S(UiTheme.COLUMN_MARGIN), gap = S(10);
            BuildStatusPanel(f);
            float top = S(UiTheme.COLUMN_MARGIN) + S(UiTheme.STATUS_HEIGHT) + gap;
            if (_coordinateReadout != CoordinateReadout.Off)
            {
                BuildCoordinatePanel(f, x, top);
                top += S(UiTheme.COORDS_HEIGHT) + gap;
            }
            float perf = BuildPerformancePanel(f, x, top); // Round Y-D1
            if (perf > 0f) { top += perf + gap; }
            if (!profileLegend && !help) { return; }

            float bottom = height - S(UiTheme.COLUMN_MARGIN);
            if (help) { bottom = BuildHelp(f, height, top) - gap; }
            bool collapse = help && _showHelp;

            if (profileLegend) { BuildProfileLegend(f, top, bottom, collapse); }
        }

        /// <summary>
        /// Bottom-left controls hint (F1 toggles): a one-line note, or the sheet stacked up from the bottom no higher
        /// than <paramref name="minTop"/> (P-A2: rows that don't fit are left out, the last "F1 hides" row stays).
        /// </summary>
        /// <returns>The top of what was drawn (the legends stack above it).</returns>
        private float BuildHelp(FontAtlas f, int height, float minTop)
        {
            float x = S(20);
            if (!_showHelp)
            {
                _ui.Text(f.Small, x, height - S(30), HINT_HELP.Text, UiTheme.TEXT_MUTED); // help is off by default: a small note
                return height - S(42);
            }

            // Columns sized to the widest key and action (measured again when the UI scale or the rows change)
            (string Key, string Action)[] rows = HelpRows();
            if (_helpScale != UiScale)
            {
                _helpScale = UiScale;
                _helpKeyWidth = 0f;
                _helpActionWidth = 0f;
                for (int i = 0; i < rows.Length; i++)
                {
                    if (i == 0)
                    {
                        _helpActionWidth = MathF.Max(_helpActionWidth, UiBatch.Measure(f.Small, rows[i].Action, S(1.2f)) - S(16)); // the title spans the columns
                        continue;
                    }
                    _helpKeyWidth = MathF.Max(_helpKeyWidth, UiBatch.Measure(f.Mono, rows[i].Key));
                    _helpActionWidth = MathF.Max(_helpActionWidth, UiBatch.Measure(f.Body, rows[i].Action));
                }
            }

            float row = S(17);
            int fit = Math.Max(0, (int)((height - S(20) - minTop - S(20)) / row));
            int shown = Math.Min(rows.Length, fit);
            if (shown < 3)
            {
                _ui.Text(f.Small, x, height - S(30), HINT_HELP.Text, UiTheme.TEXT_MUTED); // no room for the sheet
                return height - S(42);
            }
            float h = shown * row + S(20);
            float y = height - S(20) - h;
            float keyWidth = _helpKeyWidth + S(16);
            _ui.Panel(x, y, S(12) + keyWidth + _helpActionWidth + S(14), h, Rgba.Hex(0x0C0E12, 0.66f), Rgba.Hex(0xFFFFFF, 0.1f));

            float rowY = y + S(10);
            for (int n = 0; n < shown; n++)
            {
                int i = n == shown - 1 ? rows.Length - 1 : n; // the last row ("F1 hides") always shows
                if (i == 0)
                {
                    _ui.Text(f.Small, x + S(12), rowY + S(2), rows[0].Action, UiTheme.ACCENT, S(1.2f)); // the situation
                    rowY += row;
                    continue;
                }
                bool last = i >= rows.Length - 2;
                _ui.Text(f.Mono, x + S(12), rowY, rows[i].Key, last ? UiTheme.TEXT_MUTED : UiTheme.TEXT);
                _ui.Text(f.Body, x + S(12) + keyWidth, rowY - S(1), rows[i].Action, last ? UiTheme.TEXT_MUTED : UiTheme.TEXT_SOFT);
                rowY += row;
            }
            return y;
        }

        /// <summary>
        /// Top-centre banner when the player walks into a different room.
        /// </summary>
        private void BuildRoomBanner(FontAtlas f, int width)
        {
            if (CurrentRoom is not RoomInfo room || _clock >= _roomBannerUntil) { return; }
            float remaining = _roomBannerUntil - _clock;
            float alpha = Math.Clamp(remaining / 0.5f, 0f, 1f) * Math.Clamp((2.4f - remaining) / 0.2f, 0f, 1f);

            float numberWidth = UiBatch.Measure(f.Mono, room.Number) + S(20);
            float nameWidth = UiBatch.Measure(f.Bold, room.Name, S(1f)) + S(24);
            float x = MathF.Round(width * 0.5f - (numberWidth + nameWidth) * 0.5f), y = S(62), h = S(32);
            _ui.Rect(x, y, numberWidth, h, Rgba.WithAlpha(UiTheme.ACCENT, 0.92f * alpha));
            _ui.Text(f.Mono, x + S(10), y + S(8), room.Number, Rgba.WithAlpha(UiTheme.SCAN_TAG_TEXT, alpha));
            _ui.Rect(x + numberWidth, y, nameWidth, h, Rgba.WithAlpha(UiTheme.PANEL_STRONG, 0.88f * alpha));
            _ui.Text(f.Bold, x + numberWidth + S(12), y + S(7), room.Name, Rgba.WithAlpha(UiTheme.TEXT, alpha), S(1f));
        }

        /// <summary>
        /// Revit changes waiting for a refresh, or a refresh in progress (live sessions).
        /// </summary>
        private void BuildLiveBanner(FontAtlas f, int width, int height)
        {
            if (_live == null) { return; }

            uint colour;
            if (_live.Refreshing)
            {
                Text.Clear().Append("REVIT · EXTRACTING A FRESH SNAPSHOT…");
                colour = UiTheme.ACCENT;
            }
            else if (_live.ModelChanges > 0 && _live.Connected)
            {
                Text.Clear().Append("MODEL CHANGED IN REVIT (").Append(_live.ModelChanges).Append(_live.ModelChanges == 1 ? " CHANGE" : " CHANGES").Append(") · ").Append(HINT_REFRESH_KEY.Text).Append(" REFRESH");
                colour = UiTheme.MEASURE_LABEL;
            }
            else
            {
                return;
            }

            ReadOnlySpan<char> text = Text.Span;
            float textWidth = UiBatch.Measure(f.Small, text, S(1f)) + S(24);
            float x = width * 0.5f - textWidth * 0.5f;
            float y = height - S(20) - S(52) - S(8) - S(24) - S(60);
            _ui.Rect(x, y, textWidth, S(24), Rgba.Hex(0x0C0E12, 0.8f));
            _ui.Outline(x, y, textWidth, S(24), MathF.Max(1f, UiScale), Rgba.WithAlpha(colour, 0.6f));
            _ui.TextCentred(f.Small, width * 0.5f, y + S(6), text, colour, S(1f));
        }

        /// <summary>
        /// Top-centre transient message.
        /// </summary>
        private void BuildToast(FontAtlas f, int width)
        {
            if (_toast == null || _clock >= _toastUntil) { return; }
            float remaining = _toastUntil - _clock;
            float alpha = Math.Clamp(remaining / 0.4f, 0f, 1f);
            float textWidth = UiBatch.Measure(f.Body, _toast);
            float w = textWidth + S(32), h = S(32);
            float x = width * 0.5f - w * 0.5f, y = S(20);
            _ui.Panel(x, y, w, h, Rgba.WithAlpha(UiTheme.PANEL_STRONG, 0.88f * alpha), Rgba.WithAlpha(UiTheme.ACCENT, 0.6f * alpha));
            _ui.Text(f.Body, x + S(16), y + S(7), _toast, Rgba.WithAlpha(UiTheme.TEXT, alpha));
        }

        #endregion
    }
}
