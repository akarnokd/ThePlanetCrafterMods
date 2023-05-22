using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    /// <summary>
    /// Implement this interface for sending messages that will be converted to bytes from the string.
    /// </summary>
    public interface IMessageStringProvider
    {
        string GetString();
    }

    /// <summary>
    /// Base class with a sender field.
    /// </summary>
    public abstract class MessageBase : IMessageStringProvider
    {
        public ClientConnection sender;

        public abstract string GetString();
    }
}
