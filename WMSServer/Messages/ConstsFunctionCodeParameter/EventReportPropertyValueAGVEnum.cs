using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum EventReportPropertyValueAGVEnum
    {
        StopButtonPressed,
        StopButtonReleased,
    }
    public class EventReportPropertyValueAGVHexString
    {
        public const string StopButtonPressed="0001";
        public const string StopButtonReleased = "0000";
    }
}
