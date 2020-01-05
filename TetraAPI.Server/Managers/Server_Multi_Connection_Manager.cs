/*
 * This is the source code of Tetra API v0.4
 * It is licensed under GNU GPL v. 3 or later.
 * You should have received a copy of the license in this archive (see LICENSE).
 *
 * Copyright HomamAlamier, 2019-2020.
 */
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
            fileListener.BeginAccept(AcceptCallBack_FileListener, null);
        }

        private void Read_FileClient(IAsyncResult ar)
        {
            if (Disposed) return;
            object[] objs = (object[])ar.AsyncState;
            Socket client = (Socket)objs[4];
            string inf = (string)objs[3];
            bool infMode = (bool)objs[2];
            NetworkStream networkStream = (NetworkStream)objs[1];
            byte[] buffer = (byte[])objs[0];
            try
            {
                int receivedBytesCount = networkStream.EndRead(ar);
                if (infMode)
                {
                    string[] para = Encoding.UTF8.GetString(buffer, 0, receivedBytesCount).Split(new char[] { ':' });
                    switch (para[0])
                    {
                        case "GET":
                            {
                                //Check If the FileID is exists
                                if (FileManager.Contains_FileID(para[1]))
                                {
                                    //Open the file with FileStream
                                    using (FileStream fileStream = new FileStream(FileManager.GetFile(para[1]), FileMode.Open))
                                    {
                                        long currentPos = 0;
                                        string reply = "SIZE:" + fileStream.Length;
                                        var replyBytes = Encoding.UTF8.GetBytes(reply);
                                        networkStream.Write(replyBytes, 0, replyBytes.Length);
                                        networkStream.Flush();
                                        Thread.Sleep(100);
                                        //transporting file as blocks with the size of the buffer
                                        do
                                        {
                                            //Check if the remaining byte's is smaller then buffer size
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
                                    //Send file not found error code
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
                                //Generate Random FileID and create the file and open it with FileStream
                                var fid = FileManager.GenFileId() + "." + para[1];
                                string fn = @"files\" + fid ;
                                FileManager.AddFile(fn, fid);
                                WriteInfo("Receiving File : ID = " + fid);
                                inf = para[2];
                                FileStream fileStream = new FileStream(fn, FileMode.OpenOrCreate);
                                networkStream.BeginRead(buffer, 0, buffer.Length, Read_FileClient, new object[] {
                                    buffer, networkStream, false, inf, client, fileStream });
                            }
                            break;
                    }
                    infMode = false;
                }
                else
                {
                    FileStream fileStream = (FileStream)objs[5];
                    int FileSize = int.Parse(inf);
                    //Write the recieved bytes to the filestream
                    fileStream.Write(buffer, 0, receivedBytesCount);
                    fileStream.Flush();
                    // WriteInfo("Tranafering " + fileStream.Length + "/" + FileSize);
                    //Check if the length of the transported bytes = filesize
                    if (fileStream.Length >= FileSize)
                    {
                        fileStream.Close();
                        networkStream.Close();
                        networkStream.Dispose();
                        client.Close();
                        client.Dispose();
                    }
                    else
                    {
                        networkStream.BeginRead(buffer, 0, buffer.Length, Read_FileClient, new object[] {
                                    buffer, networkStream, infMode, inf, client, fileStream });
                    }
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
