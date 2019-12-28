using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using TetraAPI.Handlers;
using System.Threading;
using System.IO;
using System.Net.Security;

namespace TetraAPI.Server
{
    public class ServerMultiConnectManager : Log, IDisposable
    {
        #region Vars
        private Socket mainListener;
        private Socket fileListener;
        private List<Session> sessions;
        private X509Certificate certificate;
        private X509Certificate certificate2;
        private bool Disposed = false;
        private FileManager FileManager;
        #endregion
        #region Events
        public event EventHandler<DataReceiveEventArgs> OnDataReceive;
        public event EventHandler<FileReceiveEventArgs> OnFileReceive;
        #endregion
        #region Properties
        public int SessionCount { get { if (sessions != null) return sessions.Count; else return 0; } }
        #endregion
        #region Thread's
        private Thread timeOutHandler;
        #endregion
        #region struct's
        struct FileStreamX
        {
            public NetworkStream stream;
            public System.IO.FileStream FileStream;
            public string filename;
            public long filelen;
            public bool infoMode;
            public byte[] buffer;
            public Socket Socket;
            public string stringBuffer;
            public long rec;
        }
        #endregion
        public ServerMultiConnectManager(bool console = true) : base("SMCM")
        {
            sessions = new List<Session>();
            if (console) OnLog += LogInConsole;
            LoadCert();
            timeOutHandler = new Thread(new ThreadStart(TimeOut_Handler));
            timeOutHandler.Start();
            FileManager = new FileManager();
        }

        private void TimeOut_Handler()
        {
            while (!Disposed)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    Session session = sessions[i];
                    if (session.TimeOut == 0)
                    {
                        WriteError("Session Ended (Time Out)!. Removing...");
                        session.Socket.Dispose();
                        session.Socket = null;
                        for (int j = session.IndexInList + 1; j < sessions.Count; j++)
                        {
                            WriteInfo("Session " + sessions[i].IndexInList + " Become Session " + (i));
                            sessions[i].IndexInList = i;
                        }
                        sessions.Remove(session);
                    }
                    else
                    {
                        session.TimeOut--;
                        sessions[i] = session;
                    }
                }
                Thread.Sleep(1000);
            }
        }

        internal Session this[int position]
        {
            get
            {
                if (sessions != null && sessions.Count > position) return sessions[position];
                else return null;
            }
        }
        public void StartListening()
        {
            mainListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            mainListener.Bind(new IPEndPoint(IPAddress.Any, 42534));
            mainListener.Listen(5);
            mainListener.BeginAccept(AcceptCallBack, null);
            fileListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            fileListener.Bind(new IPEndPoint(IPAddress.Any, 42535));
            fileListener.Listen(5);
            fileListener.BeginAccept(AcceptCallBack_FileListener, null);
            WriteInfo("Started Listening...", ConsoleColor.Yellow);
        }

        private void AcceptCallBack_FileListener(IAsyncResult ar)
        {
            try
            {
                Socket client = fileListener.EndAccept(ar);
                NetworkStream networkStream = new NetworkStream(client, true);
                byte[] buffer = new byte[4096];
                networkStream.BeginRead(buffer, 0, buffer.Length, Read_FileClient, new object[] { buffer, networkStream, true, "", client });
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
        }

        private void Read_FileClient(IAsyncResult ar)
        {
            if (Disposed) return;
            object[] objs = (object[])ar.AsyncState;
            Socket client = (Socket)objs[4];
            string oper = (string)objs[3];
            bool infMode = (bool)objs[2];
            NetworkStream networkStream = (NetworkStream)objs[1];
            byte[] buffer = (byte[])objs[0];
            try
            {
                int rBC = networkStream.EndRead(ar);
                if (infMode)
                {
                    string[] para = Encoding.UTF8.GetString(buffer, 0, rBC).Split(new char[] { ':' });
                    switch (para[0])
                    {
                        case "GET":
                            {
                                if (File.Exists("files" + para[1]))
                                {
                                    using (FileStream fileStream = new FileStream(@"files\" + para[1], FileMode.Open))
                                    {
                                        long currentPos = 0;
                                        do
                                        {
                                            if (currentPos + buffer.Length > fileStream.Length)
                                            {
                                                int last = (int)(fileStream.Length - currentPos);
                                                fileStream.Read(buffer, 0, last);
                                                networkStream.Write(buffer, 0, last);
                                                networkStream.Flush();
                                                break;
                                            }
                                            else
                                            {
                                                fileStream.Read(buffer, 0, buffer.Length);
                                                networkStream.Write(buffer, 0, buffer.Length);
                                                networkStream.Flush();
                                                currentPos += buffer.Length;
                                            }
                                        } while (true);
                                        networkStream.Close();
                                        networkStream.Dispose();
                                        client.Close();
                                        client.Dispose();
                                    }
                                }
                                else
                                {
                                    networkStream.Write(new byte[] { 255, 244, 255, 244, 255, 244 }, 0, 6);
                                    networkStream.Flush();
                                    networkStream.Close();
                                    networkStream.Dispose();
                                    client.Close();
                                    client.Dispose();
                                }
                            }
                            break;
                        case "POST":
                            {

                            }
                            break;
                    }
                    oper = para[0];
                    infMode = false;
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
        }

        private void AcceptCallBack(IAsyncResult ar)
        {
            if (Disposed) return;
            try
            {
                Socket client = mainListener.EndAccept(ar);
                SslStream stream = new SslStream(new NetworkStream(client, true) { ReadTimeout = 5000, WriteTimeout = 5000 });
                stream.AuthenticateAsServer(certificate, false, System.Security.Authentication.SslProtocols.Tls, false);
                Session x = new Session() { Socket = client, Stream = stream, DataBuffer = new byte[1024], UserIndex = -1 };
                x.IndexInList = sessions.Count;
                x.TimeOut = 10;
                sessions.Add(x);
                WriteInfo("Client connected and authenticated!");
                WriteInfo("Client IP : " + (client.RemoteEndPoint as IPEndPoint).Address.ToString());
                WriteInfo("SSL STREAM: IsEncrypted = " + stream.IsEncrypted + ", SslProtocol = " + stream.SslProtocol + ", IsAuthenticated = " + stream.IsAuthenticated);
                stream.BeginRead(x.DataBuffer, 0, x.DataBuffer.Length, ReadCallBack, x);
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
            mainListener.BeginAccept(AcceptCallBack, null);
        }

        private void ReadCallBack(IAsyncResult ar)
        {
            if (Disposed) return;
            Session session = (Session)ar.AsyncState;
            try
            {
                int size = session.Stream.EndRead(ar);
                string str = UTF8Encoding.UTF8.GetString(session.DataBuffer, 0, size);
                if (str.Length > 0 && str.Substring(str.Length - 1, 1) == "\0")
                {
                    //WriteInfo("Data Received Proccessing...", ConsoleColor.DarkYellow);
                    session.DataStrorage += str;
                    OnDataReceive?.Invoke(this, new DataReceiveEventArgs(session)
                    {
                        StreamIndexInList = session.IndexInList,
                        Data = UTF8Encoding.UTF8.GetBytes(session.DataStrorage)
                    });
                    session.DataStrorage = "";
                }
                else
                {
                    session.DataStrorage += str;
                }
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
            try
            {
                if (session.Socket.Connected) session.Stream.BeginRead(session.DataBuffer, 0, session.DataBuffer.Length, ReadCallBack, session);
                else throw new Exception("Stream Cant Poll!");
            }
            catch (Exception ex)
            {
                WriteError(ex);
                WriteError("Session Ended !. Removing...");
                if (session.Socket != null)
                {
                    session.Socket.Dispose();
                    session.Socket = null;
                }
                for (int i = session.IndexInList + 1; i < sessions.Count; i++)
                {
                    WriteInfo("Session " + sessions[i].IndexInList + " Become Session " + (sessions[i].IndexInList - 1));
                    sessions[i].IndexInList--;
                }
                sessions.Remove(session);
            }
        }
        public void ReTimeOut(int IndexInList)
        {
            try
            {
                sessions[IndexInList].TimeOut = 10;
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
        }
        public void WriteToStream(int IndexInList, string data)
        {
            if (Disposed) return;
            var stream = sessions[IndexInList].Stream;
            byte[] bytes = UTF8Encoding.UTF8.GetBytes(data + "\0");
            stream.Write(bytes);
            stream.Flush();
        }
        public void WriteToStream(int IndexInList, byte[] data)
        {
            if (Disposed) return;
            var stream = sessions[IndexInList].Stream;
            List<byte> mod = new List<byte>();
            mod.AddRange(data);
            mod.AddRange(UTF8Encoding.UTF8.GetBytes("\0"));
            stream.Write(mod.ToArray());
            stream.Flush();
        }
        public void WriteToStream(int IndexInList, Command data)
        {
            if (Disposed) return;
            try
            {
                var stream = sessions[IndexInList].Stream;
                byte[] bytes = UTF8Encoding.UTF8.GetBytes(data.ToString() + "\0");
                stream.Write(bytes);
                stream.Flush();
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
        }
        void LoadCert()
        {
            if (Disposed) return;
            //X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            //store.Open(OpenFlags.ReadOnly);
            //X509CertificateCollection certificateCollection = store.Certificates.Find(X509FindType.FindBySerialNumber, "43ab5cb44de46cad4e5acd414b436325", true);
            //if (certificateCollection.Count == 1)
            //{
            //    certificate = certificateCollection[0];
            //}
            certificate = new X509Certificate2(@"cert\server.pfx", "password");
            certificate2 = new X509Certificate2(@"cert\server - Copy.pfx", "password");
        }

        public void Dispose()
        {
            Disposed = true;
            mainListener.Close();
            for (int i = 0; i < sessions.Count; i++)
            {
                if (sessions[i].Socket != null && sessions[i].Socket.Connected) sessions[i].Socket.Close();
            }
            sessions.Clear();
            if (timeOutHandler.ThreadState == ThreadState.Running)
            {
                timeOutHandler.Abort();
                timeOutHandler = null;
            }
        }
    }
}
