using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GMEPPlumbing.Services
{
  public static class MongoDBService
  {
    private static IMongoDatabase _database;
    private static IMongoClient _client;

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

    public static string GetOrCreateDrawingData(string drawingId)
    {
      var collection = _database.GetCollection<BsonDocument>("TestCollection");
      var filter = Builders<BsonDocument>.Filter.Eq("_id", drawingId);
      var document = collection.Find(filter).FirstOrDefault();

      if (document == null)
      {
        // Create new document if not found
        document = new BsonDocument
        {
            { "_id", drawingId },
            { "CreatedAt", DateTime.UtcNow },
            // Add other initial data as needed
        };
        collection.InsertOne(document);
      }

      // Return the document as a string (you might want to serialize it differently based on your needs)
      return document.ToJson();
    }

    private static void WriteToCommandLine(string message)
    {
      Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + message);
    }

    internal static void SaveDrawingData(string currentDrawingId, object dataToSave)
    {
      return;
    }
  }
}