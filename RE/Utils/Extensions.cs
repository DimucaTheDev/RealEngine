using System.Text.Json.Nodes;
using BulletSharp;
using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.World.Physics;
using RE.Rendering;
using Vector4 = System.Numerics.Vector4;

namespace RE.Utils
{
    public static class Extensions
    {
        public static MemoryStream AsMemoryStream(this Stream s)
        {
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        extension(Color c)
        {
            public uint ToImGuiColor()
            {
                return ImGui.ColorConvertFloat4ToU32(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f));
            }
        }
        extension(Color4 c)
        {
            public Vector4 ToVec4Color()
            {
                return new Vector4(c.R, c.G, c.B, c.A);
            }
            public OpenTK.Mathematics.Vector4 ToTkVec4Color()
            {
                return new(c.R, c.G, c.B, c.A);
            }
            public uint ToImGuiColor()
            {
                return ImGui.ColorConvertFloat4ToU32(new Vector4(c.R, c.G, c.B, c.A));
            }
        }

        // Time
        extension(Time.ScheduledTask task)
        {
            public void InvokeNow()
            {
                task.Action.Invoke();
                Time.RemoveTask(task);
            }
            public void TerminateIfScheduled()
            {
                if (task?.IsScheduled() ?? false)
                    Terminate(task);
            }
            public void Terminate() => Time.RemoveTask(task);
        }

        // Renderable
        extension<T>(T r) where T : Renderable
        {
            public void StartRender() => RenderManager.AddRenderable(r);
            public bool IsRendering()
            {
                return RenderManager.Renderables.TryGetValue(r.RenderLayer, out var types) &&
                       types.TryGetValue(typeof(T), out var list) &&
                       list.Contains(r);
            }
            public void StopRender()
            {
                RenderManager.RemoveRenderable(r);
            }
        }

        extension(RigidBody r)
        {
            public void Disable() => PhysicsManager.DynamicsWorld.RemoveRigidBody(r);
            public void Enable() => PhysicsManager.DynamicsWorld.AddRigidBody(r);
        }

        // Conversions
         
        

        public static JsonArray ToJsonArray(this Vector3 v) => new(v.X, v.Y, v.Z);
        public static bool IsNullOrEmpty(this string str) => string.IsNullOrEmpty(str);

    }
}
