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
                if (char.IsLetterOrDigit((char)i))
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
            long len = new System.IO.FileInfo(filename).Length;
            string id;
            do
            {
                id = GenFileId();
            } while (FileID.Contains(id));
            FileID.Add(id);
            FileLen.Add(len);
            FilePath.Add(filename);
            return new FileInf() { File_Id = id, File_Length = len };
        }
        public bool Contains_FileID(string fileid) => FileID.Contains(fileid);
        public string GenFileId()
        {
            string id = "";
            for (int i = 0; i < 24; i++)
            {
                id += chars[Random.Next(0, chars.Count)];
            }
            return id;
        }
    }
}
