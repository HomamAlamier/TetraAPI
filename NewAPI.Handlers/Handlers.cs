using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
namespace TetraAPI.Handlers
{
    [ComVisible(true)]
    public enum ChatType { Private = 0, Group = 1 }
    public static class Consts
    {
        public const string ProgramVersion = "0.001";
    }
    public enum LogType { INFO = 0, ERROR = 1 }
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class OnLogEventArgs : EventArgs
    {
        public string Data { get; set; }
        public LogType LogType { get; set; }
        public ConsoleColor ConsoleColor { get; set; }
    }
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("ab99c597-0f50-4043-8e39-5ef89c8ed177")]
    public interface ILog
    {
        event EventHandler<OnLogEventArgs> OnLog;
        void WriteError(string data);
        void WriteError(Exception data);

        void WriteInfo(string data, ConsoleColor color = ConsoleColor.Blue);
        void LogInConsole(object sender, OnLogEventArgs e);

    }
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("38e23317-2058-4e4f-bbef-32136ef5ccb7")]
    public class Log : ILog
    {
        public event EventHandler<OnLogEventArgs> OnLog;
        FileStream logfs;
        public Log(string className, string storageLocation = "")
        {
            string directory = storageLocation + @"logs" + (storageLocation.IndexOf(@"\") > -1 ? "\\" : "/");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string logFN = className + "_" + DateTime.Now.ToShortDateString() + "@" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + ".log";
            logFN = logFN.Replace(@"/", "-");
            logFN = logFN.Replace(@":", "-");
            logFN = directory + logFN;
            logfs = new FileStream(logFN, FileMode.OpenOrCreate);
        }
        public void WriteError(string data)
        {
            OnLog?.Invoke(this, new OnLogEventArgs() { Data = "[ERROR] " + data, LogType = LogType.ERROR });
            var bytes = UTF8Encoding.UTF8.GetBytes("[ERROR] " + data + "\n");
            logfs.Write(bytes, 0, bytes.Length);
            logfs.Flush();
        }
        public void WriteError(Exception data)
        {
            OnLog?.Invoke(this, new OnLogEventArgs() { Data = "[ERROR] " + data.ToString(), LogType = LogType.ERROR });
            var bytes = UTF8Encoding.UTF8.GetBytes("[ERROR] " + data.ToString() + "\n");
            logfs.Write(bytes, 0, bytes.Length);
            logfs.Flush();
        }
        public void WriteInfo(string data, ConsoleColor color = ConsoleColor.Blue)
        {
            OnLog?.Invoke(this, new OnLogEventArgs() { Data = "[INFO] " + data, LogType = LogType.INFO, ConsoleColor = color });
            var bytes = UTF8Encoding.UTF8.GetBytes("[INFO] " + data + "\n");
            logfs.Write(bytes, 0, bytes.Length);
            logfs.Flush();
        }
        public void LogInConsole(object sender, OnLogEventArgs e)
        {
            switch (e.LogType)
            {
                case LogType.INFO:
                    Console.ForegroundColor = e.ConsoleColor;
                    break;
                case LogType.ERROR:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }
            Console.WriteLine(e.Data);
            Console.ResetColor();
        }
    }
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("32718193-68a3-4572-be92-0785019fb783")]
    public interface IMessage
    {
        ChatType ChatType { set; get; }
        string MessageContent { set; get; }
        string MessageFrom { set; get; }
        string MessageTo { set; get; }
        DateTime MessageDate { set; get; }
        bool MessageReceived { set; get; }
        long MessageID { set; get; }
        string GetData();
        string GetString();

    }
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("62b9c974-b047-40e3-bda3-1af8b2373e29")]
    public class Message : IMessage
    {
        public string MessageContent { set; get; }
        public string MessageFrom { set; get; }
        public string MessageTo { set; get; }
        public DateTime MessageDate { set; get; }
        public ChatType ChatType { set; get; }
        public bool MessageReceived { set; get; }
        public long MessageID { set; get; }
        public Message() { MessageID = -1; }
        public Message(long msgID) { MessageID = msgID; }
        public Message(string msg) { Parse(msg); }
        public string GetData()
        {
            return "DATA" + MessageContent + "\0DATE" + MessageDate.ToString()
                + "\0FROM" + MessageFrom + "\0TTOO" + MessageTo + "\0LONG" + MessageID + "\0CTYP" + ChatType + "\0";
        }
        public string GetString()
        {
            return ToString();
        }
        public override string ToString()
        {
            return "Message Info : " +
                "\nMessageContent = " + MessageContent +
                "\nMessageDate = " + MessageDate +
                "\nMessageFrom = " + MessageFrom +
                "\nMessageTo = " + MessageTo +
                "\nMessageID = " + MessageID +
                "\nChatType = " + ChatType;
        }
        public static Message Parse(string data)
        {
            Message tmp = new Message();
            string[] commands = data.Split(new char[] { '\0' });
            for (int i = 0; i < commands.Length; i++)
            {
                if (commands[i] == "") continue;
                string cur = commands[i];
                string cmd = cur.Substring(0, 4);
                switch (cmd)
                {
                    case "DATA": tmp.MessageContent = cur.Substring(4); break;
                    case "DATE": tmp.MessageDate = DateTime.Parse(cur.Substring(4)); break;
                    case "FROM": tmp.MessageFrom = cur.Substring(4); break;
                    case "TTOO": tmp.MessageTo = cur.Substring(4); break;
                    case "LONG": tmp.MessageID = long.Parse(cur.Substring(4)); break;
                    case "CTYP": tmp.ChatType = (ChatType)Enum.Parse(typeof(ChatType), cur.Substring(4)); break;
                }
            }
            return tmp;
        }
    }

    public class Command
    {
        public static Command PingCommand = new Command() { CommandName = "PING", CommandArgs = "" };
        public string CommandName { get; set; }
        public string CommandArgs { get; set; }
        public static bool operator ==(Command val1, Command val2)
        {
            if (isNull(val1) || isNull(val2)) return false;
            if (val1.CommandName == val2.CommandName) return true;
            else return false;
        }
        public static bool operator ==(Command val1, string val2)
        {
            if (isNull(val1) || isNull(val2)) return false;
            if (val1.CommandName == val2) return true;
            else return false;
        }
        static bool isNull(object obj0) => obj0 == null;
        public static bool operator !=(Command val1, Command val2)
        {
            if (isNull(val1) || isNull(val2)) return false;
            if (val1.CommandName == val2.CommandName) return false;
            else return true;
        }
        public static bool operator !=(Command val1, string val2)
        {
            if (isNull(val1) || isNull(val2)) return false;
            if (val1.CommandName == val2) return false;
            else return true;
        }
        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(Command))
                return this == (Command)obj;
            else
                return false;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public override string ToString()
        {
            return CommandName + "\uFFFF" + CommandArgs;
        }
        public static Command CreateCommand(string cmd, string args)
        {
            return new Command() { CommandName = cmd, CommandArgs = args };
        }
        public static Command Parse(string cmd)
        {
            string[] str = new Regex("\uFFFF").Split(cmd);
            if (str.Length < 2) return null;
            return new Command() { CommandName = str[0], CommandArgs = str[1] };
        }
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("504f1815-ecac-46f6-a69f-6862d2494a4d")]
    public interface IUser
    {
        string Name { set; get; }
        string PID { set; get; }
        DateTime LastSeen { set; get; }
        string Password { set; get; }
        string Email { set; get; }
        string Status { set; get; }
        string ProfilePicture { set; get; }
        DateTime ProfilePicture_Date { set; get; }
        List<string> BlockedUsers { set; get; }
        int ServerID { set; get; }
        string GetData();
    }
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("69459c4a-0261-43f5-a5e5-d9a003e3b434")]
    public class User : IUser
    {
        public string Name { set; get; }
        public string PID { set; get; } // Cannot Be Repeated
        public DateTime LastSeen { set; get; }
        public string Password { set; get; }
        public string ProfilePicture { set; get; }
        public DateTime ProfilePicture_Date { set; get; }
        public string Email { set; get; } // Cannot Be Repeated
        public string Status { set; get; }
        public List<string> BlockedUsers { set; get; }
        public int ServerID { set; get; }
        public User() => BlockedUsers = new List<string>();
        public override string ToString()
        {
            string blk = string.Join("-", BlockedUsers);
            return "User " + Name + " : PID = " + PID + ",Lastseen = " + LastSeen + ",Email = " + Email + ", Password = " + Password + ", Status = " + Status + ", BlockedUsers = " + blk;
        }
        public string GetData()
        {
            string blk = string.Join("\uAAAA", BlockedUsers);
            return "NAME" + Name + "\0PIDD" + PID + "\0LTSN" + LastSeen + "\0EMAL"
                + Email + "\0PASS" + Password + "\0STTS" + Status + "\0BLOK" + blk
                + "\0PICF" + ProfilePicture + "\0PICD" + ProfilePicture_Date + "\0";
        }
        public static User Parse(string data)
        {
            string[] vals = new Regex("\0").Split(data);
            User tmp = new User();
            for (int i = 0; i < vals.Length; i++)
            {
                try
                {
                    string key = vals[i].Substring(0, 4);
                    switch (key)
                    {
                        case "NAME": tmp.Name = vals[i].Substring(4); break;
                        case "PIDD": tmp.PID = vals[i].Substring(4); break;
                        case "LTSN": tmp.LastSeen = DateTime.Parse(vals[i].Substring(4)); break;
                        case "EMAL": tmp.Email = vals[i].Substring(4); break;
                        case "PASS": tmp.Password = vals[i].Substring(4); break;
                        case "STTS": tmp.Status = vals[i].Substring(4); break;
                        case "PICT": tmp.ProfilePicture = vals[i].Substring(4); break;
                        case "PICD": tmp.ProfilePicture_Date = DateTime.Parse(vals[i].Substring(4)); break;
                        case "BLOK":
                            {
                                string[] blks = vals[i].Substring(4).Split(new char[] { '\uAAAA' });
                                tmp.BlockedUsers = new List<string>();
                                tmp.BlockedUsers.AddRange(blks);
                            }
                            break;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return tmp;
        }
    }
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("1b725308-e7e4-47d2-9560-5667e57e6ae6")]
    public interface IGroup
    {
        string Title { get; set; }
        string Description { get; set; }
        string[] MembersIDs { get; set; }
        long ID { get; set; }
        DateTime CreateDate { get; set; }
        int AddMember(string pid);
        int RemoveMember(string pid);
        string GetData();
        string GetString();
    }
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("7c0857fe-0efb-479e-b27a-7713b06c6667")]
    public class Group : IGroup
    {
        private List<string> pids;
        public string Title { get; set; }
        public string Description { get; set; }
        public long ID { get; set; }
        public DateTime CreateDate { get; set; }
        public string[] MembersIDs { get => pids.ToArray(); set { pids.Clear(); pids.AddRange(value); } }
        public Group() => pids = new List<string>();
        public int AddMember(string pid)
        {
            if (pids.Contains(pid)) return -1;
            pids.Add(pid);
            return 0;
        }
        public string GetData()
        {
            string data = "TITL" + Title + "\0DESC" + Description + "\0DATE" + CreateDate + "\0GRID" + ID + "\0MEMS" + string.Join("\uF000", pids);
            return data;
        }

        public override string ToString()
        {
            return "Group " + ID + " : Title = " + Title + ", Description = " + Description + ", Members = " + string.Join(";", pids);
        }
        public string GetString() => ToString();
        public static Group Parse(string data)
        {
            try
            {
                string[] d = data.Split(new char[] { '\0' });
                Group gr = new Group();
                foreach (var item in d)
                {
                    if (item == "" || item == null) continue;
                    string type = item.Substring(0, 4);
                    string args = item.Substring(4);
                    switch (type)
                    {
                        case "TITL": gr.Title = args; break;
                        case "DESC": gr.Description = args; break;
                        case "DATE": gr.CreateDate = DateTime.Parse(args); break;
                        case "GRID": gr.ID = long.Parse(args); break;
                        case "MEMS":
                            {
                                string[] mems = args.Split(new char[] { '\uF000' });
                                gr.MembersIDs = mems;
                            }
                            break;
                    }
                }
                return gr;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public int RemoveMember(string pid)
        {
            if (!pids.Contains(pid)) return -1;
            pids.Remove(pid);
            return 0;
        }
    }

    public interface IFileInf
    {
        string File_Id { get; set; }
        long File_Length { get; set; }
        string GetDate();
        string GetString();
    }
    public class FileInf : IFileInf
    {
        public string File_Id { get; set; }
        public long File_Length { get; set; }
        public override string ToString() => "FileID = " + File_Id + "\nFileLength = " + File_Length;
        public string GetString() => ToString();
        public string GetDate() => "FLID" + File_Id + "\0FILN" + File_Length + "\0";

        public static FileInf Parse(string data)
        {
            try
            {
                string[] d = data.Split(new char[] { '\0' });
                FileInf tmp = new FileInf();
                foreach (var item in d)
                {
                    if (item == "" || item == null) continue;
                    string type = item.Substring(0, 4);
                    string args = item.Substring(4);
                    switch (type)
                    {
                        case "FLID": tmp.File_Id = args; break;
                        case "FILN": tmp.File_Length = long.Parse(args); break;
                    }
                }
                return tmp;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
