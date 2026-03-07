using System.Diagnostics;
using System.Reflection;
using System.Text;
using RE.Launcher.Game.Console;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var exeName = Assembly.GetExecutingAssembly().GetCustomAttribute<DefaultExeName>()?.ExeName;

if (!File.Exists(args.FirstOrDefault() ?? exeName))
{
    var pName = Process.GetCurrentProcess().ProcessName;
    Console.WriteLine("Usage:");
    Console.WriteLine($"\t{pName} [{exeName}]");
    Console.WriteLine($"\t{pName} <Engine Exe> [args...]");
    return -1;
}

var psi = new ProcessStartInfo
{
    FileName = args.FirstOrDefault() ?? exeName,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    StandardOutputEncoding = new UTF8Encoding(false),
    StandardErrorEncoding = new UTF8Encoding(false),
    Arguments = string.Join(' ', args.Skip(1))
};

using var p = new Process();
p.StartInfo = psi;
p.Start();

ChildProcessTracker.AddProcess(p);

var stdoutTask = PipeAsync(p.StandardOutput, Console.Out);
var stderrTask = PipeAsync(p.StandardError, Console.Error);

await Task.WhenAll(stdoutTask, stderrTask);
await p.WaitForExitAsync();

return p.ExitCode;
//Console.WriteLine($"Exit Code: {p.ExitCode} (0x{p.ExitCode:X})");


static async Task PipeAsync(StreamReader reader, TextWriter writer)
{
    var buffer = new char[4096];
    int read;
    while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        await writer.WriteAsync(buffer, 0, read);
}