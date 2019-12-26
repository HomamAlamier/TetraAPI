using TetraAPI.Handlers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
namespace TetraAPI.Server
{
    public class UserManager : IDisposable
    {
        List<User> users;
        List<List<Message>> usersMessages;
        List<long> usersMessagesIDs;
        List<bool> IsConnectedUser;
        List<Group> groups;

        public UserManager()
        {
            users = new List<User>();
            usersMessages = new List<List<Message>>();
            usersMessagesIDs = new List<long>();
            IsConnectedUser = new List<bool>();
            groups = new List<Group>();
        }
        public int AddUser(User user)
        {
            foreach (var item in users)
            {
                if (item.PID == user.PID) return -1;
                if (item.Email == user.Email) return -2;
            }
            user.ServerID = users.Count;
            users.Add(user);
            usersMessages.Add(new List<Message>());
            usersMessagesIDs.Add(0);
            return 0;
        }
        public void AddGroup(Group group)
        {
            if (group == null) return;
            group.ID = groups.Count;
            groups.Add(group);
        }
        public int AddUserToGroup(string userPID, int groupID)
        {
            if (groupID > -1 && userPID != null)
            {
                return groups[groupID].AddMember(userPID);
            }
            return -1;
        }
        public List<Message> GetUserMessages(string userPID)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].PID == userPID) return usersMessages[i];
            }
            return null;
        }
        public void DeleteMessage(int userIndex, int MessageIndex, bool SendReceived = false, bool ForceRemove = false)
        {
            if (userIndex < users.Count)
            {
                if (ForceRemove && !SendReceived)
                {
                    usersMessages[userIndex].RemoveAt(MessageIndex);
                    return;
                }
                else if (ForceRemove && SendReceived)
                {
                    var item = usersMessages[userIndex][MessageIndex];
                    if (SendReceived && item.MessageFrom != null)
                    {
                        int ind = GetUserIndex(item.MessageFrom);
                        AddMessage(ind, new Message() { MessageDate = DateTime.Now, MessageContent = "SETRECECIVED:" + item.MessageID, MessageTo = item.MessageFrom, MessageID = item.MessageID, MessageFrom = "server" });
                    }
                    usersMessages[userIndex].RemoveAt(MessageIndex);
                    return;
                }
                foreach (var item in usersMessages[userIndex])
                {
                    if (SendReceived && item.MessageFrom != null)
                    {
                        int ind = GetUserIndex(item.MessageFrom);
                        AddMessage(ind, new Message() { MessageDate = DateTime.Now, MessageContent = "SETRECECIVED:" + item.MessageID, MessageTo = item.MessageFrom, MessageID = item.MessageID, MessageFrom = "server" });
                    }
                    if (ForceRemove) usersMessages[userIndex].RemoveAt(MessageIndex);
                    else usersMessages[userIndex].Remove(item);
                    break;
                }
            }
        }
        public List<Message> GetUserMessages(int userIndex)
        {
            if (userIndex > users.Count || userIndex < 0) return null;
            return usersMessages[userIndex];
        }
        public int GetUserIndex(string userPID)
        {
            if (userPID == null) return -1;
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].PID == userPID) return i;
            }
            return -1;
        }
        public Group GetGroup(int GID) { if (GID < groups.Count) return groups[GID]; else return null; }
        public int AddMessage(int Index, Message message)
        {
            if (Index > usersMessages.Count || Index < 0) return -1;
            if (users[Index].BlockedUsers.Contains(message.MessageFrom)) return -2;
            if (message.MessageID < 0) { message.MessageID = usersMessagesIDs[Index]; usersMessagesIDs[Index]++; }
            usersMessages[Index].Add(message);
            return 0;
        }
        public void RemoveUser(User user) { if (user != null) users.Remove(user); }
        public void RemoveGroup(Group group) { if (groups.Contains(group)) groups.Remove(group); }
        public int RemoveUserFromGroup(string userPID, int groupID)
        {
            if (groupID > -1 && userPID != null)
            {
                return groups[groupID].RemoveMember(userPID);
            }
            return -1;
        }
        public int LoginUser(User info, out User user)
        {
            user = null;
            if (info.Email == null || info.Email == "") return -2;
            if (info.Password == null || info.Password == "") return -1;
            foreach (var item in users)
            {
                if (item.Email == info.Email && item.Password == info.Password)
                {
                    User outUser = item;
                    user = outUser;
                    return 0;
                }
                else if (item.Email == info.Email && item.Password != info.Password)
                {
                    user = null;
                    return -1;
                }
            }
            return -2;
        }
        public void SetUserConnected(int userIndex, bool isConnected)
        {
            if (IsConnectedUser.Count <= userIndex) return;
            IsConnectedUser[userIndex] = isConnected;
        }
        public void BlockUser(int userIndex, string blockuserPID)
        {
            if (userIndex >= users.Count || userIndex < 0 || blockuserPID == "" || blockuserPID == null) return;
            if (!users[userIndex].BlockedUsers.Contains(blockuserPID)) users[userIndex].BlockedUsers.Add(blockuserPID);
        }
        public void UnblockUser(int userIndex, string blockuserPID)
        {
            if (userIndex >= users.Count || userIndex < 0 || blockuserPID == "" || blockuserPID == null) return;
            if (users[userIndex].BlockedUsers.Contains(blockuserPID)) users[userIndex].BlockedUsers.Remove(blockuserPID);
        }
        public string[] Search(string keyWord, int userIndex)
        {
            if (keyWord[0] != '@' || userIndex < 0 || userIndex >= users.Count) return null;
            List<string> tmp = new List<string>();
            foreach (var item in users)
            {
                if (item.PID.IndexOf(keyWord) == 0 && item.PID != users[userIndex].PID) tmp.Add(item.PID);
            }
            return tmp.ToArray();
        }
        public void Dispose()
        {
            users = null;
        }

        public User this[int index] { get { if (index < users.Count && index > -1) return users[index]; else return null; } }
    }
    internal class Session
    {
        public Socket Socket { set; get; }
        public SslStream Stream { set; get; }
        public byte[] DataBuffer { get; set; }
        public int IndexInList { get; set; }
        public string DataStrorage { get; set; }
        public int UserIndex { get; set; }
        public int TimeOut { get; set; }
    }
    public class DataReceiveEventArgs : EventArgs
    {
        public byte[] Data;
        internal DataReceiveEventArgs(Session session) => this.session = session;
        private Session session;
        public int DataLength { get => Data.Length; }
        public string UTF8Data { get => UTF8Encoding.UTF8.GetString(Data); }
        public int StreamIndexInList { get; set; }
        public int UserIndex => session.UserIndex;
        public void SetSessionUserIndex(int UserIndex)
        {
            session.UserIndex = UserIndex;
        }
    }
    public class FileReceiveEventArgs : EventArgs
    {
        public FileInf File;
    }
    public class FileManager : Log
    {
        List<string> FileID;
        List<long> FileLen;
        List<string> FilePath;
        Random Random;
        List<char> chars;
        public FileManager() : base("FileManager")
        {
            FileID = new List<string>();
            FileLen = new List<long>();
            FilePath = new List<string>();
            Random = new Random();
            chars = new List<char>();
            for (int i = 0; i < 255; i++)
            {
                if (char.IsLetterOrDigit((char)i))
                {
                    chars.Add((char)i);
                }
            }
        }
        public byte[] GetFile(FileInf fileInf)
        {
            for (int i = 0; i < FileID.Count; i++)
            {
                if (FileID[i] == fileInf.File_Id)
                {
                    return System.IO.File.ReadAllBytes(FilePath[i]);
                }
            }
            return null;
        }
        public FileInf AddFile(string filename)
        {
            long len = new System.IO.FileInfo(filename).Length;
            string id;
            do
            {
                id = GenFileId();
            } while (FileID.Contains(id));
            FileID.Add(id);
            FileLen.Add(len);
            FilePath.Add(filename);
            return new FileInf() { File_Id = id, File_Length = len };
        }
        string GenFileId()
        {
            string id = "";
            for (int i = 0; i < 24; i++)
            {
                id += chars[Random.Next(0, chars.Count)];
            }
            return id;
        }
    }
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
                NetworkStream stream = new NetworkStream(client, true);
                //stream.AuthenticateAsServer(certificate2, false, System.Security.Authentication.SslProtocols.Tls, false);
                WriteInfo("File Stream Connected !");
                FileStreamX streamX = new FileStreamX()
                {
                    stream = stream,
                    infoMode = true,
                    buffer = new byte[4096],
                    Socket = client,
                    stringBuffer = ""
                };
                stream.BeginRead(streamX.buffer, 0, streamX.buffer.Length, Read_FileStreamX, streamX);
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
        }

        private void Read_FileStreamX(IAsyncResult ar)
        {
            if (Disposed) return;
            FileStreamX fileStream = (FileStreamX)ar.AsyncState;
            try
            {
                int rSize = fileStream.stream.EndRead(ar);
                if (fileStream.infoMode)
                {
                    string str = UTF8Encoding.UTF8.GetString(fileStream.buffer, 0, rSize);
                    if (str.Length > 0 && str.Substring(str.Length - 1, 1) == "\0")
                    {
                        fileStream.stringBuffer += str;
                        string[] para = fileStream.stringBuffer.Split(new char[] { '\0' });
                        long len = long.Parse(para[1]);
                        fileStream.filename = FileManager.AddFile(para[0]).File_Id;
                        fileStream.filelen = len;
                        fileStream.infoMode = false;
                        fileStream.FileStream = new System.IO.FileStream("RR" + para[0], System.IO.FileMode.OpenOrCreate);
                    }
                    else
                    {
                        fileStream.stringBuffer += str;
                    }
                }
                else
                {
                    fileStream.FileStream.Write(fileStream.buffer, 0, rSize);
                    fileStream.FileStream.Flush();
                }
                fileStream.rec += rSize;
                if (fileStream.rec == fileStream.filelen)
                {
                    fileStream.FileStream.Close();
                    fileStream.stream.Close();
                    fileStream.Socket.Close();
                    OnFileReceive?.Invoke(this,
                        new FileReceiveEventArgs() { File = new FileInf() { File_Id = fileStream.filename, File_Length = fileStream.filelen } });
                }
                else
                    fileStream.stream.BeginRead(fileStream.buffer, 0, fileStream.buffer.Length, Read_FileStreamX, fileStream);
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
    public class Server : Log, IDisposable
    {
        #region Vars
        private ServerMultiConnectManager ConnectManager;
        private UserManager UserManager;
        private bool Console;
        #endregion
        #region Threads
        private Thread MessagesHandler;
        #endregion
        public Server(bool Console = true) : base("Server")
        {
            UserManager = new UserManager();
            if (Console) OnLog += LogInConsole;
            this.Console = Console;
        }

        public void StartServer()
        {
            ConnectManager = new ServerMultiConnectManager(Console);
            ConnectManager.OnDataReceive += ConnectManager_OnDataReceive;
            ConnectManager.StartListening();
            MessagesHandler = new Thread(new ThreadStart(Messages_Handle));
            MessagesHandler.Start();
        }

        private void Messages_Handle()
        {
            for (int i = 0; i < ConnectManager.SessionCount; i++)
            {
                Session session = ConnectManager[i];
                if (session.Socket.Connected)
                {
                    List<Message> list = UserManager.GetUserMessages(session.UserIndex);
                    if (list != null && list.Count > 0)
                    {
                        for (int j = 0; j < list.Count; j++)
                        {
                            var item = list[j];
                            Command cmd = new Command();
                            cmd.CommandName = "NMSG";
                            cmd.CommandArgs = item.GetData();
                            ConnectManager.WriteToStream(session.IndexInList, cmd);
                            WriteInfo("Message From " + item.MessageFrom + " To " + item.MessageTo, ConsoleColor.Cyan);
                            if (item.MessageContent.IndexOf("SETRECECIVED") > -1) UserManager.DeleteMessage(session.UserIndex, j, false, true);
                            else UserManager.DeleteMessage(session.UserIndex, j, true, true);
                        }
                    }
                }
            }
            Thread.Sleep(1000);
            Messages_Handle();
        }

        public void Dispose()
        {
            ConnectManager.Dispose();
            UserManager.Dispose();
        }

        private void ConnectManager_OnDataReceive(object sender, DataReceiveEventArgs e)
        {
            string str = e.UTF8Data;
            Command cmd = Command.Parse(str);
            switch (cmd.CommandName)
            {
                case "PING":
                    {
                        ConnectManager.WriteToStream(e.StreamIndexInList, Command.PingCommand);
                        ConnectManager.ReTimeOut(e.StreamIndexInList);
                    }
                    break;
                case "CUSR":
                    {
                        User x = User.Parse(cmd.CommandArgs);
                        Command c = new Command() { CommandName = "CUSR" };
                        switch (UserManager.AddUser(x))
                        {
                            case 0: c.CommandArgs = "SUCCESS"; e.SetSessionUserIndex(x.ServerID); break;
                            case -1: c.CommandArgs = "PIDERROR"; break;
                            case -2: c.CommandArgs = "EMAILERROR"; break;
                        }
                        ConnectManager.WriteToStream(e.StreamIndexInList, c);
                        WriteInfo("User create : " + c.CommandArgs, ConsoleColor.Green);
                    }
                    break;
                case "LOGN":
                    {
                        User x = User.Parse(cmd.CommandArgs);
                        WriteInfo("LOGN[" + cmd.CommandArgs + "] ! processing..");
                        Command command = new Command();
                        command.CommandName = "LOGN";
                        User outU;
                        switch (UserManager.LoginUser(x, out outU))
                        {
                            case 0: command.CommandArgs = outU.GetData(); e.SetSessionUserIndex(outU.ServerID); break;
                            case -1: command.CommandArgs = "PASSERROR"; break;
                            case -2: command.CommandArgs = "USERNOTFOUND"; break;
                        }
                        ConnectManager.WriteToStream(e.StreamIndexInList, command);
                    }
                    break;
                case "SMSG":
                    {
                        WriteInfo("Messeage Handler Requested!", ConsoleColor.DarkYellow);
                        Message msg = Message.Parse(cmd.CommandArgs);
                        if (msg.ChatType == ChatType.Private)
                        {
                            int uInd = UserManager.GetUserIndex(msg.MessageTo);
                            if (uInd > -1)
                            {
                                int result = UserManager.AddMessage(uInd, msg);
                                if (result == -2)
                                {
                                    Command cmdd = new Command();
                                    cmdd.CommandName = "MSGE";
                                    cmdd.CommandArgs = "BLOCKEDUSER";
                                    ConnectManager.WriteToStream(e.StreamIndexInList, cmdd);
                                }
                            }
                        }
                        else if (msg.ChatType == ChatType.Group)
                        {
                            Group x = UserManager.GetGroup(int.Parse(msg.MessageTo));
                            WriteInfo("Message From " + msg.MessageFrom + " To " + msg.MessageTo, ConsoleColor.Cyan);
                            string userPID = UserManager[e.UserIndex].PID;
                            msg.MessageTo = x.ID.ToString();
                            if (x != null)
                            {
                                foreach (var item in x.MembersIDs)
                                {
                                    if (item == userPID) continue;
                                    int uInd = UserManager.GetUserIndex(item);
                                    if (uInd > -1)
                                    {
                                        int result = UserManager.AddMessage(uInd, msg);
                                        if (result == -2)
                                        {
                                            Command cmdd = new Command();
                                            cmdd.CommandName = "MSGE";
                                            cmdd.CommandArgs = "BLOCKEDUSER";
                                            ConnectManager.WriteToStream(e.StreamIndexInList, cmdd);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "SBLK":
                    {
                        string cmdArgs = cmd.CommandArgs.Substring(0, cmd.CommandArgs.Length - 1);
                        WriteInfo("Block Request for " + cmdArgs + " from " + UserManager[e.UserIndex].PID + " !", ConsoleColor.DarkGreen);
                        UserManager.BlockUser(e.UserIndex, cmdArgs);
                    }
                    break;
                case "UBLK":
                    {
                        string cmdArgs = cmd.CommandArgs.Substring(0, cmd.CommandArgs.Length - 1);
                        WriteInfo("Unblock Request for " + cmdArgs + " from " + UserManager[e.UserIndex].PID + " !", ConsoleColor.DarkGreen);
                        UserManager.UnblockUser(e.UserIndex, cmdArgs);
                    }
                    break;
                case "SRCH":
                    {
                        string keyword = cmd.CommandArgs.Remove(cmd.CommandArgs.Length - 1);
                        if (keyword != "" || keyword != null)
                        {
                            string[] results = UserManager.Search(keyword, e.UserIndex);
                            string result = string.Join(",", results);
                            Command cmdd = new Command();
                            cmdd.CommandName = "SRCH";
                            cmdd.CommandArgs = result;
                            ConnectManager.WriteToStream(e.StreamIndexInList, cmdd);
                        }
                    }
                    break;
                case "CRGR":
                    {
                        Group gr = Group.Parse(cmd.CommandArgs);
                        UserManager.AddGroup(gr);
                        string userPID = UserManager[e.UserIndex].PID;
                        foreach (var item in gr.MembersIDs)
                        {
                            Message x = new Message();
                            x.MessageContent = (item != userPID ? "You Have Been Added To This Group By " + UserManager[e.UserIndex].Name + "."
                                : userPID + " created this group.");
                            x.MessageDate = DateTime.Now;
                            x.MessageID = -1;
                            x.MessageTo = item;
                            x.MessageFrom = gr.ID.ToString();
                            x.ChatType = ChatType.Group;
                            int uInd = UserManager.GetUserIndex(item);
                            UserManager.AddMessage(uInd, x);
                        }
                    }
                    break;
                case "AMGR":
                    {
                        string[] para = cmd.CommandArgs.Split(new char[] { '\0' });
                        string userPID = UserManager[e.UserIndex].PID;
                        int gID = -1;
                        if (!int.TryParse(para[1], out gID)) break;
                        var gr = UserManager.GetGroup(gID);
                        if (UserManager.AddUserToGroup(para[0], gID) == 0)
                        {
                            Message x = new Message();
                            x.MessageContent = "You have been added to this group by " + userPID + ".";
                            x.MessageDate = DateTime.Now;
                            x.MessageID = -1;
                            x.MessageTo = para[0];
                            x.MessageFrom = para[1];
                            x.ChatType = ChatType.Group;
                            int uInd = UserManager.GetUserIndex(para[0]);
                            UserManager.AddMessage(uInd, x);
                            foreach (var mem in gr.MembersIDs)
                            {
                                if (mem == para[0]) continue;
                                x = new Message();
                                x.MessageContent = (mem != userPID ? para[0] + " has been added to this group by " + userPID + "."
                                    : "You added " + para[0] + " to this group.");
                                x.MessageDate = DateTime.Now;
                                x.MessageID = -1;
                                x.MessageFrom = gID.ToString();
                                x.MessageTo = mem;
                                x.ChatType = ChatType.Group;
                                uInd = UserManager.GetUserIndex(mem);
                                UserManager.AddMessage(uInd, x);
                                for (int i = 0; i < ConnectManager.SessionCount; i++)
                                {
                                    if (UserManager[ConnectManager[i].UserIndex].PID == mem)
                                    {
                                        ConnectManager.WriteToStream(ConnectManager[i].IndexInList, new Command() { CommandName = "GTGR", CommandArgs = gr.GetData() });
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "RMGR":
                    {
                        string[] para = cmd.CommandArgs.Split(new char[] { '\0' });
                        string userPID = UserManager[e.UserIndex].PID;
                        int gID = -1;
                        if (!int.TryParse(para[1], out gID)) break;
                        var gr = UserManager.GetGroup(gID);
                        if (UserManager.RemoveUserFromGroup(para[0], gID) == 0)
                        {
                            Message x = new Message();
                            x.MessageContent = "You have been removed from this group.";
                            x.MessageDate = DateTime.Now;
                            x.MessageID = -1;
                            x.MessageTo = para[0];
                            x.MessageFrom = para[1];
                            x.ChatType = ChatType.Group;
                            int uInd = UserManager.GetUserIndex(para[0]);
                            UserManager.AddMessage(uInd, x);
                            foreach (var mem in gr.MembersIDs)
                            {
                                if (mem == para[0]) continue;
                                x = new Message();
                                x.MessageContent = (mem != userPID ? para[0] + " has been removed from this group by " + userPID + "."
                                    : "You removed " + para[0] + " from this group.");
                                x.MessageDate = DateTime.Now;
                                x.MessageID = -1;
                                x.MessageFrom = gID.ToString();
                                x.MessageTo = mem;
                                x.ChatType = ChatType.Group;
                                uInd = UserManager.GetUserIndex(mem);
                                UserManager.AddMessage(uInd, x);
                                for (int i = 0; i < ConnectManager.SessionCount; i++)
                                {
                                    if (UserManager[ConnectManager[i].UserIndex].PID == mem)
                                    {
                                        ConnectManager.WriteToStream(ConnectManager[i].IndexInList, new Command() { CommandName = "GTGR", CommandArgs = gr.GetData() });
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "GTGR":
                    {
                        int id = int.Parse(cmd.CommandArgs);
                        Command x = new Command();
                        x.CommandName = "GTGR";
                        x.CommandArgs = UserManager.GetGroup(id).GetData();
                        ConnectManager.WriteToStream(e.StreamIndexInList, x);
                    }
                    break;
                case "GUSR":
                    {
                        int uInd = -1;
                        if (cmd.CommandArgs == null || cmd.CommandArgs == "") break;
                        var val = cmd.CommandArgs.Trim(new char[] { '\0' });
                        uInd = UserManager.GetUserIndex(val);
                        User x = UserManager[uInd];
                        x.Password = "";
                        x.BlockedUsers = new List<string>();
                        x.Email = "";
                        x.ServerID = -1;
                        Command gusr = new Command() { CommandName = "GUSR", CommandArgs = x.GetData() };
                        ConnectManager.WriteToStream(e.StreamIndexInList, gusr);
                    }
                    break;
            }
        }
    }
}