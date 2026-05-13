using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Language
{
    public static class MultiLang
    {
        // ==============================================
        // 从资源字典读取文本（自动匹配当前语言）
        // ==============================================
        public static string Get(string key)
        {
            return System.Windows. Application.Current?.FindResource(key) as string ?? key;
        }

        // ==============================================
        // 把你代码里出现的所有字符串，全部定义在这里
        // ==============================================
        public static string 重复初始化硬件 => Get("重复初始化硬件");
        public static string 初始化硬件失败 => Get("初始化硬件失败");
        public static string 初始化硬件成功 => Get("初始化硬件成功");
        public static string 左上扫码模块 => Get("左上扫码模块");
        public static string 右上扫码模块 => Get("右上扫码模块");
        public static string 左下扫码模块 => Get("左下扫码模块");
        public static string 右下扫码模块 => Get("右下扫码模块");
        public static string 机械臂扫码模块 => Get("机械臂扫码模块");
        public static string PLC模块 => Get("PLC模块");
        public static string 机械手模块 => Get("机械手模块");
        public static string 以下硬件模块初始化失败 => Get("以下硬件模块初始化失败");
        public static string 关闭欧姆龙PLC失败 => Get("关闭欧姆龙PLC失败");
        public static string 关闭EPSON机械臂失败 => Get("关闭EPSON机械臂失败");
        public static string 关闭扫码枪失败 => Get("关闭扫码枪失败");
        public static string 停止硬件过程中发生异常 => Get("停止硬件过程中发生异常");
        public static string 所有硬件已资源释放完成 => Get("所有硬件已资源释放完成");
        public static string 停止硬件时发生未预期异常 => Get("停止硬件时发生未预期异常");
        public static string Stop方法执行异常 => Get("Stop方法执行异常");
        public static string 正在初始化所有硬件 => Get("正在初始化所有硬件");

        // RunViewModel 多语言
        /// <summary>
        /// 设备启动中...
        /// </summary>
        public static string DeviceStarting => Get("DeviceStarting");
        /// <summary>
        /// 设备初始化完成
        /// </summary>
        public static string DeviceInitCompleted => Get("DeviceInitCompleted");
        /// <summary>
        /// 空闲
        /// </summary>
        public static string Idle => Get("Idle");
        public static string None => Get("None");
        public static string StartDevice => Get("StartDevice");
        public static string StopDevice => Get("StopDevice");
        public static string StatusCheckTimerFail => Get("StatusCheckTimerFail");
        public static string RefreshHardwareStatusError => Get("RefreshHardwareStatusError");
        public static string RefreshHardwareStatusFail => Get("RefreshHardwareStatusFail");
        public static string DeviceStartedMonitorPLC => Get("DeviceStartedMonitorPLC");
        public static string DeviceStarted => Get("DeviceStarted");
        public static string HardwareInitCompleted => Get("HardwareInitCompleted");
        public static string SkipHardwareInit => Get("SkipHardwareInit");
        public static string DeviceStoppedStopMonitor => Get("DeviceStoppedStopMonitor");
        public static string DeviceStopping => Get("DeviceStopping");
        public static string StatusRefreshCompleted => Get("StatusRefreshCompleted");
        public static string StatusRefreshDone => Get("StatusRefreshDone");
        public static string RefreshStatusFail => Get("RefreshStatusFail");
        public static string RefreshFail => Get("RefreshFail");
        public static string Error => Get("Error");
        public static string LogCleared => Get("LogCleared");
        public static string Jig1CountCleared => Get("Jig1CountCleared");
        public static string Jig2CountCleared => Get("Jig2CountCleared");
        public static string ClearJig1CountFail => Get("ClearJig1CountFail");
        public static string ClearJig2CountFail => Get("ClearJig2CountFail");
        public static string PLCReadProcessError => Get("PLCReadProcessError");
        public static string ReadPLCRegisterFail => Get("ReadPLCRegisterFail");
        public static string WritePLCD => Get("WritePLCD");
        public static string NoHardwareSimWritePLCD => Get("NoHardwareSimWritePLCD");
        public static string Success => Get("Success");
        public static string Fail => Get("Fail");
        public static string Jig1ScanTriggered => Get("Jig1ScanTriggered");
        public static string Jig1WeldTriggered => Get("Jig1WeldTriggered");
        public static string Jig1ClearTriggered => Get("Jig1ClearTriggered");
        public static string Jig1StartScanProcess => Get("Jig1StartScanProcess");
        public static string Jig1StartWeldProcess => Get("Jig1StartWeldProcess");
        public static string Jig1StartClearProcess => Get("Jig1StartClearProcess");
        public static string Jig2ScanTriggered => Get("Jig2ScanTriggered");
        public static string Jig2WeldTriggered => Get("Jig2WeldTriggered");
        public static string Jig2ClearTriggered => Get("Jig2ClearTriggered");
        public static string Jig2StartScanProcess => Get("Jig2StartScanProcess");
        public static string Jig2StartWeldProcess => Get("Jig2StartWeldProcess");
        public static string Jig2StartClearProcess => Get("Jig2StartClearProcess");
        public static string Scanning => Get("Scanning");
        public static string ScanPosition => Get("ScanPosition");
        public static string RobotArrivedScanPos => Get("RobotArrivedScanPos");
        public static string DeviceResourceReleased => Get("DeviceResourceReleased");
        public static string ManualTriggerLeftDownScan => Get("ManualTriggerLeftDownScan");
        public static string ManualTriggerLeftUpScan => Get("ManualTriggerLeftUpScan");
        public static string ManualTriggerRightUpScan => Get("ManualTriggerRightUpScan");
        public static string ManualTriggerRightDownScan => Get("ManualTriggerRightDownScan");
        public static string ManualTriggerRobotScan => Get("ManualTriggerRobotScan");
        public static string ManualSimulate => Get("ManualSimulate");
        public static string Manual => Get("Manual");
        public static string LeftDownScan => Get("LeftDownScan");
        public static string LeftUpScan => Get("LeftUpScan");
        public static string RightUpScan => Get("RightUpScan");
        public static string RightDownScan => Get("RightDownScan");
        public static string RobotScan => Get("RobotScan");
        public static string ArrivedScanPos => Get("ArrivedScanPos");
        public static string Jig1ScanPos => Get("Jig1ScanPos");
        public static string SimRobotArrivedScanPos => Get("SimRobotArrivedScanPos");
        public static string ReportSuccessSim => Get("ReportSuccessSim");
        public static string SimRobotReportSuccess => Get("SimRobotReportSuccess");
        public static string SaveSuccess => Get("SaveSuccess");
        public static string AddBindInfo => Get("AddBindInfo");
        public static string EditBindInfo => Get("EditBindInfo");
        public static string DataLoadFailed => Get("DataLoadFailed");
        public static string FilterFailed => Get("FilterFailed");
        public static string BindInfoNotFound => Get("BindInfoNotFound");
        public static string EditDataLoadFailed => Get("EditDataLoadFailed");
        public static string ConfirmDelete => Get("ConfirmDelete");
        public static string ConfirmDeleteBindInfo => Get("ConfirmDeleteBindInfo");
        public static string DeleteSuccess => Get("DeleteSuccess");
        public static string DeleteFailed => Get("DeleteFailed");
        public static string ConfirmBatchDelete => Get("ConfirmBatchDelete");
        public static string ConfirmBatchDeleteBindInfo => Get("ConfirmBatchDeleteBindInfo");
        public static string BatchDeleteSuccess => Get("BatchDeleteSuccess");
        public static string BatchDeleteFailed => Get("BatchDeleteFailed");
        public static string BottomCodeNotEmpty => Get("BottomCodeNotEmpty");
        public static string TopCodeNotEmpty => Get("TopCodeNotEmpty");
        public static string BottomCodeExists => Get("BottomCodeExists");
        public static string Exists => Get("Exists");
        public static string Add => Get("Add");
        public static string Edit => Get("Edit");
        public static string Success2 => Get("Success2");
        public static string Fail2 => Get("Fail2");
        public static string Tip => Get("Tip");
        public static string ValidateTip => Get("ValidateTip");
        public static string AddCodeBind => Get("AddCodeBind");
        public static string EditCodeBind => Get("EditCodeBind");
        public static string DataRefreshFailed => Get("DataRefreshFailed");
        public static string CodeBindNotFound => Get("CodeBindNotFound");
        public static string ConfirmDeleteCodeBind => Get("ConfirmDeleteCodeBind");
        public static string ConfirmBatchDeleteCodeBind => Get("ConfirmBatchDeleteCodeBind");
        public static string SPCodeNotEmpty => Get("SPCodeNotEmpty");
        public static string TestResultNotEmpty => Get("TestResultNotEmpty");
        public static string AddSuccess => Get("AddSuccess");
        public static string AddFailed => Get("AddFailed");
        public static string EditSuccess => Get("EditSuccess");
        public static string EditFailed => Get("EditFailed");
        public static string ExcelNotImplemented => Get("ExcelNotImplemented");
        public static string ExcelExportFailed => Get("ExcelExportFailed");
        public static string CsvNotImplemented => Get("CsvNotImplemented");
        public static string CsvExportFailed => Get("CsvExportFailed");

        // Robot
        public static string NotRunning => Get("NotRunning");
        public static string StartConnect => Get("StartConnect");
        public static string StopDisconnect => Get("StopDisconnect");
        public static string CurrentConnections => Get("CurrentConnections");
        public static string TimedSend => Get("TimedSend");
        public static string StopTimedSend => Get("StopTimedSend");
        public static string RecvBytes => Get("RecvBytes");
        public static string SendBytes => Get("SendBytes");
        public static string TotalConnections => Get("TotalConnections");
        public static string MaxConcurrent => Get("MaxConcurrent");
        public static string TotalTransfer => Get("TotalTransfer");
        public static string Cmd1_QueryStatus => Get("Cmd1_QueryStatus");
        public static string Cmd2_Reset => Get("Cmd2_Reset");
        public static string Cmd3_Heartbeat => Get("Cmd3_Heartbeat");
        public static string Cmd4_Custom => Get("Cmd4_Custom");
        public static string InvalidPort => Get("InvalidPort");
        public static string TcpServerStarted => Get("TcpServerStarted");
        public static string TcpListenCanceled => Get("TcpListenCanceled");
        public static string TcpServerError => Get("TcpServerError");
        public static string ClientConnected => Get("ClientConnected");
        public static string UdpServerStarted => Get("UdpServerStarted");
        public static string Port => Get("Port");
        public static string UdpReceiveCanceled => Get("UdpReceiveCanceled");
        public static string UdpServerError => Get("UdpServerError");
        public static string TcpClientConnected => Get("TcpClientConnected");
        public static string UdpClientInited => Get("UdpClientInited");
        public static string Running => Get("Running");
        public static string StartFailed => Get("StartFailed");
        public static string StoppedDisconnected => Get("StoppedDisconnected");
        public static string StopFailed => Get("StopFailed");
        public static string MonitorCanceled => Get("MonitorCanceled");
        public static string RecvError => Get("RecvError");
        public static string Disconnected => Get("Disconnected");
        public static string SendDataEmpty => Get("SendDataEmpty");
        public static string NoUdpClient => Get("NoUdpClient");
        public static string SendSuccess => Get("SendSuccess");
        public static string SendFailed => Get("SendFailed");
        public static string TimedSendStarted => Get("TimedSendStarted");
        public static string TimedSendStopped => Get("TimedSendStopped");
        public static string Info => Get("Info");
        public static string Warning => Get("Warning");
        public static string Recv => Get("Recv");
        public static string Send => Get("Send");
        public static string ClientListCleared => Get("ClientListCleared");
        public static string ClientDisconnected => Get("ClientDisconnected");
        public static string CmdLibLoaded => Get("CmdLibLoaded");
        public static string LogSaved => Get("LogSaved");
        public static string LogSaveFailed => Get("LogSaveFailed");
        public static string ConfigExported => Get("ConfigExported");
        public static string ConfigExportFailed => Get("ConfigExportFailed");
        public static string ConfigImported => Get("ConfigImported");
        public static string ConfigImportFailed => Get("ConfigImportFailed");
        public static string LogFileFilter => Get("LogFileFilter");
        public static string ConfigFileFilter => Get("ConfigFileFilter");

        // Scanner Debug
        // 扫码器调试
        public static string NotConnected => Get("NotConnected");
        public static string Connected => Get("Connected");
        public static string CloseAutoParse => Get("CloseAutoParse");
        public static string OpenAutoParse => Get("OpenAutoParse");
        public static string ConnectSuccess => Get("ConnectSuccess");
        public static string ConnectSuccessMsg => Get("ConnectSuccessMsg");
        public static string DebugConnected => Get("DebugConnected");
        public static string ConnectFailed => Get("ConnectFailed");
        public static string ConnectFailedMsg => Get("ConnectFailedMsg");
        public static string ConnectError => Get("ConnectError");
        public static string ConnectErrorMsg => Get("ConnectErrorMsg");
        public static string DebugDisconnected => Get("DebugDisconnected");
        public static string DisconnectedMsg => Get("DisconnectedMsg");
        public static string DisconnectError => Get("DisconnectError");
        public static string DisconnectErrorMsg => Get("DisconnectErrorMsg");
        public static string SendFailedDisconnected => Get("SendFailedDisconnected");
        public static string SendCmd => Get("SendCmd");
        public static string SendError => Get("SendError");
        public static string SendErrorMsg => Get("SendErrorMsg");
        public static string SendFailedNotConnected => Get("SendFailedNotConnected");
        public static string AutoParseOn => Get("AutoParseOn");
        public static string AutoParseOff => Get("AutoParseOff");
        public static string RecvData => Get("RecvData");
        public static string ParseResult => Get("ParseResult");
        public static string ParseFailed => Get("ParseFailed");
        public static string ParseError => Get("ParseError");


        public static string Tip_AddUser => Get("Tip_AddUser");
        public static string Tip_EditUser => Get("Tip_EditUser");
        public static string Txt_Error => Get("Txt_Error");
        public static string Txt_Tip => Get("Txt_Tip");
        public static string Txt_Validate => Get("Txt_Validate");
        public static string Msg_RefreshFail => Get("Msg_RefreshFail");
        public static string Msg_FilterFail => Get("Msg_FilterFail");
        public static string Msg_UserNotFound => Get("Msg_UserNotFound");
        public static string Msg_LoadEditFail => Get("Msg_LoadEditFail");
        public static string Dlg_DelTitle => Get("Dlg_DelTitle");
        public static string Dlg_DelConfirm => Get("Dlg_DelConfirm");
        public static string Msg_DelSuccess => Get("Msg_DelSuccess");
        public static string Msg_DelFail => Get("Msg_DelFail");
        public static string Dlg_BatchDelTitle => Get("Dlg_BatchDelTitle");
        public static string Dlg_BatchDelConfirm => Get("Dlg_BatchDelConfirm");
        public static string Msg_BatchDelSuccess => Get("Msg_BatchDelSuccess");
        public static string Msg_BatchDelFail => Get("Msg_BatchDelFail");
        public static string Msg_NameEmpty => Get("Msg_NameEmpty");
        public static string Msg_PwdEmpty => Get("Msg_PwdEmpty");
        public static string Msg_NameExist => Get("Msg_NameExist");
        public static string Txt_Add => Get("Txt_Add");
        public static string Txt_Edit => Get("Txt_Edit");
        public static string Txt_Success => Get("Txt_Success");
        public static string Txt_Fail => Get("Txt_Fail");

        public static string LoginFailed => Get("LoginFailed");
        public static string RegisterSuccess => Get("RegisterSuccess");
        // ===== RunViewModel 新增（按字母顺序，仅新增部分）=====
        public static string AttemptWritePlcD => Get("AttemptWritePlcD");
        public static string ClearSuccessRemark => Get("ClearSuccessRemark");
        public static string DbBtEntityNotFound => Get("DbBtEntityNotFound");
        public static string DbClearCountFailed => Get("DbClearCountFailed");
        public static string DbClearCountSuccess => Get("DbClearCountSuccess");
        public static string DbClearJigCountSuccess => Get("DbClearJigCountSuccess");
        public static string DbClearNoBottomCode => Get("DbClearNoBottomCode");
        public static string DbCodeEntityNotFound => Get("DbCodeEntityNotFound");
        public static string DbCountParseFail => Get("DbCountParseFail");
        public static string DbInsertCodeEntity => Get("DbInsertCodeEntity");
        public static string DbUpdateCodeEntity => Get("DbUpdateCodeEntity");
        public static string DbUpdateCountFailed => Get("DbUpdateCountFailed");
        public static string DbUpdateCountSuccess => Get("DbUpdateCountSuccess");
        public static string DbUpdateTestResult => Get("DbUpdateTestResult");
        public static string DbUpdateTestResultFailed => Get("DbUpdateTestResultFailed");
        public static string DbUpsertCodeEntityFailed => Get("DbUpsertCodeEntityFailed");
        public static string DbVerifyBottomTopFailed => Get("DbVerifyBottomTopFailed");
        public static string ExceptionRemark => Get("ExceptionRemark");
        public static string Jig1 => Get("Jig1");
        public static string Jig1BottomScanFormatError => Get("Jig1BottomScanFormatError");
        public static string Jig1ClearComplete => Get("Jig1ClearComplete");
        public static string Jig1ClearException => Get("Jig1ClearException");
        public static string Jig1ClearFailNoCode => Get("Jig1ClearFailNoCode");
        public static string Jig1ClearParseFail => Get("Jig1ClearParseFail");
        public static string Jig1CountConvertFail => Get("Jig1CountConvertFail");
        public static string Jig1DetectedClearTrigger => Get("Jig1DetectedClearTrigger");
        public static string Jig1DetectedScanTrigger => Get("Jig1DetectedScanTrigger");
        public static string Jig1DetectedWeldTrigger => Get("Jig1DetectedWeldTrigger");
        public static string Jig1ScanException => Get("Jig1ScanException");
        public static string Jig1ScanFailRetryExhausted => Get("Jig1ScanFailRetryExhausted");
        public static string Jig1ScanParseRetry => Get("Jig1ScanParseRetry");
        public static string Jig1VerifyFail => Get("Jig1VerifyFail");
        public static string Jig1VerifySuccess => Get("Jig1VerifySuccess");
        public static string Jig1WeldComplete => Get("Jig1WeldComplete");
        public static string Jig1WeldException => Get("Jig1WeldException");
        public static string Jig1WeldParseFail => Get("Jig1WeldParseFail");
        public static string Jig1WeldParseRetry => Get("Jig1WeldParseRetry");
        public static string Jig1WeldScanFailRetryExhausted => Get("Jig1WeldScanFailRetryExhausted");
        public static string Jig2 => Get("Jig2");
        public static string Jig2BottomScanFormatError => Get("Jig2BottomScanFormatError");
        public static string Jig2ClearComplete => Get("Jig2ClearComplete");
        public static string Jig2ClearException => Get("Jig2ClearException");
        public static string Jig2ClearFailNoCode => Get("Jig2ClearFailNoCode");
        public static string Jig2ClearParseFail => Get("Jig2ClearParseFail");
        public static string Jig2CountConvertFail => Get("Jig2CountConvertFail");
        public static string Jig2DetectedClearTrigger => Get("Jig2DetectedClearTrigger");
        public static string Jig2DetectedScanTrigger => Get("Jig2DetectedScanTrigger");
        public static string Jig2DetectedWeldTrigger => Get("Jig2DetectedWeldTrigger");
        public static string Jig2ScanException => Get("Jig2ScanException");
        public static string Jig2ScanFailRetryExhausted => Get("Jig2ScanFailRetryExhausted");
        public static string Jig2ScanParseRetry => Get("Jig2ScanParseRetry");
        public static string Jig2VerifyFail => Get("Jig2VerifyFail");
        public static string Jig2VerifySuccess => Get("Jig2VerifySuccess");
        public static string Jig2WeldComplete => Get("Jig2WeldComplete");
        public static string Jig2WeldException => Get("Jig2WeldException");
        public static string Jig2WeldParseFail => Get("Jig2WeldParseFail");
        public static string Jig2WeldParseRetry => Get("Jig2WeldParseRetry");
        public static string Jig2WeldScanFailRetryExhausted => Get("Jig2WeldScanFailRetryExhausted");
        public static string ManualScanException => Get("ManualScanException");
        public static string ManualScanExceptionLog => Get("ManualScanExceptionLog");
        public static string ManualScanFail => Get("ManualScanFail");
        public static string ManualScanSuccess => Get("ManualScanSuccess");
        public static string ManualSimulateLog => Get("ManualSimulateLog");
        public static string ManualTriggerSuccessRemark => Get("ManualTriggerSuccessRemark");
        public static string MesNgRemark => Get("MesNgRemark");
        public static string MesOkRemark => Get("MesOkRemark");
        public static string Ng => Get("Ng");
        public static string NoHardwareSimWritePlcD => Get("NoHardwareSimWritePlcD");
        public static string Ok => Get("Ok");
        public static string ParseFallbackOrder => Get("ParseFallbackOrder");
        public static string ParseFallbackSingle => Get("ParseFallbackSingle");
        public static string PlcWriteStatsFooter => Get("PlcWriteStatsFooter");
        public static string PlcWriteStatsHeader => Get("PlcWriteStatsHeader");
        public static string PlcWriteStatsLine => Get("PlcWriteStatsLine");
        public static string ReportFail => Get("ReportFail");
        public static string ReportFailRemark => Get("ReportFailRemark");
        public static string ReportSuccess => Get("ReportSuccess");
        public static string ReportSuccessRemark => Get("ReportSuccessRemark");
        public static string ResetPlcErrorRegistersFail => Get("ResetPlcErrorRegistersFail");
        public static string Robot => Get("Robot");
        public static string RobotLogicException => Get("RobotLogicException");
        public static string RobotReportComplete => Get("RobotReportComplete");
        public static string RobotScanFailNoCode => Get("RobotScanFailNoCode");
        public static string RobotScanSuccess => Get("RobotScanSuccess");
        public static string ScanFail => Get("ScanFail");
        public static string ScanFailRemark => Get("ScanFailRemark");
        public static string ScanFailRetryExhaustedRemark => Get("ScanFailRetryExhaustedRemark");
        public static string ScanTypeBottomTop => Get("ScanTypeBottomTop");
        public static string ScanTypeClear => Get("ScanTypeClear");
        public static string ScanTypeReport => Get("ScanTypeReport");
        public static string ScanTypeWeldResult => Get("ScanTypeWeldResult");
        public static string SimArrivedScanPos => Get("SimArrivedScanPos");
        public static string SimJig1ScanPos => Get("SimJig1ScanPos");
        public static string SimReportSuccess => Get("SimReportSuccess");
        public static string SimulateRemark => Get("SimulateRemark");
        public static string StopHardwareFailed => Get("StopHardwareFailed");
        public static string VerifyFailRemark => Get("VerifyFailRemark");
        public static string VerifySuccessRemark => Get("VerifySuccessRemark");
        public static string WritePlcDDetail => Get("WritePlcDDetail");
        public static string WritePlcDFailed => Get("WritePlcDFailed");
        public static string WritePlcDFailedDetail => Get("WritePlcDFailedDetail");
        public static string WritePlcDSuccess => Get("WritePlcDSuccess");
        public static string WritePlcDSuccessSim => Get("WritePlcDSuccessSim");
    }
}
