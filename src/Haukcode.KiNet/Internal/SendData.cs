﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Haukcode.KiNet
{
    public class SendData
    {
        public IPEndPoint EndPoint { get; set; }

        public byte[] Data { get; set; }
    }
}
