using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    /// <summary>
    /// Implement this interface for sending messages that will be converted to bytes from the string.
    /// </summary>
    public interface IMessageStringProvider
    {
        string GetString();
    }

    /// <summary>
    /// Implement this interface for sending messages that are themselves binary data.
    /// </summary>
    public interface IMessageBytesProvider
    {
        byte[] GetBytes();
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
