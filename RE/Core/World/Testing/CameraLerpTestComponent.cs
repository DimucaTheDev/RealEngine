using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Debug;
using RE.Rendering;

namespace RE.Core.World.Testing
{
    internal class CameraLerpTestComponent : Component
    {
        int curveIndex;   // индекс кривой (каждая = 4 точки)
        float t;
        float speed = 0.25f; // скорость по параметру
        bool loop = true;
        Vector3[] path =
        {
            new(10, 0, 0),     // P0
            new(10, 2, 3),     // P1
            new(7, 3, 7),      // P2
            new(4, 2, 9),      // P3

            new(1, 1, 11),     // P1
            new(-3, 0, 10),    // P2
            new(-7, -1, 7),    // P3
            
            new(3, 2, 5),
            new(10, 0, 0),
            new(10, 2, 3),
        };

        public override void Render(FrameEventArgs args)
        {
            DrawBezierPath(path);
        }

        public override void Update(FrameEventArgs args)
        {
            UpdateCamera((float)args.Time);
        }
        Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;

            return
                uu * u * p0 +
                3f * uu * t * p1 +
                3f * u * tt * p2 +
                tt * t * p3;
        }

        void DrawBezierPath(Vector3[] points)
        {
            const int segments = 40;

            if (points == null || points.Length < 4)
                return;

            for (int i = 0; i <= points.Length - 4; i += 3)
            {
                Vector3 p0 = points[i];
                Vector3 p1 = points[i + 1];
                Vector3 p2 = points[i + 2];
                Vector3 p3 = points[i + 3];

                Vector3 prev = p0;

                for (int s = 1; s <= segments; s++)
                {
                    float t = (float)s / segments;
                    Vector3 curr = Bezier(p0, p1, p2, p3, t);

                    LineRenderer.DrawLine(prev, curr, (1, 0, 0, 1), (1, 0, 0, 1));
                    prev = curr;
                }
            }
        }

        void UpdateCamera(float deltaTime)
        {
            if (path == null || path.Length < 4)
                return;

            int maxCurve = (path.Length - 1) / 3;

            if (curveIndex >= maxCurve)
            {
                if (loop)
                {
                    curveIndex = 0;
                    t = 0f;
                }
                else
                {
                    Camera.Main.Position = path[path.Length - 1];
                    return;
                }
            }

            int i = curveIndex * 3;

            Vector3 p0 = path[i];
            Vector3 p1 = path[i + 1];
            Vector3 p2 = path[i + 2];
            Vector3 p3 = path[i + 3];

            t += speed * deltaTime;

            if (t >= 1f)
            {
                t = 0f;
                curveIndex++;
                return;
            }

            Camera.Main.Position = Bezier(p0, p1, p2, p3, t);
        }


        public override JsonNode GetSaveData()
        {
            return null!;
        }
    }
}
