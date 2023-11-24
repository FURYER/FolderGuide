using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderGuide
{
    public class Folder : ICloneable
    {
        public int id;
        public string name;
        public int parentFolderId;
        public List<File> files;
        public List<Folder> folders;
        public Folder(int id, string name, int parentFolderId, List<File> files, List<Folder> folders)
        {
            this.id = id;
            this.name = name;
            this.parentFolderId = parentFolderId;
            this.files = files;
            this.folders = folders;
        }
        public object Clone() => new Folder(id, name, parentFolderId, new List<File>(files), new List<Folder>(folders));
        public class File : ICloneable
        {
            public int id;
            public string name;
            public DateTime createDate;
            public DateTime updateDate;
            public long size;
            public int folderId;
            public FileInfo fileInfo;
            public File(int id, string name, DateTime createDate, DateTime updateDate, long size, int folderId, FileInfo fileInfo)
            {
                this.id = id;
                this.name = name;
                this.createDate = createDate;
                this.updateDate = updateDate;
                this.size = size;
                this.folderId = folderId;
                this.fileInfo = fileInfo;
            }
            public object Clone() => new File(id, name, createDate, updateDate, size, folderId, fileInfo);
        }
    }
}
