using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using GMEPPlumbing.ViewModels;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using static Mysqlx.Notice.Frame.Types;

namespace GMEPPlumbing.Services
{
  public class MariaDBService {
    public string ConnectionString { get; set; }
    public MySqlConnection Connection { get; set; }
    public Document doc { get; private set; }
    public Database db { get; private set; }
    public Editor ed { get; private set; }
    public string ProjectId { get; private set; }

    public MariaDBService() {
      ConnectionString = Properties.Settings.Default.ConnectionString;
      Connection = new MySqlConnection(ConnectionString);

      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;
    }

    string GetSafeString(MySqlDataReader reader, string fieldName) {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index)) {
        return reader.GetString(index);
      }
      return string.Empty;
    }

    int GetSafeInt(MySqlDataReader reader, string fieldName) {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index)) {
        return reader.GetInt32(index);
      }
      return 0;
    }

    float GetSafeFloat(MySqlDataReader reader, string fieldName) {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index)) {
        return reader.GetFloat(index);
      }
      return 0;
    }

    bool GetSafeBoolean(MySqlDataReader reader, string fieldName) {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index)) {
        return reader.GetBoolean(index);
      }
      return false;
    }

    decimal GetSafeDecimal(MySqlDataReader reader, string fieldName) {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index)) {
        return reader.GetDecimal(index);
      }
      return 0;
    }

    public void OpenConnectionSync() {
      if (Connection.State == System.Data.ConnectionState.Closed) {
        Connection.Open();
      }
    }

    public void CloseConnectionSync() {
      if (Connection.State == System.Data.ConnectionState.Open) {
        Connection.Close();
      }
    }

    public async Task OpenConnectionAsync() {
      if (Connection.State == System.Data.ConnectionState.Closed) {
        await Connection.OpenAsync();
      }
    }

    public async Task CloseConnectionAsync() {
      if (Connection.State == System.Data.ConnectionState.Open) {
        await Connection.CloseAsync();
      }
    }

    public async Task<MySqlConnection> OpenNewConnectionAsync() {
      MySqlConnection conn = new MySqlConnection(ConnectionString);
      await conn.OpenAsync();
      return conn;
    }

    public async Task<string> GetProjectId(string projectNo) {
      if (!string.IsNullOrEmpty(ProjectId)) {
        return ProjectId;
      }
      if (projectNo == null || projectNo == string.Empty) {
        return string.Empty;
      }
      string query = "SELECT id FROM projects WHERE gmep_project_no = @projectNo";
      await OpenConnectionAsync();
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectNo", projectNo);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();

      Dictionary<int, string> projectIds = new Dictionary<int, string>();
      string id = "";
      if (reader.Read()) {
        id = reader.GetString("id");
      }
      reader.Close();

      await CloseConnectionAsync();
      ProjectId = id;
      return id;
    }

    public string GetProjectIdSync(string projectNo) {
      if (!string.IsNullOrEmpty(ProjectId)) {
        return ProjectId;
      }
      if (projectNo == null || projectNo == string.Empty) {
        return string.Empty;
      }
      string query = "SELECT id FROM projects WHERE gmep_project_no = @projectNo";
      OpenConnectionSync();
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectNo", projectNo);
      MySqlDataReader reader = command.ExecuteReader();

      Dictionary<int, string> projectIds = new Dictionary<int, string>();
      string id = "";
      if (reader.Read()) {
        id = reader.GetString("id");
      }
      reader.Close();

      CloseConnectionSync();
      ProjectId = id;
      return id;
    }

    public async Task<WaterSystemData> GetWaterSystemData(string projectId) {
      await OpenConnectionAsync();
      //get the additional losses

      WaterSystemData waterSystemData = new WaterSystemData();
      waterSystemData.AdditionalLosses = new ObservableCollection<AdditionalLoss>();
      waterSystemData.AdditionalLosses2 = new ObservableCollection<AdditionalLoss>();

      string query = @"SELECT * FROM plumbing_additional_losses WHERE project_id = @projectId";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", projectId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (await reader.ReadAsync()) {
        bool is2 = reader.GetBoolean("is_2");
        AdditionalLoss additionalLoss = new AdditionalLoss {
          Id = reader.GetString("id"),
          Title = reader.GetString("title"),
          Amount = reader.GetString("amount"),
        };
        if (is2) {
          waterSystemData.AdditionalLosses2.Add(additionalLoss);
        }
        else {
          waterSystemData.AdditionalLosses.Add(additionalLoss);
        }
      }
      reader.Close();

      // Get the lighting data from the database

      query = @"SELECT * FROM plumbing_water_systems WHERE project_id = @projectId";

      command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", projectId);
      reader = (MySqlDataReader)await command.ExecuteReaderAsync();

      List<string> newLuminaireIds = new List<string>();

      while (await reader.ReadAsync()) {
        waterSystemData.SectionHeader1 = reader.GetString("section_header_1");
        waterSystemData.StreetLowPressure = reader.GetDouble("street_low_pressure");
        waterSystemData.StreetHighPressure = reader.GetDouble("street_high_pressure");
        waterSystemData.MeterSize = reader.GetString("meter_size");
        waterSystemData.FixtureCalculation = reader.GetDouble("fixture_calculation");
        waterSystemData.Elevation = reader.GetDouble("elevation");
        waterSystemData.BackflowPressureLoss = reader.GetDouble("backflow_pressure_loss");
        waterSystemData.OldBackflowPressureLoss = reader.GetDouble("old_backflow_pressure_loss");
        waterSystemData.PrvPressureLoss = reader.GetDouble("prv_pressure_loss");
        waterSystemData.OldPrvPressureLoss = reader.GetDouble("old_prv_pressure_loss");
        waterSystemData.PressureRequiredOrAtUnit = reader.GetDouble("pressure_required_or_at_unit");
        waterSystemData.SystemLength = reader.GetDouble("system_length");
        waterSystemData.MeterLoss = reader.GetDouble("meter_loss");
        waterSystemData.StaticLoss = reader.GetDouble("static_loss");
        waterSystemData.TotalLoss = reader.GetDouble("total_loss");
        waterSystemData.PressureAvailable = reader.GetDouble("pressure_available");
        waterSystemData.DevelopedLength = reader.GetDouble("developed_length");
        waterSystemData.AveragePressureDrop = reader.GetDouble("average_pressure_drop");
        waterSystemData.AdditionalLossesTotal = reader.GetDouble("additional_losses_total");
        waterSystemData.ExistingMeter = reader.GetBoolean("existing_meter");
        waterSystemData.PipeMaterial = reader.GetString("pipe_material");
        waterSystemData.ColdWaterMaxVelocity = reader.GetInt32("cold_water_max_velocity");
        waterSystemData.HotWaterMaxVelocity = reader.GetInt32("hot_water_max_velocity");
        waterSystemData.DevelopedLengthPercentage = reader.GetInt32("developed_length_percentage");

        // Section 2
        waterSystemData.SectionHeader2 = reader.GetString("section_header_2");
        waterSystemData.PressureRequired2 = reader.GetDouble("pressure_required_2");
        waterSystemData.MeterSize2 = reader.GetString("meter_size_2");
        waterSystemData.FixtureCalculation2 = reader.GetDouble("fixture_calculation_2");
        waterSystemData.SystemLength2 = reader.GetDouble("system_length_2");
        waterSystemData.MeterLoss2 = reader.GetDouble("meter_loss_2");
        waterSystemData.TotalLoss2 = reader.GetDouble("total_loss_2");
        waterSystemData.PressureAvailable2 = reader.GetDouble("pressure_available_2");
        waterSystemData.DevelopedLength2 = reader.GetDouble("developed_length_2");
        waterSystemData.AveragePressureDrop2 = reader.GetDouble("average_pressure_drop_2");
        waterSystemData.AdditionalLossesTotal2 = reader.GetDouble("additional_losses_total_2");
      }
      reader.Close();

      await CloseConnectionAsync();
      return waterSystemData;
    }

    private object SanitizeDouble(double value) {
      if (double.IsNaN(value) || double.IsInfinity(value))
        return 0;
      return value;
    }

    public async Task<bool> UpdateWaterSystem(WaterSystemData waterSystemData, string projectId) {
      try {
        await OpenConnectionAsync();

        // First, delete existing additional losses that are not in additionalosses or additionallosses2 for the project

        string deleteQuery =
          @"
                DELETE FROM plumbing_additional_losses
                WHERE project_id = @projectId
                AND id NOT IN (@ids)";

        var ids = string.Join(
          ",",
          waterSystemData
            .AdditionalLosses.Select(a => $"'{a.Id}'")
            .Concat(waterSystemData.AdditionalLosses2.Select(a => $"'{a.Id}'"))
        );

        if (!string.IsNullOrEmpty(ids)) {
          MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, Connection);
          deleteCommand.Parameters.AddWithValue("@projectId", projectId);
          deleteCommand.Parameters.AddWithValue("@ids", ids);
          await deleteCommand.ExecuteNonQueryAsync();
        }
        else {
          deleteQuery =
            @"
                    DELETE FROM plumbing_additional_losses
                    WHERE project_id = @projectId";
          MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, Connection);
          deleteCommand.Parameters.AddWithValue("@projectId", projectId);
          await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var loss in waterSystemData.AdditionalLosses) {
          string insertQuery =
            @"
                    INSERT INTO plumbing_additional_losses (
                        id,
                        project_id,
                        title,
                        amount,
                        is_2
                    ) VALUES (
                        @id,
                        @projectId,
                        @title,
                        @amount,
                        0
                    )
                    ON DUPLICATE KEY UPDATE
                        title = @title,
                        amount = @amount";
          MySqlCommand insertCommand = new MySqlCommand(insertQuery, Connection);
          insertCommand.Parameters.AddWithValue("@id", loss.Id);
          insertCommand.Parameters.AddWithValue("@projectId", projectId);
          insertCommand.Parameters.AddWithValue("@title", loss.Title);
          insertCommand.Parameters.AddWithValue("@amount", loss.Amount);
          await insertCommand.ExecuteNonQueryAsync();
        }
        foreach (var loss in waterSystemData.AdditionalLosses2) {
          string insertQuery =
            @"
                    INSERT INTO plumbing_additional_losses (
                        id,
                        project_id,
                        title,
                        amount,
                        is_2
                    ) VALUES (
                        @id,
                        @projectId,
                        @title,
                        @amount,
                        1
                    )
                    ON DUPLICATE KEY UPDATE
                        title = @title,
                        amount = @amount";
          MySqlCommand insertCommand = new MySqlCommand(insertQuery, Connection);
          insertCommand.Parameters.AddWithValue("@id", loss.Id);
          insertCommand.Parameters.AddWithValue("@projectId", projectId);
          insertCommand.Parameters.AddWithValue("@title", loss.Title);
          insertCommand.Parameters.AddWithValue("@amount", loss.Amount);
          await insertCommand.ExecuteNonQueryAsync();
        }

        string query =
          @"
                INSERT INTO plumbing_water_systems (
                    project_id,
                    section_header_1,
                    street_low_pressure,
                    street_high_pressure,
                    meter_size,
                    fixture_calculation,
                    elevation,
                    backflow_pressure_loss,
                    old_backflow_pressure_loss,
                    prv_pressure_loss,
                    old_prv_pressure_loss,
                    pressure_required_or_at_unit,
                    system_length,
                    meter_loss,
                    static_loss,
                    total_loss,
                    pressure_available,
                    developed_length,
                    average_pressure_drop,
                    additional_losses_total,
                    existing_meter,
                    pipe_material,
                    cold_water_max_velocity,
                    hot_water_max_velocity,
                    developed_length_percentage,
                    section_header_2,
                    pressure_required_2,
                    meter_size_2,
                    fixture_calculation_2,
                    system_length_2,
                    meter_loss_2,
                    total_loss_2,
                    pressure_available_2,
                    developed_length_2,
                    average_pressure_drop_2,
                    additional_losses_total_2
                ) VALUES (
                    @projectId,
                    @sectionHeader1,
                    @streetLowPressure,
                    @streetHighPressure,
                    @meterSize,
                    @fixtureCalculation,
                    @elevation,
                    @backflowPressureLoss,
                    @oldBackflowPressureLoss,
                    @prvPressureLoss,
                    @oldPrvPressureLoss,
                    @pressureRequiredOrAtUnit,
                    @systemLength,
                    @meterLoss,
                    @staticLoss,
                    @totalLoss,
                    @pressureAvailable,
                    @developedLength,
                    @averagePressureDrop,
                    @additionalLossesTotal,
                    @existingMeter,
                    @pipeMaterial,
                    @coldWaterMaxVelocity,
                    @hotWaterMaxVelocity,
                    @developedLengthPercentage,
                    @sectionHeader2,
                    @pressureRequired2,
                    @meterSize2,
                    @fixtureCalculation2,
                    @systemLength2,
                    @meterLoss2,
                    @totalLoss2,
                    @pressureAvailable2,
                    @developedLength2,
                    @averagePressureDrop2,
                    @additionalLossesTotal2
                )
                ON DUPLICATE KEY UPDATE
                    section_header_1 = @sectionHeader1,
                    street_low_pressure = @streetLowPressure,
                    street_high_pressure = @streetHighPressure,
                    meter_size = @meterSize,
                    fixture_calculation = @fixtureCalculation,
                    elevation = @elevation,
                    backflow_pressure_loss = @backflowPressureLoss,
                    old_backflow_pressure_loss = @oldBackflowPressureLoss,
                    prv_pressure_loss = @prvPressureLoss,
                    old_prv_pressure_loss = @oldPrvPressureLoss,
                    pressure_required_or_at_unit = @pressureRequiredOrAtUnit,
                    system_length = @systemLength,
                    meter_loss = @meterLoss,
                    static_loss = @staticLoss,
                    total_loss = @totalLoss,
                    pressure_available = @pressureAvailable,
                    developed_length = @developedLength,
                    average_pressure_drop = @averagePressureDrop,
                    additional_losses_total = @additionalLossesTotal,
                    existing_meter = @existingMeter,
                    pipe_material = @pipeMaterial,
                    cold_water_max_velocity = @coldWaterMaxVelocity,
                    hot_water_max_velocity = @hotWaterMaxVelocity,
                    developed_length_percentage = @developedLengthPercentage,
                    section_header_2 = @sectionHeader2,
                    pressure_required_2 = @pressureRequired2,
                    meter_size_2 = @meterSize2,
                    fixture_calculation_2 = @fixtureCalculation2,
                    system_length_2 = @systemLength2,
                    meter_loss_2 = @meterLoss2,
                    total_loss_2 = @totalLoss2,
                    pressure_available_2 = @pressureAvailable2,
                    developed_length_2 = @developedLength2,
                    average_pressure_drop_2 = @averagePressureDrop2,
                    additional_losses_total_2 = @additionalLossesTotal2
                    ";

        MySqlCommand command = new MySqlCommand(query, Connection);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue(
          "@sectionHeader1",
          waterSystemData.SectionHeader1 ?? string.Empty
        );
        command.Parameters.AddWithValue(
          "@streetLowPressure",
          SanitizeDouble(waterSystemData.StreetLowPressure)
        );
        command.Parameters.AddWithValue(
          "@streetHighPressure",
          SanitizeDouble(waterSystemData.StreetHighPressure)
        );
        command.Parameters.AddWithValue("@meterSize", waterSystemData.MeterSize ?? string.Empty);
        command.Parameters.AddWithValue(
          "@fixtureCalculation",
          SanitizeDouble(waterSystemData.FixtureCalculation)
        );
        command.Parameters.AddWithValue("@elevation", SanitizeDouble(waterSystemData.Elevation));
        command.Parameters.AddWithValue(
          "@backflowPressureLoss",
          SanitizeDouble(waterSystemData.BackflowPressureLoss)
        );
        command.Parameters.AddWithValue(
          "@oldBackflowPressureLoss",
          SanitizeDouble(waterSystemData.OldBackflowPressureLoss)
        );
        command.Parameters.AddWithValue(
          "@prvPressureLoss",
          SanitizeDouble(waterSystemData.PrvPressureLoss)
        );
        command.Parameters.AddWithValue(
          "@oldPrvPressureLoss",
          SanitizeDouble(waterSystemData.OldPrvPressureLoss)
        );
        command.Parameters.AddWithValue(
          "@pressureRequiredOrAtUnit",
          SanitizeDouble(waterSystemData.PressureRequiredOrAtUnit)
        );
        command.Parameters.AddWithValue(
          "@systemLength",
          SanitizeDouble(waterSystemData.SystemLength)
        );
        command.Parameters.AddWithValue("@meterLoss", SanitizeDouble(waterSystemData.MeterLoss));
        command.Parameters.AddWithValue("@staticLoss", SanitizeDouble(waterSystemData.StaticLoss));
        command.Parameters.AddWithValue("@totalLoss", SanitizeDouble(waterSystemData.TotalLoss));
        command.Parameters.AddWithValue(
          "@pressureAvailable",
          SanitizeDouble(waterSystemData.PressureAvailable)
        );
        command.Parameters.AddWithValue(
          "@developedLength",
          SanitizeDouble(waterSystemData.DevelopedLength)
        );
        command.Parameters.AddWithValue(
          "@averagePressureDrop",
          SanitizeDouble(waterSystemData.AveragePressureDrop)
        );
        command.Parameters.AddWithValue(
          "@additionalLossesTotal",
          SanitizeDouble(waterSystemData.AdditionalLossesTotal)
        );
        command.Parameters.AddWithValue("@existingMeter", waterSystemData.ExistingMeter);
        command.Parameters.AddWithValue(
          "@pipeMaterial",
          waterSystemData.PipeMaterial ?? string.Empty
        );
        command.Parameters.AddWithValue(
          "@coldWaterMaxVelocity",
          waterSystemData.ColdWaterMaxVelocity
        );
        command.Parameters.AddWithValue(
          "@hotWaterMaxVelocity",
          waterSystemData.HotWaterMaxVelocity
        );
        command.Parameters.AddWithValue(
          "@developedLengthPercentage",
          waterSystemData.DevelopedLengthPercentage
        );
        command.Parameters.AddWithValue(
          "@sectionHeader2",
          waterSystemData.SectionHeader2 ?? string.Empty
        );
        command.Parameters.AddWithValue(
          "@pressureRequired2",
          SanitizeDouble(waterSystemData.PressureRequired2)
        );
        command.Parameters.AddWithValue("@meterSize2", waterSystemData.MeterSize2 ?? string.Empty);
        command.Parameters.AddWithValue(
          "@fixtureCalculation2",
          SanitizeDouble(waterSystemData.FixtureCalculation2)
        );
        command.Parameters.AddWithValue(
          "@systemLength2",
          SanitizeDouble(waterSystemData.SystemLength2)
        );
        command.Parameters.AddWithValue("@meterLoss2", SanitizeDouble(waterSystemData.MeterLoss2));
        command.Parameters.AddWithValue("@totalLoss2", SanitizeDouble(waterSystemData.TotalLoss2));
        command.Parameters.AddWithValue(
          "@pressureAvailable2",
          SanitizeDouble(waterSystemData.PressureAvailable2)
        );
        command.Parameters.AddWithValue(
          "@developedLength2",
          SanitizeDouble(waterSystemData.DevelopedLength2)
        );
        command.Parameters.AddWithValue(
          "@averagePressureDrop2",
          SanitizeDouble(waterSystemData.AveragePressureDrop2)
        );
        command.Parameters.AddWithValue(
          "@additionalLossesTotal2",
          SanitizeDouble(waterSystemData.AdditionalLossesTotal2)
        );

        await command.ExecuteNonQueryAsync();
        await CloseConnectionAsync();
      }
      catch (Exception ex) {
        ed.WriteMessage("database error: " + ex.Message);
        await CloseConnectionAsync();
        return false;
      }
      return true;
    }

    public List<PlumbingFixtureType> GetPlumbingFixtureTypes() {
      List<PlumbingFixtureType> fixtureTypes = new List<PlumbingFixtureType>();
      OpenConnectionSync();
      string query = "SELECT * FROM plumbing_fixture_types ORDER BY abbreviation";
      MySqlCommand command = new MySqlCommand(query, Connection);
      MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read()) {
        fixtureTypes.Add(
          new PlumbingFixtureType(
            GetSafeInt(reader, "id"),
            GetSafeString(reader, "name"),
            GetSafeString(reader, "abbreviation")
          )
        );
      }
      reader.Close();
      CloseConnectionSync();
      return fixtureTypes;
    }

    public Dictionary<int,  List<PlumbingFixtureCatalogItem>> GetAllPlumbingFixtureCatalogItems() {
      Dictionary<int, List<PlumbingFixtureCatalogItem>> items = new Dictionary<int, List<PlumbingFixtureCatalogItem>>();
      OpenConnectionSync();
      string query =
        "SELECT * FROM plumbing_fixture_catalog ORDER BY description";
      MySqlCommand command = new MySqlCommand(query, Connection);
      MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read()) {
        int typeId = GetSafeInt(reader, "type_id");
        if (!items.ContainsKey(typeId)) {
          items[typeId] = new List<PlumbingFixtureCatalogItem>();
        }
        items[typeId].Add(
          new PlumbingFixtureCatalogItem(
            GetSafeInt(reader, "id"),
            GetSafeInt(reader, "type_id"),
            GetSafeString(reader, "description"),
            GetSafeString(reader, "make"),
            GetSafeString(reader, "model"),
            GetSafeDecimal(reader, "trap"),
            GetSafeDecimal(reader, "waste"),
            GetSafeDecimal(reader, "vent"),
            GetSafeDecimal(reader, "cold_water"),
            GetSafeDecimal(reader, "hot_water"),
            GetSafeString(reader, "remarks"),
            GetSafeDecimal(reader, "fixture_demand"),
            GetSafeDecimal(reader, "hot_demand"),
            GetSafeInt(reader, "dfu"),
            GetSafeString(reader, "water_block_names"),
            GetSafeString(reader, "waste_block_names"),
            GetSafeString(reader, "gas_block_names"),
            GetSafeInt(reader, "cfh"),
            GetSafeBoolean(reader, "residential"),
            GetSafeBoolean(reader, "commercial"),
            GetSafeBoolean(reader, "island")
          )
        );
      }
      reader.Close();
      CloseConnectionSync();
      return items;
    }

    public List<PlumbingFixtureCatalogItem> GetPlumbingFixtureCatalogItemsByType(int typeId) {
      List<PlumbingFixtureCatalogItem> items = new List<PlumbingFixtureCatalogItem>();
      OpenConnectionSync();
      string query =
        "SELECT * FROM plumbing_fixture_catalog WHERE type_id = @typeId ORDER BY description";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@typeId", typeId);
      MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read()) {
        items.Add(
          new PlumbingFixtureCatalogItem(
            GetSafeInt(reader, "id"),
            GetSafeInt(reader, "type_id"),
            GetSafeString(reader, "description"),
            GetSafeString(reader, "make"),
            GetSafeString(reader, "model"),
            GetSafeDecimal(reader, "trap"),
            GetSafeDecimal(reader, "waste"),
            GetSafeDecimal(reader, "vent"),
            GetSafeDecimal(reader, "cold_water"),
            GetSafeDecimal(reader, "hot_water"),
            GetSafeString(reader, "remarks"),
            GetSafeDecimal(reader, "fixture_demand"),
            GetSafeDecimal(reader, "hot_demand"),
            GetSafeInt(reader, "dfu"),
            GetSafeString(reader, "water_block_names"),
            GetSafeString(reader, "waste_block_names"),
            GetSafeString(reader, "gas_block_names"),
            GetSafeInt(reader, "cfh"),
            GetSafeBoolean(reader, "residential"),
            GetSafeBoolean(reader, "commercial"),
            GetSafeBoolean(reader, "island")
          )
        );
      }
      reader.Close();
      CloseConnectionSync();
      return items;
    }
    public PlumbingFixtureCatalogItem GetPlumbingFixtureCatalogItemById(int id) {
      PlumbingFixtureCatalogItem item = null;
      using (var conn = new MySqlConnection(ConnectionString)) {
        conn.Open();
        string query = "SELECT * FROM plumbing_fixture_catalog WHERE id = @id ORDER BY description";
        using (var command = new MySqlCommand(query, conn)) {
          command.Parameters.AddWithValue("@id", id);
          using (var reader = command.ExecuteReader()) {
            if (reader.Read()) {
              item = new PlumbingFixtureCatalogItem(
                  GetSafeInt(reader, "id"),
                  GetSafeInt(reader, "type_id"),
                  GetSafeString(reader, "description"),
                  GetSafeString(reader, "make"),
                  GetSafeString(reader, "model"),
                  GetSafeDecimal(reader, "trap"),
                  GetSafeDecimal(reader, "waste"),
                  GetSafeDecimal(reader, "vent"),
                  GetSafeDecimal(reader, "cold_water"),
                  GetSafeDecimal(reader, "hot_water"),
                  GetSafeString(reader, "remarks"),
                  GetSafeDecimal(reader, "fixture_demand"),
                  GetSafeDecimal(reader, "hot_demand"),
                  GetSafeInt(reader, "dfu"),
                  GetSafeString(reader, "water_block_names"),
                  GetSafeString(reader, "waste_block_names"),
                  GetSafeString(reader, "gas_block_names"),
                  GetSafeInt(reader, "cfh"),
                  GetSafeBoolean(reader, "residential"),
                  GetSafeBoolean(reader, "commercial"),
                  GetSafeBoolean(reader, "island")
              );
            }
          }
        }
      }
      return item;
    }

    /* public void CreatePlumbingFixture(PlumbingFixture fixture) {
       // 1 get the count of different types of the same fixture
       string query =
         @"
         SELECT DISTINCT plumbing_fixture_catalog.id, plumbing_fixtures.number FROM plumbing_fixtures
         LEFT JOIN plumbing_fixture_catalog ON plumbing_fixture_catalog.id = plumbing_fixtures.catalog_id
         LEFT JOIN plumbing_fixture_types ON plumbing_fixture_types.id = plumbing_fixture_catalog.type_id
         WHERE plumbing_fixtures.project_id = @projectId
         AND plumbing_fixture_types.abbreviation = @abbreviation
         ";
       int count = 0;
       OpenConnectionSync();
       MySqlCommand command = new MySqlCommand(query, Connection);
       command.Parameters.AddWithValue("@projectId", fixture.ProjectId);
       command.Parameters.AddWithValue("@abbreviation", fixture.TypeAbbreviation);
       MySqlDataReader reader = command.ExecuteReader();
       List<int> addedCatalogIds = new List<int>();
       List<int> addedFixtureNumbers = new List<int>();
       while (reader.Read()) {
         addedCatalogIds.Add(GetSafeInt(reader, "id"));
         addedFixtureNumbers.Add(GetSafeInt(reader, "number"));
       }
       if (addedCatalogIds.Count == 0) {
         count = 1;
       }
       for (int i = 0; i < addedCatalogIds.Count; i++) {
         if (addedCatalogIds[i] == fixture.CatalogId) {
           count = addedFixtureNumbers[i];
         }
       }
       if (count == 0) {
         count = addedCatalogIds.Count + 1;
       }
       reader.Close();
       fixture.Number = count;
       query =
         @"
         INSERT INTO plumbing_fixtures
         (id, project_id, pos_x, pos_y, catalog_id, number)
         VALUES
         (@id, @projectId, @posX, @posY, @catalogId, @number)
         ";
       command = new MySqlCommand(query, Connection);
       command.Parameters.AddWithValue("@id", fixture.Id);
       command.Parameters.AddWithValue("@projectId", fixture.ProjectId);
       command.Parameters.AddWithValue("@posX", fixture.Position.X);
       command.Parameters.AddWithValue("@posY", fixture.Position.Y);
       command.Parameters.AddWithValue("@catalogId", fixture.CatalogId);
       command.Parameters.AddWithValue("@number", fixture.Number);
       command.ExecuteNonQuery();
       CloseConnectionSync();
     }*/

    public List<PlumbingSourceType> GetPlumbingSourceTypes() {
      List<PlumbingSourceType> types = new List<PlumbingSourceType>();
      string query = "SELECT * FROM plumbing_source_types";

      OpenConnectionSync();
      MySqlCommand command = new MySqlCommand(query, Connection);
      MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read()) {
        types.Add(new PlumbingSourceType(GetSafeInt(reader, "id"), GetSafeString(reader, "type")));
      }
      reader.Close();
      CloseConnectionSync();
      return types;
    }

    /*public async Task CreatePlumbingSource(PlumbingSource source) {
      string query =
        @"
        INSERT INTO plumbing_sources
        (id, project_id, pos_x, pos_y, type_id, fixture_id)
        VALUES (@id, @projectId, @posX, @posY, @typeId, @fixtureId)
        ";
      await OpenConnectionAsync();
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@id", source.Id);
      command.Parameters.AddWithValue("@projectId", source.ProjectId);
      command.Parameters.AddWithValue("@posX", source.Position.X);
      command.Parameters.AddWithValue("@posY", source.Position.Y);
      command.Parameters.AddWithValue("@typeId", source.TypeId);
      command.Parameters.AddWithValue("@fixtureId", source.FixtureId);
      await command.ExecuteNonQueryAsync();
      await CloseConnectionAsync();
    }*/

    /*public async Task CreatePlumbingPlanBasePoint(PlumbingPlanBasePoint point) {
      string query =
        @"
        INSERT INTO plumbing_plan_base_points
        (id, project_id, viewportName, floor)
        VALUES
        (@id, @projectId, @viewportName, @floor
        ";
      // opening a new connection since we don't know how fast the user is going to click
      // in comparison to how fast the database will responsd
      MySqlConnection conn = await OpenNewConnectionAsync();
      MySqlCommand command = new MySqlCommand(query, conn);
      command.Parameters.AddWithValue("@id", point.Id);
      command.Parameters.AddWithValue("@projectId", point.ProjectId);
      command.Parameters.AddWithValue("@planTypeId", point.ViewportName);
      command.Parameters.AddWithValue("@floor", point.Floor);
      await command.ExecuteNonQueryAsync();
      await conn.CloseAsync();
    }*/



    
    /*public async Task CreatePlumbingHorizontalRoute(PlumbingHorizontalRoute route) {
      string query =
        @"
        INSERT INTO plumbing_horizontal_routes
        (id, project_id, start_pos_x, end_pos_x, start_pos_y, end_pos_y, source_id)
        VALUES (@id, @projectId, @startPosX, @endPosX, @startPosY, @endPosY, @sourceId)
        ";

      MySqlConnection conn = await OpenNewConnectionAsync();
      MySqlCommand command = new MySqlCommand(query, conn);
      command.Parameters.AddWithValue("@id", route.Id);
      command.Parameters.AddWithValue("@projectId", route.ProjectId);
      command.Parameters.AddWithValue("@startPosX", route.StartPoint.X);
      command.Parameters.AddWithValue("@startPosY", route.StartPoint.Y);
      command.Parameters.AddWithValue("@endPosX", route.EndPoint.X);
      command.Parameters.AddWithValue("@endPosY", route.EndPoint.Y);
      command.Parameters.AddWithValue("@sourceId", route.SourceId);
      await command.ExecuteNonQueryAsync();
      await conn.CloseAsync();
    }*/

    public async Task UpdatePlumbingHorizontalRoutes(
      List<PlumbingHorizontalRoute> routes,
      string projectId
    ) {
        if (routes == null)
          return;

        var idsToKeep = routes.Select(r => r.Id).ToList();

        MySqlConnection conn = await OpenNewConnectionAsync();

        if (idsToKeep.Count > 0) {

          var paramNames = idsToKeep.Select((id, i) => $"@id{i}").ToList();
          string deleteQuery = $@"
              DELETE FROM plumbing_horizontal_routes
              WHERE project_id = @projectId
              AND id NOT IN ({string.Join(",", paramNames)})
          ";
          MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
          deleteCommand.Parameters.AddWithValue("@projectId", projectId);
          for (int i = 0; i < idsToKeep.Count; i++) {
            deleteCommand.Parameters.AddWithValue(paramNames[i], idsToKeep[i]);
          }
          await deleteCommand.ExecuteNonQueryAsync();
        }
        else {

          string deleteQuery = @"
              DELETE FROM plumbing_horizontal_routes
              WHERE project_id = @projectId
          ";
          MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
          deleteCommand.Parameters.AddWithValue("@projectId", projectId);
          await deleteCommand.ExecuteNonQueryAsync();
        }


        if (routes.Count > 0) {
          string upsertQuery = @"
              INSERT INTO plumbing_horizontal_routes
              (id, project_id, start_pos_x, end_pos_x, start_pos_y, end_pos_y, start_pos_z, end_pos_z, base_point_id, type, pipe_type, slope)
              VALUES (@id, @projectId, @startPosX, @endPosX, @startPosY, @endPosY, @startPosZ, @endPosZ, @basePointId, @type, @pipeType, @slope)
              ON DUPLICATE KEY UPDATE
                  start_pos_x = @startPosX,
                  end_pos_x = @endPosX,
                  start_pos_y = @startPosY,
                  end_pos_y = @endPosY,
                  start_pos_z = @startPosZ,
                  end_pos_z = @endPosZ,
                  base_point_id = @basePointId,
                  type = @type,
                  pipe_type = @pipeType,
                  slope = @slope
          ";
          foreach (var route in routes) {
            MySqlCommand command = new MySqlCommand(upsertQuery, conn);
            command.Parameters.AddWithValue("@id", route.Id);
            command.Parameters.AddWithValue("@projectId", projectId);
            command.Parameters.AddWithValue("@startPosX", route.StartPoint.X);
            command.Parameters.AddWithValue("@startPosY", route.StartPoint.Y);
            command.Parameters.AddWithValue("@startPosZ", route.StartPoint.Z);
            command.Parameters.AddWithValue("@endPosX", route.EndPoint.X);
            command.Parameters.AddWithValue("@endPosY", route.EndPoint.Y);
            command.Parameters.AddWithValue("@endPosZ", route.EndPoint.Z);
            command.Parameters.AddWithValue("@basePointId", route.BasePointId);
            command.Parameters.AddWithValue("@type", route.Type);
            command.Parameters.AddWithValue("@pipeType", route.PipeType);
            command.Parameters.AddWithValue("@slope", route.Slope);
            await command.ExecuteNonQueryAsync();
          }
        }

        await conn.CloseAsync();
    }


    public async Task UpdatePlumbingVerticalRoutes(List<PlumbingVerticalRoute> routes, string projectId) {
      if (routes == null) {
        return;
      }
      var idsToKeep = routes.Select(r => r.Id).ToList();
      MySqlConnection conn = await OpenNewConnectionAsync();

      if (idsToKeep.Count > 0) {
        var paramNames = idsToKeep.Select((id, i) => $"@id{i}").ToList();
        string deleteQuery = $@"
              DELETE FROM plumbing_vertical_routes
              WHERE project_id = @projectId
              AND id NOT IN ({string.Join(",", paramNames)})
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", projectId);
        for (int i = 0; i < idsToKeep.Count; i++) {
          deleteCommand.Parameters.AddWithValue(paramNames[i], idsToKeep[i]);
        }
        await deleteCommand.ExecuteNonQueryAsync();
      }
      else {
        string deleteQuery = @"
              DELETE FROM plumbing_vertical_routes
              WHERE project_id = @projectId
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", projectId);
        await deleteCommand.ExecuteNonQueryAsync();
      }
      if (routes.Count > 0) {
        string upsertQuery = @"
              INSERT INTO plumbing_vertical_routes
              (id, project_id, pos_x, pos_y, pos_z, vertical_route_id, base_point_id, start_height, length, node_type_id, type, pipe_type, is_up)
              VALUES (@id, @projectId, @posX, @posY, @posZ, @verticalRouteId, @basePointId, @startHeight, @length, @nodeTypeId, @type, @pipeType, @isUp)
              ON DUPLICATE KEY UPDATE
              pos_x = @posX,
              pos_y = @posy,
              pos_z = @posZ,
              vertical_route_id = @verticalRouteId,
              base_point_id = @basePointId,
              start_height = @startHeight,
              length = @length,
              node_type_id = @nodeTypeId,
              type = @type,
              pipe_type = @pipeType,
              is_up = @isUp
          ";
        foreach (var route in routes) {
          MySqlCommand command = new MySqlCommand(upsertQuery, conn);
          command.Parameters.AddWithValue("@id", route.Id);
          command.Parameters.AddWithValue("@projectId", projectId);
          command.Parameters.AddWithValue("@posX", route.Position.X);
          command.Parameters.AddWithValue("@posY", route.Position.Y);
          command.Parameters.AddWithValue("@posZ", route.Position.Z);
          command.Parameters.AddWithValue("@verticalRouteId", route.VerticalRouteId);
          command.Parameters.AddWithValue("@basePointId", route.BasePointId);
          command.Parameters.AddWithValue("@startHeight", route.StartHeight);
          command.Parameters.AddWithValue("@length", route.Length);
          command.Parameters.AddWithValue("@nodeTypeId", route.NodeTypeId);
          command.Parameters.AddWithValue("@type", route.Type);
          command.Parameters.AddWithValue("@pipeType", route.PipeType);
          command.Parameters.AddWithValue("@isUp", route.IsUp);
          await command.ExecuteNonQueryAsync();
        }
      }
    }
    public async Task ClearPlumbingRouteInfoBoxes(string viewportId) {
      MySqlConnection conn = await OpenNewConnectionAsync();
      string deleteQuery = @"
            DELETE FROM plumbing_route_info_boxes
            WHERE viewport_id = @viewportId";
      MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
      deleteCommand.Parameters.AddWithValue("@viewportId", viewportId);
      await deleteCommand.ExecuteNonQueryAsync();
    }
    public async Task InsertPlumbingRouteInfoBoxes(List<RouteInfoBox> boxes, string viewportId) {
      if (boxes == null) {
        return;
      }
      MySqlConnection conn = await OpenNewConnectionAsync();
      
      if (boxes.Count > 0) {
        string upsertQuery = @"
              INSERT INTO plumbing_route_info_boxes
              (viewport_id, pos_x, pos_y, base_point_id, pipe_size, type, location_description, cfh, longest_run_length, direction_description, is_vertical_route)
              VALUES (@viewportId, @posX, @posY, @basePointId, @pipeSize, @type, @locationDescription, @cfh, @longestRunLength, @directionDescription, @isVerticalRoute)";
        foreach (var box in boxes) {
          MySqlCommand command = new MySqlCommand(upsertQuery, conn);
          command.Parameters.AddWithValue("@viewportId", viewportId);
          command.Parameters.AddWithValue("@posX", box.Position.X);
          command.Parameters.AddWithValue("@posY", box.Position.Y);
          command.Parameters.AddWithValue("@basePointId", box.BasePointId);
          command.Parameters.AddWithValue("@pipeSize", box.PipeSize);
          command.Parameters.AddWithValue("@type", box.Type);
          command.Parameters.AddWithValue("@locationDescription", box.LocationDescription);
          command.Parameters.AddWithValue("@cfh", box.CFH);
          command.Parameters.AddWithValue("@longestRunLength", box.LongestRunLength);
          command.Parameters.AddWithValue("@directionDescription", box.DirectionDescription);
          command.Parameters.AddWithValue("@isVerticalRoute", box.IsVerticalRoute);
          await command.ExecuteNonQueryAsync();
        }
      }
    }
    public async Task UpdatePlumbingPlanBasePoints(List<PlumbingPlanBasePoint> points, string ProjectId) {
      if (points == null) {
        return;
      }
      var idsToKeep = points.Select(p => p.Id).ToList();
      MySqlConnection conn = await OpenNewConnectionAsync();
      if (idsToKeep.Count > 0) {
        var paramNames = idsToKeep.Select((id, i) => $"@id{i}").ToList();
        string deleteQuery = $@"
              DELETE FROM plumbing_plan_base_points
              WHERE project_id = @projectId
              AND id NOT IN ({string.Join(",", paramNames)})
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", ProjectId);
        for (int i = 0; i < idsToKeep.Count; i++) {
          deleteCommand.Parameters.AddWithValue(paramNames[i], idsToKeep[i]);
        }
        await deleteCommand.ExecuteNonQueryAsync();
      }
      else {
        string deleteQuery = @"
              DELETE FROM plumbing_plan_base_points
              WHERE project_id = @projectId
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", ProjectId);
        await deleteCommand.ExecuteNonQueryAsync();
      }
      if (points.Count > 0) {
        string upsertQuery = @"
              INSERT INTO plumbing_plan_base_points
              (id, project_id, viewport_id, floor, floor_height, ceiling_height, plan, type, pos_x, pos_y, is_site, is_site_ref)
              VALUES (@id, @projectId, @viewportId, @floor, @floorHeight, @ceilingHeight, @plan, @type, @posX, @posY, @isSite, @isSiteRef)
              ON DUPLICATE KEY UPDATE
                  viewport_id = @viewportId,
                  floor = @floor,
                  floor_height = @floorHeight,  
                  ceiling_height = @ceilingHeight,
                  plan = @plan,
                  type = @type,
                  pos_x = @posX,
                  pos_y = @posY,
                  is_site = @isSite,
                  is_site_ref = @isSiteRef
          ";
        foreach (var point in points) {
          MySqlCommand command = new MySqlCommand(upsertQuery, conn);
          command.Parameters.AddWithValue("@id", point.Id);
          command.Parameters.AddWithValue("@projectId", ProjectId);
          command.Parameters.AddWithValue("@viewportId", point.ViewportId);
          command.Parameters.AddWithValue("@floor", point.Floor);
          command.Parameters.AddWithValue("@floorHeight", point.FloorHeight);
          command.Parameters.AddWithValue("@ceilingHeight", point.CeilingHeight);
          command.Parameters.AddWithValue("@plan", point.Plan);
          command.Parameters.AddWithValue("@type", point.Type);
          command.Parameters.AddWithValue("@posX", point.Point.X);
          command.Parameters.AddWithValue("@posY", point.Point.Y);
          command.Parameters.AddWithValue("@isSite", point.IsSite);
          command.Parameters.AddWithValue("@isSiteRef", point.IsSiteRef);
          await command.ExecuteNonQueryAsync();
        }
      }
      await conn.CloseAsync();
    }
    public async Task UpdatePlumbingSources(List<PlumbingSource> sources, string ProjectId) {
      if (sources == null) {
        return;
      }
      var idsToKeep = sources.Select(s => s.Id).ToList();
      MySqlConnection conn = await OpenNewConnectionAsync();
      if (idsToKeep.Count > 0) {
        var paramNames = idsToKeep.Select((id, i) => $"@id{i}").ToList();
        string deleteQuery = $@"
              DELETE FROM plumbing_sources
              WHERE project_id = @projectId
              AND id NOT IN ({string.Join(",", paramNames)})
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", ProjectId);
        for (int i = 0; i < idsToKeep.Count; i++) {
          deleteCommand.Parameters.AddWithValue(paramNames[i], idsToKeep[i]);
        }
        await deleteCommand.ExecuteNonQueryAsync();
      }
      else {
        string deleteQuery = @"
              DELETE FROM plumbing_sources
              WHERE project_id = @projectId
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", ProjectId);
        await deleteCommand.ExecuteNonQueryAsync();
      }
      if (sources.Count > 0) {
        string upsertQuery = @"
              INSERT INTO plumbing_sources
              (id, project_id, pos_x, pos_y, pos_z, type_id, base_point_id, pressure)
              VALUES (@id, @projectId, @posX, @posY, @posZ, @typeId, @basePointId, @pressure)
              ON DUPLICATE KEY UPDATE
                  pos_x = @posX,
                  pos_y = @posY,
                  pos_z = @posZ,
                  type_id = @typeId,
                  base_point_id = @basePointId,
                  pressure = @pressure
          ";
        foreach (var source in sources) {
          MySqlCommand command = new MySqlCommand(upsertQuery, conn);
          command.Parameters.AddWithValue("@id", source.Id);
          command.Parameters.AddWithValue("@projectId", ProjectId);
          command.Parameters.AddWithValue("@posX", source.Position.X);
          command.Parameters.AddWithValue("@posY", source.Position.Y);
          command.Parameters.AddWithValue("@posZ", source.Position.Z);
          command.Parameters.AddWithValue("@typeId", source.TypeId);
          command.Parameters.AddWithValue("@basePointId", source.BasePointId);
          command.Parameters.AddWithValue("@pressure", source.Pressure);
          await command.ExecuteNonQueryAsync();
        }
      }
      await conn.CloseAsync();
    }
    public async Task UpdatePlumbingFixtures(List<PlumbingFixture> fixtures, string ProjectId) {
      if (fixtures == null) {
        return;
      }
      var idsToKeep = fixtures.Select(list => list).Select(f => f.Id).ToList();
      MySqlConnection conn = await OpenNewConnectionAsync();
      if (idsToKeep.Count > 0) {
        var paramNames = idsToKeep.Select((id, i) => $"@id{i}").ToList();
        string deleteQuery = $@"
              DELETE FROM plumbing_fixtures
              WHERE project_id = @projectId
              AND id NOT IN ({string.Join(",", paramNames)})
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", ProjectId);
        for (int i = 0; i < idsToKeep.Count; i++) {
          deleteCommand.Parameters.AddWithValue(paramNames[i], idsToKeep[i]);
        }
        await deleteCommand.ExecuteNonQueryAsync();
      }
      else {
        string deleteQuery = @"
              DELETE FROM plumbing_fixtures
              WHERE project_id = @projectId
          ";
        MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, conn);
        deleteCommand.Parameters.AddWithValue("@projectId", ProjectId);
        await deleteCommand.ExecuteNonQueryAsync();
      }
      if (fixtures.Count > 0) {
        string upsertQuery = @"
              INSERT INTO plumbing_fixtures
              (id, project_id, catalog_id, number, base_point_id, type_abbreviation, rotation, block_name, flow_type_id, pos_x, pos_y, pos_z)
              VALUES (@id, @projectId, @catalogId, @number, @basePointId, @typeAbbreviation, @rotation, @blockName, @flowTypeId, @posX, @posY, @posZ)
              ON DUPLICATE KEY UPDATE
                  pos_x = @posX,
                  pos_y = @posY,
                  pos_z = @posZ,
                  rotation = @rotation,
                  number = @number,
                  flow_type_id = @flowTypeId
          ";
        foreach (var component in fixtures.Select(list => list)) {
          MySqlCommand command = new MySqlCommand(upsertQuery, conn);
          command.Parameters.AddWithValue("@id", component.Id);
          command.Parameters.AddWithValue("@projectId", ProjectId);
          command.Parameters.AddWithValue("@catalogId", component.CatalogId);
          command.Parameters.AddWithValue("@number", component.Number);
          command.Parameters.AddWithValue("@basePointId", component.BasePointId);
          command.Parameters.AddWithValue("@typeAbbreviation", component.TypeAbbreviation);
          command.Parameters.AddWithValue("@rotation", component.Rotation);
          command.Parameters.AddWithValue("@blockName", component.BlockName);
          command.Parameters.AddWithValue("@posX", component.Position.X);
          command.Parameters.AddWithValue("@posY", component.Position.Y);
          command.Parameters.AddWithValue("@posZ", component.Position.Z);
          command.Parameters.AddWithValue("@flowTypeId", component.FlowTypeId);
          await command.ExecuteNonQueryAsync();
        }
      }
      await conn.CloseAsync();
    }

    public async Task<List<PlumbingHorizontalRoute>> GetPlumbingHorizontalRoutes(string ProjectId) {
      var routes = new List<PlumbingHorizontalRoute>();
      await OpenConnectionAsync();
      string query = @"
            SELECT * FROM plumbing_horizontal_routes
            WHERE project_id = @projectId
            ORDER BY base_point_id, start_pos_x, start_pos_y, end_pos_x, end_pos_y";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", ProjectId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (reader.Read()) {
        var route = new PlumbingHorizontalRoute(
          reader.GetString("id"),
          ProjectId,
          reader.GetString("type"),
          new Point3d(
            reader.GetDouble("start_pos_x"),
            reader.GetDouble("start_pos_y"),
            reader.GetDouble("start_pos_z")
          ),
         new Point3d(
            reader.GetDouble("end_pos_x"),
            reader.GetDouble("end_pos_y"),
            reader.GetDouble("end_pos_z")
          ),
         reader.GetString("base_point_id"),
         reader.GetString("pipe_type"),
         reader.GetDouble("slope")
        );
        routes.Add(route);
      }
      reader.Close();
      await CloseConnectionAsync();
      return routes;
    }
    public async Task<List<PlumbingVerticalRoute>> GetPlumbingVerticalRoutes(string ProjectId) {
      var routes = new List<PlumbingVerticalRoute>();
      await OpenConnectionAsync();
      string query = @"
            SELECT * FROM plumbing_vertical_routes
            WHERE project_id = @projectId
            ORDER BY vertical_route_id, base_point_id, pos_x, pos_y";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", ProjectId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (reader.Read()) {
        var verticalRoute = new PlumbingVerticalRoute(
          reader.GetString("id"),
          ProjectId,
          reader.GetString("type"),
          new Point3d(
            reader.GetDouble("pos_x"),
            reader.GetDouble("pos_y"),
            reader.GetDouble("pos_z")
          ),
          reader.GetString("vertical_route_id"),
          reader.GetString("base_point_id"),
          reader.GetDouble("start_height"),
          reader.GetDouble("length"), 
          reader.GetInt32("node_type_id"),
          reader.GetString("pipe_type"),
          reader.GetBoolean("is_up")
        );
        routes.Add(verticalRoute);
      }
      reader.Close();
      await CloseConnectionAsync();
      return routes;
    }

    public async Task<List<RouteInfoBox>> GetPlumbingRouteInfoBoxes(string viewportId) {
      var boxes = new List<RouteInfoBox>();
      await OpenConnectionAsync();
      string query = @"
            SELECT * FROM plumbing_route_info_boxes
            WHERE viewport_id = @viewportId";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@viewportId", viewportId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (reader.Read()) {
        var box = new RouteInfoBox(
          reader.GetString("viewport_id"),
          new Point3d(
            reader.GetDouble("pos_x"),
            reader.GetDouble("pos_y"),
            0
          ),
          reader.GetString("base_point_id"),
          reader.GetString("pipe_size"),
          reader.GetString("type"),
          reader.GetString("location_description"),
          reader.GetString("cfh"),
          reader.GetString("longest_run_length"),
          reader.GetString("direction_description"),
          reader.GetBoolean("is_vertical_route")
        );
        boxes.Add(box);
      }
      reader.Close();
      await CloseConnectionAsync();
      return boxes;
    }

    public async Task<List<PlumbingSource>> GetPlumbingSources(string ProjectId) {
      var sources = new List<PlumbingSource>();
      await OpenConnectionAsync();
      string query = @"
            SELECT * FROM plumbing_sources
            WHERE project_id = @projectId
            ORDER BY base_point_id, pos_x, pos_y";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", ProjectId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (reader.Read()) {
        var source = new PlumbingSource(
          reader.GetString("id"),
          ProjectId,
          new Point3d(
            reader.GetDouble("pos_x"),
            reader.GetDouble("pos_y"),
            reader.GetDouble("pos_z")
          ),
          reader.GetInt32("type_id"),
          reader.GetString("base_point_id"),
          reader.GetDouble("pressure")
        );
        sources.Add(source);
      }
      reader.Close();
      await CloseConnectionAsync();
      return sources;
    }
    public async Task<List<PlumbingFixture>> GetPlumbingFixtures(string ProjectId) {
      var fixtures = new List<PlumbingFixture>();

      await OpenConnectionAsync();
      string query = @"
            SELECT * FROM plumbing_fixtures
            WHERE project_id = @projectId
            ORDER BY type_abbreviation, catalog_id, number";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", ProjectId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (reader.Read()) {
        double posx = reader.GetDouble("pos_x");
        double posy = reader.GetDouble("pos_y");
        double posz = reader.GetDouble("pos_z");

        var fixture = new PlumbingFixture(
          reader.GetString("id"),
          ProjectId,
          new Point3d(posx, posy, posz),
          reader.GetDouble("rotation"),
          reader.GetInt32("catalog_id"),
          reader.GetString("type_abbreviation"),
          reader.GetInt32("number"),
          reader.GetString("base_point_id"),
          reader.GetString("block_name"),
          reader.GetInt32("flow_type_id")
        );
        fixtures.Add(fixture);
      }
      reader.Close();
      await CloseConnectionAsync();
      return fixtures;
    }
    public async Task<List<PlumbingPlanBasePoint>> GetPlumbingPlanBasePoints(string ProjectId) {
      var points = new List<PlumbingPlanBasePoint>();
      await OpenConnectionAsync();
      string query = @"
            SELECT * FROM plumbing_plan_base_points
            WHERE project_id = @projectId
            ORDER BY viewport_id, floor, pos_x, pos_y";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", ProjectId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (reader.Read()) {
        var point = new PlumbingPlanBasePoint(
          reader.GetString("id"),
          ProjectId,
           new Point3d(
            reader.GetDouble("pos_x"),
            reader.GetDouble("pos_y"),
            0
          ),
          reader.GetString("plan"),
          reader.GetString("type"),
          reader.GetString("viewport_id"),
          reader.GetInt32("floor"),
          reader.GetDouble("floor_height"),
          reader.GetDouble("ceiling_height"),
          reader.GetBoolean("is_site"),
          reader.GetBoolean("is_site_ref")
        );
        points.Add(point);
      }
      reader.Close();
      await CloseConnectionAsync();
      return points;
    }
  }
}
