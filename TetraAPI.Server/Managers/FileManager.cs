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

    //Manage's file transport's
    public class FileManager : Log
    {
        List<string> FileID;
        List<long> FileLen;
        List<string> FilePath;
        Random Random;
        public FileManager() : base("FileManager")
        {
            FileID = new List<string>();
            FileLen = new List<long>();
            FilePath = new List<string>();
            Random = new Random();
        }
        public string GetFile(string FID)
        {
            for (int i = 0; i < FileID.Count; i++)
            {
                if (FileID[i] == FID)
                {
                    return FilePath[i];
                }
            }
            return null;
        }
        public FileInf AddFile(string filename, string fid)
        {
            long len = System.IO.File.Exists(filename) ? new System.IO.FileInfo(filename).Length : 0;
            FileID.Add(fid);
            FileLen.Add(len);
            FilePath.Add(filename);
            return new FileInf() { File_Id = fid, File_Length = len };
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
