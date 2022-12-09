using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets
{
    internal sealed class ServerExample
    {
        private Socket? _listenSocket = null;
        private readonly List<Socket?> _transferSocket = new List<Socket?>();

        private readonly int _port = 80;
        private readonly IPHostEntry _host = Dns.GetHostEntry(Dns.GetHostName());

        public async void TestServer(int comMax = 32)
        {
            IPEndPoint endPoint = new IPEndPoint(_host.AddressList[0], _port);

            using (_listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                _listenSocket.Bind(endPoint);
                _listenSocket.Listen(comMax);

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
                        Console.WriteLine($"Server income: {listenTask.Success}");
                        _transferSocket.Add(listenTask.Value);
                    }
                });

                // Run ReceiveMessage
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        for (int i = 0; i < _transferSocket.Count; i++)
                        {
                            Result<string> receiveTask = await ReceiveMessageAsync(i).ConfigureAwait(false);
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
                        for (int i = 0; i < _transferSocket.Count; i++)
                        {
                            Result sendTask = await SendMessageAsync(i, command).ConfigureAwait(false);
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

        private async Task<Result> SendMessageAsync(int id, CommandLine message)
        {
            byte[] data = TaskSocket.Encode(message);
            return await _transferSocket[id]!.TSendAsync(data, 0, data.Length, 0).ConfigureAwait(false);
        }

        private async Task<Result<string>> ReceiveMessageAsync(int id)
        {
            byte[] buffer = new byte[TaskSocket.BufferSize];

            Result<int> receiveResult = await _transferSocket[id]!.TReceiveAsync(buffer, 0, TaskSocket.BufferSize, 0).ConfigureAwait(false);

            return receiveResult.Value == 0
                ? Result<string>.Fail(string.Empty, "Error reading message from client, no data was received")
                : Result<string>.Ok(TaskSocket.Decode<string>(buffer, 0, receiveResult.Value));
        }
    }
}