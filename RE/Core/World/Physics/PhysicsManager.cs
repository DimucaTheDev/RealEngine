using BulletSharp;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Core.Scripting;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Debug.Overlay;
using RE.Utils;
using Log = Serilog.Log;
using TaskScheduler = BulletSharp.TaskScheduler;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace RE.Core.World.Physics
{
    /// <summary>
    /// Manages the initialization, configuration, and simulation of the physics engine, including world creation,
    /// scheduling, and global physics operations.
    /// </summary>
    /// <remarks>The PhysicsManager provides static methods and properties to control the physics simulation
    /// lifecycle and perform global physics actions, such as applying explosion forces. It is responsible for setting
    /// up the multi-threaded physics world and managing simulation state. Only one instance should be initialized per
    /// application. Thread safety is not guaranteed for all operations; ensure that physics methods are called from the
    /// appropriate context.
    /// </remarks>
    public class PhysicsManager : DynamicAsset
    {
        private static readonly List<TaskScheduler> Schedulers = [];
        private static bool _init;
        private static int _currentScheduler;
        private static ConstraintSolverPoolMultiThreaded _solverPool;
        private static SequentialImpulseConstraintSolverMultiThreaded _parallelSolver;
        private static DbvtBroadphase _broadphase;
        private static CollisionDispatcherMultiThreaded _dispatcher;
        private static CollisionConfiguration _collisionConfiguration;
         
        public static DiscreteDynamicsWorldMultiThreaded DynamicsWorld = null!;
        public static bool EnableSimulation = true;

        public static void Init() => new PhysicsManager().OnLoad();

        public override void OnLoad()
        {
            base.OnLoad();

            if (_init)
            {
                Log.Warning("Physics Manager is already initialized!");
                return;
            }

            CreateSchedulers();
            NextTaskScheduler();

            using (var collisionConfigurationInfo = new DefaultCollisionConstructionInfo())
            {
                collisionConfigurationInfo.DefaultMaxPersistentManifoldPoolSize = 80000; // magic number?
                collisionConfigurationInfo.DefaultMaxCollisionAlgorithmPoolSize = 80000;
                _collisionConfiguration = new DefaultCollisionConfiguration(collisionConfigurationInfo);
            }

            _dispatcher = new CollisionDispatcherMultiThreaded(_collisionConfiguration);
            _broadphase = new DbvtBroadphase();
            _solverPool = new ConstraintSolverPoolMultiThreaded(8);
            _parallelSolver = new SequentialImpulseConstraintSolverMultiThreaded();

            DynamicsWorld = new DiscreteDynamicsWorldMultiThreaded(_dispatcher, _broadphase, _solverPool,
                _parallelSolver, _collisionConfiguration);
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
                                    rb.Gravity = new(0, (float)e / -9.81f * -25f, 0);
                            }
                            else
                                rb.Gravity = new(0, (float)e, 0);
                        }
                    }
                }
            };
            Log.Debug("Physics Manager initialized");
            _init = true;
        }

        public static void Explode(Vector3 pos, float radius, float force)
        {
            for (int i = 0; i < DynamicsWorld.NumCollisionObjects; i++)
            {
                var obj = DynamicsWorld.CollisionObjectArray[i];

                if (obj is RigidBody { MotionState: not null, IsStaticObject: false } body)
                {
                    Vector3 bodyPos = body.CenterOfMassPosition.ToOpenTkVector3();

                    Vector3 dir = bodyPos - pos;
                    float distance = dir.Length;

                    if (distance < radius && distance > 0.001f)
                    {
                        dir.Normalize();

                        float attenuation = 1.0f - distance / radius;
                        float forceMagnitude = force * attenuation;

                        Vector3 impulse = dir * forceMagnitude;
                        var from = pos.ToBulletVector3();
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

        public override void OnUnload()
        {
            base.OnUnload();

            DynamicsWorld.Dispose();
            _broadphase.Dispose();
            _dispatcher.Dispose();
            _parallelSolver.Dispose();
            _collisionConfiguration.Dispose();
        }

        private static void NextTaskScheduler()
        {
            _currentScheduler++;
            if (_currentScheduler >= Schedulers.Count)
            {
                _currentScheduler = 0;
            }
            var scheduler = Schedulers[_currentScheduler];
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

            Log.Debug("Using PPL Task Scheduler");
            AddScheduler(Threads.GetPplTaskScheduler());
        }

        private static void AddScheduler(TaskScheduler scheduler)
        {
            Schedulers.Add(scheduler);
        }
    }
}