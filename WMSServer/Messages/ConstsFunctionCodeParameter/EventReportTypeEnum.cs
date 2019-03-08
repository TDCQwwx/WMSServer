using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum EventReportTypeEnum
    {
        GeneralEvent,
        WarningEvent,
        SeriousEvent
    }
    public class EventReportTypeHexString
    {
        public const string GeneralEvent = "0001";
        public const string WarningEvent = "0002";
        public const string SeriousEvent = "0003";
    }
}
