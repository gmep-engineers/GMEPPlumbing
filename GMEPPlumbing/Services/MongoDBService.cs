using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.IO;

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

        Console.WriteLine($"Attempting to connect to MongoDB with connection string: {connectionString}");

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);

        _client = new MongoClient(settings);
        _database = _client.GetDatabase("GMEPPlumbing");

        // Test the connection
        _database.RunCommand((Command<BsonDocument>)"{ping:1}");

        Console.WriteLine("Successfully connected to MongoDB.");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Failed to initialize MongoDB connection: {ex.Message}");
        if (ex.InnerException != null)
        {
          Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        throw;
      }
    }

    public static IMongoCollection<T> GetCollection<T>(string name)
    {
      if (_database == null)
      {
        Initialize();
      }
      return _database.GetCollection<T>(name);
    }

    public static void AddRandomKeyValuePair()
    {
      try
      {
        if (_database == null)
        {
          Initialize();
        }

        var collection = _database.GetCollection<BsonDocument>("TestCollection");

        var randomKey = Guid.NewGuid().ToString();
        var randomValue = Guid.NewGuid().ToString();

        var document = new BsonDocument
                {
                    { randomKey, randomValue }
                };

        collection.InsertOne(document);

        Console.WriteLine($"Added random key-value pair to TestCollection: {{{randomKey}: {randomValue}}}");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Failed to add random key-value pair: {ex.Message}");
        if (ex.InnerException != null)
        {
          Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
      }
    }
  }
}