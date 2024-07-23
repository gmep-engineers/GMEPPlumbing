using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using GMEPPlumbing.ViewModels;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace GMEPPlumbing.Services
{
  public static class MongoDBService
  {
    private static IMongoDatabase _database;
    private static IMongoClient _client;
    private const string CollectionName = "DrawingData";

    public static void Initialize()
    {
      try
      {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        IConfiguration configuration = builder.Build();
        var connectionString = configuration.GetConnectionString("MongoDB");
        WriteToCommandLine($"Attempting to connect to MongoDB with connection string: {connectionString}");

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
        throw;
      }
    }

    // Create
    public static async Task<WaterSystemData> CreateDrawingDataAsync(WaterSystemData data)
    {
      try
      {
        var collection = _database.GetCollection<WaterSystemData>(CollectionName);
        await collection.InsertOneAsync(data);
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
        var collection = _database.GetCollection<WaterSystemData>(CollectionName);
        var filter = Builders<WaterSystemData>.Filter.Eq("_id", drawingId);
        return await collection.Find(filter).FirstOrDefaultAsync();
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to get drawing data: {ex.Message}");
        throw;
      }
    }

    // Update
    public static async Task<bool> UpdateDrawingDataAsync(WaterSystemData data)
    {
      try
      {
        var collection = _database.GetCollection<WaterSystemData>(CollectionName);
        var filter = Builders<WaterSystemData>.Filter.Eq("_id", data.Id);
        var result = await collection.ReplaceOneAsync(filter, data, new ReplaceOptions { IsUpsert = true });
        return result.IsAcknowledged && (result.ModifiedCount > 0 || result.UpsertedId != null);
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to update drawing data: {ex.Message}");
        throw;
      }
    }

    // Delete
    public static async Task<bool> DeleteDrawingDataAsync(string drawingId)
    {
      try
      {
        var collection = _database.GetCollection<WaterSystemData>(CollectionName);
        var filter = Builders<WaterSystemData>.Filter.Eq("_id", drawingId);
        var result = await collection.DeleteOneAsync(filter);
        return result.IsAcknowledged && result.DeletedCount > 0;
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to delete drawing data: {ex.Message}");
        throw;
      }
    }

    // Get All
    public static async Task<List<WaterSystemData>> GetAllDrawingDataAsync()
    {
      try
      {
        var collection = _database.GetCollection<WaterSystemData>(CollectionName);
        return await collection.Find(_ => true).ToListAsync();
      }
      catch (Exception ex)
      {
        WriteToCommandLine($"Failed to get all drawing data: {ex.Message}");
        throw;
      }
    }

    private static void WriteToCommandLine(string message)
    {
      Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + message);
    }
  }
}