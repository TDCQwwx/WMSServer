using ClassLibraryHelper;
using NLog;
using SaeaServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WMSServer
{
    public class WMSServerStart
    {
        #region Field
        static NLog.Logger logger = LogManager.GetCurrentClassLogger();

        //Tcp_Server  the communication of WMSServer and WarehousePLC,AGV,WMSClient
        private static TcpSocketSaeaServer[] _server;
        private TcpSocketSaeaServerConfiguration _serverConfig;
        private ServerEventDispatcher _serverDispatcher;

        //Tcp_Client the communication of WMSServer and MESServer
        private TcpSocketSaeaClient _client;
        private TcpSocketSaeaClientConfiguration _clientConfig;
        private ClientEventDispatcher _clientDispatcher;

        private static TcpSocketSaeaSession WarehousePLCTcpSocketSaeaSession;
        private static TcpSocketSaeaSession AGVTcpSocketSaeaSession;
        private static TcpSocketSaeaSession WMSClientTcpSocketSaeaSession;

        private const int portWarehousePLCConnect = 2000;
        private const int portAGVConnect = 2001;
        private const int portWMSClientConnect = 2002;

        private WMSServerTaskState WMSServerTaskState;
        private WMSClientConnectionState WMSClientConnectionState;
        private AGVConnectionState AGVConnectionState;
        private WarehousePLCConnectionState WarehousePLCConnectionState;

        private bool outputFlag = false;//出库执行的标记，初始值为true
        private int outputLocationWMSClient;//WMSClient调度下的出库库位号
        private int outputLocationMesServer;//MesServer调度下的出库库位号

        private Queue<string> queueMesTaskCommand = new Queue<string>();//用于存储Mes发给WMS服务器发过来的指令
        //private string agvCurrentPositin;//用于标记AGV的当前坐标
        private AGVCurrentPosition AGVCurrentPosition;
        private string realTimeCommand1;//用于标记Mes的第一个任务指令
        private string realTimeCommand2;//用于标记Mes的第二个任务指令
        private string realTimeCommand3;//用于标记Mes的第三个任务指令
        private string inputRFIDNumberWMSClient;//WMSClient调度下的RFID号
        private string inputRFIDNumberMesServer;//MesServer调度下的RFID号
        #endregion

        #region Constructors
        public WMSServerStart()
        {
            WMSServerTaskState = WMSServerTaskState.None;
            WMSClientConnectionState = WMSClientConnectionState.DisConnected;
            AGVConnectionState = AGVConnectionState.DisConnected;
            WarehousePLCConnectionState = WarehousePLCConnectionState.DisConnected;
            queueMesTaskCommand.Clear();
            BuildServer();
            ConnectMesServer();
        }
        #endregion

        #region MES
        public void ConnectMesServer()
        {
            _clientConfig = new TcpSocketSaeaClientConfiguration { FrameBuilder = new LengthPrefixedFrameSlimSlimBuilder() };

            _clientDispatcher = new ClientEventDispatcher();
            _clientDispatcher.ClientStartedEvent += new ClientEventDispatcher.OnServerStartedEventHandler(OnClientStarted);
            _clientDispatcher.ClientDataReceivedEvent += new ClientEventDispatcher.OnServerDataReceivedEventHandler(OnClientDataReceived);
            _clientDispatcher.ClientClosedEvent += new ClientEventDispatcher.OnServerClosedEventHandler(OnClientClosed);

            //the actual debugging
            //private IPEndPoint serverPoint = new IPEndPoint(IPAddress.Parse("192.168.1.2"), 2000);
            //private IPEndPoint localPoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 3000);

            //the test debugging
            var serverPoint = new IPEndPoint(IPAddress.Parse("192.168.0.100"), 4000);
            var localPoint = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 3000);

            //LoopConnection
            Task.Run(async () =>
                {
                    _client = new TcpSocketSaeaClient(serverPoint, localPoint, _clientDispatcher, _clientConfig);
                    while (true)
                    {
                        try
                        {
                            if (_client.State == TcpSocketConnectionState.Closed)
                            {
                                try
                                {
                                    _client.Dispose();
                                }
                                catch (Exception ex)
                                {
                                }
                                _client = new TcpSocketSaeaClient(serverPoint, localPoint, _clientDispatcher, _clientConfig);
                                Thread.Sleep(1000);
                            }
                            if (_client.State != TcpSocketConnectionState.Connected && _client.State != TcpSocketConnectionState.Connecting)
                            {
                                await _client.Connect();
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception ex) { }
                    }
                });

        }
        public void MesTaskStartCommand(string str)
        {
            if (str.Length == 44)
            {
                //当队列中存在从Mes接收过来的指令时，需要判断是否和前面的订单号相同，如果不同，则入队列，防止连续发两条相同指令的情况
                if (queueMesTaskCommand.Count >= 1)
                {
                    bool CommandCanEnqueue = true;//默认可以入指令队列
                    foreach (var item in queueMesTaskCommand)
                    {
                        if (item.Substring(12, 16) == str.Substring(12, 16))//用于找出订单号是否和前面发的指令相同，如果相同的话丢弃，不同再入指令队列
                        {
                            CommandCanEnqueue = false;//相同的话标记为false
                            break;
                        }
                    }
                    if (CommandCanEnqueue)
                    {
                        queueMesTaskCommand.Enqueue(str);
                    }
                }
                //用于首次将Mes发给WMS服务器的指令存储起来，或者当指令队列为空时
                if (queueMesTaskCommand.Count == 0)//当指令队列为空时，入队列，并且查询是否存在合适的待出库位，执行出库
                {
                    queueMesTaskCommand.Enqueue(str);
                    MesTaskOngoingCommand();
                }
            }
        }

        //用于判断是否存在空库位，如果存在则发送指令，不存在的话则反馈出
        public void MesTaskOngoingCommand()
        {
            if (queueMesTaskCommand.Count >= 1)
            {
                WMSServerTaskState = WMSServerTaskState.MesServerIsUsing;//此时相当于开始真正执行任务
                realTimeCommand1 = queueMesTaskCommand.Peek();
                List<StorageInfo> storageInfos = MessageSendToDataBase.GetStoragesInfo();
                foreach (var item in storageInfos)
                {
                    if (item.Type == "bottle" && queueMesTaskCommand.Peek().Substring(32, 4) == "0101" || item.Type == "lid" && queueMesTaskCommand.Peek().Substring(32, 4) == "0201")
                    {
                        outputFlag = true;//表示存在装有瓶子/盖子的料盘
                        outputLocationMesServer = item.LocationID;//将可以出库的位置保存起来
                        break;//跳出循环
                    }
                }
                if (outputFlag)//表明存在装有瓶子/盖子的料盘
                {
                    SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(realTimeCommand1.Substring(12, 16), PredefinedTaskTypeWarehousePLCEnum.OutputExecution, outputLocationMesServer));
                }
                else
                {
                    //向Mes反馈没有相应的物料
                    //讲任务状态赋值为空闲
                    WMSServerTaskState = WMSServerTaskState.None;
                }
            }
            else
            {
                WMSServerTaskState = WMSServerTaskState.None;
            }
        }
        //将空盘入库完成时调用
        public void MesTaskEndCommand()
        {
            if (queueMesTaskCommand.Count >= 1)
            {
                queueMesTaskCommand.Dequeue();
            }
            MesTaskOngoingCommand();
        }
        private void OnClientStarted(object sender, TcpSocketSaeaClient e)
        {
            Console.WriteLine("WMSServer connect to MESServer successfully!");
        }
        private async void OnClientDataReceived(object sender, ClientDataReceivedEventArgs e)
        {
            string receiveStrFromMes = DataHelper.ByteArrayToHexString(e._dataBytes);
            logger.Info("Receive From MES : " + receiveStrFromMes);
            if (WMSServerTaskState == WMSServerTaskState.None)
            {
                if (AGVConnectionState == AGVConnectionState.Connected && WarehousePLCConnectionState == WarehousePLCConnectionState.Connected)
                {
                    WMSServerTaskState = WMSServerTaskState.MesServer;
                }
                if (AGVConnectionState == AGVConnectionState.DisConnected)
                {
                    //向MES服务器反馈AGV处于断开状态
                }
                if (WarehousePLCConnectionState == WarehousePLCConnectionState.DisConnected)
                {
                    //向MES服务器反馈PLC处于断开状态
                }
            }
            if (WMSServerTaskState == WMSServerTaskState.MesServer || WMSServerTaskState == WMSServerTaskState.MesServerIsUsing)
            {
                if (receiveStrFromMes.InterceptFunctionCode() == FunctionCodeHexString.ConfirmConnection_Instruction)
                {
                    logger.Info("Send To MES" + "10010010");
                    await _client.SendAsync(MessageSendToMESServer.ConfirmConnectionInstructionOrFeedback(FunctionCodeEnum.ConfirmConnection_Feedback));
                }
                if (receiveStrFromMes.InterceptFunctionCode() == FunctionCodeHexString.PredefinedTask_Instruction)
                {
                    //收到任务时的开始信号
                    await _client.SendAsync(MessageSendToMESServer.PredefinedTaskFeedback(receiveStrFromMes.InterceptFunctionCodeParameter(), PredefinedTaskStateEnum.Confirmed));
                    //await _client.SendAsync(DataHelper.HexStringToByteArray("100100120020" + receiveStrFromMes.Substring(12) + "0000"));//收到任务时的开始信号
                    logger.Info("Send To MES:" + "100100120020" + receiveStrFromMes.Substring(12) + "0000");
                    switch (receiveStrFromMes.InterceptPredefinedTaskFunctionCodeParameterTaskType())
                    {
                        case PredefinedTaskTypeMesServerHexString.CarryTraytoDockingPlatform://取物料到对接平台点
                            MesTaskStartCommand(receiveStrFromMes);
                            break;
                        case PredefinedTaskTypeMesServerHexString.ReleaseTray://对接平台点放料盘
                            realTimeCommand2 = receiveStrFromMes;//保存发来的数据
                            SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(realTimeCommand2.InterceptPredefinedTaskFunctionCodeParameterOrdernumber(), PredefinedTaskTypeAGVEnum.ReleaseTray, PredefinedTaskFirstParameterAGVEnum.Null));
                            break;
                        case PredefinedTaskTypeMesServerHexString.CarryEmptyTraytoWarehouse://AGV进料&空料盘入库
                            realTimeCommand3 = receiveStrFromMes;//保存发来的数据
                            SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(realTimeCommand3.InterceptPredefinedTaskFunctionCodeParameterOrdernumber(), PredefinedTaskTypeAGVEnum.LoadTray, PredefinedTaskFirstParameterAGVEnum.Null));
                            break;
                        default:
                            break;
                    }
                }
            }
            if (WMSServerTaskState == WMSServerTaskState.WMSClient)
            {
                //向MES服务器反馈WMS服务器正处于WMSClient的任务调度中
            }
        }
        private void OnClientClosed(object sender, TcpSocketSaeaClient session)
        {
            Console.WriteLine("WMSServer disconnected from MESServer!");
        }
        #endregion

        #region WMS
        public void BuildServer()
        {
            _serverConfig = new TcpSocketSaeaServerConfiguration { FrameBuilder = new LengthPrefixedFrameSlimSlimBuilder() };

            _serverDispatcher = new ServerEventDispatcher();
            _serverDispatcher.ServerDataReceivedEvent += new ServerEventDispatcher.OnSessionDataReceivedEventHandler(OnServerDataReceived);
            _serverDispatcher.ServerStartedEvent += new ServerEventDispatcher.OnSessionStartedEventHandler(OnServerStarted);
            _serverDispatcher.ServerClosedEvent += new ServerEventDispatcher.OnSessionClosedEventHandler(OnServerClosed);

            //the actual debugging
            //var endPoint1 = new IPEndPoint(IPAddress.Parse("192.168.5.3"), 2000);//WareHousePLC
            //var endPoint2 = new IPEndPoint(IPAddress.Parse("192.168.0.150"), 2001);//AGV
            //var endPoint3 = new IPEndPoint(IPAddress.Parse("192.168.6.3"), 2002);//WMSClient

            //the test debugging
            var endPoint1 = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 2000);//WareHousePLC
            var endPoint2 = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 2001);//AGV
            var endPoint3 = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 2002);//WMSClient

            _server = new TcpSocketSaeaServer[3] {
                new TcpSocketSaeaServer(endPoint1, _serverDispatcher, _serverConfig),
                new TcpSocketSaeaServer(endPoint2, _serverDispatcher, _serverConfig),
                new TcpSocketSaeaServer(endPoint3, _serverDispatcher, _serverConfig)};
            _server[0].Listen();//WarehousePLC
            _server[1].Listen();//AGV
            _server[2].Listen();//WMSClient
        }
        private async void OnServerStarted(object sender, TcpSocketSaeaSession e)
        {
            string strIP = e.RemoteEndPoint.Address.ToString();//IP
            int strPort = e.LocalEndPoint.Port;//Port
            switch (strPort)
            {
                case 2000:
                    WarehousePLCTcpSocketSaeaSession = e;
                    WarehousePLCConnectionState = WarehousePLCConnectionState.Connected;
                    byte[] sendBytes1 = MessageSendToWarehousePLC.ConfirmConnectionInstructionOrFeedback(FunctionCodeEnum.ConfirmConnection_Instruction);
                    string sendStr1 = sendBytes1.ByteArrayToHexString();
                    logger.Info($"Send to WarehousePLC : {sendStr1}");
                    await WarehousePLCTcpSocketSaeaSession.SendAsync(sendBytes1);
                    break;
                case 2001:
                    AGVTcpSocketSaeaSession = e;
                    AGVConnectionState = AGVConnectionState.Connected;
                    byte[] sendBytes2 = MessageSendToAGV.ConfirmConnectionInstructionOrFeedback(FunctionCodeEnum.ConfirmConnection_Instruction);
                    string sendStr2 = sendBytes2.ByteArrayToHexString();
                    logger.Info($"Send to AGV : {sendStr2}");
                    await AGVTcpSocketSaeaSession.SendAsync(sendBytes2);
                    break;
                case 2002:
                    WMSClientTcpSocketSaeaSession = e;
                    WMSClientConnectionState = WMSClientConnectionState.Connected;
                    byte[] sendBytes3 = MessageSendToWMSClient.ConfirmConnectionInstructionOrFeedback(FunctionCodeEnum.ConfirmConnection_Instruction);
                    string sendStr3 = sendBytes3.ByteArrayToHexString();
                    logger.Info($"Send to WMSClient : {sendStr3}");
                    await WMSClientTcpSocketSaeaSession.SendAsync(sendBytes3);
                    break;
                default: break;
            }
        }
        private async void OnServerDataReceived(object sender, ServerDataReceivedEventArgs e)
        {
            byte[] receiveBytes = e._dataBytes;
            string receiveStr = receiveBytes.ByteArrayToHexString();
            string strIP = e._session.RemoteEndPoint.Address.ToString();//IP
            int strPort = e._session.LocalEndPoint.Port;//Port

            #region FromWMSClient
            if (strPort == portWMSClientConnect)
            {
                if (receiveStr.InterceptControllerId() == ControllerIdHexString.WMSClient && receiveStr.InterceptFunctionCode() == FunctionCodeHexString.ConfirmConnection_Feedback)
                {
                    logger.Info($"From WMSClient : {receiveStr}.");
                }
                else
                {
                    Object obj = receiveBytes.DeserializeWithBinary();
                    Type type = obj.GetType();

                    if (type == typeof(InitializeCommand))
                    {
                        logger.Info("From WMSClient : InitializeCommand.");
                        //初始化的一系列操作，暂未定
                    }
                    if (type == typeof(OperationModeCommand))
                    {
                        OperationModeCommand operationModeCommand = obj as OperationModeCommand;
                        OperationModeResponse operationModeResponse = new OperationModeResponse();
                        logger.Info($"From WMSClient : OperationCommand {operationModeCommand.Command}.");
                        if (operationModeCommand.Command == "offline")
                        {
                            if (WMSServerTaskState == WMSServerTaskState.None|| WMSServerTaskState == WMSServerTaskState.WMSClient)
                            {
                                WMSServerTaskState = WMSServerTaskState.WMSClient;
                                operationModeResponse.Response = "OfflineIsOk";
                                SendToWMSClient(operationModeResponse.SerializeToBinary());
                                logger.Info("Send to WMSClient : Offline is ok.");
                            }
                            else
                            {
                                operationModeResponse.Response = "OfflineIsWrong";
                                SendToWMSClient(operationModeResponse.SerializeToBinary());
                                logger.Info("Send to WMSClient : Offline is Wrong.");
                            }
                        }
                        if (operationModeCommand.Command == "online")
                        {
                            if (WMSServerTaskState == WMSServerTaskState.WMSClient)
                            {
                                WMSServerTaskState = WMSServerTaskState.MesServer;
                                operationModeResponse.Response = "OnlineIsOk";
                                SendToWMSClient(operationModeResponse.SerializeToBinary());
                                logger.Info("Send to WMSClient : Online is ok.");
                            }
                            else
                            {
                                operationModeResponse.Response = "OnlineIsWrong";
                                SendToWMSClient(operationModeResponse.SerializeToBinary());
                                logger.Info("Send to WMSClient : Online is Wrong.");
                            }
                        }
                    }

                    if (type == typeof(WarehousePLCInputExecuteCommand))
                    {
                        WarehousePLCInputExecuteCommand warehousePLCInputExecuteCommand = obj as WarehousePLCInputExecuteCommand;
                        inputRFIDNumberWMSClient = warehousePLCInputExecuteCommand.InputExecuteCommandRFID;
                        logger.Info($"From WMSClient : WarehousePLCInputExecuteCommand.the RFIDNumber is {inputRFIDNumberWMSClient}");

                        SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.InputExecution, warehousePLCInputExecuteCommand.InputExecuteCommandStorageLocation));
                        //WarehousePLCTrayInputOrOutputCommandFunc(MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.InputExecution, warehousePLCInputExecuteCommand.InputExecuteCommandStorageLocation), strInputPara);
                    }
                    if (type == typeof(WarehousePLCOutputExecuteCommand))
                    {
                        WarehousePLCOutputExecuteCommand warehousePLCOutputExecuteCommand = obj as WarehousePLCOutputExecuteCommand;
                        string strOutputPara = Convert.ToString(warehousePLCOutputExecuteCommand.OutputExecuteCommandStorageLocation, 16).PadLeft(4, '0');
                        outputLocationWMSClient = warehousePLCOutputExecuteCommand.OutputExecuteCommandStorageLocation;
                        logger.Info($"From WMSClient : WarehousePLCOutputExecuteCommand.the outputLocation is {strOutputPara}");

                        SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.OutputExecution, warehousePLCOutputExecuteCommand.OutputExecuteCommandStorageLocation));
                    }
                    if (type == typeof(WarehousePLCInputLoadTrayCommand))
                    {
                        logger.Info("From WMSClient : WarehousePLCInputLoadTrayCommand.");

                        SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.InputLoad));
                        //await _server[0].SendToAsync(WarehousePLCTcpSocketSaeaSession, MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.InputLoad));
                    }
                    if (type == typeof(WarehousePLCOutputUnloadTrayCommand))
                    {
                        logger.Info("From WMSClient : WarehousePLCOutputUnloadTrayCommand.");
                        SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.OutputUnload));
                        //await _server[0].SendToAsync(WarehousePLCTcpSocketSaeaSession, MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.OutputUnload));
                    }
                    if (type == typeof(WarehousePLCReadRFIDCommand))
                    {
                        logger.Info("From WMSClient : WarehousePLCReadRFIDCommand.");
                        SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.ReadRFID, null));
                        //await _server[0].SendToAsync(WarehousePLCTcpSocketSaeaSession, MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.ReadRFID, null));
                    }
                    if (type == typeof(WarehousePLCWriteRFIDCommand))
                    {
                        WarehousePLCWriteRFIDCommand warehousePLCWriteRFIDCommand = obj as WarehousePLCWriteRFIDCommand;
                        logger.Info($"From WMSClient : WarehousePLCWriteRFIDCommand. RFIDnumber is {warehousePLCWriteRFIDCommand.WriteRFIDCommand}.");
                        SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.WriteRFID, warehousePLCWriteRFIDCommand.WriteRFIDCommand));
                        //await _server[0].SendToAsync(WarehousePLCTcpSocketSaeaSession, MessageSendToWarehousePLC.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeWarehousePLCEnum.WriteRFID, warehousePLCWriteRFIDCommand.WriteRFIDCommand));
                    }

                    if (type == typeof(AGVQueryCurrentPositionCommand))
                    {
                        logger.Info("From WMSClient : AGVQueryCurrentPositionCommand.");
                        AGVQueryCurrentPositionCommand aGVQueryCurrentPositionCommand = obj as AGVQueryCurrentPositionCommand;
                        if(aGVQueryCurrentPositionCommand.QueryCurrentPositionCommand== "QueryCurrentPositionCommand")
                        {
                            SendToAGV(MessageSendToAGV.StateQueryInstruction(StateQueryPropertyIdAGVEnum.CurrentPositionProperty, StateQueryModeEnum.Single));
                            //await _server[1].SendToAsync(AGVTcpSocketSaeaSession, MessageSendToAGV.StateQueryInstruction(StateQueryPropertyIdAGVEnum.CurrentPositionProperty, StateQueryModeEnum.Single));
                        }
                    }
                    if (type == typeof(AGVTaskCommand))
                    {
                        AGVTaskCommand aGVTaskCommand = obj as AGVTaskCommand;
                        logger.Info("From WMSClient : AGVTaskCommand.");
                        switch (aGVTaskCommand.TaskCommand)
                        {
                            case 1:
                                SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeAGVEnum.Move, PredefinedTaskFirstParameterAGVEnum.MovetoInputPoint));
                                break;
                            case 2:
                                SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeAGVEnum.Move, PredefinedTaskFirstParameterAGVEnum.MovetoOutputPoint));
                                break;
                            case 3:
                                SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeAGVEnum.Move, PredefinedTaskFirstParameterAGVEnum.MovetoDockingPlatform));
                                break;
                            case 10:
                                SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeAGVEnum.Move, PredefinedTaskFirstParameterAGVEnum.MovetoOriginalPoint));
                                break;
                            case 6:
                                SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeAGVEnum.ReleaseTray, PredefinedTaskFirstParameterAGVEnum.Null));
                                break;
                            case 7:
                                SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(OrderNumber.GetOrderNumber(), PredefinedTaskTypeAGVEnum.LoadTray, PredefinedTaskFirstParameterAGVEnum.Null));
                                break;
                            default:
                                break;
                        }
                    }

                    if (type == typeof(CloudPlatformModifyStorageInfoCommand))
                    {
                        logger.Info("From WMSClient : CloudPlatformModifyStorageInfoCommand.");
                        CloudPlatformModifyStorageInfoCommand cloudPlatformModifyStorageInfoCommand = obj as CloudPlatformModifyStorageInfoCommand;
                        //(1)更新库位信息
                        logger.Info("Send to DataBase : UpdateStorageInfo.");
                        MessageSendToDataBase.UpdateModifyStoragesInfo(cloudPlatformModifyStorageInfoCommand.Type,cloudPlatformModifyStorageInfoCommand.Location,cloudPlatformModifyStorageInfoCommand.RFIDNumberNew);
                        //(2)增加一条库位修改记录
                        logger.Info("Send to DataBase : Add ModifyStorageRecord.");
                        MessageSendToDataBase.AddModifyStorageRecord(cloudPlatformModifyStorageInfoCommand.Type, cloudPlatformModifyStorageInfoCommand.Location, cloudPlatformModifyStorageInfoCommand.RFIDNumberNew);

                        CloudPlatformModifyStorageInfoResponse cloudPlatformModifyStorageInfoResponse = new CloudPlatformModifyStorageInfoResponse();
                        cloudPlatformModifyStorageInfoResponse.Type = cloudPlatformModifyStorageInfoCommand.Type;
                        cloudPlatformModifyStorageInfoResponse.Location = cloudPlatformModifyStorageInfoCommand.Location;
                        cloudPlatformModifyStorageInfoResponse.RFIDNumber = cloudPlatformModifyStorageInfoCommand.RFIDNumberNew;
                        cloudPlatformModifyStorageInfoResponse.CategoryNew = cloudPlatformModifyStorageInfoCommand.CategoryNew;
                        cloudPlatformModifyStorageInfoResponse.AmountNew = cloudPlatformModifyStorageInfoCommand.AmountNew;
                        cloudPlatformModifyStorageInfoResponse.Result = "Ok";

                        logger.Info("Send to WMSClient : CloudPlatformModifyStorageInfoResponse.");
                        SendToWMSClient(cloudPlatformModifyStorageInfoResponse.SerializeToBinary());

                    }
                    if (type == typeof(CloudPlatformStorageQueryCommand))
                    {
                        logger.Info("From WMSClient : CloudPlatformStorageQueryCommand.");
                        List<CloudPlatformStorageQueryResponse> cloudPlatformStorageQueryResponses = new List<CloudPlatformStorageQueryResponse>();
                        List<StorageInfo> storageInfos = MessageSendToDataBase.GetStoragesInfo();

                        foreach (var item in storageInfos)
                        {
                            CloudPlatformStorageQueryResponse cloudPlatformStorageQueryResponse = new CloudPlatformStorageQueryResponse();
                            cloudPlatformStorageQueryResponse.StorageQueryLocation = item.LocationID;
                            cloudPlatformStorageQueryResponse.StorageQueryRFID = item.RFIDNumber;
                            cloudPlatformStorageQueryResponse.StorageQueryCategory = item.Type;
                            cloudPlatformStorageQueryResponse.StorageQueryAmount = item.Number;

                            cloudPlatformStorageQueryResponses.Add(cloudPlatformStorageQueryResponse);
                        }
                        SendToWMSClient(cloudPlatformStorageQueryResponses.SerializeToBinary());
                        logger.Info("Send to WMSClient : CloudPlatformStorageQueryResponse.");
                        //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, cloudPlatformStorageQueryResponses.SerializeToBinary());
                    }
                    if (type == typeof(CloudPlatformQueryInputRecordCommand))
                    {
                        logger.Info("From WMSClient : CloudPlatformQueryInputRecordCommand.");
                        List<CloudPlatformQueryInputRecordResponse> cloudPlatformQueryInputRecordResponses = new List<CloudPlatformQueryInputRecordResponse>();
                        List<InputRecord> inputRecords = MessageSendToDataBase.GetInputRecords();

                        foreach (var item in inputRecords)
                        {
                            CloudPlatformQueryInputRecordResponse cloudPlatformQueryInputRecordResponse = new CloudPlatformQueryInputRecordResponse();
                            cloudPlatformQueryInputRecordResponse.QueryInputRecordResponseLocation = item.Location;
                            cloudPlatformQueryInputRecordResponse.QueryInputRecordResponseRFIDNumber = item.RFIDNumber;
                            cloudPlatformQueryInputRecordResponse.QueryInputRecordResponseCategory = item.Category;
                            cloudPlatformQueryInputRecordResponse.QueryInputRecordResponseAmount = item.Amount;
                            cloudPlatformQueryInputRecordResponse.QueryInputRecordResponseOperationTime = item.InputTime;

                            cloudPlatformQueryInputRecordResponses.Add(cloudPlatformQueryInputRecordResponse);
                        }
                        SendToWMSClient(cloudPlatformQueryInputRecordResponses.SerializeToBinary());
                        logger.Info("Send to WMSClient : CloudPlatformQueryInputRecordResponse.");

                        //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, cloudPlatformQueryInputRecordResponses.SerializeToBinary());

                    }
                    if (type == typeof(CloudPlatformQueryOutputRecordCommand))
                    {
                        logger.Info("From WMSClient : CloudPlatformQueryOutputRecordCommand.");
                        List<CloudPlatformQueryOutputRecordResponse> cloudPlatformQueryOutputRecordResponses = new List<CloudPlatformQueryOutputRecordResponse>();
                        List<OutputRecord> outputRecords = MessageSendToDataBase.GetOutputRecords();

                        foreach (var item in outputRecords)
                        {
                            CloudPlatformQueryOutputRecordResponse cloudPlatformQueryOutputRecordResponse = new CloudPlatformQueryOutputRecordResponse();
                            cloudPlatformQueryOutputRecordResponse.QueryOutputRecordResponseLocation = item.Location;
                            cloudPlatformQueryOutputRecordResponse.QueryOutputRecordResponseRFIDNumber = item.RFIDNumber;
                            cloudPlatformQueryOutputRecordResponse.QueryOutputRecordResponseCategory = item.Category;
                            cloudPlatformQueryOutputRecordResponse.QueryOutputRecordResponseAmount = item.Amount;
                            cloudPlatformQueryOutputRecordResponse.QueryOutputRecordResponseOperationTime = item.OutputTime;

                            cloudPlatformQueryOutputRecordResponses.Add(cloudPlatformQueryOutputRecordResponse);
                        }
                        SendToWMSClient(cloudPlatformQueryOutputRecordResponses.SerializeToBinary());
                        logger.Info("Send to WMSClient : CloudPlatformQueryOutputRecordResponse.");

                        //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, cloudPlatformQueryOutputRecordResponses.SerializeToBinary());
                    }
                    if (type == typeof(CloudPlatformQueryModifyStorageRecordCommand))
                    {
                        logger.Info("From WMSClient : CloudPlatformQueryModifyStorageRecordCommand.");
                        List<CloudPlatformQueryModifyStorageRecordResponse> cloudPlatformQueryModifyStorageRecordResponses = new List<CloudPlatformQueryModifyStorageRecordResponse>();
                        List<ModifyStorageRecord> modifyStorageRecords = MessageSendToDataBase.GetModifyStorageRecords();

                        foreach (var item in modifyStorageRecords)
                        {
                            CloudPlatformQueryModifyStorageRecordResponse cloudPlatformQueryModifyStorageRecordResponse = new CloudPlatformQueryModifyStorageRecordResponse();
                            cloudPlatformQueryModifyStorageRecordResponse.Type = item.ModifyType;
                            cloudPlatformQueryModifyStorageRecordResponse.Location = item.Location;
                            cloudPlatformQueryModifyStorageRecordResponse.RFIDNumber = item.RFIDNumber;
                            cloudPlatformQueryModifyStorageRecordResponse.CategoryNew = item.Category;
                            cloudPlatformQueryModifyStorageRecordResponse.AmountNew = item.Amount;
                            cloudPlatformQueryModifyStorageRecordResponse.ModifyTime = item.ModifyTime;

                            cloudPlatformQueryModifyStorageRecordResponses.Add(cloudPlatformQueryModifyStorageRecordResponse);
                        }
                        SendToWMSClient(cloudPlatformQueryModifyStorageRecordResponses.SerializeToBinary());
                        logger.Info("Send to WMSClient : CloudPlatformQueryModifyStorageRecordResponse.");
                        //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, cloudPlatformQueryModifyStorageRecordResponses.SerializeToBinary());
                    }
                }
            }
            #endregion

            if(strPort== portAGVConnect)
            {
                logger.Info($"From AGV : {receiveStr}.");
            }
            if (strPort == portWarehousePLCConnect)
            {
                logger.Info($"From WarehousePLC : {receiveStr}.");
            }

            //Offline
            if (WMSServerTaskState == WMSServerTaskState.WMSClient)
            {
                if (strPort == portAGVConnect)
                {
                    if (receiveStr.InterceptFunctionCode() == FunctionCodeHexString.PredefinedTask_Feedback)
                    {
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.Move&&
                            receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1()==PredefinedTaskFirstParameterAGVHexString.MovetoOriginalPoint)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVTaskResponse aGVTaskResponse = new AGVTaskResponse();
                                    aGVTaskResponse.TaskResponse = 10;
                                    SendToWMSClient(aGVTaskResponse.SerializeToBinary());
                                    //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, aGVTaskResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.Move &&
                            receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1() == PredefinedTaskFirstParameterAGVHexString.MovetoInputPoint)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVTaskResponse aGVTaskResponse = new AGVTaskResponse();
                                    aGVTaskResponse.TaskResponse = 1;
                                    SendToWMSClient(aGVTaskResponse.SerializeToBinary());
                                    //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, aGVTaskResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.Move &&
                            receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1() == PredefinedTaskFirstParameterAGVHexString.MovetoOutputPoint)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVTaskResponse aGVTaskResponse = new AGVTaskResponse();
                                    aGVTaskResponse.TaskResponse = 2;
                                    SendToWMSClient(aGVTaskResponse.SerializeToBinary());
                                    //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, aGVTaskResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.Move &&
                            receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1() == PredefinedTaskFirstParameterAGVHexString.MovetoPlatform)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVTaskResponse aGVTaskResponse = new AGVTaskResponse();
                                    aGVTaskResponse.TaskResponse = 3;
                                    SendToWMSClient(aGVTaskResponse.SerializeToBinary());
                                    //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, aGVTaskResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.ReleaseTray)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVTaskResponse aGVTaskResponse = new AGVTaskResponse();
                                    aGVTaskResponse.TaskResponse = 6;
                                    SendToWMSClient(aGVTaskResponse.SerializeToBinary());
                                    //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, aGVTaskResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.LoadTray)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVTaskResponse aGVTaskResponse = new AGVTaskResponse();
                                    aGVTaskResponse.TaskResponse = 7;
                                    SendToWMSClient(aGVTaskResponse.SerializeToBinary());
                                    //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, aGVTaskResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    if (receiveStr.InterceptFunctionCode() == FunctionCodeHexString.StateQuery_Feedback)
                    {
                        AGVQueryCurrentPositionResponse aGVQueryCurrentPositionResponse = new AGVQueryCurrentPositionResponse();
                        aGVQueryCurrentPositionResponse.QueryCurrentPositionResponse= Convert.ToInt32(receiveStr.Substring(20, 4), 16);
                        SendToWMSClient(aGVQueryCurrentPositionResponse.SerializeToBinary());
                        //await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, aGVQueryCurrentPositionResponse.SerializeToBinary());
                    }
                    if (receiveStr.InterceptFunctionCode() == FunctionCodeHexString.EventReport_Instruction)
                    {

                    }
                }
                if (strPort == portWarehousePLCConnect)
                {
                    if (receiveStr.InterceptFunctionCode() == FunctionCodeHexString.PredefinedTask_Feedback)
                    {
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.InputLoad)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    WarehousePLCInputLoadTrayResponse warehousePLCInputLoadTrayResponse = new WarehousePLCInputLoadTrayResponse();
                                    warehousePLCInputLoadTrayResponse.InputLoadTrayResponse = "Completed";
                                    await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, warehousePLCInputLoadTrayResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.OutputUnload)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    WarehousePLCOutputUnloadTrayResponse warehousePLCOutputUnloadTrayResponse = new WarehousePLCOutputUnloadTrayResponse();
                                    warehousePLCOutputUnloadTrayResponse.OutputUnloadTrayResponse = "Completed";
                                    await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, warehousePLCOutputUnloadTrayResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.ReadRFID)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    WarehousePLCReadRFIDResponse warehousePLCReadRFIDResponse = new WarehousePLCReadRFIDResponse();
                                    warehousePLCReadRFIDResponse.ReadRFIDResponse = receiveStr.InterceptPredefinedTaskFunctionCodeParameterRFIDNumber();
                                    await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, warehousePLCReadRFIDResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.WriteRFID)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    WarehousePLCWriteRFIDResponse warehousePLCWriteRFIDResponse = new WarehousePLCWriteRFIDResponse();
                                    warehousePLCWriteRFIDResponse.WriteRFIDResponse = "Ok";
                                    await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, warehousePLCWriteRFIDResponse.SerializeToBinary());
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.InputExecution)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    WarehousePLCInputExecuteResponse warehousePLCInputExecuteResponse = new WarehousePLCInputExecuteResponse();
                                    warehousePLCInputExecuteResponse.InputExecuteResponseStorageLocation = Convert.ToInt32(inputRFIDNumberWMSClient.Substring(0,4), 16);
                                    warehousePLCInputExecuteResponse.InputExecuteResponseResult = "Ok";
                                    await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, warehousePLCInputExecuteResponse.SerializeToBinary());
                                    //【1】增加入库记录
                                    logger.Info("Send to DataBase : Add InputRecord.");
                                    MessageSendToDataBase.AddInputRecord(inputRFIDNumberWMSClient);
                                    //【2】更新库位信息
                                    logger.Info("Send to DataBase : Update Storage Info.");
                                    MessageSendToDataBase.UpdateInputStoragesInfo(inputRFIDNumberWMSClient);
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.OutputExecution)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    WarehousePLCOutputExecuteResponse warehousePLCOutputExecuteResponse = new WarehousePLCOutputExecuteResponse();
                                    warehousePLCOutputExecuteResponse.OutputExecuteResponseStorageLocation = outputLocationWMSClient;
                                    warehousePLCOutputExecuteResponse.OutputExecuteResponseResult = "Ok";
                                    await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, warehousePLCOutputExecuteResponse.SerializeToBinary());

                                    int outputLocation = outputLocationWMSClient;
                                    //【1】增加出库记录
                                    logger.Info("Send to DataBase : Add OutputRecord.");
                                    MessageSendToDataBase.AddOutputRecord(outputLocation);
                                    //【2】更新库位信息
                                    logger.Info("Send to DataBase : Update Storage Info.");
                                    MessageSendToDataBase.UpdateOutputStoragesInfo(outputLocation);
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    if (receiveStr.InterceptFunctionCode() == FunctionCodeHexString.StateQuery_Feedback)
                    {
                    }
                    if (receiveStr.InterceptFunctionCode() == FunctionCodeHexString.EventReport_Instruction)
                    {
                    }
                }
            }
            //Online
            if (WMSServerTaskState == WMSServerTaskState.MesServerIsUsing)
            {
                if (strPort == portWarehousePLCConnect)
                {
                    if (receiveStr.InterceptFunctionCode() == FunctionCodeHexString.PredefinedTask_Feedback)
                    {
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.InputExecution)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    //【1】增加一条入库记录
                                    MessageSendToDataBase.AddInputRecord(inputRFIDNumberMesServer);
                                    //【2】更新数据库-将入库的RFID号作为参数
                                    MessageSendToDataBase.UpdateInputStoragesInfo(inputRFIDNumberMesServer);
                                    //【3】清除任务队列的首项，如果队列中有任务则继续执行，没有任务则等待Mes新的任务
                                    MesTaskEndCommand();
                                    //【4】反馈给MES第三个任务完成
                                    await _client.SendAsync(MessageSendToMESServer.PredefinedTaskFeedback(realTimeCommand3.InterceptFunctionCodeParameter(),PredefinedTaskStateEnum.Completed));
                                    //await _client.SendAsync(DataHelper.HexStringToByteArray("100100120020" + realTimeCommand3.Substring(12) + "0001"));
                                    logger.Info("Send To MES :" + "100100120020" + realTimeCommand3.Substring(12) + "0001");
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.OutputExecution)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    //【1】增加一条出库记录
                                    MessageSendToDataBase.AddOutputRecord(Convert.ToInt32(receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1(),16));
                                    //【2】更新数据库-将库位号作为参数
                                    MessageSendToDataBase.UpdateOutputStoragesInfo(Convert.ToInt32(receiveStr.Substring(32, 4), 16));
                                    //【3】调度AGV到出库点
                                    SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(realTimeCommand1.Substring(12, 16), PredefinedTaskTypeAGVEnum.Move, PredefinedTaskFirstParameterAGVEnum.MovetoOutputPoint));
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.InputLoad)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    Thread.Sleep(500);
                                    //入库上料完成后读取RFID
                                    SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(realTimeCommand3.Substring(12, 16), PredefinedTaskTypeWarehousePLCEnum.ReadRFID, "00000000"));
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.OutputUnload)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.ReadRFID)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    Thread.Sleep(500);
                                    //读取RFID完成后，重新写入空料盘的RFID
                                    SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(realTimeCommand1.Substring(12, 16), PredefinedTaskTypeWarehousePLCEnum.WriteRFID, receiveStr.Substring(32, 4) + "0000"));
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeWarehousePLCHexString.WriteRFID)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    //写入RFID完成后，入库执行
                                    Thread.Sleep(500);
                                    inputRFIDNumberMesServer = receiveStr.Substring(32, 8);
                                    SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(realTimeCommand3.Substring(12, 16), PredefinedTaskTypeWarehousePLCEnum.InputExecution, Convert.ToInt32(receiveStr.Substring(32, 4), 16)));
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                if (strPort == portAGVConnect)
                {
                    if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.Move)
                    {
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1() == PredefinedTaskFirstParameterAGVHexString.MovetoOriginalPoint)
                        {
                             switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVCurrentPosition = AGVCurrentPosition.OriginalPoint;
                                    //agvCurrentPositin = "原点";
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1() == PredefinedTaskFirstParameterAGVHexString.MovetoInputPoint)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVCurrentPosition = AGVCurrentPosition.InputPoint;
                                    //agvCurrentPositin = "入库点";
                                    //立库PLC入库上料
                                    SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(realTimeCommand3.Substring(12, 16), PredefinedTaskTypeWarehousePLCEnum.InputLoad));
                                    //调度AGV放料
                                    SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(realTimeCommand3.Substring(12, 16), PredefinedTaskTypeAGVEnum.ReleaseTray, PredefinedTaskFirstParameterAGVEnum.Null));
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1() == PredefinedTaskFirstParameterAGVHexString.MovetoOutputPoint)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVCurrentPosition = AGVCurrentPosition.OutputPoint;
                                    //agvCurrentPositin = "出库点";
                                    //调度AGV进料
                                    SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(realTimeCommand1.Substring(12, 16), PredefinedTaskTypeAGVEnum.LoadTray, PredefinedTaskFirstParameterAGVEnum.Null));
                                    //立库PLC出库下料
                                    SendToWarehousePLC(MessageSendToWarehousePLC.PredefinedTaskInstruction(realTimeCommand1.Substring(12, 16), PredefinedTaskTypeWarehousePLCEnum.OutputUnload));
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterParameter1() == PredefinedTaskFirstParameterAGVHexString.MovetoPlatform)
                        {
                            switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                            {
                                case PredefinedTaskStateHexString.Confirmed:
                                    break;
                                case PredefinedTaskStateHexString.Completed:
                                    AGVCurrentPosition = AGVCurrentPosition.DockingPlatform;
                                    //agvCurrentPositin = "对接平台点";
                                    //回复Mes取瓶子/取盖子到达对接平台点完成
                                    await _client.SendAsync(MessageSendToMESServer.PredefinedTaskFeedback(realTimeCommand1.InterceptFunctionCodeParameter(),PredefinedTaskStateEnum.Completed));
                                    //await _client.SendAsync(DataHelper.HexStringToByteArray("100100120020" + realTimeCommand1.Substring(12) + "0001"));
                                    break;
                                case PredefinedTaskStateHexString.Progressing:
                                    break;
                                case PredefinedTaskStateHexString.Error:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.ReleaseTray)
                    {
                       
                        switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                        {
                            case PredefinedTaskStateHexString.Confirmed:
                                break;
                            case PredefinedTaskStateHexString.Completed:
                                //if (agvCurrentPositin == "对接平台点")
                                if (AGVCurrentPosition==AGVCurrentPosition.DockingPlatform)
                                {
                                    //对接平台点放料盘完成
                                    await _client.SendAsync(MessageSendToMESServer.PredefinedTaskFeedback(realTimeCommand2.InterceptFunctionCodeParameter(), PredefinedTaskStateEnum.Completed));
                                    //await _client.SendAsync(DataHelper.HexStringToByteArray("100100120020" + realTimeCommand2.Substring(12) + "0001"));
                                    logger.Info("Send To MES :" + "0018100100120020" + realTimeCommand2.Substring(12) + "0001");
                                }
                                break;
                            case PredefinedTaskStateHexString.Progressing:
                                break;
                            case PredefinedTaskStateHexString.Error:
                                break;
                            default:
                                break;
                        }
                    }
                    if (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskType() == PredefinedTaskTypeAGVHexString.LoadTray)
                    {
                        switch (receiveStr.InterceptPredefinedTaskFunctionCodeParameterTaskState())
                        {
                            case PredefinedTaskStateHexString.Confirmed:
                                break;
                            case PredefinedTaskStateHexString.Completed:
                                //if (agvCurrentPositin == "对接平台点")
                                if (AGVCurrentPosition==AGVCurrentPosition.DockingPlatform)
                                {
                                    //调度去入库点
                                    SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(realTimeCommand3.InterceptPredefinedTaskFunctionCodeParameterOrdernumber(), PredefinedTaskTypeAGVEnum.Move, PredefinedTaskFirstParameterAGVEnum.MovetoInputPoint));
                                }
                                //if (agvCurrentPositin == "出库点")
                                if (AGVCurrentPosition==AGVCurrentPosition.OutputPoint)
                                {
                                    //调度去对接平台点
                                    SendToAGV(MessageSendToAGV.PredefinedTaskInstruction(realTimeCommand1.InterceptPredefinedTaskFunctionCodeParameterOrdernumber(), PredefinedTaskTypeAGVEnum.Move, PredefinedTaskFirstParameterAGVEnum.MovetoDockingPlatform));
                                }
                                break;
                            case PredefinedTaskStateHexString.Progressing:
                                break;
                            case PredefinedTaskStateHexString.Error:
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
        private void OnServerClosed(object sender, TcpSocketSaeaSession session)
        {
            string strIP = session.RemoteEndPoint.Address.ToString();
            int port = session.RemoteEndPoint.Port;
            switch (port)
            {
                case 2000:
                    WarehousePLCConnectionState = WarehousePLCConnectionState.DisConnected;
                    break;
                case 2001:
                    AGVConnectionState = AGVConnectionState.DisConnected;
                    break;
                case 2002:
                    WMSClientConnectionState = WMSClientConnectionState.DisConnected;
                    if (WMSServerTaskState == WMSServerTaskState.WMSClient)
                    {
                        WMSServerTaskState = WMSServerTaskState.None;
                    }
                    break;
                default:
                    break;
            }
        }
        #endregion

        public async static void SendToAGV(byte[] bts)
        {
            logger.Info($"Send to AGV : {bts.ByteArrayToHexString()}.");
            await _server[1].SendToAsync(AGVTcpSocketSaeaSession, bts);
        }
        public async static void SendToWMSClient(byte[] bts)
        {
            await _server[2].SendToAsync(WMSClientTcpSocketSaeaSession, bts);
        }
        public async static void SendToWarehousePLC(byte[] bts)
        {
            logger.Info($"Send to WarehousePLC : {bts.ByteArrayToHexString()}");
            await _server[0].SendToAsync(WarehousePLCTcpSocketSaeaSession, bts);
        }

    }
}
