namespace StreamConnect
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class PluggableSocket : IPluggableStreamAsync, IDisposable
    {
        private readonly Socket baseSocket;
        private readonly Stream networkStream;

        public PluggableSocket(Socket socket)
        {
            this.baseSocket = socket;
            this.networkStream = new NetworkStream(socket);
        }

        public void Dispose()
        {
            networkStream.Dispose();
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            return networkStream.ReadAsync(buffer, offset, count, cancelToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            return networkStream.WriteAsync(buffer, offset, count, cancelToken);
        }

        bool IPluggableStreamAsync.IsConnected()
        {
            return this.baseSocket.Connected;
        }
    }
}