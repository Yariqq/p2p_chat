using System;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Management;

namespace P2P_char_forms
{
    public partial class ChatForm : Form
    {
        private delegate void AddMessage(string message);

        private string userName;

        private const int port = 10101;
        private const int portTcp = 6660;
        private string broadcastAddress = "192.168.20.255";
        private IPAddress myIP;

        private Socket sendingSocket;
        private Socket receivingSocket;

        private Socket tcpSocketSender;
        private Socket listenSocket;
        private Socket handler = null;

        Thread receivingThread;
        Thread receivingTcpThread;

        public ChatForm()
        {
            InitializeComponent();
            Load += new EventHandler(ChatForm_Load);
            btnSend.Click += new EventHandler(btnSend_Click);
        }

        private void ChatForm_Load(object sender, EventArgs e)
        {
            using (LoginForm logform = new LoginForm())
            {
                logform.ShowDialog();
                if (logform.UserName == "")
                {
                    Close();
                }
                else
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            myIP = ip;
                            userName = logform.UserName + '[' + ip.ToString() + "] ";
                        }
                    }
                    Show();
                }
            }
            tbSend.Focus();
            InitializeSender();
            InitializeReceiver();

        }

        private void InitializeSender()
        {
            try
            {
                sendingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sendingSocket.Connect(broadcastAddress, port);
                sendingSocket.EnableBroadcast = true;
                string tosend = userName + " : " + "addmetolist";
                byte[] data = Encoding.UTF8.GetBytes(tosend);
                sendingSocket.Send(data, data.Length, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void InitializeReceiver()
        {
            try
            {
                receivingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint ep = new IPEndPoint(myIP, port);
                receivingSocket.Bind(ep);
                receivingSocket.EnableBroadcast = true;

                IPEndPoint ipPoint = new IPEndPoint(myIP, portTcp);
                listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listenSocket.Bind(ipPoint);
                listenSocket.Listen(10);

                ThreadStart startUdp = new ThreadStart(Receiver);
                receivingThread = new Thread(startUdp);
                receivingThread.IsBackground = true;
                receivingThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                if (richTextBox1.Lines.Length != 0)
                {
                    tbSend.Text = tbSend.Text.TrimEnd();
                    if (!string.IsNullOrEmpty(tbSend.Text))
                    {
                        string toSend = userName + " : " + tbSend.Text + '\n';
                        byte[] data = Encoding.UTF8.GetBytes(toSend);
                        tcpSocketSender.Send(data);
                        if (rbChat.InvokeRequired)
                            rbChat.Invoke(new Action<string>((s) => rbChat.AppendText(s)), DateTime.Now.ToString("hh:mm:ss") + ' ' + toSend);
                        else
                            rbChat.AppendText(DateTime.Now.ToString("hh:mm:ss") + ' ' + toSend); ;
                        tbSend.Clear();
                    }
                    tbSend.Focus();
                }
                else
                {
                    MessageBox.Show("You are not connected to another user.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private bool AddToListNewClient(ref string message)
        {
            /*если я сервер и ко мне подключается клиент - отправляю ему
             свой ip и добавляю его в список узлов*/
            if (message.Contains("addmetolist") && !message.Contains(myIP.ToString()))
            {
                string[] extractNameOfClient = message.Split(' ');
                string[] ipToConnect = extractNameOfClient[0].Split('[');
                string onlyIp = ipToConnect[1].Trim(']');
                if (richTextBox1.InvokeRequired)
                    richTextBox1.Invoke(new Action<string>((s) => richTextBox1.AppendText(s)), extractNameOfClient[0]);
                else
                    richTextBox1.AppendText(extractNameOfClient[0]); ;
                message = DateTime.Now.ToString("hh:mm:ss") + ' ' + extractNameOfClient[0] + " has connected...\n";

                string toSend = userName + " : " + "replyfromserver";
                byte[] data = Encoding.UTF8.GetBytes(toSend);
                sendingSocket.Send(data, data.Length, 0);

                ThreadStart startTcp = new ThreadStart(ReceiveTcpMessages);
                receivingTcpThread = new Thread(startTcp);
                receivingTcpThread.IsBackground = true;
                receivingTcpThread.Start();

                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(onlyIp), portTcp);
                tcpSocketSender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                tcpSocketSender.Connect(ep);
                return true;
            }
            /*если я клиент и мне поступило сообщение от сервера с его ip, 
             то подключаюсь к нему по Tcp и отравляю сообщение*/
            else if (message.Contains("replyfromserver"))
            {
                string[] extractNameOfClient = message.Split(' ');
                if (richTextBox1.InvokeRequired)
                    richTextBox1.Invoke(new Action<string>((s) => richTextBox1.AppendText(s)), extractNameOfClient[0]);
                else
                    richTextBox1.AppendText(extractNameOfClient[0]); ;

                ThreadStart startTcp = new ThreadStart(ReceiveTcpMessages);
                receivingTcpThread = new Thread(startTcp);
                receivingTcpThread.IsBackground = true;
                receivingTcpThread.Start();

                string[] ipToConnect = extractNameOfClient[0].Split('[');
                string onlyIp = ipToConnect[1].Trim(']');
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(onlyIp), portTcp);
                tcpSocketSender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                tcpSocketSender.Connect(ep);
                return false;
            }
            else
            {
                return false;
            }
        }

        private void ReceiveTcpMessages()
        {
            try
            {
                handler = listenSocket.Accept();
                if (rbChat.Lines.Length > 2)
                {
                    string tosend = rbChat.Text;
                    byte[] history = Encoding.UTF8.GetBytes(tosend);
                    tcpSocketSender.Send(history, history.Length, 0);
                }
                int bytes = 0;
                byte[] data = new byte[1024];
                while ((bytes = handler.Receive(data)) > 0)
                {
                    string recvMessage = Encoding.UTF8.GetString(data, 0, bytes);
                    if (rbChat.InvokeRequired)
                        rbChat.Invoke(new Action<string>((s) => rbChat.AppendText(s)), DateTime.Now.ToString("hh:mm:ss") + ' ' + recvMessage);
                    else
                        rbChat.AppendText(DateTime.Now.ToString("hh:mm:ss") + ' ' + recvMessage); ;
                }
            }
            catch (Exception ex)
            { }
            finally
            {
                string[] disconnectingClient = handler.RemoteEndPoint.ToString().Split(':');
                string[] lines = richTextBox1.Lines;
                for (int index = 0; index < lines.Length; index++)
                {
                    if (lines[index].Contains(disconnectingClient[0]))
                    {
                        if (rbChat.InvokeRequired)
                            rbChat.Invoke(new Action<string>((s) => rbChat.AppendText(s)), DateTime.Now.ToString("hh:mm:ss") + ' ' + lines[index] + " has disconnected...\n");
                        else
                            rbChat.AppendText(DateTime.Now.ToString("hh:mm:ss") + ' ' + lines[index] + " has disconnected...\n"); ;
                        lines[index] = string.Empty;
                        break;
                    }
                }
                richTextBox1.Lines = lines;
                handler.Close();
                handler.Dispose();
            }
        }

        private void Receiver()
        {
            try
            {
                AddMessage messageDelegate = MessageReceived;
                while (true)
                {
                    byte[] data = new byte[1024];
                    receivingSocket.Receive(data);
                    string message = Encoding.UTF8.GetString(data);
                    if (message.Contains(myIP.ToString()) == false)
                    {
                        if (AddToListNewClient(ref message))
                        {
                            Invoke(messageDelegate, message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void MessageReceived(string message)
        {
            rbChat.Text += message;
        }

        private void tbSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSend.PerformClick();
            }
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (tcpSocketSender != null)
            {
                tcpSocketSender.Close();
            }
            if (listenSocket != null)
            {
                listenSocket.Close();
            }
            if (receivingTcpThread != null)
            {
                receivingTcpThread.Abort();
            }
            if (receivingThread != null)
            {
                receivingThread.Abort();
            }
            if (sendingSocket != null)
            {
                sendingSocket.Close();
            }
            if (receivingSocket != null)
            {
                receivingSocket.Close();
            }
        }

        private void rbChat_TextChanged(object sender, EventArgs e)
        {
            rbChat.SelectionStart = rbChat.Text.Length;
            rbChat.ScrollToCaret();
        }
    }
}
