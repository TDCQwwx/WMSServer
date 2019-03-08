using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum StateQueryResultAGVEnum
    {
        CurrentPositionResult,
    }
    public class StateQueryResultAGVHexString
    {
        public const string CurrentPositionResult_OriginalPosition = "000A";
        public const string CurrentPositionResult_InputPosition = "0001";
        public const string CurrentPositionResult_OutputPosition = "0002";
        public const string CurrentPositionResult_DockingPlatformPosition = "0003";
    }
}
