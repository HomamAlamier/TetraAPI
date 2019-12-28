using TetraAPI.Handlers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.IO;

namespace TetraAPI.Server
{

    //Sturct for session's info's
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
        //Initialize the server and used managers
        public void StartServer()
        {
            ConnectManager = new ServerMultiConnectManager(Console);
            ConnectManager.OnDataReceive += ConnectManager_OnDataReceive;
            ConnectManager.StartListening();
            MessagesHandler = new Thread(new ThreadStart(Messages_Handle));
            MessagesHandler.Start();
        }
        //Handle's Messages
        //Check the messages every 1 sec and try to send it if the user is online
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
                            // If this message has (SETRECECIVED)
                            // this message is used to alter message state
                            // like (sent,received,readed)
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
                // Ping Command
                // Used to determine if the client connection is up or not
                // if the server dont get a response after 5 sec it will close the connection
                case "PING":
                    {
                        ConnectManager.WriteToStream(e.StreamIndexInList, Command.PingCommand);
                        ConnectManager.ReTimeOut(e.StreamIndexInList);
                    }
                    break;
                // Create User Request Command
                case "CUSR":
                    {
                        //Parse The Incoming Data
                        User x = User.Parse(cmd.CommandArgs);
                        Command c = new Command() { CommandName = "CUSR" };
                        //Call AddUser From Usermanager
                        //If the email is already taken return's -2
                        //If the PID is already taken return's -1
                        //If all things correct return's 0
                        switch (UserManager.AddUser(x))
                        {
                            case 0: c.CommandArgs = "SUCCESS"; e.SetSessionUserIndex(x.ServerID); break;
                            case -1: c.CommandArgs = "PIDERROR"; break;
                            case -2: c.CommandArgs = "EMAILERROR"; break;
                        }
                        //Responed to the client what's the result
                        ConnectManager.WriteToStream(e.StreamIndexInList, c);
                        WriteInfo("User create : " + c.CommandArgs, ConsoleColor.Green);
                    }
                    break;
                //Login Request Commnad
                case "LOGN":
                    {
                        //Parse The Incoming Data
                        User x = User.Parse(cmd.CommandArgs);
                        WriteInfo("LOGN[" + cmd.CommandArgs + "] ! processing..");
                        Command command = new Command();
                        command.CommandName = "LOGN";
                        User outU;
                        //Call LoginUser from UserManager
                        //If email was not associated with an account is wrong return's -2
                        //If password is not correct return's -1
                        //If user email and password is correct return's 0 and output user info
                        switch (UserManager.LoginUser(x, out outU))
                        {
                            case 0: command.CommandArgs = outU.GetData(); e.SetSessionUserIndex(outU.ServerID); break;
                            case -1: command.CommandArgs = "PASSERROR"; break;
                            case -2: command.CommandArgs = "USERNOTFOUND"; break;
                        }
                        ConnectManager.WriteToStream(e.StreamIndexInList, command);
                    }
                    break;
                //Send Message Command
                case "SMSG":
                    {
                        WriteInfo("Messeage Handler Requested!", ConsoleColor.DarkYellow);
                        //Parse The Incoming Data
                        Message msg = Message.Parse(cmd.CommandArgs);

                        //Check if the chat type is private or group
                        //If it's private search for user with PID = Message.MessageTo
                        //If it's group search for the group and get group member's
                        //and send the message to every group member and not for the sender
                        if (msg.ChatType == ChatType.Private)
                        {
                            int uInd = UserManager.GetUserIndex(msg.MessageTo);
                            if (uInd > -1)
                            {

                                //Call AddMessage from UserManager
                                //If the sender is blocked by the receiver return's -2
                                //If the user is not found return's -1
                                //If all things right return's 0
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
                                        //Call AddMessage from UserManager
                                        //If the sender is blocked by the receiver return's -2
                                        //If the user is not found return's -1
                                        //If all things right return's 0
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
                //Block Request Command
                case "SBLK":
                    {
                        string cmdArgs = cmd.CommandArgs.Substring(0, cmd.CommandArgs.Length - 1);
                        WriteInfo("Block Request for " + cmdArgs + " from " + UserManager[e.UserIndex].PID + " !", ConsoleColor.DarkGreen);
                        UserManager.BlockUser(e.UserIndex, cmdArgs);
                    }
                    break;
                //Unblock Request Command
                case "UBLK":
                    {
                        string cmdArgs = cmd.CommandArgs.Substring(0, cmd.CommandArgs.Length - 1);
                        WriteInfo("Unblock Request for " + cmdArgs + " from " + UserManager[e.UserIndex].PID + " !", ConsoleColor.DarkGreen);
                        UserManager.UnblockUser(e.UserIndex, cmdArgs);
                    }
                    break;
                //Search Request Command
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
                //Create Group Request Command
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
                //Add Member To Group Command
                //Add's a user to a group
                //and send message to the this user from the group
                //and send message to the other group member that this user have been added
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
                //Remove Member From Group Command
                //Remove's a user from a group
                //and send message to the this user from the group the he have been removed
                //and send message to the other group member that this user have been removed
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
                //Get Group Info Command
                case "GTGR":
                    {
                        int id = int.Parse(cmd.CommandArgs);
                        Command x = new Command();
                        x.CommandName = "GTGR";
                        x.CommandArgs = UserManager.GetGroup(id).GetData();
                        ConnectManager.WriteToStream(e.StreamIndexInList, x);
                    }
                    break;
                //Get User Info Command
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