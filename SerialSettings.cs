using System.IO.Ports;

namespace StreamConnect
{
    public static class SerialSettings
    {
        public static bool DtrEnable = true;
        public static int BaudRate = 19200;
        public static Parity Parity = Parity.None;
        public static int DataBits = 8;
        public static StopBits StopBits = StopBits.One;
    }
}
