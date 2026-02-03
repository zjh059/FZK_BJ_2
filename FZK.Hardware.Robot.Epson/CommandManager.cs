using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Epson
{
    /// <summary>
    /// 机械臂指令管理器（线程安全，管理待确认指令的全生命周期）
    /// </summary>
    internal class CommandManager
    {
        /// <summary>
        /// 待确认指令字典（Key=指令ID，Value=指令实体）
        /// </summary>
        private readonly ConcurrentDictionary<long, RobotCommand> _pendingCommands = new ConcurrentDictionary<long, RobotCommand>();

        /// <summary>
        /// 全局自增指令ID（保证指令ID唯一，用Interlocked保证原子性）
        /// </summary>
        private long _commandId = 0;

        /// <summary>
        /// 获取下一个唯一指令ID（原子操作，线程安全）
        /// </summary>
        /// <returns>自增ID</returns>
        public long GetNextCommandId()
        {
            return Interlocked.Increment(ref _commandId);
        }

        /// <summary>
        /// 注册指令（发送前注册，加入待确认字典）
        /// </summary>
        /// <param name="command">指令实体</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterCommand(RobotCommand command)
        {
            if (command == null || command.Id <= 0) return false;
            return _pendingCommands.TryAdd(command.Id, command);
        }

        /// <summary>
        /// 更新指令状态（收到机械臂响应/超时/重发时调用）
        /// </summary>
        /// <param name="commandId">指令ID</param>
        /// <param name="updateAction">状态更新操作</param>
        /// <returns>是否更新成功</returns>
        public bool UpdateCommand(long commandId, Action<RobotCommand> updateAction)
        {
            if (!_pendingCommands.TryGetValue(commandId, out var command) || updateAction == null)
                return false;

            // 加锁保证单条指令的状态更新原子性
            lock (command)
            {
                updateAction(command);
            }
            return true;
        }

        /// <summary>
        /// 查询所有超时的指令（超时检查任务调用）
        /// </summary>
        /// <param name="timeoutMs">超时时间(ms)</param>
        /// <returns>超时指令列表</returns>
        public List<RobotCommand> GetTimeoutCommands(int timeoutMs)
        {
            if (timeoutMs <= 0) return new List<RobotCommand>();

            var now = DateTime.Now;
            return _pendingCommands.Values
                .Where(c => c.State == RobotCommandState.Sending || c.State == RobotCommandState.Retrying)
                .Where(c => (now - c.LastSendTime).TotalMilliseconds >= timeoutMs)
                .ToList();
        }

        /// <summary>
        /// 移除指令（执行完成/超时重发完毕后，从待确认字典中移除）
        /// </summary>
        /// <param name="commandId">指令ID</param>
        /// <param name="command">被移除的指令实体</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveCommand(long commandId, out RobotCommand command)
        {
            return _pendingCommands.TryRemove(commandId, out command);
        }

        /// <summary>
        /// 清空所有指令（关闭连接时调用）
        /// </summary>
        public void ClearAllCommands()
        {
            _pendingCommands.Clear();
            // 重置指令ID（可选，根据需求决定）
            Interlocked.Exchange(ref _commandId, 0);
        }

        /// <summary>
        /// 根据指令ID获取指令实体
        /// </summary>
        /// <param name="commandId">指令ID</param>
        /// <returns>指令实体（null则不存在）</returns>
        public RobotCommand GetCommand(long commandId)
        {
            _pendingCommands.TryGetValue(commandId, out var command);
            return command;
        }
    }
}
