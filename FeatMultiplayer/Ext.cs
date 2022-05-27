using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text;

namespace FeatMultiplayer
{
    static class Ext
    {
        public static void Write(this NetworkStream stream, string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static void Write(this NetworkStream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        public static void Clear<T>(this ConcurrentQueue<T> queue)
        {
            while (queue.TryDequeue(out _)) ;
        }
    }
}
