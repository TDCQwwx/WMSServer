using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum PredefinedTaskTypeWarehousePLCEnum
    {
        InputExecution,
        OutputExecution,
        OutputUnload,
        InputLoad,
        ReadRFID,
        WriteRFID
    }
    public class PredefinedTaskTypeWarehousePLCHexString
    {
        public const string InputExecution = "0001";
        public const string OutputExecution = "0002";
        public const string InputLoad = "0003";
        public const string OutputUnload = "0004";
        public const string ReadRFID = "0005";
        public const string WriteRFID = "0006";
    }
}
