using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Haukcode.KiNet.Model;

namespace Haukcode.KiNet
{
    public class ReceiveDataPacket : ReceiveDataBase
    {
        public BasePacket Packet { get; set; } = null!;
    }
}
