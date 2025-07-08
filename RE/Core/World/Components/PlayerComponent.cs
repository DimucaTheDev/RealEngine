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
            _targetCameraYOffset = _isCrouching ? _crouchCameraOffset : _standCameraOffset;
            _currentCameraYOffset = MathHelper.Lerp(_currentCameraYOffset, _targetCameraYOffset, (float)(args.Time * _cameraLerpSpeed));
            var basePosition = _playerGameObject.Transform.Position;
            _camera.Position = new Vector3(basePosition.X, basePosition.Y + _currentCameraYOffset, basePosition.Z);


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
            if (input.IsKeyDown(Keys.A))
                moveDirection -= Vector3.Normalize(Vector3.Cross(_camera.Front, _camera.Up));
            if (input.IsKeyDown(Keys.D))
                moveDirection += Vector3.Normalize(Vector3.Cross(_camera.Front, _camera.Up));

            bool crouchKeyDown = input.IsKeyDown(Keys.LeftControl);
            bool sprintKeyDown = input.IsKeyDown(Keys.LeftShift);

            var velocity = rbN.LinearVelocity;
            var horizontalSpeed = _isCrouching ? 3 : (sprintKeyDown ? 10.5f : 6.5f);


            if (crouchKeyDown != _isCrouching)
            {

                float newHeight = crouchKeyDown ? _crouchHeight : _standHeight;

                if (!_isCrouching || (_isCrouching && CanStandUp()))
                {
                    _playerGameObject.Transform.Scale = new Vector3(0.75f, newHeight, 0.75f);
                    _isCrouching = crouchKeyDown;
                }

                var boxCollider = _playerGameObject.GetComponent<CapsuleColliderComponent>();
                if (boxCollider != null)
                {
                    var rb = boxCollider.RigidBody;
                    var transform = rb.WorldTransform;

                    rb.CollisionShape = boxCollider.CreateCollisionShape();
                    rb.WorldTransform = transform;
                    rb.MotionState?.SetWorldTransform(ref transform);

                    var rbGravity = rb.Gravity;

                    rb.Disable();
                    rb.Enable();

                    rb.Gravity = rbGravity;

                }
            }


            if (moveDirection != Vector3.Zero)
            {
                moveDirection = moveDirection.Normalized() * horizontalSpeed;
                rbN.LinearVelocity = moveDirection.ToBulletVector3() with { Y = velocity.Y };
            }
            else
            {
                rbN.LinearVelocity = new BulletSharp.Math.Vector3(0, velocity.Y, 0);
            }

            if (input.IsKeyDown(Keys.Space) && IsGrounded() && _jumpCd <= 0)
            {
                rbN.LinearVelocity = rbN.LinearVelocity with { Y = 0 };
                rbN.ApplyCentralImpulse(9 * BulletSharp.Math.Vector3.UnitY);
                _jumpCd = 0.1f; // 200 ms cooldown for jumping
            }
            if (_jumpCd >= 0) _jumpCd -= (float)args.Time;

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
            Vector3 to = from - new Vector3(0, 0.2f, 0); // короткий луч вниз


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
        bool CanStandUp()
        {
            var from = _playerGameObject.Transform.Position + new Vector3(0, _crouchHeight / 2f, 0); // верх текущего положения
            var to = from + new Vector3(0, _standHeight - _crouchHeight, 0); // куда хотим "дорасти"

            var rayFrom = from.ToBulletVector3();
            var rayTo = to.ToBulletVector3();

            var callback = new ClosestRayResultCallback(ref rayFrom, ref rayTo);
            PhysicsManager.DynamicsWorld.RayTest(rayFrom, rayTo, callback);

            return !callback.HasHit;
        }

    }
}
