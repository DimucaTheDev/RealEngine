using System.Text.Json.Nodes;
using BulletSharp;
using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Audio;
using RE.Core.Scripting;
using RE.Core.World.Components.Physics;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Rendering.Renderables;
using RE.Utils;
using Serilog;
using Camera = RE.Rendering.Camera;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace RE.Core.World.Components
{
    [ComponentInfo("World/Player", Description = "Handles player logic")]
    internal class PlayerComponent : Component, IDebugRenderer
    {
        private Camera _camera = Camera.Instance;
        protected GameObject PlayerGameObject = null!;
        private bool _isCrouching = false;
        private const float StandHeight = 1.75f;
        private const float CrouchHeight = 0.4f;
        private const float CrouchCameraOffset = 0.7f;
        private const float StandCameraOffset = 1.45f;
        private const float CameraLerpSpeed = 10f;
        private float _jumpCd = 0;
        private float _currentCameraYOffset = 1.45f;
        private float _targetCameraYOffset = 1.45f;
        private bool _wasGrounded;
        private GameObject? _holdingObject;
        private float _objMass;

        private const float BobAmount = 0.05f;
        private const float BobSpeed = 10f;
        private const float CameraLerpSpeed2 = 6f;

        private float _bobTimer = 0f;
        private float _bobBlend = 0f;
        private float _currentCameraYOffset2 = 0f;

        private SpriteRenderer _spriteSpawnpoint = new SpriteRenderer(Vector3.Zero, "assets/sprites/editor/spawn.png", scale: 1);

        private float _soundCooldown = 0f;

        [EditorProperty] public string InteractDenySound { get; set; } = "common/wpn_denyselect";

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

        }

        public override void Update(FrameEventArgs args)
        {
            var input = Game.Instance.KeyboardState;


            if (!((Game.Instance.CursorState != CursorState.Grabbed || ImGui.GetIO().WantCaptureMouse)))
            {
                if (SceneEditor.Enabled)
                {

                    return;
                }

                var rbN = PlayerGameObject.GetComponent<RigidBodyComponent>()?.RigidBody;
                if (rbN == null)
                    return;

                #region HL2-like Movement

                var rb = PlayerGameObject.GetComponent<RigidBodyComponent>()?.RigidBody;
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

                _soundCooldown += (float)args.Time;

                if ((input.IsKeyDown(Keys.W) || input.IsKeyDown(Keys.S) || input.IsKeyDown(Keys.D) ||
                     input.IsKeyDown(Keys.A)) && !_isCrouching && _wasGrounded)
                {
                    if (_soundCooldown >= 0.45f)
                    {
                        SoundManager.Play("hardboot_generic", new SoundPlaybackSettings()
                        {
                            //  InWorld = true,
                            Position = PlayerGameObject.Transform.Position,
                            ShowDebugInfo = true,
                            Volume = .3f
                        });
                        _soundCooldown = 0f;
                    }
                }


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
                bool justLanded = grounded && !_wasGrounded;

                if (grounded && !_wasGrounded)
                {
                    // обнуляем вертикальную скорость при касании земли
                    var lv = rb.LinearVelocity;
                    rb.LinearVelocity = new BulletSharp.Math.Vector3(currentVel.X, 0, currentVel.Z);
                }
                else
                {
                    rb.LinearVelocity = new BulletSharp.Math.Vector3(currentVel.X, rb.LinearVelocity.Y, currentVel.Z);
                }

                _wasGrounded = grounded;

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

                if (input.IsKeyPressed(Keys.E))
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
                        // Log.Debug($"Ray hit object at distance: {callback.ClosestHitFraction * rayLength}");
                        // Log.Debug($"Hit object's UserObject type: {callback.CollisionObject.UserObject?.GetType().Name ?? "null"}");

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
                                _holdingObject = controller.Owner;
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
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(InteractDenySound))
                                    SoundManager.Play(InteractDenySound, new SoundPlaybackSettings()
                                    {
                                        InWorld = false,
                                        Pitch = 1,
                                        Volume = .25f
                                    });
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(InteractDenySound))
                                SoundManager.Play(InteractDenySound, new SoundPlaybackSettings()
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
                        if (!string.IsNullOrWhiteSpace(InteractDenySound))
                            SoundManager.Play(InteractDenySound, new SoundPlaybackSettings()
                            {
                                InWorld = false,
                                Pitch = 1,
                                Volume = .25f
                            });
                    }
                }




                if (input.IsKeyDown(Keys.Escape))
                {
                    Game.Instance.CursorState = CursorState.Normal;
                    _camera.FirstMove = true;
                }

                // UpdateCameraPosition((float)args.Time, currentHorizontalVelocity.LengthSquared > 1e-6, sprintKeyDown, _isCrouching);

            }
            _targetCameraYOffset = _isCrouching ? CrouchCameraOffset : StandCameraOffset;
            _currentCameraYOffset = MathHelper.Lerp(_currentCameraYOffset, _targetCameraYOffset,
                (float)(Time.DeltaTime * CameraLerpSpeed));


            if (_holdingObject != null)
            {
                var rigidBodyComponent = _holdingObject.GetComponent<RigidBodyComponent>();
                var rigidBody = rigidBodyComponent?.RigidBody;

                if (rigidBody != null)
                {
                    var mass = rigidBodyComponent.Mass;
                    Vector3 currentPos = _holdingObject.Transform.Position; // Gets the current position of the object being held.
                    Vector3 rawTargetPos = _camera.Position + _camera.Front * 4; // Calculates a raw target position 4 units in front of the camera.
                    _smoothedTargetPos = Vector3.Lerp(_smoothedTargetPos, rawTargetPos, 0.2f); // Smoothly interpolates the _smoothedTargetPos towards the rawTargetPos.

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
                    var targetOrientation = BulletSharp.Math.Quaternion.Identity;

                    float rotationSmoothFactor = 5f * Time.DeltaTime;

                    var newOrientation = BulletSharp.Math.Quaternion.Slerp(currentOrientation, targetOrientation, rotationSmoothFactor);

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


            if (!SceneEditor.Enabled)
            {
                var basePosition = PlayerGameObject.Transform.Position;
                _camera.Position = new Vector3(basePosition.X, basePosition.Y + _currentCameraYOffset, basePosition.Z);
            }
        }
        private Vector3 _smoothedTargetPos = Vector3.Zero;
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

        void UpdateCameraPosition2(float deltaTime, bool isWalking, bool isSprinting, bool isCrouching)
        {
            float targetYOffset = isCrouching ? CrouchCameraOffset : StandCameraOffset;
            _currentCameraYOffset = MathHelper.Lerp(_currentCameraYOffset, targetYOffset, deltaTime * CameraLerpSpeed);

            if (isWalking)
                _bobTimer += deltaTime * BobSpeed * (isSprinting ? 2f : 1f);
            else
                _bobTimer = 0f;

            float bobAmount = BobAmount * (isCrouching ? 0.5f : 1f);
            float bobOffsetY = MathF.Sin(_bobTimer) * bobAmount * 2;
            float bobOffsetX = MathF.Sin(_bobTimer * 0.5f) * bobAmount * 2f;

            Vector3 playerPos = PlayerGameObject.Transform.Position;
            _camera.Position = new Vector3(
                playerPos.X + bobOffsetX,
                playerPos.Y + _currentCameraYOffset + bobOffsetY,
                playerPos.Z
            );
        }
        void UpdateCameraPosition(float deltaTime, bool isWalking, bool isSprinting, bool isCrouching)
        {
            float targetYOffset = isCrouching ? CrouchCameraOffset : StandCameraOffset;
            _currentCameraYOffset2 = MathHelper.Lerp(_currentCameraYOffset2, targetYOffset, deltaTime * CameraLerpSpeed2);

            float walkSpeedFactor = isSprinting ? 2f : 1f;
            _bobTimer += deltaTime * BobSpeed * walkSpeedFactor;

            float targetBlend = isWalking ? 1f : 0f;
            _bobBlend = MathHelper.Lerp(_bobBlend, targetBlend, deltaTime * 5f);

            float bobAmount = BobAmount * (isCrouching ? 0.5f : 1f);
            float bobOffsetY = MathF.Sin(_bobTimer * 1f) * bobAmount * _bobBlend;
            float bobOffsetX = MathF.Sin(_bobTimer * 0.5f) * bobAmount * 0.5f * _bobBlend;

            Vector3 playerPos = PlayerGameObject.Transform.Position;
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
