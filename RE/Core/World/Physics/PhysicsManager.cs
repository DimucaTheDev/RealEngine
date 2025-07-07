using BulletSharp;
using OpenTK.Mathematics;
using RE.Core.Scripting;
using RE.Rendering.Renderables;
using RE.Utils;
using Log = Serilog.Log;
using TaskScheduler = BulletSharp.TaskScheduler;

namespace RE.Core.World.Physics
{
    internal static class PhysicsManager
    {
        private static ConstraintSolverPoolMultiThreaded _solverPool;
        private static SequentialImpulseConstraintSolverMultiThreaded _parallelSolver;
        private static DbvtBroadphase _broadphase;
        private static CollisionDispatcherMultiThreaded _dispatcher;
        private static CollisionConfiguration CollisionConfiguration;
        private static List<PhysicObject> _physicObjects = new();
        private static HashSet<Tuple<PhysicObject, PhysicObject>> _currentCollisions = new();
        private static HashSet<Tuple<PhysicObject, PhysicObject>> _previousCollisions = new();
        private static bool _init = false;
        private static List<TaskScheduler> _schedulers = new List<TaskScheduler>();
        private static int _currentScheduler = 0;

        public static DiscreteDynamicsWorldMultiThreaded DynamicsWorld;

        public static event Action<PhysicObject, PhysicObject>? CollisionEnter;
        public static event Action<PhysicObject, PhysicObject>? CollisionStay;
        public static event Action<PhysicObject, PhysicObject>? CollisionExit;

        public static void NextTaskScheduler()
        {
            _currentScheduler++;
            if (_currentScheduler >= _schedulers.Count)
            {
                _currentScheduler = 0;
            }
            TaskScheduler scheduler = _schedulers[_currentScheduler];
            scheduler.NumThreads = scheduler.MaxNumThreads;
            Threads.TaskScheduler = scheduler;
        }
        private static void CreateSchedulers()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-s"))
            {
                Log.Information("Using Sequential Task Scheduler");
                AddScheduler(Threads.GetSequentialTaskScheduler());
            }
            if (args.Contains("-mpt"))
            {
                Log.Information("Using Multi-Processing Task Scheduler");
                AddScheduler(Threads.GetOpenMPTaskScheduler());
            }
            if (args.Contains("-tbb"))
            {
                Log.Information("Using TBB Task Scheduler");
                AddScheduler(Threads.GetTbbTaskScheduler());
            }

            AddScheduler(Threads.GetPplTaskScheduler());
        }
        private static void AddScheduler(TaskScheduler scheduler)
        {
            if (scheduler != null)
            {
                _schedulers.Add(scheduler);
            }
        }

        public static void Init()
        {
            if (_init)
            {
                Log.Warning("Physics Manager is already initialized!");
                return;
            }

            CreateSchedulers();
            NextTaskScheduler();

            using (var collisionConfigurationInfo = new DefaultCollisionConstructionInfo
            {
                DefaultMaxPersistentManifoldPoolSize = 80000,
                DefaultMaxCollisionAlgorithmPoolSize = 80000
            })
            {
                CollisionConfiguration = new DefaultCollisionConfiguration(collisionConfigurationInfo);
            }

            _dispatcher = new CollisionDispatcherMultiThreaded(CollisionConfiguration);
            _broadphase = new DbvtBroadphase();
            _solverPool = new ConstraintSolverPoolMultiThreaded(8);
            _parallelSolver = new SequentialImpulseConstraintSolverMultiThreaded();
            DynamicsWorld = new DiscreteDynamicsWorldMultiThreaded(_dispatcher, _broadphase, _solverPool,
                _parallelSolver, CollisionConfiguration);
            DynamicsWorld.SolverInfo.SolverMode = SolverModes.Simd | SolverModes.UseWarmStarting;
            DynamicsWorld.Gravity = new BulletSharp.Math.Vector3(0, -9.81f, 0);

            Variables.VariableChanged += (s, e) =>
            {
                if (s == "gravity")
                {
                    DynamicsWorld.Gravity = new(0, (float)e!, 0);
                    foreach (var obj in _physicObjects)
                    {
                        obj.RigidBody.Gravity = new(0, (float)e!, 0);
                    }
                }
            };
            _init = true;
        }

        public static void Explode(Vector3 pos, float radius, float force)
        {
            Vector3 explosionCenter = pos;
            float explosionRadius = radius;
            float explosionForce = force;

            for (int i = 0; i < DynamicsWorld.NumCollisionObjects; i++)
            {
                var obj = DynamicsWorld.CollisionObjectArray[i];

                if (obj is RigidBody body && body.MotionState != null && !body.IsStaticObject)
                {
                    Vector3 bodyPos = body.CenterOfMassPosition.ToOpenTkVector3();

                    Vector3 dir = bodyPos - explosionCenter;
                    float distance = dir.Length;

                    if (distance < explosionRadius && distance > 0.001f)
                    {
                        dir.Normalize();

                        float attenuation = 1.0f - distance / explosionRadius;
                        float forceMagnitude = explosionForce * attenuation;

                        Vector3 impulse = dir * forceMagnitude;
                        var from = explosionCenter.ToBulletVector3();
                        var to = bodyPos.ToBulletVector3();
                        var rayCallback = new AllHitsRayResultCallback(from, to);

                        DynamicsWorld.RayTest(from, to, rayCallback);

                        bool blocked = false;

                        if (rayCallback.HasHit)
                        {
                            for (int k = 0; k < rayCallback.CollisionObjects.Count; k++)
                            {
                                var hitObj = rayCallback.CollisionObjects[k]; // change i to k for correct index

                                if (hitObj is RigidBody rb)
                                {
                                    if (rb.IsStaticObject)
                                    {
                                        blocked = true;
                                        break; // static object
                                    }
                                }
                            }
                        }

                        if (!blocked)
                        {
                            body.Activate();
                            body.ApplyImpulse(impulse.ToBulletVector3(), Vector3.Zero.ToBulletVector3());
                        }

                    }
                }
            }
        }


        public static PhysicObject CreateMeshPhysicObject(ModelRenderer modelRenderer, float mass)
        {
            if (modelRenderer.PhysicsVertices == null || modelRenderer.PhysicsIndices == null)
                throw new InvalidOperationException("Model does not have physics data");

            // Convert to BulletSharp.Math types (assuming your engine uses different Vector3/Quaternion)
            BulletSharp.Math.Matrix startTransform = BulletSharp.Math.Matrix.Identity;
            startTransform.Origin = new BulletSharp.Math.Vector3(modelRenderer.Position.X, modelRenderer.Position.Y, modelRenderer.Position.Z);
            startTransform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(new BulletSharp.Math.Quaternion(modelRenderer.Rotation.X, modelRenderer.Rotation.Y, modelRenderer.Rotation.Z, modelRenderer.Rotation.W));

            var bulletVertices = ConvertToBulletVectors(modelRenderer.PhysicsVertices, modelRenderer.Scale);
            var bulletIndices = modelRenderer.PhysicsIndices.ToArray();

            var meshInterface = new TriangleIndexVertexArray(
                bulletIndices,
                bulletVertices);

            var aabbMin = new BulletSharp.Math.Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var aabbMax = new BulletSharp.Math.Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (var v in bulletVertices)
            {
                aabbMin.X = Math.Min(aabbMin.X, v.X);
                aabbMin.Y = Math.Min(aabbMin.Y, v.Y);
                aabbMin.Z = Math.Min(aabbMin.Z, v.Z);

                aabbMax.X = Math.Max(aabbMax.X, v.X);
                aabbMax.Y = Math.Max(aabbMax.Y, v.Y);
                aabbMax.Z = Math.Max(aabbMax.Z, v.Z);
            }

            // Create the BvhTriangleMeshShape from the mesh interface.
            // The 'true' argument means 'useQuantizedAabbCompression', which is usually good for static meshes.
            CollisionShape meshShape = new BvhTriangleMeshShape(meshInterface, true);

            DefaultMotionState motionState = new DefaultMotionState(startTransform);

            BulletSharp.Math.Vector3 localInertia = BulletSharp.Math.Vector3.Zero;
            if (mass > 0f) // Only calculate inertia if mass is greater than 0 (i.e., dynamic object)
            {
                meshShape.CalculateLocalInertia(mass, out localInertia);
            }

            RigidBodyConstructionInfo rigidBodyCI = new RigidBodyConstructionInfo(mass, motionState, meshShape, localInertia);
            RigidBody rigidBody = new RigidBody(rigidBodyCI);

            // Explicitly set collision flags and activation state for static objects
            if (mass == 0f) // If it's intended to be a static, non-moving collider
            {
                rigidBody.CollisionFlags |= CollisionFlags.StaticObject;
                rigidBody.ActivationState = ActivationState.DisableDeactivation; // Prevent static objects from deactivating
            }
            // else if (mass > 0f) { // If it's a dynamic object (mesh shapes are generally bad for this)
            //    rigidBody.ActivationState = ActivationState.ActiveTag; // Allow normal activation/deactivation
            // }

            DynamicsWorld.AddRigidBody(rigidBody);

            PhysicObject physObject = new PhysicObject(modelRenderer, rigidBody);
            // No need to store IntPtrs for freeing here, as BulletSharp manages the mesh data lifetime.
            _physicObjects.Add(physObject);
            return physObject;
        }

        private static BulletSharp.Math.Vector3[] ConvertToBulletVectors(float[] data, Vector3 scale)
        {
            if (data.Length % 3 != 0)
                throw new ArgumentException("PhysicsVertices should be multiple of 3");

            var result = new BulletSharp.Math.Vector3[data.Length / 3];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new BulletSharp.Math.Vector3(
                    data[i * 3 + 0] * scale.X,
                    data[i * 3 + 1] * scale.Y,
                    data[i * 3 + 2] * scale.Z
                );
            }
            return result;
        }

        public static PhysicObject CreateSpherePhysicsObject(ModelRenderer modelRenderer, float mass, float? radius = null)
        {
            BulletSharp.Math.Matrix startTransform = BulletSharp.Math.Matrix.Identity;
            startTransform.Origin = new BulletSharp.Math.Vector3(modelRenderer.Position.X, modelRenderer.Position.Y, modelRenderer.Position.Z);
            startTransform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(new BulletSharp.Math.Quaternion(modelRenderer.Rotation.X, modelRenderer.Rotation.Y, modelRenderer.Rotation.Z, modelRenderer.Rotation.W));
            radius ??= modelRenderer.Scale.X  /* * 0.5f*/; // Assuming uniform scale for sphere
            CollisionShape sphereShape = new SphereShape(radius.Value);
            DefaultMotionState motionState = new DefaultMotionState(startTransform);
            BulletSharp.Math.Vector3 localInertia = BulletSharp.Math.Vector3.Zero;
            if (mass != 0)
            {
                sphereShape.CalculateLocalInertia(mass, out localInertia);
            }
            RigidBodyConstructionInfo rigidBodyCI = new RigidBodyConstructionInfo(mass, motionState, sphereShape, localInertia);
            RigidBody rigidBody = new RigidBody(rigidBodyCI);
            DynamicsWorld.AddRigidBody(rigidBody);
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

            DynamicsWorld.AddRigidBody(rigidBody);

            PhysicObject physObject = new PhysicObject(modelRenderer, rigidBody);
            _physicObjects.Add(physObject);
            return physObject;
        }

        public static void RemovePhysicsObject(PhysicObject physicObject, bool removeMesh = false)
        {
            if (_physicObjects.Contains(physicObject))
            {
                DynamicsWorld.RemoveRigidBody(physicObject.RigidBody);

                _currentCollisions.RemoveWhere(pair => pair.Item1 == physicObject || pair.Item2 == physicObject);
                _previousCollisions.RemoveWhere(pair => pair.Item1 == physicObject || pair.Item2 == physicObject);

                physicObject.RigidBody.Dispose();
                _physicObjects.Remove(physicObject);
                physicObject.Dispose();
                if (removeMesh)
                    physicObject.Model.StopRender();

            }
        }
        public static void Update(float deltaTime)
        {
            if (!_init) return;

            DynamicsWorld.StepSimulation(deltaTime, 10, 1f / 60f);
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
                DynamicsWorld.RemoveRigidBody(obj.RigidBody);
                obj.RigidBody.Dispose();
                obj.Dispose();
            }
            _physicObjects.Clear();

            DynamicsWorld.Dispose();
            _parallelSolver.Dispose();
            _broadphase.Dispose();
            _dispatcher.Dispose();
            CollisionConfiguration.Dispose();
        }
    }
}