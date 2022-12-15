using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets
{
    sealed class ServerExample : IDisposable
    {
        private const int maxSocket = 32;

        private Socket _listenSocket = null;
        private readonly Dictionary<Guid, Socket> _transferSocket = new Dictionary<Guid, Socket>(maxSocket);

        private readonly int _port = 80;
        private readonly IPHostEntry _host = Dns.GetHostEntry(Dns.GetHostName());

        public async void TestServer()
        {
            IPEndPoint endPoint = new IPEndPoint(_host.AddressList[0], _port);

            using (_listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                _listenSocket.Bind(endPoint);
                _listenSocket.Listen(maxSocket);

                // Continuous server connection
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        Result<Socket> listenTask = await ListenAsync().ConfigureAwait(false);
                        if (listenTask.Failure)
                        {
                            throw new Exception(listenTask.Error);
                        }
                        _transferSocket.Add(Guid.NewGuid(), listenTask.Value);
                    }
                });

                // Run ReceiveMessage
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        foreach (var guid in _transferSocket.Keys)
                        {
                            Result<string> receiveTask = await ReceiveMessageAsync(guid).ConfigureAwait(false);
                            if (receiveTask.Failure)
                            {
                                throw new Exception(receiveTask.Error);
                            }
                            Console.WriteLine($"Server: {receiveTask.Value}");
                        }
                    }
                });

                // Run SendMessage
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        Thread.Sleep(100);
                        CommandLine command = new CommandLine("test", new Dictionary<string, string>() { { "server", "say" } });
                        foreach (var guid in _transferSocket.Keys)
                        {
                            Result sendTask = await SendMessageAsync(guid, command).ConfigureAwait(false);
                            if (sendTask.Failure)
                            {
                                throw new Exception(sendTask.Error);
                            }
                        }
                    }
                });

                await Task.Delay(-1);
            }
        }

        private async Task<Result<Socket>> ListenAsync()
        {
            return await _listenSocket!.TListenAsync().ConfigureAwait(false);
        }

        private async Task<Result> SendMessageAsync(Guid id, CommandLine message)
        {
            byte[] data = TaskSocket.Encode(message);
            return await _transferSocket[id]!.TSendAsync(data, 0, data.Length, 0).ConfigureAwait(false);
        }

        private async Task<Result<string>> ReceiveMessageAsync(Guid id)
        {
            byte[] buffer = new byte[TaskSocket.BufferSize];

            Result<int> receiveResult = await _transferSocket[id].TReceiveAsync(buffer, 0, TaskSocket.BufferSize, 0).ConfigureAwait(false);

            return receiveResult.Value == 0
                ? Result<string>.Fail(string.Empty, "Error reading message from client, no data was received")
                : Result<string>.Ok(TaskSocket.Decode<string>(buffer, 0, receiveResult.Value));
        }

        public void Dispose()
        {
            _listenSocket.Dispose();
        }
    }
}