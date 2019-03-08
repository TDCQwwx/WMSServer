using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public class MessageSendToMESServer
    {
        private const string controllerId = ControllerIdHexString.WMSServer;
        private static string functionCode;
        //private const string objectId = ObjectIdHexString.WMSServer;
        private static string taskType;

        public static byte[] ConfirmConnectionInstructionOrFeedback(FunctionCodeEnum functionCodeEnum)
        {
            switch (functionCodeEnum)
            {
                case FunctionCodeEnum.ConfirmConnection_Instruction:
                    functionCode = FunctionCodeHexString.ConfirmConnection_Instruction;
                    break;
                case FunctionCodeEnum.ConfirmConnection_Feedback:
                    functionCode = FunctionCodeHexString.ConfirmConnection_Feedback;
                    break;
                default:
                    break;
            }
            return (controllerId + functionCode).HexStringToByteArray();
        }

        public static byte[] PredefinedTaskFeedback(string functionCodeParameter,PredefinedTaskStateEnum predefinedTaskStateEnum)
        {
            functionCode = FunctionCodeHexString.PredefinedTask_Feedback;
            switch (predefinedTaskStateEnum)
            {
                case PredefinedTaskStateEnum.Confirmed:
                    taskType = PredefinedTaskStateHexString.Confirmed;
                    break;
                case PredefinedTaskStateEnum.Completed:
                    taskType = PredefinedTaskStateHexString.Completed;
                    break;
                case PredefinedTaskStateEnum.Progressing:
                    taskType = PredefinedTaskStateHexString.Progressing;
                    break;
                case PredefinedTaskStateEnum.Error:
                    taskType = PredefinedTaskStateHexString.Error;
                    break;
                default:
                    break;
            }

            return (controllerId + functionCode + functionCodeParameter + taskType).HexStringToByteArray();
        }
    }
}
