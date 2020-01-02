using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TetraAPI.Handlers
{


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
}
