using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Data.OleDb;
using System.Threading;

namespace FolderGuide
{
    public partial class Form1 : Form
    {
        DataBase dataBase = new DataBase();
        List<Folder.File> copyFiles = new List<Folder.File>();
        Folder copyFolder;
        List<Folder> mainFolder = new List<Folder>();
        int folderIndex = 0;
        int fileIndex = 0;
        TreeNode lastSelected;
        static CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        CancellationToken token = cancelTokenSource.Token;
        bool newFolder = true;
        List<Task> tasks = new List<Task>();

        public Form1()
        {
            InitializeComponent();
            treeViewFolders.ImageList = imageList1;
        }

        private void buttonOpenFolder_ClickAsync(object sender, EventArgs e)
        {
            if (newFolder)
                newFolder = false;
            else
            {
                cancelTokenSource.Cancel();
            }
            Task.WaitAll();
            cancelTokenSource = new CancellationTokenSource();
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                dataBase.OpenConnection();
                OleDbCommand cmd = new OleDbCommand($"DELETE FROM Files", dataBase.GetConnection());
                cmd.ExecuteNonQuery();
                cmd = new OleDbCommand($"DELETE FROM Folders", dataBase.GetConnection());
                cmd.ExecuteNonQuery();
                dataBase.CloseConnection();
                DirectoryInfo folderPath = new DirectoryInfo(folderBrowserDialog.SelectedPath);
                labelShowPath.Text = "Корневая директория: " + folderPath;
                dataGridView1.Rows.Clear();
                lastSelected = null;
                try
                {
                    new Task(async () => await loadFolders(folderPath)).Start();
                }
                catch { }
                textBoxFind.Text = "";
                label1.Text = "Текущая папка:";
            }
        }

        public async Task loadFolders(DirectoryInfo ParentFolder)
        {
            if (cancelTokenSource.Token.IsCancellationRequested)
                return;
            Invoke(new Action(delegate ()
            {
                treeViewFolders.Nodes.Clear();
            }));
            mainFolder.Clear();
            dataBase.OpenConnection();
            try
            {
                mainFolder.Add(await structurizeFolders(ParentFolder, cancelTokenSource.Token));
            }
            catch { }
            dataBase.CloseConnection();
            Invoke(new Action(delegate()
            {
                label2.Text = "Выполнено";
            }));
            TreeNode treeNode = new TreeNode();
            foreach (Folder folder in mainFolder)
            {
                try
                {
                    treeNode = await loadInUI(folder, treeNode);
                }
                catch { }
                Invoke(new Action(delegate ()
                {
                    treeViewFolders.Nodes.Add(treeNode);
                    treeNode.ExpandAll();
                }));
            }
        }

        public async Task<Folder> structurizeFolders(DirectoryInfo folder, CancellationToken token2, int parentId = -1)
        {
            if (token2.IsCancellationRequested)
                return null;
            Folder folder2 = new Folder(folderIndex++, folder.Name, -1, null, null);
            if (parentId == -1)
                folder2.parentFolderId = folder2.id;
            else
                folder2.parentFolderId = parentId;
            folder2.files = new List<Folder.File>();
            folder2.folders = new List<Folder>();
            OleDbCommand cmd = new OleDbCommand($"INSERT INTO Folders (folderId, folderName, parentFolderId) VALUES (?,?,?)", dataBase.GetConnection());
            cmd.Parameters.AddWithValue("@folderId", folder2.id);
            cmd.Parameters.AddWithValue("@folderName", folder2.name);
            cmd.Parameters.AddWithValue("@parentFolderId", folder2.parentFolderId);
            cmd.ExecuteNonQuery();
            FileInfo[] fileInfos = new FileInfo[] { };
            try
            {
                fileInfos = folder.GetFiles();
            }
            catch { }
            Invoke(new Action(delegate()
            {
                progressBar1.Value = 0;
                progressBar1.Maximum = fileInfos.Length;
            }));
            foreach (FileInfo file2 in fileInfos)
            {
                Folder.File file = new Folder.File(fileIndex++, file2.Name, file2.CreationTime, file2.LastWriteTime, file2.Length, folder2.id, file2);
                OleDbCommand cmd2 = new OleDbCommand($"INSERT INTO Files (fileId, fileName, createDate, lastUpdateDate, fileSize, folderId, fileData) VALUES (?,?,?,?,?,?,?)", dataBase.GetConnection());
                cmd2.Parameters.AddWithValue("@fileId", OleDbType.Variant).Value = file.id;
                cmd2.Parameters.AddWithValue("@fileName", OleDbType.Variant).Value = file.name;
                cmd2.Parameters.AddWithValue("@createDate", OleDbType.Variant).Value = file.createDate;
                cmd2.Parameters.AddWithValue("@lastUpdateDate", OleDbType.Variant).Value = file.createDate;
                cmd2.Parameters.AddWithValue("@fileSize", OleDbType.Variant).Value = (int)file.size;
                cmd2.Parameters.AddWithValue("@folderId", OleDbType.Variant).Value = file.folderId;
                cmd2.Parameters.AddWithValue("@fileData", OleDbType.Variant).Value = file.fileInfo;
                cmd2.ExecuteNonQuery();
                folder2.files.Add(file);
                Invoke(new Action(delegate ()
                {
                    progressBar1.Value++;
                    label2.Text = file2.Name;
                }));
            }
            DirectoryInfo[] directoryInfos = new DirectoryInfo[] { };
            try
            {
                directoryInfos = folder.GetDirectories();
            }
            catch { }
            foreach (var Folder in directoryInfos)
            {
                try
                {
                    Folder folder1 = await structurizeFolders(Folder, token2, folder2.id);
                    if (folder1 != null)
                        folder2.folders.Add(folder1);
                }
                catch { }
            }
            return folder2;
        }

        public async Task<TreeNode> loadInUI(Folder folder, TreeNode treeNode)
        {
            if (cancelTokenSource.Token.IsCancellationRequested)
                return null;
            if (folder == null)
            {
                treeNode = new TreeNode();
                return treeNode;
            }
            TreeNode treeNode1 = new TreeNode(folder.name);
            foreach (var folder2 in folder.folders)
            {
                try
                {
                    treeNode1.Nodes.Add(await loadInUI(folder2, treeNode1));
                }
                catch { }
            }
            treeNode1.Tag = folder;
            treeNode1.ImageIndex = 0;
            treeNode1.SelectedImageIndex = 0;
            return treeNode1;
        }

        private void treeViewFolders_AfterSelect(object sender, TreeViewEventArgs e)
        {
            Task task = new Task(() => AfterSelect(sender), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
        }

        public void AfterSelect(object sender)
        {
            if (sender != null)
            {
                Invoke(new Action(delegate ()
                {
                    lastSelected = ((TreeView)sender).SelectedNode;
                    label1.Text = "Текущая папка: " + lastSelected.FullPath;
                }));
            }
            Invoke(new Action(delegate ()
            {
                dataGridView1.Rows.Clear();
            }));
            TreeNode node = lastSelected;
            Folder folder = node.Tag as Folder;
            long filesSize = 0;
            foreach (var file in folder.files)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.CreateCells(dataGridView1);
                row.Cells[0].Value = file.name;
                row.Cells[1].Value = file.createDate;
                row.Cells[2].Value = file.updateDate;
                row.Cells[3].Value = file.size + " байт";
                if (textBoxFind.Text.Length > 0 && (file.name.Contains(textBoxFind.Text) || file.size.ToString().Contains(textBoxFind.Text) || file.createDate.ToString().Contains(textBoxFind.Text) || file.updateDate.ToString().Contains(textBoxFind.Text)))
                {
                    row.DefaultCellStyle.BackColor = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#fff58a");
                }
                else
                    row.DefaultCellStyle.BackColor = Color.White;
                row.Tag = file;
                Invoke(new Action(delegate ()
                {
                    dataGridView1.Rows.Add(row);
                }));
                filesSize += file.size;
            }
            Invoke(new Action(delegate ()
            {
                labelFolderSize.Text = "Размер файлов в папке: " + filesSize.ToString() + " байт";
                labelFolderSize.Tag = filesSize;
            }));
        }

        private void toolStripMenuItemCopy_Click(object sender, EventArgs e)
        {
            Task task = new Task(() => CopyClick(), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
        }

        public void CopyClick()
        {
            copyFiles.Clear();
            DataGridViewSelectedRowCollection selectedRows = dataGridView1.SelectedRows;
            foreach (DataGridViewRow row in selectedRows)
            {
                if (row.Index < 0)
                {
                    continue;
                }
                copyFiles.Add((Folder.File)((Folder.File)row.Tag).Clone());
            }
            dataGridView1.ClearSelection();
        }

        private void ToolStripMenuItemTreeCopy_Click(object sender, EventArgs e)
        {
            Task task = new Task(() => TreeCopyClick(), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
        }

        public void TreeCopyClick()
        {
            Invoke(new Action(delegate ()
            {
                dataGridView1.Rows.Clear();
                if (treeViewFolders.SelectedNode == null)
                {
                    return;
                }
                copyFolder = (Folder)((Folder)treeViewFolders.SelectedNode.Tag).Clone();
                treeViewFolders.SelectedNode = null;
            }));
        }

        private void ToolStripMenuItemCut_Click(object sender, EventArgs e)
        {
            Task task = new Task(() => CutClick(), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
        }

        public void CutClick()
        {
            Invoke(new Action(delegate ()
            {
                dataGridView1.Rows.Clear();
                if (treeViewFolders.SelectedNode == null)
                {
                    return;
                }
                Folder folder = (Folder)treeViewFolders.SelectedNode.Tag;
                copyFolder = (Folder)folder.Clone();
                List<Folder> newFolder = new List<Folder>(mainFolder);
                mainFolder.Clear();
                dataBase.OpenConnection();
                CutFolderDB(folder);
                dataBase.CloseConnection();
                foreach (Folder folder1 in newFolder)
                {
                    if (folder1 == folder)
                    {
                        continue;
                    }
                    mainFolder.Add(CutFolder(folder1, folder));
                }
                treeViewFolders.Nodes.Remove(treeViewFolders.SelectedNode);
            }));
        }

        public Folder CutFolder(Folder folder, Folder cutFolder)
        {
            for (int i = 0; i < folder.folders.Count; i++)
            {
                if (folder.folders[i].id == cutFolder.id)
                {
                    folder.folders.RemoveAt(i);
                    return folder;
                }
                folder.folders[i] = CutFolder(folder.folders[i], cutFolder);
            }
            return folder;
        }

        public Folder CutFolderDB(Folder folder)
        {
            OleDbCommand cmd = new OleDbCommand($"DELETE FROM Folders WHERE folderId =?", dataBase.GetConnection());
            cmd.Parameters.AddWithValue("@folderId", folder.id);
            cmd.ExecuteNonQuery();
            foreach (Folder item in folder.folders)
            {
                CutFolderDB(item);
            }
            return folder;
        }

        private void ToolStripMenuItem1Cut_Click(object sender, EventArgs e)
        {
            Task task = new Task(() => TreeCutClick(), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
        }

        public void TreeCutClick()
        {
            copyFiles.Clear();
            dataBase.OpenConnection();
            Invoke(new Action(delegate ()
            {
                Folder folder = (Folder)treeViewFolders.SelectedNode.Tag;
                for (int i = 0; i < dataGridView1.SelectedRows.Count; i++)
                {
                    if (dataGridView1.SelectedRows[i].Index < 0)
                    {
                        continue;
                    }
                    copyFiles.Add((Folder.File)((Folder.File)dataGridView1.SelectedRows[i].Tag).Clone());
                    folder = CutFiles(folder, (Folder.File)dataGridView1.SelectedRows[i].Tag);
                }
            }));
            dataBase.CloseConnection();
            foreach (DataGridViewRow row in dataGridView1.SelectedRows)
            {
                Invoke(new Action(delegate ()
                {
                    dataGridView1.Rows.Remove(row);
                }));
            }
        }

        public Folder CutFiles(Folder folder, Folder.File file)
        {
            for (int i = 0; i < folder.files.Count; i++)
            {
                if (folder.files[i].id == file.id)
                {
                    OleDbCommand cmd = new OleDbCommand($"DELETE FROM Files WHERE fileId =?", dataBase.GetConnection());
                    cmd.Parameters.AddWithValue("@fileId", file.id);
                    cmd.ExecuteNonQuery();
                    folder.files.RemoveAt(i);
                }
            }
            return folder;
        }

        private void ToolStripMenuItemPaste_Click(object sender, EventArgs e)
        {
            Task task = new Task(() => PasteFiles(), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
        }

        public void PasteFiles()
        {
            dataBase.OpenConnection();
            Invoke(new Action(delegate ()
            {
                if (treeViewFolders.SelectedNode == null)
                    return;
                Folder folder = (Folder)treeViewFolders.SelectedNode.Tag;
                foreach (Folder.File file in copyFiles)
                {
                    Folder.File file2 = (Folder.File)file.Clone();
                    file2.id = fileIndex++;
                    folder.files.Add(file2);
                    OleDbCommand cmd = new OleDbCommand($"INSERT INTO Files (fileId, fileName, createDate, lastUpdateDate, fileSize, folderId, fileData) VALUES (?,?,?,?,?,?,?)", dataBase.GetConnection());
                    cmd.Parameters.AddWithValue("@fileId", OleDbType.Variant).Value = file2.id;
                    cmd.Parameters.AddWithValue("@fileName", OleDbType.Variant).Value = file2.name;
                    cmd.Parameters.AddWithValue("@createDate", OleDbType.Variant).Value = file2.createDate;
                    cmd.Parameters.AddWithValue("@lastUpdateDate", OleDbType.Variant).Value = file2.createDate;
                    cmd.Parameters.AddWithValue("@fileSize", OleDbType.Variant).Value = (int)file2.size;
                    cmd.Parameters.AddWithValue("@folderId", OleDbType.Variant).Value = file2.folderId;
                    cmd.Parameters.AddWithValue("@fileData", OleDbType.Variant).Value = file2.fileInfo;
                    cmd.ExecuteNonQuery();
                    LoadGridNewFiles(file2);
                }
                labelFolderSize.Text = "Размер файлов в папке: " + ((long)labelFolderSize.Tag).ToString() + " байт";
            }));
            dataBase.CloseConnection();
        }

        public void LoadGridNewFiles(Folder.File file)
        {
            DataGridViewRow row = new DataGridViewRow();
            row.CreateCells(dataGridView1);
            row.Cells[0].Value = file.name;
            row.Cells[1].Value = file.createDate;
            row.Cells[2].Value = file.updateDate;
            row.Cells[3].Value = file.size + " байт";
            row.Tag = file;
            dataGridView1.Rows.Add(row);
            labelFolderSize.Tag = (long)labelFolderSize.Tag + file.size;
        }

        private void ToolStripMenuItem1Paste_Click(object sender, EventArgs e)
        {
            Task task = new Task(() => PasteFolder(), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
            dataGridView1.Rows.Clear();
        }

        public async void PasteFolder()
        {
            bool check = false;
            dataBase.OpenConnection();
            Invoke(new Action(delegate ()
            {
                if (treeViewFolders.SelectedNode == null || copyFolder == null)
                {
                    return;
                }
                Folder folder = (Folder)treeViewFolders.SelectedNode.Tag;
                foreach (Folder folder1 in mainFolder)
                {
                    if (folder == folder1)
                    {
                        for (int i = 0; i < mainFolder.Count; i++)
                        {
                            if (mainFolder[i] == folder)
                            {
                                Folder[] list = new Folder[] { };
                                try
                                {
                                    mainFolder.CopyTo(list, i + 1);
                                    mainFolder.RemoveRange(i + 1, mainFolder.Count - (i + 1));
                                }
                                catch { }
                                Folder folder2 = (Folder)copyFolder.Clone();
                                folder2.parentFolderId = folder2.id;
                                mainFolder.Add(folder2);
                                if (list.Length > 0)
                                    mainFolder.AddRange(list);
                                PasteFolderDB(folder2);
                                check = true;
                                break;
                            }
                        }
                        if (check)
                            break;
                    }
                    PasteFolder(folder, folder1);
                    PasteFolderDB(folder1, folder.id);
                }
                treeViewFolders.Nodes.Clear();
            }));
            dataBase.CloseConnection();
            TreeNode treeNode = new TreeNode();
            foreach (Folder folder1 in mainFolder)
            {
                treeNode = await loadInUI(folder1, treeNode);
                Invoke(new Action(delegate ()
                {
                    treeViewFolders.Nodes.Add(treeNode);
                    treeNode.ExpandAll();
                }));
            }
        }

        public void PasteFolder(Folder afterFolder, Folder folder)
        {
            if (folder == null)
                return;
            for (int i = 0; i < folder.folders.Count; i++)
            {
                if (folder.folders[i] == afterFolder)
                {
                    Folder[] list = new Folder[folder.folders.Count - (i + 1)];
                    try
                    {
                        Array.Copy(folder.folders.ToArray(), i + 1, list, 0, folder.folders.Count - (i + 1));
                        folder.folders.RemoveRange(i + 1, folder.folders.Count - (i + 1));
                    }
                    catch { }
                    Folder folder2 = (Folder)copyFolder.Clone();
                    folder.folders.Add(folder2);
                    if (list.Length > 0)
                        folder.folders.AddRange(list);
                    return;
                }
                PasteFolder(afterFolder, folder.folders[i]);
            }
        }

        public Folder PasteFolderDB(Folder folder, int parentId = -1)
        {
            folder.id = folderIndex++;
            if (parentId == -1)
                folder.parentFolderId = folder.id;
            else
                folder.parentFolderId = parentId;
            OleDbCommand cmd = new OleDbCommand($"INSERT INTO Folders (folderId, folderName, parentFolderId) VALUES (?,?,?)", dataBase.GetConnection());
            cmd.Parameters.AddWithValue("@folderId", folder.id);
            cmd.Parameters.AddWithValue("@folderName", folder.name);
            cmd.Parameters.AddWithValue("@parentFolderId", folder.parentFolderId);
            cmd.ExecuteNonQuery();
            foreach (Folder.File file in folder.files)
            {
                file.id = fileIndex++;
                file.folderId = folder.id;
                OleDbCommand cmd2 = new OleDbCommand($"INSERT INTO Files (fileId, fileName, createDate, lastUpdateDate, fileSize, folderId, fileData) VALUES (?,?,?,?,?,?,?)", dataBase.GetConnection());
                cmd2.Parameters.AddWithValue("@fileId", OleDbType.Variant).Value = file.id;
                cmd2.Parameters.AddWithValue("@fileName", OleDbType.Variant).Value = file.name;
                cmd2.Parameters.AddWithValue("@createDate", OleDbType.Variant).Value = file.createDate;
                cmd2.Parameters.AddWithValue("@lastUpdateDate", OleDbType.Variant).Value = file.createDate;
                cmd2.Parameters.AddWithValue("@fileSize", OleDbType.Variant).Value = (int)file.size;
                cmd2.Parameters.AddWithValue("@folderId", OleDbType.Variant).Value = file.folderId;
                cmd2.Parameters.AddWithValue("@fileData", OleDbType.Variant).Value = file.fileInfo;
                cmd2.ExecuteNonQuery();
            }
            foreach (Folder item in folder.folders)
            {
                PasteFolderDB(item, folder.id);
            }
            return folder;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Task task = new Task(() => FindFiles(), cancelTokenSource.Token);
            task.Start();
            tasks.Add(task);
        }

        public void FindFiles()
        {
            Invoke(new Action(delegate ()
            {
                treeViewFolders.SelectedNode = lastSelected;
            }));
            if (lastSelected != null)
                treeViewFolders_AfterSelect(null, null);
            foreach (TreeNode treeNode in treeViewFolders.Nodes)
            {
                foreach (Folder.File file in ((Folder)treeNode.Tag).files)
                {
                    if (textBoxFind.Text.Length > 0 && (file.name.Contains(textBoxFind.Text) || file.size.ToString().Contains(textBoxFind.Text) || file.createDate.ToString().Contains(textBoxFind.Text) || file.updateDate.ToString().Contains(textBoxFind.Text)))
                    {
                        treeNode.BackColor = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#fff58a");
                    }
                    else
                    {
                        treeNode.BackColor = Color.White;
                    }
                }
                FindFile(textBoxFind.Text, treeNode);
            }
        }

        public void FindFile(string mask, TreeNode treeNode)
        {
            foreach (TreeNode treeNode1 in treeNode.Nodes)
            {
                Folder folder = (Folder)treeNode1.Tag;
                bool check = false;
                foreach (Folder.File file in folder.files)
                {
                    if (mask.Length > 0 && (file.name.Contains(mask) || file.size.ToString().Contains(mask) || file.createDate.ToString().Contains(mask) || file.updateDate.ToString().Contains(mask)))
                    {
                        check = true;
                    }
                }
                if (check)
                {
                    treeNode1.BackColor = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#fff58a");
                }
                else
                {
                    treeNode1.BackColor = Color.White;
                }
                FindFile(mask, treeNode1);
            }
        }
    }
}
