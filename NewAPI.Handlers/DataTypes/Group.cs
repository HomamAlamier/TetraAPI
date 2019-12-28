using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TetraAPI.Handlers
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("1b725308-e7e4-47d2-9560-5667e57e6ae6")]
    //Group Class Interface
    //Used for COM Interface
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
        private List<string> pids; //List of PID of member's
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
        /// <summary>
        /// Get user info in the shape for communcation
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// Convert String DataType To Group DataType
        /// </summary>
        /// <param name="data">Group String Data</param>
        /// <returns>Group datatype from the string data</returns>
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
}
