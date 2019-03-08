using SaeaServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public class ServerEventDispatcher : ITcpSocketSaeaServerEventDispatcher
    {
        //TCP会话开始
        public delegate void OnSessionStartedEventHandler(object sender, TcpSocketSaeaSession e);
        public event OnSessionStartedEventHandler ServerStartedEvent;
        //数据接收
        public delegate void OnSessionDataReceivedEventHandler(object sender, ServerDataReceivedEventArgs e);
        public event OnSessionDataReceivedEventHandler ServerDataReceivedEvent;
        //TCP会话结束
        public delegate void OnSessionClosedEventHandler(object sender, TcpSocketSaeaSession session);
        public event OnSessionClosedEventHandler ServerClosedEvent;
        public async Task OnSessionStarted(TcpSocketSaeaSession session)
        {
            ServerStartedEvent(this, session);
            await Task.CompletedTask;
        }
        public async Task OnSessionDataReceived(TcpSocketSaeaSession session, byte[] data, int offset, int count)
        {
            ServerDataReceivedEvent(this, new ServerDataReceivedEventArgs(session, data, offset, count));
            await Task.CompletedTask;
        }
        public async Task OnSessionClosed(TcpSocketSaeaSession session)
        {
            ServerClosedEvent(this, session);
            await Task.CompletedTask;
        }
    }
}
