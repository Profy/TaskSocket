using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets
{
    sealed class ClientExample : IDisposable
    {
        private Socket _clientSocket = null;

        private readonly int _port = 80;
        private readonly IPHostEntry _host = Dns.GetHostEntry(Dns.GetHostName());

        public async void TestClient(int id)
        {
            IPEndPoint endPoint = new IPEndPoint(_host.AddressList[0], _port);

            using (_clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                // Connect client
                Result connectTask = await ConnectAsync(endPoint).ConfigureAwait(false);
                if (connectTask.Failure)
                {
                    throw new Exception(connectTask.Error);
                }
                Console.WriteLine($"Client {id}: {connectTask.Success}");

                // Run ReceiveMessage
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        Result<CommandLine> receiveTask = await ReceiveMessageAsync().ConfigureAwait(false);
                        if (receiveTask.Failure)
                        {
                            Console.WriteLine($"Client {id}: {receiveTask.Error}");
                        }
                        Console.WriteLine($"Client {id}: {receiveTask.Value}");
                    }
                });

                // Run SendMessage
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        Thread.Sleep(100);
                        Result sendTask = await SendMessageAsync($"Client {id} say ok").ConfigureAwait(false);
                        if (sendTask.Failure)
                        {
                            Console.WriteLine($"Client {id}: {sendTask.Error}");
                        }
                    }
                });

                await Task.Delay(-1);
            }
        }

        private async Task<Result> ConnectAsync(EndPoint ipEndPoint)
        {
            return await _clientSocket!.TConnectAsync(ipEndPoint).ConfigureAwait(false);
        }

        private async Task<Result> SendMessageAsync(string message)
        {
            byte[] data = TaskSocket.Encode(message);
            return await _clientSocket!.TSendAsync(data, 0, data.Length, 0).ConfigureAwait(false);
        }

        private async Task<Result<CommandLine>> ReceiveMessageAsync()
        {
            byte[] buffer = new byte[TaskSocket.BufferSize];

            Result<int> receiveResult = await _clientSocket!.TReceiveAsync(buffer, 0, TaskSocket.BufferSize, 0).ConfigureAwait(false);

            return receiveResult.Failure
                ? Result<CommandLine>.Fail(default, receiveResult.Error)
                : receiveResult.Value == 0
                ? Result<CommandLine>.Fail(default, "Error reading message from client, no data was received")
                : Result<CommandLine>.Ok(TaskSocket.Decode<CommandLine>(buffer, 0, receiveResult.Value));
        }

        public void Dispose()
        {
            _clientSocket.Dispose();
        }
    }
}