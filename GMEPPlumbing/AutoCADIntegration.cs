using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using GMEPPlumbing.Views;
using System;
using System.Collections;
using System.Windows.Forms.Integration;

[assembly: CommandClass(typeof(GMEPPlumbing.AutoCADIntegration))]

namespace GMEPPlumbing
{
  public class AutoCADIntegration
  {
    private const string XRecordKey = "GMEPPlumbingID";
    private PaletteSet pw;
    private UserInterface myControl;
    private string currentDrawingId;
    private WaterSystemViewModel viewModel;

    [CommandMethod("Water")]
    public void Water()
    {
      // Initialize MongoDB connection
      MongoDBService.Initialize();

      Document doc = Application.DocumentManager.MdiActiveDocument;
      Database db = doc.Database;
      Editor ed = doc.Editor;

      currentDrawingId = RetrieveOrCreateDrawingId(db, ed);

      InitializeUserInterface();
    }

    private string RetrieveOrCreateDrawingId(Database db, Editor ed)
    {
      string drawingId = null;

      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        try
        {
          // Try to retrieve existing XRecord
          drawingId = RetrieveXRecordId(db, tr);

          if (string.IsNullOrEmpty(drawingId))
          {
            // Generate new ID if not found
            drawingId = Guid.NewGuid().ToString();
            CreateXRecordId(db, tr, drawingId);
            ed.WriteMessage($"\nCreated new Drawing ID: {drawingId}");
          }
          else
          {
            ed.WriteMessage($"\nRetrieved existing Drawing ID: {drawingId}");
          }

          tr.Commit();
        }
        catch (System.Exception ex)
        {
          ed.WriteMessage($"\nError handling Drawing ID: {ex.Message}");
          tr.Abort();
        }
      }

      return drawingId;
    }

    private string RetrieveXRecordId(Database db, Transaction tr)
    {
      RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
      if (!regAppTable.Has(XRecordKey))
        return null;

      DBDictionary namedObjDict = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
      if (!namedObjDict.Contains(XRecordKey))
        return null;

      Xrecord xRec = (Xrecord)tr.GetObject(namedObjDict.GetAt(XRecordKey), OpenMode.ForRead);
      TypedValue[] values = xRec.Data.AsArray();
      return values.Length > 0 ? values[0].Value.ToString() : null;
    }

    private void CreateXRecordId(Database db, Transaction tr, string drawingId)
    {
      RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
      if (!regAppTable.Has(XRecordKey))
      {
        RegAppTableRecord regAppTableRecord = new RegAppTableRecord();
        regAppTableRecord.Name = XRecordKey;
        regAppTable.Add(regAppTableRecord);
        tr.AddNewlyCreatedDBObject(regAppTableRecord, true);
      }

      DBDictionary namedObjDict = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
      Xrecord xRec = new Xrecord();
      xRec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, drawingId));
      namedObjDict.SetAt(XRecordKey, xRec);
      tr.AddNewlyCreatedDBObject(xRec, true);
    }

    private void InitializeUserInterface()
    {
      viewModel = new WaterSystemViewModel(
          new WaterMeterLossCalculationService(),
          new WaterStaticLossService(),
          new WaterTotalLossService(),
          new WaterPressureAvailableService(),
          new WaterDevelopedLengthService(),
          new WaterRemainingPressurePer100FeetService(),
          new WaterAdditionalLosses(),
          new WaterAdditionalLosses(),
          currentDrawingId);

      myControl = new UserInterface(viewModel);
      var host = new ElementHost();
      host.Child = myControl;

      pw = new PaletteSet("GMEP Plumbing Water Calculator");
      pw.Style = PaletteSetStyles.ShowAutoHideButton |
                 PaletteSetStyles.ShowCloseButton |
                 PaletteSetStyles.ShowPropertiesMenu;
      pw.DockEnabled = DockSides.Left | DockSides.Right;

      pw.Size = new System.Drawing.Size(1280, 800);
      pw.MinimumSize = new System.Drawing.Size(1280, 800);
      pw.Add("MyTab", host);

      pw.Visible = true;
      pw.Dock = DockSides.Left;
      pw.RolledUp = false;

      // Add event handler for PaletteSet closing
      pw.StateChanged += Pw_StateChanged;
    }

    private async void Pw_StateChanged(object sender, PaletteSetStateEventArgs e)
    {
      if (e.NewState == StateEventIndex.Hide)
      {
        // PaletteSet is being closed
        try
        {
          WaterSystemData data = viewModel.GetWaterSystemData();
          bool updateResult = await MongoDBService.UpdateDrawingDataAsync(data);
          if (updateResult)
          {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nSuccessfully updated drawing data in MongoDB.");
          }
          else
          {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nFailed to update drawing data in MongoDB.");
          }
        }
        catch (System.Exception ex)
        {
          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nError updating drawing data: {ex.Message}");
        }
      }
    }
  }
}