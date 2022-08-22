using System;

namespace StreamConnect
{
    class Program
    {
        static void Main(string[] args)
        {
            // Tool to bridge physical COM ports, VMWare virtual COM ports via named pipes and IpToSerial servers 
            // Supported use cases:
            // 
            // Bridge VMware virtual COM port to a phyical COM port
            // 
            // 1) Configure VM port with "use named pipe", "this end is the client", "the other end is an application"
            // 2) Set VM pipe name to "\\.\pipe\<unique name>" e.g. \\.\pipe\v_com_1
            // 3) Run 'StreamConnect \\.\pipe\v_com_1 COM1'
            // 4) App will accept incoming pipe clients and redirect data to/from the local COM port
            //
            // Emulate IpToSerial device
            // 1) Connect a PED to a local COM port
            // 2) Run 'StreamConnect 0.0.0.0:<unique port> COM1'
            // 3) App will accept incoming TCP/IP sockets and redirect data to/from the local COM port

            if (args.Length == 2)
            {
                var bridge = new CrossoverBinding(args[0], args[1]);
                Console.WriteLine("Source {0} -> Dest {1}", bridge.sourceBinding, bridge.destBinding);
                bridge.Start();
                Console.WriteLine("Press any key to disconnect");
                Console.ReadKey();
                bridge.Stop();
            }
            else
            {
                Console.WriteLine("Usage: {0} <source> <dest>", System.AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine(@"Source/dest mappings allow formats:");
                Console.WriteLine("  <ip or host>:<port>");
                Console.WriteLine("  <Named pipe>");
                Console.WriteLine($"  COM<n> (com port, {SerialSettings.AsString()})");
            }
        }
    }
}
