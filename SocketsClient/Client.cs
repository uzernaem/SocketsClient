using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Sockets
{
    public partial class frmMain : Form
    {
        private TcpClient Client = new TcpClient();     // клиентский сокет
        private UdpClient UClient = new UdpClient();
        Random rnd = new Random();
        private IPAddress IP;                           // IP-адрес клиента
        private Socket ClientSock;
        private TcpListener Listener;                   // сокет сервера
        private List<Thread> Threads = new List<Thread>();      // список потоков приложения (кроме родительского)
        private bool _continue = true;                          // флаг, указывающий продолжается ли работа с сокетами

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();

            tbLogin.Text += rnd.Next().ToString();

            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());    // информация об IP-адресах и имени машины, на которой запущено приложение
            IP = hostEntry.AddressList[0];                                  // IP-адрес, который будет указан в заголовке окна для идентификации клиента
            int Port = 1010;                                                // порт, который будет указан при создании сокета

            // определяем IP-адрес машины в формате IPv4
            foreach (IPAddress address in hostEntry.AddressList)
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    IP = address;
                    break;
                }

            this.Text += "     " + IP.ToString();                           // выводим IP-адрес текущей машины в заголовок формы

            // создаем серверный сокет (Listener для приема заявок от клиентских сокетов)
            Listener = new TcpListener(IP, 1011);
            Listener.Start();

            // создаем и запускаем поток, выполняющий обслуживание серверного сокета
            Threads.Clear();
            Threads.Add(new Thread(ReceiveMessage));
            Threads[Threads.Count - 1].Start();
        }

        private void ReceiveMessage()
        {
            // входим в бесконечный цикл для работы с клиентскими сокетом
            while (_continue)
            {
                ClientSock = Listener.AcceptSocket();           // получаем ссылку на очередной клиентский сокет
                Threads.Add(new Thread(ReadMessages));          // создаем и запускаем поток, обслуживающий конкретный клиентский сокет
                Threads[Threads.Count - 1].Start(ClientSock);
            }
        }

        private void ReadMessages(object ClientSock)
        {
            string msg = "";        // полученное сообщение

            // входим в бесконечный цикл для работы с клиентским сокетом
            while (_continue)
            {
                byte[] buff = new byte[1024];                           // буфер прочитанных из сокета байтов
                ((Socket)ClientSock).Receive(buff);                     // получаем последовательность байтов из сокета в буфер buff

                msg = Encoding.Unicode.GetString(buff);     // выполняем преобразование байтов в последовательность символов

                rtbMessages.Invoke((MethodInvoker)delegate
                {
                    if (msg.Replace("\0", "") != "")
                        rtbMessages.Text += "\n >> " + msg;             // выводим полученное сообщение на форму
                });
                Thread.Sleep(500);
            }
        }

        // подключение к серверному сокету
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                var RequestData = Encoding.ASCII.GetBytes("SomeRequestData");
                var ServerEp = new IPEndPoint(IPAddress.Any, 0);

                UClient.EnableBroadcast = true;
                UClient.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 8888));

                var ServerResponseData = UClient.Receive(ref ServerEp);
                var ServerResponse = Encoding.ASCII.GetString(ServerResponseData);
                //Console.WriteLine("Recived {0} from {1}", ServerResponse, ServerEp.Address.ToString());
                tbIP.Text = "Найден сервер " + ServerEp.Address.ToString();
                UClient.Close();

                int Port = 1010;                                // номер порта, через который выполняется обмен сообщениями
                IPAddress IP = ServerEp.Address;            // разбор IP-адреса сервера, указанного в поле tbIP
                Client.Connect(IP, Port);                       // подключение к серверному сокету
                btnConnect.Enabled = false;
                btnSend.Enabled = true;
                tbLogin.Enabled = false;
                byte[] buff = Encoding.Unicode.GetBytes(tbLogin.Text);   // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
                Stream stm = Client.GetStream();                                                    // получаем файловый поток клиентского сокета
                stm.Write(buff, 0, buff.Length);
            }
            catch
            {
                MessageBox.Show("Введен некорректный IP-адрес");
            }
        }

        // отправка сообщения
        private void btnSend_Click(object sender, EventArgs e)
        {
            byte[] buff = Encoding.Unicode.GetBytes(tbLogin.Text + " >> " + tbMessage.Text);   // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
            Stream stm = Client.GetStream();                                                    // получаем файловый поток клиентского сокета
            stm.Write(buff, 0, buff.Length);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            byte[] buff = Encoding.Unicode.GetBytes(tbLogin.Text + "_logout");   // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
            Stream stm = Client.GetStream();                                                    // получаем файловый поток клиентского сокета
            stm.Write(buff, 0, buff.Length);

            Client.Close();         // закрытие клиентского сокета

            _continue = false;      // сообщаем, что работа с сокетами завершена

            // завершаем все потоки
            foreach (Thread t in Threads)
            {
                t.Abort();
                t.Join(500);
            }

            // закрываем клиентский сокет
            if (ClientSock != null)
                ClientSock.Close();

            // приостанавливаем "прослушивание" серверного сокета
            if (Listener != null)
                Listener.Stop();
        }
    }
}