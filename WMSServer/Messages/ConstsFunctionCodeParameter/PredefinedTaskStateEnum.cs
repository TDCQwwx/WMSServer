using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum PredefinedTaskStateEnum
    {
        Confirmed,
        Completed,
        Progressing,
        Error
    }
    public class PredefinedTaskStateHexString
    {
        public const string Confirmed = "0000";
        public const string Completed = "0001";
        public const string Progressing = "0002";
        public const string Error = "FFFF";
    }
}
