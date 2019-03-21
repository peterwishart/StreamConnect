namespace StreamConnect
{
    using System;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class StreamCrossover
    {
        private const int MaxMessageSize = 1024;

        [Conditional("OperationDebugs")]
        private static void OpDiag(Operation op, int byteCount, byte[] buffer)
        {
            var tmpBuffer = new byte[byteCount];
            Array.Copy(buffer, tmpBuffer, byteCount);
            for (int i = 0; i < byteCount; i++)
                if (tmpBuffer[i] < 32)
                    tmpBuffer[i] = 32;            

            var sb = new StringBuilder(byteCount);
            sb.Append("0x");
            if (byteCount > 16)
            {
                for (int i = 0; i < 8; i++)                
                    sb.Append(buffer[i].ToString("x2"));
                sb.Append("..");
                for (int i = byteCount - 8; i < byteCount; i++)
                    sb.Append(buffer[i].ToString("x2"));
            }
            else
            {
                for (int i = 0; i < byteCount; i++)               
                    sb.Append(buffer[i].ToString("x2"));                
            }
            Console.WriteLine(String.Format("{0}\t{1}\t{2}", Enum.GetName(typeof(Operation), op), byteCount, sb));
        }

        private enum Operation
        {
            OutboundWrite,
            OutboundRead,
            InboundRead,
            InboundWrite
        }

        // handle sync reads from each end and pass to the other end
        public static async Task HandleClientAsync(CancellationToken cancelToken, IPluggableStreamAsync inputStream, IPluggableStreamAsync outputStream)
        {
            while (!cancelToken.IsCancellationRequested)
            {             
                byte[] outputRead = new byte[MaxMessageSize];
                byte[] inputRead = new byte[MaxMessageSize];

                var readFromOutput = outputStream.ReadAsync(outputRead, 0, MaxMessageSize, cancelToken);
                var readFromInput = inputStream.ReadAsync(inputRead, 0, MaxMessageSize, cancelToken);
                while (!cancelToken.IsCancellationRequested)
                {
                    int byteCount;
                    var nextTask = await Task.WhenAny(new[] { readFromOutput, readFromInput });
                    Console.Write(".");
                    if (nextTask == readFromInput)
                    {
                        try
                        {
                            byteCount = await nextTask;
                        }
                        catch (System.IO.IOException e) when ((e.InnerException as SocketException)?.ErrorCode == 10054)
                        {
                            throw new OperationCanceledException("Input stream disconnected (WSACONNRESET)");
                        }
                        OpDiag(Operation.InboundRead, byteCount, inputRead);
                        if (byteCount == 0)
                        {
                            throw new OperationCanceledException("Input stream disconnected");
                        }
                        var outputWrite = new byte[MaxMessageSize];
                        Array.Copy(inputRead, outputWrite, byteCount);
                        var backTask = outputStream.WriteAsync(outputWrite, 0, byteCount, cancelToken);
                        readFromInput = inputStream.ReadAsync(inputRead, 0, MaxMessageSize, cancelToken);
                    }
                    else
                    if (nextTask == readFromOutput)
                    {
                        byteCount = await nextTask;
                        OpDiag(Operation.OutboundRead, byteCount, outputRead);
                        var inputWrite = new byte[MaxMessageSize];
                        Array.Copy(outputRead, inputWrite, byteCount);
                        var backTask = inputStream.WriteAsync(inputWrite, 0, byteCount, cancelToken);
                        readFromOutput = outputStream.ReadAsync(outputRead, 0, MaxMessageSize, cancelToken);
                    }
                }       
            }
        }
    }
}
