using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum FunctionCodeEnum
    {
        ConfirmConnection_Instruction,
        PredefinedTask_Instruction,
        StateQuery_Instruction,
        EventReport_Instruction,
        FaultReport_Instruction,

        ConfirmConnection_Feedback,
        PredefinedTask_Feedback,
        StateQuery_Feedback,
        EventReport_Feedback,
        FaultReport_Feedback
    }
    public class FunctionCodeHexString
    {
        public const string ConfirmConnection_Instruction = "0000";
        public const string PredefinedTask_Instruction = "0002";
        public const string StateQuery_Instruction = "0004";
        public const string EventReport_Instruction = "0005";
        public const string FaultReport_Instruction = "00FF";

        public const string ConfirmConnection_Feedback = "0010";
        public const string PredefinedTask_Feedback = "0012";
        public const string StateQuery_Feedback = "0014";
        public const string EventReport_Feedback = "0015";
        public const string FaultReport_Feedback = "00FF";
    }
}
