using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Audio;
using RE.Rendering;
using Serilog;
using static ImGuiNET.ImGui;

namespace RE.Debug.Overlay;

internal class DebugOverlay : Renderable
{

    private DebugOverlay()
    {
        RenderLayerManager.AddRenderable(this);
    }

    public static DebugOverlay? Instance { get; private set; }
    public override RenderLayer RenderLayer => RenderLayer.ImGui;
    public override bool IsVisible { get; set; } = true;

    public override void Render(FrameEventArgs args)
    {
        Begin("123");
        if (Button("gc")) GC.Collect();
        var instance = Camera.Instance;
        Text($"Cam pos: ({instance.Position.X:F}; {instance.Position.Y:F}; {instance.Position.Z:F})");
        if (Button("1")) RenderLayerManager.RemoveRenderables<LineManager>();
        //Text($"a: ({LineManager.a.X:F}, {LineManager.a.Y:F})");
        Selectable("wireframe", ref w);
        if (w)
        {
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
        }
        else
        {
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        }
        if (Button("do_shit()"))
        {
            var start = Vector3.Zero;
            for (var i = 0; i < 50000; i++)
            {
                var end = start + new Vector3(Random.Shared.NextSingle() * .1f, Random.Shared.NextSingle() * .1f,
                    Random.Shared.NextSingle() * .1f);
                LineManager.Main.AddLine(start, end, new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 1), 2000);
                start = end;
            }
        }

        if (Button("play random"))
        {
            var soundId = SoundManager.SoundMap.ToList()[Random.Shared.Next(SoundManager.SoundMap.Count)];
            SoundManager.Play(soundId.Key, new() { Volume = .2f });
        }

        //Text((1 / args.Time).ToString("F2") + " FPS\n" +
        //     $"DeltaTime: {Time.DeltaTime:F3} s\n" +
        //     $"Time: {Time.ElapsedTime:F3} s\n" +
        //     $"Camera Position: {Camera.Instance.Position.X:F2}, {Camera.Instance.Position.Y:F2}, {Camera.Instance.Position.Z:F2}" +
        //     $"\nAverage 5sFPS: {Game.A:F2}");
        End();

        if (Begin("test"))
        {
            BeginDisabled(s != null!);
            if (Button("init"))
            {
                s = SoundManager.Get("ambience/wind");
                s.Position = Camera.Instance.Position;
                s.Volume = .2f;
                s.Stopped += () => Log.Information("Sound stopped");
                s.Paused += () => Log.Information("Sound paused");
                s.Playing += () => Log.Information("Sound playing");
                s.Resumed += () => Log.Information("Sound resumed");
                Log.Debug($"{s.Source} p:{s.Pitch} l:{s.Length} v:{s.Volume}");
            }
            EndDisabled();
            Text($"{MathF.Round(s?.Offset ?? 0, 1):F1}/{MathF.Round(s?.Length ?? 0, 1):F1}");

            BeginDisabled(s == null);
            SameLine();
            if (Button("play"))
                s.Play();
            SameLine();
            if (Button("stop"))
                s.Stop();
            SameLine();
            if (Button("pause"))
                s.Pause();
            SameLine();
            if (Button("resume"))
                s.Resume();
            Checkbox("Loop", ref l);
            SameLine();
            if (Button("upd")) s.Loop = l;
            Text($"vol: {s?.Volume:F3}");
            if (SliderFloat("max distance", ref m, 0, 20))
            {
                s.MaxDistance = m;
            }
            if (SliderFloat("ref distance", ref f, 0, 20))
            {
                s.ReferenceDistance = f;
            }
            EndDisabled();
        }

        End();

        Begin("Sound Player");
        Text($"{SoundManager.SoundMap.Count} sound IDs");
        if (SoundManager.ActiveSounds.Count > 0)
        {
            SameLine();
            Text($"{SoundManager.ActiveSounds.Count(s => s.IsPlaying && !s.IsPaused)}/{SoundManager.ActiveSounds.Count} active sounds.");
        }
        if (Button("Stop"))
            SoundManager.StopAll();
        InputText("Search...", ref search, 256);
        Separator();
        BeginChild("Sounds");
        foreach (var sound in SoundManager.SoundMap.Where(s => s.Key.Contains(search)))
        {
            if (Button(sound.Key))
            {
                SoundManager.Play(sound.Key, new() { Volume = .2f, InWorld = true, MaxDistance = 5, ReferenceDistance = 1 });
            }

            if (sound.Value.Count > 1)
            {
                for (int i = 0; i < sound.Value.Count; i++)
                {
                    SameLine();
                    if (Button(i.ToString()))
                        SoundManager.Play(sound.Key, new() { VariantIndex = i, Volume = .2f });
                }
            }
        }
        EndChild();
        End();
    }

    private float m = 10, f = 1;
    private bool w;
    private string search = "";
    private bool l;
    private Sound s;
    public static void Init()
    {
        Instance ??= new DebugOverlay();
    }
}