using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum StateQueryModeEnum
    {
        Single,
        CycleStart,
        CycleEnd
    }
    public class StateQueryModeHexString
    {
        public const string Single = "0001";
        public const string CycleStart = "0002";
        public const string CycleEnd = "0000";
    }
}
