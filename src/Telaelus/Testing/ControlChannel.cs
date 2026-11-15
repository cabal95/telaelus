using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Telaelus.Presentation;
using Godot;

namespace Telaelus.Testing;

/// <summary>
/// A debug-only HTTP endpoint on 127.0.0.1 that lets an agent drive the running game
/// with the scenario command set, one request at a time. A background thread accepts
/// connections (a loopback TcpListener with a minimal HTTP/1.1 parser, so no URL
/// reservation or admin rights are needed) and queues each request; this node, which
/// runs with ProcessMode.Always so it keeps working while the game is paused, runs
/// them on the main thread with the shared <see cref="CommandExecutor"/>.
///
/// Endpoints: GET /status, GET /snapshot, POST /commands (one step object or an array
/// of them), POST /quit. Every request is logged to session.json in the run folder.
/// </summary>
public partial class ControlChannel : Node
{
    private sealed class Request
    {
        public required string Method { get; init; }
        public required string Path { get; init; }
        public required string Body { get; init; }
        public TaskCompletionSource<(int Status, string Body)> Response { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private AppRoot _root = null!;
    private TestArgs _args = null!;
    private CommandExecutor _executor = null!;
    private string _outputDir = "";
    private TcpListener? _listener;
    private Thread? _acceptThread;
    private readonly ConcurrentQueue<Request> _queue = new();
    private readonly JsonArray _session = new();

    // The /commands request in progress.
    private Request? _current;
    private List<ScenarioCommand> _commands = new();
    private int _next;
    private JsonArray _results = new();
    private bool _allPassed;
    private string? _error;

    private volatile bool _quitResponseSent;
    private bool _quitting;

    /// <summary>Attaches the channel if the command line asks for one (debug builds only).</summary>
    public static void AttachIfRequested(AppRoot root)
    {
        TestArgs args = TestArgs.Parse();
        if (args.ControlPort is null)
        {
            return;
        }
        if (!OS.IsDebugBuild())
        {
            GD.PushWarning("--control-port is ignored outside debug builds.");
            return;
        }
        if (args.ScenarioPath is not null)
        {
            GD.PushWarning("--control-port is ignored when --scenario is given.");
            return;
        }

        var channel = new ControlChannel
        {
            Name = "ControlChannel",
            ProcessMode = ProcessModeEnum.Always,
            ProcessPriority = 100,
            _root = root,
            _args = args
        };
        root.AddChild(channel);
    }

    public override void _Ready()
    {
        _outputDir = TestPaths.RunFolder(_args.OutputDir, "live");
        bool screenshots = !_args.NoScreenshots && !TestArgs.IsHeadless;
        _executor = new CommandExecutor(_root, _outputDir, screenshots);
        if (_args.StartPaused)
        {
            GetTree().Paused = true;
        }

        int port = _args.ControlPort!.Value;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "ControlChannel"
        };
        _acceptThread.Start();
        GD.Print($"CONTROL listening on http://127.0.0.1:{port} (output {_outputDir})");
    }

    public override void _ExitTree()
    {
        _listener?.Stop();
    }

    public override void _Process(double delta)
    {
        _executor.BeginFrame();

        if (_quitting)
        {
            if (_quitResponseSent)
            {
                GetTree().Quit(0);
            }
            return;
        }

        if (_current is not null)
        {
            ContinueCommands();
            return;
        }

        if (_queue.TryDequeue(out Request? request))
        {
            Handle(request);
        }
    }

    // ---- Main-thread request handling ---------------------------------

    private void Handle(Request request)
    {
        switch ((request.Method, request.Path))
        {
            case ("GET", "/status"):
                Complete(request, 200, new JsonObject
                {
                    ["ready"] = true,
                    ["paused"] = _executor.Paused,
                    ["frame"] = _executor.Frame,
                    ["output"] = _outputDir
                });
                break;
            case ("GET", "/snapshot"):
                Complete(request, 200, StateSnapshot.Build(_root, _executor));
                break;
            case ("POST", "/quit"):
                _quitting = true;
                Complete(request, 200, new JsonObject
                {
                    ["quitting"] = true
                });
                break;
            case ("POST", "/commands"):
                StartCommands(request);
                break;
            default:
                Complete(request, 404, new JsonObject
                {
                    ["error"] = $"no endpoint {request.Method} {request.Path}"
                });
                break;
        }
    }

    private void StartCommands(Request request)
    {
        var commands = new List<ScenarioCommand>();
        try
        {
            using JsonDocument doc = JsonDocument.Parse(request.Body);
            JsonElement root = doc.RootElement;
            IEnumerable<JsonElement> steps = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray()
                : new[]
                {
                    root
                };
            int index = 0;
            foreach (JsonElement step in steps)
            {
                try
                {
                    commands.AddRange(ScenarioCommand.Parse(step));
                }
                catch (FormatException ex)
                {
                    throw new FormatException($"step {index}: {ex.Message}");
                }
                index++;
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            // Nothing from an invalid request runs.
            Complete(request, 400, new JsonObject
            {
                ["error"] = ex.Message
            });
            return;
        }

        _current = request;
        _commands = commands;
        _next = 0;
        _results = new JsonArray();
        _allPassed = true;
        _error = null;
        ContinueCommands();
    }

    private void ContinueCommands()
    {
        while (_error is null && !_executor.Busy && _next < _commands.Count)
        {
            int index = _next++;
            ScenarioCommand command = _commands[index];
            var result = new JsonObject
            {
                ["index"] = index,
                ["command"] = command.Name,
                ["input"] = command.Raw.DeepClone()
            };
            try
            {
                StepOutcome outcome = command.Run(_executor);
                result["outcome"] = outcome.Passed ? "ok" : "failed";
                if (outcome.Message is not null)
                {
                    result["message"] = outcome.Message;
                }
                if (outcome.Detail is JsonObject detail)
                {
                    if (detail["file"] is JsonNode file)
                    {
                        detail["path"] = Path.Combine(_executor.OutputDir, file.GetValue<string>());
                    }
                    result["detail"] = detail;
                }
                _allPassed &= outcome.Passed;
            }
            catch (Exception ex)
            {
                // A runtime failure (for example an invalid placement) stops the rest of the request.
                result["outcome"] = "error";
                result["message"] = ex.Message;
                _error = $"command {index} ({command.Name}) threw: {ex.Message}";
            }
            _results.Add(result);
        }

        if (_error is not null || (!_executor.Busy && _next >= _commands.Count))
        {
            Request request = _current!;
            _current = null;
            Complete(request, 200, new JsonObject
            {
                ["passed"] = _allPassed && _error is null,
                ["error"] = _error,
                ["results"] = _results,
                ["snapshot"] = StateSnapshot.Build(_root, _executor)
            });
        }
    }

    private void Complete(Request request, int status, JsonObject body)
    {
        string json = body.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });

        JsonNode? requestBody;
        try
        {
            requestBody = request.Body.Length > 0 ? JsonNode.Parse(request.Body) : null;
        }
        catch (JsonException)
        {
            requestBody = request.Body;
        }
        _session.Add(new JsonObject
        {
            ["time"] = DateTime.Now.ToString("HH:mm:ss.fff"),
            ["method"] = request.Method,
            ["path"] = request.Path,
            ["request"] = requestBody,
            ["status"] = status,
            ["response"] = JsonNode.Parse(json)
        });
        Directory.CreateDirectory(_outputDir);
        File.WriteAllText(Path.Combine(_outputDir, "session.json"), _session.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        request.Response.SetResult((status, json));
    }

    // ---- Background thread: HTTP over a loopback socket ---------------

    private void AcceptLoop()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = _listener!.AcceptTcpClient();
            }
            catch (Exception)
            {
                return; // Listener stopped.
            }

            using (client)
            {
                try
                {
                    Serve(client);
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"Control channel connection failed: {ex.Message}");
                }
            }
        }
    }

    private void Serve(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        string requestLine = ReadLine(stream);
        string[] parts = requestLine.Split(' ');
        if (parts.Length < 2)
        {
            Write(stream, 400, "{\"error\":\"bad request line\"}");
            return;
        }

        int contentLength = 0;
        string header;
        while ((header = ReadLine(stream)).Length > 0)
        {
            int colon = header.IndexOf(':');
            if (colon > 0 && header[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                contentLength = int.Parse(header[(colon + 1)..].Trim());
            }
        }

        byte[] body = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int n = stream.Read(body, read, contentLength - read);
            if (n == 0)
            {
                break;
            }
            read += n;
        }

        string path = parts[1];
        int query = path.IndexOf('?');
        if (query >= 0)
        {
            path = path[..query];
        }

        var request = new Request
        {
            Method = parts[0].ToUpperInvariant(),
            Path = path,
            Body = Encoding.UTF8.GetString(body, 0, read)
        };
        _queue.Enqueue(request);
        (int status, string json) = request.Response.Task.GetAwaiter().GetResult();
        Write(stream, status, json);
        if (request.Path == "/quit")
        {
            _quitResponseSent = true;
        }
    }

    private static string ReadLine(NetworkStream stream)
    {
        var bytes = new List<byte>();
        while (true)
        {
            int b = stream.ReadByte();
            if (b < 0 || b == '\n')
            {
                break;
            }
            if (b != '\r')
            {
                bytes.Add((byte)b);
            }
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static void Write(NetworkStream stream, int status, string json)
    {
        string reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            _ => "Error"
        };
        byte[] body = Encoding.UTF8.GetBytes(json + "\n");
        string head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json\r\n"
            + $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        byte[] headBytes = Encoding.ASCII.GetBytes(head);
        stream.Write(headBytes, 0, headBytes.Length);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }
}
