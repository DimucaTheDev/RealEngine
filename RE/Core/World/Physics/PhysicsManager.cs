using BulletSharp;
using OpenTK.Mathematics;
using RE.Core.Scripting;
using RE.Core.World.Components;
using RE.Debug.Overlay;
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
        private static bool _init = false;
        private static List<TaskScheduler> _schedulers = new List<TaskScheduler>();
        private static int _currentScheduler = 0;

        public static DiscreteDynamicsWorldMultiThreaded DynamicsWorld = null!;
        public static bool EnableSimulation = true;

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
                    foreach (var obj in DynamicsWorld.CollisionObjectArray)
                    {
                        if (obj is RigidBody rb)
                        {
                            if (obj.UserObject is RigidBodyComponent or ColliderComponent)
                            {
                                if (((Component)obj.UserObject).Owner.Parent?.GetComponent<PlayerComponent>() != null!)
                                    rb.Gravity = new(0, (((float)e) / -9.81f) * -25f, 0);
                            }
                            else
                                rb.Gravity = new(0, (float)e!, 0);
                        }
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

                if (obj is RigidBody { MotionState: not null, IsStaticObject: false } body)
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
                            foreach (var hitObj in rayCallback.CollisionObjects)
                            {
                                if (hitObj is RigidBody { IsStaticObject: true })
                                {
                                    blocked = true;
                                    break; // static object
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

        public static void Update(float deltaTime)
        {
            if (!_init)
                return;
            if (!SceneEditor.Enabled)
            {
                DynamicsWorld.StepSimulation(deltaTime, EnableSimulation ? 5 : 0, Time.DeltaTime);
            }

        }
        public static void Dispose()
        {
            _broadphase.Dispose();
            _dispatcher.Dispose();
            DynamicsWorld.Dispose();
            _parallelSolver.Dispose();
            CollisionConfiguration.Dispose();
        }
    }
}