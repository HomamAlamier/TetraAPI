using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TetraAPI.Handlers
{
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
