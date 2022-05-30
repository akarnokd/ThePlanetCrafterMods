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
    public interface MessageStringProvider
    {
        string GetString();
    }

    /// <summary>
    /// Implement this interface for sending messages that are themselves binary data.
    /// </summary>
    public interface MessageBytesProvider
    {
        byte[] GetBytes();
    }
}
