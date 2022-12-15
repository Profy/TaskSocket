using System.Collections.Generic;

namespace System.Net.Sockets
{
    sealed class CommandSendExample : ICommandSender // Comment to hide command in task socket editor tools.
    {
        public string Command { get; set; } = "CommandToExecute";
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>() { { "a", "value" }, { "b", "value" }, { "c", "value" } };

        public event EventHandler Sended;
        public void OnSended()
        {
            Sended?.Invoke(this, EventArgs.Empty);
        }
    }

    sealed class CommandReceiveExample : ICommandReceive // Comment to hide command in task socket editor tools.
    {
        public string Command { get; set; } = "CommandToExecute";
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>() { { "a", "value" }, { "b", "value" }, { "c", "value" } };

        public event EventHandler Received;
        public void OnReceive()
        {
            Received?.Invoke(this, EventArgs.Empty);
        }
    }
}