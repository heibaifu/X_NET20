﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NewLife;
using NewLife.Log;
using NewLife.Net;
using NewLife.Net.Sockets;
using NewLife.Reflection;
using NewLife.Threading;
using XCoder;

namespace XNet
{
    public partial class FrmMain : Form
    {
        NetServer _Server;
        ISocketClient _Client;

        #region 窗体
        public FrmMain()
        {
            InitializeComponent();

            this.Icon = IcoHelper.GetIcon("网络");
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            txtReceive.UseWinFormControl();
            //NetHelper.Debug = true;

            txtReceive.SetDefaultStyle(12);
            txtSend.SetDefaultStyle(12);
            numMutilSend.SetDefaultStyle(12);

            gbReceive.Tag = gbReceive.Text;
            gbSend.Tag = gbSend.Text;

            var list = EnumHelper.GetDescriptions<WorkModes>().Select(kv => kv.Value).ToList();
            foreach (var item in GetNetServers())
            {
                list.Add(item.Name);
            }
            cbMode.DataSource = list;
            cbMode.SelectedIndex = 0;

            cbAddr.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAddr.DataSource = GetIPs();

            var config = NetConfig.Current;
            if (config.Port > 0) numPort.Value = config.Port;

            // 加载保存的颜色
            UIConfig.Apply(txtReceive);

            LoadConfig();
        }
        #endregion

        #region 加载/保存 配置
        void LoadConfig()
        {
            var cfg = NetConfig.Current;
            mi显示应用日志.Checked = cfg.ShowLog;
            mi显示网络日志.Checked = cfg.ShowSocketLog;
            mi显示接收字符串.Checked = cfg.ShowReceiveString;
            mi显示发送数据.Checked = cfg.ShowSend;
            mi显示接收数据.Checked = cfg.ShowReceive;
            mi显示统计信息.Checked = cfg.ShowStat;
        }

        void SaveConfig()
        {
            var cfg = NetConfig.Current;
            cfg.ShowLog = mi显示应用日志.Checked;
            cfg.ShowSocketLog = mi显示网络日志.Checked;
            cfg.ShowReceiveString = mi显示接收字符串.Checked;
            cfg.ShowSend = mi显示发送数据.Checked;
            cfg.ShowReceive = mi显示接收数据.Checked;
            cfg.ShowStat = mi显示统计信息.Checked;
            cfg.Save();
        }
        #endregion

        #region 收发数据
        TimerX _timer;
        void Connect()
        {
            _Server = null;
            _Client = null;

            var port = (Int32)numPort.Value;

            var cfg = NetConfig.Current;
            cfg.Port = port;

            var mode = GetMode();
            switch (mode)
            {
                case WorkModes.UDP_TCP:
                    _Server = new NetServer();
                    break;
                case WorkModes.UDP_Server:
                    _Server = new NetServer();
                    _Server.ProtocolType = ProtocolType.Udp;
                    break;
                case WorkModes.TCP_Server:
                    _Server = new NetServer();
                    _Server.ProtocolType = ProtocolType.Tcp;
                    break;
                case WorkModes.TCP_Client:
                    var tcp = new TcpSession();
                    _Client = tcp;

                    cfg.Address = cbAddr.Text;
                    break;
                case WorkModes.UDP_Client:
                    var udp = new UdpServer();
                    _Client = udp;

                    cfg.Address = cbAddr.Text;
                    break;
                default:
                    if ((Int32)mode > 0)
                    {
                        var ns = GetNetServers().Where(n => n.Name == cbMode.Text).FirstOrDefault();
                        if (ns == null) throw new XException("未识别服务[{0}]", mode);

                        _Server = ns.GetType().CreateInstance() as NetServer;
                    }
                    break;
            }

            if (_Client != null)
            {
                _Client.Log = cfg.ShowLog ? XTrace.Log : Logger.Null;
                _Client.Received += OnReceived;
                _Client.Remote.Port = port;
                _Client.Remote.Host = cbAddr.Text;

                _Client.LogSend = cfg.ShowSend;
                _Client.LogReceive = cfg.ShowReceive;

                _Client.Open();
            }
            else if (_Server != null)
            {
                if (_Server == null) _Server = new NetServer();
                _Server.Log = cfg.ShowLog ? XTrace.Log : Logger.Null;
                _Server.SocketLog = cfg.ShowSocketLog ? XTrace.Log : Logger.Null;
                _Server.Port = port;
                if (!cbAddr.Text.Contains("所有本地")) _Server.Local.Host = cbAddr.Text;
                _Server.Received += OnReceived;

                _Server.LogSend = cfg.ShowSend;
                _Server.LogReceive = cfg.ShowReceive;

                _Server.Start();
            }

            pnlSetting.Enabled = false;
            btnConnect.Text = "关闭";

            cfg.Save();

            _timer = new TimerX(ShowStat, null, 5000, 5000);

            BizLog = TextFileLog.Create("NetLog");
        }

        void Disconnect()
        {
            if (_Client != null)
            {
                _Client.Dispose();
                _Client = null;
            }
            if (_Server != null)
            {
                _Server.Dispose();
                _Server = null;
            }
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            pnlSetting.Enabled = true;
            btnConnect.Text = "打开";
        }

        void ShowStat(Object state)
        {
            if (!NetConfig.Current.ShowStat) return;

            var sb = new StringBuilder();
            if (_Client != null)
            {
                XTrace.WriteLine("发送：{0} 接收：{1}", _Client.StatSend, _Client.StatReceive);
            }
            else if (_Server != null)
            {
                XTrace.WriteLine("在线：{3:n0}/{4:n0} 会话：{2} 发送：{0} 接收：{1}", _Server.StatSend, _Server.StatReceive, _Server.StatSession, _Server.SessionCount, _Server.MaxSessionCount);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            SaveConfig();

            var btn = sender as Button;
            if (btn.Text == "打开")
                Connect();
            else
                Disconnect();
        }

        /// <summary>业务日志输出</summary>
        ILog BizLog;

        void OnReceived(Object sender, ReceivedEventArgs e)
        {
            var session = sender as ISocketSession;
            if (session == null)
            {
                var ns = sender as INetSession;
                if (ns == null) return;
                session = ns.Session;
            }

            if (NetConfig.Current.ShowReceiveString)
            {
                var line = e.ToStr();
                XTrace.WriteLine(line);

                if (BizLog != null) BizLog.Info(line);
            }
        }

        Int32 _pColor = 0;
        Int32 BytesOfReceived = 0;
        Int32 BytesOfSent = 0;
        Int32 lastReceive = 0;
        Int32 lastSend = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!pnlSetting.Enabled)
            {
                var rcount = BytesOfReceived;
                var tcount = BytesOfSent;
                if (rcount != lastReceive)
                {
                    gbReceive.Text = (gbReceive.Tag + "").Replace("0", rcount + "");
                    lastReceive = rcount;
                }
                if (tcount != lastSend)
                {
                    gbSend.Text = (gbSend.Tag + "").Replace("0", tcount + "");
                    lastSend = tcount;
                }

                if (cbColor.Checked) txtReceive.ColourDefault(_pColor);
                _pColor = txtReceive.TextLength;
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            var str = txtSend.Text;
            if (String.IsNullOrEmpty(str))
            {
                MessageBox.Show("发送内容不能为空！", this.Text);
                txtSend.Focus();
                return;
            }

            // 多次发送
            var count = (Int32)numMutilSend.Value;
            var sleep = (Int32)numSleep.Value;
            var ths = (Int32)numThreads.Value;
            if (count <= 0) count = 1;
            if (sleep <= 0) sleep = 100;

            // 处理换行
            str = str.Replace("\n", "\r\n");

            if (ths <= 1)
                //SendAsync(_Client, str, count, sleep);
                _Client.SendAsync(str.GetBytes(), count, sleep);
            else
            {
                // 多线程测试
                ThreadPoolX.QueueUserWorkItem(() =>
                {
                    for (int i = 0; i < ths; i++)
                    {
                        var client = _Client.Remote.CreateRemote();
                        client.StatSend = _Client.StatSend;
                        client.StatReceive = _Client.StatReceive;
                        //SendAsync(client, str, count, sleep);
                        client.SendAsync(str.GetBytes(), count, sleep);
                    }
                });
            }
        }

        //void SendAsync(ISocketClient client, String str, Int32 count, Int32 sleep)
        //{
        //    ThreadStart func = () =>
        //    {
        //        for (int i = 0; i < count; i++)
        //        {
        //            if (client != null) client.Send(str);

        //            if (count > 1) Thread.Sleep(sleep);
        //        }
        //    };
        //    //ThreadPoolX.QueueUserWorkItem(func);
        //    var th = new Thread(func);
        //    th.Start();
        //}
        #endregion

        #region 右键菜单
        private void mi清空_Click(object sender, EventArgs e)
        {
            txtReceive.Clear();
            //spList.ClearReceive();
        }

        private void mi清空2_Click(object sender, EventArgs e)
        {
            txtSend.Clear();
            //spList.ClearSend();
        }

        private void mi显示应用日志_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            mi.Checked = !mi.Checked;
        }

        private void mi显示网络日志_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            mi.Checked = !mi.Checked;
        }

        private void mi显示发送数据_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            mi.Checked = !mi.Checked;
        }

        private void mi显示接收数据_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            mi.Checked = !mi.Checked;
        }

        private void mi显示统计信息_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            NetConfig.Current.ShowStat = mi.Checked = !mi.Checked;
        }

        private void mi显示接收字符串_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            NetConfig.Current.ShowReceiveString = mi.Checked = !mi.Checked;
        }
        #endregion

        private void cbMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            var mode = GetMode();
            if ((Int32)mode == 0) return;

            switch (mode)
            {
                case WorkModes.TCP_Client:
                case WorkModes.UDP_Client:
                    cbAddr.DropDownStyle = ComboBoxStyle.DropDown;
                    cbAddr.DataSource = null;
                    cbAddr.Items.Clear();
                    cbAddr.Text = NetConfig.Current.Address;
                    break;
                default:
                case WorkModes.UDP_TCP:
                case WorkModes.UDP_Server:
                case WorkModes.TCP_Server:
                    cbAddr.DropDownStyle = ComboBoxStyle.DropDownList;
                    cbAddr.DataSource = GetIPs();
                    break;
                case (WorkModes)0xFF:
                    cbAddr.DropDownStyle = ComboBoxStyle.DropDownList;
                    cbAddr.DataSource = GetIPs();

                    // 端口
                    var ns = GetNetServers().Where(n => n.Name == cbMode.Text).FirstOrDefault();
                    if (ns != null && ns.Port > 0) numPort.Value = ns.Port;

                    break;
            }
        }

        WorkModes GetMode()
        {
            var mode = cbMode.Text;
            if (String.IsNullOrEmpty(mode)) return (WorkModes)0;

            var list = EnumHelper.GetDescriptions<WorkModes>().Where(kv => kv.Value == mode).ToList();
            if (list.Count == 0) return (WorkModes)0xFF;

            return (WorkModes)list[0].Key;
        }

        static String[] GetIPs()
        {
            var list = NetHelper.GetIPs().Select(e => e.ToString()).ToList();
            list.Insert(0, "所有本地IPv4/IPv6");
            list.Insert(1, IPAddress.Any.ToString());
            list.Insert(2, IPAddress.IPv6Any.ToString());

            return list.ToArray();
        }

        static NetServer[] _ns;
        static NetServer[] GetNetServers()
        {
            if (_ns != null) return _ns;

            var list = new List<NetServer>();
            foreach (var item in typeof(NetServer).GetAllSubclasses(true))
            {
                var ns = item.CreateInstance() as NetServer;
                if (ns != null) list.Add(ns);
            }

            return _ns = list.ToArray();
        }
    }
}