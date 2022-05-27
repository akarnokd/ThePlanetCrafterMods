using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    public interface MessageStringProvider
    {
        string GetString();
    }
    public interface MessageBytesProvider
    {
        byte[] GetBytes();
    }
}
