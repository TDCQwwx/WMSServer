using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public class MessageSendToWMSClient
    {
        private const string controllerId = ControllerIdHexString.WMSServer;
        private static string functionCode;
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
    }
}
