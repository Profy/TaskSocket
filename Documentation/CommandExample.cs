using System.Collections.Generic;

namespace System.Net.Sockets
{
    sealed class CommandSendExample : ICommandSender
    {
        public string command { get; set; } = "CommandToExecute";
        public Dictionary<string, string> args { get; set; } = new Dictionary<string, string>() { { "a", "value" }, { "b", "value" }, { "c", "value" } };

        public event EventHandler Sended;
        public void OnSended()
        {
            Sended?.Invoke(this, EventArgs.Empty);
        }
    }

    sealed class CommandReceiveExample : ICommandReceive
    {
        public string command { get; set; } = "CommandToExecute";
        public Dictionary<string, string> args { get; set; } = new Dictionary<string, string>() { { "a", "value" }, { "b", "value" }, { "c", "value" } };

        public event EventHandler Received;
        public void OnReceive()
        {
            Received?.Invoke(this, EventArgs.Empty);
        }
    }
}