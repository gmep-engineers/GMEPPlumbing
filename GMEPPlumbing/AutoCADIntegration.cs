using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using GMEPPlumbing.Commands;
using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using GMEPPlumbing.Views;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;

[assembly: CommandClass(typeof(GMEPPlumbing.AutoCADIntegration))]
[assembly: CommandClass(typeof(GMEPPlumbing.Commands.TableCommand))]

namespace GMEPPlumbing
{
  public class AutoCADIntegration
  {
    private const string XRecordKey = "GMEPPlumbingID";
    private PaletteSet pw;
    private UserInterface myControl;
    private string currentDrawingId;
    private WaterSystemViewModel viewModel;

    public Document doc { get; private set; }
    public Database db { get; private set; }
    public Editor ed { get; private set; }

    [CommandMethod("Water")]
    public void Water()
    {
      MongoDBService.Initialize();

      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;

      RetrieveOrCreateDrawingId();
      InitializeUserInterface();
      LoadDataAsync();
    }

    public void WriteMessage(string message)
    {
      ed.WriteMessage(message);
    }

    public void RetrieveOrCreateDrawingId()
    {
      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        try
        {
          DateTime creationTime = RetrieveXRecordId(db, tr);

          if (string.IsNullOrEmpty(currentDrawingId))
          {
            currentDrawingId = Guid.NewGuid().ToString();
            creationTime = GetFileCreationTime();
            CreateXRecordId(db, tr, currentDrawingId);
            ed.WriteMessage($"\nCreated new Drawing ID: {currentDrawingId}, Creation Time: {creationTime}");
          }
          else
          {
            ed.WriteMessage($"\nRetrieved existing Drawing ID: {currentDrawingId}, Creation Time: {creationTime}");
          }

          tr.Commit();
        }
        catch (System.Exception ex)
        {
          ed.WriteMessage($"\nError handling Drawing ID: {ex.Message}");
          tr.Abort();
        }
      }
    }

    public DateTime RetrieveXRecordId(Database db, Transaction tr)
    {
      RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
      if (!regAppTable.Has(XRecordKey))
        return DateTime.MinValue;

      DBDictionary namedObjDict = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
      if (!namedObjDict.Contains(XRecordKey))
        return DateTime.MinValue;

      Xrecord xRec = (Xrecord)tr.GetObject(namedObjDict.GetAt(XRecordKey), OpenMode.ForRead);
      TypedValue[] values = xRec.Data.AsArray();

      if (values.Length < 2)
        return DateTime.MinValue;

      currentDrawingId = values[0].Value.ToString();
      double acadDate = (double)values[1].Value;

      DateTime creationTime = new DateTime(1899, 12, 30).AddDays(acadDate);

      return creationTime;
    }

    public void CreateXRecordId(Database db, Transaction tr, string drawingId)
    {
      RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
      if (!regAppTable.Has(XRecordKey))
      {
        RegAppTableRecord regAppTableRecord = new RegAppTableRecord
        {
          Name = XRecordKey
        };
        regAppTable.Add(regAppTableRecord);
        tr.AddNewlyCreatedDBObject(regAppTableRecord, true);
      }

      DBDictionary namedObjDict = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
      Xrecord xRec = new Xrecord();

      // Get the file creation time
      DateTime creationTime = GetFileCreationTime();

      // Convert DateTime to AutoCAD date (number of days since December 30, 1899)
      double acadDate = (creationTime - new DateTime(1899, 12, 30)).TotalDays;

      // Create a ResultBuffer with multiple TypedValues
      xRec.Data = new ResultBuffer(
          new TypedValue((int)DxfCode.Text, drawingId),
          new TypedValue((int)DxfCode.Real, acadDate)
      );

      namedObjDict.SetAt(XRecordKey, xRec);
      tr.AddNewlyCreatedDBObject(xRec, true);
    }

    private void InitializeUserInterface()
    {
      // Create the viewModel & get the data off mongoDB
      viewModel = new WaterSystemViewModel(
          new WaterMeterLossCalculationService(),
          new WaterStaticLossService(),
          new WaterTotalLossService(),
          new WaterPressureAvailableService(),
          new WaterDevelopedLengthService(),
          new WaterRemainingPressurePer100FeetService(),
          new WaterAdditionalLosses(),
          new WaterAdditionalLosses());

      myControl = new UserInterface(viewModel);
      var host = new ElementHost();
      host.Child = myControl;

      pw = new PaletteSet("GMEP Plumbing Water Calculator");
      pw.Style = PaletteSetStyles.ShowAutoHideButton |
                 PaletteSetStyles.ShowCloseButton |
                 PaletteSetStyles.ShowPropertiesMenu;
      pw.DockEnabled = DockSides.Left | DockSides.Right;

      pw.Size = new System.Drawing.Size(1200, 800);
      pw.MinimumSize = new System.Drawing.Size(1200, 800);
      pw.Add("MyTab", host);

      pw.Visible = true;
      pw.Dock = DockSides.Left;
      pw.RolledUp = false;

      // Add event handler for PaletteSet closing
      pw.StateChanged += Pw_StateChanged;
    }

    private async void LoadDataAsync()
    {
      try
      {
        var data = await MongoDBService.GetDrawingDataAsync(currentDrawingId);
        if (data != null)
        {
          myControl.Dispatcher.Invoke(() =>
          {
            viewModel.UpdatePropertiesFromData(data);
          });

          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nSuccessfully loaded data from MongoDB.\n");
        }
      }
      catch (System.Exception ex)
      {
        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nError loading data from MongoDB: {ex.Message}\n");
      }
    }

    private async void Pw_StateChanged(object sender, PaletteSetStateEventArgs e)
    {
      if (e.NewState == StateEventIndex.Hide)
      {
        try
        {
          WaterSystemData data = viewModel.GetWaterSystemData();
          bool updateResult = await MongoDBService.UpdateDrawingDataAsync(data, currentDrawingId);
          if (updateResult)
          {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nSuccessfully updated drawing data in MongoDB.\n");
          }
          else
          {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nFailed to update drawing data in MongoDB.\n");
          }
        }
        catch (System.Exception ex)
        {
          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nError updating drawing data: {ex.Message}\n");
        }
      }
    }

    private DateTime GetFileCreationTime()
    {
      if (doc != null && !string.IsNullOrEmpty(doc.Name))
      {
        FileInfo fileInfo = new FileInfo(doc.Name);
        return fileInfo.CreationTime.ToUniversalTime();
      }
      else
      {
        return DateTime.UtcNow;
      }
    }
  }
}