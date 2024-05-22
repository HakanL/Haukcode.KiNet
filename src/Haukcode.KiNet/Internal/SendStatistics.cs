using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haukcode.KiNet
{
    public class SendStatistics
    {
        public int DroppedPackets { get; set; }

        public int QueueLength { get; set; }

        public int DestinationCount { get; set; }

        public int SlowSends { get; set; }
    }
}
