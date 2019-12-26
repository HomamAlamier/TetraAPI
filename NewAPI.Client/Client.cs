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
        const string ipAll = "40.121.93.124";
        private Socket socket;
        private SslStream socketStream;
        private int conTrys = 3;
        private int conCurTry = 0;
        private int timeBetweenTrys = 5000;
#pragma warning disable CS0414 // The field 'Client.timeBetweenTrysM' is assigned but its value is never used
        private int timeBetweenTrysM = 1;
#pragma warning restore CS0414 // The field 'Client.timeBetweenTrysM' is assigned but its value is never used
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
#pragma warning disable CS0169 // The field 'Client.filename' is never used
        private string filename;
#pragma warning restore CS0169 // The field 'Client.filename' is never used
        private string sLoc = "";
        #endregion
        #region Threading
        private Thread connectHandler;
        private Thread pingHandler;
        #endregion
        #region Properties
        public bool IsConnected { get => IsConnected; }
        public int ConnectTrys { get => conTrys; set => conTrys = value; }
        public int TimeBetweenTrys { get => timeBetweenTrys; set => timeBetweenTrys = value; }
        public long Latency { get => latency; }
        public User CurrentUser { get => curUser; }
        public ConnectionState ConnectionStates { get; set; }

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
        public Client()
        {
            Disposed = false;
            logg.OnLog += logg.LogInConsole;
            ConnectionStates = ConnectionState.Disconnected;
            SocketPermission permission = new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", 47596);
            permission.Demand();
        }
        public Client(bool Console = true, string StorageLocation = "")
        {
            Disposed = false;
            logg = new Log("Client", StorageLocation);
            this.StorageLocation = StorageLocation;
            if (Console) logg.OnLog += logg.LogInConsole;
            ConnectionStates = ConnectionState.Disconnected;
            SocketPermission permission = new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", 47596);
            permission.Demand();
        }
        public void Start() { connectHandler = new Thread(new ThreadStart(HANDLER_CONNECT)); connectHandler.Start(); }
        private void HANDLER_CONNECT()
        {
            try
            {
                if (conCurTry == conTrys)
                {
                    logg.WriteError("Connection Failed After " + conTrys + " time's! Waiting " + (timeBetweenTrys / 1000) + " seconds before retry.");
                    conCurTry = 0;
                    ConnectionStates = ConnectionState.Disconnected;
                    Thread.Sleep(timeBetweenTrys);
                    timeBetweenTrys = 60000;
                }
                logg.WriteInfo("Trying to connect (Try " + (conCurTry + 1) + ")");
                ConnectionStates = ConnectionState.Connecting;
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IAsyncResult result = socket.BeginConnect(connectIP, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                if (!success) throw new Exception("Connection Timed Out !");
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
                    OnConnectSuccessfully?.Invoke(this, null);
                    conCurTry = 0;
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
            //if (sslPolicyErrors == SslPolicyErrors.None)
            //certificate.has
            //log.Add("Certificate error: " + sslPolicyErrors);
            //return false;
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
                //socket.Shutdown(SocketShutdown.Both);
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
        public void LoginUserAsync(User user)
        {
            if (!socket.Connected) return;
            Command cmd = new Command();
            cmd.CommandName = "LOGN";
            cmd.CommandArgs = user.GetData();
            WriteToStream(socketStream, cmd);
        }

        public void SendMessageAsync(Message msg)
        {
            if (curUser == null || !socket.Connected) return;
            Command cmd = new Command();
            cmd.CommandName = "SMSG";
            cmd.CommandArgs = msg.GetData();
            WriteToStream(socketStream, cmd);
        }
        public void SetUserBlockAsync(bool Block, string PID)
        {
            if (curUser == null || !socket.Connected) return;
            Command cmd = new Command();
            if (Block) cmd.CommandName = "SBLK";
            else cmd.CommandName = "UBLK";
            cmd.CommandArgs = PID;
            WriteToStream(socketStream, cmd);
        }

        public void SearchForUserAsync(string keyWord)
        {
            if (curUser == null || !socket.Connected || keyWord == null || keyWord == "") return;
            if (keyWord[0] != '@') keyWord = "@" + keyWord;
            Command cmd = new Command();
            cmd.CommandName = "SRCH";
            cmd.CommandArgs = keyWord;
            WriteToStream(socketStream, cmd);
        }

        public void CreateGroupAsync(Group group)
        {
            Command cmd = new Command();
            cmd.CommandName = "CRGR";
            cmd.CommandArgs = group.GetData();
            WriteToStream(socketStream, cmd);
        }

        public void AddMemberToGroupAsync(string pid, int grID)
        {
            Command cmd = new Command();
            cmd.CommandName = "AMGR";
            cmd.CommandArgs = pid + "\0" + grID + "\0";
            WriteToStream(socketStream, cmd);
        }

        public void GetGroupInfoAsync(int GRID)
        {
            Command cmd = new Command()
            {
                CommandName = "GTGR",
                CommandArgs = GRID.ToString() + "\0"
            };
            WriteToStream(socketStream, cmd);
        }

        public void RemoveMemberFromGroupAsync(string pid, int grID)
        {
            Command cmd = new Command();
            cmd.CommandName = "RMGR";
            cmd.CommandArgs = pid + "\0" + grID + "\0";
            WriteToStream(socketStream, cmd);
        }

        public void GetUserInfoAsync(string pid)
        {
            Command gusr = new Command() { CommandName = "GUSR", CommandArgs = pid };
            WriteToStream(socketStream, gusr);
        }
        public void SendFile(string filename)
        {
            try
            {
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IAsyncResult result = sock.BeginConnect(connectIP_file, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                if (!success) throw new Exception("Connection Timed Out !");
                if (sock != null && sock.Connected)
                {
                    logg.WriteInfo("File Client Connected Successfully!");
                    NetworkStream stream = new NetworkStream(sock, true);
                    //stream.AuthenticateAsClient(ClientAuthName);
                    long cur = 0;
                    int bufSize = 4096;
                    byte[] buffer;
                    long len = new System.IO.FileInfo(filename).Length;
                    string[] ff = filename.Split(new char[] { '\\' });
                    string xS = ff[ff.Length - 1] + "\0" + len + "\0";
                    var b = UTF8Encoding.UTF8.GetBytes(xS);
                    stream.Write(b, 0, b.Length);
                    stream.Flush();
                    System.IO.FileStream stream1 = new System.IO.FileStream(filename, System.IO.FileMode.Open);
                    while (true)
                    {
                        buffer = new byte[bufSize];
                        if (cur + bufSize > len)
                        {
                            int size = stream1.Read(buffer, 0, (int)(len - cur));
                            stream.Write(buffer, 0, size);
                            stream.Flush();
                            break;
                        }
                        else
                        {
                            int size = stream1.Read(buffer, 0, bufSize);
                            stream.Write(buffer, 0, size);
                            stream.Flush();
                            cur += bufSize;
                        }
                        logg.WriteInfo("Transfering " + cur + "/" + len);
                    }
                    stream1.Close();

                }
            }
            catch (Exception ex)
            {
                logg.WriteError(ex);
                if (ex.InnerException != null) logg.WriteError(ex.InnerException);
            }
        }
    }
}
