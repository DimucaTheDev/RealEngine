using System.Text.Json.Nodes;
using BulletSharp;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Audio;
using RE.Core.Scripting;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Utils;
using Serilog;
using Camera = RE.Rendering.Camera;

namespace RE.Core.World.Components
{
    [ComponentInfo("World/Player", Description = "Handles player logic")]
    internal class PlayerComponent : Component, ISceneRenderer
    {
        private Camera _camera;
        private GameObject _playerGameObject;
        private bool _isCrouching = false;
        private float _standHeight = 1.75f;
        private float _crouchHeight = 0.4f;
        private float _crouchCameraOffset = 0.7f;
        private float _standCameraOffset = 1.45f;
        private float _cameraLerpSpeed = 10f;
        private float _jumpCd = 0;
        private float _currentCameraYOffset = 1.45f;
        private float _targetCameraYOffset = 1.45f;
        private bool wasGrounded;
        private float _soundCooldown;
        private GameObject? _holdedObject;

        float _bobAmount = 0.05f;
        float _bobSpeed = 10f;
        float _cameraLerpSpeed2 = 6f;

        float _bobTimer = 0f;
        float _bobBlend = 0f;
        float _currentCameraYOffset2 = 0f;

        SpriteRenderer _spriteSpawnpoint;

        public override void Start()
        {
            _camera = Camera.Instance;
            _playerGameObject = new GameObject(Owner)
            {
                DoNotShowInEditor = true,
                DoNotSave = true,
                Transform =
                {
                    Scale = new Vector3(0.75f, _standHeight, 0.75f)
                }
            };
            var rigidBodyComponent = new RigidBodyComponent();
            _playerGameObject.Components.Add(new CapsuleColliderComponent());
            _playerGameObject.Components.Add(rigidBodyComponent);

            SceneManager.CurrentScene.GameObjects.Add(_playerGameObject);
            rigidBodyComponent.RigidBody.AngularFactor = BulletSharp.Math.Vector3.Zero;
            rigidBodyComponent.RigidBody.ActivationState = ActivationState.DisableDeactivation;
            rigidBodyComponent.RigidBody.Gravity = new BulletSharp.Math.Vector3(0, -25f, 0);

            _spriteSpawnpoint = new SpriteRenderer(Vector3.Zero, "assets/sprites/editor/spawn.png", scale: 1);
        }

        public override void OnSceneLoading(Scene scene)
        {
            _playerGameObject.SetPosition(Owner.Transform.Position);
        }


        public override void Update(FrameEventArgs args)
        {
            var input = Game.Instance.KeyboardState;

            if (input.IsKeyPressed(Keys.GraveAccent))
            {
                if (ConsoleWindow.Instance!.IsVisible)
                {
                    ConsoleWindow.Instance!.IsVisible = false;
                    Game.Instance.CursorState = CursorState.Grabbed;
                }
                else
                {
                    ConsoleWindow.Instance!.IsVisible = true;
                    Game.Instance.CursorState = CursorState.Normal;
                    _camera.FirstMove = true;
                }
            }



            if (!((Game.Instance.CursorState != CursorState.Grabbed || ImGui.GetIO().WantCaptureMouse)))
            {
                if (SceneEditor.Enabled)
                {
                    var p = _camera.Position;

                    var speed = 7f * Time.DeltaTime;

                    if (input.IsKeyDown(Keys.W))
                        p += (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
                    if (input.IsKeyDown(Keys.S))
                        p -= (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
                    if (input.IsKeyDown(Keys.A))
                        p -= Vector3.Normalize(Vector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
                    if (input.IsKeyDown(Keys.D))
                        p += Vector3.Normalize(Vector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
                    if (input.IsKeyDown(Keys.Space))
                        p += Vector3.UnitY * speed;
                    if (input.IsKeyDown(Keys.LeftShift))
                        p -= Vector3.UnitY * speed;
                    if (input.IsKeyDown(Keys.Escape))
                    {
                        Camera.Instance.FirstMove = true;
                        Game.Instance.CursorState = CursorState.Normal;
                    }
                    _camera.Position = (p);
                    return;
                }

                var rbN = _playerGameObject.GetComponent<RigidBodyComponent>()?.RigidBody;
                if (rbN == null)
                    return;

                #region HL2-like Movement

                var rb = _playerGameObject.GetComponent<RigidBodyComponent>()?.RigidBody;
                if (rb == null)
                    return;

                // Направления
                Vector3 moveDir = Vector3.Zero;
                var camForward = (_camera.Front with { Y = 0 }).Normalized();
                var camRight = Vector3.Normalize(Vector3.Cross(camForward, _camera.Up));

                if (input.IsKeyDown(Keys.W))
                    moveDir += camForward;
                if (input.IsKeyDown(Keys.S))
                    moveDir -= camForward;
                if (input.IsKeyDown(Keys.D))
                    moveDir += camRight;
                if (input.IsKeyDown(Keys.A))
                    moveDir -= camRight;

                if (moveDir.LengthSquared > 1e-5f)
                    moveDir = moveDir.Normalized();

                // Параметры
                bool crouching = _isCrouching;
                bool sprinting = input.IsKeyDown(Keys.LeftShift);
                float maxSpeed = crouching ? 5f : (sprinting ? 13.5f : 10f);

                float accel = 120f;
                float friction = 8f;

                Vector3 currentVel = new((float)rb.LinearVelocity.X, 0, (float)rb.LinearVelocity.Z);
                Vector3 targetVel = moveDir * maxSpeed;

                // Ускорение
                if (moveDir.LengthSquared > 0.001f)
                {
                    Vector3 velDelta = targetVel - currentVel;

                    if (velDelta.LengthSquared > 1e-6f) // <--- фикс: проверка на 0
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


                // Прыжок и вертикальная скорость
                bool grounded = IsGrounded();
                bool justLanded = grounded && !wasGrounded;

                if (grounded && !wasGrounded)
                {
                    // обнуляем вертикальную скорость при касании земли
                    var lv = rb.LinearVelocity;
                    rb.LinearVelocity = new BulletSharp.Math.Vector3(currentVel.X, 0, currentVel.Z);
                }
                else
                {
                    rb.LinearVelocity = new BulletSharp.Math.Vector3(currentVel.X, rb.LinearVelocity.Y, currentVel.Z);
                }

                wasGrounded = grounded;

                if (input.IsKeyDown(Keys.Space) && grounded && _jumpCd <= 0)
                {
                    rb.ApplyCentralImpulse(9f * BulletSharp.Math.Vector3.UnitY);
                    _jumpCd = 0.3f; // можно сразу уменьшить до 0.1 для быстрого bunnyhop
                }

                if (_jumpCd > 0f)
                    _jumpCd -= Time.DeltaTime;

                var crouchKeyDown = input.IsKeyDown(Keys.LeftControl);
                if (crouchKeyDown != _isCrouching)
                {
                    float newHeight = crouchKeyDown ? _crouchHeight : _standHeight;

                    if (!_isCrouching || (_isCrouching && CanStandUp()))
                    {
                        _playerGameObject.Transform.Scale = new Vector3(0.75f, newHeight, 0.75f);
                        _isCrouching = crouchKeyDown;

                        var capsuleCollider = _playerGameObject.GetComponent<CapsuleColliderComponent>();
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

                if (input.IsKeyPressed(Keys.E))
                {
                    float rayLength = 5.5f;
                    BulletSharp.Math.Vector3 rayFrom = _camera.Position.ToBulletVector3();
                    BulletSharp.Math.Vector3 rayTo = (_camera.Position + _camera.Front * rayLength).ToBulletVector3();

                    var callback = new ClosestRayResultCallback(ref rayFrom, ref rayTo);

                    PhysicsManager.DynamicsWorld.RayTest(rayFrom, rayTo, callback);

                    bool wasHolding = _holdedObject != null;

                    if (_holdedObject != null!)
                    {
                        var rigidBody = _holdedObject.GetComponent<RigidBodyComponent>()?.RigidBody
                                        ?? _holdedObject.GetComponent<ColliderComponent>()?.RigidBody;
                        if (rigidBody != null)
                            rigidBody.Activate(true);
                        _holdedObject = null;
                    }

                    if (callback.HasHit)
                    {
                        Log.Debug($"Ray hit object at distance: {callback.ClosestHitFraction * rayLength}");
                        Log.Debug($"Hit object's UserObject type: {callback.CollisionObject.UserObject?.GetType().Name ?? "null"}");

                        if (!wasHolding && callback.CollisionObject.UserObject is Component controller)
                        {
                            if (controller.GetComponent<UsableComponent>() != null!)
                            {
                                controller.GetComponent<UsableComponent>()!.OnUsed?.Invoke();
                                SoundManager.Play("buttons/blip", new SoundPlaybackSettings()
                                {
                                    InWorld = false,
                                    Pitch = 1,
                                    Volume = .25f
                                });
                            }
                            else if (controller.GetComponent<RigidBodyComponent>() != null)
                            {
                                _holdedObject = controller.Owner;
                                var rigidBody = _holdedObject!.GetComponent<RigidBodyComponent>()?.RigidBody
                                                ?? _holdedObject.GetComponent<ColliderComponent>()?.RigidBody;
                                if (rigidBody != null)
                                    rigidBody.ActivationState = ActivationState.DisableDeactivation;
                            }
                            else
                            {
                                SoundManager.Play("common/wpn_denyselect", new SoundPlaybackSettings()
                                {
                                    InWorld = false,
                                    Pitch = 1,
                                    Volume = .25f
                                });
                            }
                        }
                        else
                        {
                            SoundManager.Play("common/wpn_denyselect", new SoundPlaybackSettings()
                            {
                                InWorld = false,
                                Pitch = 1,
                                Volume = .25f
                            });
                            Log.Debug("Ray hit an object, but its UserObject is not a Controller.");
                        }
                    }
                    else
                    {
                        SoundManager.Play("common/wpn_denyselect", new SoundPlaybackSettings()
                        {
                            InWorld = false,
                            Pitch = 1,
                            Volume = .25f
                        });
                    }
                }


                if (input.IsKeyPressed(Keys.F11))
                    Game.Instance.ToggleFullscreen();
                if (input.IsKeyPressed(Keys.F1))
                {
                    RenderManager.RemoveCameraFrustum();
                    if (input.IsKeyDown(Keys.LeftControl))
                    {
                        return;
                    }

                    RenderManager.CreateCameraFrustum();
                }

                if (input.IsKeyPressed(Keys.F2))
                {
                    var p = Game.TakeScreenshot();
                    Log.Information($"Screenshot saved to {p}");
                }

                if (input.IsKeyDown(Keys.Escape))
                {
                    Game.Instance.CursorState = CursorState.Normal;
                    _camera.FirstMove = true;
                }

                // UpdateCameraPosition((float)args.Time, currentHorizontalVelocity.LengthSquared > 1e-6, sprintKeyDown, _isCrouching);

            }
            _targetCameraYOffset = _isCrouching ? _crouchCameraOffset : _standCameraOffset;
            _currentCameraYOffset = MathHelper.Lerp(_currentCameraYOffset, _targetCameraYOffset,
                (float)(Time.DeltaTime * _cameraLerpSpeed));


            if (_holdedObject != null)
            {
                const float smoothSpeed = 10f;
                var targetPos = _camera.Position + _camera.Front * 4;

                var rigidBodyComponent = _holdedObject.GetComponent<RigidBodyComponent>();
                var rigidBody = rigidBodyComponent?.RigidBody;

                if (rigidBody != null)
                {
                    var mass = rigidBodyComponent.Mass;

                    Vector3 currentPos = _holdedObject.Transform.Position;
                    Vector3 direction = targetPos - currentPos;

                    float speedFactor = 1f / MathF.Max(mass, 0.01f);
                    float effectiveSpeed = smoothSpeed * speedFactor;

                    Vector3 velocity = direction * effectiveSpeed;
                    rigidBody.LinearVelocity = velocity.ToBulletVector3();


                    var currentOrientation = rigidBody.Orientation;
                    var targetOrientation = BulletSharp.Math.Quaternion.Identity;

                    float rotationSmoothFactor = 5f * Time.DeltaTime;

                    var newOrientation = BulletSharp.Math.Quaternion.Slerp(currentOrientation, targetOrientation, rotationSmoothFactor);

                    _holdedObject.SetRotation(newOrientation.ToOpenTkQuaternion());

                    rigidBody.AngularVelocity *= 0.8f;

                    rigidBody.ActivationState = ActivationState.ActiveTag;

                    if (Game.Instance.MouseState.IsButtonPressed(MouseButton.Button1))
                    {
                        rigidBody.ApplyImpulse(_camera.Front.ToBulletVector3() * 7, BulletSharp.Math.Vector3.Zero);
                        _holdedObject = null;
                    }
                }
            }


            if (!SceneEditor.Enabled)
            {
                var basePosition = _playerGameObject.Transform.Position;
                _camera.Position = new Vector3(basePosition.X, basePosition.Y + _currentCameraYOffset, basePosition.Z);
            }
        }
        bool IsGrounded(Vector3 rel)
        {
            float currentHeight = _isCrouching ? _crouchHeight : _standHeight;
            float radius = 0.75f;
            float fullHeight = currentHeight + 2 * radius;

            Vector3 pos = _playerGameObject.Transform.Position + rel;
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
            var from = _playerGameObject.Transform.Position + new Vector3(0, _crouchHeight / 2f, 0) + rel;
            var to = from + new Vector3(0, _standHeight - _crouchHeight, 0) + rel;

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

        void UpdateCameraPosition2(float deltaTime, bool isWalking, bool isSprinting, bool isCrouching)
        {
            float targetYOffset = isCrouching ? _crouchCameraOffset : _standCameraOffset;
            _currentCameraYOffset = MathHelper.Lerp(_currentCameraYOffset, targetYOffset, deltaTime * _cameraLerpSpeed);

            if (isWalking)
                _bobTimer += deltaTime * _bobSpeed * (isSprinting ? 2f : 1f);
            else
                _bobTimer = 0f;

            float bobAmount = _bobAmount * (isCrouching ? 0.5f : 1f);
            float bobOffsetY = MathF.Sin(_bobTimer) * bobAmount * 2;
            float bobOffsetX = MathF.Sin(_bobTimer * 0.5f) * bobAmount * 2f;

            Vector3 playerPos = _playerGameObject.Transform.Position;
            _camera.Position = new Vector3(
                playerPos.X + bobOffsetX,
                playerPos.Y + _currentCameraYOffset + bobOffsetY,
                playerPos.Z
            );
        }
        void UpdateCameraPosition(float deltaTime, bool isWalking, bool isSprinting, bool isCrouching)
        {
            float targetYOffset = isCrouching ? _crouchCameraOffset : _standCameraOffset;
            _currentCameraYOffset2 = MathHelper.Lerp(_currentCameraYOffset2, targetYOffset, deltaTime * _cameraLerpSpeed2);

            float walkSpeedFactor = isSprinting ? 2f : 1f;
            _bobTimer += deltaTime * _bobSpeed * walkSpeedFactor;

            float targetBlend = isWalking ? 1f : 0f;
            _bobBlend = MathHelper.Lerp(_bobBlend, targetBlend, deltaTime * 5f);

            float bobAmount = _bobAmount * (isCrouching ? 0.5f : 1f);
            float bobOffsetY = MathF.Sin(_bobTimer * 1f) * bobAmount * _bobBlend;
            float bobOffsetX = MathF.Sin(_bobTimer * 0.5f) * bobAmount * 0.5f * _bobBlend;

            Vector3 playerPos = _playerGameObject.Transform.Position;
            _camera.Position = new Vector3(
                playerPos.X + bobOffsetX,
                playerPos.Y + _currentCameraYOffset2 + bobOffsetY,
                playerPos.Z
            );
        }

        public void DebugRender(FrameEventArgs args)
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
