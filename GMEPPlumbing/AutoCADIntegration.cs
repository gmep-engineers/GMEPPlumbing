using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using GMEPPlumbing.Commands;
using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using GMEPPlumbing.Views;
using MongoDB.Bson.Serialization.Conventions;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;
using System.Windows.Media.Media3D;
using Autodesk.AutoCAD.Geometry;

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
    private bool needsXRecordUpdate = false;
    private string newDrawingId;
    private DateTime newCreationTime;
    public MariaDBService MariaDBService { get; set; } = new MariaDBService();

    public Document doc { get; private set; }
    public Database db { get; private set; }
    public Editor ed { get; private set; }
    public string ProjectId { get; private set; } = string.Empty;

    [CommandMethod("SETPLUMBINGBASEPOINT")]
    public async void SetPlumbingBasePoint()
    {
        doc = Application.DocumentManager.MdiActiveDocument;
        db = doc.Database;
        ed = doc.Editor;


        var prompt = new Views.BasePointPromptWindow();
        bool? result = prompt.ShowDialog();
        if (result != true)
        {
            ed.WriteMessage("\nOperation cancelled.");
            return;
        }
        bool water = prompt.Water;
        bool gas = prompt.Gas;
        bool sewerVent = prompt.SewerVent;
        bool storm = prompt.Storm;
        string planName = prompt.PlanName;
        string floorQtyResult = prompt.FloorQty;


        string viewport = "";
        if (water) viewport += "Water";
        if (viewport != "" && gas) viewport += "/";
        if (gas) viewport += "Gas";
        if (viewport != "" && sewerVent) viewport += "/";
        if (sewerVent) viewport += "Sewer/Vent";
        if (viewport != "" && storm) viewport += "/";
        if (storm) viewport += "Storm";



        if (!int.TryParse(floorQtyResult, out int floorQty))
        {
            ed.WriteMessage("\nInvalid floor quantity. Please enter a valid integer.");
            return;
        }
      
        for (int i = 0; i < floorQty; i++)
        {
            Point3d point;
            ObjectId blockId;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord curSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);


                BlockTableRecord block;
                string message = "\nCreating Plumbing Base Point for " + planName + " on floor " + (i + 1);
                BlockReference br = CADObjectCommands.CreateBlockReference(
                tr,
                bt,
                "GMEP PLMG Basepoint",
                out block,
                out point
                );
                    br.Layer = "Defpoints";
                    if (br != null)
                {
                    curSpace.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);
                    blockId = br.ObjectId;
                    DynamicBlockReferencePropertyCollection properties = br.DynamicBlockReferencePropertyCollection;
                    foreach (DynamicBlockReferenceProperty prop in properties)
                    {
                        if (prop.PropertyName == "Plan")
                        {
                            prop.Value = planName;
                        }
                        else if (prop.PropertyName == "Floor")
                        {
                            prop.Value = i + 1;
                        }
                        else if (prop.PropertyName == "Type")
                        {
                            prop.Value = viewport;
                        }
                    }
                }
                tr.Commit();
            }
        }
    }

    [CommandMethod("Water")]
    public async void Water()
    {
      //MongoDBService.Initialize();
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      ProjectId = await MariaDBService.GetProjectId(projectNo);

      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;

      RetrieveOrCreateDrawingId();
      InitializeUserInterface();
      LoadDataAsync();

      pw.Focus();
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
            var newCreationTime = GetFileCreationTime();
            ed.WriteMessage($"\nNew Creation Time: {newCreationTime}");

            if (Math.Abs((newCreationTime - creationTime).TotalSeconds) > 1)
            {
              needsXRecordUpdate = true;
              this.newDrawingId = Guid.NewGuid().ToString();
              this.newCreationTime = newCreationTime;
              ed.WriteMessage($"\nXRecord update needed. Will update after data load.");
              ed.WriteMessage($"\nOld Creation Time: {creationTime}");
              ed.WriteMessage($"\nNew Creation Time: {newCreationTime}");
            }
            else
            {
              ed.WriteMessage("\nCreation time has not changed. No update needed.");
            }
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

    private void UpdateXRecordId(Transaction tr, string newId, DateTime newCreationTime)
    {
      DBDictionary namedObjDict = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
      if (namedObjDict.Contains(XRecordKey))
      {
        Xrecord xRec = (Xrecord)tr.GetObject(namedObjDict.GetAt(XRecordKey), OpenMode.ForWrite);
        // Convert DateTime to AutoCAD date (number of days since December 30, 1899)
        double acadDate = (newCreationTime - new DateTime(1899, 12, 30)).TotalDays;
        // Update the Xrecord with new data
        xRec.Data = new ResultBuffer(
            new TypedValue((int)DxfCode.Text, newId),
            new TypedValue((int)DxfCode.Real, acadDate)
        );
      }
      else
      {
        // If the XRecord doesn't exist, create a new one
        CreateXRecordId(db, tr, newId);
      }
    }

    private void UpdateXRecordAfterDataLoad()
    {
      Document doc = Application.DocumentManager.MdiActiveDocument;
      using (DocumentLock docLock = doc.LockDocument())
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          try
          {
            UpdateXRecordId(tr, newDrawingId, newCreationTime);
            currentDrawingId = newDrawingId;
            ed.WriteMessage($"\nUpdated Drawing ID: {currentDrawingId}, New Creation Time: {newCreationTime}");
            tr.Commit();
          }
          catch (System.Exception ex)
          {
            ed.WriteMessage($"\nError updating XRecord after data load: {ex.Message}");
            tr.Abort();
          }
        }
      }
      needsXRecordUpdate = false;
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
        //var data = await MongoDBService.GetDrawingDataAsync(currentDrawingId);
        var data = await MariaDBService.GetWaterSystemData(ProjectId);
        if (data != null)
        {
          myControl.Dispatcher.Invoke(() =>
          {
            viewModel.UpdatePropertiesFromData(data);
          });

          if (needsXRecordUpdate)
          {
            UpdateXRecordAfterDataLoad();
          }

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
          //bool updateResult = await MongoDBService.UpdateDrawingDataAsync(data, currentDrawingId);
          bool updateResult = await MariaDBService.UpdateWaterSystem(data, ProjectId);
          if (updateResult)
          {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nSuccessfully updated drawing data in MongoDB.\n");
          }
          else
          {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nFailed to update drawing data in MongoDB. (possibly no data has changed since the last update)\n");
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