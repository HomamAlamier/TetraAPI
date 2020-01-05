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
using System.Runtime.InteropServices;
using System.Text;

namespace TetraAPI.Handlers
{
    [ComVisible(true)]
    public enum ChatType { Private = 0, Group = 1 }
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("32718193-68a3-4572-be92-0785019fb783")]
    //Message Class Interface
    //Used for COM Interface
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
        /// <summary>
        /// Get user info in the shape for communcation
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// Convert String DataType To Message DataType
        /// </summary>
        /// <param name="data">Message String Data</param>
        /// <returns>Message datatype from the string data</returns>
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
}
