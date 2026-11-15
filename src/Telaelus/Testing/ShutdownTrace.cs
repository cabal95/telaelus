using System;
using System.IO;
using Godot;

namespace Telaelus.Testing;

/// <summary>
/// Breadcrumbs for the way out of a scenario run, written to <c>shutdown.log</c> in its output folder
/// and flushed line by line, so a run that dies while quitting shows how far it got. A line is the
/// milliseconds since the first mark and the step. Only a scenario run starts it.
/// </summary>
public static partial class ShutdownTrace
{
    private static StreamWriter? _log;
    private static readonly System.Diagnostics.Stopwatch Clock = new();

    public static void Start(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        _log = new StreamWriter(Path.Combine(outputDir, "shutdown.log"), append: false)
        {
            AutoFlush = true
        };
        Clock.Restart();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Mark("process exit (.NET)");
        Mark("trace started");
    }

    public static void Mark(string step)
    {
        StreamWriter? log = _log;
        if (log is null)
        {
            return;
        }
        try
        {
            lock (Clock)
            {
                log.WriteLine($"{Clock.ElapsedMilliseconds,8} ms  {step}");
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A node that marks its own leaving the tree and being deleted.</summary>
    public sealed partial class Watcher : Node
    {
        public override void _ExitTree()
        {
            Mark("watcher: exit tree");
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
            {
                Mark("watcher: predelete");
            }
            else if (what == NotificationWMCloseRequest)
            {
                Mark("close requested");
            }
        }
    }
}
