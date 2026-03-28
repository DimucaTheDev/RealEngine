using System.Text.Json.Nodes;
using Hexa.NET.ImGuizmo;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Debug;
using RE.Editor.Panels.Viewport;
using RE.Rendering;
using RE.Utils;
using SceneEditor = RE.Editor.SceneEditor;

namespace RE.Core.World.Testing
{
    internal class BezierTest : Component, IViewportRenderer
    {

        int curveIndex;   // индекс кривой (каждая = 4 точки)
        float t;
        float speed = 0.25f; // скорость по параметру
        bool loop = true;
        Vector3[] path =
            {
                new (  8.0f,  4.0f, -12.0f), // Anchor 0
                new (  6.0f,  6.0f,  -6.0f),
                new (  4.0f,  5.0f,   0.0f),

                new (  2.0f,  4.0f,   5.0f), // Anchor 1
                new (  0.0f,  2.0f,  10.0f),
                new ( -4.0f,  2.0f,  12.0f),

                new ( -8.0f,  3.0f,  14.0f), // Anchor 2
                new (-12.0f,  6.0f,  15.0f),
                new (-16.0f,  9.0f,  14.0f),

                new (-18.0f, 12.0f,  10.0f), // Anchor 3
                new (-19.0f, 15.0f,   4.0f),
                new (-16.0f, 18.0f,  -2.0f),

                new (-13.0f, 20.0f,  -7.0f), // Anchor 4
                new (-10.0f, 18.0f, -12.0f),
                new ( -6.0f, 15.0f, -16.0f),

                new ( -2.0f, 12.0f, -18.0f), // Anchor 5
                new (  2.0f,  9.0f, -19.0f),
                new (  6.0f,  6.0f, -17.0f),

                new (  9.0f,  3.0f, -13.0f), // Anchor 6
                new ( 11.0f,  1.0f,  -8.0f),
                new ( 12.0f,  0.0f,  -2.0f),

                new ( 13.0f, -1.0f,   4.0f), // Anchor 7
                new ( 14.0f, -1.0f,  10.0f),
                new ( 12.0f,  0.0f,  15.0f),

                new (  9.0f,  2.0f,  18.0f), // Anchor 8
                new (  6.0f,  3.0f,  19.0f),
                new (  3.0f,  4.0f,  17.0f),

                new (  1.0f,  5.0f,  13.0f), // Anchor 9
                new (  0.0f,  6.0f,   8.0f),
                new ( -1.0f,  7.0f,   2.0f),

                new ( -2.0f,  8.0f,  -2.0f), // Anchor 10
                new ( -3.0f,  9.0f,  -6.0f),
                new ( -4.0f,  9.0f, -10.0f),

                new ( -5.0f,  8.0f, -14.0f), // Anchor 11
                new ( -6.0f,  6.0f, -17.0f),
                new ( -6.0f,  4.0f, -19.0f),

                new ( -4.0f,  2.0f, -18.0f), // Anchor 12
                new ( -2.0f,  0.0f, -15.0f),
                new (  0.0f, -1.0f, -10.0f),

                new (  2.0f, -2.0f,  -6.0f), // Anchor 13
                new (  4.0f, -2.5f,  -2.0f),
                new (  6.0f, -2.0f,   2.0f),

                new (  7.5f, -1.5f,   6.0f), // Anchor 14
                new (  9.0f, -1.0f,  10.0f),
                new (  9.5f,  0.0f,  14.0f),

                new (  9.0f,  2.0f,  16.0f), // Anchor 15
                new (  8.0f,  4.0f,  17.0f),
                new (  6.0f,  6.0f,  17.0f),

                new (  4.0f,  8.0f,  16.0f), // Anchor 16
                new (  2.0f,  9.0f,  14.0f),
                new (  0.0f, 10.0f,  10.0f),

                new ( -2.0f, 11.0f,   6.0f), // Anchor 17
                new ( -4.0f, 11.5f,   2.0f),
                new ( -6.0f, 11.0f,  -2.0f),

                new ( -8.0f, 10.0f,  -6.0f), // Anchor 18
                new (-10.0f,  9.0f,  -9.0f),
                new (-12.0f,  8.0f, -12.0f),

                new (-14.0f,  7.0f, -14.0f), // Anchor 19
            };

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

            if (points == null! || points.Length < 4)
                return;

            if (SceneEditor.SelectedObject == Owner)
                for (int i = 0; i < path.Length; i++)
                {
                    ImGuizmo.SetGizmoSizeClipSpace(i % 3 == 0 ? 0.1f : 0.05f);
                    ImGuizmo.SetID(100 + i);
                    var bulletMatrix = Camera.Editor.GetProjectionMatrix().ToBulletMatrix();
                    var matrix = Camera.Editor.GetViewMatrix().ToBulletMatrix();
                    var bulletMatrix1 = Matrix4.CreateTranslation(points[i]).ToBulletMatrix();
                    if (ImGuizmo.Manipulate(ref matrix.M11, ref bulletMatrix.M11, ImGuizmoOperation.Translate, ImGuizmoMode.World, ref bulletMatrix1.M11))
                    {
                        path[i] = bulletMatrix1.Decompose(out _, out _, out var pos) ? pos.ToOpenTkVector3() : path[i];
                    }
                }


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

                    LineRenderer.DrawLine(p0, p1, (0, 0, 1, 1), (0, 0, 1, 1));
                    LineRenderer.DrawLine(p0, p1, (0, 0, 1, 1), (0, 0, 1, 1));
                    LineRenderer.DrawLine(p2, p3, (0, 0, 1, 1), (0, 0, 1, 1));


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
                    Camera.Main.Position = path[^1];
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

        /// <inheritdoc />
        public override JsonNode GetSaveData()
        {
            return new JsonObject();
        }

        /// <inheritdoc />
        public void ViewportRender(Vector2 pos, Vector2 size)
        {
            ImGuizmo.SetDrawlist();
            DrawBezierPath(path);
        }
    }
}
