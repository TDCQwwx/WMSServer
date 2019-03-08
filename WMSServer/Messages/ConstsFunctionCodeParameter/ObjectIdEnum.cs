using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum ObjectIdEnum
    {
        WMSServer,
        WareHousePLC,
        AGV
    }
    public class ObjectIdHexString
    {
        public const string WMSServer = "0020";
        public const string WareHousePLC = "0021";
        public const string AGV = "0022";
    }
}
