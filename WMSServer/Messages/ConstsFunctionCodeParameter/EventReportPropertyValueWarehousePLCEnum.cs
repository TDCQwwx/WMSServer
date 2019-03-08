using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum EventReportPropertyValueWarehousePLCEnum
    {
        StopButtonPressed,
        StopButtonReleased,
    }
    public class EventReportPropertyValueWarehousePLCHexString
    {
        public const string StopButtonPressed = "0001";
        public const string StopButtonReleased = "0000";
    }
}
