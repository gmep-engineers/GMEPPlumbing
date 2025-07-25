﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using GMEPPlumbing;
using GMEPPlumbing.Commands;
using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using GMEPPlumbing.Views;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Misc;
using Org.BouncyCastle.Bcpg.OpenPgp;
using SharpCompress.Common;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using static MongoDB.Bson.Serialization.Serializers.SerializerHelper;

[assembly: CommandClass(typeof(GMEPPlumbing.AutoCADIntegration))]
[assembly: CommandClass(typeof(GMEPPlumbing.CADObjectCommands))]
[assembly: CommandClass(typeof(GMEPPlumbing.PlumbingCalculationMethods))]
[assembly: CommandClass(typeof(GMEPPlumbing.Commands.TableCommand))]
[assembly: ExtensionApplication(typeof(GMEPPlumbing.PluginEntry))]

namespace GMEPPlumbing
{
  public class AutoCADIntegration {
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
    public static bool IsSaving { get; private set; }
    public static bool SettingObjects { get; set; }

    public AutoCADIntegration() {
      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;
      SettingObjects = false;
      IsSaving = false;
    }

    public static void AttachHandlers(Document doc) {
      var db = doc.Database;
      var ed = doc.Editor;

      // Prevent multiple attachments

      db.BeginSave -= (s, e) => IsSaving = true;
      db.SaveComplete -= (s, e) => IsSaving = false;
      db.AbortSave -= (s, e) => IsSaving = false;

      db.BeginSave += (s, e) => IsSaving = true;
      db.SaveComplete += (s, e) => IsSaving = false;
      db.AbortSave += (s, e) => IsSaving = false;

      db.ObjectErased -= Db_VerticalRouteErased;
      db.ObjectErased += Db_VerticalRouteErased;
      db.ObjectModified -= Db_VerticalRouteModified;
      db.ObjectModified += Db_VerticalRouteModified;
      db.ObjectModified -= Db_BasePointModified;
      db.ObjectModified += Db_BasePointModified;
      db.SaveComplete -= Db_DocumentSaved;
      db.SaveComplete += Db_DocumentSaved;
      // ... attach other handlers as needed ...
    }

    [CommandMethod("PlumbingHorizontalRoute")]
    public async void PlumbingHorizontalRoute() {
      string BasePointId = CADObjectCommands.GetActiveView();
  

      List<string> routeGUIDS = new List<string>();
      string layer = "Defpoints";
  
      PromptKeywordOptions pko = new PromptKeywordOptions("\nSelect route type: ");
      if (CADObjectCommands.ActiveViewTypes.Contains("Water")) { 
        pko.Keywords.Add("HotWater");
        pko.Keywords.Add("ColdWater");
      }
      if (CADObjectCommands.ActiveViewTypes.Contains("Gas")) {
        pko.Keywords.Add("Gas");
      }
      if (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent")) {
        pko.Keywords.Add("Waste");
        pko.Keywords.Add("Vent");
      }
      //pko.Keywords.Add("Storm");
      PromptResult pr = ed.GetKeywords(pko);
      if (pr.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        return;
      }
      string result = pr.StringResult;


      switch (result) {
        case "HotWater":
          layer = "P-DOMW-HOTW";
          break;
        case "ColdWater":
          layer = "P-DOMW-CWTR";
          break;
        case "Gas":
          layer = "P-GAS";
          break;
        case "Waste":
          layer = "P-GREASE-WASTE";
          break;
        case "Vent":
          layer = "P-WV-VENT";
          break;
         /*case "Storm":
             layer = "GMEP_PLUMBING_STORM";
             break;*/
        default:
          ed.WriteMessage("\nInvalid route type selected.");
          return;
      }

      PromptKeywordOptions pko2 = new PromptKeywordOptions("\nForward or Backward?");
      pko2.Keywords.Add("Forward");
      pko2.Keywords.Add("Backward");
      PromptResult pr2 = ed.GetKeywords(pko2);
      if (pr2.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        return;
      }
      string direction = pr2.StringResult;

      //start of placement logic

      PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the horizontal route from the floor (in feet): ");
      pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();

      double routeHeight = 0;
      while (true) {
        try {
          PromptDoubleResult pdr = ed.GetDouble(pdo);
          if (pdr.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          if (pdr.Status != PromptStatus.OK) {
            ed.WriteMessage("\nInvalid input. Please enter a valid number.");
            continue;
          }

          routeHeight = pdr.Value;
          // GetHeightLimits returns Tuple<double, double> (min, max)
          var heightLimits = CADObjectCommands.GetHeightLimits(CADObjectCommands.GetActiveView());
          double minHeight = heightLimits.Item1;
          double maxHeight = heightLimits.Item2;

          if (routeHeight < minHeight || routeHeight > maxHeight) {
            ed.WriteMessage($"\nHeight must be between {minHeight} and {maxHeight} feet. Please enter a valid height.");
            pdo.Message = $"\nHeight must be between {minHeight} and {maxHeight} feet:";
            continue;
          }
          break; // Valid input
        }
        catch (System.Exception ex) {
          ed.WriteMessage($"\nError: {ex.Message}");
          continue;
        }
      }

      double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;

      //Beginning display
      var routeHeightDisplay = new RouteHeightDisplay(ed);
      routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);


      PromptPointOptions ppo2 = new PromptPointOptions("\nSpecify start point for route: ");
      ppo2.AllowNone = false;
      PromptPointResult ppr2 = ed.GetPoint(ppo2);
      if (ppr2.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        routeHeightDisplay.Disable();
        return;
      }

      Point3d startPointLocation2 = ppr2.Value;
      ObjectId addedLineId2 = ObjectId.Null;
      string LineGUID2 = Guid.NewGuid().ToString();

      PromptPointOptions ppo3 = new PromptPointOptions("\nSpecify next point for route: ");
      ppo3.BasePoint = startPointLocation2;
      ppo3.UseBasePoint = true;

      PromptPointResult ppr3 = ed.GetPoint(ppo3);
      if (ppr3.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        routeHeightDisplay.Disable();
        return;
      }

      Point3d endPointLocation2 = ppr3.Value;

      using (Transaction tr2 = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr2.GetObject(db.BlockTableId, OpenMode.ForWrite);
        BlockTableRecord btr = (BlockTableRecord)
          tr2.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

        Line line = new Line();
        line.StartPoint = new Point3d(startPointLocation2.X, startPointLocation2.Y, zIndex);
        line.EndPoint = new Point3d(endPointLocation2.X, endPointLocation2.Y, zIndex);

        line.Layer = layer;
        btr.AppendEntity(line);
        tr2.AddNewlyCreatedDBObject(line, true);
        addedLineId2 = line.ObjectId;
        tr2.Commit();
      }
      routeGUIDS.Add(LineGUID2);
      AttachRouteXData(addedLineId2, LineGUID2, BasePointId);
      AddArrowsToLine(addedLineId2, LineGUID2);

      while (true) {
        //Select a starting point/object
        PromptEntityOptions peo = new PromptEntityOptions("\nSelect a line");
        peo.SetRejectMessage("\nSelect a line");
        peo.AddAllowedClass(typeof(Line), true);
        PromptEntityResult per = ed.GetEntity(peo);

        if (per.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          routeHeightDisplay.Disable();
          return;
        }
        ObjectId basePointId = per.ObjectId;

        Point3d startPointLocation = Point3d.Origin;
        ObjectId addedLineId = ObjectId.Null;

        string LineGUID = Guid.NewGuid().ToString();

        // Check if the selected object is a BlockReference or Line
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          Entity basePoint = (Entity)tr.GetObject(basePointId, OpenMode.ForRead);

          //get line choice
          if (basePoint is Line basePointLine) {
            //retrieving the lines xdata
            ResultBuffer xData = basePointLine.GetXDataForApplication(XRecordKey);
            if (xData == null || xData.AsArray().Length < 2) {
              ed.WriteMessage("\nSelected line does not have the required XData.");
              return;
            }
            TypedValue[] values = xData.AsArray();
            string Id = values[1].Value as string;
            if (!routeGUIDS.Contains(Id)) {
              ed.WriteMessage("\nSelected line is not part of the active route.");
              continue;
            }

            //Placing Line
            LineStartPointPreviewJig jig = new LineStartPointPreviewJig(basePointLine);
            PromptResult jigResult = ed.Drag(jig);
            startPointLocation = jig.ProjectedPoint;
            layer = basePointLine.Layer;
          }

          PromptPointOptions ppo = new PromptPointOptions("\nSpecify next point for route: ");
          ppo.BasePoint = startPointLocation;
          ppo.UseBasePoint = true;

          Point3d endPointLocation3 = Point3d.Origin;

          while (true) {
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) {
              ed.WriteMessage("\nCommand cancelled.");
              routeHeightDisplay.Disable();
              return;
            }

            if (layer == "P-GAS" && basePoint is Line basePointLine2) {
              Vector3d prevDir = basePointLine2.EndPoint - basePointLine2.StartPoint;
              Vector3d newDir = ppr.Value - startPointLocation;
              double angle = prevDir.GetAngleTo(newDir);

              if (angle > Math.PI / 4) {
                ed.WriteMessage("\nAngle exceeds 45 degrees. Please pick a point closer to the previous direction.");
                ppo.Message = "\nNext Line must be 45 degrees or less";
                continue;
              }
            }

            endPointLocation3 = ppr.Value;
            break;
          }

          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
          BlockTableRecord btr = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

          Line line = new Line();
          if (direction == "Forward") {
            line.StartPoint = new Point3d(startPointLocation.X, startPointLocation.Y, zIndex);
            line.EndPoint = new Point3d(endPointLocation3.X, endPointLocation3.Y, zIndex);
          }
          else if (direction == "Backward") {
            line.StartPoint = new Point3d(endPointLocation3.X, endPointLocation3.Y, zIndex);
            line.EndPoint = new Point3d(startPointLocation.X, startPointLocation.Y, zIndex);
          }
      
          line.Layer = layer;
          btr.AppendEntity(line);
          tr.AddNewlyCreatedDBObject(line, true);
          addedLineId = line.ObjectId;

          //PropagateUpRouteInfo(tr, layer, LineGUID);

          tr.Commit();
        }
        routeGUIDS.Add(LineGUID);
        AttachRouteXData(addedLineId, LineGUID, BasePointId);
        AddArrowsToLine(addedLineId, LineGUID);
      }
      routeHeightDisplay.Disable();
    }

    [CommandMethod("PlumbingVerticalRoute")]
    public async void PlumbingVerticalRoute() {
      
      string basePointGUID = CADObjectCommands.GetActiveView();
    

      SettingObjects = true;
      string layer = "Defpoints";
      List<ObjectId> basePointIds = new List<ObjectId>();
      int startFloor = 0;
      Point3d StartBasePointLocation = new Point3d(0, 0, 0);
      Point3d StartUpLocation = new Point3d(0, 0, 0);
      ObjectId startPipeId = ObjectId.Null;
      string verticalRouteId = Guid.NewGuid().ToString();
      ObjectId gmepTextStyleId;
      string viewGUID = "";
      int typeId = 0;
      Dictionary<int, double> floorHeights = new Dictionary<int, double>();

      PromptKeywordOptions pko = new PromptKeywordOptions("\nSelect route type: ");
      if (CADObjectCommands.ActiveViewTypes.Contains("Water")) {
        pko.Keywords.Add("HotWater");
        pko.Keywords.Add("ColdWater");
      }
      if (CADObjectCommands.ActiveViewTypes.Contains("Gas")) {
        pko.Keywords.Add("Gas");
      }
      if (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent")) {
        pko.Keywords.Add("Waste");
        pko.Keywords.Add("Vent");
      }
      //pko.Keywords.Add("Storm");
      PromptResult pr = ed.GetKeywords(pko);
      if (pr.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        return;
      }
      string result = pr.StringResult;

      switch (result) {
        case "HotWater":
          layer = "P-DOMW-HOTW";
          break;
        case "ColdWater":
          layer = "P-DOMW-CWTR";
          break;
        case "Gas":
          layer = "P-GAS";
          break;
        case "Waste":
          layer = "P-GREASE-WASTE";
          break;
        case "Vent":
          layer = "P-WV-VENT";
          break;
        /*
       case "Storm":
           layer = "GMEP_PLUMBING_STORM";
           break;*/
        default:
          ed.WriteMessage("\nInvalid route type selected.");
          return;
      }

      PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the vertical route from the floor (in feet): ");
      pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();
      double routeHeight = 0;
      while (true) {
        try {
          PromptDoubleResult pdr = ed.GetDouble(pdo);
          if (pdr.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          if (pdr.Status != PromptStatus.OK) {
            ed.WriteMessage("\nInvalid input. Please enter a valid number.");
            continue;
          }
          routeHeight = pdr.Value;
          // GetHeightLimits returns Tuple<double, double> (min, max)
          var heightLimits = CADObjectCommands.GetHeightLimits(CADObjectCommands.GetActiveView());
          double minHeight = heightLimits.Item1;
          double maxHeight = heightLimits.Item2;
          if (routeHeight < minHeight || routeHeight > maxHeight) {
            ed.WriteMessage($"\nHeight must be between {minHeight} and {maxHeight} feet. Please enter a valid height.");
            pdo.Message = $"\nHeight must be between {minHeight} and {maxHeight} feet:";
            continue;
          }
          break; // Valid input
        }
        catch (System.Exception ex) {
          ed.WriteMessage($"\nError: {ex.Message}");
          continue;
        }
      }
      double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;

      //beginning display
      var routeHeightDisplay = new RouteHeightDisplay(ed);
      routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        //retrieving the view of the basepoint
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (id.IsValid) {
            using (
              BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord
            ) {
              if (anonymousBtr != null) {
                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                  var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                  var pc2 = entity.DynamicBlockReferencePropertyCollection;
                  bool match = false;
                  string tempViewGUID = "";
                  foreach (DynamicBlockReferenceProperty prop in pc2) {
                    if (prop.PropertyName == "view_id") {
                      tempViewGUID = prop.Value.ToString();
                    }
                    if (prop.PropertyName == "id") {
                      if (prop.Value.ToString() == basePointGUID) {
                        match = true;
                      }
                    }
                  }
                  if (match) {
                    viewGUID = tempViewGUID;
                    break;
                  }
                }
              }
            }
          }
        }
        tr.Commit();
      }

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)
          tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        Dictionary<string, List<ObjectId>> basePoints = new Dictionary<string, List<ObjectId>>();
        TextStyleTable textStyleTable = (TextStyleTable)
          tr.GetObject(doc.Database.TextStyleTableId, OpenMode.ForRead);
        if (textStyleTable.Has("gmep")) {
          gmepTextStyleId = textStyleTable["gmep"];
        }
        else {
          ed.WriteMessage("\nText style 'gmep' not found. Using default text style.");
          gmepTextStyleId = doc.Database.Textstyle;
        }
        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (id.IsValid) {
            using (
              BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord
            ) {
              if (anonymousBtr != null) {
                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                  var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                  var pc = entity.DynamicBlockReferencePropertyCollection;
                  foreach (DynamicBlockReferenceProperty prop in pc) {
                    if (prop.PropertyName == "view_id") {
                      string key = prop.Value.ToString();
                      if (key != "0") {
                        if (!basePoints.ContainsKey(key)) {
                          basePoints[key] = new List<ObjectId>();
                        }
                        basePoints[key].Add(entity.ObjectId);
                      }
                    }
                  }
                }
              }
            }
          }
        }
        ed.WriteMessage("\nFound " + basePoints.Count + " base points in the drawing.");

        basePointIds = basePoints[viewGUID];




        BlockReference firstFloorBasePoint = null;

        foreach (ObjectId objId in basePointIds) {
          var entity2 = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
          var pc2 = entity2.DynamicBlockReferencePropertyCollection;

          bool selectedPoint = false;
          int tempFloor = 0;
          double tempFloorHeight = 0;
          foreach (DynamicBlockReferenceProperty prop in pc2) {
            if (prop.PropertyName == "floor") {
              tempFloor = Convert.ToInt32(prop.Value);
            }
            if (prop.PropertyName == "id") {
              if (prop.Value.ToString() == basePointGUID) {
                selectedPoint = true;
              }
            }
            if (prop.PropertyName == "floor_height") {
              tempFloorHeight = Convert.ToDouble(prop.Value);
            }
          }
          if (tempFloor != 0) {
            if (!floorHeights.ContainsKey(tempFloor)) {
              floorHeights[tempFloor] = tempFloorHeight;
            }
          }
          if (selectedPoint) {
            startFloor = tempFloor;
            firstFloorBasePoint = entity2;
            StartBasePointLocation = entity2.Position;
          }

        }
        if (firstFloorBasePoint != null) {
          BlockTableRecord block = null;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            "GMEP_PLUMBING_LINE_VERTICAL",
            "Vertical Route",
            out block,
            out StartUpLocation
          );
          if (br != null) {
            br.Layer = layer;
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            curSpace.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            startPipeId = br.ObjectId;
          }
          else {
            ed.WriteMessage("\nFailed to create vertical route block reference.");
            routeHeightDisplay.Disable();
            return;
          }
        }

        tr.Commit();
      }
      //getting difference between start base point and up point
      Vector3d upVector = StartUpLocation - StartBasePointLocation;

      //picking end floor
      PromptKeywordOptions endFloorOptions = new PromptKeywordOptions("\nEnding Floor: ");
      for (int i = 1; i <= basePointIds.Count; i++) {
          endFloorOptions.Keywords.Add(i.ToString());
      }
      PromptResult endFloorResult = ed.GetKeywords(endFloorOptions);
      if (endFloorResult.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        routeHeightDisplay.Disable();
        return;
      }
      int endFloor = int.Parse(endFloorResult.StringResult);

      Dictionary<int, BlockReference> BasePointRefs = new Dictionary<int, BlockReference>();
      Dictionary<int, string> BasePointGUIDs = new Dictionary<int, string>();
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

        foreach (ObjectId objId in basePointIds) {
          var entity2 = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
          var pc2 = entity2.DynamicBlockReferencePropertyCollection;

          int floor = 0;
          string guid = "";
          foreach (DynamicBlockReferenceProperty prop in pc2) {
            if (prop.PropertyName == "floor") {
              floor = Convert.ToInt32(prop.Value);
              BasePointRefs.Add(floor, entity2);
            }
            if (prop.PropertyName == "id") {
              guid = prop.Value.ToString();
            }
          }
          if (floor != 0 && guid != "") {
            BasePointGUIDs.Add(floor, guid);
          }
        }
        tr.Commit();
      }
      routeHeightDisplay.Disable();

      if (endFloor > startFloor) {
        Point3d labelPoint = Point3d.Origin;
        Point3d labelPoint2 = Point3d.Origin;
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          //delete previous start pipe
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockReference startPipe = tr.GetObject(startPipeId, OpenMode.ForWrite) as BlockReference;
          startPipe.Erase(true);

          //start pipe
          Point3d newUpPointLocation2 = BasePointRefs[startFloor].Position + upVector;
          BlockTableRecord blockDef2 =
            tr.GetObject(bt["GMEP_PLUMBING_LINE_UP"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace2 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef2 = new BlockReference(newUpPointLocation2, blockDef2.ObjectId);
          RotateJig rotateJig = new RotateJig(upBlockRef2);
          PromptResult rotatePromptResult = ed.Drag(rotateJig);

          if (rotatePromptResult.Status != PromptStatus.OK) {
            return;
          }
          upBlockRef2.Position = new Point3d(newUpPointLocation2.X, newUpPointLocation2.Y, zIndex);
          labelPoint = upBlockRef2.Position;

          upBlockRef2.Layer = layer;
          curSpace2.AppendEntity(upBlockRef2);
          tr.AddNewlyCreatedDBObject(upBlockRef2, true);

          // Attach the vertical route ID to the start pipe
          var pc2 = upBlockRef2.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc2) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[startFloor];
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
            }
            if (prop.PropertyName == "length") {
              prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item2 - routeHeight;
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = routeHeight; 
            }
          }

          // Set the vertical route ID
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint, "UP TO UPPER");

        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          //Continue Pipe
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          for (int i = startFloor + 1; i < endFloor; i++) {
            Point3d newUpPointLocation = BasePointRefs[i].Position + upVector;
            BlockTableRecord blockDef =
              tr.GetObject(bt["GMEP_PLUMBING_LINE_VERTICAL"], OpenMode.ForRead) as BlockTableRecord;
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            // Create the BlockReference at the desired location
            BlockReference upBlockRef = new BlockReference(newUpPointLocation, blockDef.ObjectId);
            upBlockRef.Layer = layer;
            upBlockRef.Position = new Point3d(newUpPointLocation.X, newUpPointLocation.Y, floorHeights[i]*12);
            curSpace.AppendEntity(upBlockRef);
            tr.AddNewlyCreatedDBObject(upBlockRef, true);
            var pc2 = upBlockRef.DynamicBlockReferencePropertyCollection;

            foreach (DynamicBlockReferenceProperty prop in pc2) {
              if (prop.PropertyName == "id") {
                prop.Value = Guid.NewGuid().ToString();
              }
              if (prop.PropertyName == "base_point_id") {
                prop.Value = BasePointGUIDs[i];
              }
              if (prop.PropertyName == "vertical_route_id") {
                prop.Value = verticalRouteId;
              }
              if (prop.PropertyName == "length") {
                prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[i]).Item2;
              }
            }
          }

          //end pipe

          ZoomToBlock(ed, BasePointRefs[endFloor]);
          var promptDoubleOptions = new PromptDoubleOptions("\nEnter the height of the start of the vertical route from the floor (in feet): ");
          promptDoubleOptions.AllowNegative = false;
          promptDoubleOptions.AllowZero = false;
          promptDoubleOptions.DefaultValue = 0;
          double height = 0;

          while (true) {
            PromptDoubleResult promptDoubleResult = ed.GetDouble(promptDoubleOptions);
            if (promptDoubleResult.Status == PromptStatus.OK) {
              height = promptDoubleResult.Value;
              Tuple<double, double> heightLimits = CADObjectCommands.GetHeightLimits(BasePointGUIDs[endFloor]);
              double upperHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[endFloor]).Item2;
              double lowerHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[endFloor]).Item1;
              if (height > upperHeightLimit || height < lowerHeightLimit) {
                ed.WriteMessage($"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.");
                promptDoubleOptions.Message = $"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.";
                continue;
              }
              else if (promptDoubleResult.Status == PromptStatus.Cancel) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              else if (promptDoubleResult.Status == PromptStatus.Error) {
                ed.WriteMessage("\nError in input. Please try again.");
                continue;
              }
              break;
            }
          }


          Point3d newUpPointLocation3 = BasePointRefs[endFloor].Position + upVector;
          BlockTableRecord blockDef3 = tr.GetObject(bt["GMEP_PLUMBING_LINE_DOWN"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace3 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
          labelPoint2 = upBlockRef3.Position;
          RotateJig rotateJig2 = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
          if (rotatePromptResult2.Status != PromptStatus.OK) {
            return;
          }
          upBlockRef3.Position = new Point3d(newUpPointLocation3.X, newUpPointLocation3.Y, (floorHeights[endFloor] + height)*12);

          upBlockRef3.Layer = layer;
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc3) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[endFloor];
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
            }
            if (prop.PropertyName == "length") {
              prop.Value = height;
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = height;
            }
          }
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint2, "UP FROM LOWER");
      }
      else if (endFloor < startFloor) {
        Point3d labelPoint = Point3d.Origin;
        Point3d labelPoint2 = Point3d.Origin;
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          //delete previous start pipe
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockReference startPipe = tr.GetObject(startPipeId, OpenMode.ForWrite) as BlockReference;

          startPipe.Erase(true);

          //start pipe
          Point3d newUpPointLocation2 = BasePointRefs[startFloor].Position + upVector;
          BlockTableRecord blockDef2 =
            tr.GetObject(bt["GMEP_PLUMBING_LINE_DOWN"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace2 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef2 = new BlockReference(newUpPointLocation2, blockDef2.ObjectId);
          RotateJig rotateJig = new RotateJig(upBlockRef2);
          PromptResult rotatePromptResult = ed.Drag(rotateJig);
          if (rotatePromptResult.Status != PromptStatus.OK) {
            return;
          }
          upBlockRef2.Position = new Point3d(newUpPointLocation2.X, newUpPointLocation2.Y, zIndex);
          upBlockRef2.Layer = layer;
          curSpace2.AppendEntity(upBlockRef2);
          tr.AddNewlyCreatedDBObject(upBlockRef2, true);
          labelPoint = upBlockRef2.Position;

          var pc2 = upBlockRef2.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc2) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[startFloor];
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
            }
            if (prop.PropertyName == "length") {
              prop.Value = routeHeight;
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = routeHeight;
            }
          }
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint, "DOWN TO LOWER");

        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          //Continue Pipe
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          for (int i = startFloor - 1; i > endFloor; i--) {
            Point3d newUpPointLocation = BasePointRefs[i].Position + upVector;
            BlockTableRecord blockDef =
              tr.GetObject(bt["GMEP_PLUMBING_LINE_VERTICAL"], OpenMode.ForRead) as BlockTableRecord;
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            // Create the BlockReference at the desired location
            BlockReference upBlockRef = new BlockReference(newUpPointLocation, blockDef.ObjectId);
            upBlockRef.Layer = layer;
            upBlockRef.Position = new Point3d(newUpPointLocation.X, newUpPointLocation.Y, floorHeights[i]*12);
            curSpace.AppendEntity(upBlockRef);
            tr.AddNewlyCreatedDBObject(upBlockRef, true);
            var pc = upBlockRef.DynamicBlockReferencePropertyCollection;

            foreach (DynamicBlockReferenceProperty prop in pc) {
              if (prop.PropertyName == "id") {
                prop.Value = Guid.NewGuid().ToString();
              }
              if (prop.PropertyName == "base_point_id") {
                prop.Value = BasePointGUIDs[i];
              }
              if (prop.PropertyName == "vertical_route_id") {
                prop.Value = verticalRouteId;
              }
              if (prop.PropertyName == "length") {
                prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[i]).Item2;
              }
            }
          }

          //end pipe
          ZoomToBlock(ed, BasePointRefs[endFloor]);

          var promptDoubleOptions = new PromptDoubleOptions("\nEnter the height of the start of the vertical route from the floor (in feet): ");
          promptDoubleOptions.AllowNegative = false;
          promptDoubleOptions.AllowZero = false;
          promptDoubleOptions.DefaultValue = 0;
          double height = 0;

          while (true) {
            PromptDoubleResult promptDoubleResult = ed.GetDouble(promptDoubleOptions);
            if (promptDoubleResult.Status == PromptStatus.OK) {
              height = promptDoubleResult.Value;
              Tuple<double, double> heightLimits = CADObjectCommands.GetHeightLimits(BasePointGUIDs[endFloor]);
              double upperHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[endFloor]).Item2;
              double lowerHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[endFloor]).Item1;
              if (height > upperHeightLimit || height < lowerHeightLimit) {
                ed.WriteMessage($"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.");
                promptDoubleOptions.Message = $"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.";
                continue;
              }
              break;
            }
            else if (promptDoubleResult.Status == PromptStatus.Cancel) {
              ed.WriteMessage("\nCommand cancelled.");
              return;
            }
            else if (promptDoubleResult.Status == PromptStatus.Error) {
              ed.WriteMessage("\nError in input. Please try again.");
              continue;
            }
          }

          Point3d newUpPointLocation3 = BasePointRefs[endFloor].Position + upVector;
          BlockTableRecord blockDef3 =
            tr.GetObject(bt["GMEP_PLUMBING_LINE_UP"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace3 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
          labelPoint2 = upBlockRef3.Position;
          RotateJig rotateJig2 = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
          if (rotatePromptResult2.Status != PromptStatus.OK) {
            return;
          }
          upBlockRef3.Layer = layer;
          upBlockRef3.Position = new Point3d(newUpPointLocation3.X, newUpPointLocation3.Y, (floorHeights[endFloor] + height) * 12);
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc3) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[endFloor];
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
            }
            if (prop.PropertyName == "length") {
              prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[endFloor]).Item2 - height;
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = height;
            }
          }
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint2, "DOWN FROM UPPER");
      }
      else if (endFloor == startFloor) {
        PromptKeywordOptions pko3 = new PromptKeywordOptions("\nUp or Down?");
        pko3.Keywords.Add("Up");
        pko3.Keywords.Add("Down");

        PromptResult pr3 = ed.GetKeywords(pko3);
        if (pr3.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          routeHeightDisplay.Disable();
          return;
        }
        string blockName = "GMEP_PLUMBING_LINE_DOWN";
        string direction2 = pr3.StringResult;

        PromptDoubleOptions pdo2 = new PromptDoubleOptions(
          $"\nHow Far {direction2}(Ft)?"
        );
        pdo2.AllowNegative = false;
        pdo2.AllowZero = false;
        pdo2.DefaultValue = 3;
        double length = 0;
        while (true) {
          PromptDoubleResult pdr2 = ed.GetDouble(pdo2);
          if (pdr2.Status == PromptStatus.OK) {
            length = pdr2.Value;
            if (direction2 == "Up") {
              double heightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item2;
              double height = routeHeight;
              double limit = heightLimit - height;
              if (length > limit) {
                ed.WriteMessage($"\nFull height of fixture cannot exceed {heightLimit}. Current fixture height is {height}. Please enter a valid length.");
                pdo2.Message = $"\nFull height of fixture cannot exceed {heightLimit}. Current fixture height is {height}. Please enter a valid length.";
                continue;
              }
            }
            if (direction2 == "Down") {
              double lowerHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item1;
              double height = routeHeight;
              if (length > height - lowerHeightLimit) {
                ed.WriteMessage($"\nCurrent Height is {height} feet from the floor. Cannot go further. Please enter a valid length.");
                pdo2.Message = $"\nCurrent Height is {height} feet from the floor. Cannot go further. Please enter a valid length.";
                continue;
              }
            }
          }
          else if (pdr2.Status == PromptStatus.Error) {
            ed.WriteMessage("\nError in input. Please try again.");
            continue;
          }
          else if (pdr2.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          break;
        }

        Point3d labelPoint3 = Point3d.Origin;
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          //delete previous start pipe
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockReference startPipe = tr.GetObject(startPipeId, OpenMode.ForWrite) as BlockReference;

          startPipe.Erase(true);

          Point3d newUpPointLocation3 = BasePointRefs[startFloor].Position + upVector;
          BlockTableRecord blockDef3 =
            tr.GetObject(bt[blockName], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace3 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
          RotateJig rotateJig = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult = ed.Drag(rotateJig);
          if (rotatePromptResult.Status != PromptStatus.OK) {
            return;
          }
          if (direction2 == "Up") {
            zIndex += length * 12;
          }
          upBlockRef3.Position = new Point3d(newUpPointLocation3.X, newUpPointLocation3.Y, zIndex);
          upBlockRef3.Layer = layer;
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          labelPoint3 = upBlockRef3.Position;

          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc3) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[startFloor];
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
            }
            if (prop.PropertyName == "length") {
              prop.Value = length;
            }
            if (prop.PropertyName == "start_height") {
              if (direction2 == "Up") {
                prop.Value = routeHeight + length;
              }
              else if (direction2 == "Down") {
                prop.Value = routeHeight;
              }
            }
          }
          tr.Commit();
          
        }
        MakeVerticalRouteLabel(labelPoint3, direction2.ToUpper());
      }
      SettingObjects = false;
    }

    [CommandMethod("SETPLUMBINGBASEPOINT")]
    public async void SetPlumbingBasePoint() {
      SettingObjects = true;
      var prompt = new Views.BasePointPromptWindow();
      bool? result = prompt.ShowDialog();
      double currentFloorHeight = -10;
      double currentCeilingHeight = -1;
      double currentRouteHeight = 3;
      if (result != true) {
        ed.WriteMessage("\nOperation cancelled.");
        return;
      }
      bool water = prompt.Water;
      bool gas = prompt.Gas;
      bool sewerVent = prompt.SewerVent;
      bool storm = false;
      string planName = prompt.PlanName.ToUpper();
      string floorQtyResult = prompt.FloorQty;
      string ViewId = Guid.NewGuid().ToString();

      string viewport = "";
      if (water)
        viewport += "Water";
      if (viewport != "" && gas)
        viewport += "-";
      if (gas)
        viewport += "Gas";
      if (viewport != "" && sewerVent)
        viewport += "-";
      if (sewerVent)
        viewport += "Sewer-Vent";
      if (viewport != "" && storm)
        viewport += "-";
      if (storm)
        viewport += "Storm";

      if (!int.TryParse(floorQtyResult, out int floorQty)) {
        ed.WriteMessage("\nInvalid floor quantity. Please enter a valid integer.");
        return;
      }

      for (int i = 0; i < floorQty; i++) {

        PromptDoubleOptions heightOptions = new PromptDoubleOptions(
             $"\nEnter the height from ground level for floor {i + 1} on plan {planName}:"
         );
        heightOptions.AllowNegative = false;
        heightOptions.AllowZero = false;
        heightOptions.DefaultValue = currentCeilingHeight + 1;

        while (true) {
          PromptDoubleResult heightResult = ed.GetDouble(heightOptions);

          if (heightResult.Status == PromptStatus.OK) {
            double tempFloorHeight = heightResult.Value;
            if (tempFloorHeight <= currentCeilingHeight) {
              heightOptions.Message = $"\nHeight must be greater than the previous ceiling height ({currentCeilingHeight}). Please enter a valid height.";
              continue;
            }
            currentFloorHeight = heightResult.Value;
            break;
          }
          else if (heightResult.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nOperation cancelled.");
            return;
          }
          else {
            ed.WriteMessage("\nInvalid input. Please enter a positive, non-zero number.");
          }

        }

        PromptDoubleOptions ceilingHeightOptions = new PromptDoubleOptions(
           $"\nEnter the height of the ceiling from ground level for floor {i + 1} on plan {planName}:"
        );
        ceilingHeightOptions.AllowNegative = false;
        ceilingHeightOptions.AllowZero = false;
        ceilingHeightOptions.DefaultValue = currentFloorHeight + 10;

        while (true) {
          PromptDoubleResult ceilingHeightResult = ed.GetDouble(ceilingHeightOptions);
          if (ceilingHeightResult.Status == PromptStatus.OK) {
            double tempCeilingHeight = ceilingHeightResult.Value;
            if (tempCeilingHeight <= currentFloorHeight) {
              ceilingHeightOptions.Message = $"\nCeiling height must be greater than the floor height ({currentFloorHeight}). Please enter a valid height.";
              continue;
            }
            currentCeilingHeight = ceilingHeightResult.Value;
            break;
          }
          else if (ceilingHeightResult.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nOperation cancelled.");
            return;
          }
          else {
            ed.WriteMessage("\nInvalid input. Please enter a positive, non-zero number.");
          }
        }

        PromptDoubleOptions routeHeightOptions = new PromptDoubleOptions(
             $"\nEnter the route height from floor {i + 1} on plan {planName}:"
         );
        routeHeightOptions.AllowNegative = false;
        routeHeightOptions.AllowZero = false;
        routeHeightOptions.DefaultValue = 3;

        while (true) {
          PromptDoubleResult routeHeightResult = ed.GetDouble(routeHeightOptions);

          if (routeHeightResult.Status == PromptStatus.OK) {
            double tempRouteHeight = routeHeightResult.Value;
            if (tempRouteHeight < 0) {
              heightOptions.Message = $"\nRoute Height must be greater than zero.";
              continue;
            }
            currentRouteHeight = routeHeightResult.Value;
            break;
          }
          else if (routeHeightResult.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nOperation cancelled.");
            return;
          }
          else {
            ed.WriteMessage("\nInvalid input. Please enter a positive, non-zero number.");
          }
        }

        Point3d point;
        ObjectId blockId;
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord curSpace = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

          BlockTableRecord block;
          //string message = "\nCreating Plumbing Base Point for " + planName + " on floor " + (i + 1);
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            "GMEP_PLUMBING_BASEPOINT",
            "Plumbing Base Point for " + planName + " on floor " + (i + 1),
            out block,
            out point
          );
          br.Layer = "Defpoints";
          if (br != null) {
            curSpace.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            blockId = br.ObjectId;

            DynamicBlockReferencePropertyCollection properties =
              br.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty prop in properties) {
              if (prop.PropertyName == "plan") {
                prop.Value = planName;
              }
              else if (prop.PropertyName == "floor") {
                prop.Value = i + 1;
              }
              else if (prop.PropertyName == "type") {
                prop.Value = viewport;
              }
              else if (prop.PropertyName == "view_id") {
                prop.Value = ViewId;
              }
              else if (prop.PropertyName == "id") {
                prop.Value = Guid.NewGuid().ToString();
              }
              else if (prop.PropertyName == "pos_x") {
                prop.Value = point.X;
              }
              else if (prop.PropertyName == "pos_y") {
                prop.Value = point.Y;
              }
              else if (prop.PropertyName == "floor_height") {
                prop.Value = currentFloorHeight;
              }
              else if (prop.PropertyName == "route_height") {
                prop.Value = currentRouteHeight;
              }
              else if (prop.PropertyName == "ceiling_height") {
                prop.Value = currentCeilingHeight;
              }
            }
          }
          tr.Commit();
        }
      }
      SettingObjects = false;
    }


    [CommandMethod("Water")]
    public async void Water() {
      //MongoDBService.Initialize();
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      ProjectId = await MariaDBService.GetProjectId(projectNo);

      RetrieveOrCreateDrawingId();
      InitializeUserInterface();
      LoadDataAsync();

      pw.Focus();
    }

    public static void ZoomToBlock(Editor ed, BlockReference blockRef) {
      Extents3d ext = blockRef.GeometricExtents;
      using (ViewTableRecord view = ed.GetCurrentView()) {
        double halfWidth = view.Width / 2.0;
        double halfHeight = view.Height / 2.0;
        double minX = view.CenterPoint.X - halfWidth;
        double maxX = view.CenterPoint.X + halfWidth;
        double minY = view.CenterPoint.Y - halfHeight;
        double maxY = view.CenterPoint.Y + halfHeight;

        if (
            ext.MinPoint.X >= minX && ext.MaxPoint.X <= maxX &&
            ext.MinPoint.Y >= minY && ext.MaxPoint.Y <= maxY
        ) {
          return;
        }
        view.CenterPoint = new Point2d(
          (ext.MinPoint.X + ext.MaxPoint.X) / 2,
          (ext.MinPoint.Y + ext.MaxPoint.Y) / 2
        );
        view.Height = ext.MaxPoint.Y * 50 - ext.MinPoint.Y * 50;
        view.Width = ext.MaxPoint.X * 50 - ext.MinPoint.X * 50;
        ed.SetCurrentView(view);
      }
    }

    public void WriteMessage(string message) {
      ed.WriteMessage(message);
    }

    private void AddArrowsToLine(ObjectId lineId, string lineGUID) {
      while (true) {
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          Line line = (Line)tr.GetObject(lineId, OpenMode.ForWrite);
          double arrowLength = 5.0;
          double arrowSize = 3.0;
          string blockName = "GMEP_PLUMBING_LINE_ARROW";

          Vector3d dir = (line.EndPoint - line.StartPoint).GetNormal();
          double angle = dir.AngleOnPlane(new Plane(Point3d.Origin, Vector3d.ZAxis));
          if (line.Layer == "meow")
            angle += Math.PI;

          // Get the BlockTable and BlockTableRecord
          BlockTable bt = (BlockTable)tr.GetObject(line.Database.BlockTableId, OpenMode.ForRead);
          if (!bt.Has(blockName)) {
            ed.WriteMessage($"\nBlock '{blockName}' not found in drawing.");
            return;
          }
          ObjectId blockDefId = bt[blockName];
          BlockTableRecord btr = (BlockTableRecord)tr.GetObject(line.OwnerId, OpenMode.ForWrite);

          LineArrowJig lineArrowJig = new LineArrowJig(line, blockDefId, 1, angle);
          PromptResult jigResult = ed.Drag(lineArrowJig);
          // Break if user presses Escape or Enter
          if (jigResult.Status != PromptStatus.OK)
            break;
          Point3d arrowPos = lineArrowJig.InsertionPoint;
          BlockReference arrowRef = new BlockReference(arrowPos, blockDefId) {
            Rotation = angle,
            Layer = line.Layer,
          };
          btr.AppendEntity(arrowRef);
          tr.AddNewlyCreatedDBObject(arrowRef, true);
          DynamicBlockReferencePropertyCollection properties =
            arrowRef.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in properties) {
            if (prop.PropertyName == "line_id") {
              prop.Value = lineGUID;
            }
          }
          tr.Commit();
        }
      }
    }

    public void RetrieveOrCreateDrawingId() {
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        try {
          DateTime creationTime = RetrieveXRecordId(db, tr);

          if (string.IsNullOrEmpty(currentDrawingId)) {
            currentDrawingId = Guid.NewGuid().ToString();
            creationTime = GetFileCreationTime();
            CreateXRecordId(db, tr, currentDrawingId);
            ed.WriteMessage(
              $"\nCreated new Drawing ID: {currentDrawingId}, Creation Time: {creationTime}"
            );
          }
          else {
            ed.WriteMessage(
              $"\nRetrieved existing Drawing ID: {currentDrawingId}, Creation Time: {creationTime}"
            );
            var newCreationTime = GetFileCreationTime();
            ed.WriteMessage($"\nNew Creation Time: {newCreationTime}");

            if (Math.Abs((newCreationTime - creationTime).TotalSeconds) > 1) {
              needsXRecordUpdate = true;
              this.newDrawingId = Guid.NewGuid().ToString();
              this.newCreationTime = newCreationTime;
              ed.WriteMessage($"\nXRecord update needed. Will update after data load.");
              ed.WriteMessage($"\nOld Creation Time: {creationTime}");
              ed.WriteMessage($"\nNew Creation Time: {newCreationTime}");
            }
            else {
              ed.WriteMessage("\nCreation time has not changed. No update needed.");
            }
          }

          tr.Commit();
        }
        catch (System.Exception ex) {
          ed.WriteMessage($"\nError handling Drawing ID: {ex.Message}");
          tr.Abort();
        }
      }
    }

    private void AttachRouteXData(ObjectId lineId, string id, string basePointId) {
      ed.WriteMessage("Id: " + id + " basePointId: " + basePointId);
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        Line line = (Line)tr.GetObject(lineId, OpenMode.ForWrite);
        if (line == null)
          return;

        RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
        if (!regAppTable.Has(XRecordKey)) {
          RegAppTableRecord regAppTableRecord = new RegAppTableRecord { Name = XRecordKey };
          regAppTable.Add(regAppTableRecord);
          tr.AddNewlyCreatedDBObject(regAppTableRecord, true);
        }
        ResultBuffer rb = new ResultBuffer(
          new TypedValue((int)DxfCode.ExtendedDataRegAppName, XRecordKey),
          new TypedValue(1000, id),
          new TypedValue(1000, basePointId)
        );
        line.XData = rb;
        rb.Dispose();
        tr.Commit();
      }
    }

    private void UpdateXRecordId(Transaction tr, string newId, DateTime newCreationTime) {
      DBDictionary namedObjDict = (DBDictionary)
        tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
      if (namedObjDict.Contains(XRecordKey)) {
        Xrecord xRec = (Xrecord)tr.GetObject(namedObjDict.GetAt(XRecordKey), OpenMode.ForWrite);
        // Convert DateTime to AutoCAD date (number of days since December 30, 1899)
        double acadDate = (newCreationTime - new DateTime(1899, 12, 30)).TotalDays;
        // Update the Xrecord with new data
        xRec.Data = new ResultBuffer(
          new TypedValue((int)DxfCode.Text, newId),
          new TypedValue((int)DxfCode.Real, acadDate)
        );
      }
      else {
        // If the XRecord doesn't exist, create a new one
        CreateXRecordId(db, tr, newId);
      }
    }

    private void UpdateXRecordAfterDataLoad() {
      using (DocumentLock docLock = doc.LockDocument()) {
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          try {
            UpdateXRecordId(tr, newDrawingId, newCreationTime);
            currentDrawingId = newDrawingId;
            ed.WriteMessage(
              $"\nUpdated Drawing ID: {currentDrawingId}, New Creation Time: {newCreationTime}"
            );
            tr.Commit();
          }
          catch (System.Exception ex) {
            ed.WriteMessage($"\nError updating XRecord after data load: {ex.Message}");
            tr.Abort();
          }
        }
      }
      needsXRecordUpdate = false;
    }

    public DateTime RetrieveXRecordId(Database db, Transaction tr) {
      RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
      if (!regAppTable.Has(XRecordKey))
        return DateTime.MinValue;

      DBDictionary namedObjDict = (DBDictionary)
        tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
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

    public void CreateXRecordId(Database db, Transaction tr, string drawingId) {
      RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
      if (!regAppTable.Has(XRecordKey)) {
        RegAppTableRecord regAppTableRecord = new RegAppTableRecord { Name = XRecordKey };
        regAppTable.Add(regAppTableRecord);
        tr.AddNewlyCreatedDBObject(regAppTableRecord, true);
      }

      DBDictionary namedObjDict = (DBDictionary)
        tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
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

    private void InitializeUserInterface() {
      // Create the viewModel & get the data off mongoDB
      viewModel = new WaterSystemViewModel(
        new WaterMeterLossCalculationService(),
        new WaterStaticLossService(),
        new WaterTotalLossService(),
        new WaterPressureAvailableService(),
        new WaterDevelopedLengthService(),
        new WaterRemainingPressurePer100FeetService(),
        new WaterAdditionalLosses(),
        new WaterAdditionalLosses()
      );

      myControl = new UserInterface(viewModel);
      var host = new ElementHost();
      host.Child = myControl;

      pw = new PaletteSet("GMEP Plumbing Water Calculator");
      pw.Style =
        PaletteSetStyles.ShowAutoHideButton
        | PaletteSetStyles.ShowCloseButton
        | PaletteSetStyles.ShowPropertiesMenu;
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

    private async void LoadDataAsync() {
      try {
        //var data = await MongoDBService.GetDrawingDataAsync(currentDrawingId);
        var data = await MariaDBService.GetWaterSystemData(ProjectId);
        if (data != null) {
          myControl.Dispatcher.Invoke(() => {
            viewModel.UpdatePropertiesFromData(data);
          });

          if (needsXRecordUpdate) {
            UpdateXRecordAfterDataLoad();
          }

          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
            "\nSuccessfully loaded data from MongoDB.\n"
          );
        }
      }
      catch (System.Exception ex) {
        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
          $"\nError loading data from MongoDB: {ex.Message}\n"
        );
      }
    }

    private async void Pw_StateChanged(object sender, PaletteSetStateEventArgs e) {
      if (e.NewState == StateEventIndex.Hide) {
        try {
          WaterSystemData data = viewModel.GetWaterSystemData();
          //bool updateResult = await MongoDBService.UpdateDrawingDataAsync(data, currentDrawingId);
          bool updateResult = await MariaDBService.UpdateWaterSystem(data, ProjectId);
          if (updateResult) {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
              "\nSuccessfully updated drawing data in MongoDB.\n"
            );
          }
          else {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
              "\nFailed to update drawing data in MongoDB. (possibly no data has changed since the last update)\n"
            );
          }
        }
        catch (System.Exception ex) {
          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
            $"\nError updating drawing data: {ex.Message}\n"
          );
        }
      }
    }

    private DateTime GetFileCreationTime() {
      if (doc != null && !string.IsNullOrEmpty(doc.Name)) {
        FileInfo fileInfo = new FileInfo(doc.Name);
        return fileInfo.CreationTime.ToUniversalTime();
      }
      else {
        return DateTime.UtcNow;
      }
    }

    public void MakeVerticalRouteLabel(Point3d dnPoint, string direction) {
      CADObjectCommands.CreateArrowJig("D0", dnPoint);
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        direction
      );
    }

    private void MakePlumbingFixtureLabel(PlumbingFixture fixture, PlumbingFixtureType type) {
      double distance = 3;
      double x = fixture.Position.X + (distance * Math.Sin(fixture.Rotation));
      double y = fixture.Position.Y - (distance * Math.Cos(fixture.Rotation));
      Point3d dnPoint = new Point3d(x, y, 0);
      if (fixture.TypeAbbreviation == "VE") {
        CADObjectCommands.CreateTextWithJig(
          CADObjectCommands.TextLayer,
          TextHorizontalMode.TextLeft,
          "EXIT THROUGH ROOF"
        );
        return;
      }

      if (fixture.TypeAbbreviation != "CO") {
        CADObjectCommands.CreateTextWithJig(
          CADObjectCommands.TextLayer,
          TextHorizontalMode.TextLeft,
          fixture.TypeAbbreviation + "-" + fixture.Number.ToString()
        );
      }
      else {
        string typeAbb = "";
        switch (fixture.BlockName) {
          case "GMEP CO STRAIGHT":
            typeAbb = "2\" WCO";
            break;
          case "GMEP CO ANGLED":
            typeAbb = "2\" WCO";
            break;
          case "GMEP CO FLOOR":
            typeAbb = "2\" GCO";
            break;

        }
        CADObjectCommands.CreateTextWithJig(
         CADObjectCommands.TextLayer,
         TextHorizontalMode.TextLeft,
         typeAbb
       );
      }
    }

    private void MakePlumbingSourceLabel(PlumbingSource source, PlumbingSourceType type) {
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        type.Type.ToUpper()
      );
    }

    [CommandMethod("PF")]
    [CommandMethod("PlumbingFixture")]
    public void PlumbingFixture() {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);
    
      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;

      string basePointId = CADObjectCommands.GetActiveView();

      List<PlumbingFixtureType> plumbingFixtureTypes = MariaDBService.GetPlumbingFixtureTypes();
      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
      keywordOptions.Message = "\nSelect fixture type:";

      plumbingFixtureTypes.ForEach(t => {
        keywordOptions.Keywords.Add(t.Abbreviation + " - " + t.Name);
      });
      keywordOptions.Keywords.Default = "WC - Water Closet";
      keywordOptions.AllowNone = false;
      PromptResult keywordResult = ed.GetKeywords(keywordOptions);

      if (keywordResult.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        return;
      }
      string keywordResultString = keywordResult.StringResult;
      PlumbingFixtureType selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t =>
        keywordResultString.StartsWith(t.Abbreviation)
      );
      if (selectedFixtureType == null) {
        selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t => t.Abbreviation == "WC");
      }

      PlumbingFixtureCatalogItem selectedCatalogItem = null;
      if (selectedFixtureType.Abbreviation != "CO" && selectedFixtureType.Abbreviation != "VE") {
        List<PlumbingFixtureCatalogItem> plumbingFixtureCatalogItems =
          MariaDBService.GetPlumbingFixtureCatalogItemsByType(selectedFixtureType.Id);

        keywordOptions = new PromptKeywordOptions("");
        keywordOptions.Message = "\nSelect catalog item:";
        plumbingFixtureCatalogItems.ForEach(i => {
          keywordOptions.Keywords.Add(
            i.Id.ToString() + " - " + i.Description + " - " + i.Make + " " + i.Model
          );
        });

        keywordOptions.Keywords.Default =
          plumbingFixtureCatalogItems[0].Id.ToString()
          + " - "
          + plumbingFixtureCatalogItems[0].Description
          + " - "
          + plumbingFixtureCatalogItems[0].Make
          + " "
          + plumbingFixtureCatalogItems[0].Model;
        keywordResult = ed.GetKeywords(keywordOptions);

        keywordResultString = keywordResult.StringResult;
        if (keywordResultString.Contains(' ')) {
          keywordResultString = keywordResultString.Split(' ')[0];
        }
        selectedCatalogItem = plumbingFixtureCatalogItems.FirstOrDefault(
          i => i.Id.ToString() == keywordResultString
        );
      }

      if (selectedFixtureType.BlockName.Contains("%WHSIZE%")) {
        if (selectedFixtureType.Abbreviation == "WH") {
          keywordOptions = new PromptKeywordOptions("");
          keywordOptions.Message = "\nSelect WH size";
          keywordOptions.Keywords.Add("50 gal.");
          keywordOptions.Keywords.Add("80 gal.");
          keywordOptions.Keywords.Default = "50 gal.";
          keywordOptions.AllowNone = false;
          keywordResult = ed.GetKeywords(keywordOptions);
          if (keywordResult.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          string whSize = keywordResult.StringResult;
          if (whSize.Contains(' ')) {
            whSize = whSize.Split(' ')[0];
          }
          selectedFixtureType.BlockName = selectedFixtureType.BlockName.Replace(
            "%WHSIZE%",
            whSize
          );
        }
      }

      if (selectedFixtureType.BlockName.Contains("%FSSIZE%")) {
        if (selectedFixtureType.Abbreviation == "FS") {
          keywordOptions = new PromptKeywordOptions("");
          keywordOptions.Message = "\nSelect FS size";
          keywordOptions.Keywords.Add("12\"");
          keywordOptions.Keywords.Add("6\"");
          keywordOptions.Keywords.Default = "12\"";
          keywordOptions.AllowNone = false;
          keywordResult = ed.GetKeywords(keywordOptions);
          if (keywordResult.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          string fsSize = keywordResult.StringResult.Replace("\"", "");
          if (fsSize.Contains(' ')) {
            fsSize = fsSize.Split(' ')[0];
          }
          selectedFixtureType.BlockName = selectedFixtureType.BlockName.Replace(
            "%FSSIZE%",
            fsSize
          );
        }
      }
      if (selectedFixtureType.BlockName.Contains("%COSTYLE%")) {
        if (selectedFixtureType.Abbreviation == "CO") {
          // Prompt for WCO style
          keywordOptions = new PromptKeywordOptions("");
          keywordOptions.Message = "\nSelect CO style";
          keywordOptions.Keywords.Add("STRAIGHT");
          keywordOptions.Keywords.Add("ANGLED");
          keywordOptions.Keywords.Add("FLOOR");
          keywordOptions.Keywords.Default = "STRAIGHT";
          keywordOptions.AllowNone = false;
          keywordResult = ed.GetKeywords(keywordOptions);
          if (keywordResult.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          string coStyle = keywordResult.StringResult.Replace("\"", "");
          if (coStyle.Contains(' ')) {
            coStyle = coStyle.Split(' ')[0];
          }
          string blockName = selectedFixtureType.BlockName;
          selectedFixtureType.BlockName = selectedFixtureType.BlockName.Replace(
            "%COSTYLE%",
            coStyle
          );
        }
      }
      PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the vertical route from the floor (in feet): ");
      pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();
      double routeHeight = 0;
      while (true) {
        try {
          PromptDoubleResult pdr = ed.GetDouble(pdo);
          if (pdr.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          if (pdr.Status != PromptStatus.OK) {
            ed.WriteMessage("\nInvalid input. Please enter a valid number.");
            continue;
          }
          routeHeight = pdr.Value;
          // GetHeightLimits returns Tuple<double, double> (min, max)
          var heightLimits = CADObjectCommands.GetHeightLimits(CADObjectCommands.GetActiveView());
          double minHeight = heightLimits.Item1;
          double maxHeight = heightLimits.Item2;
          if (routeHeight < minHeight || routeHeight > maxHeight) {
            ed.WriteMessage($"\nHeight must be between {minHeight} and {maxHeight} feet. Please enter a valid height.");
            pdo.Message = $"\nHeight must be between {minHeight} and {maxHeight} feet:";
            continue;
          }
          break; // Valid input
        }
        catch (System.Exception ex) {
          ed.WriteMessage($"\nError: {ex.Message}");
          continue;
        }
      }
      PlumbingFixture plumbingFixture = null;
      double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;

      var routeHeightDisplay = new RouteHeightDisplay(ed);
      routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);

      if (!String.IsNullOrEmpty(selectedFixtureType.BlockName)) {
        List<string> blockNames = new List<string>();
        blockNames.Add(selectedFixtureType.BlockName);
        if (selectedCatalogItem != null && (selectedCatalogItem.Id == 2 || selectedCatalogItem.Id == 3 || selectedCatalogItem.Id == 25)) {
          blockNames.Add("GMEP PLUMBING GAS OUTPUT");
        }
        foreach (string blockName in blockNames) {
          // ed.WriteMessage("\nSelect base point for " + selectedFixtureType.Name);
          ObjectId blockId;
          //string blockName = selectedFixtureType.BlockName;
          Point3d point;
          double rotation = 0;
          int number = 0;
          string GUID = Guid.NewGuid().ToString();
          try {
            using (Transaction tr = db.TransactionManager.StartTransaction()) {
              BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
              BlockTableRecord btr;
              BlockReference br = CADObjectCommands.CreateBlockReference(
                tr,
                bt,
                blockName,
                "Plumbing Fixture " + selectedFixtureType.Name,
                out btr,
                out point
              );
              if (br != null) {
                BlockTableRecord curSpace = (BlockTableRecord)
                  tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                RotateJig rotateJig = new RotateJig(br);
                PromptResult rotatePromptResult = ed.Drag(rotateJig);

                if (rotatePromptResult.Status != PromptStatus.OK) {
                  ed.WriteMessage("\nRotation cancelled.");
                  routeHeightDisplay.Disable();
                  return;
                }
                br.Position = new Point3d(br.Position.X, br.Position.Y, zIndex);
                rotation = br.Rotation;

                curSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
              }
              else {
                ed.WriteMessage("\nBlock reference could not be created.");
                routeHeightDisplay.Disable();
                return;
              }

              blockId = br.Id;
              tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction()) {
              BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
              var modelSpace = (BlockTableRecord)
                tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
              BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
              DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
              foreach (DynamicBlockReferenceProperty prop in pc) {
                if (prop.PropertyName == "id") {
                  prop.Value = GUID;
                }
                if (prop.PropertyName == "gmep_plumbing_fixture_demand" && selectedCatalogItem != null) {
                  prop.Value = (double)selectedCatalogItem.FixtureDemand;
                }
                if (prop.PropertyName == "gmep_plumbing_fixture_hot_demand" && selectedCatalogItem != null) {
                  prop.Value = (double)selectedCatalogItem.HotDemand;
                }
                if (prop.PropertyName == "base_point_id") {
                  prop.Value = basePointId;
                }
                if (prop.PropertyName == "type_abbreviation") {
                  prop.Value = selectedFixtureType.Abbreviation;
                }
                if (prop.PropertyName == "catalog_id" && selectedCatalogItem != null) {
                  prop.Value = selectedCatalogItem.Id;
                }
              }
              int catalogId = selectedCatalogItem != null ? selectedCatalogItem.Id : 0;
              PlumbingFixture fixture = new PlumbingFixture(
                GUID,
                projectId,
                point,
                rotation,
                catalogId,
                selectedFixtureType.Abbreviation,
                0,
                basePointId,
                blockName
              );
              foreach (DynamicBlockReferenceProperty prop in pc) {
                if (prop.PropertyName == "number") {
                  number = DetermineFixtureNumber(fixture);
                  prop.Value = number;
                }
              }
              tr.Commit();
            }
            int catalogId2 = selectedCatalogItem != null ? selectedCatalogItem.Id : 0;
            plumbingFixture = new PlumbingFixture(
              GUID,
              projectId,
              point,
              rotation,
              catalogId2,
              selectedFixtureType.Abbreviation,
              number,
              basePointId,
              blockName
            );

            
          }
          catch (System.Exception ex) {
            ed.WriteMessage(ex.ToString());
            routeHeightDisplay.Disable();
            Console.WriteLine(ex.ToString());
          }
        }
        MakePlumbingFixtureLabel(plumbingFixture, selectedFixtureType);
      }
      routeHeightDisplay.Disable();
    }

    [CommandMethod("PlumbingSource")]
    public void CreatePlumbingSource() {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);

      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;

      string basePointGUID = CADObjectCommands.GetActiveView();

      List<PlumbingSourceType> plumbingSourceTypes = MariaDBService.GetPlumbingSourceTypes();
      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");

      keywordOptions.Message = "\nSelect fixture type:";

      plumbingSourceTypes.ForEach(t => {
        keywordOptions.Keywords.Add(t.Id.ToString() + " " + t.Type);
      });
      keywordOptions.Keywords.Default = "1 Water Meter";
      keywordOptions.AllowNone = false;
      PromptResult keywordResult = ed.GetKeywords(keywordOptions);
      if (keywordResult.Status != PromptStatus.OK) {
        ed.WriteMessage("\nOperation cancelled.");
        return;
      }

      string keywordResultString = keywordResult.StringResult;

      PlumbingSourceType selectedSourceType = plumbingSourceTypes.FirstOrDefault(t =>
        keywordResultString == t.Id.ToString()
      );
      if (selectedSourceType == null) {
        selectedSourceType = plumbingSourceTypes.FirstOrDefault(t => t.Type == "Water Meter");
      }

      if (selectedSourceType.Type == "Water Heater") {
        ed.Command("PlumbingFixture", "WH");
        return;
      }
      if (selectedSourceType.Type == "Insta-hot Water Heater") {
        ed.Command("PlumbingFixture", "IWH");
        return;
      }

      PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the source from the floor (in feet): ");
      pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();

      double routeHeight = 0;
      while (true) {
        try {
          PromptDoubleResult pdr = ed.GetDouble(pdo);
          if (pdr.Status == PromptStatus.Cancel) {
            ed.WriteMessage("\nCommand cancelled.");
            return;
          }
          if (pdr.Status != PromptStatus.OK) {
            ed.WriteMessage("\nInvalid input. Please enter a valid number.");
            continue;
          }

          routeHeight = pdr.Value;
          // GetHeightLimits returns Tuple<double, double> (min, max)
          var heightLimits = CADObjectCommands.GetHeightLimits(CADObjectCommands.GetActiveView());
          double minHeight = heightLimits.Item1;
          double maxHeight = heightLimits.Item2;

          if (routeHeight < minHeight || routeHeight > maxHeight) {
            ed.WriteMessage($"\nHeight must be between {minHeight} and {maxHeight} feet. Please enter a valid height.");
            pdo.Message = $"\nHeight must be between {minHeight} and {maxHeight} feet:";
            continue;
          }
          break; // Valid input
        }
        catch (System.Exception ex) {
          ed.WriteMessage($"\nError: {ex.Message}");
          continue;
        }
      }
      double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;

      var routeHeightDisplay = new RouteHeightDisplay(ed);
      routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);

      ed.WriteMessage("\nSelect base point for plumbing source");
      ObjectId blockId;
      string blockName = "GMEP SOURCE";
      Point3d point;
      double rotation = 0;
      string sourceId = Guid.NewGuid().ToString();
      try {
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            "Plumbing Source",
            out btr,
            out point
          );
          if (br != null) {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            RotateJig rotateJig = new RotateJig(br);
            PromptResult rotatePromptResult = ed.Drag(rotateJig);

            if (rotatePromptResult.Status != PromptStatus.OK) {
              ed.WriteMessage("\nOperation cancelled.");
              routeHeightDisplay.Disable();
              return;
            }
            rotation = br.Rotation;
            br.Position = new Point3d(br.Position.X, br.Position.Y, zIndex);


            curSpace.AppendEntity(br);

            tr.AddNewlyCreatedDBObject(br, true);
          }
          else {
            ed.WriteMessage("\nFailed to create block reference.");
            routeHeightDisplay.Disable();
            return;
          }

          blockId = br.Id;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "id") {
              prop.Value = sourceId;
            }
            if (prop.PropertyName == "type_id") {
              prop.Value = selectedSourceType.Id;
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = basePointGUID;
            }
          }
          tr.Commit();
        }
        PlumbingSource plumbingSource = new PlumbingSource(
          sourceId,
          projectId,
          point,
          selectedSourceType.Id,
          basePointGUID
        );
        //MariaDBService.CreatePlumbingSource(plumbingSource);
        MakePlumbingSourceLabel(plumbingSource, selectedSourceType);
      }
      catch (System.Exception ex) {
        ed.WriteMessage(ex.ToString());
        routeHeightDisplay.Disable();
        Console.WriteLine(ex.ToString());
      }
      routeHeightDisplay.Disable();
    }

    /*public Point3d CreateVentBlock(
      decimal fixtureDemand,
      string projectId,
      int selectedCatalogItemId,
      string selectedFixtureTypeAbbr,
      int index,
      string basePointId,
      string fixtureId, 
      double zIndex = 0
    ) {
      ed.WriteMessage("\nSelect base point for vent");
      ObjectId blockId;
      Point3d point;
      double rotation = 0;
      string GUID = Guid.NewGuid().ToString();
      string blockName = "GMEP VENT";
      try {
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            "Vent",
            out btr,
            out point
          );
          if (br != null) {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            RotateJig rotateJig = new RotateJig(br);
            PromptResult rotatePromptResult = ed.Drag(rotateJig);

            if (rotatePromptResult.Status != PromptStatus.OK) {
              return new Point3d();
            }
            rotation = br.Rotation;
            br.Position = new Point3d(br.Position.X, br.Position.Y, zIndex);
            curSpace.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);

          }
          blockId = br.Id;
          point = br.Position;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "gmep_plumbing_id") {
              prop.Value = GUID;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_dfu") {
              prop.Value = (double)fixtureDemand;
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = basePointId;
            }
            if (prop.PropertyName == "type_abbreviation")
            {
               prop.Value = selectedFixtureTypeAbbr;
            }
            if (prop.PropertyName == "catalog_id") {
              prop.Value = selectedCatalogItemId;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_id") {
              prop.Value = fixtureId;
            }
          }
          tr.Commit();
          PlumbingFixture plumbingFixture = new PlumbingFixture(
            GUID,
            projectId,
            new Point3d(),
            rotation,
            selectedCatalogItemId,
            selectedFixtureTypeAbbr,
            0,
            basePointId,
            fixtureId,
            blockName
          );
          //MariaDBService.CreatePlumbingFixture(plumbingFixture);
          MakePlumbingFixtureWasteVentLabel(plumbingFixture, br.Position, blockName, index);
        }
        return point;
      }
      catch (System.Exception ex) {
        ed.WriteMessage(ex.ToString());
        Console.WriteLine(ex.ToString());
        return new Point3d();
      }
    }

    public Point3d CreateDrainBlock(
      decimal fixtureDemand,
      string projectId,
      int selectedCatalogItemId,
      string selectedFixtureTypeAbbr,
      int index,
      Point3d ventPosition,
      string basePointId,
      string fixtureId,
      double zIndex = 0
    ) {
      ed.WriteMessage("\nSelect base point for drain");
      ObjectId blockId;
      Point3d point;
      string GUID = Guid.NewGuid().ToString();
      string blockName = "GMEP DRAIN";
      try {
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            "Drain",
            out btr,
            out point
          );
          if (br != null) {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            br.Position = new Point3d(br.Position.X, br.Position.Y, zIndex);
            curSpace.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            point = br.Position;
            Vector3d direction = ventPosition - point;
            double angle = Math.Atan2(direction.X, direction.Y);
            Point3d startPoint = new Point3d(
              ventPosition.X - (1.5 * Math.Sin(angle)),
              ventPosition.Y - (1.5 * Math.Cos(angle)),
              0
            );
            Point3d endPoint = new Point3d(
              point.X + (1.5 * Math.Sin(angle)),
              point.Y + (1.5 * Math.Cos(angle)),
              0
            );
            Line line = new Line();
            line.StartPoint = new Point3d(startPoint.X, startPoint.Y, zIndex);
            line.EndPoint = new Point3d(endPoint.X, endPoint.Y, zIndex);
            line.Layer = "P-WV-W-BELOW";
            curSpace.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
          }

          blockId = br.Id;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "gmep_plumbing_id") {
              prop.Value = GUID;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_dfu") {
              prop.Value = (double)fixtureDemand;
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = basePointId;
            }
            if (prop.PropertyName == "type_abbreviation") {
              prop.Value = selectedFixtureTypeAbbr;
            }
            if (prop.PropertyName == "catalog_id") {
              prop.Value = selectedCatalogItemId;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_id") {
              prop.Value = fixtureId;
            }
          }
          tr.Commit();
          PlumbingFixture plumbingFixture = new PlumbingFixture(
            GUID,
            projectId,
            new Point3d(),
            0,
            selectedCatalogItemId,
            selectedFixtureTypeAbbr,
            0,
            basePointId,
            fixtureId,
            blockName
          );
          //MariaDBService.CreatePlumbingFixture(plumbingFixture);
          MakePlumbingFixtureWasteVentLabel(plumbingFixture, br.Position, blockName, index);
        }
        return point;
      }
      catch (System.Exception ex) {
        ed.WriteMessage(ex.ToString());
        Console.WriteLine(ex.ToString());
        return new Point3d();
      }
    }

    public void CreateWasteVentBlock(
      string blockName,
      decimal fixtureDemand,
      string projectId,
      int selectedCatalogItemId,
      string selectedFixtureTypeAbbr,
      int index,
      string basePointId,
      string fixtureId,
      double zIndex = 0
    ) {
      ed.WriteMessage("\nSelect base point for " + selectedFixtureTypeAbbr);
      ObjectId blockId;
      Point3d point;
      double rotation = 0;
      string GUID = Guid.NewGuid().ToString();
      if (blockName.Contains("%WCOSTYLE%")) {
        PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
        keywordOptions.Message = "\nSelect WCO style";
        keywordOptions.Keywords.Add("STRAIGHT");
        keywordOptions.Keywords.Add("ANGLED");
        keywordOptions.Keywords.Add("FLOOR");
        keywordOptions.Keywords.Default = "STRAIGHT";
        keywordOptions.AllowNone = false;
        PromptResult keywordResult = ed.GetKeywords(keywordOptions);
        string wcoStyle = keywordResult.StringResult.Replace("\"", "");
        if (wcoStyle.Contains(' ')) {
          wcoStyle = wcoStyle.Split(' ')[0];
        }
        blockName = blockName.Replace("%WCOSTYLE%", wcoStyle);
      }
      try {
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            "Waste Vent",
            out btr,
            out point
          );
          if (br != null) {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            if (blockName != "GMEP WCO FLOOR") {
              RotateJig rotateJig = new RotateJig(br);
              PromptResult rotatePromptResult = ed.Drag(rotateJig);

              if (rotatePromptResult.Status != PromptStatus.OK) {
                return;
              }
              rotation = br.Rotation;
            }
            br.Position = new Point3d(br.Position.X, br.Position.Y, zIndex);

            curSpace.AppendEntity(br);

            tr.AddNewlyCreatedDBObject(br, true);
          }

          blockId = br.Id;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "gmep_plumbing_id") {
              prop.Value = GUID;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_dfu") {
              prop.Value = (double)fixtureDemand;
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = basePointId;
            }
            if (prop.PropertyName == "type_abbreviation") {
              prop.Value = selectedFixtureTypeAbbr;
            }
            if (prop.PropertyName == "catalog_id") {
              prop.Value = selectedCatalogItemId;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_id") {
              prop.Value = fixtureId;
            }
          }
          tr.Commit();
          PlumbingFixture plumbingFixture = new PlumbingFixture(
            GUID,
            projectId,
            br.Position,
            br.Rotation,
            selectedCatalogItemId,
            selectedFixtureTypeAbbr,
            0,
            basePointId,
            fixtureId,
            blockName
          );
          //MariaDBService.CreatePlumbingFixture(plumbingFixture);

          MakePlumbingFixtureWasteVentLabel(plumbingFixture, br.Position, blockName, index);
        }
      }
      catch (System.Exception ex) {
        ed.WriteMessage(ex.ToString());
        Console.WriteLine(ex.ToString());
      }
    }*/

    public static void Db_VerticalRouteErased(object sender, ObjectErasedEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      try {
        if (
          e.Erased
          && !SettingObjects
          && !IsSaving
          && e.DBObject is BlockReference blockRef
          && IsVerticalRouteBlock(blockRef)
        ) {
          ed.WriteMessage($"\nObject {e.DBObject.ObjectId} was erased.");

          string VerticalRouteId = string.Empty;
          var properties = blockRef.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in properties) {
            if (prop.PropertyName == "vertical_route_id") {
              VerticalRouteId = prop.Value?.ToString();
            }
          }
          if (!string.IsNullOrEmpty(VerticalRouteId)) {
            SettingObjects = true;

            using (Transaction tr = db.TransactionManager.StartTransaction()) {
              BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
              List<string> blockNames = new List<string>
              {
                "GMEP_PLUMBING_LINE_UP",
                "GMEP_PLUMBING_LINE_DOWN",
                "GMEP_PLUMBING_LINE_VERTICAL",
              };
              foreach (var name in blockNames) {
                BlockTableRecord basePointBlock = (BlockTableRecord)
                  tr.GetObject(bt[name], OpenMode.ForWrite);
                foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
                  if (id.IsValid) {
                    using (
                      BlockTableRecord anonymousBtr =
                        tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord
                    ) {
                      if (anonymousBtr != null) {
                        foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                          if (objId.IsValid) {
                            var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;

                            var pc = entity.DynamicBlockReferencePropertyCollection;

                            foreach (DynamicBlockReferenceProperty prop in pc) {
                              if (
                                prop.PropertyName == "vertical_route_id"
                                && prop.Value?.ToString() == VerticalRouteId
                              ) {
                                entity.Erase();
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
              tr.Commit();
            }
            SettingObjects = false;
          }
        }
      }
      catch (System.Exception ex) {
        ed.WriteMessage($"\nError in Db_ObjectErased: {ex.Message}");
      }
    }

    public static void Db_VerticalRouteModified(object sender, ObjectEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      Dictionary<string, ObjectId> basePoints = new Dictionary<string, ObjectId>();
      if (
        !SettingObjects
        && !IsSaving
        && e.DBObject is BlockReference blockRef
        && IsVerticalRouteBlock(blockRef)
      ) {
        if (blockRef == null || blockRef.IsErased || blockRef.IsDisposed)
          return;
        var dynamicProperties = blockRef.DynamicBlockReferencePropertyCollection;
        if (dynamicProperties == null || dynamicProperties.Count == 0) return;
        

        SettingObjects = true;
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
          BlockTableRecord basePointBlock = (BlockTableRecord)
            tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
          foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
            if (id.IsValid) {
              using (
                BlockTableRecord anonymousBtr =
                  tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord
              ) {
                if (anonymousBtr != null) {
                  foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                    var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                    var pc = entity.DynamicBlockReferencePropertyCollection;
                    foreach (DynamicBlockReferenceProperty prop in pc) {
                      if (prop.PropertyName == "id") {
                        string basePointId = prop.Value?.ToString();
                        if (!string.IsNullOrEmpty(basePointId) && basePointId != "0") {
                          basePoints.Add(basePointId, entity.ObjectId);
                        }
                      }
                    }
                  }
                }
              }
            }
          }
          tr.Commit();
        }

        string VerticalRouteId = string.Empty;
        string BasePointId = string.Empty;
        var properties = blockRef.DynamicBlockReferencePropertyCollection;
        foreach (DynamicBlockReferenceProperty prop in properties) {
          if (prop.PropertyName == "vertical_route_id") {
            VerticalRouteId = prop.Value?.ToString();
          }
          if (prop.PropertyName == "base_point_id") {
            BasePointId = prop.Value?.ToString();
          }
        }
        if (BasePointId != "" && basePoints.ContainsKey(BasePointId)) {
          ObjectId basePointIdObj = basePoints[BasePointId];

          using (Transaction tr = db.TransactionManager.StartTransaction()) {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            BlockReference basePointRef = (BlockReference)
              tr.GetObject(basePointIdObj, OpenMode.ForWrite);
            Vector3d distanceVector = blockRef.Position - basePointRef.Position;

            List<string> blockNames = new List<string>
            {
              "GMEP_PLUMBING_LINE_UP",
              "GMEP_PLUMBING_LINE_DOWN",
              "GMEP_PLUMBING_LINE_VERTICAL",
            };

            foreach (var name in blockNames) {
              BlockTableRecord basePointBlock = (BlockTableRecord)
                tr.GetObject(bt[name], OpenMode.ForWrite);
              foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
                if (id.IsValid) {
                  using (
                    BlockTableRecord anonymousBtr =
                      tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord
                  ) {
                    if (anonymousBtr != null) {
                      foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                        if (objId.IsValid) {
                          var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;

                          var pc = entity.DynamicBlockReferencePropertyCollection;

                          string BasePointId2 = string.Empty;
                          bool match = false;
                          foreach (DynamicBlockReferenceProperty prop in pc) {
                            if (
                              prop.PropertyName == "vertical_route_id"
                              && prop.Value?.ToString() == VerticalRouteId
                            ) {
                              match = true;
                            }
                            if (prop.PropertyName == "base_point_id") {
                              BasePointId2 = prop.Value?.ToString();
                            }
                          }
                          if (match) {
                            BlockReference basePointRef2 =
                              tr.GetObject(basePoints[BasePointId2], OpenMode.ForRead)
                              as BlockReference;
                            entity.Position = basePointRef2.Position + distanceVector;
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            tr.Commit();
          }
        }
        SettingObjects = false;
      }
    }

    /*public static void Db_BasePointErased(object sender, ObjectEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      try {
        if (
          e.DBObject is BlockReference blockRef
          && IsPlumbingBasePointBlock(blockRef)
          && !SettingObjects
          && !IsSaving
        ) {
          SettingObjects = true;
          string Id = string.Empty;
          var pc = blockRef.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "id") {
              Id = prop.Value?.ToString();
            }
          }
          using (Transaction tr = db.TransactionManager.StartTransaction()) {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            List<string> blockNames = new List<string>
            {
                "GMEP_PLUMBING_LINE_UP",
                "GMEP_PLUMBING_LINE_DOWN",
                "GMEP_PLUMBING_LINE_VERTICAL",
            };
            foreach (var name in blockNames) {
              BlockTableRecord basePointBlock = (BlockTableRecord)
                tr.GetObject(bt[name], OpenMode.ForWrite);
              foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
                if (id.IsValid) {
                  using (
                    BlockTableRecord anonymousBtr =
                      tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord
                  ) {
                    if (anonymousBtr != null) {
                      foreach (
                        ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)
                      ) {
                        if (objId.IsValid) {
                          var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;
                          var pc2 = entity.DynamicBlockReferencePropertyCollection;
                          foreach (DynamicBlockReferenceProperty prop in pc2) {
                            if (
                              prop.PropertyName == "base_point_id"
                              && prop.Value?.ToString() == Id
                            ) {
                              entity.Erase();
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            tr.Commit();
          }
          SettingObjects = false;
        }
      }
      catch (System.Exception ex) {
        ed.WriteMessage($"\nError in Db_BasePointErased: {ex.Message}");
      }
    }*/
    public static void Db_BasePointModified(object sender, ObjectEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      if (
        !SettingObjects
        && !IsSaving
        && e.DBObject is BlockReference blockRef
        && IsPlumbingBasePointBlock(blockRef)
      ) {
        if (blockRef == null || blockRef.IsErased || blockRef.IsDisposed)
          return;
        var properties = blockRef.DynamicBlockReferencePropertyCollection;
        if (properties == null || properties.Count == 0) return;

        SettingObjects = true;

        string Id = string.Empty;
        Vector3d distanceVector = new Vector3d(0, 0, 0);


        var pc = blockRef.DynamicBlockReferencePropertyCollection;
        if (pc != null) {
          double pos_x = 0;
          double pos_y = 0;
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "id") {
              Id = prop.Value?.ToString();
            }
            if (prop.PropertyName == "pos_x") {
              pos_x = Convert.ToDouble(prop.Value);
              prop.Value = blockRef.Position.X;
            }
            if (prop.PropertyName == "pos_y") {
              pos_y = Convert.ToDouble(prop.Value);
              prop.Value = blockRef.Position.Y;
            }
          }
          Point3d position = new Point3d(pos_x, pos_y, 0);
          distanceVector = blockRef.Position - position;
        }


        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);

          List<string> blockNames = new List<string>
          {
              "GMEP_PLUMBING_LINE_UP",
              "GMEP_PLUMBING_LINE_DOWN",
              "GMEP_PLUMBING_LINE_VERTICAL",
          };

          foreach (var name in blockNames) {
            BlockTableRecord basePointBlock = (BlockTableRecord)
              tr.GetObject(bt[name], OpenMode.ForWrite);
            foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
              if (id.IsValid) {
                using (
                  BlockTableRecord anonymousBtr =
                    tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord
                ) {
                  if (anonymousBtr != null) {
                    foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                      if (objId.IsValid) {
                        var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;

                        var pc2 = entity.DynamicBlockReferencePropertyCollection;

                        foreach (DynamicBlockReferenceProperty prop in pc2) {

                          if (prop.PropertyName == "base_point_id") {
                            string BasePointId = prop.Value?.ToString();
                            if (BasePointId == Id) {
                              entity.Position += distanceVector;
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
          tr.Commit();
        }
        SettingObjects = false;
      }
    }


    private static bool IsVerticalRouteBlock(BlockReference blockRef) {
      foreach (
        DynamicBlockReferenceProperty prop in blockRef.DynamicBlockReferencePropertyCollection
      ) {
        if (prop.PropertyName == "vertical_route_id")
          return true;
      }
      return false;
    }
    private static bool IsPlumbingBasePointBlock(BlockReference blockRef) {
      if (!CADObjectCommands.IsEditing) {
        foreach (
          DynamicBlockReferenceProperty prop in blockRef.DynamicBlockReferencePropertyCollection
        ) {
          if (prop.PropertyName == "view_id")
            return true;
        }
      }
      return false;
    }

    public static async void Db_DocumentSaved(object sender, DatabaseIOEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      MariaDBService mariaDBService = new MariaDBService();
      ed.WriteMessage("\nDocument saved, updating plumbing data...");
      try {
        string projectNo = CADObjectCommands.GetProjectNoFromFileName();
        string ProjectId = await mariaDBService.GetProjectId(projectNo);

        List<PlumbingHorizontalRoute> horizontalRoutes = GetHorizontalRoutesFromCAD(ProjectId);
        List<PlumbingVerticalRoute> verticalRoutes = GetVerticalRoutesFromCAD(ProjectId);
        List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD(ProjectId);
        List<PlumbingSource> sources = GetPlumbingSourcesFromCAD(ProjectId);
        List<PlumbingFixture> fixtures = GetPlumbingFixturesFromCAD(ProjectId);

        await mariaDBService.UpdatePlumbingHorizontalRoutes(horizontalRoutes, ProjectId);
        await mariaDBService.UpdatePlumbingVerticalRoutes(verticalRoutes, ProjectId);
        await mariaDBService.UpdatePlumbingPlanBasePoints(basePoints, ProjectId);
        await mariaDBService.UpdatePlumbingSources(sources, ProjectId);
        await mariaDBService.UpdatePlumbingFixtures(fixtures, ProjectId);
      }
      catch (System.Exception ex) {
        ed.WriteMessage("\nError getting ProjectId: " + ex.Message);
        return;
      }

    }

    public static List<PlumbingHorizontalRoute> GetHorizontalRoutesFromCAD(string ProjectId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      List<PlumbingHorizontalRoute> routes = new List<PlumbingHorizontalRoute>();
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId entId in modelSpace) {
          if (entId.ObjectClass == RXClass.GetClass(typeof(Line))) {
            Line line = tr.GetObject(entId, OpenMode.ForRead) as Line;
            if (line != null) {
              string type = "";
              switch (line.Layer) {
                case "P-DOMW-CWTR":
                  type = "Cold Water";
                  break;
                case "P-DOMW-HOTW":
                  type = "Hot Water";
                  break;
                case "P-GAS":
                  type = "Gas";
                  break;
                case "P-WV-VENT":
                  type = "Vent";
                  break;
                case "P-GREASE-WASTE":
                  type = "Waste";
                  break;
              }
              ResultBuffer xdata = line.GetXDataForApplication(XRecordKey);
              if (xdata != null && xdata.AsArray().Length > 2) {
                TypedValue[] values = xdata.AsArray();

                PlumbingHorizontalRoute route = new PlumbingHorizontalRoute(values[1].Value.ToString(), ProjectId, type, line.StartPoint, line.EndPoint, values[2].Value.ToString());
                routes.Add(route);
              }
            }
          }
        }
        tr.Commit();
      }
      ed.WriteMessage(ProjectId + " - Found " + routes.Count + " horizontal routes in the drawing.");
      return routes;
    }


    public static List<PlumbingVerticalRoute> GetVerticalRoutesFromCAD(string ProjectId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      List<PlumbingVerticalRoute> routes = new List<PlumbingVerticalRoute>();

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        List<string> blockNames = new List<string>
        {
            "GMEP_PLUMBING_LINE_UP",
            "GMEP_PLUMBING_LINE_DOWN",
            "GMEP_PLUMBING_LINE_VERTICAL"
        };

        foreach (var name in blockNames) {
          BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);
          foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
            if (id.IsValid) {
              using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord) {
                if (anonymousBtr != null) {

                  foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                    if (objId.IsValid) {
                      var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;

                      var pc = entity.DynamicBlockReferencePropertyCollection;

                      string Id = string.Empty;
                      string VerticalRouteId = string.Empty;
                      string BasePointId = string.Empty;
                      double pointX = 0;
                      double pointY = 0;
                      double startHeight = 0;
                      double length = 0;

                      foreach (DynamicBlockReferenceProperty prop in pc) {

                        if (prop.PropertyName == "vertical_route_id") {
                          VerticalRouteId = prop.Value.ToString();
                        }
                        if (prop.PropertyName == "base_point_id") {
                          BasePointId = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "id") {
                          Id = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "Connection X") {
                          pointX = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "Connection Y") {
                          pointY = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "start_height") {
                          startHeight = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "length") {
                          length = Convert.ToDouble(prop.Value);
                        }
                      }
                      if (Id != "0") {
                        double rotation = entity.Rotation;
                        double rotatedX = pointX * Math.Cos(rotation) - pointY * Math.Sin(rotation);
                        double rotatedY = pointX * Math.Sin(rotation) + pointY * Math.Cos(rotation);
                        var connectionPointLocation = new Point3d(entity.Position.X + rotatedX, entity.Position.Y + rotatedY, entity.Position.Z);

                        int nodeTypeId = 0;
                        switch (name) {
                          case "GMEP_PLUMBING_LINE_UP":
                            nodeTypeId = 1;
                            break;
                          case "GMEP_PLUMBING_LINE_DOWN":
                            nodeTypeId = 3;
                            break;
                          case "GMEP_PLUMBING_LINE_VERTICAL":
                            nodeTypeId = 2;
                            break;
                        }
                        string type = "";
                        switch(entity.Layer) {
                          case "P-DOMW-CWTR":
                            type = "Cold Water";
                            break;
                          case "P-DOMW-HOTW":
                            type = "Hot Water";
                            break;
                          case "P-GREASE-WASTE":
                            type = "Waste";
                            break;
                          case "P-WV-VENT":
                            type = "Vent";
                            break;
                          case "P-GAS":
                            type = "Gas";
                            break;
                        }

                        PlumbingVerticalRoute route = new PlumbingVerticalRoute(
                          Id,
                          ProjectId,
                          type,
                          entity.Position,
                          connectionPointLocation,
                          VerticalRouteId,
                          BasePointId,
                          startHeight,
                          length,
                          nodeTypeId
                        );
                        routes.Add(route);
                      }
                    }
                  }
                }
              }
            }
          }
        }
        tr.Commit();
      }
      return routes;
    }
  
    public static List<PlumbingPlanBasePoint> GetPlumbingBasePointsFromCAD(string ProjectId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      List<PlumbingPlanBasePoint> points = new List<PlumbingPlanBasePoint>();

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (id.IsValid) {
            using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord) {
              if (anonymousBtr != null) {

                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                  if (objId.IsValid) {
                    var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;

                    var pc = entity.DynamicBlockReferencePropertyCollection;

                    string Id = string.Empty;
                    string Plan = string.Empty;
                    string ViewId = string.Empty;
                    string Type = string.Empty;
                    int Floor = 0;
                    double FloorHeight = 0;
                    double CeilingHeight = 0;

                    foreach (DynamicBlockReferenceProperty prop in pc) {
                      if (prop.PropertyName == "floor") {
                        Floor = Convert.ToInt32(prop.Value);
                      }
                      if (prop.PropertyName == "plan") {
                        Plan = prop.Value?.ToString();
                      }
                      if (prop.PropertyName == "id") {
                        Id = prop.Value?.ToString();
                      }
                      if (prop.PropertyName == "type") {
                        Type = prop.Value?.ToString();
                      }
                      if (prop.PropertyName == "view_id") {
                        ViewId = prop.Value?.ToString();
                      }
                      if (prop.PropertyName == "floor_height") {
                        FloorHeight = Convert.ToDouble(prop.Value);
                      }
                      if (prop.PropertyName == "ceiling_height") {
                        CeilingHeight = Convert.ToDouble(prop.Value);
                      }

                    }
                    if (Id != "0") {
                      PlumbingPlanBasePoint BasePoint = new PlumbingPlanBasePoint(
                        Id,
                        ProjectId,
                        entity.Position,
                        Plan,
                        Type,
                        ViewId,
                        Floor,
                        FloorHeight,
                        CeilingHeight
                      );
                      points.Add(BasePoint);
                    }
                  }
                }
              }
            }
          }
        }
        tr.Commit();
      }
      ed.WriteMessage(ProjectId + " - Found " + points.Count + " basepoints in the drawing.");
      return points;
    }

    public static List<PlumbingSource> GetPlumbingSourcesFromCAD(string ProjectId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      List<PlumbingSource> sources = new List<PlumbingSource>();

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        List<string> blockNames = new List<string>
        {
          "GMEP SOURCE",
          "GMEP WH 80",
          "GMEP WH 50",
          "GMEP IWH",
          "GMEP DRAIN",
          "GMEP FS 12",
          "GMEP FS 6",
          "GMEP FD"
        };
        foreach (string name in blockNames) {
          BlockTableRecord sourceBlock = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);
          foreach (ObjectId id in sourceBlock.GetAnonymousBlockIds()) {
            if (id.IsValid) {
              using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord) {
                if (anonymousBtr != null) {
                  foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                    if (objId.IsValid) {
                      var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;

                      var pc = entity.DynamicBlockReferencePropertyCollection;

                      string GUID = string.Empty;
                      string basePointId = string.Empty;
                      int typeId = 0;
                      int Floor = 0;
                      double hotWaterX = 0;
                      double hotWaterY = 0;

                      foreach (DynamicBlockReferenceProperty prop in pc) {
                        if (prop.PropertyName == "id") {
                          GUID = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "base_point_id") {
                          basePointId = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "type_id") {
                          typeId = Convert.ToInt32(prop.Value);
                        }
                        if (prop.PropertyName == "Hot Water X") {
                          hotWaterX = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "Hot Water Y") {
                          hotWaterY = Convert.ToDouble(prop.Value);
                        }
                      }
                      if (typeId == 4) {
                        continue;
                      }
                      if (name == "GMEP WH 50" || name == "GMEP WH 80" || name == "GMEP IWH") {
                        typeId = 2;
                      }
                      if (name == "GMEP DRAIN" || name == "GMEP FS 12" || name == "GMEP FS 6" || name == "GMEP FD") {
                        typeId = 4;
                      }
                      if (!string.IsNullOrEmpty(GUID) && GUID != "0") {
                        Point3d position = entity.Position;
                        if (hotWaterX != 0 && hotWaterY != 0) {
                          double rotation = entity.Rotation;
                          double rotatedX = hotWaterX * Math.Cos(rotation) - hotWaterY * Math.Sin(rotation);
                          double rotatedY = hotWaterX * Math.Sin(rotation) + hotWaterY * Math.Cos(rotation);
                          position = new Point3d(entity.Position.X + rotatedX, entity.Position.Y + rotatedY, entity.Position.Z);
                        }
                        PlumbingSource source = new PlumbingSource(
                          GUID,
                          ProjectId,
                          position,
                          typeId,
                          basePointId
                        );
                        sources.Add(source);
                      }
                    }
                  }
                }
              }
            }
          }
        }
        tr.Commit();
      }
      ed.WriteMessage(ProjectId + " - Found " + sources.Count + " plumbing sources in the drawing.");
      return sources;
    }

  
  public static List<PlumbingFixture> GetPlumbingFixturesFromCAD(string ProjectId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      List<PlumbingFixture> fixtures = new List<PlumbingFixture>();

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        List<string> blockNames = new List<string>
        {
          "GMEP WH 80",
          "GMEP WH 50",
          "GMEP DRAIN",
          "GMEP CP",
          "GMEP FS 12",
          "GMEP FS 6",
          "GMEP FD",
          "GMEP RPBFP",
          "GMEP IWH",
          "GMEP SOURCE",
          "GMEP PLUMBING VENT EXIT",
          "GMEP PLUMBING GAS OUTPUT"
          //"GMEP WCO STRAIGHT",
          //"GMEP WCO ANGLED",
          //"GMEP WCO FLOOR",
        };
        foreach (string name in blockNames) {
          BlockTableRecord fixtureBlock = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);
          foreach (ObjectId id in fixtureBlock.GetAnonymousBlockIds()) {
            if (id.IsValid) {
              using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord) {
                if (anonymousBtr != null) {
                  foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                    if (objId.IsValid) {
                      var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;

                      var pc = entity.DynamicBlockReferencePropertyCollection;

                      string GUID = string.Empty;
                      string basePointId = string.Empty;
                      string selectedFixtureTypeAbbr = string.Empty;
                      int selectedCatalogItemId = 0;
                      double coldWaterX = 0;
                      double coldWaterY = 0;
                      int number = 0;
                      int sourceTypeId = 0;

                      foreach (DynamicBlockReferenceProperty prop in pc) {
                        if (prop.PropertyName == "id") {
                          GUID = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "base_point_id") {
                          basePointId = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "type_abbreviation") {
                          selectedFixtureTypeAbbr = prop.Value.ToString();
                        }
                        if (prop.PropertyName == "catalog_id") {
                         selectedCatalogItemId = Convert.ToInt32(prop.Value);
                        }
                        if (prop.PropertyName == "Cold Water X") {
                          coldWaterX = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "Cold Water Y") {
                          coldWaterY = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "number") {
                          number = Convert.ToInt32(prop.Value);
                        }
                        if (prop.PropertyName == "type_id") {
                          sourceTypeId = Convert.ToInt32(prop.Value);
                        }

                      }
                 
                      if (!string.IsNullOrEmpty(GUID) && GUID != "0" && (sourceTypeId == 0 || sourceTypeId == 4)) {
                        Point3d position = entity.Position;
                        if (coldWaterX != 0 && coldWaterY != 0) {
                          double rotation = entity.Rotation;
                          double rotatedX = coldWaterX * Math.Cos(rotation) - coldWaterY * Math.Sin(rotation);
                          double rotatedY = coldWaterX * Math.Sin(rotation) + coldWaterY * Math.Cos(rotation);
                          position = new Point3d(entity.Position.X + rotatedX, entity.Position.Y + rotatedY, entity.Position.Z);
                        }
                        if (sourceTypeId == 4) {
                          selectedFixtureTypeAbbr = "SWR";
                        }
                        PlumbingFixture fixture = new PlumbingFixture(
                          GUID,
                          ProjectId,
                          position,
                          entity.Rotation,
                          selectedCatalogItemId,
                          selectedFixtureTypeAbbr,
                          number,
                          basePointId,
                          name
                        );
                        
                        fixtures.Add(fixture);
                      }
                    }
                  }
                }
              }
            }
          }
        }
        tr.Commit();
      }
      ed.WriteMessage(ProjectId + " - Found " + fixtures.Count + " plumbing fixtures in the drawing.");
      return fixtures;
    }
    public int DetermineFixtureNumber(PlumbingFixture fixture) {
      var fixtures = GetPlumbingFixturesFromCAD(fixture.ProjectId)
        .Where(f => f.TypeAbbreviation == fixture.TypeAbbreviation && f.Id != fixture.Id)
        .ToList();
      
      var existing = fixtures.FirstOrDefault(f => f.CatalogId == fixture.CatalogId);

      if (existing != null) {
        ed.WriteMessage($"\nExisting fixture found: {existing?.Number ?? 0} for catalog {fixture.CatalogId}");
        return existing.Number;
      }

      int nextNumber = fixtures.Select(f => f.Number).DefaultIfEmpty(0).Max() + 1;
      return nextNumber;
    }
  }






  public class PluginEntry : IExtensionApplication
  {
    public void Initialize()
    {
      // Attach to document events
      Application.DocumentManager.DocumentCreated += DocumentManager_DocumentCreated;
      Application.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;

      // Optionally, initialize for already open documents
      foreach (Document doc in Application.DocumentManager)
      {
        AutoCADIntegration.AttachHandlers(doc);
      }
    }

    public void Terminate()
    {
      // Clean up if needed
    }

    private void DocumentManager_DocumentCreated(object sender, DocumentCollectionEventArgs e)
    {
      AutoCADIntegration.AttachHandlers(e.Document);
    }

    private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
    {
      AutoCADIntegration.AttachHandlers(e.Document);
    }
  }


  public class RouteHeightDisplay {
    private readonly Editor _ed;
    private double _routeHeight;
    private string _viewName = string.Empty;
    private int _floor = 0;
    private bool _enabled = false;

    public RouteHeightDisplay(Editor ed) {
      _ed = ed;
    }

    public void Enable(double routeHeight, string viewName, int floor) {
      if (_enabled) return;
      _routeHeight = routeHeight;
      _viewName = viewName;
      _floor = floor;
      _ed.PointMonitor += Ed_PointMonitor;
      _enabled = true;
    }

    public void Disable() {
      if (!_enabled) return;
      _ed.PointMonitor -= Ed_PointMonitor;
      _enabled = false;
      TransientManager.CurrentTransientManager.EraseTransients(TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
    }

    public void Update(double routeHeight, string viewName, int floor) {
      _routeHeight = routeHeight;
      _viewName = viewName;
      _floor = floor;
    }

    private void Ed_PointMonitor(object sender, PointMonitorEventArgs e) {
      var view = _ed.GetCurrentView();
      var db = _ed.Document.Database;


      string viewNameText = $"View: {_viewName}";
      string floorText = $"Floor: {_floor}";
      string routeHeightText = $"Height: {_routeHeight:0.##} ft";

      double textHeight = view.Height / 70;
      double lineSpacing = textHeight * 1.2;

      string[] lines = { viewNameText, routeHeightText, floorText };
      int maxLen = lines.Max(s => s.Length);
      double textWidth = textHeight * maxLen * 0.6;
      double padding = textHeight * 0.4;

      // Position text 10 units left of the cursor
      var pos = e.Context.RawPoint + new Vector3d(-(view.Width / 10.5), -(view.Height / 150), 0);

      // Rectangle corners (lower left, lower right, upper right, upper left)
      double rectHeight = lineSpacing * 3 + padding * 2;
      Point3d lowerLeft = new Point3d(pos.X - padding, pos.Y - padding, pos.Z);
      Point3d lowerRight = new Point3d(pos.X + textWidth + padding, pos.Y - padding, pos.Z);
      Point3d upperRight = new Point3d(pos.X + textWidth + padding, pos.Y + rectHeight - padding, pos.Z);
      Point3d upperLeft = new Point3d(pos.X - padding, pos.Y + rectHeight - padding, pos.Z);

      // Create filled rectangle using Solid
      var solid = new Solid(lowerLeft, lowerRight, upperLeft, upperRight);
      solid.ColorIndex = 8; // Light gray, or set as needed

      var border = new Polyline(4);
      border.AddVertexAt(0, new Point2d(lowerLeft.X, lowerLeft.Y), 0, 0, 0);
      border.AddVertexAt(1, new Point2d(lowerRight.X, lowerRight.Y), 0, 0, 0);
      border.AddVertexAt(2, new Point2d(upperRight.X, upperRight.Y), 0, 0, 0);
      border.AddVertexAt(3, new Point2d(upperLeft.X, upperLeft.Y), 0, 0, 0);
      border.Closed = true;
      border.Color = Autodesk.AutoCAD.Colors.Color.FromRgb(0, 0, 0);

      // Create the text
      var text1 = new DBText {
        Position = new Point3d(pos.X, pos.Y + lineSpacing * 2, pos.Z),
        Height = textHeight,
        TextString = viewNameText,
        Layer = "0",
        Color = Autodesk.AutoCAD.Colors.Color.FromRgb(0, 0, 0),
        TextStyleId = db.Textstyle,
      };
      var text2 = new DBText {
        Position = new Point3d(pos.X, pos.Y + lineSpacing, pos.Z),
        Height = textHeight,
        TextString = floorText,
        Layer = "0",
        Color = Autodesk.AutoCAD.Colors.Color.FromRgb(0, 0, 0),
        TextStyleId = db.Textstyle,
      };
      var text3 = new DBText {
        Position = new Point3d(pos.X, pos.Y, pos.Z),
        Height = textHeight,
        TextString = routeHeightText,
        Layer = "0",
        Color = Autodesk.AutoCAD.Colors.Color.FromRgb(0, 0, 0),
        TextStyleId = db.Textstyle,
      };

      // Remove previous transients
      TransientManager.CurrentTransientManager.EraseTransients(
          TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());

      // Draw background solid first, then text
      TransientManager.CurrentTransientManager.AddTransient(
          solid, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
      TransientManager.CurrentTransientManager.AddTransient(
          border, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
      TransientManager.CurrentTransientManager.AddTransient(
          text1, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
      TransientManager.CurrentTransientManager.AddTransient(
          text2, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
      TransientManager.CurrentTransientManager.AddTransient(
          text3, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
    }
  }
}
