using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Logger
{
    public static class Logs
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        // 为每个日志级别提供两个重载：
        // - 仅消息
        // - 异常 + 消息（将异常作为参数传递，让 NLog 格式化）

        public static void LogTrace(string message) => Logger.Trace(message);
        public static void LogTrace(Exception ex, string message) => Logger.Trace(ex, message);

        public static void LogDebug(string message) => Logger.Debug(message);
        public static void LogDebug(Exception ex, string message) => Logger.Debug(ex, message);

        public static void LogInfo(string message) => Logger.Info(message);
        public static void LogInfo(Exception ex, string message) => Logger.Info(ex, message);

        public static void LogWarn(string message) => Logger.Warn(message);
        public static void LogWarn(Exception ex, string message) => Logger.Warn(ex, message);

        public static void LogError(string message) => Logger.Error(message);
        public static void LogError(Exception ex, string message) => Logger.Error(ex, message);
        public static void LogError(Exception ex) => Logger.Error(ex);

        public static void LogFatal(string message) => Logger.Fatal(message);
        public static void LogFatal(Exception ex, string message) => Logger.Fatal(ex, message);
        public static void LogFatal(Exception ex) => Logger.Fatal(ex);
    }
}
