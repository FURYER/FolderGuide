using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data.OleDb;
using System.Data;
using System.Windows.Forms;

namespace FolderGuide
{
    public class DataBase
    {
        OleDbConnection connection = new OleDbConnection(ConfigurationManager.ConnectionStrings["FolderGuide.Properties.Settings.ConnectionString"].ConnectionString);

        public void OpenConnection()
        {
            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка:\n" + ex.Message);
            }
        }

        public virtual void CloseConnection()
        {
            connection.Close();
        }

        public OleDbConnection GetConnection()
        {
            return connection;
        }
    }
}
