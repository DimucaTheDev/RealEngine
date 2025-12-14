using BulletSharp;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using OpenTK.Graphics.ES11;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.World;
using RE.Core.World.Components.Physics;
using RE.Core.World.Physics;
using RE.Rendering;
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

        private readonly int _translateIcon;
        private readonly int _rotateIcon;
        private readonly int _scaleIcon;
        private readonly int _lightOffIcon;
        private readonly int _lightOnIcon;
        private readonly int _skyboxOffIcon;
        private readonly int _skyboxOnIcon;
        private readonly int _axisOffIcon;
        private readonly int _axisOnIcon;
        private readonly int _gridOffIcon;
        private readonly int _gridOnIcon;
        private readonly int _physOptionsIcon;

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

        ~ViewportPanel() => CollisionWorld.Dispose();

        public ViewportPanel()
        {
            SetupBullet();

            _translateIcon = LoadTexture("assets/sprites/editor/translate.png");
            _rotateIcon = LoadTexture("assets/sprites/editor/rotate.png");
            _scaleIcon = LoadTexture("assets/sprites/editor/scale.png");
            _lightOffIcon = LoadTexture("assets/sprites/editor/lightOff.png");
            _lightOnIcon = LoadTexture("assets/sprites/editor/lightOn.png");
            _skyboxOffIcon = LoadTexture("assets/sprites/editor/skyboxOff.png");
            _skyboxOnIcon = LoadTexture("assets/sprites/editor/skyboxOn.png");
            _axisOffIcon = LoadTexture("assets/sprites/editor/axisOff.png");
            _axisOnIcon = LoadTexture("assets/sprites/editor/axisOn.png");
            _gridOffIcon = LoadTexture("assets/sprites/editor/gridOff.png");
            _gridOnIcon = LoadTexture("assets/sprites/editor/gridOn.png");
            _physOptionsIcon = (int)Util.CreateMissingTexture(4);
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
                    Camera.Instance.RenderWidth = (int)ViewportSize.X;
                    Camera.Instance.RenderHeight = (int)ViewportSize.Y;
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
                var v = Camera.Instance.GetViewMatrix().ToBulletMatrix();
                var p = Camera.Instance.GetProjectionMatrix().ToBulletMatrix();
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

                var currentCamera = Camera.Instance;

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

                LineRenderer.Main.AddLine(pStart.ToOpenTkVector3(), pEnd.ToOpenTkVector3(), (0, 1, 0, 1), (0, 1, 0, 1), 5000);

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
                        Console.WriteLine(0);
                    }
                    else
                    {
                        _lastSelectedIndex = (_lastSelectedIndex + 1) % currentHitObjects.Count;
                        Console.WriteLine(_lastSelectedIndex);
                    }

                    if (currentHitObjects.Any())
                    {
                        var objectToSelect = currentHitObjects[_lastSelectedIndex];
                        SceneEditor.SelectedObject = objectToSelect;
                        Console.WriteLine(objectToSelect.Name);
                    }
                    else
                    {
                        SceneEditor.SelectedObject = null;
                        Console.WriteLine("null");
                        _lastSelectedIndex = -1;
                        _lastHitObjects.Clear();
                    }

                    _lastClickPosition = currentClickPosition;
                }
                else
                {
                    SceneEditor.SelectedObject = null;
                    Console.WriteLine("no hit, null");
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

            Camera.Instance.Position += offset;
        }
        private void DrawGrid()
        {
            //var color = new OpenTK.Mathematics.Vector4(0.25f, 0.25f, 0.25f, 0.1f);
            var color = new OpenTK.Mathematics.Vector4(0.75f, 0.75f, 0.75f, 0.3f);
            var size = 50;
            var camPos = (Vector3i)Camera.Instance.Position;

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
                $"Fov: {Camera.Instance.Fov}°",
                $"Scene: {SceneManager.CurrentScene.Name}",
                $"FPS: {1/Time.DeltaTime:F0}",
                $"Camera pos: {Camera.Instance.Position.X:F2} | {Camera.Instance.Position.Y:F2} | {Camera.Instance.Position.Z:F2}",
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
            var imTextureId1 = new ImTextureID((ulong)_translateIcon);
            var imTextureId2 = new ImTextureID((ulong)_rotateIcon);
            var imTextureId3 = new ImTextureID((ulong)_scaleIcon);
            var imTextureId4 = new ImTextureID((ulong)_lightOffIcon);
            var imTextureId5 = new ImTextureID((ulong)_lightOnIcon);
            var imTextureId6 = new ImTextureID((ulong)_skyboxOffIcon);
            var imTextureId7 = new ImTextureID((ulong)_skyboxOnIcon);
            var imTextureId8 = new ImTextureID((ulong)_axisOffIcon);
            var imTextureId9 = new ImTextureID((ulong)_axisOnIcon);
            var imTextureId10 = new ImTextureID((ulong)_gridOffIcon);
            var imTextureId11 = new ImTextureID((ulong)_gridOnIcon);
            var imTextureId12 = new ImTextureID((ulong)_physOptionsIcon);

            const int size = 20;

            BeginDisabled(Operation == ImGuizmoOperation.Translate);
            if (ImageButton("##translate", new ImTextureRef() { TexID = imTextureId1 }, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Translate;
            EndDisabled();

            BeginDisabled(Operation == ImGuizmoOperation.Rotate);
            SameLine();
            if (ImageButton("##rotate", new ImTextureRef() { TexID = imTextureId2 }, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Rotate;
            EndDisabled();

            BeginDisabled(Operation == ImGuizmoOperation.Scale);
            SameLine();
            if (ImageButton("##scale", new ImTextureRef() { TexID = imTextureId3 }, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Scale;
            EndDisabled();

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

            SameLine(350);

            if (ImageButton("##light", new ImTextureRef() { TexID = SceneEditor.PreviewLight ? imTextureId5 : imTextureId4 }, new Vector2(size, size)))
                SceneEditor.PreviewLight = !SceneEditor.PreviewLight;
            SameLine();
            if (ImageButton("##skybox", new ImTextureRef() { TexID = SceneEditor.PreviewSkybox ? imTextureId7 : imTextureId6 }, new Vector2(size, size)))
                SceneEditor.PreviewSkybox = !SceneEditor.PreviewSkybox;
            SameLine();
            if (ImageButton("##axis", new ImTextureRef() { TexID = SceneEditor.ShowAxis ? imTextureId9 : imTextureId8 }, new Vector2(size, size)))
                SceneEditor.ShowAxis = !SceneEditor.ShowAxis;
            SameLine();
            if (ImageButton("##grid", new ImTextureRef() { TexID = SceneEditor.ShowGrid ? imTextureId11 : imTextureId10 }, new Vector2(size, size)))
                SceneEditor.ShowGrid = !SceneEditor.ShowGrid;
            SameLine();
            if (ImageButton("##phys", new ImTextureRef() { TexID = imTextureId12 }, new Vector2(size, size)))
                _showPhysOptions = true;
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

                BulletDebugDrawer.Mode = (DebugDrawModes)(((uint)(_showOverlay ? 0xffffffffff : 0)) &
                                                                              (
                                                                                  ((uint)DebugDrawModes.DrawAabb & (uint)(_showAabb ? 0xffffffffff : 0)) |
                                                                                  ((uint)DebugDrawModes.DrawWireframe & (uint)(_showWireframe ? 0xffffffffff : 0))
                                                                              ));
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
            var p = Camera.Instance.Position;

            var speed = 7f * Time.DeltaTime;
            var input = Game.Instance.KeyboardState;
            var mInput = Game.Instance.MouseState;
            if (mInput.ScrollDelta.Y < 0 && IsWindowHovered())
                p += -(Camera.Instance.Front).Normalized();
            if (mInput.ScrollDelta.Y > 0 && IsWindowHovered())
                p += (Camera.Instance.Front).Normalized();

            if (input.IsKeyDown(Keys.W))
                p += (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.S))
                p -= (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.A))
                p -= TkVector3.Normalize(TkVector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
            if (input.IsKeyDown(Keys.D))
                p += TkVector3.Normalize(TkVector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
            if (input.IsKeyDown(Keys.Space))
                p += TkVector3.UnitY * speed;
            if (input.IsKeyDown(Keys.LeftShift))
                p -= TkVector3.UnitY * speed;

            if (!GetIO().WantTextInput)
                Camera.Instance.Position = (p);
        }
        private int LoadTexture(string path)
        {
            var t = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, t);

            var pathToFace = path;

            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(pathToFace);
            var pixels = new byte[4 * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
                InternalFormat.Rgba,
                image.Width, image.Height, 0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return t;
        }
        private TkVector3 GetRightVector()
        {
            var right = TkVector3.Cross(WorldUp, Camera.Instance.Front);
            return TkVector3.Normalize(right);
        }
        private TkVector3 GetUpVector()
        {
            var right = GetRightVector();
            var up = TkVector3.Cross(Camera.Instance.Front, right);
            return TkVector3.Normalize(up);
        }
    }
}