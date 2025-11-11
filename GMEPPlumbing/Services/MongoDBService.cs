using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Markup;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using GMEPPlumbing.ViewModels;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace GMEPPlumbing.Services
{
  public static class MongoDBService
  {
    private static IMongoDatabase _database;
    private static IMongoClient _client;
    private const string CollectionName = "DrawingData";

    // This class wraps WaterSystemData with an _id for MongoDB
    private class WaterSystemDataWrapper
    {
      [BsonId]
      public string Id { get; set; }

      public WaterSystemData Data { get; set; }
    }

    public static void Initialize()
    {
      try
      {
        string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string dllDirectory = Path.GetDirectoryName(dllPath);
        WriteToCommandLine($"DLL directory: {dllDirectory}");

        var builder = new ConfigurationBuilder()
          .SetBasePath(dllDirectory)
          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        IConfiguration configuration = builder.Build();
        var connectionString = configuration.GetConnectionString("MongoDB");
        WriteToCommandLine($"Attempting to connect to MongoDB with connection string.\n");

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _client = new MongoClient(settings);
        _database = _client.GetDatabase("GMEPPlumbing");

        // Test the connection
        _database.RunCommand((Command<BsonDocument>)"{ping:1}");
        WriteToCommandLine("Successfully connected to MongoDB.");
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to initialize MongoDB connection: {ex.Message}");
        if (ex.InnerException != null)
        {
          WriteToCommandLine($"Inner exception: {ex.InnerException.Message}");
        }
        WriteToCommandLine($"Stack trace: {ex.StackTrace}");
        throw;
      }
    }

    // Create
    public static async Task<WaterSystemData> CreateDrawingDataAsync(
      WaterSystemData data,
      string drawingId
    )
    {
      try
      {
        var collection = _database.GetCollection<WaterSystemDataWrapper>(CollectionName);
        var wrapper = new WaterSystemDataWrapper { Id = drawingId, Data = data };
        await collection.InsertOneAsync(wrapper);
        return data;
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to create drawing data: {ex.Message}");
        throw;
      }
    }

    // Read
    public static async Task<WaterSystemData> GetDrawingDataAsync(string drawingId)
    {
      try
      {
        var collection = _database.GetCollection<WaterSystemDataWrapper>(CollectionName);
        var filter = Builders<WaterSystemDataWrapper>.Filter.Eq(w => w.Id, drawingId);
        var wrapper = await collection.Find(filter).FirstOrDefaultAsync();
        return wrapper?.Data;
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to get drawing data: {ex.Message}");
        throw;
      }
    }

    // Update
    public static async Task<bool> UpdateDrawingDataAsync(
      WaterSystemData data,
      string currentDrawingId
    )
    {
      try
      {
        var collection = _database.GetCollection<WaterSystemDataWrapper>(CollectionName);
        var filter = Builders<WaterSystemDataWrapper>.Filter.Eq(w => w.Id, currentDrawingId);
        var update = Builders<WaterSystemDataWrapper>.Update.Set(w => w.Data, data);
        var result = await collection.UpdateOneAsync(
          filter,
          update,
          new UpdateOptions { IsUpsert = true }
        );
        return result.IsAcknowledged && (result.ModifiedCount > 0 || result.UpsertedId != null);
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to update drawing data: {ex.Message}");
        throw;
      }
    }

    private static void WriteToCommandLine(string message)
    {
      Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
        "\n" + message
      );
    }
  }
}
