using BulletSharp;
using OpenTK.Mathematics;
using RE.Rendering.Renderables;
using Log = Serilog.Log;

namespace RE.Core.Physics
{
    internal static class PhysManager
    {
        private static CollisionConfiguration? _collisionConfiguration;
        private static CollisionDispatcher? _dispatcher;
        private static BroadphaseInterface? _broadphase;
        private static ConstraintSolver? _solver;
        private static DiscreteDynamicsWorld? _dynamicsWorld;

        private static List<PhysicObject> _physicObjects = new();
        private static HashSet<Tuple<PhysicObject, PhysicObject>> _currentCollisions = new();
        private static HashSet<Tuple<PhysicObject, PhysicObject>> _previousCollisions = new();
        private static bool _init = false;

        public static event Action<PhysicObject, PhysicObject>? CollisionEnter;
        public static event Action<PhysicObject, PhysicObject>? CollisionStay;
        public static event Action<PhysicObject, PhysicObject>? CollisionExit;

        public static void Init()
        {
            if (_init)
            {
                Log.Warning("Physics Manager is already initialized!");
                return;
            }
            _collisionConfiguration = new DefaultCollisionConfiguration();
            _dispatcher = new CollisionDispatcher(_collisionConfiguration);
            _broadphase = new DbvtBroadphase();
            _solver = new SequentialImpulseConstraintSolver();
            _dynamicsWorld = new DiscreteDynamicsWorld(_dispatcher, _broadphase, _solver, _collisionConfiguration);
            _dynamicsWorld.Gravity = new BulletSharp.Math.Vector3(0, -9.81f, 0);

            _init = true;


        }
        //remove me
        public static PhysicObject c(BulletSharp.Math.Vector3 scale)
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
        public static PhysicObject CreateCubePhysicsObject(ModelRenderer modelRenderer, float mass)
        {
            BulletSharp.Math.Matrix startTransform = BulletSharp.Math.Matrix.Identity;
            startTransform.Origin = new BulletSharp.Math.Vector3(modelRenderer.Position.X, modelRenderer.Position.Y, modelRenderer.Position.Z);
            startTransform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(new BulletSharp.Math.Quaternion(modelRenderer.Rotation.X, modelRenderer.Rotation.Y, modelRenderer.Rotation.Z, modelRenderer.Rotation.W));

            BulletSharp.Math.Vector3 halfExtents = new BulletSharp.Math.Vector3(
                modelRenderer.Scale.X * 1,// 0.5f,
                modelRenderer.Scale.Y * 1,// 0.5f,
                modelRenderer.Scale.Z * 1 // 0.5f
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

        public static void RemovePhysicsObject(PhysicObject physicObject)
        {
            if (_physicObjects.Contains(physicObject))
            {
                _dynamicsWorld.RemoveRigidBody(physicObject.RigidBody);

                _currentCollisions.RemoveWhere(pair => pair.Item1 == physicObject || pair.Item2 == physicObject);
                _previousCollisions.RemoveWhere(pair => pair.Item1 == physicObject || pair.Item2 == physicObject);

                physicObject.RigidBody.Dispose();
                _physicObjects.Remove(physicObject);
                physicObject.Dispose();
            }
        }
        public static void Update(float deltaTime)
        {
            if (!_init) return;

            _dynamicsWorld.StepSimulation(deltaTime, 10, 1f / 60f);
            _currentCollisions.Clear();

            int numManifolds = _dispatcher.NumManifolds;
            for (int i = 0; i < numManifolds; i++)
            {
                PersistentManifold contactManifold = _dispatcher.GetManifoldByIndexInternal(i);
                CollisionObject obA = contactManifold.Body0;
                CollisionObject obB = contactManifold.Body1;

                if (obA.UserObject is PhysicObject physObjA && obB.UserObject is PhysicObject physObjB)
                {
                    bool hasActualContact = false;
                    for (int j = 0; j < contactManifold.NumContacts; j++)
                    {
                        ManifoldPoint pt = contactManifold.GetContactPoint(j);

                        const float contactThreshold = 0.005f;

                        if (pt.Distance <= contactThreshold)
                        {
                            hasActualContact = true;
                            break;
                        }
                    }

                    if (hasActualContact)
                    {
                        _currentCollisions.Add(GetCollisionPair(physObjA, physObjB));
                    }
                }
            }

            foreach (var previousPair in _previousCollisions)
            {
                if (!_currentCollisions.Contains(previousPair))
                {
                    CollisionExit?.Invoke(previousPair.Item1, previousPair.Item2);
                    previousPair.Item1.OnColliderExit(previousPair.Item2);
                    previousPair.Item2.OnColliderExit(previousPair.Item1);
                }
            }

            foreach (var currentPair in _currentCollisions)
            {
                if (!_previousCollisions.Contains(currentPair))
                {
                    CollisionEnter?.Invoke(currentPair.Item1, currentPair.Item2);
                    currentPair.Item1.OnColliderEnter(currentPair.Item2);
                    currentPair.Item2.OnColliderEnter(currentPair.Item1);
                }
                else
                {
                    CollisionStay?.Invoke(currentPair.Item1, currentPair.Item2);
                    currentPair.Item1.OnColliderStay(currentPair.Item2);
                    currentPair.Item2.OnColliderStay(currentPair.Item1);
                }
            }

            _previousCollisions.Clear();
            foreach (var pair in _currentCollisions)
            {
                _previousCollisions.Add(pair);
            }
        }

        private static Tuple<PhysicObject, PhysicObject> GetCollisionPair(PhysicObject objA, PhysicObject objB)
        {
            if (objA.GetHashCode() < objB.GetHashCode())
            {
                return Tuple.Create(objA, objB);
            }
            else
            {
                return Tuple.Create(objB, objA);
            }
        }

        public static void Dispose()
        {
            _currentCollisions.Clear();
            _previousCollisions.Clear();

            foreach (var obj in _physicObjects)
            {
                _dynamicsWorld.RemoveRigidBody(obj.RigidBody);
                obj.RigidBody.Dispose();
                obj.Dispose();
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