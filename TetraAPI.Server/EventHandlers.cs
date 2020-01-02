using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TetraAPI.Handlers;
namespace TetraAPI.Server
{
    //Class for EventHandler to return data info
    //Used in ServerMultiConnectManager
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
    //Class for EventHandler
    public class FileReceiveEventArgs : EventArgs
    {
        public FileInf File;
    }
}
