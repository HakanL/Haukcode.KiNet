using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Haukcode.KiNet.Model;

namespace Haukcode.KiNet;

public class ReceiveDataPacket
{
    public double TimestampMS { get; set; }

    public IPEndPoint Source { get; set; } = null!;

    public IPEndPoint Destination { get; set; } = null!;

    public BasePacket Packet { get; set; } = null!;
}
