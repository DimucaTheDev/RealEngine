using BulletSharp;
using BulletSharp.Math;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.Input;
using RE.Core.World;
using RE.Core.World.Components.Physics;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Rendering.Texturing;
using RE.Utils;
using static Hexa.NET.ImGui.ImGui;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Quaternion = BulletSharp.Math.Quaternion;
using TkVector3 = OpenTK.Mathematics.Vector3;
using Vector2 = System.Numerics.Vector2;
using Vector3 = BulletSharp.Math.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace RE.Editor.Panels.Viewport
{
    public class ViewportPanel
    {
        public static Vector2 ViewportSize { get; set; } = new(1280, 720);
        public static CollisionWorld CollisionWorld = null!;
        public static bool MouseDown;

        public ImGuizmoOperation Operation = ImGuizmoOperation.Translate;
        public ImGuizmoMode Mode = ImGuizmoMode.World;

        private static readonly TkVector3 WorldUp = new(0, 1, 0);

        private readonly Texture _translateIcon;
        private readonly Texture _rotateIcon;
        private readonly Texture _scaleIcon;
        private readonly Texture _lightOffIcon;
        private readonly Texture _lightOnIcon;
        private readonly Texture _skyboxOffIcon;
        private readonly Texture _skyboxOnIcon;
        private readonly Texture _axisOffIcon;
        private readonly Texture _axisOnIcon;
        private readonly Texture _gridOffIcon;
        private readonly Texture _gridOnIcon;
        private readonly Texture _physOptionsIcon;
        private readonly Texture _particlesOn;
        private readonly Texture _particlesOff;
        private readonly Texture _startSimulation;
        private readonly Texture _stopSimulation;

        private readonly string[] _options = ["World", "Local"];

        private int _selectedIndex;
        private int _lastSelectedIndex = -1;
        private List<GameObject> _lastHitObjects = new();
        private bool _showPhysOptions;
        private bool _physHasHovered;
        private bool _showOverlay;
        private bool _showAabb, _showWireframe, _xray;
        private bool _isOverViewport;
        private bool _rotating;
        private CollisionDispatcher _dispatcher;
        private DbvtBroadphase _broadphase;
        private SpriteRenderer _cameraSprite;
        private float mouseX;
        private float mouseY;
        private Vector2 _lastMousePos;
        private Vector2 _lastClickPosition;
        private Vector2 _lockedMousePos;
        private Vector2 _lockedGlobalPos;
        private TkVector3 _cameraVelocity = TkVector3.Zero;
        private Vector4 _viewportRect;
        private string _sceneBeforeSimulation;

        public static bool CameraRotate;

        ~ViewportPanel() => CollisionWorld.Dispose();

        public ViewportPanel()
        {
            SetupBullet();

            _cameraSprite = new(TkVector3.Zero, "Assets/editor/sprites/camera.png", scale: 0.5f);

            _translateIcon = new StaticTexture("assets/editor/sprites/translate.png");
            _rotateIcon = new StaticTexture("assets/editor/sprites/rotate.png");
            _scaleIcon = new StaticTexture("assets/editor/sprites/scale.png");
            _lightOffIcon = new StaticTexture("assets/editor/sprites/lightOff.png");
            _lightOnIcon = new StaticTexture("assets/editor/sprites/lightOn.png");
            _skyboxOffIcon = new StaticTexture("assets/editor/sprites/skyboxOff.png");
            _skyboxOnIcon = new StaticTexture("assets/editor/sprites/skyboxOn.png");
            _axisOffIcon = new StaticTexture("assets/editor/sprites/axisOff.png");
            _axisOnIcon = new StaticTexture("assets/editor/sprites/axisOn.png");
            _gridOffIcon = new StaticTexture("assets/editor/sprites/gridOff.png");
            _gridOnIcon = new StaticTexture("assets/editor/sprites/gridOn.png");
            _physOptionsIcon = new AnimatedTexture(
            [StaticTexture.CreateMissingTexture(4),
                StaticTexture.CreateMissingTexture(4, [0, 255, 0, 255])], 2);
            _particlesOn = new StaticTexture("assets/editor/sprites/previewParticlesOn.png");
            _particlesOff = new StaticTexture("assets/editor/sprites/previewParticlesOff.png");
            _startSimulation = new StaticTexture("assets/editor/sprites/run.png");
            _stopSimulation = new StaticTexture("assets/editor/sprites/stop.png");
        }

        private void SetupBullet()
        {
            var defaultCollisionConfiguration = new DefaultCollisionConfiguration();
            _dispatcher = new CollisionDispatcher(defaultCollisionConfiguration);
            _broadphase = new DbvtBroadphase();

            //CollisionWorld = new CollisionWorld(PhysicsManager.DynamicsWorld.Dispatcher, PhysicsManager.DynamicsWorld.Broadphase, defaultCollisionConfiguration);
            CollisionWorld = new CollisionWorld(_dispatcher, _broadphase, defaultCollisionConfiguration);
            CollisionWorld.DebugDrawer = new BulletDebugDrawer();
        }

        public void Draw()
        {
            _cameraSprite.Position = Camera.Main.Position;
            _cameraSprite.Render(new(Time.DeltaTime));
            RenderManager.DrawCameraFrustum();

            UpdateBulletObjects();
            PhysicsManager.DynamicsWorld.DebugDrawWorld();

            SetNextWindowPos(new Vector2(318, 27), ImGuiCond.FirstUseEver);
            SetNextWindowSize(new Vector2(1194, 665), ImGuiCond.FirstUseEver);

            var windowFlags = ImGuiWindowFlags.NoScrollbar;
            if (_isOverViewport)
                windowFlags |= ImGuiWindowFlags.NoMove;

            if (Begin("Viewport", windowFlags))
            {
                DrawToolbar();
                Separator();

                if (IsWindowHovered() && (IsMouseDown(ImGuiMouseButton.Left) || IsMouseDown(ImGuiMouseButton.Right)))
                    SetWindowFocus("Viewport");


                MouseDown = (IsWindowFocused() && IsWindowHovered());
                bool hovered = IsWindowHovered(ImGuiHoveredFlags.RootWindow | ImGuiHoveredFlags.AllowWhenBlockedByPopup);
                bool gizmoActive = ImGuizmo.IsUsingAny() || ImGuizmo.IsOver();
                var io = GetIO();
                bool guiWantsMouse = io.WantCaptureMouse || IsAnyItemActive();

                if (hovered && !gizmoActive && !guiWantsMouse)
                {
                    CameraRotate = hovered && IsMouseDown(ImGuiMouseButton.Right);
                }
                bool guiWantsKeyboard = io.WantCaptureKeyboard || IsAnyItemActive();
                if (IsWindowFocused())
                    MoveCamera();

                var contentSize = GetContentRegionAvail();

                if (contentSize is { X: > 0, Y: > 0 } && (ViewportSize.X != contentSize.X || ViewportSize.Y != contentSize.Y))
                {
                    ViewportSize = contentSize;
                    Game.Instance.SetupSceneFbo((int)ViewportSize.X, (int)ViewportSize.Y);
                    Game.Instance.SetupOitFbo((int)ViewportSize.X, (int)ViewportSize.Y);
                    Camera.Editor.RenderWidth = (int)ViewportSize.X;
                    Camera.Editor.RenderHeight = (int)ViewportSize.Y;
                }

                var size = _viewportRect = SetImGuizmoRect();

                Image(new ImTextureRef { TexID = Game.Instance.SceneTextureId }, ViewportSize,
                    new Vector2(0, 1),
                    new Vector2(1, 0));
                _isOverViewport = IsItemHovered();

                if (_isOverViewport)
                {
                    CameraControl();
                }

                DrawDebugText();

                DrawGrid();
                DrawGizmos();

                foreach (var go in SceneManager.CurrentScene.GameObjects)
                {
                    foreach (var comp in go.Components)
                    {
                        if (comp is IViewportRenderer vr)
                        {
                            vr.ViewportRender(new OpenTK.Mathematics.Vector2(size.X, size.Y), new OpenTK.Mathematics.Vector2(size.Z, size.W));
                        }
                    }
                }
                if (IsMouseDown(ImGuiMouseButton.Middle) && hovered && !gizmoActive)
                    PanoramicCameraMove();
                LeftMouseButtonHandler();
            }
            End();

            //CollisionWorld.DebugDrawWorld();
        }

        private void CameraControl()
        {
            Camera cam = Camera.Editor;

            bool rmbDown = IsMouseDown(ImGuiMouseButton.Right);
            bool rmbClicked = IsMouseClicked(ImGuiMouseButton.Right);

            if (rmbClicked && IsWindowHovered())
            {
                _rotating = true;
                _lockedGlobalPos = Mouse.ScreenPosition.ToSystemVector2();
            }

            SetMouseCursor(rmbDown ? ImGuiMouseCursor.None : ImGuiMouseCursor.Arrow);

            if (!rmbDown)
            {
                if (_rotating)
                {
                    _rotating = false;
                    WinApi.SetCursorPos((int)_lockedGlobalPos.X, (int)_lockedGlobalPos.Y);
                }
                return;
            }

            if (_rotating)
            {
                Vector2 currentGlobalPos = Mouse.ScreenPosition.ToSystemVector2();

                Vector2 delta = currentGlobalPos - _lockedGlobalPos;

                if (delta.LengthSquared() > 0)
                {
                    float mouseSensitivity = 0.2f;
                    cam.Yaw += delta.X * mouseSensitivity;
                    cam.Pitch -= delta.Y * mouseSensitivity;
                    cam.Pitch = MathHelper.Clamp(cam.Pitch, -89f, 89f);

                    WinApi.SetCursorPos((int)_lockedGlobalPos.X, (int)_lockedGlobalPos.Y);
                }
            }
        }
        private static Vector4 SetImGuizmoRect()
        {
            var contentRegionSize = GetContentRegionAvail();
            var viewportStart = GetCursorScreenPos();
            var x = viewportStart.X;
            var y = viewportStart.Y;
            var w = contentRegionSize.X;
            var h = contentRegionSize.Y;
            ImGuizmo.SetRect(x, y, w, h);

            return new(x, y, w, h);
        }
        private void DrawGizmos()
        {
            if (SceneEditor.SelectedObject != null)
            {
                var v = Camera.Editor.GetViewMatrix().ToBulletMatrix();
                var p = Camera.Editor.GetProjectionMatrix().ToBulletMatrix();
                var transform = SceneEditor.SelectedObject.Transform;
                var pos = transform.Position;
                var rot = transform.Rotation;
                var sc = transform.Scale;
                var scaleMatrix = Matrix4.CreateScale(sc);
                var rotationMatrix = Matrix4.CreateFromQuaternion(rot);
                var translationMatrix = Matrix4.CreateTranslation(pos);
                var openTkWorldMatrix = (scaleMatrix * rotationMatrix * translationMatrix).ToBulletMatrix();
                ImGuizmo.SetDrawlist();
                ImGuizmo.SetID(0);
                if (ImGuizmo.Manipulate(ref v.M11, ref p.M11, Operation, Mode, ref openTkWorldMatrix.M11))
                {
                    openTkWorldMatrix.Decompose(out var scale, out var rotation, out var translation);
                    SceneEditor.SelectedObject.Transform.Position = translation.ToOpenTkVector3();
                    SceneEditor.SelectedObject.Transform.Rotation = rotation.ToOpenTkQuaternion();
                    SceneEditor.SelectedObject.Transform.Scale = scale.ToOpenTkVector3();
                }
            }
        }

        private void LeftMouseButtonHandler()
        {
            if (IsMouseClicked(ImGuiMouseButton.Left) && IsWindowFocused() && IsWindowHovered() &&
                (!IsMouseDown(ImGuiMouseButton.Right) && !IsMouseDown(ImGuiMouseButton.Middle)))
            {
                if (ImGuizmo.IsUsingAny() || ImGuizmo.IsOver())
                    return;

                var mousePosGlobal = GetMousePos();
                var viewportSize = new Vector2(_viewportRect.Z, _viewportRect.W);

                float vx = mousePosGlobal.X - _viewportRect.X;
                float vy = mousePosGlobal.Y - _viewportRect.Y;

                vy = _viewportRect.W - vy;

                if (vx < 0 || vy < 0 || vx > viewportSize.X || vy > viewportSize.Y)
                    return;

                var screenNear = new TkVector3(vx, vy, 0.0f);
                var screenFar = new TkVector3(vx, vy, 1.0f);

                var currentCamera = Camera.Editor;

                var mView = currentCamera.GetViewMatrix();
                var mProj = currentCamera.GetProjectionMatrix();
                var mViewProjInverse = Matrix4.Invert(mView * mProj);

                float vpX = 0;
                float vpY = 0;
                var vpW = viewportSize.X;
                var vpH = viewportSize.Y;
                var minZ = 0.0f;
                var maxZ = 1.0f;


                var rayOriginWs = TkVector3.Unproject(
                    screenNear,
                    vpX, vpY, vpW, vpH,
                    minZ, maxZ,
                    mViewProjInverse);

                var rayEndWs = TkVector3.Unproject(
                    screenFar,
                    vpX, vpY, vpW, vpH,
                    minZ, maxZ,
                    mViewProjInverse);

                var pStart = rayOriginWs.ToBulletVector3();
                var pEnd = rayEndWs.ToBulletVector3();

                var callback = new AllHitsRayResultCallback(pStart, pEnd);

                CollisionWorld.RayTest(pStart, pEnd, callback);
                //LineRenderer.Main.AddLine(pStart.ToOpenTkVector3(), pEnd.ToOpenTkVector3(), (1, 0, 0, 1), (1, 0, 0, 1));

                if (callback.HasHit)
                {
                    var hits = callback.CollisionObjects.Zip(
                            callback.HitFractions,
                            (obj, fraction) => new { Object = obj, Fraction = fraction }
                        ).OrderBy(hit => hit.Fraction).ToList();

                    var currentClickPosition = GetMousePos();
                    var cursorMoved = (currentClickPosition - _lastClickPosition).LengthSquared() > 16 /* 4x4 pixels */;
                    List<GameObject> currentHitObjects = hits
                        .Select(h =>
                        {
                            if (h.Object.UserObject is Component component)
                            {
                                return component.Owner;
                            }
                            if (h.Object.UserObject is GameObject g)
                            {
                                return g;
                            }
                            return null;
                        })
                        .Where(g => g != null)
                        .DistinctBy(s => s!.Id)
                        .ToList()!;


                    if (cursorMoved || currentHitObjects.Count != _lastHitObjects.Count)
                    {
                        _lastSelectedIndex = 0;
                        _lastHitObjects = currentHitObjects;
                    }
                    else
                    {
                        _lastSelectedIndex = (_lastSelectedIndex + 1) % currentHitObjects.Count;
                    }

                    if (currentHitObjects.Any())
                    {
                        var objectToSelect = currentHitObjects[_lastSelectedIndex];
                        SceneEditor.SelectedObject = objectToSelect;
                    }
                    else
                    {
                        SceneEditor.SelectedObject = null;
                        _lastSelectedIndex = -1;
                        _lastHitObjects.Clear();
                    }

                    _lastClickPosition = currentClickPosition;
                }
                else
                {
                    SceneEditor.SelectedObject = null;
                }
            }
        }
        private void UpdateBulletObjects()
        {
            foreach (var g in SceneManager.CurrentScene.GameObjects)
            {
                if (g.ViewportObject == null!)
                    continue;

                var pos = g.Transform.Position;
                var rot = g.Transform.Rotation;

                var position = new Vector3(pos.X, pos.Y, pos.Z);
                var rotation = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);

                g.ViewportObject.WorldTransform = Matrix.RotationQuaternion(rotation) * Matrix.Translation(position);

                var scale = g.Transform.Scale;
                var bulletScale = new Vector3(scale.X, scale.Y, scale.Z);

                if (g.ViewportObject.CollisionShape.LocalScaling != bulletScale)
                {
                    g.ViewportObject.CollisionShape.LocalScaling = bulletScale;
                }
                CollisionWorld.UpdateSingleAabb(g.ViewportObject);

            }
        }
        private void PanoramicCameraMove()
        {
            var mouseDelta = GetIO().MouseDelta;

            if (mouseDelta.LengthSquared() == 0)
                return;

            var up = GetUpVector();
            var right = GetRightVector();
            var panSpeed = 0.01f;
            var offset = TkVector3.Zero;

            offset -= -right * mouseDelta.X * panSpeed;

            offset += -up * -mouseDelta.Y * panSpeed;

            Camera.Editor.Position += offset;
        }
        private void DrawGrid()
        {
            //var color = new OpenTK.Mathematics.Vector4(0.25f, 0.25f, 0.25f, 0.1f);
            var color = new OpenTK.Mathematics.Vector4(0.75f, 0.75f, 0.75f, 0.3f);
            var size = 50;
            var camPos = (Vector3i)Camera.Editor.Position;

            if (SceneEditor.ShowGrid)
            {
                for (var i = -size; i < size; i++)
                {
                    LineRenderer.DrawLine(new TkVector3(camPos.X + i, 0, camPos.Z + -size),
                        new TkVector3(camPos.X + i, 0, camPos.Z + size), color, color);
                }

                for (var i = -size; i < size; i++)
                {
                    LineRenderer.DrawLine(new TkVector3(camPos.X + -size, 0, camPos.Z + i),
                        new TkVector3(camPos.X + size, 0, camPos.Z + i), color, color);
                }
            }

            if (SceneEditor.ShowAxis)
            {
                if (Math.Abs(camPos.Z) < size)
                    LineRenderer.DrawLine(new(camPos.X + -size, 0, 0), new(camPos.X + size, 0, 0), new(1, 0, 0, 1),
                        new(1, 0, 0, 1));
                if (Math.Abs(camPos.X) < size)
                    LineRenderer.DrawLine(new(0, 0, camPos.Z + -size), new(0, 0, camPos.Z + size), new(0, 0, 1, 1),
                        new(0, 0, 1, 1));
                if (Math.Abs(camPos.X) < size && Math.Abs(camPos.Z) < size)
                    LineRenderer.DrawLine(new(0, -size, 0), new(0, size, 0), new(0, 1, 0, 1), new(0, 1, 0, 1));
            }
        }
        private void DrawDebugText()
        {
            string[] data = [
                $"Viewport size: {ViewportSize.X}x{ViewportSize.Y}",
                $"Fov: {Camera.Editor.Fov}°",
                $"Scene: {SceneManager.CurrentScene.Name}",
                $"FPS: {1/Time.DeltaTime:F0}",
                $"Camera pos: {Camera.Editor.Position.X:F2} | {Camera.Editor.Position.Y:F2} | {Camera.Editor.Position.Z:F2}",
                $"Selected Obj: {(SceneEditor.SelectedObject == null ? "<None>" : $"{SceneEditor.SelectedObject.Name??"<unnamed>"} ({SceneEditor.SelectedObject.Id})")}"
            ];
            foreach (var line in data.Index())
            {
                SetCursorPos(new Vector2(15, 70 + CalcTextSize(line.Item).Y * line.Index));
                Text(line.Item);
            }
        }
        private void DrawToolbar()
        {
            void TextTooltip(string text)
            {
                if (IsItemHovered(ImGuiHoveredFlags.DelayShort))
                {
                    BeginTooltip();
                    Text(text);
                    EndTooltip();
                }
            }

            const int size = 20;

            BeginDisabled(Operation == ImGuizmoOperation.Translate);
            if (ImageButton("##translate", _translateIcon, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Translate;
            EndDisabled();
            TextTooltip("Move selected object in XYZ axis.");


            BeginDisabled(Operation == ImGuizmoOperation.Rotate);
            SameLine();
            if (ImageButton("##rotate", _rotateIcon, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Rotate;
            EndDisabled();
            TextTooltip("Rotate selected object.");

            BeginDisabled(Operation == ImGuizmoOperation.Scale);
            SameLine();
            if (ImageButton("##scale", _scaleIcon, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Scale;
            EndDisabled();
            TextTooltip("Resize selected object.");

            SameLine(150);
            AlignTextToFramePadding();
            Text("Object Space: ");

            SameLine();
            SetNextItemWidth(60);
            if (Combo("##tr_space", ref _selectedIndex, _options, _options.Length))
            {
                var selectedSpace = _options[_selectedIndex];
                Mode = selectedSpace switch
                {
                    "Local" => ImGuizmoMode.Local,
                    "World" => ImGuizmoMode.World,
                    _ => Mode
                };
            }
            TextTooltip("Should be object moved, rotated, or scaled relatively to itself or world.");

            SameLine(350);

            if (ImageButton("##simulation", !SceneEditor.SimulationRunning ? _startSimulation : _stopSimulation,
                    new Vector2(size, size)))
            {
                if (!SceneEditor.SimulationRunning)
                {
                    _sceneBeforeSimulation = SceneManager.SerializeScene(SceneManager.CurrentScene);
                    SceneEditor.SimulationRunning = true;
                }
                else
                {
                    SceneManager.CurrentScene.Dispose();
                    SceneManager.CurrentScene = SceneManager.DeserializeScene(_sceneBeforeSimulation);
                    SceneEditor.SimulationRunning = false;
                }
            }
            TextTooltip($"{(!SceneEditor.SimulationRunning ? "Start" : "Stop")} world simulation.\nScene will return to its original state after simulation stops.");

            SameLine(400);

            if (ImageButton("##light", SceneEditor.PreviewLight ? _lightOnIcon : _lightOffIcon, new Vector2(size, size)))
                SceneEditor.PreviewLight = !SceneEditor.PreviewLight;
            TextTooltip("Preview light on objects");

            SameLine();
            if (ImageButton("##skybox", SceneEditor.PreviewSkybox ? _skyboxOnIcon : _skyboxOffIcon, new Vector2(size, size)))
                SceneEditor.PreviewSkybox = !SceneEditor.PreviewSkybox;
            TextTooltip("Show skybox in background");

            SameLine();
            if (ImageButton("##axis", SceneEditor.ShowAxis ? _axisOnIcon : _axisOffIcon, new Vector2(size, size)))
                SceneEditor.ShowAxis = !SceneEditor.ShowAxis;
            TextTooltip("Show XYZ axis lines in center of the world.");

            SameLine();
            if (ImageButton("##grid", SceneEditor.ShowGrid ? _gridOnIcon : _gridOffIcon, new Vector2(size, size)))
                SceneEditor.ShowGrid = !SceneEditor.ShowGrid;
            TextTooltip("Show gray grid on XZ axis.");

            SameLine();
            if (ImageButton("##phys", _physOptionsIcon, new Vector2(size, size)))
                _showPhysOptions = true;
            TextTooltip("Show Bullet Physics engine overlay settings.");
            if (_showPhysOptions)
                ShowPhysTooltip();

            SameLine();
            if (ImageButton("##particles", SceneEditor.PreviewParticles ? _particlesOn : _particlesOff, new Vector2(size, size)))
                SceneEditor.PreviewParticles = !SceneEditor.PreviewParticles;
            TextTooltip("Update and render particles in editor.");

        }

        private void ShowPhysTooltip()
        {
            var bg = 0.2f;
            PushStyleColor(ImGuiCol.WindowBg, new Vector4(bg, bg, bg, 1));

            OpenPopup("##physOpt");
            SetNextWindowPos(GetMousePos(), ImGuiCond.Appearing);
            if (Begin("##physOpt",
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize))
            {
                if (!_physHasHovered)
                    SetWindowFocus();

                Text("Bullet Physics overlay options");
                Checkbox("Show Overlay", ref _showOverlay);
                Separator();

                BeginDisabled(!_showOverlay);

                Checkbox("Xray", ref _xray);
                Checkbox("AABB", ref _showAabb);
                Checkbox("Wireframe", ref _showWireframe);

                EndDisabled();

                if (_showOverlay)
                {
                    BulletDebugDrawer.Mode = _showAabb ? DebugDrawModes.DrawAabb : 0;
                    BulletDebugDrawer.Mode |= _showWireframe ? DebugDrawModes.DrawWireframe : 0;
                }
                else
                {
                    BulletDebugDrawer.Mode = 0;
                }

                BulletDebugDrawer.Xray = _xray;

                if (IsWindowHovered() && !_physHasHovered)
                    _physHasHovered = true;
                if (!IsWindowFocused() && _physHasHovered)
                {
                    _showPhysOptions = false;
                    _physHasHovered = false;
                }
            }
            End();

            PopStyleColor();
        }

        private void MoveCamera()
        {
            const float targetSpeed = 14f;
            const float lerpFactor = 10f;

            var cam = Camera.Editor;
            TkVector3 inputDir = TkVector3.Zero;

            if (Keyboard.IsKeyDown(Keys.W))
                inputDir += (cam.Front with { Y = 0 }).Normalized();
            if (Keyboard.IsKeyDown(Keys.S))
                inputDir -= (cam.Front with { Y = 0 }).Normalized();
            if (Keyboard.IsKeyDown(Keys.A))
                inputDir -= TkVector3.Normalize(TkVector3.Cross(cam.Front, cam.Up));
            if (Keyboard.IsKeyDown(Keys.D))
                inputDir += TkVector3.Normalize(TkVector3.Cross(cam.Front, cam.Up));

            if (Keyboard.IsKeyDown(Keys.Space))
                inputDir += TkVector3.UnitY;
            if (Keyboard.IsKeyDown(Keys.LeftShift))
                inputDir -= TkVector3.UnitY;

            if (inputDir.LengthSquared > 0)
                inputDir = inputDir.Normalized();

            float currentSpeedLimit = targetSpeed;
            TkVector3 targetVelocity = inputDir * currentSpeedLimit;
            _cameraVelocity = TkVector3.Lerp(_cameraVelocity, targetVelocity, lerpFactor * Time.DeltaTime);
            cam.Position += _cameraVelocity * Time.DeltaTime;

            if (Mouse.ScrollDelta != 0 && IsWindowHovered())
            {
                cam.Position += cam.Front.Normalized() * Mouse.ScrollDelta * 2.0f;
            }
        }
        private TkVector3 GetRightVector()
        {
            var right = TkVector3.Cross(WorldUp, Camera.Editor.Front);
            return TkVector3.Normalize(right);
        }
        private TkVector3 GetUpVector()
        {
            var right = GetRightVector();
            var up = TkVector3.Cross(Camera.Editor.Front, right);
            return TkVector3.Normalize(up);
        }
    }
}