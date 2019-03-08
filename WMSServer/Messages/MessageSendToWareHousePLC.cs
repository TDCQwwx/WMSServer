using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public class MessageSendToWarehousePLC
    {
        private const string controllerId = ControllerIdHexString.WMSServer;
        private static string functionCode;
        private static string functionCodeParameter;
        private const string objectId = ObjectIdHexString.WareHousePLC;
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
       /// <summary>
       /// 预定义动作任务指令——入库执行||出库执行
       /// </summary>
       /// <param name="orderNumber">订单号</param>
       /// <param name="predefinedTaskTypeWarehousePLCEnum">任务类型</param>
       /// <param name="warehouseLocation">入库||出库位置</param>
       /// <returns></returns>
        public static byte[] PredefinedTaskInstruction(string orderNumber,
            PredefinedTaskTypeWarehousePLCEnum predefinedTaskTypeWarehousePLCEnum,
            int warehouseLocation)
        {
            functionCode = FunctionCodeHexString.PredefinedTask_Instruction;
            switch (predefinedTaskTypeWarehousePLCEnum)
            {
                case PredefinedTaskTypeWarehousePLCEnum.InputExecution:
                    predefinedTaskType = PredefinedTaskTypeWarehousePLCHexString.InputExecution;
                    break;
                case PredefinedTaskTypeWarehousePLCEnum.OutputExecution:
                    predefinedTaskType = PredefinedTaskTypeWarehousePLCHexString.OutputExecution;
                    break;
                default:
                    break;
            }
            predefinedTaskParameter1 = Convert.ToString(warehouseLocation, 16).PadLeft(4, '0');
            predefinedTaskParameter2 = predefinedTaskParameter3 = "0000";
            functionCodeParameter = objectId + orderNumber + predefinedTaskType + predefinedTaskParameter1 + predefinedTaskParameter2 + predefinedTaskParameter3;

            return (controllerId + functionCode + functionCodeParameter).HexStringToByteArray();
        }
        /// <summary>
        /// 预定义动作任务指令——入库上料||出库下料
        /// </summary>
        /// <param name="orderNumber">订单号</param>
        /// <param name="predefinedTaskTypeWarehousePLCEnum">任务类型</param>
        /// <returns></returns>
        public static byte[] PredefinedTaskInstruction(string orderNumber,
            PredefinedTaskTypeWarehousePLCEnum predefinedTaskTypeWarehousePLCEnum)
        {
            functionCode = FunctionCodeHexString.PredefinedTask_Instruction;
            switch (predefinedTaskTypeWarehousePLCEnum)
            {
                case PredefinedTaskTypeWarehousePLCEnum.OutputUnload:
                    predefinedTaskType = PredefinedTaskTypeWarehousePLCHexString.OutputUnload;
                    break;
                case PredefinedTaskTypeWarehousePLCEnum.InputLoad:
                    predefinedTaskType = PredefinedTaskTypeWarehousePLCHexString.InputLoad;
                    break;
                default:
                    break;
            }

            predefinedTaskParameter1 = predefinedTaskParameter2 = predefinedTaskParameter3 = "0000";
            functionCodeParameter = objectId + orderNumber + predefinedTaskType + predefinedTaskParameter1 + predefinedTaskParameter2 + predefinedTaskParameter3;

            return (controllerId + functionCode + functionCodeParameter).HexStringToByteArray();
        }
        /// <summary>
        /// 预定义动作任务指令——读取RFID号||写入RFID号
        /// </summary>
        /// <param name="orderNumber">订单号</param>
        /// <param name="predefinedTaskTypeWarehousePLCEnum">任务类型</param>
        /// <param name="rfidNumber">RFID号</param>
        /// <returns></returns>
        public static byte[] PredefinedTaskInstruction(string orderNumber,
           PredefinedTaskTypeWarehousePLCEnum predefinedTaskTypeWarehousePLCEnum,
           string rfidNumber)
        {
            functionCode = FunctionCodeHexString.PredefinedTask_Instruction;
            switch (predefinedTaskTypeWarehousePLCEnum)
            {
                case PredefinedTaskTypeWarehousePLCEnum.ReadRFID:
                    predefinedTaskType = PredefinedTaskTypeWarehousePLCHexString.ReadRFID;
                    predefinedTaskParameter1 = predefinedTaskParameter2 = "0000";
                    break;
                case PredefinedTaskTypeWarehousePLCEnum.WriteRFID:
                    predefinedTaskType = PredefinedTaskTypeWarehousePLCHexString.WriteRFID;
                    predefinedTaskParameter1 = rfidNumber.Substring(0, 4);
                    predefinedTaskParameter2 = rfidNumber.Substring(4, 4);
                    break;
                default:
                    break;
            }
            predefinedTaskParameter3 = "0000";
            functionCodeParameter = objectId + orderNumber + predefinedTaskType + predefinedTaskParameter1 + predefinedTaskParameter2 + predefinedTaskParameter3;

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
