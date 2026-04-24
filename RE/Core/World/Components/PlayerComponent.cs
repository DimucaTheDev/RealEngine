using System.Text.Json.Nodes;
using BulletSharp;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core.Audio;
using RE.Core.Input;
using RE.Core.Scripting.Attributes;
using RE.Core.Ui;
using RE.Core.Ui.Controls;
using RE.Core.World.Components.Physics;
using RE.Core.World.Physics;
using RE.Editor;
using RE.Rendering.Renderables;
using RE.Rendering.Texturing;
using RE.Utils;
using Serilog;
using Camera = RE.Rendering.Camera;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Quaternion = BulletSharp.Math.Quaternion;

namespace RE.Core.World.Components
{
    [ComponentInfo("World/Player", Description = "Handles player logic")]
    internal class PlayerComponent : Component, IEditorRender
    {
        private const float MouseSensitivity = 0.2f;
        private Camera _camera = Camera.Main;
        protected GameObject PlayerGameObject = null!;
        private bool _isCrouching;
        private const float StandHeight = 1.75f;
        private const float CrouchHeight = 0.4f;
        private const float CrouchCameraOffset = 0.7f;
        private const float StandCameraOffset = 1.45f;
        private const float CameraLerpSpeed = 10f;
        private float _jumpCd;
        private float _currentCameraYOffset = 1.45f;
        private float _targetCameraYOffset = 1.45f;
        private bool _wasGrounded;
        private GameObject? _holdingObject;
        private float _objMass;


        private SpriteRenderer _spriteSpawnpoint = new(Vector3.Zero, "assets/editor/sprites/spawn.png", scale: 1);

        private float _soundCooldown;

        [EditorProperty] public string InteractDenySound { get; set; } = "event:/DenySelect";

        public override void Start()
        {
            PlayerGameObject = new GameObject(Owner)
            {
                DoNotShowInEditor = true,
                DoNotSave = true,
                Transform =
                {
                    Scale = new Vector3(0.75f, StandHeight, 0.75f)
                }
            };
            PlayerGameObject.SetPosition(Owner.Transform.Position);
            var rigidBodyComponent = new RigidBodyComponent();
            PlayerGameObject.Components.Add(new CapsuleColliderComponent());
            PlayerGameObject.Components.Add(rigidBodyComponent);

            SceneManager.CurrentScene.GameObjects.Add(PlayerGameObject);
            rigidBodyComponent.RigidBody.AngularFactor = BulletSharp.Math.Vector3.Zero;
            rigidBodyComponent.RigidBody.ActivationState = ActivationState.DisableDeactivation;
            rigidBodyComponent.RigidBody.Gravity = new BulletSharp.Math.Vector3(0, -25f, 0);

            Mouse.CursorState = CursorState.Grabbed;
            FirstMove = true;

            path.AddRange(
                new(0, 5, 0.0f),
                new(0, 5, 0.0f),
                new(0, 5, 0.0f));

            im = new ImageControl(new ImageRenderer(StaticTexture.CreateMonoColorTexture((.1f, 0.1f, 0.1f, 1)), new Vector2(0, 0), (3000, 3000)));
            Hud.Root.Children.AddRange(im, new ImageControl("assets/testing/alpha.png")
            {
                ZIndex = 1
            }, new LabelControl { ZIndex = 2, Text = "hud test", Position = (0,200)});
        }

        private ImageControl im;
        private float d;

        /// <inheritdoc />
        public override bool IsOpaque => false;

        /// <inheritdoc />
        public override void Render(FrameEventArgs args)
        {
        }

        private void CameraControl()
        {
            float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
            {
                float t = (value - fromMin) / (fromMax - fromMin);
                return toMin + t * (toMax - toMin);
            }

            if (d <= 0.1)
            {
                d += Time.DeltaTime / 3;
            }
            else if (d <= 1)
            {
                d += Time.DeltaTime / 3;
                var color = Vector4.Lerp(
                    new Vector4(.1f, 0.1f, 0.1f, 1),
                    new Vector4(1, 1, 1, 0),
                    Remap(d, 0.1f, 1, 0, 1));

                im.ImageRenderer.ReplaceImage(StaticTexture.CreateMonoColorTexture(color), true);
            }
            else
            {
                Hud.Root.Children.Remove(im);
            }


            Camera cam = _camera;

            if (Mouse.IsButtonPressed(MouseButton.Left))
                Mouse.CursorState = CursorState.Grabbed;
            if (Keyboard.IsKeyPressed(Keys.Escape))
            {
                Mouse.CursorState = CursorState.Normal;
                FirstMove = true;
                return;
            }

            if (Mouse.CursorState != CursorState.Grabbed)
                return;

            var deltaX = Mouse.Delta.X;
            var deltaY = -Mouse.Delta.Y;

            if (FirstMove)
            {
                FirstMove = false;
                return;
            }

            if (Mouse.IsButtonPressed(MouseButton.Right))
                AddDummyCube();

            cam.Yaw += deltaX * MouseSensitivity;
            cam.Pitch += deltaY * MouseSensitivity;

            cam.Pitch = MathHelper.Clamp(cam.Pitch, -89f, 89f);

            Vector3 front;
            front.X = MathF.Cos(MathHelper.DegreesToRadians(cam.Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(cam.Pitch));
            front.Y = MathF.Sin(MathHelper.DegreesToRadians(cam.Pitch));
            front.Z = MathF.Sin(MathHelper.DegreesToRadians(cam.Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(cam.Pitch));
            cam.Front = Vector3.Normalize(front);
        }

        private List<Vector3> path =
        [
            new(-30, 0.0f, 63),
            new(-20, 8.0f, 50),
            new(0, 9.0f, 0),
        ];

        private bool BadassTransition()
        {
            int maxCurve = (path.Count - 1) / 3;
            if (curveIndex < maxCurve)
            {
                UpdateCamera(Time.DeltaTime);
                Camera.Main.LookAt((0, 5.75f, -4));
                return true;
            }
            return false;
        }

        public override void Update(FrameEventArgs args)
        {
            CameraControl();

            if (BadassTransition())
            { }

            #region HL2-like Movement

            var rb = PlayerGameObject.GetComponent<RigidBodyComponent>()?.RigidBody;
            if (rb == null)
                return;

            Vector3 moveDir = Vector3.Zero;
            var camForward = (_camera.Front with { Y = 0 }).Normalized();
            var camRight = Vector3.Normalize(Vector3.Cross(camForward, _camera.Up));

            if (Keyboard.IsKeyDown(Keys.W))
                moveDir += camForward;
            if (Keyboard.IsKeyDown(Keys.S))
                moveDir -= camForward;
            if (Keyboard.IsKeyDown(Keys.D))
                moveDir += camRight;
            if (Keyboard.IsKeyDown(Keys.A))
                moveDir -= camRight;

            _soundCooldown += (float)args.Time;

            if ((Keyboard.IsKeyDown(Keys.W) || Keyboard.IsKeyDown(Keys.S) || Keyboard.IsKeyDown(Keys.D) ||
                 Keyboard.IsKeyDown(Keys.A)) && !_isCrouching && _wasGrounded && moveDir.Length > 0.75f)
            {

                if ((_soundCooldown >= 0.45f && !Keyboard.Shift) || (_soundCooldown >= 0.3f && Keyboard.Shift))
                {
                    SoundManager.PlayOneShotEvent("event:/Step");
                    _soundCooldown = 0f;
                }
            }


            if (moveDir.LengthSquared > 1e-5f)
                moveDir = moveDir.Normalized();

            bool crouching = _isCrouching;
            bool sprinting = Keyboard.IsKeyDown(Keys.LeftShift);
            float maxSpeed = crouching ? 5f : (sprinting ? 13.5f : 10f);

            float accel = 120f;
            float friction = 8f;

            Vector3 currentVel = new(rb.LinearVelocity.X, 0, rb.LinearVelocity.Z);
            Vector3 targetVel = moveDir * maxSpeed;

            if (moveDir.LengthSquared > 0.001f)
            {
                Vector3 velDelta = targetVel - currentVel;

                if (velDelta.LengthSquared > 1e-6f)
                {
                    Vector3 accelStep = velDelta.Normalized() * accel * Time.DeltaTime;

                    if (accelStep.LengthSquared > velDelta.LengthSquared)
                        accelStep = velDelta;

                    currentVel += accelStep;
                }
            }
            else
            {
                float speed = currentVel.Length;
                if (speed > 0)
                {
                    float drop = speed * friction * Time.DeltaTime;
                    float newSpeed = MathF.Max(0, speed - drop);

                    if (newSpeed > 0)
                        currentVel = currentVel.Normalized() * newSpeed;
                    else
                        currentVel = Vector3.Zero;
                }
            }


            bool grounded = IsGrounded();
            bool justLanded = grounded && !_wasGrounded;

            if (grounded && !_wasGrounded)
            {
                var lv = rb.LinearVelocity;
                rb.LinearVelocity = new BulletSharp.Math.Vector3(currentVel.X, 0, currentVel.Z);
            }
            else
            {
                rb.LinearVelocity = new BulletSharp.Math.Vector3(currentVel.X, rb.LinearVelocity.Y, currentVel.Z);
            }

            _wasGrounded = grounded;

            if (Keyboard.IsKeyDown(Keys.Space) && grounded && _jumpCd <= 0)
            {
                rb.ApplyCentralImpulse(9f * BulletSharp.Math.Vector3.UnitY);
                _jumpCd = 0.3f;
            }

            if (_jumpCd > 0f)
                _jumpCd -= Time.DeltaTime;

            var crouchKeyDown = Keyboard.IsKeyDown(Keys.LeftControl);
            if (crouchKeyDown != _isCrouching)
            {
                float newHeight = crouchKeyDown ? CrouchHeight : StandHeight;

                if (!_isCrouching || (_isCrouching && CanStandUp()))
                {
                    PlayerGameObject.Transform.Scale = new Vector3(0.75f, newHeight, 0.75f);
                    _isCrouching = crouchKeyDown;

                    var capsuleCollider = PlayerGameObject.GetComponent<CapsuleColliderComponent>();
                    if (capsuleCollider != null)
                    {
                        var transform = rb.WorldTransform;

                        rb.CollisionShape = capsuleCollider.CreateCollisionShape();
                        rb.WorldTransform = transform;
                        rb.MotionState?.SetWorldTransform(ref transform);

                        var rbGravity = rb.Gravity;
                        rb.Disable();
                        rb.Enable();
                        rb.Gravity = rbGravity;
                    }
                }
            }

            #endregion

            if (Keyboard.IsKeyPressed(Keys.E))
            {
                float rayLength = 5.5f;
                BulletSharp.Math.Vector3 rayFrom = _camera.Position.ToBulletVector3();
                BulletSharp.Math.Vector3 rayTo = (_camera.Position + _camera.Front * rayLength).ToBulletVector3();

                var callback = new ClosestRayResultCallback(ref rayFrom, ref rayTo);

                PhysicsManager.DynamicsWorld.RayTest(rayFrom, rayTo, callback);

                bool wasHolding = _holdingObject != null;

                if (_holdingObject != null!)
                {
                    var rigidBody = _holdingObject.GetComponent<RigidBodyComponent>()?.RigidBody
                                    ?? _holdingObject.GetComponent<ColliderComponent>()?.RigidBody;
                    if (_holdingObject.GetComponent<RigidBodyComponent>() != null)
                        _holdingObject.GetComponent<RigidBodyComponent>()!.Mass = _objMass;
                    if (rigidBody != null)
                        rigidBody.Activate(true);
                    _holdingObject = null;
                }

                if (callback.HasHit)
                {
                    if (!wasHolding && callback.CollisionObject.UserObject is Component controller)
                    {
                        if (controller.GetComponent<UsableComponent>() != null!)
                        {
                            controller.GetComponent<UsableComponent>()!.OnUsed?.Invoke();
                            SoundManager.PlayOneShotEvent("event:/Blip");
                        }
                        else if (controller.GetComponent<RigidBodyComponent>() != null)
                        {
                            _holdingObject = controller.Owner;
                            SoundManager.PlayOneShotEvent("event:/Select");
                            if (_holdingObject.GetComponent<RigidBodyComponent>() != null)
                            {
                                var rigidBodyComponent = _holdingObject.GetComponent<RigidBodyComponent>();
                                _objMass = rigidBodyComponent!.Mass;
                                rigidBodyComponent.Mass = 1;
                            }

                            var rigidBody = _holdingObject!.GetComponent<RigidBodyComponent>()?.RigidBody
                                            ?? _holdingObject.GetComponent<ColliderComponent>()?.RigidBody;
                            if (rigidBody != null)
                                rigidBody.ActivationState = ActivationState.DisableDeactivation;
                        }
                        else if (_holdingObject == null)
                        {
                            if (!string.IsNullOrWhiteSpace(InteractDenySound))
                                SoundManager.PlayOneShotEvent(InteractDenySound);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(InteractDenySound))
                            //   SoundManager.PlayOneShotEvent(InteractDenySound);
                            Log.Debug("Ray hit an object, but its UserObject is not a Controller.");
                    }
                }
                else if (!wasHolding)
                {
                    if (!string.IsNullOrWhiteSpace(InteractDenySound))
                        SoundManager.PlayOneShotEvent(InteractDenySound);
                }
            }

            _targetCameraYOffset = _isCrouching ? CrouchCameraOffset : StandCameraOffset;
            _currentCameraYOffset = MathHelper.Lerp(_currentCameraYOffset, _targetCameraYOffset,
                Time.DeltaTime * CameraLerpSpeed);


            if (_holdingObject != null)
            {
                var rigidBodyComponent = _holdingObject.GetComponent<RigidBodyComponent>();
                var rigidBody = rigidBodyComponent?.RigidBody;

                if (rigidBody != null)
                {
                    var mass = rigidBodyComponent.Mass;
                    Vector3 currentPos = _holdingObject.Transform.Position;
                    Vector3 rawTargetPos = _camera.Position + _camera.Front * 4;
                    _smoothedTargetPos = Vector3.Lerp(_smoothedTargetPos, rawTargetPos, 0.2f);

                    Vector3 direction = _smoothedTargetPos - currentPos;
                    Vector3 currentVelocity = rigidBody.LinearVelocity.ToOpenTkVector3();

                    float stiffness = 800f;
                    float damping = 80f;

                    Vector3 force = (direction * stiffness - currentVelocity * damping);

                    float maxForce = 100f * mass;
                    if (force.Length > maxForce)
                        force = Vector3.Normalize(force) * maxForce;

                    rigidBody.ApplyCentralForce(force.ToBulletVector3());

                    var currentOrientation = rigidBody.Orientation;
                    var targetOrientation = Quaternion.Identity;

                    float rotationSmoothFactor = 5f * Time.DeltaTime;

                    var newOrientation = Quaternion.Slerp(currentOrientation, targetOrientation, rotationSmoothFactor);

                    _holdingObject.SetRotation(newOrientation.ToOpenTkQuaternion());

                    rigidBody.AngularVelocity *= 0.8f;

                    rigidBody.ActivationState = ActivationState.ActiveTag;

                    if (false && Game.Instance.MouseState.IsButtonPressed(MouseButton.Button1))
                    {
                        if (_holdingObject.GetComponent<RigidBodyComponent>() != null)
                            _holdingObject.GetComponent<RigidBodyComponent>().Mass = _objMass;
                        rigidBody.ApplyImpulse(_camera.Front.ToBulletVector3() * 7, BulletSharp.Math.Vector3.Zero);
                        _holdingObject = null;
                    }
                }
            }

            var basePosition = PlayerGameObject.Transform.Position;
            _camera.Position = new Vector3(basePosition.X, basePosition.Y + _currentCameraYOffset, basePosition.Z)
                + CameraBob(currentVel, (float)args.Time, grounded);

            if (rb.LinearVelocity.Y < -5 && !_falling)
            {
                StartFall();
            }
            else if (rb.LinearVelocity.Y < 0 && _falling)
            {
                _fall.SetParameter("DeltaY", -rb.LinearVelocity.Y);
                _prevDeltaY = rb.LinearVelocity.Y;
            }
            else if (rb.LinearVelocity.Y >= 0 && _falling)
            {
                _fall.Dispose();
                _falling = false;
                if (_prevDeltaY <= -17)
                    SoundManager.PlayOneShotEvent("event:/HardFall");
            }
        }

        void StartFall()
        {
            _falling = true;
            _fall = SoundManager.PlayEvent("event:/FallLoop");
        }

        private bool _falling;
        private Sound _fall;
        private float _prevDeltaY;

        private float bobTime = 0f;
        private float lastBob = 0f;
        private float lastSide = 0f;

        public static bool cl_viewbob_enabled = true;
        public static float cl_viewbob_timer = 7f;
        public static float cl_viewbob_scale = .8f;


        public Vector3 CameraBob(Vector3 velocity, float deltaTime, bool onGround)
        {
            if (!cl_viewbob_enabled)
                return Vector3.Zero;

            float speed = new Vector2(velocity.X, velocity.Z).Length;

            if (speed < 0.01f)
                return Vector3.Zero;

            float t = Time.ElapsedTime * cl_viewbob_timer;

            float xOffset =
                MathF.Sin(t) *
                speed *
                cl_viewbob_scale / 100f;

            float yOffset =
                MathF.Sin(2f * t) *
                speed *
                cl_viewbob_scale / 400f;

            return new Vector3(xOffset, yOffset, 0f);
        }



        int curveIndex;
        float t;
        float speed = 0.25f;
        bool loop = false;
        private bool ended = false;
        Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;

            return
                uu * u * p0 +
                3f * uu * t * p1 +
                3f * u * tt * p2 +
                tt * t * p3;
        }
        void UpdateCamera(float deltaTime)
        {
            if (path == null || path.Count < 4)
                return;

            int maxCurve = (path.Count - 1) / 3;

            if (curveIndex >= maxCurve)
            {
                if (loop)
                {
                    curveIndex = 0;
                    t = 0f;
                }
                else
                {
                    PlayerGameObject.SetPosition(path[^1], true);
                    return;
                }
            }

            int i = curveIndex * 3;

            Vector3 p0 = path[i];
            Vector3 p1 = path[i + 1];
            Vector3 p2 = path[i + 2];
            Vector3 p3 = path[i + 3];

            t += speed * deltaTime;

            if (t >= 1f)
            {
                t = 0f;
                curveIndex++;
                return;
            }

            PlayerGameObject.SetPosition(Bezier(p0, p1, p2, p3, t), true);
        }

        private static void AddDummyCube()
        {
            GameObject obj = new GameObject();
            obj.Components.Add(new MeshComponent("assets/models/crate.fbx"));
            Vector3 front = Camera.Main.Front;

            obj.Components.Add(new BoxColliderComponent());
            var rb = new RigidBodyComponent(20);
            obj.Components.Add(rb);

            SceneManager.CurrentScene.GameObjects.Add(obj);

            BulletSharp.Math.Vector3 cameraFrontBullet = new BulletSharp.Math.Vector3(front.X, front.Y, front.Z);
            rb.RigidBody.Restitution = 0.2f;
            rb.RigidBody.Friction = 1;
            float impulseStrength = 5.0f;
            BulletSharp.Math.Vector3 impulseVector = cameraFrontBullet * impulseStrength;
            rb.RigidBody.ApplyImpulse(impulseVector, BulletSharp.Math.Vector3.Zero);

            obj.SetPosition(2 * front + Camera.Main.Position);
        }

        private Vector3 _smoothedTargetPos = Vector3.Zero;
        private bool FirstMove = true;
        private float mouseX;
        private float mouseY;

        bool IsGrounded(Vector3 rel)
        {
            float currentHeight = _isCrouching ? CrouchHeight : StandHeight;
            float radius = 0.75f;
            float fullHeight = currentHeight + 2 * radius;

            Vector3 pos = PlayerGameObject.Transform.Position + rel;
            Vector3 from = pos - new Vector3(0, fullHeight / 2.0f, 0) + (0, 0.2f, 0);
            Vector3 to = from - new Vector3(0, 0.2f, 0);


            var rayFrom = from.ToBulletVector3();
            var rayTo = to.ToBulletVector3();
            var callback = new ClosestRayResultCallback(ref rayFrom, ref rayTo);
            PhysicsManager.DynamicsWorld.RayTest(rayFrom, rayTo, callback);

            return callback.HasHit;
        }

        bool IsGrounded()
        {
            return IsGrounded((0.5f, 0, 0)) || IsGrounded((0, 0, 0.5f)) || IsGrounded((-0.5f, 0, 0)) ||
                   IsGrounded((0, 0, -0.5f)) || IsGrounded((0.5f, 0, 0.5f)) || IsGrounded((-0.5f, 0, 0.5f)) ||
                   IsGrounded((-0.5f, 0, -0.5f)) || IsGrounded((0.5f, 0, -0.5f));
        }

        bool CanStandUp(Vector3 rel)
        {
            var from = PlayerGameObject.Transform.Position + new Vector3(0, CrouchHeight / 2f, 0) + rel;
            var to = from + new Vector3(0, StandHeight - CrouchHeight, 0) + rel;

            var rayFrom = from.ToBulletVector3();
            var rayTo = to.ToBulletVector3();

            var callback = new ClosestRayResultCallback(ref rayFrom, ref rayTo);
            PhysicsManager.DynamicsWorld.RayTest(rayFrom, rayTo, callback);

            return !callback.HasHit;
        }
        bool CanStandUp()
        {
            var offset = 0.3f;
            return CanStandUp((offset, 0, 0)) && CanStandUp((0, 0, offset)) && CanStandUp((-offset, 0, 0)) &&
                   CanStandUp((0, 0, -offset)) && CanStandUp((offset, 0, offset)) && CanStandUp((-offset, 0, offset)) &&
                   CanStandUp((-offset, 0, -offset)) && CanStandUp((offset, 0, -offset));
        }
        public void EditorRender(FrameEventArgs args)
        {
            _spriteSpawnpoint.Position = Owner.Transform.Position;

            _spriteSpawnpoint.Render(args);
        }
        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            return root;
        }
    }
}
