using System;
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
  public class MariaDBService
  {
    public string ConnectionString { get; set; }
    public MySqlConnection Connection { get; set; }
    public Document doc { get; private set; }
    public Database db { get; private set; }
    public Editor ed { get; private set; }
    public string ProjectId { get; private set; }

    public MariaDBService()
    {
      ConnectionString = Properties.Settings.Default.ConnectionString;
      Connection = new MySqlConnection(ConnectionString);

      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;
    }

    string GetSafeString(MySqlDataReader reader, string fieldName)
    {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index))
      {
        return reader.GetString(index);
      }
      return string.Empty;
    }

    int GetSafeInt(MySqlDataReader reader, string fieldName)
    {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index))
      {
        return reader.GetInt32(index);
      }
      return 0;
    }

    float GetSafeFloat(MySqlDataReader reader, string fieldName)
    {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index))
      {
        return reader.GetFloat(index);
      }
      return 0;
    }

    bool GetSafeBoolean(MySqlDataReader reader, string fieldName)
    {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index))
      {
        return reader.GetBoolean(index);
      }
      return false;
    }

    decimal GetSafeDecimal(MySqlDataReader reader, string fieldName)
    {
      int index = reader.GetOrdinal(fieldName);
      if (!reader.IsDBNull(index))
      {
        return reader.GetDecimal(index);
      }
      return 0;
    }

    public void OpenConnectionSync()
    {
      if (Connection.State == System.Data.ConnectionState.Closed)
      {
        Connection.Open();
      }
    }

    public void CloseConnectionSync()
    {
      if (Connection.State == System.Data.ConnectionState.Open)
      {
        Connection.Close();
      }
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

    public async Task<MySqlConnection> OpenNewConnectionAsync()
    {
      MySqlConnection conn = new MySqlConnection(ConnectionString);
      await conn.OpenAsync();
      return conn;
    }

    public async Task<string> GetProjectId(string projectNo)
    {
      if (!string.IsNullOrEmpty(ProjectId))
      {
        return ProjectId;
      }
      if (projectNo == null || projectNo == string.Empty)
      {
        return string.Empty;
      }
      string query = "SELECT id FROM projects WHERE gmep_project_no = @projectNo";
      await OpenConnectionAsync();
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectNo", projectNo);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();

      Dictionary<int, string> projectIds = new Dictionary<int, string>();
      string id = "";
      if (reader.Read())
      {
        id = reader.GetString("id");
      }
      reader.Close();

      await CloseConnectionAsync();
      ProjectId = id;
      return id;
    }

    public string GetProjectIdSync(string projectNo)
    {
      if (!string.IsNullOrEmpty(ProjectId))
      {
        return ProjectId;
      }
      if (projectNo == null || projectNo == string.Empty)
      {
        return string.Empty;
      }
      string query = "SELECT id FROM projects WHERE gmep_project_no = @projectNo";
      OpenConnectionSync();
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectNo", projectNo);
      MySqlDataReader reader = command.ExecuteReader();

      Dictionary<int, string> projectIds = new Dictionary<int, string>();
      string id = "";
      if (reader.Read())
      {
        id = reader.GetString("id");
      }
      reader.Close();

      CloseConnectionSync();
      ProjectId = id;
      return id;
    }

    public async Task<WaterSystemData> GetWaterSystemData(string projectId)
    {
      await OpenConnectionAsync();
      //get the additional losses

      WaterSystemData waterSystemData = new WaterSystemData();
      waterSystemData.AdditionalLosses = new ObservableCollection<AdditionalLoss>();
      waterSystemData.AdditionalLosses2 = new ObservableCollection<AdditionalLoss>();

      string query = @"SELECT * FROM plumbing_additional_losses WHERE project_id = @projectId";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@projectId", projectId);
      MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
        bool is2 = reader.GetBoolean("is_2");
        AdditionalLoss additionalLoss = new AdditionalLoss
        {
          Id = reader.GetString("id"),
          Title = reader.GetString("title"),
          Amount = reader.GetString("amount"),
        };
        if (is2)
        {
          waterSystemData.AdditionalLosses2.Add(additionalLoss);
        }
        else
        {
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

      while (await reader.ReadAsync())
      {
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

    private object SanitizeDouble(double value)
    {
      if (double.IsNaN(value) || double.IsInfinity(value))
        return 0;
      return value;
    }

    public async Task<bool> UpdateWaterSystem(WaterSystemData waterSystemData, string projectId)
    {
      try
      {
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

        if (!string.IsNullOrEmpty(ids))
        {
          MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, Connection);
          deleteCommand.Parameters.AddWithValue("@projectId", projectId);
          deleteCommand.Parameters.AddWithValue("@ids", ids);
          await deleteCommand.ExecuteNonQueryAsync();
        }
        else
        {
          deleteQuery =
            @"
                    DELETE FROM plumbing_additional_losses
                    WHERE project_id = @projectId";
          MySqlCommand deleteCommand = new MySqlCommand(deleteQuery, Connection);
          deleteCommand.Parameters.AddWithValue("@projectId", projectId);
          await deleteCommand.ExecuteNonQueryAsync();
        }

        foreach (var loss in waterSystemData.AdditionalLosses)
        {
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
        foreach (var loss in waterSystemData.AdditionalLosses2)
        {
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
      catch (Exception ex)
      {
        ed.WriteMessage("database error: " + ex.Message);
        await CloseConnectionAsync();
        return false;
      }
      return true;
    }

    public List<PlumbingFixtureType> GetPlumbingFixtureTypes()
    {
      List<PlumbingFixtureType> fixtureTypes = new List<PlumbingFixtureType>();
      OpenConnectionSync();
      string query = "SELECT * FROM plumbing_fixture_types ORDER BY abbreviation";
      MySqlCommand command = new MySqlCommand(query, Connection);
      MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
        fixtureTypes.Add(
          new PlumbingFixtureType(
            GetSafeInt(reader, "id"),
            GetSafeString(reader, "name"),
            GetSafeString(reader, "abbreviation"),
            GetSafeString(reader, "water_gas_block_name"),
            GetSafeString(reader, "waste_vent_block_name")
          )
        );
      }
      reader.Close();
      CloseConnectionSync();
      return fixtureTypes;
    }

    public List<PlumbingFixtureCatalogItem> GetPlumbingFixtureCatalogItemsByType(int typeId)
    {
      List<PlumbingFixtureCatalogItem> items = new List<PlumbingFixtureCatalogItem>();
      OpenConnectionSync();
      string query =
        "SELECT * FROM plumbing_fixture_catalog WHERE type_id = @typeId ORDER BY description";
      MySqlCommand command = new MySqlCommand(query, Connection);
      command.Parameters.AddWithValue("@typeId", typeId);
      MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
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
            GetSafeString(reader, "water_gas_block_name"),
            GetSafeString(reader, "waste_vent_block_name")
          )
        );
      }
      reader.Close();
      CloseConnectionSync();
      return items;
    }

    public void CreatePlumbingFixture(PlumbingFixture fixture)
    {
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
      while (reader.Read())
      {
        addedCatalogIds.Add(GetSafeInt(reader, "id"));
        addedFixtureNumbers.Add(GetSafeInt(reader, "number"));
      }
      if (addedCatalogIds.Count == 0)
      {
        count = 1;
      }
      for (int i = 0; i < addedCatalogIds.Count; i++)
      {
        if (addedCatalogIds[i] == fixture.CatalogId)
        {
          count = addedFixtureNumbers[i];
        }
      }
      if (count == 0)
      {
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
    }

    public List<PlumbingSourceType> GetPlumbingSourceTypes()
    {
      List<PlumbingSourceType> types = new List<PlumbingSourceType>();
      string query = "SELECT * FROM plumbing_source_types";

      OpenConnectionSync();
      MySqlCommand command = new MySqlCommand(query, Connection);
      MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
        types.Add(new PlumbingSourceType(GetSafeInt(reader, "id"), GetSafeString(reader, "type")));
      }
      reader.Close();
      CloseConnectionSync();
      return types;
    }

    public async Task CreatePlumbingSource(PlumbingSource source)
    {
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
    }

    public async Task CreatePlumbingPlanBasePoint(PlumbingPlanBasePoint point)
    {
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
    }
  }
}
