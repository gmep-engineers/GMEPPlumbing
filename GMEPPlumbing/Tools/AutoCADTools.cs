using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MongoDB.Driver.Core.Misc;
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

namespace GMEPPlumbing
{
  public class CADObjectCommands
  {
    public static double Scale { get; set; } = -1.0;

    public static string TextLayer = "P-HC-PPLM-TEXT";

    public static string ActiveBasePointId { get; set; } = "";

    public static double ActiveRouteHeight = -1;

    public static bool SettingFlag = false;

    public static double ActiveFloorHeight = 0;

    public static string ActiveViewName { get; set; } = "";

    public static int ActiveFloor { get; set; } = 0;

    public static bool IsEditing { get; set; } = false;

    public static List<string> ActiveViewTypes = new List<string>();

    //public static bool SettingFlag= false;

    [CommandMethod("SetPlumbingRouteHeight")]
    public static void SetPlumbingRouteHeight() {
      Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      Editor ed = doc.Editor;
      Database db = doc.Database;

      string GUID = GetActiveView();
      Tuple<double, double> heightLimits = GetHeightLimits(GUID);
      double routeHeight = 0;
      double? newHeight = null;

      // 1. Find the current route height (read-only, no transaction needed for just reading)
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (!id.IsValid) continue;
          var anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord;
          if (anonymousBtr == null) continue;
          foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
            var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
            if (entity == null) continue;
            var pc = entity.DynamicBlockReferencePropertyCollection;
            string basePointId = "";
            foreach (DynamicBlockReferenceProperty prop in pc) {
              if (prop.PropertyName == "id")
                basePointId = prop.Value.ToString();
            }
            if (basePointId == GUID) {
              foreach (DynamicBlockReferenceProperty prop in pc) {
                if (prop.PropertyName == "route_height" && prop.Value != null) {
                  routeHeight = Convert.ToDouble(prop.Value);
                  break;
                }
              }
            }
          }
        }
      }

      // 2. Prompt for new value (outside transaction)
      PromptDoubleOptions promptDoubleOptions = new PromptDoubleOptions("\nEnter the plumbing route height: ");
      promptDoubleOptions.AllowNegative = true;
      promptDoubleOptions.DefaultValue = routeHeight;

      while (true) {
        PromptDoubleResult promptDoubleResult = ed.GetDouble(promptDoubleOptions);
        if (promptDoubleResult.Status == PromptStatus.OK) {
          if (promptDoubleResult.Value > heightLimits.Item2 ||  promptDoubleResult.Value < heightLimits.Item1) {
            ed.WriteMessage($"\nHeight cannot be more than {heightLimits.Item2} or less than {heightLimits.Item1}.");
            promptDoubleOptions.Message = $"\nHeight cannot be more than {heightLimits.Item2} or less than {heightLimits.Item1}. Please enter a valid height: ";
            continue;
          }
          newHeight = promptDoubleResult.Value;
          break;
        }
        else if (promptDoubleResult.Status == PromptStatus.Cancel) {
          ed.WriteMessage("\nOperation cancelled.");
          return;
        }
        else {
          ed.WriteMessage("\nInvalid input. Please enter a valid height.");
          continue;
        }
      }

      if (!newHeight.HasValue)
        return;

      IsEditing = true;
      // 3. Now perform the write in a transaction
      using (DocumentLock docLock = doc.LockDocument())
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (!id.IsValid) continue;
          var anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord;
          if (anonymousBtr == null) continue;
          foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
            var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;
            if (entity == null) continue;
            var pc = entity.DynamicBlockReferencePropertyCollection;
            string basePointId = "";
            foreach (DynamicBlockReferenceProperty prop in pc) {
              if (prop.PropertyName == "id")
                basePointId = prop.Value.ToString();
            }
            if (basePointId == GUID) {;
              foreach (DynamicBlockReferenceProperty propWrite in pc) {
                if (propWrite.PropertyName == "route_height") {
                  propWrite.Value = newHeight.Value;
                  ActiveRouteHeight = newHeight.Value;
                  break;
                }
              }
            }
          }
        }
        tr.Commit();
      }
      IsEditing = false;
    }

    public static double GetPlumbingRouteHeight() {
      Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      Editor ed = doc.Editor;
      Database db = doc.Database;
      if (ActiveRouteHeight == -1) {
        SetActiveView();
      }
      return ActiveRouteHeight;
    }

    public static Tuple<double, double> GetHeightLimits(string GUID) {
      Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      Editor ed = doc.Editor;
      Database db = doc.Database;
      Tuple<double, double> heightLimits = new Tuple<double, double>(0, 0);

      int activefloor = 0;

      Dictionary<int, double> floorHeights = new Dictionary<int, double>();
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        // Dictionary<string, List<ObjectId>> basePoints = new Dictionary<string, List<ObjectId>>();
        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (id.IsValid) {
            using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord) {
              if (anonymousBtr != null) {
                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                  var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                  var pc = entity.DynamicBlockReferencePropertyCollection;
                  string basePointId = "";
                  double floorHeight = 0;
                  int floor = 0;
                  foreach (DynamicBlockReferenceProperty prop in pc) {
                    if (prop.PropertyName == "id") {
                      basePointId = prop.Value.ToString();
                    }
                    if (prop.PropertyName == "floor_height") {
                      floorHeight = Convert.ToDouble(prop.Value);
                    }
                    if (prop.PropertyName == "floor") {
                      floor = Convert.ToInt32(prop.Value);
                    }
                  }
                  if (basePointId != "") {
                    floorHeights[floor] = floorHeight;
                  }
                  if (basePointId == GUID) {
                    activefloor = floor;
                  }
                }
              }
            }
          }
        }
      }
      if (floorHeights.Count == 0) {
        ed.WriteMessage("\nNo base points found in the drawing.");
        return new Tuple<double, double>(0,0);
      }
      if (floorHeights.ContainsKey(activefloor)) {
        double upperHeightLimit = 0;
        double lowerHeightLimit = 0;
        if (activefloor != floorHeights.Count) {
          upperHeightLimit = floorHeights[activefloor + 1] - floorHeights[activefloor];
        }
        else {
          upperHeightLimit = 10000;
        }
        if (activefloor == 1) {
          lowerHeightLimit = -10000;
        }
        heightLimits = new Tuple<double, double>(
          lowerHeightLimit,
          upperHeightLimit
        );
      }
      else {
        ed.WriteMessage("\nNo height limit found for the active base point.");
        return new Tuple<double, double>(0, 0);
      }
      return heightLimits;
    }

    [CommandMethod("SetScale")]
    public static void SetScale()
    {
      var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      var ed = doc.Editor;

      var promptStringOptions = new PromptStringOptions(
        "\nEnter the scale value (e.g., 1/4, 3/16, 1/8): "
      );
      var promptStringResult = ed.GetString(promptStringOptions);

      if (promptStringResult.Status == PromptStatus.OK)
      {
        string scaleString = promptStringResult.StringResult;
        string[] scaleParts = scaleString.Split('/');

        if (
          scaleParts.Length == 2
          && double.TryParse(scaleParts[0], out double numerator)
          && double.TryParse(scaleParts[1], out double denominator)
        )
        {
          Scale = numerator / denominator;
          ed.WriteMessage($"\nScale set to {scaleString} ({Scale})");
        }
        else
        {
          ed.WriteMessage(
            $"\nInvalid scale format. Please enter the scale in the format 'numerator/denominator' (e.g., 1/4, 3/16, 1/8)."
          );
        }
      }
    }

    [CommandMethod("SetActiveView")]
    public static void SetActiveView() {
      Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      Editor ed = doc.Editor;
      Database db = doc.Database;

      List<ObjectId> basePointIds = new List<ObjectId>();

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
        Dictionary<string, List<ObjectId>> basePoints = new Dictionary<string, List<ObjectId>>();

        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (id.IsValid) {
            using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord) {
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
        //meow meow
        List<string> keywords = new List<string>();
        foreach (var key in basePoints.Keys) {
          var objId = basePoints[key][0];
          var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
          var pc = entity.DynamicBlockReferencePropertyCollection;
          string planName = "";
          string viewport = "";
          foreach (DynamicBlockReferenceProperty prop in pc) {
            if (prop.PropertyName == "plan") {
              planName = prop.Value.ToString();
            }
            if (prop.PropertyName == "type") {
              viewport = prop.Value.ToString();
            }
          }
          if (planName != "" && viewport != "") {
            string keyword = planName + ":" + viewport;
            if (!keywords.Contains(keyword)) {
              keywords.Add(keyword);
            }
            else {
              int count = keywords.Count(x => x == keyword || (x.StartsWith(keyword + "(") && x.EndsWith(")")));
              keywords.Add(keyword + "(" + (count + 1).ToString() + ")");
            }
          }
        }
        PromptKeywordOptions promptOptions = new PromptKeywordOptions("\nPick View: ");
        foreach (var keyword in keywords) {
          promptOptions.Keywords.Add(keyword);
        }
        PromptResult pr = ed.GetKeywords(promptOptions);
        string resultKeyword = pr.StringResult;
        int index = keywords.IndexOf(resultKeyword);
        basePointIds = basePoints.ElementAt(index).Value;

        //Picking start floor
        PromptKeywordOptions floorOptions = new PromptKeywordOptions("\nPick Floor: ");
        for (int i = 1; i <= basePointIds.Count; i++) {
          floorOptions.Keywords.Add(i.ToString());
        }
        PromptResult floorResult = ed.GetKeywords(floorOptions);
        int startFloor = int.Parse(floorResult.StringResult);


        foreach (ObjectId objId in basePointIds) {
          var entity2 = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
          var pc2 = entity2.DynamicBlockReferencePropertyCollection;

          int floor = 0;
          double floorHeight = 0;
          string basePointId = "";
          double routeHeight = 0;
          List<string> viewTypes = new List<string>();
          foreach (DynamicBlockReferenceProperty prop in pc2) {
            if (prop.PropertyName == "floor") {
              floor = Convert.ToInt32(prop.Value);
            }
            if (prop.PropertyName == "id") {
              basePointId = prop.Value.ToString();
            }
            if (prop.PropertyName == "floor_height") {
              floorHeight = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "route_height") {
              routeHeight = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "type") {
              if (prop.Value.ToString().Contains("Water")) {
                viewTypes.Add("Water");
              }
              if (prop.Value.ToString().Contains("Gas")) {
                viewTypes.Add("Gas");
              }
              if (prop.Value.ToString().Contains("Sewer-Vent")) {
                viewTypes.Add("Sewer-Vent");
              }
              if (prop.Value.ToString().Contains("Storm")) {
                viewTypes.Add("Storm");
              }
            }
          }
          if (floor == startFloor) {
            AutoCADIntegration.ZoomToBlock(ed, entity2);
            ActiveBasePointId = basePointId;
            ActiveFloorHeight = floorHeight;
            ActiveRouteHeight = routeHeight;
            ActiveFloor = floor;
            ActiveViewName = resultKeyword;
            ActiveViewTypes = viewTypes;
          }
        }
        tr.Commit();
      }
    }
    public static string GetActiveView() {
      Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      Editor ed = doc.Editor;
      Database db = doc.Database;
      if (string.IsNullOrEmpty(ActiveBasePointId)) {
        SetActiveView();
      }

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);

        foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds()) {
          if (id.IsValid) {
            using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord) {
              if (anonymousBtr != null) {
                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false)) {
                  var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                  var pc = entity.DynamicBlockReferencePropertyCollection;
                  foreach (DynamicBlockReferenceProperty prop in pc) {
                    if (prop.PropertyName == "Id") {
                      if (prop.Value.ToString() == ActiveBasePointId) {
                        AutoCADIntegration.ZoomToBlock(ed, entity);
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
      return ActiveBasePointId;
    }


    public static string GetProjectNoFromFileName()
    {
      Document doc = Autodesk
        .AutoCAD
        .ApplicationServices
        .Core
        .Application
        .DocumentManager
        .MdiActiveDocument;
      string fileName = Path.GetFileName(doc.Name);
      return Regex.Match(fileName, @"[0-9]{2}-[0-9]{3}").Value;
    }

    public static BlockReference CreateBlockReference(
      Transaction tr,
      BlockTable bt,
      string blockName,
      string name,
      out BlockTableRecord block,
      out Point3d point
    )
    {
      if (!bt.Has(blockName))
      {
        Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
          $"Block '{blockName}' not found in the BlockTable."
        );
        block = null;
        point = Point3d.Origin;
        return null;
      }
      block = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);

      BlockJig blockJig = new BlockJig(name);
      PromptResult res = blockJig.DragMe(block.ObjectId, out point);
      if (res.Status != PromptStatus.OK)
      {
        return null;
      }
      BlockReference br = new BlockReference(point, block.ObjectId);
      return br;
    }

    public static Point3d CreateArrowJig(
      string layerName,
      Point3d center,
      bool createHorizontalLeg = true
    )
    {
      Document acDoc = Autodesk
        .AutoCAD
        .ApplicationServices
        .Application
        .DocumentManager
        .MdiActiveDocument;
      Database acCurDb = acDoc.Database;
      Editor ed = acDoc.Editor;

      Point3d thirdClickPoint = Point3d.Origin;
      if (Scale == -1.0)
      {
        SetScale();
      }

      if (Scale == -1.0)
      {
        Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
          "Please set the scale using the SetScale command before creating objects."
        );
        return new Point3d();
      }
      using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
      {
        BlockTable acBlkTbl =
          acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;
        /*BlockTableRecord acBlkTblRec =
          acTrans.GetObject(acBlkTbl[$"ar{Scale}"], OpenMode.ForRead) as BlockTableRecord;*/
        BlockTableRecord acBlkTblRec =
          acTrans.GetObject(acBlkTbl[$"ar0.25"], OpenMode.ForRead) as BlockTableRecord;
        using (BlockReference acBlkRef = new BlockReference(Point3d.Origin, acBlkTblRec.ObjectId))
        {
          ArrowJig arrowJig = new ArrowJig(acBlkRef, center);
          PromptResult promptResult = ed.Drag(arrowJig);

          if (promptResult.Status == PromptStatus.OK)
          {
            BlockTableRecord currentSpace =
              acTrans.GetObject(acCurDb.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
            currentSpace.AppendEntity(acBlkRef);
            acTrans.AddNewlyCreatedDBObject(acBlkRef, true);

            acBlkRef.Layer = layerName;

            acTrans.Commit();

            Point3d firstClickPoint = arrowJig.LeaderPoint;

            Line line = new Line(arrowJig.LeaderPoint, arrowJig.InsertionPoint);
            line.Layer = layerName;

            Vector3d direction = arrowJig.InsertionPoint - arrowJig.LeaderPoint;
            double angle = direction.GetAngleTo(Vector3d.XAxis, Vector3d.ZAxis);
            using (Transaction tr = acDoc.Database.TransactionManager.StartTransaction())
            {
              BlockTableRecord btr = (BlockTableRecord)
                tr.GetObject(acDoc.Database.CurrentSpaceId, OpenMode.ForWrite);
              btr.AppendEntity(line);
              tr.AddNewlyCreatedDBObject(line, true);

              tr.Commit();
            }
            if (angle != 0 && angle != Math.PI && createHorizontalLeg)
            {
              DynamicLineJig lineJig = new DynamicLineJig(arrowJig.InsertionPoint, Scale);
              PromptResult dynaLineJigRes = ed.Drag(lineJig);
              if (dynaLineJigRes.Status == PromptStatus.OK)
              {
                using (Transaction tr = acDoc.Database.TransactionManager.StartTransaction())
                {
                  BlockTableRecord btr = (BlockTableRecord)
                    tr.GetObject(acDoc.Database.CurrentSpaceId, OpenMode.ForWrite);
                  btr.AppendEntity(lineJig.line);
                  tr.AddNewlyCreatedDBObject(lineJig.line, true);

                  thirdClickPoint = lineJig.line.EndPoint;
                  tr.Commit();
                }
              }
            }
          }
        }
      }
      return thirdClickPoint;
    }

    public static void CreateTextWithJig(
      string layerName,
      TextHorizontalMode horizontalMode,
      string defaultText = null
    )
    {
      Document acDoc = Autodesk
        .AutoCAD
        .ApplicationServices
        .Application
        .DocumentManager
        .MdiActiveDocument;
      Database acCurDb = acDoc.Database;
      Editor ed = acDoc.Editor;

      if (Scale == -1.0)
      {
        SetScale();
      }

      if (Scale == -1.0)
      {
        Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
          "Please set the scale using the SetScale command before creating objects."
        );
        return;
      }

      double baseScale = 1.0 / 4.0;
      double baseTextHeight = 4.5;
      double textHeight = (baseScale / Scale) * baseTextHeight;

      string userText = defaultText;

      if (string.IsNullOrEmpty(userText))
      {
        PromptStringOptions promptStringOptions = new PromptStringOptions("\nEnter the text: ");
        PromptResult promptResult = ed.GetString(promptStringOptions);

        if (promptResult.Status != PromptStatus.OK)
        {
          ed.WriteMessage("\nText input canceled.");
          return;
        }

        userText = promptResult.StringResult;
      }

      GeneralTextJig jig = new GeneralTextJig(userText, textHeight, horizontalMode);
      PromptResult pr = ed.Drag(jig);

      if (pr.Status == PromptStatus.OK)
      {
        using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
        {
          BlockTable acBlkTbl =
            acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;
          BlockTableRecord acBlkTblRec =
            acTrans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite)
            as BlockTableRecord;

          DBText dbText = new DBText
          {
            Position = jig.InsertionPoint,
            TextString = userText,
            Height = textHeight,
            HorizontalMode = horizontalMode,
            Layer = layerName,
          };

          if (horizontalMode != TextHorizontalMode.TextLeft)
          {
            dbText.AlignmentPoint = jig.InsertionPoint;
          }

          TextStyleTable tst =
            acTrans.GetObject(acCurDb.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
          if (tst.Has("rpm"))
          {
            dbText.TextStyleId = tst["rpm"];
          }
          else
          {
            ed.WriteMessage("\nText style 'rpm' not found.");
          }

          acBlkTblRec.AppendEntity(dbText);
          acTrans.AddNewlyCreatedDBObject(dbText, true);
          acTrans.Commit();
        }

        ed.WriteMessage(
          $"\nText '{userText}' created at {jig.InsertionPoint} with height {textHeight}."
        );
      }
      else
      {
        ed.WriteMessage("\nPoint selection canceled.");
      }
    }
  }
}
