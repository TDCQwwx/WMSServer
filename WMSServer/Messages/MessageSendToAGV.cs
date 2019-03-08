using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    /// <summary>
    /// the messages of send to AGV:Instruction or Feedback
    /// message = controllerId + functionCode + functionCodeParameter
    /// Instruction：WMSServer as sender，Feedback：WMSServer as receiver
    /// </summary>
    public class MessageSendToAGV
    {
        private const string controllerId = ControllerIdHexString.WMSServer;
        private static string functionCode;
        private static string functionCodeParameter;
        private const string objectId = ObjectIdHexString.AGV;
        private static string predefinedTaskType;
        private static string predefinedTaskParameter1;
        private static string predefinedTaskParameter2;
        private static string predefinedTaskParameter3;
        private static string stateQueryPropertyId;
        private static string stateQueryMode;
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
        public static byte[] PredefinedTaskInstruction(string orderNumber, 
            PredefinedTaskTypeAGVEnum predefinedTaskTypeAGVEnum,
            PredefinedTaskFirstParameterAGVEnum predefinedTaskFirstParameterAGVEnum)
        {
            functionCode = FunctionCodeHexString.PredefinedTask_Instruction;
            switch (predefinedTaskTypeAGVEnum)
            {
                case PredefinedTaskTypeAGVEnum.Move:
                    predefinedTaskType = PredefinedTaskTypeAGVHexString.Move;
                    break;
                case PredefinedTaskTypeAGVEnum.ReleaseTray:
                    predefinedTaskType = PredefinedTaskTypeAGVHexString.ReleaseTray;
                    break;
                case PredefinedTaskTypeAGVEnum.LoadTray:
                    predefinedTaskType = PredefinedTaskTypeAGVHexString.LoadTray;
                    break;
                default:
                    break;
            }
            switch (predefinedTaskFirstParameterAGVEnum)
            {
                case PredefinedTaskFirstParameterAGVEnum.Null:
                    predefinedTaskParameter1 = PredefinedTaskFirstParameterAGVHexString.Null;
                    break;
                case PredefinedTaskFirstParameterAGVEnum.MovetoOriginalPoint:
                    predefinedTaskParameter1 = PredefinedTaskFirstParameterAGVHexString.MovetoOriginalPoint;
                    break;
                case PredefinedTaskFirstParameterAGVEnum.MovetoInputPoint:
                    predefinedTaskParameter1 = PredefinedTaskFirstParameterAGVHexString.MovetoInputPoint;
                    break;
                case PredefinedTaskFirstParameterAGVEnum.MovetoOutputPoint:
                    predefinedTaskParameter1 = PredefinedTaskFirstParameterAGVHexString.MovetoOutputPoint;
                    break;
                case PredefinedTaskFirstParameterAGVEnum.MovetoDockingPlatform:
                    predefinedTaskParameter1 = PredefinedTaskFirstParameterAGVHexString.MovetoPlatform;
                    break;
                default:
                    break;
            }
            predefinedTaskParameter2 = predefinedTaskParameter3 = "0000";
            functionCodeParameter = objectId + orderNumber + predefinedTaskType + predefinedTaskParameter1 + predefinedTaskParameter2 + predefinedTaskParameter3;
            return (controllerId + functionCode + functionCodeParameter).HexStringToByteArray();
        }
        public static byte[] StateQueryInstruction(StateQueryPropertyIdAGVEnum stateQueryPropertyIdAGVEnum,StateQueryModeEnum stateQueryModeEnum)
        {
            functionCode = FunctionCodeHexString.StateQuery_Instruction;
            switch (stateQueryPropertyIdAGVEnum)
            {
                case StateQueryPropertyIdAGVEnum.CurrentPositionProperty:
                    stateQueryPropertyId = StateQueryPropertyAGVHexString.CurrentPositionProperty;
                    break;
                default:
                    break;
            }
            switch (stateQueryModeEnum)
            {
                case StateQueryModeEnum.Single:
                    stateQueryMode = StateQueryModeHexString.Single;
                    break;
                case StateQueryModeEnum.CycleStart:
                    stateQueryMode = StateQueryModeHexString.CycleStart;
                    break;
                case StateQueryModeEnum.CycleEnd:
                    stateQueryMode = StateQueryModeHexString.CycleEnd;
                    break;
                default:
                    break;
            }
            functionCodeParameter = objectId + stateQueryPropertyId + stateQueryMode;
            return (controllerId + functionCode + functionCodeParameter).HexStringToByteArray();
        }
        public static byte[] EventReportFeedback(string eventReportMessage)
        {
            functionCode = FunctionCodeHexString.EventReport_Feedback;
            functionCodeParameter = eventReportMessage.InterceptFunctionCodeParameter();

            return (controllerId + functionCode + functionCodeParameter).HexStringToByteArray();
        }
    }
}
