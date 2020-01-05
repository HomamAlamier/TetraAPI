/*
 * This is the source code of Tetra API v0.4
 * It is licensed under GNU GPL v. 3 or later.
 * You should have received a copy of the license in this archive (see LICENSE).
 *
 * Copyright HomamAlamier, 2019-2020.
 */
using TetraAPI.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using ThreadState = System.Threading.ThreadState;
namespace TetraAPI.Client
{
    #region Enum's
    [ComVisible(true)]
    public enum ConnectionState { Connected = 0, Disconnected = 1, Connecting = 2 }
    [ComVisible(true)]
    public enum CreateUserState { Success = 0, PIDError = 1, EmailError = 2 }
    [ComVisible(true)]
    public enum LoginState { Success = 0, PasswordError = 1, UserNotFound = 2 }
    [ComVisible(true)]
    public enum MessageError { BlockedFromUser = 0, InvaildUser = 1 }
    #endregion
    #region EventArg's
    public class CreateUserEventArgs : EventArgs
    {
        public CreateUserState State { get; set; }
    }
    public class MessageReceiveEventArgs : EventArgs
    {
        public Message Message { set; get; }
    }
    public class MessageStatusEventArgs : EventArgs
    {
        public int MessageID { get; set; }
        public bool Received { get; set; }
    }
    public class LoginEventArgs : EventArgs
    {
        public LoginState State { get; set; }
    }
    public class MessageErrorEventArgs : EventArgs
    {
        public MessageError Error { set; get; }
    }
    public class SearchEventArgs : EventArgs
    {
        public string[] Results { set; get; }
    }
    public class GroupInfoEventArgs : EventArgs
    {
        public Group Group { get; set; }
    }
    public class UserInfoEventArgs : EventArgs
    {
        public User User { get; set; }
    }
    #endregion
    #region Delegate's
    public delegate void Event(object sender, EventArgs e);
    public delegate void Event_CreateUserEventArgs(object sender, CreateUserEventArgs e);
    public delegate void Event_MessageReceiveEventArgs(object sender, MessageReceiveEventArgs e);
    public delegate void Event_MessageStatusEventArgs(object sender, MessageStatusEventArgs e);
    public delegate void Event_LoginEventArgs(object sender, LoginEventArgs e);
    public delegate void Event_MessageErrorEventArgs(object sender, MessageErrorEventArgs e);
    public delegate void Event_SearchEventArgs(object sender, SearchEventArgs e);
    public delegate void Event_GroupInfo(object sender, GroupInfoEventArgs e);
    public delegate void Event_UserInfo(object sender, UserInfoEventArgs e);
    #endregion
    #region Com Delegate's
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event();
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_MessageReceiveEventArgs(Message msg);
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_CreateUserEventArgs(CreateUserState state);
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_LoginEventArgs(LoginState state);
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_SearchEventArgs(string[] results);
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_MessageStatusEventArgs(int MessageID, bool Received);
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_GroupInfo(Group group);
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_UserInfo(User user);
    [ComVisible(true), UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void COM_Event_MessageErrorEventArgs(MessageError error);
    #endregion
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("7fb85332-d806-4d0e-8d51-0f1fc1a7997f")]
    public interface IClient
    {
        void Start();
        void CreateUserAsync(User user);
        void LoginUserAsync(User user);
        void SendMessageAsync(Message message);
        void SetUserBlockAsync(bool Block, string UserPID);
        void SearchForUserAsync(string keyword);
        void CreateGroupAsync(Group group);
        void AddMemberToGroupAsync(string pid, int grID);
        void RemoveMemberFromGroupAsync(string pid, int grID);
        void GetGroupInfoAsync(int GRID);
        void GetUserInfoAsync(string pid);
        void SendFile(string Filename, object ID, bool IsGroup = false);
        void GetFile(string fileID);
        void Dispose();
        bool IsConnected { get; }
        int ConnectTrys { get; set; }
        int TimeBetweenTrys { get; set; }
        long Latency { get; }
        User CurrentUser { get; }
        ConnectionState ConnectionStates { get; set; }
        unsafe void set_OnConnectSuccessfully([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event vo_id);
        unsafe void set_OnMessageReceive([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_MessageReceiveEventArgs vo_id);
        unsafe void set_OnCreateUser([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_CreateUserEventArgs vo_id);
        unsafe void set_OnMessageStateChange([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_MessageStatusEventArgs vo_id);
        unsafe void set_OnLogin([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_LoginEventArgs vo_id);
        unsafe void set_OnSearchResult([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_SearchEventArgs vo_id);
        unsafe void set_OnMessageError([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_MessageErrorEventArgs vo_id);
        unsafe void set_OnGroupInfoReceived([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_GroupInfo vo_id);
        unsafe void set_OnUserInfoReceived([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_UserInfo vo_id);
    }
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("F64BAA0F-E347-402D-B798-B53CF3B025FB")]
    public class Client : IClient
    {

        #region Vars
        const string ipAll = "127.0.0.1";//= "40.121.93.124";
        private Socket socket;
        private SslStream socketStream;
        private int conTrys = 3;
        private int conCurTry = 0;
        private int timeBetweenTrys = 5000;
        private IPEndPoint connectIP = new IPEndPoint(IPAddress.Parse(ipAll), 42534);
        private IPEndPoint connectIP_file = new IPEndPoint(IPAddress.Parse(ipAll), 42535);
        private string ClientAuthName = "";
        private byte[] socketBuffer;
        private string commandStorage = "";
        private Stopwatch pingWatch;
        private long latency = 0;
        private User curUser;
        private bool Disposed;
        private Log logg;
        private string sLoc = "";
        private List<UploadFileInfo> uploadFilesPendingList;
        private List<string> downloadFilesPendingList;
        #endregion
        #region Threading
        private Thread connectHandler;
        private Thread pingHandler;
        private Thread filesHandler;
        #endregion
        #region Properties
        public bool IsConnected { get => IsConnected; }
        /// <summary>
        /// Count of connect try's
        /// </summary>
        public int ConnectTrys { get => conTrys; set => conTrys = value; }
        /// <summary>
        /// The time between each connect try
        /// </summary>
        public int TimeBetweenTrys { get => timeBetweenTrys; set => timeBetweenTrys = value; }

        /// <summary>
        /// Get\s the latency between the server and the client
        /// </summary>
        public long Latency { get => latency; }
        /// <summary>
        /// Get\s current (logined / created) user info
        /// </summary>
        public User CurrentUser { get => curUser; }
        /// <summary>
        /// Get\s client connection status
        /// </summary>
        public ConnectionState ConnectionStates { get; set; }
        /// <summary>
        /// Get's Or Set's StorageLocation for the client data like logs and database's and media files
        /// </summary>
        public string StorageLocation
        {
            get => sLoc; set
            {
                if (value == "")
                {
                    sLoc = value;
                    return;
                }
                if (!Directory.Exists(value))
                {
                    try
                    {
                        Directory.CreateDirectory(value);
                        sLoc = value;
                    }
                    catch (Exception ex)
                    {
                        logg.WriteError(ex);
                    }
                }
                else
                {
                    sLoc = value;
                }
            }
        }
        #endregion
        #region Events
        public Event OnConnectSuccessfully { get; set; }
        public Event_CreateUserEventArgs OnCreateUser { get; set; }
        public Event_MessageReceiveEventArgs OnMessageReceive { get; set; }
        public Event_MessageStatusEventArgs OnMessageStateChange { get; set; }
        public Event_LoginEventArgs OnLogin { get; set; }
        public Event_MessageErrorEventArgs OnMessageError { get; set; }
        public Event_SearchEventArgs OnSearchResult { get; set; }
        public Event_GroupInfo OnGroupInfoReceived { get; set; }
        public Event_UserInfo OnUserInfoReceived { get; set; }
        #endregion
        #region COM_CallBack's
        public unsafe void set_OnConnectSuccessfully([MarshalAs(UnmanagedType.FunctionPtr)] COM_Event vo_id)
        {
            OnConnectSuccessfully = (s, e) =>
            {
                vo_id();
            };
        }
        public unsafe void set_OnMessageReceive([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_MessageReceiveEventArgs vo_id)
        {
            OnMessageReceive = (s, e) =>
            {
                vo_id(e.Message);
            };
        }
        public unsafe void set_OnCreateUser([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_CreateUserEventArgs vo_id)
        {
            OnCreateUser = (s, e) =>
            {
                vo_id(e.State);
            };
        }
        public unsafe void set_OnMessageStateChange([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_MessageStatusEventArgs vo_id)
        {
            OnMessageStateChange = (s, e) =>
            {
                vo_id(e.MessageID, e.Received);
            };
        }
        public unsafe void set_OnLogin([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_LoginEventArgs vo_id)
        {
            OnLogin = (s, e) =>
            {
                vo_id(e.State);
            };
        }
        public unsafe void set_OnSearchResult([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_SearchEventArgs vo_id)
        {
            OnSearchResult = (s, e) =>
            {
                vo_id(e.Results);
            };
        }
        public unsafe void set_OnMessageError([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_MessageErrorEventArgs vo_id)
        {
            OnMessageError = (s, e) =>
            {
                vo_id(e.Error);
            };
        }
        public unsafe void set_OnGroupInfoReceived([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_GroupInfo vo_id)
        {
            OnGroupInfoReceived = (s, e) =>
            {
                vo_id(e.Group);
            };
        }
        public unsafe void set_OnUserInfoReceived([MarshalAs(UnmanagedType.FunctionPtr)]COM_Event_UserInfo vo_id)
        {
            OnUserInfoReceived = (s, e) =>
            {
                vo_id(e.User);
            };
        }
        #endregion
        #region Structure's
        private struct UploadFileInfo
        {
            public string Filename;
            public object ID;
            public bool IsGroup;
            public UploadFileInfo(string filename, object id, bool isgroup)
            {
                Filename = filename;
                ID = id;
                IsGroup = isgroup;
            }
        }
        #endregion
        /// <summary>
        /// Initialize The Client
        /// </summary>
        public Client()
        {
            Disposed = false;
            logg.OnLog += logg.LogInConsole;
            ConnectionStates = ConnectionState.Disconnected;
            SocketPermission permission = new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", 47596);
            permission.Demand();
            uploadFilesPendingList = new List<UploadFileInfo>();
            downloadFilesPendingList = new List<string>();
        }
        /// <summary>
        /// Initialize The Client
        /// </summary>
        /// <param name="Console">True if you want to log into the console</param>
        /// <param name="StorageLocation">Client files StorageLocation</param>
        public Client(bool Console = true, string StorageLocation = "")
        {
            Disposed = false;
            logg = new Log("Client", StorageLocation);
            this.StorageLocation = StorageLocation;
            if (Console) logg.OnLog += logg.LogInConsole;
            ConnectionStates = ConnectionState.Disconnected;
            SocketPermission permission = new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", 47596);
            permission.Demand();
            uploadFilesPendingList = new List<UploadFileInfo>();
            downloadFilesPendingList = new List<string>();
        }
        /// <summary>
        /// Start The Client
        /// </summary>
        public void Start() { connectHandler = new Thread(new ThreadStart(HANDLER_CONNECT)); connectHandler.Start(); }

        //This thread handle's the connection try's and fails
        private void HANDLER_CONNECT()
        {
            try
            {
                //check if current try's count = total try's count
                if (conCurTry == conTrys)
                {
                    logg.WriteError("Connection Failed After " + conTrys + " time's! Waiting " + (timeBetweenTrys / 1000) + " seconds before retry.");
                    conCurTry = 0;
                    ConnectionStates = ConnectionState.Disconnected;
                    //first time sleep for the time we specified
                    //other time's sleep for 60 second's
                    Thread.Sleep(timeBetweenTrys);
                    timeBetweenTrys = 60000;
                }
                logg.WriteInfo("Trying to connect (Try " + (conCurTry + 1) + ")");
                //Set Connection Status to connecting
                ConnectionStates = ConnectionState.Connecting;
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IAsyncResult result = socket.BeginConnect(connectIP, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                if (!success) throw new Exception("Connection Timed Out !");
                //Check if async ressult was successfull or not
                //If it is successfull begin autenticate with the server
                if (socket != null && socket.Connected)
                {
                    logg.WriteInfo("Client Connected Successfully!");
                    SslStream stream = new SslStream(new NetworkStream(socket, true), false, new RemoteCertificateValidationCallback(CertificateCheck));
                    stream.AuthenticateAsClient(ClientAuthName);
                    socketBuffer = new byte[4096];
                    stream.BeginRead(socketBuffer, 0, socketBuffer.Length, StreamReadHandler, stream);
                    logg.WriteInfo("Ssl tunnel connected successfully!", ConsoleColor.Green);
                    logg.WriteInfo
                        (
                        "SSL STREAM: IsEncrypted = " + stream.IsEncrypted
                        + ", SslProtocol = " + stream.SslProtocol
                        + ", IsAuthenticated = " + stream.IsAuthenticated
                        );
                    ConnectionStates = ConnectionState.Connected;
                    socketStream = stream;
                    //If the authenticate successfully completed invoke OnConnectSuccessfully
                    OnConnectSuccessfully?.Invoke(this, null);
                    conCurTry = 0;
                    //Start Ping Thread
                    pingHandler = new Thread(new ThreadStart(Handle_Ping));
                    pingHandler.Start();
                    timeBetweenTrys = 5000;
                    connectHandler.Abort();
                }
            }
            catch (Exception ex)
            {
                logg.WriteError(ex);
                conCurTry++;
                if (socket != null && ConnectionStates == ConnectionState.Connected) return;
                HANDLER_CONNECT();
            }
        }
        //Thread that handle's the ping operation
        private void Handle_Ping()
        {
            while (socket != null && socket.Connected)
            {
                pingWatch = new Stopwatch();
                pingWatch.Start();
                WriteToStream(socketStream, Command.PingCommand);
                Thread.Sleep(5000);
                if (Disposed) break;
            }
            if (!Disposed)
            {
                logg.WriteError("Connection Lost !. Reconnecting...");
                connectHandler = new Thread(new ThreadStart(HANDLER_CONNECT));
                connectHandler.Start();
            }
            pingHandler.Abort();
        }
        //Thread that handle's file transfering
        private void Handle_Files()
        {
            while (ConnectionStates == ConnectionState.Connected)
            {
                try
                {
                    if (uploadFilesPendingList.Count > 0)
                    {
                        for (int index = 0; index < uploadFilesPendingList.Count; index++)
                        {
                            string filename = uploadFilesPendingList[index].Filename;
                            logg.WriteInfo("Transfering " + filename + "...");
                            using (FileStream fileStream = new FileStream(filename, FileMode.Open))
                            {
                                Socket transferSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                IAsyncResult result = transferSock.BeginConnect(connectIP_file, null, null);
                                bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                                if (!success) throw new Exception("File Transfer Connection Timed Out !");
                                if (transferSock.Connected)
                                {
                                    var fileSplit = filename.Split(new char[] { '.' });
                                    string fileExt = fileSplit[fileSplit.Length - 1];
                                    string cmd = "POST:" + fileExt + ":" + fileStream.Length;
                                    using (NetworkStream networkStream = new NetworkStream(transferSock, true))
                                    {
                                        byte[] buffer = new byte[4096];
                                        long curPos = 0;
                                        var cmdBytes = Encoding.UTF8.GetBytes(cmd);
                                        networkStream.Write(cmdBytes, 0, cmdBytes.Length);
                                        networkStream.Flush();
                                        Thread.Sleep(100);
                                        while (curPos < fileStream.Length)
                                        {
                                            if (curPos + buffer.Length > fileStream.Length)
                                            {
                                                long remainingBytes = fileStream.Length - curPos;
                                                fileStream.Read(buffer, 0, (int)remainingBytes);
                                                networkStream.Write(buffer, 0, (int)remainingBytes);
                                                networkStream.Flush();
                                                curPos += remainingBytes;
                                            }
                                            else
                                            {
                                                fileStream.Read(buffer, 0, buffer.Length);
                                                networkStream.Write(buffer, 0, buffer.Length);
                                                networkStream.Flush();
                                                curPos += 4096;
                                            }
                                        }
                                    }
                                    transferSock.Close();
                                    transferSock.Dispose();
                                    transferSock = null;
                                }
                            }
                            uploadFilesPendingList.RemoveAt(index);
                            logg.WriteInfo("Transfering Finished!");
                        }
                    }
                    if (downloadFilesPendingList.Count > 0)
                    {
                        List<int> doneIndexs = new List<int>();
                        for (int index = 0; index < downloadFilesPendingList.Count; index++)
                        {
                            Socket transferSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            string filename = downloadFilesPendingList[index];
                            IAsyncResult result = transferSock.BeginConnect(connectIP_file, null, null);
                            bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                            if (!success) throw new Exception("File Transfer Connection Timed Out !");
                            if (transferSock.Connected)
                            {
                                NetworkStream networkStream = new NetworkStream(transferSock, true);
                                var cmd = "GET:" + filename;
                                var cmdBytes = Encoding.UTF8.GetBytes(cmd);
                                networkStream.Write(cmdBytes, 0, cmdBytes.Length);
                                networkStream.Flush();
                                byte[] buffer = new byte[4096];
                                networkStream.BeginRead(buffer, 0, buffer.Length, ReceiveFileAsync,
                                    new object[] { buffer, 0, 0, true, transferSock, filename, networkStream, null });
                                doneIndexs.Add(index);
                            }
                        }
                        foreach (var item in doneIndexs)
                            downloadFilesPendingList.RemoveAt(item);
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    logg.WriteError(ex);
                }
            }
        }
        private void ReceiveFileAsync(IAsyncResult ar)
        {
            object[] objs = (object[])ar.AsyncState;
            byte[] buffer = (byte[])objs[0];
            int curPos = (int)objs[1];
            int filesize = (int)objs[2];
            bool infMode = (bool)objs[3];
            Socket transferSock = (Socket)objs[4];
            string filename = (string)objs[5];
            NetworkStream networkStream = (NetworkStream)objs[6];
            FileStream fileStream = (FileStream)objs[7];
            int receivedBytesCount = networkStream.EndRead(ar);
            if (infMode)
            {
                string cmd = Encoding.UTF8.GetString(buffer, 0, receivedBytesCount);
                var split = cmd.Split(new char[] { ':' });
                if (split[0] == "SIZE")
                {
                    filesize = int.Parse(split[1]);
                    infMode = false;
                    fileStream = new FileStream(filename, FileMode.OpenOrCreate);
                    logg.WriteInfo("Receiveing File With Size Of : " + filesize);
                }
            }
            else
            {
                fileStream.Write(buffer, 0, receivedBytesCount);
                fileStream.Flush();
                curPos += receivedBytesCount;
                if (curPos >= filesize)
                {
                    logg.WriteInfo("File Received.");
                    networkStream.Close();
                    networkStream.Dispose();
                    fileStream.Close();
                    fileStream.Dispose();
                    transferSock.Close();
                    transferSock.Dispose();
                    return;
                }
            }
            networkStream.BeginRead(buffer, 0, buffer.Length, ReceiveFileAsync,
                new object[] { buffer, curPos, filesize,
                    infMode, transferSock, filename,
                    networkStream, fileStream });
        }
        //Handle's Incoming Data
        private void StreamReadHandler(IAsyncResult ar)
        {
            var stream = (SslStream)ar.AsyncState;
            try
            {
                int size = stream.EndRead(ar);
                string str = UTF8Encoding.UTF8.GetString(socketBuffer, 0, size);
                if (str.Substring(str.Length - 1, 1) == "\0")
                {
                    commandStorage += str;
                    Command cmd = Command.Parse(commandStorage);
                    if (cmd == Command.PingCommand)
                    {
                        pingWatch.Stop();
                        if (pingWatch.ElapsedMilliseconds < 200) logg.WriteInfo("Ping Status = " + pingWatch.ElapsedMilliseconds + "ms", ConsoleColor.Green);
                        else logg.WriteInfo("Ping Status = " + pingWatch.ElapsedMilliseconds + "ms", ConsoleColor.Red);
                        latency = pingWatch.ElapsedMilliseconds;
                    }
                    //this switch handle's result's and error's from sent command
                    switch (cmd.CommandName)
                    {
                        case "CUSR":
                            {
                                switch (cmd.CommandArgs.Remove(cmd.CommandArgs.Length - 1, 1))
                                {
                                    case "PIDERROR":
                                        OnCreateUser?.Invoke(this, new CreateUserEventArgs() { State = CreateUserState.PIDError });
                                        logg.WriteError("Create user : pid is already exists!");
                                        curUser = null;
                                        break;
                                    case "EMAILERROR":
                                        OnCreateUser?.Invoke(this, new CreateUserEventArgs() { State = CreateUserState.EmailError });
                                        logg.WriteError("Create user : email is already exists!");
                                        curUser = null;
                                        break;
                                    case "SUCCESS":
                                        OnCreateUser?.Invoke(this, new CreateUserEventArgs() { State = CreateUserState.Success });
                                        logg.WriteInfo("Create user successfully completed!", ConsoleColor.Green);
                                        break;
                                }
                            }
                            break;
                        case "NMSG":
                            {
                                Message msg = Message.Parse(cmd.CommandArgs);
                                if (msg.MessageContent.IndexOf("SETRECECIVED") == 0)
                                {
                                    OnMessageStateChange?.Invoke(this, new MessageStatusEventArgs() { MessageID = (int)msg.MessageID, Received = true });
                                }
                                else
                                {
                                    OnMessageReceive?.Invoke(this, new MessageReceiveEventArgs() { Message = msg });
                                }
                            }
                            break;
                        case "LOGN":
                            {
                                if (cmd.CommandArgs[cmd.CommandArgs.Length - 1] == '\0') cmd.CommandArgs = cmd.CommandArgs.Remove(cmd.CommandArgs.Length - 1, 1);
                                if (cmd.CommandArgs == "PASSERROR")
                                {
                                    OnLogin?.Invoke(this, new LoginEventArgs() { State = LoginState.PasswordError });
                                    break;
                                }
                                if (cmd.CommandArgs == "USERNOTFOUND")
                                {
                                    OnLogin?.Invoke(this, new LoginEventArgs() { State = LoginState.UserNotFound });
                                    break;
                                }
                                User x = User.Parse(cmd.CommandArgs);
                                curUser = x;
                                OnLogin?.Invoke(this, new LoginEventArgs() { State = LoginState.Success });
                            }
                            break;
                        case "MSGE":
                            {
                                switch (cmd.CommandArgs.Remove(cmd.CommandArgs.Length - 1, 1))
                                {
                                    case "BLOCKEDUSER": OnMessageError?.Invoke(this, new MessageErrorEventArgs() { Error = MessageError.BlockedFromUser }); break;
                                    case "INVALIDUSER": OnMessageError?.Invoke(this, new MessageErrorEventArgs() { Error = MessageError.InvaildUser }); break;
                                }
                            }
                            break;
                        case "SRCH":
                            {
                                string[] results = cmd.CommandArgs.Remove(cmd.CommandArgs.Length - 1, 1).Split(new char[] { ',' });
                                if (results.Length > 0)
                                {
                                    OnSearchResult?.Invoke(this, new SearchEventArgs() { Results = results });
                                }
                            }
                            break;
                        case "GTGR":
                            {
                                Group x = Group.Parse(cmd.CommandArgs);
                                OnGroupInfoReceived?.Invoke(this, new GroupInfoEventArgs() { Group = x });
                            }
                            break;
                        case "GUSR":
                            {
                                User gusr = User.Parse(cmd.CommandArgs);
                                OnUserInfoReceived?.Invoke(this, new UserInfoEventArgs() { User = gusr });
                            }
                            break;
                    }
                    commandStorage = "";
                }
                else
                {
                    commandStorage += str;
                }
            }
            catch (Exception ex)
            {
                logg.WriteError(ex);
                commandStorage = "";
            }
            try
            {
                stream.BeginRead(socketBuffer, 0, socketBuffer.Length, StreamReadHandler, stream);
            }
            catch (Exception ex)
            {
                logg.WriteError(ex);
            }
        }

        private bool CertificateCheck(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            //Skip cretificate check for now
            return true;
        }
        public void Dispose()
        {
            Disposed = true;
            if (connectHandler != null)
            {
                if (connectHandler.ThreadState == ThreadState.Running) connectHandler.Abort();
                connectHandler = null;
            }
            if (pingHandler != null)
            {
                if (pingHandler.ThreadState == ThreadState.Running) pingHandler.Abort();
                pingHandler = null;
            }
            if (socket.Connected)
            {
                socketStream.Close();
                socket.Close();
            }
            socket = null;
        }
        void WriteToStream(SslStream stream, string data)
        {
            byte[] bytes = UTF8Encoding.UTF8.GetBytes(data + "\0");
            stream.Write(bytes);
            stream.Flush();
        }
        void WriteToStream(SslStream stream, byte[] data)
        {
            List<byte> mod = new List<byte>();
            mod.AddRange(data);
            mod.AddRange(UTF8Encoding.UTF8.GetBytes("\0"));
            stream.Write(mod.ToArray());
            stream.Flush();
        }
        void WriteToStream(SslStream stream, Command data)
        {
            try
            {
                byte[] bytes = UTF8Encoding.UTF8.GetBytes(data.ToString() + "\0");
                stream.Write(bytes);
                stream.Flush();
            }
            catch (Exception e)
            {
                logg.WriteError(e);
            }
        }
        /// <summary>
        /// Send a create user request to server
        /// when the result come back it will invoke OnCreateUser
        /// </summary>
        /// <param name="user">User info to be sent</param>
        public void CreateUserAsync(User user)
        {
            if (curUser == null && !socket.Connected) return;
            if (user.PID[0] != '@') user.PID = "@" + user.PID;
            Command cmd = new Command();
            cmd.CommandName = "CUSR";
            cmd.CommandArgs = user.GetData();
            WriteToStream(socketStream, cmd);
            curUser = user;
        }
        /// <summary>
        /// Send a login user request to server
        /// when the result come back it will invoke OnLogin
        /// </summary>
        /// <param name="user">User info to be sent(email and password needed only)</param>
        public void LoginUserAsync(User user)
        {
            if (!socket.Connected) return;
            Command cmd = new Command();
            cmd.CommandName = "LOGN";
            cmd.CommandArgs = user.GetData();
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a message to a user
        /// when the result come back it will invoke OnMessageReceive
        /// if the result was an error it will invoke OnMessageError
        /// if the result was to change message state it will invoke OnMessageStateChange
        /// </summary>
        /// <param name="msg">Message info to be sent</param>
        public void SendMessageAsync(Message msg)
        {
            if (curUser == null || !socket.Connected) return;
            Command cmd = new Command();
            cmd.CommandName = "SMSG";
            cmd.CommandArgs = msg.GetData();
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a block user request to server
        /// </summary>
        /// <param name="Block">True to block or false to unblock</param>
        /// <param name="PID">User Personal ID to be blocked or unblocked</param>
        public void SetUserBlockAsync(bool Block, string PID)
        {
            if (curUser == null || !socket.Connected) return;
            Command cmd = new Command();
            if (Block) cmd.CommandName = "SBLK";
            else cmd.CommandName = "UBLK";
            cmd.CommandArgs = PID;
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a search for user request to server
        /// when the result come back it will invoke OnSearchResult
        /// </summary>
        /// <param name="keyWord">KeyWord value to be used in the search proccess</param>
        public void SearchForUserAsync(string keyWord)
        {
            if (curUser == null || !socket.Connected || keyWord == null || keyWord == "") return;
            if (keyWord[0] != '@') keyWord = "@" + keyWord;
            Command cmd = new Command();
            cmd.CommandName = "SRCH";
            cmd.CommandArgs = keyWord;
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a create group request to server
        /// </summary>
        /// <param name="group">Group info to be sent</param>
        public void CreateGroupAsync(Group group)
        {
            Command cmd = new Command();
            cmd.CommandName = "CRGR";
            cmd.CommandArgs = group.GetData();
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a add member to group request to server
        /// </summary>
        /// <param name="pid">User Personal ID to be added</param>
        /// <param name="grID">Group ID to be added into</param>
        public void AddMemberToGroupAsync(string pid, int grID)
        {
            Command cmd = new Command();
            cmd.CommandName = "AMGR";
            cmd.CommandArgs = pid + "\0" + grID + "\0";
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a group info request to server
        /// when the result come back it will invoke OnGroupInfoReceived
        /// </summary>
        /// <param name="GRID">Group ID</param>
        public void GetGroupInfoAsync(int GRID)
        {
            Command cmd = new Command()
            {
                CommandName = "GTGR",
                CommandArgs = GRID.ToString() + "\0"
            };
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a remove member to group request to server
        /// </summary>
        /// <param name="pid">User Personal ID to be removed</param>
        /// <param name="grID">Group ID to be removed from</param>
        public void RemoveMemberFromGroupAsync(string pid, int grID)
        {
            Command cmd = new Command();
            cmd.CommandName = "RMGR";
            cmd.CommandArgs = pid + "\0" + grID + "\0";
            WriteToStream(socketStream, cmd);
        }
        /// <summary>
        /// Send a user info request to server
        /// when the result come back it will invoke OnUserInfoReceived
        /// </summary>
        /// <param name="pid">User Personal ID</param>
        public void GetUserInfoAsync(string pid)
        {
            Command gusr = new Command() { CommandName = "GUSR", CommandArgs = pid };
            WriteToStream(socketStream, gusr);
        }
        //The following method is in development
        /// <summary>
        /// Send a file to a user or group (Testing)
        /// </summary>
        /// <param name="Filename">The filepath for the file do you want to upload</param>
        /// <param name="ID">The ID of user/group do you want to send the file to</param>
        /// <param name="IsGroup">Specify if it's group or not</param>
        public void SendFile(string Filename, object ID, bool IsGroup = false)
        {
            if (File.Exists(Filename))
                uploadFilesPendingList.Add(new UploadFileInfo(Filename, ID, IsGroup));
            else
                throw new FileNotFoundException();
            if (filesHandler == null || filesHandler.IsAlive == false)
            {
                filesHandler = new Thread(new ThreadStart(Handle_Files));
                filesHandler.Start();
            }
        }
        /// <summary>
        /// Download a file from the server using specific FileID
        /// </summary>
        /// <param name="fileID">The FileID to send to the server</param>
        public void GetFile(string fileID)
        {
            downloadFilesPendingList.Add(fileID);
        }
    }
}
