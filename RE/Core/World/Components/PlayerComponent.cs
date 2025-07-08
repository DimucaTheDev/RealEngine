using BulletSharp;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core.World.Physics;
using RE.Debug.Overlay;
using RE.Rendering;
using RE.Utils;
using Serilog;

namespace RE.Core.World.Components
{
    internal class PlayerComponent : Component
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


        float _bobAmount = 0.05f;
        float _bobSpeed = 10f;
        float _cameraLerpSpeed2 = 6f;

        float _bobTimer = 0f;
        float _bobBlend = 0f;
        float _currentCameraYOffset2 = 0f;
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

        public override void Start()
        {
            _camera = Camera.Instance;
            _playerGameObject = new GameObject();
            _playerGameObject.Transform.Scale = new Vector3(0.75f, _standHeight, 0.75f);
            _playerGameObject.Components.Add(new CapsuleColliderComponent());
            var rigidBodyComponent = new RigidBodyComponent();
            _playerGameObject.Components.Add(rigidBodyComponent);

            SceneManager.CurrentScene.GameObjects.Add(_playerGameObject);
            rigidBodyComponent.GetRigidBody().AngularFactor = BulletSharp.Math.Vector3.Zero;
            rigidBodyComponent.GetRigidBody().ActivationState = ActivationState.DisableDeactivation;
            rigidBodyComponent.GetRigidBody().Gravity = new BulletSharp.Math.Vector3(0, -25f, 0);
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

            if (Game.Instance.CursorState != CursorState.Grabbed || ImGui.GetIO().WantCaptureMouse) return;

            var rbN = _playerGameObject.GetComponent<RigidBodyComponent>()?.GetRigidBody();
            if (rbN == null) return;

            Vector3 moveDirection = Vector3.Zero;

            if (input.IsKeyDown(Keys.W))
                moveDirection += (_camera.Front with { Y = 0 }).Normalized();
            if (input.IsKeyDown(Keys.S))
                moveDirection -= (_camera.Front with { Y = 0 }).Normalized();
            if (input.IsKeyDown(Keys.A)) moveDirection -= Vector3.Normalize(Vector3.Cross(_camera.Front, _camera.Up));
            if (input.IsKeyDown(Keys.D)) moveDirection += Vector3.Normalize(Vector3.Cross(_camera.Front, _camera.Up));

            if (moveDirection.LengthSquared > 1e-6)
            {
                moveDirection = moveDirection.Normalized();
            }

            bool crouchKeyDown = input.IsKeyDown(Keys.LeftControl);
            bool sprintKeyDown = input.IsKeyDown(Keys.LeftShift);

            float maxSpeed = _isCrouching ? 5f : (sprintKeyDown ? 13.5f : 10f);
            float acceleration = 20f;
            float friction = 1f;

            Vector3 currentHorizontalVelocity = new Vector3(
                (float)rbN.LinearVelocity.X,
                0, (float)rbN.LinearVelocity.Z
            );

            // SECRET UFO MESSAGE: YA NE EBU CHO ZDES PROISHODIT

            Vector3 desiredHorizontalVelocity = moveDirection * maxSpeed;

            Vector3 velocityDifference = desiredHorizontalVelocity - currentHorizontalVelocity;

            if (moveDirection.LengthSquared > 1e-6)
            {
                Vector3 accelerationVector = velocityDifference * acceleration * Time.DeltaTime;

                if (accelerationVector.LengthSquared > velocityDifference.LengthSquared)
                {
                    accelerationVector = velocityDifference;
                }

                currentHorizontalVelocity += accelerationVector;

                if (currentHorizontalVelocity.LengthSquared > maxSpeed * maxSpeed)
                {
                    currentHorizontalVelocity = currentHorizontalVelocity.Normalized() * maxSpeed;
                }
            }
            else
            {
                float currentSpeed = currentHorizontalVelocity.Length;
                if (currentSpeed > 0)
                {
                    float drop = currentSpeed * friction * Time.DeltaTime;
                    float newSpeed = MathF.Max(0, currentSpeed - drop);
                    currentHorizontalVelocity = currentHorizontalVelocity.Normalized() * newSpeed;
                }
            }

            rbN.LinearVelocity = new BulletSharp.Math.Vector3(
                currentHorizontalVelocity.X,
                (float)rbN.LinearVelocity.Y, currentHorizontalVelocity.Z
            );

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
                        var rb = capsuleCollider.RigidBody;
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

            if (input.IsKeyDown(Keys.Space) && IsGrounded() && _jumpCd <= 0)
            {
                rbN.LinearVelocity = rbN.LinearVelocity with { Y = 0 };
                rbN.ApplyCentralImpulse(9 * BulletSharp.Math.Vector3.UnitY);
                _jumpCd = 0.1f;
            }
            if (_jumpCd >= 0) _jumpCd -= (float)args.Time;

            // UpdateCameraPosition((float)args.Time, currentHorizontalVelocity.LengthSquared > 1e-6, sprintKeyDown, _isCrouching);
            _targetCameraYOffset = _isCrouching ? _crouchCameraOffset : _standCameraOffset;
            _currentCameraYOffset = MathHelper.Lerp(_currentCameraYOffset, _targetCameraYOffset, (float)(Time.DeltaTime * _cameraLerpSpeed));
            var basePosition = _playerGameObject.Transform.Position;
            _camera.Position = new Vector3(basePosition.X, basePosition.Y + _currentCameraYOffset, basePosition.Z);

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

    }
}
