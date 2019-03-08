using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum ControllerIdEnum
    {
        MESServer,
        WMSServer,
        WMSClient,
        WareHousePLC,
        AGV
    }
    public class ControllerIdHexString
    {
        public const string MESServer = "0001";
        public const string WMSServer = "1001";
        public const string WMSClient = "1101";
        public const string WareHousePLC = "1002";
        public const string AGV = "1003";
    }
}
