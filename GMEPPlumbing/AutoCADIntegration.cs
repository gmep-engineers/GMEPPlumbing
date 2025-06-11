using System;
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
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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

[assembly: CommandClass(typeof(GMEPPlumbing.AutoCADIntegration))]
[assembly: CommandClass(typeof(GMEPPlumbing.Commands.TableCommand))]
[assembly: ExtensionApplication(typeof(GMEPPlumbing.PluginEntry))]

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
    public static bool IsSaving { get; private set; }
    public static bool SettingObjects { get; set; }

    public AutoCADIntegration()
    {
      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;
      SettingObjects = false;
      IsSaving = false;
    }

    public static void AttachHandlers(Document doc)
    {
      var db = doc.Database;
      var ed = doc.Editor;

      // Prevent multiple attachments

      db.BeginSave -= (s, e) => IsSaving = true;
      db.SaveComplete -= (s, e) => IsSaving = false;
      db.AbortSave -=(s, e) => IsSaving = false;

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
    public async void PlumbingHorizontalRoute()
    {
      List<string> routeGUIDS = new List<string>();
      string layer = "Defpoints";
      string sourceId = "";

      PromptEntityOptions sourcePeo = new PromptEntityOptions(
        "\nSelect where the route is being sourced from (source or vertical route)"
      );
      sourcePeo.SetRejectMessage("\nSelect where the route is being sourced from");
      sourcePeo.AddAllowedClass(typeof(BlockReference), true);
      PromptEntityResult sourcePer = ed.GetEntity(sourcePeo);

      if (sourcePer.Status != PromptStatus.OK)
      {
        ed.WriteMessage("\nCommand cancelled.");
        return;
      }
      ObjectId sourceObjectId = sourcePer.ObjectId;
      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        BlockReference sourceBlockRef = (BlockReference)
          tr.GetObject(sourceObjectId, OpenMode.ForRead);
        var pc = sourceBlockRef.DynamicBlockReferencePropertyCollection;
        bool match = false;
        foreach (DynamicBlockReferenceProperty prop in pc)
        {
          if (prop.PropertyName == "id")
          {
            sourceId = prop.Value.ToString();
          }
          if (prop.PropertyName == "vertical_route_id")
          {
            match = true;
          }
        }
        if (!match)
        {
          return;
        }
        layer = sourceBlockRef.Layer;

        tr.Commit();
      }

      PromptPointOptions ppo2 = new PromptPointOptions("\nSpecify start point for route: ");
      ppo2.AllowNone = false;
      PromptPointResult ppr2 = ed.GetPoint(ppo2);
      if (ppr2.Status != PromptStatus.OK)
      {
        ed.WriteMessage("\nCommand cancelled.");
        return;
      }

      Point3d startPointLocation2 = ppr2.Value;
      ObjectId addedLineId2 = ObjectId.Null;
      string LineGUID2 = Guid.NewGuid().ToString();

      PromptPointOptions ppo3 = new PromptPointOptions("\nSpecify next point for route: ");
      ppo3.BasePoint = startPointLocation2;
      ppo3.UseBasePoint = true;

      PromptPointResult ppr3 = ed.GetPoint(ppo3);
      if (ppr3.Status != PromptStatus.OK)
        return;

      Point3d endPointLocation2 = ppr3.Value;

      using (Transaction tr2 = db.TransactionManager.StartTransaction())
      {
        BlockTable bt = (BlockTable)tr2.GetObject(db.BlockTableId, OpenMode.ForWrite);
        BlockTableRecord btr = (BlockTableRecord)
          tr2.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

        Line line = new Line();
        line.StartPoint = startPointLocation2;
        line.EndPoint = endPointLocation2;
        line.Layer = layer;
        btr.AppendEntity(line);
        tr2.AddNewlyCreatedDBObject(line, true);
        addedLineId2 = line.ObjectId;
        tr2.Commit();
      }
      routeGUIDS.Add(LineGUID2);
      AttachRouteXData(addedLineId2, LineGUID2, sourceId);
      AddArrowsToLine(addedLineId2, LineGUID2);

      while (true)
      {
        //Select a starting point/object
        PromptEntityOptions peo = new PromptEntityOptions("\nSelect a line");
        peo.SetRejectMessage("\nSelect a line");
        peo.AddAllowedClass(typeof(Line), true);
        PromptEntityResult per = ed.GetEntity(peo);

        if (per.Status != PromptStatus.OK)
        {
          ed.WriteMessage("\nCommand cancelled.");
          return;
        }
        ObjectId basePointId = per.ObjectId;

        Point3d startPointLocation = Point3d.Origin;
        ObjectId addedLineId = ObjectId.Null;

        string LineGUID = Guid.NewGuid().ToString();

        // Check if the selected object is a BlockReference or Line
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          Entity basePoint = (Entity)tr.GetObject(basePointId, OpenMode.ForRead);

          //get line choice
          if (basePoint is Line basePointLine)
          {
            //retrieving the lines xdata
            ResultBuffer xData = basePointLine.GetXDataForApplication(XRecordKey);
            if (xData == null || xData.AsArray().Length < 2)
            {
              ed.WriteMessage("\nSelected line does not have the required XData.");
              return;
            }
            TypedValue[] values = xData.AsArray();
            string Id = values[1].Value as string;
            if (!routeGUIDS.Contains(Id))
            {
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

          PromptPointResult ppr = ed.GetPoint(ppo);
          if (ppr.Status != PromptStatus.OK)
            return;

          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
          BlockTableRecord btr = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

          Line line = new Line();
          line.StartPoint = startPointLocation;
          line.EndPoint = new Point3d(ppr.Value.X, ppr.Value.Y, 0);
          line.Layer = layer;
          btr.AppendEntity(line);
          tr.AddNewlyCreatedDBObject(line, true);
          addedLineId = line.ObjectId;

          //PropagateUpRouteInfo(tr, layer, LineGUID);

          tr.Commit();
        }
        routeGUIDS.Add(LineGUID);
        AttachRouteXData(addedLineId, LineGUID, sourceId);
        AddArrowsToLine(addedLineId, LineGUID);
      }
    }

    [CommandMethod("PlumbingVerticalRoute")]
    public async void PlumbingVerticalRoute()
    {
      SettingObjects = true;
      string layer = "Defpoints";
      PromptKeywordOptions pko = new PromptKeywordOptions("\nSelect route type: ");
      pko.Keywords.Add("HotWater");
      pko.Keywords.Add("ColdWater");
      pko.Keywords.Add("Gas");
      // pko.Keywords.Add("Sewer");
      //pko.Keywords.Add("Storm");
      PromptResult pr2 = ed.GetKeywords(pko);
      string result = pr2.StringResult;

      switch (result)
      {
        case "HotWater":
          layer = "P-DOMW-HOTW";
          break;
        case "ColdWater":
          layer = "P-DOMW-CWTR";
          break;
        case "Gas":
          layer = "P-GAS";
          break;
        /* case "Sewer":
                layer = "GMEP_PLUMBING_SEWER";
                break;
            case "Storm":
                layer = "GMEP_PLUMBING_STORM";
                break;*/
        default:
          ed.WriteMessage("\nInvalid route type selected.");
          return;
      }

      List<ObjectId> basePointIds = new List<ObjectId>();
      int startFloor = 0;
      Point3d StartBasePointLocation = new Point3d(0, 0, 0);
      Point3d StartUpLocation = new Point3d(0, 0, 0);
      ObjectId startPipeId = ObjectId.Null;
      string verticalRouteId = Guid.NewGuid().ToString();
      ObjectId gmepTextStyleId;

      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)
          tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        Dictionary<string, List<ObjectId>> basePoints = new Dictionary<string, List<ObjectId>>();
        TextStyleTable textStyleTable = (TextStyleTable)
          tr.GetObject(doc.Database.TextStyleTableId, OpenMode.ForRead);
        if (textStyleTable.Has("gmep"))
        {
          gmepTextStyleId = textStyleTable["gmep"];
        }
        else
        {
          ed.WriteMessage("\nText style 'gmep' not found. Using default text style.");
          gmepTextStyleId = doc.Database.Textstyle;
        }

        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
        {
          if (id.IsValid)
          {
            using (
              BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord
            )
            {
              if (anonymousBtr != null)
              {
                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                {
                  var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                  var pc = entity.DynamicBlockReferencePropertyCollection;
                  foreach (DynamicBlockReferenceProperty prop in pc)
                  {
                    if (prop.PropertyName == "View_Id")
                    {
                      string key = prop.Value.ToString();
                      if (key != "0")
                      {
                        if (!basePoints.ContainsKey(key))
                        {
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
        //meow meow
        List<string> keywords = new List<string>();
        foreach (var key in basePoints.Keys)
        {
          var objId = basePoints[key][0];
          var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
          var pc = entity.DynamicBlockReferencePropertyCollection;
          string planName = "";
          string viewport = "";
          foreach (DynamicBlockReferenceProperty prop in pc)
          {
            if (prop.PropertyName == "Plan")
            {
              planName = prop.Value.ToString();
            }
            if (prop.PropertyName == "Type")
            {
              viewport = prop.Value.ToString();
            }
          }
          if (planName != "" && viewport != "")
          {
            string keyword = planName + ":" + viewport;
            if (!keywords.Contains(keyword))
            {
              keywords.Add(keyword);
            }
            else
            {
              int count = keywords.Count(x =>
                x == keyword || (x.StartsWith(keyword + "(") && x.EndsWith(")"))
              );
              keywords.Add(keyword + "(" + (count + 1).ToString() + ")");
            }
          }
        }
        PromptKeywordOptions promptOptions = new PromptKeywordOptions("\nPick View: ");
        foreach (var keyword in keywords)
        {
          promptOptions.Keywords.Add(keyword);
        }
        PromptResult pr = ed.GetKeywords(promptOptions);
        string resultKeyword = pr.StringResult;
        int index = keywords.IndexOf(resultKeyword);
        // ed.WriteMessage("BasePoints: " + basePoints.Count().ToString() + "ketwords: " + keywords.Count().ToString() + "index: " + index.ToString() + "resultKeyword: " + resultKeyword);
        basePointIds = basePoints.ElementAt(index).Value;

        //Picking start floor
        PromptKeywordOptions floorOptions = new PromptKeywordOptions("\nStarting Floor: ");
        for (int i = 1; i <= basePointIds.Count; i++)
        {
          floorOptions.Keywords.Add(i.ToString());
        }
        PromptResult floorResult = ed.GetKeywords(floorOptions);
        startFloor = int.Parse(floorResult.StringResult);

        BlockReference firstFloorBasePoint = null;

        foreach (ObjectId objId in basePointIds)
        {
          var entity2 = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
          var pc2 = entity2.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc2)
          {
            if (prop.PropertyName == "Floor")
            {
              int floor = Convert.ToInt32(prop.Value);
              if (floor == startFloor)
              {
                ZoomToBlock(ed, entity2);
              }
              if (firstFloorBasePoint == null && floor == startFloor)
              {
                firstFloorBasePoint = entity2;
                StartBasePointLocation = entity2.Position;
              }
            }
          }
        }
        if (firstFloorBasePoint != null)
        {
          BlockTableRecord block = null;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            "GMEP_PLUMBING_LINE_VERTICAL",
            out block,
            out StartUpLocation
          );
          if (br != null)
          {
            br.Layer = layer;
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            curSpace.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            startPipeId = br.ObjectId;
          }
        }

        tr.Commit();
      }
      //getting difference between start base point and up point
      Vector3d upVector = StartUpLocation - StartBasePointLocation;

      //picking end floor
      PromptKeywordOptions endFloorOptions = new PromptKeywordOptions("\nEnding Floor: ");
      for (int i = 1; i <= basePointIds.Count; i++)
      {
        if (i != startFloor)
        {
          endFloorOptions.Keywords.Add(i.ToString());
        }
      }
      PromptResult endFloorResult = ed.GetKeywords(endFloorOptions);
      int endFloor = int.Parse(endFloorResult.StringResult);

      Dictionary<int, BlockReference> BasePointRefs = new Dictionary<int, BlockReference>();
      Dictionary<int, string> BasePointGUIDs = new Dictionary<int, string>();
      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

        foreach (ObjectId objId in basePointIds)
        {
          var entity2 = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
          var pc2 = entity2.DynamicBlockReferencePropertyCollection;

          int floor = 0;
          string guid = "";
          foreach (DynamicBlockReferenceProperty prop in pc2)
          {
            if (prop.PropertyName == "Floor")
            {
              floor = Convert.ToInt32(prop.Value);
              BasePointRefs.Add(floor, entity2);
            }
            if (prop.PropertyName == "Id")
            {
              guid = prop.Value.ToString();
            }
          }
          if (floor != 0 && guid != "")
          {
            BasePointGUIDs.Add(floor, guid);
          }
        }
        tr.Commit();
      }

      if (endFloor > startFloor)
      {
        Point3d labelPoint = Point3d.Origin;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
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

          if (rotatePromptResult.Status != PromptStatus.OK)
          {
            return;
          }
          labelPoint = upBlockRef2.Position;

          upBlockRef2.Layer = layer;
          curSpace2.AppendEntity(upBlockRef2);
          tr.AddNewlyCreatedDBObject(upBlockRef2, true);

          // Attach the vertical route ID to the start pipe
          var pc2 = upBlockRef2.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc2)
          {
            if (prop.PropertyName == "id")
            {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id")
            {
              prop.Value = BasePointGUIDs[startFloor];
            }
            if (prop.PropertyName == "vertical_route_id")
            {
              prop.Value = verticalRouteId;
            }
          }

          // Set the vertical route ID
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint, "UP");

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          //Continue Pipe
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          for (int i = startFloor + 1; i < endFloor; i++)
          {
            Point3d newUpPointLocation = BasePointRefs[i].Position + upVector;
            BlockTableRecord blockDef =
              tr.GetObject(bt["GMEP_PLUMBING_LINE_VERTICAL"], OpenMode.ForRead) as BlockTableRecord;
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            // Create the BlockReference at the desired location
            BlockReference upBlockRef = new BlockReference(newUpPointLocation, blockDef.ObjectId);
            upBlockRef.Layer = layer;
            curSpace.AppendEntity(upBlockRef);
            tr.AddNewlyCreatedDBObject(upBlockRef, true);
            var pc2 = upBlockRef.DynamicBlockReferencePropertyCollection;

            foreach (DynamicBlockReferenceProperty prop in pc2)
            {
              if (prop.PropertyName == "id")
              {
                prop.Value = Guid.NewGuid().ToString();
              }
              if (prop.PropertyName == "base_point_id")
              {
                prop.Value = BasePointGUIDs[i];
              }
              if (prop.PropertyName == "vertical_route_id")
              {
                prop.Value = verticalRouteId;
              }
            }
          }

          //end pipe
          ZoomToBlock(ed, BasePointRefs[endFloor]);
          Point3d newUpPointLocation3 = BasePointRefs[endFloor].Position + upVector;
          BlockTableRecord blockDef3 =
            tr.GetObject(bt["GMEP_PLUMBING_LINE_DOWN"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace3 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
          RotateJig rotateJig2 = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
          if (rotatePromptResult2.Status != PromptStatus.OK)
          {
            return;
          }

          upBlockRef3.Layer = layer;
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc3)
          {
            if (prop.PropertyName == "id")
            {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id")
            {
              prop.Value = BasePointGUIDs[endFloor];
            }
            if (prop.PropertyName == "vertical_route_id")
            {
              prop.Value = verticalRouteId;
            }
          }
          tr.Commit();
        }
      }
      else if (endFloor < startFloor)
      {
        Point3d labelPoint2 = Point3d.Origin;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
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
          if (rotatePromptResult.Status != PromptStatus.OK)
          {
            return;
          }
          upBlockRef2.Layer = layer;
          curSpace2.AppendEntity(upBlockRef2);
          tr.AddNewlyCreatedDBObject(upBlockRef2, true);
          labelPoint2 = upBlockRef2.Position;

          var pc2 = upBlockRef2.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc2)
          {
            if (prop.PropertyName == "id")
            {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id")
            {
              prop.Value = BasePointGUIDs[startFloor];
            }
            if (prop.PropertyName == "vertical_route_id")
            {
              prop.Value = verticalRouteId;
            }
          }
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint2, "DOWN");

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          //Continue Pipe
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          for (int i = startFloor - 1; i > endFloor; i--)
          {
            Point3d newUpPointLocation = BasePointRefs[i].Position + upVector;
            BlockTableRecord blockDef =
              tr.GetObject(bt["GMEP_PLUMBING_LINE_VERTICAL"], OpenMode.ForRead) as BlockTableRecord;
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            // Create the BlockReference at the desired location
            BlockReference upBlockRef = new BlockReference(newUpPointLocation, blockDef.ObjectId);
            upBlockRef.Layer = layer;
            curSpace.AppendEntity(upBlockRef);
            tr.AddNewlyCreatedDBObject(upBlockRef, true);
            var pc = upBlockRef.DynamicBlockReferencePropertyCollection;

            foreach (DynamicBlockReferenceProperty prop in pc)
            {
              if (prop.PropertyName == "id")
              {
                prop.Value = Guid.NewGuid().ToString();
              }
              if (prop.PropertyName == "base_point_id")
              {
                prop.Value = BasePointGUIDs[i];
              }
              if (prop.PropertyName == "vertical_route_id")
              {
                prop.Value = verticalRouteId;
              }
            }
          }

          //end pipe
          ZoomToBlock(ed, BasePointRefs[endFloor]);
          Point3d newUpPointLocation3 = BasePointRefs[endFloor].Position + upVector;
          BlockTableRecord blockDef3 =
            tr.GetObject(bt["GMEP_PLUMBING_LINE_UP"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace3 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
          RotateJig rotateJig2 = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
          if (rotatePromptResult2.Status != PromptStatus.OK)
          {
            return;
          }
          upBlockRef3.Layer = layer;
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;

          foreach (DynamicBlockReferenceProperty prop in pc3)
          {
            if (prop.PropertyName == "id")
            {
              prop.Value = Guid.NewGuid().ToString();
            }
            if (prop.PropertyName == "base_point_id")
            {
              prop.Value = BasePointGUIDs[endFloor];
            }
            if (prop.PropertyName == "vertical_route_id")
            {
              prop.Value = verticalRouteId;
            }
          }
          tr.Commit();
        }
      }
      SettingObjects = false;
    }

    [CommandMethod("SETPLUMBINGBASEPOINT")]
    public async void SetPlumbingBasePoint()
    {
      SettingObjects = true;
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
          BlockTableRecord curSpace = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

          BlockTableRecord block;
          string message =
            "\nCreating Plumbing Base Point for " + planName + " on floor " + (i + 1);
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            "GMEP_PLUMBING_BASEPOINT",
            out block,
            out point
          );
          br.Layer = "Defpoints";
          if (br != null)
          {
            curSpace.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            blockId = br.ObjectId;
            DynamicBlockReferencePropertyCollection properties =
              br.DynamicBlockReferencePropertyCollection;
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
              else if (prop.PropertyName == "View_Id")
              {
                prop.Value = ViewId;
              }
              else if (prop.PropertyName == "Id")
              {
                prop.Value = Guid.NewGuid().ToString();
              }
              else if (prop.PropertyName == "pos_x") {
                prop.Value = point.X;
              }
              else if (prop.PropertyName == "pos_y") {
                prop.Value = point.Y;
              }
            }
          }
          tr.Commit();
        }
      }
      SettingObjects = false;
    }

    [CommandMethod("Water")]
    public async void Water()
    {
      //MongoDBService.Initialize();
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      ProjectId = await MariaDBService.GetProjectId(projectNo);

      RetrieveOrCreateDrawingId();
      InitializeUserInterface();
      LoadDataAsync();

      pw.Focus();
    }

    public static void ZoomToBlock(Editor ed, BlockReference blockRef)
    {
      Extents3d ext = blockRef.GeometricExtents;
      using (ViewTableRecord view = ed.GetCurrentView())
      {
        view.CenterPoint = new Point2d(
          (ext.MinPoint.X + ext.MaxPoint.X) / 2,
          (ext.MinPoint.Y + ext.MaxPoint.Y) / 2
        );
        view.Height = ext.MaxPoint.Y * 50 - ext.MinPoint.Y * 50;
        view.Width = ext.MaxPoint.X * 50 - ext.MinPoint.X * 50;
        ed.SetCurrentView(view);
      }
    }

    public void WriteMessage(string message)
    {
      ed.WriteMessage(message);
    }

    private void AddArrowsToLine(ObjectId lineId, string lineGUID)
    {
      while (true)
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
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
          if (!bt.Has(blockName))
          {
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
          BlockReference arrowRef = new BlockReference(arrowPos, blockDefId)
          {
            Rotation = angle,
            Layer = line.Layer,
          };
          btr.AppendEntity(arrowRef);
          tr.AddNewlyCreatedDBObject(arrowRef, true);
          DynamicBlockReferencePropertyCollection properties =
            arrowRef.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in properties)
          {
            if (prop.PropertyName == "line_id")
            {
              prop.Value = lineGUID;
            }
          }
          tr.Commit();
        }
      }
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
            ed.WriteMessage(
              $"\nCreated new Drawing ID: {currentDrawingId}, Creation Time: {creationTime}"
            );
          }
          else
          {
            ed.WriteMessage(
              $"\nRetrieved existing Drawing ID: {currentDrawingId}, Creation Time: {creationTime}"
            );
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

    private void AttachRouteXData(ObjectId lineId, string id, string sourceId)
    {
      ed.WriteMessage("Id: " + id + " SourceId: " + sourceId);
      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        Line line = (Line)tr.GetObject(lineId, OpenMode.ForWrite);
        if (line == null)
          return;

        RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
        if (!regAppTable.Has(XRecordKey))
        {
          RegAppTableRecord regAppTableRecord = new RegAppTableRecord { Name = XRecordKey };
          regAppTable.Add(regAppTableRecord);
          tr.AddNewlyCreatedDBObject(regAppTableRecord, true);
        }
        ResultBuffer rb = new ResultBuffer(
          new TypedValue((int)DxfCode.ExtendedDataRegAppName, XRecordKey),
          new TypedValue(1000, id),
          new TypedValue(1000, sourceId)
        );
        line.XData = rb;
        rb.Dispose();
        tr.Commit();
      }
    }

    private void UpdateXRecordId(Transaction tr, string newId, DateTime newCreationTime)
    {
      DBDictionary namedObjDict = (DBDictionary)
        tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
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
      using (DocumentLock docLock = doc.LockDocument())
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          try
          {
            UpdateXRecordId(tr, newDrawingId, newCreationTime);
            currentDrawingId = newDrawingId;
            ed.WriteMessage(
              $"\nUpdated Drawing ID: {currentDrawingId}, New Creation Time: {newCreationTime}"
            );
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

    public void CreateXRecordId(Database db, Transaction tr, string drawingId)
    {
      RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
      if (!regAppTable.Has(XRecordKey))
      {
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

          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
            "\nSuccessfully loaded data from MongoDB.\n"
          );
        }
      }
      catch (System.Exception ex)
      {
        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
          $"\nError loading data from MongoDB: {ex.Message}\n"
        );
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
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
              "\nSuccessfully updated drawing data in MongoDB.\n"
            );
          }
          else
          {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
              "\nFailed to update drawing data in MongoDB. (possibly no data has changed since the last update)\n"
            );
          }
        }
        catch (System.Exception ex)
        {
          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
            $"\nError updating drawing data: {ex.Message}\n"
          );
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

    private void MakeCwDnLabel(Point3d dnPoint)
    {
      CADObjectCommands.CreateArrowJig("D0", dnPoint);
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        "3/4\"CW DN"
      );
    }

    private void MakeCwHwDnLabel(Point3d dnPoint, double rotation)
    {
      double distance = 3.9101;
      double x1 = dnPoint.X - (distance * Math.Cos(rotation));
      double y1 = dnPoint.Y - (distance * Math.Sin(rotation));
      CADObjectCommands.CreateArrowJig("D0", new Point3d(x1, y1, 0));
      double x2 = dnPoint.X + (distance * Math.Cos(rotation));
      double y2 = dnPoint.Y + (distance * Math.Sin(rotation));
      CADObjectCommands.CreateArrowJig("D0", new Point3d(x2, y2, 0), false);
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        "3/4\"CW&HW DN"
      );
    }

    public void MakeVentLabel(Point3d dnPoint)
    {
      CADObjectCommands.CreateArrowJig("D0", dnPoint);
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        "2\" UP ABV. CLG."
      );
    }

    public void MakeVerticalRouteLabel(Point3d dnPoint, string direction)
    {
      CADObjectCommands.CreateArrowJig("D0", dnPoint);
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        "PLMG " + direction
      );
    }

    private void MakePlumbingFixtureWaterGasLabel(PlumbingFixture fixture, PlumbingFixtureType type)
    {
      double distance = 3;
      double x = fixture.Position.X + (distance * Math.Sin(fixture.Rotation));
      double y = fixture.Position.Y - (distance * Math.Cos(fixture.Rotation));
      Point3d dnPoint = new Point3d(x, y, 0);
      switch (type.WaterGasBlockName)
      {
        case "GMEP CW DN":
          MakeCwDnLabel(dnPoint);
          break;
        case "GMEP CW HW DN":
          MakeCwHwDnLabel(dnPoint, fixture.Rotation);
          break;
      }
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        fixture.TypeAbbreviation + "-" + fixture.Number.ToString()
      );
    }

    private void MakePlumbingSourceLabel(PlumbingSource source, PlumbingSourceType type)
    {
      CADObjectCommands.CreateTextWithJig(
        CADObjectCommands.TextLayer,
        TextHorizontalMode.TextLeft,
        type.Type.ToUpper()
      );
    }

    private void MakePlumbingFixtureWasteVentLabel(
      PlumbingFixture fixture,
      Point3d position,
      string blockName,
      int index
    )
    {
      switch (blockName)
      {
        case "GMEP VENT":
          MakeVentLabel(position);
          break;
        case "GMEP WCO STRAIGHT":
        case "GMEP WCO ANGLED":
          CADObjectCommands.CreateTextWithJig(
            CADObjectCommands.TextLayer,
            TextHorizontalMode.TextLeft,
            "2\" WCO"
          );
          break;
        case "GMEP WCO FLOOR":
          CADObjectCommands.CreateTextWithJig(
            CADObjectCommands.TextLayer,
            TextHorizontalMode.TextLeft,
            "2\" GCO"
          );
          break;
      }
      if (index == 0)
      {
        CADObjectCommands.CreateTextWithJig(
          CADObjectCommands.TextLayer,
          TextHorizontalMode.TextLeft,
          fixture.TypeAbbreviation + "-" + fixture.Number.ToString()
        );
      }
    }

    [CommandMethod("PF")]
    [CommandMethod("PlumbingFixture")]
    public void PlumbingFixture()
    {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);
      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;

      List<PlumbingFixtureType> plumbingFixtureTypes = MariaDBService.GetPlumbingFixtureTypes();
      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
      keywordOptions.Message = "\nSelect fixture type:";

      plumbingFixtureTypes.ForEach(t =>
      {
        keywordOptions.Keywords.Add(t.Abbreviation + " - " + t.Name);
      });
      keywordOptions.Keywords.Default = "WC - Water Closet";
      keywordOptions.AllowNone = false;
      PromptResult keywordResult = ed.GetKeywords(keywordOptions);
      string keywordResultString = keywordResult.StringResult;
      PlumbingFixtureType selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t =>
        keywordResultString.StartsWith(t.Abbreviation)
      );
      if (selectedFixtureType == null)
      {
        selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t => t.Abbreviation == "WC");
      }
      List<PlumbingFixtureCatalogItem> plumbingFixtureCatalogItems =
        MariaDBService.GetPlumbingFixtureCatalogItemsByType(selectedFixtureType.Id);

      keywordOptions = new PromptKeywordOptions("");
      keywordOptions.Message = "\nSelect catalog item:";
      plumbingFixtureCatalogItems.ForEach(i =>
      {
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
      if (keywordResultString.Contains(' '))
      {
        keywordResultString = keywordResultString.Split(' ')[0];
      }
      PlumbingFixtureCatalogItem selectedCatalogItem = plumbingFixtureCatalogItems.FirstOrDefault(
        i => i.Id.ToString() == keywordResultString
      );

      if (selectedFixtureType.WaterGasBlockName.Contains("%WHSIZE%"))
      {
        if (selectedFixtureType.Abbreviation == "WH")
        {
          keywordOptions = new PromptKeywordOptions("");
          keywordOptions.Message = "\nSelect WH size";
          keywordOptions.Keywords.Add("50 gal.");
          keywordOptions.Keywords.Add("80 gal.");
          keywordOptions.Keywords.Default = "50 gal.";
          keywordOptions.AllowNone = false;
          keywordResult = ed.GetKeywords(keywordOptions);
          string whSize = keywordResult.StringResult;
          if (whSize.Contains(' '))
          {
            whSize = whSize.Split(' ')[0];
          }
          selectedFixtureType.WaterGasBlockName = selectedFixtureType.WaterGasBlockName.Replace(
            "%WHSIZE%",
            whSize
          );
        }
      }

      if (selectedFixtureType.WasteVentBlockName.Contains("%FSSIZE%"))
      {
        if (selectedFixtureType.Abbreviation == "FS")
        {
          keywordOptions = new PromptKeywordOptions("");
          keywordOptions.Message = "\nSelect FS size";
          keywordOptions.Keywords.Add("12\"");
          keywordOptions.Keywords.Add("6\"");
          keywordOptions.Keywords.Default = "12\"";
          keywordOptions.AllowNone = false;
          keywordResult = ed.GetKeywords(keywordOptions);
          string fsSize = keywordResult.StringResult.Replace("\"", "");
          if (fsSize.Contains(' '))
          {
            fsSize = fsSize.Split(' ')[0];
          }
          selectedFixtureType.WasteVentBlockName = selectedFixtureType.WasteVentBlockName.Replace(
            "%FSSIZE%",
            fsSize
          );
        }
      }

      if (!String.IsNullOrEmpty(selectedFixtureType.WaterGasBlockName))
      {
        ed.WriteMessage("\nSelect base point for " + selectedFixtureType.Name);
        ObjectId blockId;
        string blockName = selectedFixtureType.WaterGasBlockName;
        Point3d point;
        double rotation = 0;
        string fixtureId = Guid.NewGuid().ToString();
        try
        {
          using (Transaction tr = db.TransactionManager.StartTransaction())
          {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord btr;
            BlockReference br = CADObjectCommands.CreateBlockReference(
              tr,
              bt,
              blockName,
              out btr,
              out point
            );
            if (br != null)
            {
              BlockTableRecord curSpace = (BlockTableRecord)
                tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
              RotateJig rotateJig = new RotateJig(br);
              PromptResult rotatePromptResult = ed.Drag(rotateJig);

              if (rotatePromptResult.Status != PromptStatus.OK)
              {
                return;
              }
              rotation = br.Rotation;

              curSpace.AppendEntity(br);

              tr.AddNewlyCreatedDBObject(br, true);
            }

            blockId = br.Id;
            tr.Commit();
          }
          using (Transaction tr = db.TransactionManager.StartTransaction())
          {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
            var modelSpace = (BlockTableRecord)
              tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
            DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty prop in pc)
            {
              if (prop.PropertyName == "gmep_plumbing_fixture_id")
              {
                prop.Value = fixtureId;
              }
              if (prop.PropertyName == "gmep_plumbing_fixture_demand")
              {
                prop.Value = (double)selectedCatalogItem.FixtureDemand;
              }
              if (prop.PropertyName == "gmep_plumbing_fixture_hot_demand")
              {
                prop.Value = (double)selectedCatalogItem.HotDemand;
              }
            }
            tr.Commit();
          }
          PlumbingFixture plumbingFixture = new PlumbingFixture(
            fixtureId,
            projectId,
            point,
            rotation,
            selectedCatalogItem.Id,
            selectedFixtureType.Abbreviation,
            0
          );
          MariaDBService.CreatePlumbingFixture(plumbingFixture);
          if (selectedFixtureType.Abbreviation == "WH")
          {
            MariaDBService.CreatePlumbingSource(
              new PlumbingSource(
                Guid.NewGuid().ToString(),
                projectId,
                plumbingFixture.Position,
                selectedFixtureType.Id,
                plumbingFixture.Id
              )
            );
          }
          MakePlumbingFixtureWaterGasLabel(plumbingFixture, selectedFixtureType);
        }
        catch (System.Exception ex)
        {
          ed.WriteMessage(ex.ToString());
          Console.WriteLine(ex.ToString());
        }
      }
      if (!String.IsNullOrEmpty(selectedFixtureType.WasteVentBlockName))
      {
        string[] wasteVentBlockNames = selectedFixtureType.WasteVentBlockName.Split(',');
        int index = 0;
        Point3d ventPosition = new Point3d();
        foreach (string wasteVentBlockName in wasteVentBlockNames)
        {
          ed.WriteMessage("\nSelect base point for " + selectedFixtureType.Name);
          string blockName = wasteVentBlockName;
          double rotation = 0;
          string fixtureId = Guid.NewGuid().ToString();
          try
          {
            if (wasteVentBlockName == "GMEP VENT")
            {
              ventPosition = CreateVentBlock(
                selectedCatalogItem.FixtureDemand,
                projectId,
                selectedCatalogItem.Id,
                selectedFixtureType.Abbreviation,
                index
              );
            }
            else if (wasteVentBlockName == "GMEP DRAIN")
            {
              CreateDrainBlock(
                selectedCatalogItem.FixtureDemand,
                projectId,
                selectedCatalogItem.Id,
                selectedFixtureType.Abbreviation,
                index,
                ventPosition
              );
            }
            else
            {
              CreateWasteVentBlock(
                wasteVentBlockName,
                selectedCatalogItem.FixtureDemand,
                projectId,
                selectedCatalogItem.Id,
                selectedFixtureType.Abbreviation,
                index
              );
            }
            index++;
          }
          catch (System.Exception ex)
          {
            ed.WriteMessage(ex.ToString());
            Console.WriteLine(ex.ToString());
          }
        }
      }
    }

    [CommandMethod("PlumbingSource")]
    public void CreatePlumbingSource()
    {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);
      doc = Application.DocumentManager.MdiActiveDocument;
      db = doc.Database;
      ed = doc.Editor;

      //getting the base point for the plumbing source
      PromptEntityOptions promptOptions = new PromptEntityOptions(
        "\nSelect a basepoint for the plumbing source"
      );
      promptOptions.SetRejectMessage("\nSelected object is not a block reference.");
      promptOptions.AddAllowedClass(typeof(BlockReference), true);
      PromptEntityResult promptResult = ed.GetEntity(promptOptions);
      if (promptResult.Status != PromptStatus.OK) {
        ed.WriteMessage("\nCommand cancelled.");
        return;
      }
      ObjectId basePointId = promptResult.ObjectId;

      string basePointGUID = string.Empty;
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockReference basePointBlockRef = (BlockReference)tr.GetObject(basePointId, OpenMode.ForRead);
        if (basePointBlockRef == null) {
          ed.WriteMessage("\nInvalid object selected.");
          return;
        }

        bool isBasePointBlock = false;
        DynamicBlockReferencePropertyCollection pc = basePointBlockRef.DynamicBlockReferencePropertyCollection;
        foreach (DynamicBlockReferenceProperty prop in pc) {
          if (prop.PropertyName == "Id") {
            basePointGUID = prop.Value.ToString();
          }
          if (prop.PropertyName == "View_Id") {
            isBasePointBlock = true;
          }
        }
        if (!isBasePointBlock) {
          ed.WriteMessage("\nBlockreference is not a basepoint.");
          return;
        }
        tr.Commit();
      }

      List<PlumbingSourceType> plumbingSourceTypes = MariaDBService.GetPlumbingSourceTypes();
      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");

      keywordOptions.Message = "\nSelect fixture type:";

      plumbingSourceTypes.ForEach(t =>
      {
        keywordOptions.Keywords.Add(t.Id.ToString() + " " + t.Type);
      });
      keywordOptions.Keywords.Default = "1 Water Meter";
      keywordOptions.AllowNone = false;
      PromptResult keywordResult = ed.GetKeywords(keywordOptions);
      string keywordResultString = keywordResult.StringResult;

      PlumbingSourceType selectedSourceType = plumbingSourceTypes.FirstOrDefault(t =>
        keywordResultString == t.Id.ToString()
      );
      if (selectedSourceType == null)
      {
        selectedSourceType = plumbingSourceTypes.FirstOrDefault(t => t.Type == "Water Meter");
      }

      if (selectedSourceType.Type == "Water Heater")
      {
        ed.Command("PlumbingFixture", "WH");
        return;
      }

      ed.WriteMessage("\nSelect base point for plumbing source");
      ObjectId blockId;
      string blockName = "GMEP SOURCE";
      Point3d point;
      double rotation = 0;
      string sourceId = Guid.NewGuid().ToString();
      try
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            out btr,
            out point
          );
          if (br != null)
          {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            RotateJig rotateJig = new RotateJig(br);
            PromptResult rotatePromptResult = ed.Drag(rotateJig);

            if (rotatePromptResult.Status != PromptStatus.OK)
            {
              return;
            }
            rotation = br.Rotation;

            curSpace.AppendEntity(br);

            tr.AddNewlyCreatedDBObject(br, true);
          }

          blockId = br.Id;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc)
          {
            if (prop.PropertyName == "gmep_plumbing_source_id")
            {
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
          string.Empty
        );
        MariaDBService.CreatePlumbingSource(plumbingSource);
        MakePlumbingSourceLabel(plumbingSource, selectedSourceType);
      }
      catch (System.Exception ex)
      {
        ed.WriteMessage(ex.ToString());
        Console.WriteLine(ex.ToString());
      }
    }

    public Point3d CreateVentBlock(
      decimal fixtureDemand,
      string projectId,
      int selectedCatalogItemId,
      string selectedFixtureTypeAbbr,
      int index
    )
    {
      ed.WriteMessage("\nSelect base point for vent");
      ObjectId blockId;
      Point3d point;
      double rotation = 0;
      string fixtureId = Guid.NewGuid().ToString();
      string blockName = "GMEP VENT";
      try
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            out btr,
            out point
          );
          if (br != null)
          {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            RotateJig rotateJig = new RotateJig(br);
            PromptResult rotatePromptResult = ed.Drag(rotateJig);

            if (rotatePromptResult.Status != PromptStatus.OK)
            {
              return new Point3d();
            }
            rotation = br.Rotation;
            curSpace.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
          }
          blockId = br.Id;
          point = br.Position;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc)
          {
            if (prop.PropertyName == "gmep_plumbing_fixture_id")
            {
              prop.Value = fixtureId;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_dfu")
            {
              prop.Value = (double)fixtureDemand;
            }
          }
          tr.Commit();
          PlumbingFixture plumbingFixture = new PlumbingFixture(
            fixtureId,
            projectId,
            new Point3d(),
            rotation,
            selectedCatalogItemId,
            selectedFixtureTypeAbbr,
            0
          );
          MariaDBService.CreatePlumbingFixture(plumbingFixture);
          MakePlumbingFixtureWasteVentLabel(plumbingFixture, br.Position, blockName, index);
        }
        return point;
      }
      catch (System.Exception ex)
      {
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
      Point3d ventPosition
    )
    {
      ed.WriteMessage("\nSelect base point for drain");
      ObjectId blockId;
      Point3d point;
      string fixtureId = Guid.NewGuid().ToString();
      string blockName = "GMEP DRAIN";
      try
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            out btr,
            out point
          );
          if (br != null)
          {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
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
            line.StartPoint = startPoint;
            line.EndPoint = endPoint;
            line.Layer = "P-WV-W-BELOW";
            curSpace.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
          }

          blockId = br.Id;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc)
          {
            if (prop.PropertyName == "gmep_plumbing_fixture_id")
            {
              prop.Value = fixtureId;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_dfu")
            {
              prop.Value = (double)fixtureDemand;
            }
          }
          tr.Commit();
          PlumbingFixture plumbingFixture = new PlumbingFixture(
            fixtureId,
            projectId,
            new Point3d(),
            0,
            selectedCatalogItemId,
            selectedFixtureTypeAbbr,
            0
          );
          MariaDBService.CreatePlumbingFixture(plumbingFixture);
          MakePlumbingFixtureWasteVentLabel(plumbingFixture, br.Position, blockName, index);
        }
        return point;
      }
      catch (System.Exception ex)
      {
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
      int index
    )
    {
      ed.WriteMessage("\nSelect base point for " + selectedFixtureTypeAbbr);
      ObjectId blockId;
      Point3d point;
      double rotation = 0;
      string fixtureId = Guid.NewGuid().ToString();
      if (blockName.Contains("%WCOSTYLE%"))
      {
        PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
        keywordOptions.Message = "\nSelect WCO style";
        keywordOptions.Keywords.Add("STRAIGHT");
        keywordOptions.Keywords.Add("ANGLED");
        keywordOptions.Keywords.Add("FLOOR");
        keywordOptions.Keywords.Default = "STRAIGHT";
        keywordOptions.AllowNone = false;
        PromptResult keywordResult = ed.GetKeywords(keywordOptions);
        string wcoStyle = keywordResult.StringResult.Replace("\"", "");
        if (wcoStyle.Contains(' '))
        {
          wcoStyle = wcoStyle.Split(' ')[0];
        }
        blockName = blockName.Replace("%WCOSTYLE%", wcoStyle);
      }
      try
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord btr;
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            blockName,
            out btr,
            out point
          );
          if (br != null)
          {
            BlockTableRecord curSpace = (BlockTableRecord)
              tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            if (blockName != "GMEP WCO FLOOR")
            {
              RotateJig rotateJig = new RotateJig(br);
              PromptResult rotatePromptResult = ed.Drag(rotateJig);

              if (rotatePromptResult.Status != PromptStatus.OK)
              {
                return;
              }
              rotation = br.Rotation;
            }

            curSpace.AppendEntity(br);

            tr.AddNewlyCreatedDBObject(br, true);
          }

          blockId = br.Id;
          tr.Commit();
        }
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
          var modelSpace = (BlockTableRecord)
            tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
          BlockReference br = (BlockReference)tr.GetObject(blockId, OpenMode.ForWrite);
          DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in pc)
          {
            if (prop.PropertyName == "gmep_plumbing_fixture_id")
            {
              prop.Value = fixtureId;
            }
            if (prop.PropertyName == "gmep_plumbing_fixture_dfu")
            {
              prop.Value = (double)fixtureDemand;
            }
          }
          tr.Commit();
          PlumbingFixture plumbingFixture = new PlumbingFixture(
            fixtureId,
            projectId,
            br.Position,
            br.Rotation,
            selectedCatalogItemId,
            selectedFixtureTypeAbbr,
            0
          );
          MariaDBService.CreatePlumbingFixture(plumbingFixture);

          MakePlumbingFixtureWasteVentLabel(plumbingFixture, br.Position, blockName, index);
        }
      }
      catch (System.Exception ex)
      {
        ed.WriteMessage(ex.ToString());
        Console.WriteLine(ex.ToString());
      }
    }

    public static void Db_VerticalRouteErased(object sender, ObjectErasedEventArgs e)
    {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      try
      {
        if (
          e.Erased
          && !SettingObjects
          && !IsSaving
          && e.DBObject is BlockReference blockRef
          && IsVerticalRouteBlock(blockRef)
        )
        {
          ed.WriteMessage($"\nObject {e.DBObject.ObjectId} was erased.");

          string VerticalRouteId = string.Empty;
          var properties = blockRef.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in properties)
          {
            if (prop.PropertyName == "vertical_route_id")
            {
              VerticalRouteId = prop.Value?.ToString();
            }
          }
          if (!string.IsNullOrEmpty(VerticalRouteId))
          {
            SettingObjects = true;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
              BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
              List<string> blockNames = new List<string>
              {
                "GMEP_PLUMBING_LINE_UP",
                "GMEP_PLUMBING_LINE_DOWN",
                "GMEP_PLUMBING_LINE_VERTICAL",
              };
              foreach (var name in blockNames)
              {
                BlockTableRecord basePointBlock = (BlockTableRecord)
                  tr.GetObject(bt[name], OpenMode.ForWrite);
                foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
                {
                  if (id.IsValid)
                  {
                    using (
                      BlockTableRecord anonymousBtr =
                        tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord
                    )
                    {
                      if (anonymousBtr != null)
                      {
                        foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                        {
                          if (objId.IsValid)
                          {
                            var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;

                            var pc = entity.DynamicBlockReferencePropertyCollection;

                            foreach (DynamicBlockReferenceProperty prop in pc)
                            {
                              if (
                                prop.PropertyName == "vertical_route_id"
                                && prop.Value?.ToString() == VerticalRouteId
                              )
                              {
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
      catch (System.Exception ex)
      {
        ed.WriteMessage($"\nError in Db_ObjectErased: {ex.Message}");
      }
    }
 
    public static void Db_VerticalRouteModified(object sender, ObjectEventArgs e)
    {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      Dictionary<string, ObjectId> basePoints = new Dictionary<string, ObjectId>();
      if (
        !SettingObjects
        && !IsSaving
        && e.DBObject is BlockReference blockRef
        && IsVerticalRouteBlock(blockRef)
      )
      {
        SettingObjects = true;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
          BlockTableRecord basePointBlock = (BlockTableRecord)
            tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
          foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
          {
            if (id.IsValid)
            {
              using (
                BlockTableRecord anonymousBtr =
                  tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord
              )
              {
                if (anonymousBtr != null)
                {
                  foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                  {
                    var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                    var pc = entity.DynamicBlockReferencePropertyCollection;
                    foreach (DynamicBlockReferenceProperty prop in pc)
                    {
                      if (prop.PropertyName == "Id")
                      {
                        string basePointId = prop.Value?.ToString();
                        if (!string.IsNullOrEmpty(basePointId) && basePointId != "0")
                        {
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
        foreach (DynamicBlockReferenceProperty prop in properties)
        {
          if (prop.PropertyName == "vertical_route_id")
          {
            VerticalRouteId = prop.Value?.ToString();
          }
          if (prop.PropertyName == "base_point_id")
          {
            BasePointId = prop.Value?.ToString();
          }
        }
        if (BasePointId != "" && basePoints.ContainsKey(BasePointId))
        {
          ObjectId basePointIdObj = basePoints[BasePointId];

          using (Transaction tr = db.TransactionManager.StartTransaction())
          {
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

            foreach (var name in blockNames)
            {
              BlockTableRecord basePointBlock = (BlockTableRecord)
                tr.GetObject(bt[name], OpenMode.ForWrite);
              foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
              {
                if (id.IsValid)
                {
                  using (
                    BlockTableRecord anonymousBtr =
                      tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord
                  )
                  {
                    if (anonymousBtr != null)
                    {
                      foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                      {
                        if (objId.IsValid)
                        {
                          var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;

                          var pc = entity.DynamicBlockReferencePropertyCollection;

                          string BasePointId2 = string.Empty;
                          bool match = false;
                          foreach (DynamicBlockReferenceProperty prop in pc)
                          {
                            if (
                              prop.PropertyName == "vertical_route_id"
                              && prop.Value?.ToString() == VerticalRouteId
                            )
                            {
                              match = true;
                            }
                            if (prop.PropertyName == "base_point_id")
                            {
                              BasePointId2 = prop.Value?.ToString();
                            }
                          }
                          if (match)
                          {
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
            if (prop.PropertyName == "Id") {
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
        SettingObjects = true;

        string Id = string.Empty;
        Vector3d distanceVector = new Vector3d(0, 0, 0);


        var pc = blockRef.DynamicBlockReferencePropertyCollection;
        if (pc != null) {
          double pos_x = 0;
          double pos_y = 0;
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "Id") {
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


    private static bool IsVerticalRouteBlock(BlockReference blockRef)
    {
      foreach (
        DynamicBlockReferenceProperty prop in blockRef.DynamicBlockReferencePropertyCollection
      )
      {
        if (prop.PropertyName == "vertical_route_id")
          return true;
      }
      return false;
    }
    private static bool IsPlumbingBasePointBlock(BlockReference blockRef) {
      foreach (
        DynamicBlockReferenceProperty prop in blockRef.DynamicBlockReferencePropertyCollection
      ) {
        if (prop.PropertyName == "View_Id")
          return true;
      }
      return false;
    }

    public static async void Db_DocumentSaved(object sender, DatabaseIOEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      MariaDBService mariaDBService = new MariaDBService();

      try {
        string projectNo = CADObjectCommands.GetProjectNoFromFileName();
        string ProjectId = await mariaDBService.GetProjectId(projectNo);

        List<PlumbingHorizontalRoute> horizontalRoutes = GetHorizontalRoutesFromCAD(ProjectId);
        List<PlumbingVerticalRoute> verticalRoutes = GetVerticalRoutesFromCAD(ProjectId);
        List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD(ProjectId);

        await mariaDBService.UpdatePlumbingHorizontalRoutes(horizontalRoutes, ProjectId);
        await mariaDBService.UpdatePlumbingVerticalRoutes(verticalRoutes, ProjectId);
        await mariaDBService.UpdatePlumbingPlanBasePoints(basePoints, ProjectId);
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
              ResultBuffer xdata = line.GetXDataForApplication(XRecordKey);
              if (xdata != null && xdata.AsArray().Length > 2) {
                TypedValue[] values = xdata.AsArray();

                PlumbingHorizontalRoute route = new PlumbingHorizontalRoute(values[1].Value.ToString(), ProjectId, line.StartPoint, line.EndPoint, values[2].Value.ToString());
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
                      string SourceId = string.Empty;

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
                        if (prop.PropertyName == "source_id") {
                          SourceId = prop.Value?.ToString();
                        }
                      
                      }
                      if (Id != "0") {
                        PlumbingVerticalRoute route = new PlumbingVerticalRoute(
                          Id,
                          ProjectId,
                          entity.Position,
                          SourceId,
                          VerticalRouteId,
                          name
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

                    foreach (DynamicBlockReferenceProperty prop in pc) {
                        if (prop.PropertyName == "Floor") {
                          Floor = Convert.ToInt32(prop.Value);
                        }
                        if (prop.PropertyName == "Plan") {
                          Plan = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "Id") {
                          Id = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "Type") {
                          Type = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "View_Id") {
                          ViewId = prop.Value?.ToString();
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
                        Floor
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
}
