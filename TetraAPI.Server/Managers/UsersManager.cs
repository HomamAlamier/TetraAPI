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
using TetraAPI.Handlers;
namespace TetraAPI.Server
{
    //Server User Manager
    //Add or remove user's
    //Also Manage's Group's
    public class UserManager : IDisposable
    {
        //User's List
        List<User> users;
        //Users Message's List
        List<List<Message>> usersMessages;
        //User's Message's ID's to get the state of the message like (sent,received,read)
        List<long> usersMessagesIDs;
        //This List indecates if the user is online or not (for fast search)
        List<bool> IsConnectedUser;
        //List Of Group's
        List<Group> groups;

        //Initialize The Manager
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

        //Login Request
        //If user found return 0 and output user info
        //If user found but the password was wrong return -1
        //If user not found at all return -2
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
}
