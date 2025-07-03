using BulletSharp;
using OpenTK.Mathematics; // Add this using directive
using RE.Rendering.Renderables;

namespace RE.Core.Physics
{
    internal class PhysManager : IDisposable
    {
        private CollisionConfiguration _collisionConfiguration;
        private CollisionDispatcher _dispatcher;
        private BroadphaseInterface _broadphase;
        private ConstraintSolver _solver;
        private DiscreteDynamicsWorld _dynamicsWorld;

        // List to keep track of created PhysicObjects to dispose them correctly
        private List<PhysicObject> _physicObjects = new List<PhysicObject>();

        public static PhysManager Instance { get; set; }

        public static void Init()
        {
            Instance = new PhysManager();
        }

        public PhysManager()
        {
            _collisionConfiguration = new DefaultCollisionConfiguration();
            _dispatcher = new CollisionDispatcher(_collisionConfiguration);
            _broadphase = new DbvtBroadphase();
            _solver = new SequentialImpulseConstraintSolver();
            _dynamicsWorld = new DiscreteDynamicsWorld(_dispatcher, _broadphase, _solver, _collisionConfiguration);

            _dynamicsWorld.Gravity = new BulletSharp.Math.Vector3(0, -9.81f, 0);
        }

        public PhysicObject c(BulletSharp.Math.Vector3 scale)
        {
            var modelRenderer = new ModelRenderer("Assets/Models/plane.fbx", Vector3.Zero, Quaternion.Identity);
            BulletSharp.Math.Matrix startTransform = BulletSharp.Math.Matrix.Identity;
            startTransform.Origin = new BulletSharp.Math.Vector3(modelRenderer.Position.X, modelRenderer.Position.Y, modelRenderer.Position.Z);
            startTransform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(new BulletSharp.Math.Quaternion(modelRenderer.Rotation.X, modelRenderer.Rotation.Y, modelRenderer.Rotation.Z, modelRenderer.Rotation.W));

            BulletSharp.Math.Vector3 halfExtents = scale;
            CollisionShape boxShape = new BoxShape(halfExtents);

            DefaultMotionState motionState = new DefaultMotionState(startTransform);

            BulletSharp.Math.Vector3 localInertia = BulletSharp.Math.Vector3.Zero;


            RigidBodyConstructionInfo rigidBodyCI = new RigidBodyConstructionInfo(0, motionState, boxShape, localInertia);
            RigidBody rigidBody = new RigidBody(rigidBodyCI);

            _dynamicsWorld.AddRigidBody(rigidBody);

            PhysicObject physObject = new PhysicObject(modelRenderer, rigidBody);
            _physicObjects.Add(physObject);
            return physObject;
        }
        public PhysicObject CreateCubePhysicsObject(ModelRenderer modelRenderer, float mass)
        {
            BulletSharp.Math.Matrix startTransform = BulletSharp.Math.Matrix.Identity;
            startTransform.Origin = new BulletSharp.Math.Vector3(modelRenderer.Position.X, modelRenderer.Position.Y, modelRenderer.Position.Z);
            startTransform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(new BulletSharp.Math.Quaternion(modelRenderer.Rotation.X, modelRenderer.Rotation.Y, modelRenderer.Rotation.Z, modelRenderer.Rotation.W));

            BulletSharp.Math.Vector3 halfExtents = new BulletSharp.Math.Vector3(
                modelRenderer.Scale.X * 1,// 0.5f,
                modelRenderer.Scale.Y * 1,// 0.5f,
                modelRenderer.Scale.Z * 1// 0.5f
            );
            CollisionShape boxShape = new BoxShape(halfExtents);

            DefaultMotionState motionState = new DefaultMotionState(startTransform);

            BulletSharp.Math.Vector3 localInertia = BulletSharp.Math.Vector3.Zero;
            if (mass != 0)
            {
                boxShape.CalculateLocalInertia(mass, out localInertia);
            }

            RigidBodyConstructionInfo rigidBodyCI = new RigidBodyConstructionInfo(mass, motionState, boxShape, localInertia);
            RigidBody rigidBody = new RigidBody(rigidBodyCI);

            _dynamicsWorld.AddRigidBody(rigidBody);

            PhysicObject physObject = new PhysicObject(modelRenderer, rigidBody);
            _physicObjects.Add(physObject);
            return physObject;
        }
        public void RemovePhysicsObject(PhysicObject physicObject)
        {
            if (_physicObjects.Contains(physicObject))
            {
                _dynamicsWorld.RemoveRigidBody(physicObject.RigidBody);
                physicObject.RigidBody.Dispose();
                _physicObjects.Remove(physicObject);
                physicObject.Dispose();
            }
        }

        public void Update(float deltaTime)
        {
            _dynamicsWorld.StepSimulation(deltaTime);
        }

        public void Dispose()
        {
            // Clean up physics objects
            foreach (var obj in _physicObjects)
            {
                _dynamicsWorld.RemoveRigidBody(obj.RigidBody);
                obj.RigidBody.Dispose();
                obj.Dispose(); // Dispose of the PhysicObject itself
            }
            _physicObjects.Clear();

            _dynamicsWorld.Dispose();
            _solver.Dispose();
            _broadphase.Dispose();
            _dispatcher.Dispose();
            _collisionConfiguration.Dispose();
        }
    }
}