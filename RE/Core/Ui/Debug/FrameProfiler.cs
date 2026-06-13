using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;

namespace RE.Core.Ui.Debug
{
    public static class FrameProfiler
    {
        public sealed class ProfilerNode
        {
            public readonly List<ProfilerNode> Children = new();
            public string Name = "";
            public double Start;
            public double End;
        }

        public readonly struct ProfileScope : IDisposable
        {
            public void Dispose()
            {
                End();
            }
        }

        private static readonly Stopwatch Sw = new();
        private static readonly Stack<ProfilerNode> Stack = new();
        private static ProfilerNode _lastFrame = new();

        public static ProfilerNode Root { get; private set; } = new();
        public static ProfilerNode GetLastFrame() => _lastFrame ?? Root;

        internal static int UpdateDelay = 20;

        public static void BeginFrame()
        {
            if (Stack.Count - 1 > 0 /* ignore root*/)
            {
                Stack.Pop();
                GL.PopDebugGroup();
            }

            Root = new ProfilerNode
            {
                Name = "Frame",
                Start = 0
            };

            Sw.Restart();
            Stack.Clear();
            Stack.Push(Root);
        }

        public static void EndFrame()
        {
            Root.End = Sw.Elapsed.TotalMilliseconds;

            if (Stack.Count - 1 > 0 /* ignore root*/)
            {
                Stack.Clear();
                GL.PopDebugGroup();
            }

            if (Time.ElapsedFrames % UpdateDelay == 0)
                _lastFrame = Root;
        }

        [MustDisposeResource]
        public static ProfileScope Scope(string name)
        {
            var parent = Stack.Peek();

            var node = new ProfilerNode
            {
                Name = name,
                Start = Sw.Elapsed.TotalMilliseconds
            };

            parent.Children.Add(node);
            Stack.Push(node);

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 0, -1, name);

            return new ProfileScope();
        }

        private static void End()
        {
            if (Stack.Count <= 1) return;

            var node = Stack.Pop();
            node.End = Sw.Elapsed.TotalMilliseconds;
            GL.PopDebugGroup();
        }
    }
}