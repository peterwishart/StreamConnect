namespace StreamConnect
{
    using System;
    using System.IO.Pipes;
    using System.IO.Ports;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class CrossoverBinding: IDisposable
    {
        public enum BindingType { Serial, NamedPipe, Socket }

        public class BindingInfo
        {
            public BindingType BindingType { get; private set; }

            public string Address { get; private set; }

            public int? Port { get; private set; }

            private static IPEndPoint IPEndPointParse(string endpointstring)
            {
                string[] values = endpointstring.Split(new[] { ':' });

                if (2 > values.Length)
                {
                    throw new FormatException("Invalid endpoint format");
                }

                IPAddress ipaddress;
                string ipaddressstring = string.Join(":", values.Take(values.Length - 1).ToArray());
                if (!IPAddress.TryParse(ipaddressstring, out ipaddress))
                {
                    var resolvedIps = Dns.GetHostAddresses(ipaddressstring);
                    if (resolvedIps.Length > 0)
                        ipaddress = resolvedIps[0];
                    else
                        throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", ipaddressstring));
                }

                int port;
                if (!int.TryParse(values[values.Length - 1], out port)
                 || port < IPEndPoint.MinPort
                 || port > IPEndPoint.MaxPort)
                {
                    throw new FormatException(string.Format("Invalid end point port '{0}'", values[values.Length - 1]));
                }

                return new IPEndPoint(ipaddress, port);
            }

            public BindingInfo(string specification)
            {
                // todo: additional com port params
                if (specification.ToLowerInvariant().StartsWith("com"))
                {
                    this.BindingType = BindingType.Serial;
                    this.Address = null;
                    this.Port = Int32.Parse(specification.Substring(3));
                }
                else if (specification.ToLowerInvariant().StartsWith(@"\\.\pipe\"))
                {
                    this.BindingType = BindingType.NamedPipe;
                    this.Address = specification.Substring(9);
                    this.Port = null;
                }
                else
                {
                    var tempEp = IPEndPointParse(specification);
                    this.BindingType = BindingType.Socket;
                    this.Address = tempEp.Address.ToString();
                    this.Port = tempEp.Port;
                }
            }

            public override string ToString()
            {
                return string.Format(
                    "{0} {1}{2}",
                    BindingType.ToString(),
                    this.BindingType == BindingType.Socket
                        ? this.Address + ":"
                        : (this.BindingType == BindingType.Serial ? "COM" : @"\\.\pipe\"+this.Address),
                    this.Port.HasValue ? this.Port.ToString() : String.Empty);
            }
        }

        public BindingInfo sourceBinding {get;private set;}
        public BindingInfo destBinding { get; private set; }        

        public CrossoverBinding(String source, String dest)
        {
            sourceBinding = new BindingInfo(source);
            destBinding = new BindingInfo(dest);
        }

        private CancellationTokenSource cancelSource;

        public void Start()
        {
            cancelSource = new CancellationTokenSource();
            var task = CrossoverBindingAsync(cancelSource.Token, sourceBinding, destBinding);
            task.Wait();            
        }

        public void Stop()
        {
            cancelSource?.Cancel();
        }

        private static async Task ClientConnectAsync(CancellationToken cancelToken, IPluggableStreamAsync serverStream, BindingInfo clientBinding)
        {
            IPluggableStreamAsync clientStream;

            if (clientBinding.BindingType == BindingType.Serial)
            {
                using (var serialPortClient = new SerialPort(string.Format("COM{0}", clientBinding.Port), SerialSettings.BaudRate, SerialSettings.Parity, SerialSettings.DataBits, SerialSettings.StopBits))
                {
                    try
                    {
                        serialPortClient.DtrEnable = true;
                        serialPortClient.Open();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw new InvalidOperationException("Could not connect COM port");
                    }

                    clientStream = new PluggableStream(serialPortClient.BaseStream);
                    Console.WriteLine("Connection made");
                    await StreamCrossover.HandleClientAsync(cancelToken, serverStream, clientStream);
                }
            }        
            else if (clientBinding.BindingType == BindingType.Socket)
            {
                var ep = new IPEndPoint(IPAddress.Parse(clientBinding.Address), clientBinding.Port ?? 80);
                while (!cancelToken.IsCancellationRequested)
                {
                    using (var socketClient = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                    {
                        try
                        {
                            socketClient.Connect(ep);

                        }
                        catch (SocketException e)
                        {
                            Console.WriteLine("Exception connecting to outgoing host: " + e + ": " + e.Message);
                            await Task.Delay(10000);
                            continue;
                        }
                
                        socketClient.NoDelay = true;

                        clientStream = new PluggableSocket(socketClient);
                        Console.WriteLine("Connection made");

                        try
                        {
                            await StreamCrossover.HandleClientAsync(cancelToken, serverStream, clientStream);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                        socketClient.Disconnect(false);

                        if (!serverStream.IsConnected())
                        {
                            return;
                        }
                    }
                }
            }
            else if (clientBinding.BindingType == BindingType.NamedPipe)
            {
                using (var pipeClient = new NamedPipeClientStream(".", clientBinding.Address, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    pipeClient.Connect();
                    if (!pipeClient.IsConnected)
                    {
                        return;
                    }
                    
                    clientStream = new PluggableStream(pipeClient);
                    Console.WriteLine("Connection made");

                    try
                    {
                        await StreamCrossover.HandleClientAsync(cancelToken, serverStream, clientStream);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }                  
                }
            }            
        }

        private static async Task CrossoverBindingAsync(CancellationToken cancelToken, BindingInfo sourceBinding, BindingInfo destBinding)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                switch (sourceBinding.BindingType)
                {
                    case BindingType.NamedPipe:
                    {
                        using (
                            var pipeServer = new NamedPipeServerStream(
                                sourceBinding.Address,
                                PipeDirection.InOut,
                                1,
                                PipeTransmissionMode.Byte,
                                PipeOptions.Asynchronous,
                                4096,
                                4096))
                        {
                            while (!cancelToken.IsCancellationRequested)
                            {
                                pipeServer.WaitForConnection();
                                Console.WriteLine("Connection from " + sourceBinding);

                                if (pipeServer.IsConnected)
                                {
                                    var serverStream = new PluggableStream(pipeServer);
                                    try
                                    {
                                        await ClientConnectAsync(cancelToken, serverStream, destBinding);
                                    }
                                    catch (OperationCanceledException e)
                                    {
                                        Console.WriteLine(e.Message);
                                    }
                                }
                                pipeServer.Disconnect();
                            }

                            break;
                        }
                    }
                    case BindingType.Serial:
                    {
                        SerialPort serialPortServer;
                        try
                        {
                            serialPortServer = new SerialPort(
                                String.Format("COM{0}", sourceBinding.Port), SerialSettings.BaudRate, SerialSettings.Parity, SerialSettings.DataBits, SerialSettings.StopBits);
                            if (SerialSettings.DtrEnable)
                            {
                                serialPortServer.DtrEnable = true;
                            }
                            serialPortServer.Open();
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // could not connect COM port, disconnect the client
                            serialPortServer = null;
                            throw new OperationCanceledException();
                        }

                        if (serialPortServer != null)
                        {                                             
                            Console.WriteLine("Connection from " + sourceBinding + "(" + SerialSettings.AsString() + ")");
                            var serverStream = new PluggableStream(serialPortServer.BaseStream);
                            await ClientConnectAsync(cancelToken, serverStream, destBinding);                        
                            serialPortServer.Close();
                        }

                        break;
                    }
                    case BindingType.Socket:
                    {
                        var ep = new IPEndPoint(IPAddress.Parse(sourceBinding.Address), sourceBinding.Port ?? 80);
                        using (var socketServer = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                        {
                            socketServer.Bind(ep);
                            socketServer.Listen(1);
                            while (!cancelToken.IsCancellationRequested)
                            {
                                using (var dataSocket = socketServer.Accept())
                                {
                                    dataSocket.NoDelay = true;
                                    Console.WriteLine("Connection from " + sourceBinding);
                                    var serverStream = new PluggableSocket(dataSocket);
                                    try
                                    {
                                        await ClientConnectAsync(cancelToken, serverStream, destBinding);
                                    }
                                    catch (OperationCanceledException e)
                                    {
                                        Console.WriteLine(e.Message);
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        public void Dispose()
        {
            ((IDisposable)cancelSource).Dispose();
        }
    }
}