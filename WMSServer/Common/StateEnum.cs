using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum WMSServerTaskState
    {
        None = 0,//任务状态为空
        MesServer = 1,//WMSServer等待被MesServer调度
        MesServerIsUsing = 2,//WMSServer正在被MesServer调度
        WMSClient = 3//WMSClient正在调度WMSServer
    }
    public enum WMSClientConnectionState
    {
        DisConnected,
        Connected
    }
    public enum AGVConnectionState
    {
        DisConnected,
        Connected
    }
    public enum WarehousePLCConnectionState
    {
        DisConnected,
        Connected
    }
    public enum AGVCurrentPosition
    {
        OriginalPoint = 1,
        InputPoint = 2,
        OutputPoint = 4,
        DockingPlatform = 8
    }
}
