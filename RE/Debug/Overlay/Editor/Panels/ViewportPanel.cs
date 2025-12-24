using BulletSharp;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.World;
using RE.Core.World.Components.Physics;
using RE.Core.World.Physics;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Utils;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using TkVector3 = OpenTK.Mathematics.Vector3;
using static Hexa.NET.ImGui.ImGui;
using Quaternion = BulletSharp.Math.Quaternion;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace RE.Debug.Overlay.Editor.Panels
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

        private readonly string[] _options = ["World", "Local"];

        private int _selectedIndex;
        private Vector2 _lastClickPosition;
        private int _lastSelectedIndex = -1;
        private List<GameObject> _lastHitObjects = new();
        private bool _showPhysOptions;
        private bool _physHasHovered;
        private bool _showOverlay;
        private bool _showAabb, _showWireframe, _xray;
        private CollisionDispatcher _dispatcher;
        private DbvtBroadphase _broadphase;
        private SpriteRenderer _cameraSprite;

        ~ViewportPanel() => CollisionWorld.Dispose();

        public ViewportPanel()
        {
            SetupBullet();

            _cameraSprite = new(Vector3.Zero, "Assets/sprites/editor/camera.png", scale: 0.5f);

            _translateIcon = new("assets/sprites/editor/translate.png");
            _rotateIcon = new("assets/sprites/editor/rotate.png");
            _scaleIcon = new("assets/sprites/editor/scale.png");
            _lightOffIcon = new("assets/sprites/editor/lightOff.png");
            _lightOnIcon = new("assets/sprites/editor/lightOn.png");
            _skyboxOffIcon = new("assets/sprites/editor/skyboxOff.png");
            _skyboxOnIcon = new("assets/sprites/editor/skyboxOn.png");
            _axisOffIcon = new("assets/sprites/editor/axisOff.png");
            _axisOnIcon = new("assets/sprites/editor/axisOn.png");
            _gridOffIcon = new("assets/sprites/editor/gridOff.png");
            _gridOnIcon = new("assets/sprites/editor/gridOn.png");
            _physOptionsIcon = Util.CreateMissingTexture(4);
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

            if (Begin("Viewport", ImGuiWindowFlags.NoScrollbar))
            {
                DrawToolbar();
                Separator();

                if (IsWindowHovered() && !GetIO().WantCaptureKeyboard && !IsAnyMouseDown())
                    SetWindowFocus();

                MouseDown = (IsWindowFocused() && IsWindowHovered());
                if (IsWindowFocused())
                {
                    MoveCamera();
                }

                PanoramicCameraMove();
                LeftMouseButtonHandler();


                var contentSize = GetContentRegionAvail();

                if (contentSize is { X: > 0, Y: > 0 } && (ViewportSize.X != contentSize.X || ViewportSize.Y != contentSize.Y))
                {
                    ViewportSize = contentSize;
                    Game.Instance.SetupSceneFbo((int)ViewportSize.X, (int)ViewportSize.Y);
                    Camera.Editor.RenderWidth = (int)ViewportSize.X;
                    Camera.Editor.RenderHeight = (int)ViewportSize.Y;
                }

                SetImGuizmoRect();

                Image(new ImTextureRef() { TexID = Game.Instance.SceneTextureId }, ViewportSize,
                    new Vector2(0, 1),
                    new Vector2(1, 0));

                DrawDebugText();

                DrawGrid();
                DrawGizmos();
            }
            End();
        }
        public void SetImGuizmoRect()
        {
            var contentRegionSize = GetContentRegionAvail();
            var viewportStart = GetCursorScreenPos();
            var x = viewportStart.X;
            var y = viewportStart.Y;
            var w = contentRegionSize.X;
            var h = contentRegionSize.Y;

            ImGuizmo.SetRect(x, y, w, h);
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

                if (ImGuizmo.Manipulate(ref v.M11, ref p.M11, Operation, Mode, ref openTkWorldMatrix.M11))
                {
                    openTkWorldMatrix.Decompose(out var scale, out var rotation, out var translation);
                    transform.Position = translation.ToOpenTkVector3();
                    transform.Rotation = rotation.ToOpenTkQuaternion();
                    transform.Scale = scale.ToOpenTkVector3();
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

                var viewportScreenPos = GetCursorScreenPos();
                var mousePosGlobal = GetMousePos();
                var viewportAvailableSize = GetContentRegionAvail();

                var viewportX = mousePosGlobal.X - viewportScreenPos.X;
                var viewportY = mousePosGlobal.Y - viewportScreenPos.Y;

                if (viewportX < 0 || viewportY < 0 || viewportX > viewportAvailableSize.X || viewportY > viewportAvailableSize.Y)
                    return;

                var yOpentk = viewportAvailableSize.Y - viewportY;

                var currentCamera = Camera.Editor;

                var mView = currentCamera.GetViewMatrix();
                var mProj = currentCamera.GetProjectionMatrix();
                var mViewProjInverse = Matrix4.Invert(mView * mProj);

                float vpX = 0;
                float vpY = 0;
                var vpW = viewportAvailableSize.X;
                var vpH = viewportAvailableSize.Y;
                var minZ = 0.0f;
                var maxZ = 1.0f;

                var screenNear = new TkVector3(viewportX, yOpentk, minZ);
                var screenFar = new TkVector3(viewportX, yOpentk, maxZ);

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

                var position = new BulletSharp.Math.Vector3(pos.X, pos.Y, pos.Z);
                var rotation = new BulletSharp.Math.Quaternion(rot.X, rot.Y, rot.Z, rot.W);

                g.ViewportObject.WorldTransform = BulletSharp.Math.Matrix.RotationQuaternion(rotation) * BulletSharp.Math.Matrix.Translation(position);

                var scale = g.Transform.Scale;
                var bulletScale = new BulletSharp.Math.Vector3(scale.X, scale.Y, scale.Z);

                if (g.ViewportObject.CollisionShape.LocalScaling != bulletScale)
                {
                    g.ViewportObject.CollisionShape.LocalScaling = bulletScale;
                }
                CollisionWorld.UpdateSingleAabb(g.ViewportObject);

            }
        }
        private void PanoramicCameraMove()
        {
            var isMiddleMouseDown = IsMouseDown(ImGuiMouseButton.Middle);
            var shouldPan = isMiddleMouseDown && IsWindowFocused();

            if (!shouldPan)
            {
                return;
            }
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

            var imTextureId1 = _translateIcon;
            var imTextureId2 = _rotateIcon;
            var imTextureId3 = _scaleIcon;
            var imTextureId4 = _lightOffIcon;
            var imTextureId5 = _lightOnIcon;
            var imTextureId6 = _skyboxOffIcon;
            var imTextureId7 = _skyboxOnIcon;
            var imTextureId8 = _axisOffIcon;
            var imTextureId9 = _axisOnIcon;
            var imTextureId10 = _gridOffIcon;
            var imTextureId11 = _gridOnIcon;
            var imTextureId12 = _physOptionsIcon;

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
            TextTooltip("Show gray grid on XZ axis. Can be laggy!");

            SameLine();
            if (ImageButton("##phys", _physOptionsIcon, new Vector2(size, size)))
                _showPhysOptions = true;
            TextTooltip("Show Bullet Physics engine overlay settings.");
            if (_showPhysOptions)
                ShowPhysTooltip();
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
            var p = Camera.Editor.Position;

            var speed = 7f * Time.DeltaTime;
            var input = Game.Instance.KeyboardState;
            var mInput = Game.Instance.MouseState;
            if (mInput.ScrollDelta.Y < 0 && IsWindowHovered())
                p += -(Camera.Editor.Front).Normalized();
            if (mInput.ScrollDelta.Y > 0 && IsWindowHovered())
                p += (Camera.Editor.Front).Normalized();

            if (input.IsKeyDown(Keys.W))
                p += (Camera.Editor.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.S))
                p -= (Camera.Editor.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.A))
                p -= TkVector3.Normalize(TkVector3.Cross(Camera.Editor.Front, Camera.Editor.Up)) * speed;
            if (input.IsKeyDown(Keys.D))
                p += TkVector3.Normalize(TkVector3.Cross(Camera.Editor.Front, Camera.Editor.Up)) * speed;
            if (input.IsKeyDown(Keys.Space))
                p += TkVector3.UnitY * speed;
            if (input.IsKeyDown(Keys.LeftShift))
                p -= TkVector3.UnitY * speed;

            if (!GetIO().WantTextInput)
                Camera.Editor.Position = (p);
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