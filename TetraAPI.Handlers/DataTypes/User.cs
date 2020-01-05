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
using System.Text.RegularExpressions;

namespace TetraAPI.Handlers
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("504f1815-ecac-46f6-a69f-6862d2494a4d")]
    //User Class Interface
    //Used for COM Interface
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
        public string Name { set; get; } //Name Of the User
        public string PID { set; get; } //User Personal ID (Cannot Be Repeated)
        public DateTime LastSeen { set; get; } //User Last Seen Date
        public string Password { set; get; }
        public string ProfilePicture { set; get; } //User Profile Picture Path
        public DateTime ProfilePicture_Date { set; get; } //User Profile Picture Set Date
        public string Email { set; get; } // Cannot Be Repeated
        public string Status { set; get; } //User Status
        public List<string> BlockedUsers { set; get; } //Blocked user's list from this user 
        public int ServerID { set; get; } //User ID on the server (used in server only)
        public User() => BlockedUsers = new List<string>();
        public override string ToString()
        {
            string blk = string.Join("-", BlockedUsers);
            return "User " + Name + " : PID = " + PID + ",Lastseen = " + LastSeen + ",Email = " + Email + ", Password = " + Password + ", Status = " + Status + ", BlockedUsers = " + blk;
        }
        /// <summary>
        /// Get user info in the shape for communcation
        /// </summary>
        /// <returns></returns>
        public string GetData()
        {
            string blk = string.Join("\uAAAA", BlockedUsers);
            return "NAME" + Name + "\0PIDD" + PID + "\0LTSN" + LastSeen + "\0EMAL"
                + Email + "\0PASS" + Password + "\0STTS" + Status + "\0BLOK" + blk
                + "\0PICF" + ProfilePicture + "\0PICD" + ProfilePicture_Date + "\0";
        }

        /// <summary>
        /// Convert String DataType To User DataType
        /// </summary>
        /// <param name="data">User String Data</param>
        /// <returns>User datatype from the string data</returns>
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
}
