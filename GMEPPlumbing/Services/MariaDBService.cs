using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace GMEPPlumbing.Services
{
    public class MariaDBService
    {
        public string ConnectionString { get; set; }
        public MySqlConnection Connection { get; set; }

        public MariaDBService()
        {
            ConnectionString = connectionString;
            Connection = new MySqlConnection(ConnectionString);
        }
    }
}
