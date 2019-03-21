namespace StreamConnect
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class PluggableStream: IPluggableStreamAsync
    {
        private readonly Stream baseStream;

        public PluggableStream(Stream baseStream)
        {
            this.baseStream = baseStream;
        }

        public bool IsConnected()
        {
            // Stream does not expose a connected property, will get a 0 byte Read on disconnection.            
            return true;
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            return this.baseStream.ReadAsync(buffer, offset, count, cancelToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            return this.baseStream.WriteAsync(buffer, offset, count, cancelToken);
        }
    }
}