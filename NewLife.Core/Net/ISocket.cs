﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NewLife.Log;

namespace NewLife.Net
{
    /// <summary>基础Socket接口</summary>
    /// <remarks>
    /// 封装所有基础接口的共有特性！
    /// 
    /// 核心设计理念：事件驱动，接口统一，简单易用！
    /// 异常处理理念：确保主流程简单易用，特殊情况的异常通过事件处理！
    /// </remarks>
    public interface ISocket : IDisposable2
    {
        #region 属性
        /// <summary>名称。主要用于日志输出</summary>
        String Name { get; set; }

        /// <summary>基础Socket对象</summary>
        Socket Socket { get; /*set;*/ }

        /// <summary>本地地址</summary>
        NetUri Local { get; set; }

        /// <summary>端口</summary>
        Int32 Port { get; set; }

        /// <summary>是否抛出异常，默认false不抛出。Send/Receive时可能发生异常，该设置决定是直接抛出异常还是通过<see cref="Error"/>事件</summary>
        Boolean ThrowException { get; set; }

        /// <summary>发送统计</summary>
        IStatistics StatSend { get; set; }

        /// <summary>接收统计</summary>
        IStatistics StatReceive { get; set; }

        /// <summary>日志提供者</summary>
        ILog Log { get; set; }

        /// <summary>是否输出发送日志。默认false</summary>
        Boolean LogSend { get; set; }

        /// <summary>是否输出接收日志。默认false</summary>
        Boolean LogReceive { get; set; }
        #endregion

        #region 方法
        /// <summary>已重载。日志加上前缀</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void WriteLog(string format, params object[] args);
        #endregion

        #region 事件
        /// <summary>错误发生/断开连接时</summary>
        event EventHandler<ExceptionEventArgs> Error;
        #endregion
    }

    /// <summary>远程通信Socket，仅具有收发功能</summary>
    public interface ISocketRemote : ISocket
    {
        #region 属性
        /// <summary>远程地址</summary>
        NetUri Remote { get; set; }

        /// <summary>通信开始时间</summary>
        DateTime StartTime { get; }

        /// <summary>最后一次通信时间，主要表示会话活跃时间，包括收发</summary>
        DateTime LastTime { get; }
        #endregion

        #region 方法
        /// <summary>发送数据</summary>
        /// <remarks>
        /// 目标地址由<seealso cref="Remote"/>决定
        /// </remarks>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">数量</param>
        /// <returns>是否成功</returns>
        Boolean Send(Byte[] buffer, Int32 offset = 0, Int32 count = -1);

        /// <summary>接收数据</summary>
        /// <returns></returns>
        Byte[] Receive();

        /// <summary>读取指定长度的数据，一般是一帧</summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">数量</param>
        /// <returns>实际读取字节数</returns>
        Int32 Receive(Byte[] buffer, Int32 offset = 0, Int32 count = -1);
        #endregion

        #region 异步接收
        /// <summary>开始异步接收数据</summary>
        /// <returns>是否成功</returns>
        Boolean ReceiveAsync();

        /// <summary>数据到达事件</summary>
        event EventHandler<ReceivedEventArgs> Received;
        #endregion
    }

    /// <summary>远程通信Socket扩展</summary>
    public static class SocketRemoteHelper
    {
        #region 发送
        /// <summary>发送数据流</summary>
        /// <param name="session">会话</param>
        /// <param name="stream">数据流</param>
        /// <returns>返回是否成功</returns>
        public static Boolean Send(this ISocketRemote session, Stream stream)
        {
            // UDP的最大缓冲区
            var size = 1460L;
            var remain = stream.Length - stream.Position;
            // 空数据直接发出
            if (remain == 0) return session.Send(new Byte[0]);

            // TCP可以加大
            if (remain > size && session.Socket.SocketType == SocketType.Stream)
            {
                // 至少使用发送缓冲区的大小，默认8k
                size = session.Socket.SendBufferSize;
                // 超大数据流，缓存加大一千倍
                while (size < 0x80000000 && remain > size << 10)
                    size <<= 1;
            }
            if (remain < size) size = (Int32)remain;
            var buffer = new Byte[size];
            while (true)
            {
                var count = stream.Read(buffer, 0, buffer.Length);
                if (count <= 0) break;

                if (!session.Send(buffer, 0, count)) return false;

                if (count < buffer.Length) break;
            }
            return true;
        }

        /// <summary>发送字符串</summary>
        /// <param name="session">会话</param>
        /// <param name="msg">要发送的字符串</param>
        /// <param name="encoding">文本编码，默认null表示UTF-8编码</param>
        /// <returns>返回自身，用于链式写法</returns>
        public static Boolean Send(this ISocketRemote session, String msg, Encoding encoding = null)
        {
            if (String.IsNullOrEmpty(msg)) return session.Send(new Byte[0]);

            if (encoding == null) encoding = Encoding.UTF8;
            return session.Send(encoding.GetBytes(msg));
        }
        #endregion

        #region 接收
        /// <summary>接收字符串</summary>
        /// <param name="session">会话</param>
        /// <param name="encoding">文本编码，默认null表示UTF-8编码</param>
        /// <returns></returns>
        public static String ReceiveString(this ISocketRemote session, Encoding encoding = null)
        {
            var buffer = session.Receive();
            if (buffer == null || buffer.Length < 1) return null;

            if (encoding == null) encoding = Encoding.UTF8;
            return encoding.GetString(buffer);
        }
        #endregion
    }
}