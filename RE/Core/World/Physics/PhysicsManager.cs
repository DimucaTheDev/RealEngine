using System.Reflection;
using BulletSharp;
using DotRecast.Core.Collections.Extensions;
using Hexa.NET.OpenGL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Core.Initializing;
using RE.Core.Scripting;
using RE.Core.Ui.Debug;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Utils;
using GL = OpenTK.Graphics.OpenGL.GL;
using Log = Serilog.Log;
using SceneEditor = RE.Editor.SceneEditor;
using TaskScheduler = BulletSharp.TaskScheduler;

#pragma warning disable CS8618

namespace RE.Core.World.Physics
{
    /// <summary>
    /// Manages the initialization, configuration, and simulation of the physics engine, including world creation,
    /// scheduling, and global physics operations.
    /// </summary>
    /// <remarks>The PhysicsManager provides static methods and properties to control the physics simulation
    /// lifecycle and perform global physics actions, such as applying explosion forces. It is responsible for setting
    /// up the multithreaded physics world and managing simulation state. Only one instance should be initialized per
    /// application. Thread safety is not guaranteed for all operations; ensure that physics methods are called from the
    /// appropriate context.
    /// </remarks>
    public static class PhysicsManager
    {
        private class ContactState
        {
            public bool isInside;
            public int Counter;
        }

        private static readonly List<TaskScheduler> Schedulers = [];
        private static bool _init;
        private static int _currentScheduler;
        private static ConstraintSolverPoolMultiThreaded _solverPool;
        private static SequentialImpulseConstraintSolverMultiThreaded _parallelSolver;
        private static DbvtBroadphase _broadphase;
        private static CollisionDispatcherMultiThreaded _dispatcher;
        private static CollisionConfiguration _collisionConfiguration;
        private static readonly int MaxThreadSolver = Environment.ProcessorCount;
        private static readonly Dictionary<(CollisionObject, CollisionObject), ContactState> _states = new();

        const int EnterDelay = 3;
        const int ExitDelay = 10;

        /// <summary>
        /// Provides access to the multithreaded discrete dynamics world instance used for physics simulation.
        /// </summary>
        /// <remarks>This field should be initialized before use. It enables multi-threaded processing of
        /// physics simulations, which can improve performance in scenarios with complex interactions or large numbers
        /// of objects.</remarks>
        public static DiscreteDynamicsWorldMultiThreaded DynamicsWorld = null!;

        /// <summary>
        /// Indicates whether simulation mode is enabled.
        /// </summary>
        /// <remarks>If set to <see langword="false"/>, <see cref="Update"/> method will do nothing.</remarks>
        public static bool EnableSimulation = true;

        public static float Accumulator;
        public const float FixedTimeStep = 1 / 120f;

        internal static void Init()
        {
            if (_init)
            {
                Log.Error("Physics Manager is already initialized!");
                return;
            }

            CreateSchedulers();
            NextTaskScheduler();

            using (var collisionConfigurationInfo = new DefaultCollisionConstructionInfo())
            {
                collisionConfigurationInfo.DefaultMaxPersistentManifoldPoolSize = 80000;
                collisionConfigurationInfo.DefaultMaxCollisionAlgorithmPoolSize = 80000;
                _collisionConfiguration = new DefaultCollisionConfiguration(collisionConfigurationInfo);
            }

            _dispatcher = new CollisionDispatcherMultiThreaded(_collisionConfiguration);
            _broadphase = new DbvtBroadphase();
            _solverPool = new ConstraintSolverPoolMultiThreaded(MaxThreadSolver);
            Log.Debug("Solver pool thread count set to {Threads}", MaxThreadSolver);
            _parallelSolver = new SequentialImpulseConstraintSolverMultiThreaded();

            RecreateWorld();

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
                                    break;
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

        /// <summary>
        /// Advances the physics simulation by the specified time step if the simulation is initialized and no jobs are
        /// pending.
        /// </summary>
        /// <remarks>This method has no effect if the simulation is not initialized or if there are
        /// pending initialization jobs. The simulation step is only performed when the scene editor is
        /// disabled.</remarks>
        /// <param name="deltaTime">The amount of time, in seconds, to advance the simulation. Must be a non-negative value.</param>
        public static void Update(float deltaTime)
        {
            if (!_init || Initializer.HasJob)
                return;

            if (!SceneEditor.Enabled || (SceneEditor.SimulationRunning))
            {
                using (FrameProfiler.Scope("bullet"))
                {
                    Accumulator += deltaTime;
                    while (Accumulator >= FixedTimeStep)
                    {
                        using (FrameProfiler.Scope("step"))
                        {
                            DynamicsWorld.StepSimulation(FixedTimeStep, 0);
                            foreach (var obj in DynamicsWorld.CollisionObjectArray)
                            {
                                foreach (var c in (obj.UserObject as Component)?.Owner.Components!)
                                {
                                    if (!c.IsEnabled) continue;
                                    c.PhysicsSync();
                                }
                            }
                        }

                        Accumulator -= FixedTimeStep;
                    }

                    Alpha = Accumulator / FixedTimeStep;

                    using (FrameProfiler.Scope("collision"))
                    {
                        ProcessCollisions(DynamicsWorld);
                    }
                }
            }

            if (BulletDebugDrawer.Mode != DebugDrawModes.None)
                DynamicsWorld.DebugDrawWorld();
        }

        public static float Alpha { get; private set; } //todo: move

        internal static void RecreateWorld()
        {
            DynamicsWorld?.Dispose();
            DynamicsWorld = new DiscreteDynamicsWorldMultiThreaded(_dispatcher, _broadphase, _solverPool,
                _parallelSolver, _collisionConfiguration);
            DynamicsWorld.SolverInfo.SolverMode = SolverModes.Simd | SolverModes.UseWarmStarting;
            DynamicsWorld.SolverInfo.NumIterations = 10;
            //DynamicsWorld.SolverInfo.TimeStep = FixedTimeStep;
            DynamicsWorld.Gravity = new BulletSharp.Math.Vector3(0, -9.81f, 0);
            DynamicsWorld.DebugDrawer = new BulletDebugDrawer();
        }

        private static void ProcessCollisions(DiscreteDynamicsWorld world)
        {
            var dispatcher = world.Dispatcher;

            var activePairs = new HashSet<(CollisionObject, CollisionObject)>();

            int numManifolds = dispatcher.NumManifolds;

            for (int i = 0; i < numManifolds; i++)
            {
                var manifold = dispatcher.GetManifoldByIndexInternal(i);

                var a = manifold.Body0;
                var b = manifold.Body1;

                if (a == null || b == null)
                    continue;

                bool hasContact = manifold.NumContacts > 0;

                if (!hasContact)
                    continue;

                var pair = a.GetHashCode() < b.GetHashCode() ? (a, b) : (b, a);
                activePairs.Add(pair);

                var compA = a.UserObject as Component
                            ?? throw new InvalidOperationException($"Collision object {a} is not a Component");

                var compB = b.UserObject as Component
                            ?? throw new InvalidOperationException($"Collision object {b} is not a Component");

                if (!_states.TryGetValue(pair, out var state))
                {
                    state = new ContactState();
                    _states[pair] = state;
                }

                if (state.isInside)
                {
                    compA.Owner.Components.Where(c => c.IsEnabled).ForEach(c => c.OnCollide(compB.Owner));
                    compB.Owner.Components.Where(c => c.IsEnabled).ForEach(c => c.OnCollide(compA.Owner));
                }
                else
                {
                    state.Counter++;

                    if (state.Counter >= EnterDelay)
                    {
                        state.isInside = true;
                        state.Counter = 0;

                        compA.Owner.Components.Where(c => c.IsEnabled).ForEach(c => c.OnCollisionEnter(compB.Owner));
                        compB.Owner.Components.Where(c => c.IsEnabled).ForEach(c => c.OnCollisionEnter(compA.Owner));
                    }
                }
            }

            foreach (var kv in _states)
            {
                var pair = kv.Key;
                var state = kv.Value;

                if (activePairs.Contains(pair))
                    continue;

                if (!state.isInside)
                    continue;

                state.Counter--;

                if (state.Counter <= -ExitDelay)
                {
                    var a = pair.Item1;
                    var b = pair.Item2;

                    var compA = a.UserObject as Component
                                ?? throw new InvalidOperationException($"Collision object {a} is not a Component");

                    var compB = b.UserObject as Component
                                ?? throw new InvalidOperationException($"Collision object {b} is not a Component");

                    compA.Owner.Components.Where(c => c.IsEnabled).ForEach(c => c.OnCollisionExit(compB.Owner));
                    compB.Owner.Components.Where(c => c.IsEnabled).ForEach(c => c.OnCollisionExit(compA.Owner));

                    state.isInside = false;
                    state.Counter = 0;
                }
            }

            var toRemove = new List<(CollisionObject, CollisionObject)>();

            foreach (var kv in _states)
            {
                if (kv.Value is { isInside: false, Counter: 0 })
                    toRemove.Add(kv.Key);
            }

            foreach (var key in toRemove)
                _states.Remove(key);
        }

        internal static void Unload()
        {
            DynamicsWorld.Dispose();
            _broadphase.Dispose();
            _dispatcher.Dispose();
            _parallelSolver.Dispose();
            _collisionConfiguration.Dispose();
        }

        private static void NextTaskScheduler()
        {
            if (!Schedulers.Any())
            {
                Log.Fatal("No Bullet physics scheduler specified.");
                Environment.Exit(-1);
                return;
            }

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
            if (Game.CommandParseResult.GetValue<bool>("--phys-sequential"))
            {
                Log.Information("Using Sequential Task Scheduler");
                AddScheduler(Threads.GetSequentialTaskScheduler());
            }

            if (Game.CommandParseResult.GetValue<bool>("--phys-mp"))
            {
                Log.Information("Using Multi-Processing Task Scheduler");
                AddScheduler(Threads.GetOpenMPTaskScheduler());
            }

            if (Game.CommandParseResult.GetValue<bool>("--phys-tbb"))
            {
                Log.Information("Using TBB Task Scheduler");
                AddScheduler(Threads.GetTbbTaskScheduler());
            }

            if (Game.CommandParseResult.GetValue<bool>("--phys-ppl"))
            {
                Log.Information("Using PPL Task Scheduler");
                AddScheduler(Threads.GetPplTaskScheduler());
            }
        }

        private static void AddScheduler(TaskScheduler scheduler)
        {
            Schedulers.Add(scheduler);
        }

        public static void Destroy()
        {
            if (!_init)
                return;

            DynamicsWorld.Dispose();

            _dispatcher.Dispose();
            _collisionConfiguration.Dispose();
            _broadphase.Dispose();

            _parallelSolver.Dispose();
            _solverPool.Dispose();

            Schedulers.Clear();

            _init = false;
        }
    }
}