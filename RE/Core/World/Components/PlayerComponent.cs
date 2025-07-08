using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
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
        private float _crouchHeight = 0.8f;
        private float _crouchCameraOffset = 0.7f;
        private float _standCameraOffset = 1.6f;
        private float _cameraLerpSpeed = 5f;

        private float _currentCameraYOffset = 1.6f;
        private float _targetCameraYOffset = 1.6f;


        public override void Start()
        {
            _camera = Camera.Instance;
            _playerGameObject = new GameObject();
            _playerGameObject.Transform.Scale = new Vector3(0.5f, 1.75f, 0.5f);
            _playerGameObject.Components.Add(new BoxColliderComponent());
            _playerGameObject.Components.Add(new RigidBodyComponent());


            SceneManager.CurrentScene.GameObjects.Add(_playerGameObject);
            _playerGameObject.GetComponent<RigidBodyComponent>().GetRigidBody().AngularFactor = BulletSharp.Math.Vector3.Zero;
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

            var velocity = rbN.LinearVelocity;
            var horizontalSpeed = _isCrouching ? 2 : 7f;


            bool crouchKeyDown = input.IsKeyDown(Keys.LeftShift);

            if (crouchKeyDown != _isCrouching)
            {
                _isCrouching = crouchKeyDown;

                float newHeight = crouchKeyDown ? _crouchHeight : _standHeight;
                _playerGameObject.Transform.Scale = new Vector3(0.5f, newHeight, 0.5f);

                var boxCollider = _playerGameObject.GetComponent<BoxColliderComponent>();
                if (boxCollider != null)
                {
                    var rb = boxCollider.RigidBody;
                    var transform = rb.WorldTransform;

                    rb.CollisionShape = boxCollider.CreateCollisionShape();
                    rb.WorldTransform = transform;
                    rb.MotionState?.SetWorldTransform(ref transform);

                    rb.UpdateInertiaTensor();
                    rb.Activate();
                }
            }





            if (moveDirection != Vector3.Zero)
            {
                moveDirection = moveDirection.Normalized() * horizontalSpeed;
                rbN.LinearVelocity = moveDirection.ToBulletVector3();
            }
            else
            {
                rbN.LinearVelocity = new BulletSharp.Math.Vector3(0, velocity.Y, 0);
            }

            if (input.IsKeyPressed(Keys.Space))
            {
                _playerGameObject.GetComponent<RigidBodyComponent>().GetRigidBody().ApplyImpulse(6 * BulletSharp.Math.Vector3.UnitY, BulletSharp.Math.Vector3.Zero);
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
        }
    }
}
