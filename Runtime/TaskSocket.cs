using System.Text;
using System.Threading.Tasks;

namespace System.Net.Sockets
{
    /// <summary>
    /// Socket with Task Parallel Library.
    /// </summary>
    public static class TaskSocket
    {
        #region Constants
        /// <summary>
        /// Default <see cref="TListenTimeoutAsync(Socket, int)"/> timeout value in miliseconds.
        /// </summary>
        public const int ListenTimeoutMs = 3000;
        /// <summary>
        /// Default <see cref="TConnectTimeoutAsync(Socket, EndPoint, int)"/> timeout value in miliseconds.
        /// </summary>
        public const int ConnectTimeoutMs = 3000;
        /// <summary>
        /// Default <see cref="TReceiveTimeoutAsync(Socket, byte[], int, int, SocketFlags, int)"/> timeout value in miliseconds.
        /// </summary>
        public const int ReceiveTimeoutMs = 3000;
        /// <summary>
        /// Default <see cref="TSendTimeoutAsync(Socket, byte[], int, int, SocketFlags, int)"/> timeout value in miliseconds.
        /// </summary>
        public const int SendTimeoutMs = 3000;
        #endregion

        #region Message
        /// <summary>
        /// Socket message data buffer size.
        /// </summary>
        public static int BufferSize { get; private set; } = 1024;
        /// <summary>
        /// Set the default message data buffer size.
        /// </summary>
        public static void SetBufferSize(int size)
        {
            if ((size < 8 || size > ushort.MaxValue))
            {
                throw new NotSupportedException("Size of the buffer must be between 8 and 65535");
            }
            BufferSize = size;
        }

        /// <summary>
        /// Default string encoding used to encode/decode messages.
        /// </summary>
        public static Encoding DefaultEncoding { get; private set; } = Encoding.UTF8;
        /// <summary>
        /// Set the default string encoding used to encode/decode messages.
        /// </summary>
        public static void SetDefaultEncoding(Encoding encoding)
        {
            DefaultEncoding = encoding;
        }
        /// <summary>
        /// Encode a string message to byte array.
        /// </summary>
        public static byte[] Encode(string message)
        {
            return DefaultEncoding.GetBytes(message);
        }
        /// <summary>
        /// Encode a commande line message to byte array.
        /// </summary>
        public static byte[] Encode(CommandLine message)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return message.ToByte();
        }

        /// <summary>
        /// Decode a byte array message in <see cref="T"/> type.
        /// </summary>
        public static T Decode<T>(byte[] message)
        {
            return typeof(T) == typeof(byte[])
                ? (T)(object)message
                : typeof(T) == typeof(string)
                    ? (T)(object)DefaultEncoding.GetString(message)
                    : typeof(T) == typeof(CommandLine)
                                    ? (T)(object)CommandLine.Parse(message)
                                    : throw new NotSupportedException($"{typeof(T)} not supported. Supported type: {typeof(string)} {typeof(byte[])} {typeof(CommandLine)}");
        }
        /// <summary>
        /// Decode a byte array message in <see cref="T"/> type.
        /// </summary>
        public static T Decode<T>(byte[] message, int index, int count)
        {
            return typeof(T) == typeof(byte[])
                ? (T)(object)message
                : typeof(T) == typeof(string)
                    ? (T)(object)DefaultEncoding.GetString(message, index, count)
                    : typeof(T) == typeof(CommandLine)
                                    ? (T)(object)CommandLine.Parse(message, index, count)
                                    : throw new NotSupportedException($"{typeof(T)} not supported. Supported type: {typeof(string)} {typeof(byte[])} {typeof(CommandLine)}");
        }
        #endregion

        #region Socket operation
        /// <summary>
        /// Start server listening accept connection. Once connected, use the socket transmitted in the <see cref="Result"/> to communicate with the client.
        /// </summary>
        /// <param name="socket">Server socket.</param>
        /// <returns>The socket that is used to transmit data to the client.</returns>
        public static async Task<Result<Socket>> TListenAsync(this Socket socket)
        {
            Socket transferSocket = null;
            try
            {
                using (Task<Socket> acceptTask = Task<Socket>.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null))
                {
                    transferSocket = await acceptTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<Socket>.Fail(socket, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<Socket>.Ok(transferSocket);
        }
        /// <summary>
        /// Start server listening accept connection. Once connected, use the socket transmitted in the <see cref="Result"/> to communicate with the client.
        /// </summary>
        /// <param name="socket">Server socket.</param>
        /// <returns>The socket that is used to transmit data to the client.</returns>
        public static async Task<Result<Socket>> TListenTimeoutAsync(this Socket socket, int timeoutMs = ListenTimeoutMs)
        {
            if (timeoutMs < 1)
            {
                return await TListenAsync(socket).ConfigureAwait(false);
            }

            Socket transferSocket = null;
            try
            {
                Task<Socket> acceptTask = Task<Socket>.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);

                if (acceptTask != await Task.WhenAny(acceptTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    throw new TimeoutException();
                }

                transferSocket = await acceptTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<Socket>.Fail(socket, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<Socket>.Ok(transferSocket);
        }

        /// <summary>
        /// Start client connection.
        /// </summary>
        /// <param name="socket">Client socket.</param>
        /// <param name="endpoint">Server network adress.</param>
        public static async Task<Result> TConnectAsync(this Socket socket, EndPoint endpoint)
        {
            try
            {
                using (Task connectTask = Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null))
                {
                    await connectTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result.Fail($"{ex.Message} ({ex.GetType()})");
            }

            return Result.Ok();
        }
        /// <summary>
        /// Start client connection.
        /// </summary>
        /// <param name="socket">Client socket.</param>
        /// <param name="endpoint">Server network adress.</param>
        public static async Task<Result> TConnectTimeoutAsync(this Socket socket, EndPoint endpoint, int timeoutMs = ConnectTimeoutMs)
        {
            if (timeoutMs < 1)
            {
                return await TConnectAsync(socket, endpoint).ConfigureAwait(false);
            }

            try
            {
                Task connectTask = Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null);
                if (connectTask != await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    throw new TimeoutException();
                }

                await connectTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result.Fail($"{ex.Message} ({ex.GetType()})");
            }

            return Result.Ok();
        }

        /// <summary>
        /// Start listening to retrieve data.
        /// </summary>
        /// <param name="socket">Server or Client socket.</param>
        /// <param name="buffer">An array of type Byte that is the storage location for the received data.</param>
        /// <param name="offset">The zero-based position in the buffer parameter at which to store the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="socketFlags">A bitwise combination of the SocketFlags values.</param>
        /// <returns>The result of the communication. If the communication was successful, the result contains the number of bytes received.</returns>
        public static async Task<Result<int>> TReceiveAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            int bytes = 0;
            try
            {
                IAsyncResult asyncResult = socket.BeginReceive(buffer, offset, size, socketFlags, null, null);
                using (Task<int> receiveTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult)))
                {
                    bytes = await receiveTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<int>.Fail(bytes, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<int>.Ok(bytes);
        }
        /// <summary>
        /// Start listening to retrieve data.
        /// </summary>
        /// <param name="socket">Server or Client socket.</param>
        /// <param name="buffer">An array of type Byte that is the storage location for the received data.</param>
        /// <param name="offset">The zero-based position in the buffer parameter at which to store the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="socketFlags">A bitwise combination of the SocketFlags values.</param>
        /// <returns>The result of the communication. If the communication was successful, the result contains the number of bytes received.</returns>
        public static async Task<Result<int>> TReceiveTimeoutAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags, int timeoutMs = ReceiveTimeoutMs)
        {
            if (timeoutMs < 1)
            {
                return await TReceiveAsync(socket, buffer, offset, size, socketFlags).ConfigureAwait(false);
            }

            int bytes = 0;
            try
            {
                IAsyncResult asyncResult = socket.BeginReceive(buffer, offset, size, socketFlags, null, null);
                Task<int> receiveTask = Task.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));
                if (receiveTask != await Task.WhenAny(receiveTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    throw new TimeoutException();
                }

                bytes = await receiveTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<int>.Fail(bytes, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<int>.Ok(bytes);
        }

        /// <summary>
        /// Start com to send data.
        /// </summary>
        /// <param name="socket">Server or Client socket.</param>
        /// <param name="buffer">An array of type Byte that contains the data to send.</param>
        /// <param name="offset">The zero-based position in the buffer parameter at which to begin sending data.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="socketFlags">A bitwise combination of the SocketFlags values.</param>
        /// <returns>The result of the communication. If the communication was successful, the result contains the number of bytes sended.</returns>
        public static async Task<Result<int>> TSendAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            int bytes = 0;
            try
            {
                IAsyncResult asyncResult = socket.BeginSend(buffer, offset, size, socketFlags, null, null);
                using (Task<int> sendTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult)))
                {
                    bytes = await sendTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<int>.Fail(bytes, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<int>.Ok(bytes);
        }
        /// <summary>
        /// Start com to send data.
        /// </summary>
        /// <param name="socket">Server or Client socket.</param>
        /// <param name="buffer">An array of type Byte that contains the data to send.</param>
        /// <param name="offset">The zero-based position in the buffer parameter at which to begin sending data.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="socketFlags">A bitwise combination of the SocketFlags values.</param>
        /// <returns>The result of the communication. If the communication was successful, the result contains the number of bytes sended.</returns>
        public static async Task<Result<int>> TSendTimeoutAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags, int timeoutMs = SendTimeoutMs)
        {
            if (timeoutMs < 1)
            {
                return await TSendAsync(socket, buffer, offset, size, socketFlags).ConfigureAwait(false);
            }

            int bytes = 0;
            try
            {
                IAsyncResult asyncResult = socket.BeginSend(buffer, offset, size, socketFlags, null, null);
                Task<int> sendTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));
                if (sendTask != await Task.WhenAny(sendTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    throw new TimeoutException();
                }

                bytes = await sendTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<int>.Fail(bytes, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<int>.Ok(bytes);
        }

        /// <summary>
        /// Start com to send a file.
        /// </summary>
        /// <param name="socket">Server or Client socket.</param>
        /// <param name="filePath">A string that contains the path and name of the file to send. This parameter can be null.</param>
        /// <returns>The result of the communication. If the communication was successful, the result contains the number of bytes sended.</returns>
        public static async Task<Result<int>> TSendFileAsync(this Socket socket, string filePath)
        {
            int bytes = 0;
            try
            {
                IAsyncResult asyncResult = socket.BeginSendFile(filePath, null, null);
                using (Task<int> sendTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult)))
                {
                    bytes = await sendTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<int>.Fail(bytes, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<int>.Ok(bytes);
        }
        /// <summary>
        /// Start com to send a file.
        /// </summary>
        /// <param name="socket">Server or Client socket.</param>
        /// <param name="filePath">A string that contains the path and name of the file to send. This parameter can be null.</param>
        /// <returns>The result of the communication. If the communication was successful, the result contains the number of bytes sended.</returns>
        public static async Task<Result<int>> TSendFileTimeoutAsync(this Socket socket, string filePath, int timeoutMs = SendTimeoutMs)
        {
            if (timeoutMs < 1)
            {
                return await TSendFileAsync(socket, filePath).ConfigureAwait(false);
            }

            int bytes = 0;
            try
            {
                IAsyncResult asyncResult = socket.BeginSendFile(filePath, null, null);
                Task<int> sendTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));
                if (sendTask != await Task.WhenAny(sendTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    throw new TimeoutException();
                }

                bytes = await sendTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException or TimeoutException)
            {
                return Result<int>.Fail(bytes, $"{ex.Message} ({ex.GetType()})");
            }

            return Result<int>.Ok(bytes);
        }
        #endregion
    }
}