using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using UnityEditor;

using UnityEngine;

public class TaskSocketEditor : EditorWindow
{
    private const int maxSocket = 32;

    // Host
    private readonly IPHostEntry _host = Dns.GetHostEntry(Dns.GetHostName());
    private string hostName = string.Empty;
    private string adress = string.Empty;
    private int port = 0;

    // Server socket
    private Socket _server = null;
    private readonly Dictionary<Guid, Socket> _transferSocket = new Dictionary<Guid, Socket>(maxSocket);
    private bool _running = false;

    // Async com
    private readonly CancellationTokenSource cts = new CancellationTokenSource();

    // Command
    private class CommandGUI
    {
        internal ICommandSender sender = null;
        internal bool foldout = true;

        public CommandGUI(ICommandSender sender)
        {
            this.sender = sender;
        }
        public CommandGUI(ICommandSender sender, bool foldout) : this(sender)
        {
            this.foldout = foldout;
        }
    }
    private CommandGUI[] _commands = new CommandGUI[0];

    // Log
    private class LogGUI
    {
        internal List<string> log = new List<string>();
        internal bool foldout = true;

        public LogGUI() { }
        public LogGUI(List<string> log)
        {
            this.log = log;
        }
        public LogGUI(List<string> log, bool foldout) : this(log)
        {
            this.foldout = foldout;
        }
    }
    private readonly Dictionary<Guid, LogGUI> _receiveLog = new Dictionary<Guid, LogGUI>(maxSocket);

    // Event
    private bool startRuntime = true;
    private bool closeRuntime = true;

    // GUI
    private readonly Regex _camelCase = new Regex("(\\B[A-Z])", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    [MenuItem("Tools/Socket")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(TaskSocketEditor));
    }

    private void OnEnable()
    {
        GetCommandList();

        AssemblyReloadEvents.afterAssemblyReload += GetCommandList;
        EditorApplication.playModeStateChanged += AutoServer;
    }

    private void OnDisable()
    {
        _commands = Array.Empty<CommandGUI>();

        AssemblyReloadEvents.afterAssemblyReload -= GetCommandList;
        EditorApplication.playModeStateChanged -= AutoServer;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Connections are automatically closed and the server stopped if this window is closed.", MessageType.Info);
        SettingsGUI();
        EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);

        EditorGUILayout.HelpBox(_commands.Length > 0 ? "The commands below are not serialized. They are only dedicated to debugging. To modify a command, modify its original class." : "To add commands to the list, see CommandExample.cs", MessageType.Info);
        CommandsGUI();
        EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);

        ServerGUI();
        EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);

        ConnectionGUI();
    }

    private void OnDestroy()
    {
        EditorApplication.playModeStateChanged -= AutoServer;

        if (_running)
        {
            CloseServer();
        }
    }

    /// <summary>
    /// Server settings.
    /// </summary>
    private void SettingsGUI()
    {
        EditorGUILayout.LabelField("Server Settings", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(_running);
        EditorGUILayout.BeginHorizontal();
        Label("Host name"); hostName = EditorGUILayout.TextField(string.IsNullOrWhiteSpace(hostName) ? _host.HostName : hostName);
        EditorGUILayout.Space(10);
        Label("Adresse"); adress = EditorGUILayout.TextField(string.IsNullOrWhiteSpace(adress) ? _host.AddressList[0].ToString() : adress);
        Label(":"); port = EditorGUILayout.IntField(port < 1 ? 80 : port);
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
    }
    /// <summary>
    /// Command list.
    /// </summary>
    private void CommandsGUI()
    {
        for (int i = 0; i < _commands.Length; i++)
        {
            _commands[i].foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_commands[i].foldout, _camelCase.Replace(_commands[i].sender.Command, " $1"));
            if (_commands[i].foldout)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                Label("Command"); _commands[i].sender.Command = EditorGUILayout.TextField(_commands[i].sender.Command);
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                Label("Arguments");
                foreach (string key in _commands[i].sender.Args.Keys.ToList())
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.TextField(key);
                    _commands[i].sender.Args[key] = EditorGUILayout.TextField(_commands[i].sender.Args[key]);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button($"Send \"{_commands[i].sender.CommandLine}\""))
                {
                    if (!_running)
                    {
                        Debug.LogWarning("Operation cancel. No client connected.");
                        return;
                    }

                    // Send message to all clients.
                    var sending = _commands[i].sender;
                    foreach (var socket in _transferSocket)
                    {
                        _ = Task.Run(async () =>
                        {
                            if (cts.Token.IsCancellationRequested)
                            {
                                cts.Token.ThrowIfCancellationRequested();
                            }

                            byte[] data = TaskSocket.Encode(sending.CommandLine);
                            Result sendTask = await socket.Value.TSendAsync(data, 0, data.Length, 0).ConfigureAwait(false);
                            if (sendTask.Failure)
                            {
                                CloseTransfert(socket.Key);
                                throw new Exception(sendTask.Error);
                            }
                            sending.OnSended();
                        }, cts.Token);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
    /// <summary>
    /// Start/Stop server.
    /// </summary>
    private void ServerGUI()
    {
        EditorGUILayout.BeginHorizontal();
        Label("Start at runtime"); startRuntime = EditorGUILayout.Toggle(startRuntime);
        Label("Auto closing when application quit"); closeRuntime = EditorGUILayout.Toggle(closeRuntime);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (!_running)
        {
            if (GUILayout.Button("Start server"))
            {
                StartServer();
            }
        }
        else
        {
            if (GUILayout.Button("Close server"))
            {
                CloseServer();
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    /// <summary>
    /// Server connection state.
    /// </summary>
    private void ConnectionGUI()
    {
        if (_running)
        {
            EditorGUILayout.LabelField($"Active socket: {_transferSocket.Count}", EditorStyles.boldLabel);
            foreach (var socket in _transferSocket)
            {
                _receiveLog[socket.Key].foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_receiveLog[socket.Key].foldout, socket.Key.ToString());
                if (_receiveLog[socket.Key].foldout)
                {
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.TextArea(string.Join(System.Environment.NewLine, _receiveLog[socket.Key].log));
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }
    }

    private void StartServer()
    {
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(adress), port);

        _server = new Socket(SocketType.Stream, ProtocolType.Tcp);
        _server.Bind(endPoint);
        _server.Listen(maxSocket);

        // Continuous server connection
        _ = Task.Run(async () =>
        {
            while (true)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    cts.Token.ThrowIfCancellationRequested();
                }

                Result<Socket> listenTask = await _server.TListenAsync().ConfigureAwait(false);
                if (listenTask.Failure)
                {
                    throw new Exception(listenTask.Error);
                }
                else
                {
                    Guid id = Guid.NewGuid();
                    _transferSocket.Add(id, listenTask.Value);
                    _receiveLog.Add(id, new LogGUI());
                }
            }
        }, cts.Token);

        // Continuous message reception
        _ = Task.Run(async () =>
        {
            while (true)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    cts.Token.ThrowIfCancellationRequested();
                }

                foreach (var socket in _transferSocket)
                {
                    byte[] buffer = new byte[TaskSocket.BufferSize];

                    Result<int> receiveResult = await socket.Value.TReceiveAsync(buffer, 0, TaskSocket.BufferSize, 0).ConfigureAwait(false);
                    if (receiveResult.Value == 0)
                    {
                        throw new Exception("Error reading message from client, no data was received");
                    }
                    else
                    {
                        if (_receiveLog.TryGetValue(socket.Key, out LogGUI logs))
                        {
                            logs.log.Add(TaskSocket.Decode<string>(buffer, 0, receiveResult.Value));
                            continue;
                        }
                        else if (_receiveLog.TryAdd(socket.Key, new LogGUI(new List<string>() { TaskSocket.Decode<string>(buffer, 0, receiveResult.Value) })))
                        {
                            continue;
                        }
                    }
                }
            }
        }, cts.Token);

        _running = true;
    }
    private void CloseServer()
    {
        cts.Cancel();

        // Transferts
        foreach (var socket in _transferSocket)
        {
            try
            {
                socket.Value.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                socket.Value.Close();
            }
        }
        _transferSocket.Clear();
        _receiveLog.Clear();

        // Server connection
        if (_server != null)
        {
            try
            {
                _server.Close();
            }
            finally
            {
                _server.Dispose();
            }
        }

        _running = false;
    }

    private bool CloseTransfert(Guid id)
    {
        if (_transferSocket.TryGetValue(id, out var socket))
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                socket.Close();
            }
            _transferSocket.Remove(id);
            _receiveLog.Remove(id);
            return true;
        }

        return false;
    }

    private void AutoServer(PlayModeStateChange state)
    {
        if (startRuntime && state == PlayModeStateChange.EnteredPlayMode && !_running)
        {
            StartServer();
        }
        if (closeRuntime && state == PlayModeStateChange.ExitingPlayMode && _running)
        {
            CloseServer();
        }
    }

    private void GetCommandList()
    {
        IEnumerable<Type> scripts = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(t => t.GetInterfaces().Contains(typeof(ICommandSender)));
        _commands = (scripts.Select(_type => new CommandGUI((ICommandSender)Activator.CreateInstance(_type), true)).ToArray());
    }

    #region Helper
    private static void Label(string str)
    {
        GUIContent label = new GUIContent(str);
        EditorGUILayout.LabelField(label, GUILayout.Width(GUI.skin.label.CalcSize(label).x));
    }
    #endregion
}