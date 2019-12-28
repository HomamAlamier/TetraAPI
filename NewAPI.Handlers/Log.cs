using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TetraAPI.Handlers
{
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
}
