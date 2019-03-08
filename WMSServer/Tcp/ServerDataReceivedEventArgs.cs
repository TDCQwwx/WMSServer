using SaeaServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public class ServerDataReceivedEventArgs : EventArgs
    {
        public byte[] _dataBytes;
        public TcpSocketSaeaSession _session;
        public ServerDataReceivedEventArgs(TcpSocketSaeaSession session, byte[] bytesData, int offSet, int count)
        {
            _dataBytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                _dataBytes[i] = bytesData[offSet + i];
            }
            _session = session;
        }
    }
}
