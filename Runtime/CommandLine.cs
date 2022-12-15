using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace System.Net.Sockets
{
    /// <summary>
    /// Command line argument specially designed to be sent by a socket.
    /// </summary>
    public sealed class CommandLine
    {
        #region Fields
        /// <summary>
        /// Name of the command to execute.
        /// </summary>
        public string Command { get; private set; } = string.Empty;
        /// <summary>
        /// Command line argument. Key is parameter, Value is value.
        /// </summary>
        public ReadOnlyDictionary<string, string> Args { get; private set; } = null;
        #endregion

        #region Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        private CommandLine() { }
        /// <summary>
        /// Constructs a new command to sent by a socket.
        /// </summary>
        /// <param name="command">Name of the command to execute.</param>
        /// <param name="args">Command line argument. Key is parameter, Value is value.</param>
        /// <example>new Command("Execute", new Dictionary<string, string>() { { "debug", "false" } })</example>
        public CommandLine(string command, Dictionary<string, string> args = null)
        {
            Command = command;
            Args = args != null && args.Any() ? new ReadOnlyDictionary<string, string>(args) : null;
        }
        #endregion

        #region Parse
        /// <summary>
        /// Convert this object to string.
        /// </summary>
        public override string ToString()
        {
            string utf8 = Command;
            if (Args != null && Args.Any())
            {
                foreach (KeyValuePair<string, string> arg in Args)
                {
                    utf8 += $" --{arg.Key} {arg.Value}";
                }
            }
            return utf8;
        }
        /// <summary>
        /// Convert this object to byte array.
        /// </summary>
        public byte[] ToByte()
        {
            string utf8 = Command;
            if (Args != null && Args.Any())
            {
                foreach (KeyValuePair<string, string> arg in Args)
                {
                    utf8 += $" {arg.Key} {arg.Value}";
                }
            }
            return TaskSocket.DefaultEncoding.GetBytes(utf8);
        }

        /// <summary>
        /// Returns a formatted list from <see cref="Args"/>.
        /// </summary>
        public List<string> Arguments()
        {
            if (Args != null && Args.Any())
            {
                List<string> args = new List<string>(Args.Keys.Count);
                foreach (KeyValuePair<string, string> arg in Args)
                {
                    args.Add($"--{arg.Key} {arg.Value}");
                }
                return args;
            }

            return new List<string>();
        }

        /// <summary>
        /// Convert byte array to <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine Parse(byte[] bytes)
        {
            return bytes is null ? throw new ArgumentNullException(nameof(bytes)) : Parse(TaskSocket.DefaultEncoding.GetString(bytes));
        }
        /// <summary>
        /// Convert byte array to <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine Parse(byte[] bytes, int index, int count)
        {
            return bytes is null
                ? throw new ArgumentNullException(nameof(bytes))
                : Parse(TaskSocket.DefaultEncoding.GetString(bytes, index, count));
        }

        /// <summary>
        /// Convert string to <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine Parse(string str)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            string[] spl = str.Split(' ');
            if (spl.Length < 2)
            {
                return new CommandLine(spl[0], null);
            }
            else
            {
                Dictionary<string, string> args = new Dictionary<string, string>();
                for (int i = 1; i < spl.Length; i += 2)
                {
                    args.Add(spl[i], spl[i + 1]);
                }
                return new CommandLine(spl[0], args);
            }
        }
        #endregion
    }

    public interface ICommand
    {
        CommandLine CommandLine => new CommandLine(Command, Args);

        string Command { get; set; }
        Dictionary<string, string> Args { get; set; }

        void SetCommandLine(string command, Dictionary<string, string> args = null)
        {
            this.Command = command;
            this.Args = args;
        }
        void SetCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            this.Command = command;
        }

        bool AddArgs(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Args.TryAdd(key, value);
        }
        bool UpdateArgs(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (Args.ContainsKey(key))
            {
                Args[key] = value;
                return true;
            }
            return false;
        }
        bool RemoveArgs(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            return Args.Remove(key);
        }
    }

    public interface ICommandSender : ICommand
    {
        event EventHandler Sended;
        void OnSended();
    }

    public interface ICommandReceive : ICommand
    {
        event EventHandler Received;
        void OnReceive();
    }
}