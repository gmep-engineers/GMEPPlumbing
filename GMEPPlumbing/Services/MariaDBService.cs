using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMEPPlumbing.ViewModels;
using MySql.Data.MySqlClient;
using System.Text.Json;
using Mysqlx.Crud;

namespace GMEPPlumbing.Services
{
    public class MariaDBService
    {
        public string ConnectionString { get; set; }
        public MySqlConnection Connection { get; set; }

        public MariaDBService()
        {
            ConnectionString = Properties.Settings.Default.ConnectionString;
            Connection = new MySqlConnection(ConnectionString);
        }
        public async Task OpenConnectionAsync()
        {
            if (Connection.State == System.Data.ConnectionState.Closed)
            {
                await Connection.OpenAsync();
            }
        }

        public async Task CloseConnectionAsync()
        {
            if (Connection.State == System.Data.ConnectionState.Open)
            {
                await Connection.CloseAsync();
            }
        }
        public async Task<WaterSystemData> GetWaterSystemData(
            string projectId
        )
        {
            // Get the lighting data from the database
            WaterSystemData waterSystemData = new WaterSystemData();
            string query = @"SELECT * WHERE project_id = @projectId";
            await OpenConnectionAsync();
            MySqlCommand command = new MySqlCommand(query, Connection);
            command.Parameters.AddWithValue("@projectId", projectId);
            MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();

            List<string> newLuminaireIds = new List<string>();

            while (await reader.ReadAsync())
            {
                waterSystemData.SectionHeader1 = reader.GetString("section_header_1");
                waterSystemData.SectionHeader2 = reader.GetString("section_header_1");
                waterSystemData.StreetHighPressure = reader.GetDouble("street_high_pressure");
                waterSystemData.StreetLowPressure = reader.GetDouble("street_low_pressure");


            }
            reader.Close();


            await CloseConnectionAsync();
            return lightings;
        }


    }
}
