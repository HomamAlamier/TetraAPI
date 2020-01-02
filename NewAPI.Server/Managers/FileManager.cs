using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TetraAPI.Handlers;
namespace TetraAPI.Server
{

    //Manage's file transport's
    public class FileManager : Log
    {
        List<string> FileID;
        List<long> FileLen;
        List<string> FilePath;
        Random Random;
        List<char> chars;
        public FileManager() : base("FileManager")
        {
            FileID = new List<string>();
            FileLen = new List<long>();
            FilePath = new List<string>();
            Random = new Random();
            chars = new List<char>();
            for (int i = 0; i < 255; i++)
            {
                if (char.IsLetter((char)i) || char.IsNumber((char)i))
                {
                    chars.Add((char)i);
                }
            }
        }
        public byte[] GetFile(FileInf fileInf)
        {
            for (int i = 0; i < FileID.Count; i++)
            {
                if (FileID[i] == fileInf.File_Id)
                {
                    return System.IO.File.ReadAllBytes(FilePath[i]);
                }
            }
            return null;
        }
        public FileInf AddFile(string filename)
        {
            long len = System.IO.File.Exists(filename) ? new System.IO.FileInfo(filename).Length : 0;
            string id = filename.Substring(0, filename.IndexOf("."));
            FileID.Add(id);
            FileLen.Add(len);
            FilePath.Add(filename);
            return new FileInf() { File_Id = id, File_Length = len };
        }
        public bool Contains_FileID(string fileid) => FileID.Contains(fileid);
        public string GenFileId()
        {
            const string chars =
                "1234567890" +
                "QWERTYUIOPASDFGHJKLZXCVBNM" +
                "qwertyuiopasdfghjklzxcvbnm";
            return new string(Enumerable.Repeat(chars, 8)
              .Select(s => s[Random.Next(s.Length)]).ToArray());
        }
    }
}
