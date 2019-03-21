namespace StreamConnect
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPluggableStreamAsync
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken);
        bool IsConnected();
    }
}
