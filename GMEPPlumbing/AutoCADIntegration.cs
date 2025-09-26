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
using Google.Protobuf.WellKnownTypes;
using GMEPPlumbing.Tools;
using MySqlX.XDevAPI.Common;
using Mysqlx.Session;
using Autodesk.AutoCAD.Windows.ToolPalette;
using GMEPPlumbing.Properties;
using System.Windows.Shapes;
using static Google.Protobuf.Compiler.CodeGeneratorResponse.Types;
using Microsoft.Extensions.Logging.Abstractions;

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
    //public Document doc { get; private set; }
    //public Database db { get; private set; }
    //public Editor ed { get; private set; }
    public string ProjectId { get; private set; } = string.Empty;
    public static bool IsSaving { get; private set; }
    public static bool SettingObjects { get; set; }
    private static readonly Dictionary<string, List<string>> pendingDuplicationRoutes = new Dictionary<string, List<string>>();
    
    private static readonly List<string> activePlacingDuplicationRoutes = new List<string>();

    public AutoCADIntegration() {
      SettingObjects = false;
      IsSaving = false;
    }
    private static void SaveStart(object s, EventArgs e) => IsSaving = true;
    private static void SaveEnd(object s, EventArgs e) => IsSaving = false;
    public static void AttachHandlers(Document doc) {
      var db = doc.Database;
      var ed = doc.Editor;

      // Prevent multiple attachments

      db.BeginSave -= SaveStart;
      db.SaveComplete -= SaveEnd;
      db.AbortSave -= SaveEnd;

      db.BeginSave += SaveStart;
      db.SaveComplete += SaveEnd;
      db.AbortSave += SaveEnd;

      db.ObjectErased -= Db_VerticalRouteErased;
      db.ObjectErased += Db_VerticalRouteErased;
      db.ObjectModified -= Db_VerticalRouteModified;
      db.ObjectModified += Db_VerticalRouteModified;
      db.ObjectModified -= Db_BasePointModified;
      db.ObjectModified += Db_BasePointModified;
      db.ObjectAppended -= Db_ObjectAppended;
      db.ObjectAppended += Db_ObjectAppended;
      db.SaveComplete -= Db_DocumentSaved;
      db.SaveComplete += Db_DocumentSaved;
      doc.CommandEnded -= Doc_CommandEnded;
      doc.CommandEnded += Doc_CommandEnded;
      // ... attach other handlers as needed ...
    }
    public static void DetachHandlers(Document doc) {
      var db = doc.Database;

      db.BeginSave -= SaveStart;
      db.SaveComplete -= SaveEnd;
      db.AbortSave -= SaveEnd;

      db.ObjectErased -= Db_VerticalRouteErased;
      db.ObjectModified -= Db_VerticalRouteModified;
      db.ObjectModified -= Db_BasePointModified;
      db.SaveComplete -= Db_DocumentSaved;
      db.ObjectAppended -= Db_ObjectAppended;
      doc.CommandEnded -= Doc_CommandEnded;
      // ... detach other handlers as needed ...
    }
    [CommandMethod("TestGasChart")]
    public void TestGasChart() {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;
      GasPipeSizingChart chart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 2);
      GasEntry entry = chart.GetData(300, 31);
      if (entry is SemiRigidCopperGasEntry copperEntry) {
        ed.WriteMessage($"\nNominal KL: {copperEntry.NominalKL}, Nominal ACR: {copperEntry.NominalACR}, Outside: {copperEntry.OutsideDiameter}, Inside: {copperEntry.InsideDiameter}\n");
      }
    }

    [CommandMethod("PlumbingHorizontalRoute")]
    public void PlumbingHorizontalRoute() {
     HorizontalRoute();
    }
    [CommandMethod("PlumbingHorizontalRouteGround")]
    public void PlumbingHorizontalRouteGround() {
      HorizontalRoute(0);
    }
    [CommandMethod("PlumbingHorizontalRouteFixtureHeight")]
    public void PlumbingHorizontalRouteFixtureHeight() {
      double routeHeight = CADObjectCommands.GetPlumbingRouteHeight();
      HorizontalRoute(routeHeight);
    }
    [CommandMethod("PlumbingHorizontalRouteVariable")]
    public void PlumbingHorizontalRouteVariable() {
      double routeHeight = CADObjectCommands.GetPlumbingRouteHeight();
      HorizontalRoute(-1);
    }
    public List<PlumbingHorizontalRoute> HorizontalRoute(double? routeHeight = null, string result = null, bool hasArrows = true, string direction = null, Point3d? startPoint = null, bool selectStart = true, string startMessage = "\nSpecify start point for route: ", string selectLinePointMessage = "\nSelect a Line: ", string endMessage = "\nSelect End Point: ", string fixtureDropId = null) {

      List<PlumbingHorizontalRoute> horizontalRoutes = new List<PlumbingHorizontalRoute>();
      string BasePointId = CADObjectCommands.GetActiveView();

      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return horizontalRoutes;

      var db = doc.Database;
      var ed = doc.Editor;
 
      //List<string> routeGUIDS = new List<string>();
      string layer = "Defpoints";

      if (result == null) {
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
          pko.Keywords.Add("GreaseWaste");
        }
        //pko.Keywords.Add("Storm");
        PromptResult pr = ed.GetKeywords(pko);
        if (pr.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return horizontalRoutes;
        }
        result = pr.StringResult;
      }


      switch (result) {
        case "Hot Water":
        case "HotWater":
          layer = "P-DOMW-HOTW";
          break;
        case "Cold Water":
        case "ColdWater":
          layer = "P-DOMW-CWTR";
          break;
        case "Gas":
          layer = "P-GAS";
          break;
        case "Waste":
          layer = "P-WV-W-BELOW";
          break;
        case "Grease Waste":
        case "GreaseWaste":
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
          return horizontalRoutes;
      }
      string pipeType = "";
      if (result == "ColdWater" || result == "HotWater") {
        if (CADObjectCommands.IsResidential) {
          PromptKeywordOptions pko1 = new PromptKeywordOptions("\nSelect Pipe Type: ");
          pko1.Keywords.Add("Copper", "CopperTypeL", "Copper Type L");
          pko1.Keywords.Add("CPVCSCH80", "CPVCSCH80", "CPVC SCH80");
          pko1.Keywords.Add("CPVCSDRII", "CPVCSDRII", "CPVC SDR II");
          pko1.Keywords.Add("PEX");
          PromptResult pr1 = ed.GetKeywords(pko1);
          if (pr1.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            return horizontalRoutes;
          }
          pipeType = pr1.StringResult;
        }
        else {
          {
            pipeType = "Copper";
          }
        }
      }
      /* else if (result == "Gas") {
         PromptKeywordOptions pko1 = new PromptKeywordOptions("\nSelect Pipe Type: ");
         pko1.Keywords.Add("Copper", "Semi-Rigid Copper Tubing", "Semi-Rigid Copper Tubing");
         pko1.Keywords.Add("Metal", "Schedule 40 Metallic Pipe", "Schedule 40 Metallic Pipe");
         pko1.Keywords.Add("Steel", "Corrugated Stainless Steel Tubing", "Corrugated Stainless Steel Tubing");
         pko1.Keywords.Add("Plastic","Polyethylene Plastic Pipe", "Polyethylene Plastic Pipe");
         PromptResult pr1 = ed.GetKeywords(pko1);
         if (pr1.Status != PromptStatus.OK) {
           ed.WriteMessage("\nCommand cancelled.");
           return;
         }
         pipeType = pr1.StringResult;
       }*/



      if (direction == null) {
        PromptKeywordOptions pko2 = new PromptKeywordOptions("\nForward or Backward?");
        pko2.Keywords.Add("Forward");
        pko2.Keywords.Add("Backward");
        PromptResult pr2 = ed.GetKeywords(pko2);
        if (pr2.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return horizontalRoutes;
        }
        direction = pr2.StringResult;
      }

      if (routeHeight == null) {
        if (result != "Waste" && result != "GreaseWaste") {
          routeHeight = CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight;
        }
        else {
          routeHeight = 0;
        }
      }
      else if (routeHeight == -1) {
        PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the horizontal route from the floor (in feet): ");
        pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();
        while (true) {
          try {
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            if (pdr.Status == PromptStatus.Cancel) {
              ed.WriteMessage("\nCommand cancelled.");
              return horizontalRoutes;
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
            break;
          }
          catch (System.Exception ex) {
            ed.WriteMessage($"\nError: {ex.Message}");
            continue;
          }
        }
      }


      double zIndex = ((double)routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;

      //Beginning display
      var routeHeightDisplay = new RouteHeightDisplay(ed);
      routeHeightDisplay.Enable((double)routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);

      double slope = 0;
      if (result == "Waste" || result == "Vent" || result == "GreaseWaste") {
        PromptKeywordOptions pko3 = new PromptKeywordOptions("\nWhat is the slope? (1% or 2%)");
        pko3.Keywords.Add("1%");
        pko3.Keywords.Add("2%");
        PromptResult pr3 = ed.GetKeywords(pko3);
        if (pr3.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          routeHeightDisplay.Disable();
          return horizontalRoutes;
        }
        if (pr3.StringResult == "1%") {
          slope = 0.01;
        }
        else if (pr3.StringResult == "2%") {
          slope = 0.02;
        }
      }
      if (selectStart) {
        if (startPoint == null) {
          PromptPointOptions ppo2 = new PromptPointOptions(startMessage);
          ppo2.AllowNone = false;
          PromptPointResult ppr2 = ed.GetPoint(ppo2);
          if (ppr2.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            routeHeightDisplay.Disable();
            return horizontalRoutes;
          }

          startPoint = ppr2.Value;
        }
        Point3d startPointLocation2 = (Point3d)startPoint;

        ObjectId addedLineId2 = ObjectId.Null;
        string LineGUID2 = Guid.NewGuid().ToString();

        HorizontalRouteJig routeJig = new HorizontalRouteJig(startPointLocation2, layer, endMessage);

        PromptResult routeResult = ed.Drag(routeJig);
        if (routeResult.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          routeHeightDisplay.Disable();
          return horizontalRoutes;
        }

        Point3d endPointLocation2 = routeJig.line.EndPoint;

        using (Transaction tr2 = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr2.GetObject(db.BlockTableId, OpenMode.ForWrite);
          BlockTableRecord btr = (BlockTableRecord)
            tr2.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

          Line line = new Line();
          if (direction == "Forward") {
            line.StartPoint = new Point3d(startPointLocation2.X, startPointLocation2.Y, zIndex);
            line.EndPoint = new Point3d(endPointLocation2.X, endPointLocation2.Y, zIndex);
          }
          else if (direction == "Backward") {
            line.StartPoint = new Point3d(endPointLocation2.X, endPointLocation2.Y, zIndex);
            line.EndPoint = new Point3d(startPointLocation2.X, startPointLocation2.Y, zIndex);
          }

          PlumbingHorizontalRoute firstRoute = new PlumbingHorizontalRoute(
            LineGUID2,
            ProjectId,
            result,
            line.StartPoint,
            line.EndPoint,
            BasePointId,
            pipeType,
            slope
          );
          horizontalRoutes.Add(firstRoute);

          line.Layer = layer;
          btr.AppendEntity(line);
          tr2.AddNewlyCreatedDBObject(line, true);
          addedLineId2 = line.ObjectId;
          tr2.Commit();
        }

        //routeGUIDS.Add(LineGUID2);
        AttachRouteXData(addedLineId2, LineGUID2, BasePointId, pipeType, slope, fixtureDropId);
        if (hasArrows) {
          AddArrowsToLine(addedLineId2, LineGUID2);
        }
      }

      while (true) {

        slope = 0;
        if (result == "Waste" || result == "Vent" || result == "GreaseWaste") {
          PromptKeywordOptions pko3 = new PromptKeywordOptions("\nWhat is the slope? (1% or 2%)");
          pko3.Keywords.Add("1%");
          pko3.Keywords.Add("2%");
          PromptResult pr3 = ed.GetKeywords(pko3);
          if (pr3.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            routeHeightDisplay.Disable();
            break;
          }
          if (pr3.StringResult == "1%") {
            slope = 0.01;
          }
          else if (pr3.StringResult == "2%") {
            slope = 0.02;
          }
        }

        //Select a starting point/object
        PromptEntityOptions peo = new PromptEntityOptions(selectLinePointMessage);
        peo.SetRejectMessage("\nSelect a line");
        peo.AddAllowedClass(typeof(Line), true);
        PromptEntityResult per = ed.GetEntity(peo);

        if (per.Status != PromptStatus.OK || per.Status == PromptStatus.Cancel || per.ObjectId == ObjectId.Null) {
          ed.WriteMessage("\nCommand cancelled.");
          routeHeightDisplay.Disable();
          break;
        }

       
        ObjectId basePointId = per.ObjectId;

        Point3d startPointLocation = Point3d.Origin;
        Point3d endPointLocation3 = Point3d.Origin;
        ObjectId addedLineId = ObjectId.Null;

        string LineGUID = Guid.NewGuid().ToString();

        // Check if the selected object is a BlockReference or Line
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          Entity basePoint = (Entity)tr.GetObject(basePointId, OpenMode.ForRead);

          //get line choice
          if (basePoint is Line basePointLine) {
            //retrieving the lines xdata
            ResultBuffer xData = basePointLine.GetXDataForApplication(XRecordKey);
            if (xData == null || xData.AsArray().Length < 5) {
              ed.WriteMessage("\nSelected line does not have the required XData.");
              continue;
            }
            TypedValue[] values = xData.AsArray();
            string basePointGuid = values[2].Value as string;
            if (basePointLine.Layer != layer || basePointGuid != CADObjectCommands.GetActiveView()) {
              ed.WriteMessage("\nSelected line is not valid.");
              continue;
            }

            //Placing Line
            LineStartPointPreviewJig jig = new LineStartPointPreviewJig(basePointLine);
            PromptResult jigResult = ed.Drag(jig);
            startPointLocation = jig.ProjectedPoint;
            layer = basePointLine.Layer;
          }

          HorizontalRouteJig routeJig2 = new HorizontalRouteJig(startPointLocation, layer);

          while (true) {
            PromptResult routeResult2 = ed.Drag(routeJig2);
            if (routeResult2.Status != PromptStatus.OK) {
              ed.WriteMessage("\nCommand cancelled.");
              routeHeightDisplay.Disable();
              return horizontalRoutes;
            }
            Point3d endPoint = routeJig2.line.EndPoint;

            if (layer == "P-GAS" && basePoint is Line basePointLine2) {
              Vector3d prevDir = (basePointLine2.EndPoint - basePointLine2.StartPoint).GetNormal();
              Vector3d newDir = (endPoint - startPointLocation).GetNormal();
              double angle = prevDir.GetAngleTo(newDir);

              if (angle > Math.PI / 4) {
                ed.WriteMessage("\nAngle exceeds 45 degrees. Please pick a point closer to the previous direction.");
                routeJig2.message = $"\nAngle is {angle}. Next Line must be 45 degrees or less.";
                continue;
              }
            }

            endPointLocation3 = endPoint;
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
          PlumbingHorizontalRoute nextRoute = new PlumbingHorizontalRoute(
            LineGUID,
            ProjectId,
            result,
            line.StartPoint,
            line.EndPoint,
            BasePointId,
            pipeType,
            slope
          );
          horizontalRoutes.Add(nextRoute);

          line.Layer = layer;
          btr.AppendEntity(line);
          tr.AddNewlyCreatedDBObject(line, true);
          addedLineId = line.ObjectId;

          tr.Commit();
        }
        
        //routeGUIDS.Add(LineGUID);
        AttachRouteXData(addedLineId, LineGUID, BasePointId, pipeType, slope, fixtureDropId);
        if (hasArrows) {
          AddArrowsToLine(addedLineId, LineGUID);
        }  
      }
      routeHeightDisplay.Disable();
      return horizontalRoutes;
    }
    public async void SpecializedHorizontalRoute(string type, string pipeType, double height, Point3d startPoint, Point3d? endPoint = null, string fixtureDropId = "") {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var db = doc.Database;
      var ed = doc.Editor;
      //double routeHeight = CADObjectCommands.GetPlumbingRouteHeight();
      double zIndex = (height + CADObjectCommands.ActiveFloorHeight) * 12;
      ObjectId addedLineId = ObjectId.Null;
      string LineGUID = Guid.NewGuid().ToString();

      string layer = "";

      switch (type) {
        case "Hot Water":
        case "HotWater":
          layer = "P-DOMW-HOTW";
          break;
        case "Cold Water":
        case "ColdWater":
          layer = "P-DOMW-CWTR";
          break;
        case "Gas":
          layer = "P-GAS";
          break;
        case "Grease Waste":
        case "GreaseWaste":
          layer = "P-GREASE-WASTE";
          break;
        case "Waste":
          layer = "P-WV-W-BELOW";
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

      if (endPoint == null) {
        HorizontalRouteJig jig = new HorizontalRouteJig(startPoint, layer);
        PromptResult jigResult = ed.Drag(jig);
        if (jigResult.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return;
        }
        endPoint = jig.line.EndPoint;
      }

      Point3d endPointTemp = (Point3d)endPoint;

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
        BlockTableRecord btr = (BlockTableRecord)
          tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        Line line = new Line();
        line.StartPoint = new Point3d(startPoint.X, startPoint.Y, zIndex);
        line.EndPoint = new Point3d(endPointTemp.X, endPointTemp.Y, zIndex);

       

        line.Layer = layer;
        btr.AppendEntity(line);
        tr.AddNewlyCreatedDBObject(line, true);
        addedLineId = line.ObjectId;
        tr.Commit();
      }
      double slope = 0;
      if (type == "Waste" || type == "Vent" || type == "GreaseWaste") {
        PromptKeywordOptions pko3 = new PromptKeywordOptions("\nWhat is the slope? (1% or 2%)");
        pko3.Keywords.Add("1%");
        pko3.Keywords.Add("2%");
        PromptResult pr3 = ed.GetKeywords(pko3);
        if (pr3.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return;
        }
        if (pr3.StringResult == "1%") {
          slope = 0.01;
        }
        else if (pr3.StringResult == "2%") {
          slope = 0.02;
        }
      }
      AttachRouteXData(addedLineId, LineGUID, CADObjectCommands.GetActiveView(), pipeType, slope, fixtureDropId);
      //AddArrowsToLine(addedLineId, LineGUID);
    }

    [CommandMethod("PlumbingVerticalRoute")]
    public async void PlumbingVerticalRoute() {
      // Call the method with a null parameter to avoid ambiguity
      VerticalRoute();
    }
    public Dictionary<string, PlumbingVerticalRoute> VerticalRoute(string type = null, double? routeHeight = null, int? endFloor = null, string direction = null, double? length = null, double? endFloorHeight = null, string message = "Vertical Route", string fixtureType = "") {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return null;

      var db = doc.Database;
      var ed = doc.Editor;


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
      bool isUp = false;
      Dictionary<int, double> floorHeights = new Dictionary<int, double>();
      Dictionary<string, PlumbingVerticalRoute> verticalRoutes = new Dictionary<string, PlumbingVerticalRoute>();

      if (type == null) {
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
          pko.Keywords.Add("GreaseWaste");
        }
        //pko.Keywords.Add("Storm");
        PromptResult pr = ed.GetKeywords(pko);
        if (pr.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return null;
        }
        type = pr.StringResult;
      }

      switch (type) {
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
          layer = "P-WV-W-BELOW";
          break;
        case "GreaseWaste":
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
          return null;
      }

      string pipeType = "";
      if (type == "ColdWater" || type == "HotWater") {
        if (CADObjectCommands.IsResidential) {
          PromptKeywordOptions pko1 = new PromptKeywordOptions("\nSelect Pipe Type: ");
          pko1.Keywords.Add("Copper", "CopperTypeL", "Copper Type L");
          pko1.Keywords.Add("CPVCSCH80", "CPVCSCH80", "CPVC SCH80");
          pko1.Keywords.Add("CPVCSDRII", "CPVCSDRII", "CPVC SDR II");
          pko1.Keywords.Add("PEX");
          PromptResult pr1 = ed.GetKeywords(pko1);
          if (pr1.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            return null;
          }
          pipeType = pr1.StringResult;
        }
        else 
        {
            pipeType = "Copper";
        }
      }
     /* else if (type == "Gas") {
        PromptKeywordOptions pko1 = new PromptKeywordOptions("\nSelect Pipe Type: ");
        pko1.Keywords.Add("Copper", "Semi-Rigid Copper Tubing", "Semi-Rigid Copper Tubing");
        pko1.Keywords.Add("Metal", "Schedule 40 Metallic Pipe", "Schedule 40 Metallic Pipe");
        pko1.Keywords.Add("Steel", "Corrugated Stainless Steel Tubing", "Corrugated Stainless Steel Tubing");
        pko1.Keywords.Add("Plastic", "Polyethylene Plastic Pipe", "Polyethylene Plastic Pipe");
        PromptResult pr1 = ed.GetKeywords(pko1);
        if (pr1.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return null;
        }
        pipeType = pr1.StringResult;
      }*/


      if (routeHeight == null) {
        PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the vertical route from the floor (in feet): ");
        pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();
        routeHeight = 0;
        while (true) {
          try {
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            if (pdr.Status == PromptStatus.Cancel) {
              ed.WriteMessage("\nCommand cancelled.");
              return null;
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
      }
      double zIndex = ((double)routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;

      //beginning display
      var routeHeightDisplay = new RouteHeightDisplay(ed);
      routeHeightDisplay.Enable((double)routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);

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

                  string key = "";
                  bool isSite = false;
                  bool isSiteRef = false;
                  foreach (DynamicBlockReferenceProperty prop in pc) {
                    if (prop.PropertyName == "view_id") {
                      key = prop.Value.ToString();
                    }
                    if (prop.PropertyName == "is_site") {
                      isSite = Convert.ToDouble(prop.Value) == 1;
                    }
                    if (prop.PropertyName == "is_site_ref") {
                      isSiteRef = Convert.ToDouble(prop.Value) == 1;
                    }
                  }
                  if (key != "0" && !isSiteRef) {
                    if (CADObjectCommands.ActiveIsSite == isSite) {
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
            message,
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
            return null;
          }
        }

        tr.Commit();
      }
      //getting difference between start base point and up point
      Vector3d upVector = StartUpLocation - StartBasePointLocation;

      //picking end floor
      if (endFloor == null) {
        PromptKeywordOptions endFloorOptions = new PromptKeywordOptions("\nEnding Floor: ");
        for (int i = 1; i <= basePointIds.Count; i++) {
          endFloorOptions.Keywords.Add(i.ToString());
        }
        PromptResult endFloorResult = ed.GetKeywords(endFloorOptions);
        if (endFloorResult.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          routeHeightDisplay.Disable();
          return null;
        }
        endFloor = int.Parse(endFloorResult.StringResult);
      }

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
        isUp = true;
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
            return null;
          }
          upBlockRef2.Position = new Point3d(newUpPointLocation2.X, newUpPointLocation2.Y, zIndex);
          labelPoint = upBlockRef2.Position;

          upBlockRef2.Layer = layer;
          curSpace2.AppendEntity(upBlockRef2);
          tr.AddNewlyCreatedDBObject(upBlockRef2, true);

          // Attach the vertical route ID to the start pipe
          var pc2 = upBlockRef2.DynamicBlockReferencePropertyCollection;


          PlumbingVerticalRoute newRoute = new PlumbingVerticalRoute();
          foreach (DynamicBlockReferenceProperty prop in pc2) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
              newRoute.Id = prop.Value.ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[startFloor];
              newRoute.BasePointId = prop.Value.ToString();
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
              newRoute.VerticalRouteId = prop.Value.ToString();
            }
            if (prop.PropertyName == "length") {
              prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item2 - routeHeight;
              newRoute.Length = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = routeHeight;
              newRoute.StartHeight = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "pipe_type") {
              prop.Value = pipeType;
              newRoute.PipeType = prop.Value.ToString();
            }
            if (prop.PropertyName == "is_up") {
              prop.Value = isUp ? 1 : 0;
              newRoute.IsUp = isUp;
            }
            if (prop.PropertyName == "fixture_type") {
              prop.Value = fixtureType;
              newRoute.FixtureType = prop.Value.ToString();
            }
          }
          newRoute.Type = type;
          newRoute.ProjectId = ProjectId;
          newRoute.Position = upBlockRef2.Position;
          newRoute.Rotation = upBlockRef2.Rotation;
          verticalRoutes.Add(newRoute.BasePointId, newRoute);

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

            PlumbingVerticalRoute newRoute = new PlumbingVerticalRoute();

            foreach (DynamicBlockReferenceProperty prop in pc2) {
              if (prop.PropertyName == "id") {
                prop.Value = Guid.NewGuid().ToString();
                newRoute.Id = prop.Value.ToString();
              }
              if (prop.PropertyName == "base_point_id") {
                prop.Value = BasePointGUIDs[i];
                newRoute.BasePointId = prop.Value.ToString();
              }
              if (prop.PropertyName == "vertical_route_id") {
                prop.Value = verticalRouteId;
                newRoute.VerticalRouteId = prop.Value.ToString();
              }
              if (prop.PropertyName == "length") {
                prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[i]).Item2;
                newRoute.Length = Convert.ToDouble(prop.Value);
              }
              if (prop.PropertyName == "pipe_type") {
                prop.Value = pipeType;
                newRoute.PipeType = prop.Value.ToString();
              }
              if (prop.PropertyName == "is_up") {
                prop.Value = isUp ? 1 : 0;
                newRoute.IsUp = isUp;
              }
              if (prop.PropertyName == "fixture_type") {
                prop.Value = fixtureType;
                newRoute.FixtureType = prop.Value.ToString();
              }

            }
            newRoute.Type = type;
            newRoute.ProjectId = ProjectId;
            newRoute.Position = upBlockRef.Position;
            newRoute.Rotation = upBlockRef.Rotation;
            verticalRoutes.Add(newRoute.BasePointId, newRoute);
          }

          //end pipe

          ZoomToBlock(ed, BasePointRefs[(int)endFloor]);
          var promptDoubleOptions = new PromptDoubleOptions("\nEnter the height of the start of the vertical route from the floor (in feet): ");
          promptDoubleOptions.AllowNegative = false;
          promptDoubleOptions.AllowZero = false;
          promptDoubleOptions.DefaultValue = 0;

          if (endFloorHeight == null) {
            while (true) {
              PromptDoubleResult promptDoubleResult = ed.GetDouble(promptDoubleOptions);
              if (promptDoubleResult.Status == PromptStatus.OK) {
                endFloorHeight = promptDoubleResult.Value;
                Tuple<double, double> heightLimits = CADObjectCommands.GetHeightLimits(BasePointGUIDs[(int)endFloor]);
                double upperHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[(int)endFloor]).Item2;
                double lowerHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[(int)endFloor]).Item1;
                if (endFloorHeight > upperHeightLimit || endFloorHeight < lowerHeightLimit) {
                  ed.WriteMessage($"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.");
                  promptDoubleOptions.Message = $"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.";
                  continue;
                }
                else if (promptDoubleResult.Status == PromptStatus.Cancel) {
                  ed.WriteMessage("\nCommand cancelled.");
                  return null;
                }
                else if (promptDoubleResult.Status == PromptStatus.Error) {
                  ed.WriteMessage("\nError in input. Please try again.");
                  continue;
                }
                break;
              }
            }
          }


          Point3d newUpPointLocation3 = BasePointRefs[(int)endFloor].Position + upVector;
          BlockTableRecord blockDef3 = tr.GetObject(bt["GMEP_PLUMBING_LINE_DOWN"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace3 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
          labelPoint2 = upBlockRef3.Position;
          RotateJig rotateJig2 = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
          if (rotatePromptResult2.Status != PromptStatus.OK) {
            return null;
          }
          upBlockRef3.Position = new Point3d(newUpPointLocation3.X, newUpPointLocation3.Y, (floorHeights[(int)endFloor] + (double)endFloorHeight) * 12);

          upBlockRef3.Layer = layer;
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;
          PlumbingVerticalRoute newRoute2 = new PlumbingVerticalRoute();

          foreach (DynamicBlockReferenceProperty prop in pc3) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
              newRoute2.Id = prop.Value.ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[(int)endFloor];
              newRoute2.BasePointId = prop.Value.ToString();
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
              newRoute2.VerticalRouteId = prop.Value.ToString();
            }
            if (prop.PropertyName == "length") {
              prop.Value = endFloorHeight;
              newRoute2.Length = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = endFloorHeight;
              newRoute2.StartHeight = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "pipe_type") {
              prop.Value = pipeType;
              newRoute2.PipeType = prop.Value.ToString();
            }
            if (prop.PropertyName == "is_up") {
              prop.Value = isUp ? 1 : 0;
              newRoute2.IsUp = isUp;
            }
            if (prop.PropertyName == "fixture_type") {
              prop.Value = fixtureType;
              newRoute2.FixtureType = prop.Value.ToString();
            }
          }
          newRoute2.Type = type;
          newRoute2.ProjectId = ProjectId;
          newRoute2.Position = upBlockRef3.Position;
          newRoute2.Rotation = upBlockRef3.Rotation;
          verticalRoutes.Add(newRoute2.BasePointId, newRoute2);
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint2, "UP FROM LOWER");
      }
      else if (endFloor < startFloor) {
        isUp = false;
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
            return null;
          }
          upBlockRef2.Position = new Point3d(newUpPointLocation2.X, newUpPointLocation2.Y, zIndex);
          upBlockRef2.Layer = layer;
          curSpace2.AppendEntity(upBlockRef2);
          tr.AddNewlyCreatedDBObject(upBlockRef2, true);
          labelPoint = upBlockRef2.Position;

          var pc2 = upBlockRef2.DynamicBlockReferencePropertyCollection;
          PlumbingVerticalRoute newRoute = new PlumbingVerticalRoute();
          foreach (DynamicBlockReferenceProperty prop in pc2) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
              newRoute.Id = prop.Value.ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[startFloor];
              newRoute.BasePointId = prop.Value.ToString();
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
              newRoute.VerticalRouteId = prop.Value.ToString();
            }
            if (prop.PropertyName == "length") {
              prop.Value = routeHeight;
              newRoute.Length = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = routeHeight;
              newRoute.StartHeight = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "pipe_type") {
              prop.Value = pipeType;
              newRoute.PipeType = prop.Value.ToString();
            }
            if (prop.PropertyName == "is_up") {
              prop.Value = isUp ? 1 : 0;
              newRoute.IsUp = isUp;
            }
            if (prop.PropertyName == "fixture_type") {
              prop.Value = fixtureType;
              newRoute.FixtureType = prop.Value.ToString();
            }
          }
          newRoute.Type = type;
          newRoute.ProjectId = ProjectId;
          newRoute.Position = upBlockRef2.Position;
          newRoute.Rotation = upBlockRef2.Rotation;
          verticalRoutes.Add(newRoute.BasePointId, newRoute);
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

            PlumbingVerticalRoute newRoute = new PlumbingVerticalRoute();
            foreach (DynamicBlockReferenceProperty prop in pc) {
              if (prop.PropertyName == "id") {
                prop.Value = Guid.NewGuid().ToString();
                newRoute.Id = prop.Value.ToString();
              }
              if (prop.PropertyName == "base_point_id") {
                prop.Value = BasePointGUIDs[i];
                newRoute.BasePointId = prop.Value.ToString();
              }
              if (prop.PropertyName == "vertical_route_id") {
                prop.Value = verticalRouteId;
                newRoute.VerticalRouteId = prop.Value.ToString();
              }
              if (prop.PropertyName == "length") {
                prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[i]).Item2;
                newRoute.Length = Convert.ToDouble(prop.Value);
              }
              if (prop.PropertyName == "pipe_type") {
                prop.Value = pipeType;
                newRoute.PipeType = prop.Value.ToString();
              }
              if (prop.PropertyName == "is_up") {
                prop.Value = isUp ? 1 : 0;
                newRoute.IsUp = isUp;
              }
              if (prop.PropertyName == "fixture_type") {
                prop.Value = fixtureType;
                newRoute.FixtureType = prop.Value.ToString();
              }
            }
            newRoute.Type = type;
            newRoute.ProjectId = ProjectId;
            newRoute.Position = upBlockRef.Position;
            newRoute.Rotation = upBlockRef.Rotation;
            verticalRoutes.Add(newRoute.BasePointId, newRoute);
          }

          //end pipe
          ZoomToBlock(ed, BasePointRefs[(int)endFloor]);
          if (endFloorHeight == null) {
            var promptDoubleOptions = new PromptDoubleOptions("\nEnter the height of the start of the vertical route from the floor (in feet): ");
            promptDoubleOptions.AllowNegative = false;
            promptDoubleOptions.AllowZero = false;
            promptDoubleOptions.DefaultValue = 0;

            while (true) {
              PromptDoubleResult promptDoubleResult = ed.GetDouble(promptDoubleOptions);
              if (promptDoubleResult.Status == PromptStatus.OK) {
                endFloorHeight = promptDoubleResult.Value;
                Tuple<double, double> heightLimits = CADObjectCommands.GetHeightLimits(BasePointGUIDs[(int)endFloor]);
                double upperHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[(int)endFloor]).Item2;
                double lowerHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[(int)endFloor]).Item1;
                if (endFloorHeight > upperHeightLimit || endFloorHeight < lowerHeightLimit) {
                  ed.WriteMessage($"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.");
                  promptDoubleOptions.Message = $"\nHeight cannot exceed {upperHeightLimit} or be less than {lowerHeightLimit}. Please enter a valid height.";
                  continue;
                }
                break;
              }
              else if (promptDoubleResult.Status == PromptStatus.Cancel) {
                ed.WriteMessage("\nCommand cancelled.");
                return null;
              }
              else if (promptDoubleResult.Status == PromptStatus.Error) {
                ed.WriteMessage("\nError in input. Please try again.");
                continue;
              }
            }
          }

          Point3d newUpPointLocation3 = BasePointRefs[(int)endFloor].Position + upVector;
          BlockTableRecord blockDef3 =
            tr.GetObject(bt["GMEP_PLUMBING_LINE_UP"], OpenMode.ForRead) as BlockTableRecord;
          BlockTableRecord curSpace3 = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
          labelPoint2 = upBlockRef3.Position;
          RotateJig rotateJig2 = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
          if (rotatePromptResult2.Status != PromptStatus.OK) {
            return null;
          }
          upBlockRef3.Layer = layer;
          upBlockRef3.Position = new Point3d(newUpPointLocation3.X, newUpPointLocation3.Y, (floorHeights[(int)endFloor] + (double)endFloorHeight) * 12);
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;
          PlumbingVerticalRoute endRoute = new PlumbingVerticalRoute();

          foreach (DynamicBlockReferenceProperty prop in pc3) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
              endRoute.Id = prop.Value.ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[(int)endFloor];
              endRoute.BasePointId = prop.Value.ToString();
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
              endRoute.VerticalRouteId = prop.Value.ToString();
            }
            if (prop.PropertyName == "length") {
              prop.Value = CADObjectCommands.GetHeightLimits(BasePointGUIDs[(int)endFloor]).Item2 - (double)endFloorHeight;
              endRoute.Length = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "start_height") {
              prop.Value = (double)endFloorHeight;
              endRoute.StartHeight = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "pipe_type") {
              prop.Value = pipeType;
              endRoute.PipeType = prop.Value.ToString();
            }
            if (prop.PropertyName == "is_up") {
              prop.Value = isUp ? 1 : 0;
              endRoute.IsUp = isUp;
            }
            if (prop.PropertyName == "fixture_type") {
              prop.Value = fixtureType;
              endRoute.FixtureType = prop.Value.ToString();
            }
          }
          endRoute.Type = type;
          endRoute.ProjectId = ProjectId;
          endRoute.Position = upBlockRef3.Position;
          endRoute.Rotation = upBlockRef3.Rotation;
          verticalRoutes.Add(endRoute.BasePointId, endRoute);
          tr.Commit();
        }
        MakeVerticalRouteLabel(labelPoint2, "DOWN FROM UPPER");
      }
      else if (endFloor == startFloor) {
        string blockName = "GMEP_PLUMBING_LINE_DOWN";
        if (direction == null) {
          PromptKeywordOptions pko3 = new PromptKeywordOptions("\nUp or Down?");
          pko3.Keywords.Add("Up");
          pko3.Keywords.Add("UpToCeiling");
          pko3.Keywords.Add("Down");
          pko3.Keywords.Add("DownToFloor");

          PromptResult pr3 = ed.GetKeywords(pko3);
          if (pr3.Status != PromptStatus.OK) {
            ed.WriteMessage("\nCommand cancelled.");
            routeHeightDisplay.Disable();
            return null;
          }
          direction = pr3.StringResult;
        }
        if (length == null && (direction == "Up" || direction == "Down" || (direction == "DownToFloor" && CADObjectCommands.ActiveFloor == 1) || (direction == "UpToCeiling" && CADObjectCommands.ActiveFloor == floorHeights.Values.Max()))){
          string tempDirection = direction;
          if (tempDirection == "UpToCeiling") {
            tempDirection = "Up";
          }
          else if (tempDirection == "DownToFloor") {
            tempDirection = "Down";
          }
        
          PromptDoubleOptions pdo2 = new PromptDoubleOptions(
            $"\nHow Far {tempDirection}(Ft)?"
          );
          pdo2.AllowNegative = false;
          pdo2.AllowZero = false;
          pdo2.DefaultValue = 3;
          length = 0;

          while (true) {
            PromptDoubleResult pdr2 = ed.GetDouble(pdo2);
            if (pdr2.Status == PromptStatus.OK) {
              length = pdr2.Value;
              if (direction == "Up") {
                double heightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item2;
                double height = (double)routeHeight;
                double limit = heightLimit - height;
                if (length > limit) {
                  ed.WriteMessage($"\nFull height of fixture cannot exceed {heightLimit}. Current fixture height is {height}. Please enter a valid length.");
                  pdo2.Message = $"\nFull height of fixture cannot exceed {heightLimit}. Current fixture height is {height}. Please enter a valid length.";
                  continue;
                }
              }
              if (direction == "Down") {
                double lowerHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item1;
                double height = (double)routeHeight;
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
              return null;
            }
            break;
          }
        }
        if (direction == "Up" || direction == "UpToCeiling") {
          isUp = true;
        }
        else if (direction == "Down" || direction == "DownToFloor") {
          isUp = false;
        }

        if (direction == "UpToCeiling") {
          double heightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item2;
          double height = (double)routeHeight;
          length = heightLimit - height;
        }
        else if (direction == "DownToFloor") {
          double lowerHeightLimit = CADObjectCommands.GetHeightLimits(BasePointGUIDs[startFloor]).Item1;
          double height = (double)routeHeight;
          length = height - lowerHeightLimit;
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
          if (direction == "Up" || direction == "UpToCeiling") {
            upBlockRef3.Rotation = Math.PI;
          }
          RotateJig rotateJig = new RotateJig(upBlockRef3);
          PromptResult rotatePromptResult = ed.Drag(rotateJig);
          if (rotatePromptResult.Status != PromptStatus.OK) {
            return null;
          }
          if (direction == "Up" || direction == "UpToCeiling") {
            zIndex += (double)length * 12;
          }
          upBlockRef3.Position = new Point3d(newUpPointLocation3.X, newUpPointLocation3.Y, zIndex);
          upBlockRef3.Layer = layer;
          curSpace3.AppendEntity(upBlockRef3);
          tr.AddNewlyCreatedDBObject(upBlockRef3, true);
          labelPoint3 = upBlockRef3.Position;

          var pc3 = upBlockRef3.DynamicBlockReferencePropertyCollection;
          PlumbingVerticalRoute newRoute = new PlumbingVerticalRoute();
          foreach (DynamicBlockReferenceProperty prop in pc3) {
            if (prop.PropertyName == "id") {
              prop.Value = Guid.NewGuid().ToString();
              newRoute.Id = prop.Value.ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              prop.Value = BasePointGUIDs[startFloor];
              newRoute.BasePointId = prop.Value.ToString();
            }
            if (prop.PropertyName == "vertical_route_id") {
              prop.Value = verticalRouteId;
              newRoute.VerticalRouteId = prop.Value.ToString();
            }
            if (prop.PropertyName == "length") {
              prop.Value = length;
              newRoute.Length = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "start_height") {
              if (direction == "Up" || direction == "UpToCeiling") {
                prop.Value = routeHeight + length;
              }
              else if (direction == "Down" || direction == "DownToFloor") {
                prop.Value = routeHeight;
              }
              newRoute.StartHeight = Convert.ToDouble(prop.Value);
            }
            if (prop.PropertyName == "pipe_type") {
              prop.Value = pipeType;
              newRoute.PipeType = prop.Value.ToString();
            }
            if (prop.PropertyName == "is_up") {
              prop.Value = isUp ? 1 : 0;
              newRoute.IsUp = isUp;
            }
            if (prop.PropertyName == "fixture_type") {
              prop.Value = fixtureType;
              newRoute.FixtureType = prop.Value.ToString();
            }
          }
          newRoute.Type = type;
          newRoute.ProjectId = ProjectId;
          newRoute.Position = upBlockRef3.Position;
          newRoute.Rotation = upBlockRef3.Rotation;
          verticalRoutes.Add(newRoute.BasePointId, newRoute);
          tr.Commit();
          
        }
        MakeVerticalRouteLabel(labelPoint3, direction.ToUpper());
      }
      SettingObjects = false;
      return verticalRoutes;
    }

    [CommandMethod("SETPLUMBINGBASEPOINT")]
    public async void SetPlumbingBasePoint() {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      SettingObjects = true;
      var prompt = new Views.BasePointPromptWindow();
      bool? result = prompt.ShowDialog();
      double currentFloorHeight = -10;
      if (!CADObjectCommands.IsResidential) {
        currentFloorHeight = -15;
      }
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
      bool site = prompt.Site;
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
        if (!CADObjectCommands.IsResidential) {
          ceilingHeightOptions.DefaultValue = currentFloorHeight + 15;
        }

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
          if (br == null) {
            ed.WriteMessage("\nOperation cancelled.");
            SettingObjects = false;
            return;
          }
          br.Layer = "Defpoints";
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
          
          tr.Commit();
        }
      }
      if (site) {
        SetSiteBasePoint(ViewId);
      }
      SettingObjects = false;
    }
    [CommandMethod("SETPLUMBINGSITEBASEPOINT")]
    public async void SetPlumbingSiteBasePoint() {
      SettingObjects = true;
      
      CADObjectCommands.GetActiveView();
      bool sitePointExists = GetPlumbingBasePointsFromCAD().Any(i => i.ViewportId == CADObjectCommands.ActiveViewId && (i.IsSite || i.IsSiteRef));
      if (!sitePointExists) {
        SetSiteBasePoint(CADObjectCommands.ActiveViewId);
      }
      else {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var ed = doc.Editor;
        ed.WriteMessage("\nA Site Base Point already exists for this view. Operation cancelled.");
      }
     
      SettingObjects = false;

    }
    public async void SetSiteBasePoint(string viewId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;
      List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD().Where(i => i.ViewportId == viewId).OrderBy(i => i.Floor).ToList();
      List<int> floors = basePoints.Select(i => i.Floor).Distinct().ToList();

      PromptKeywordOptions pko = new PromptKeywordOptions("\nSelect bottom floor of site plan: " );
      foreach (int floor in floors) {
        pko.Keywords.Add(floor.ToString());
      }
      PromptResult pr = ed.GetKeywords(pko);
      if (pr.Status != PromptStatus.OK) {
        ed.WriteMessage("\nOperation cancelled.");
        SettingObjects = false;
        return;
      }
      int bottomPointFloor = int.Parse(pr.StringResult);

      floors.RemoveAll(i => i < bottomPointFloor);
      PromptKeywordOptions pko2 = new PromptKeywordOptions("\nSelect top floor of site plan: ");
      foreach (int floor in floors) {
        pko2.Keywords.Add(floor.ToString());
      }
      PromptResult pr2 = ed.GetKeywords(pko2);
      if (pr2.Status != PromptStatus.OK) {
        ed.WriteMessage("\nOperation cancelled.");
        SettingObjects = false;
        return;
      }
      int topPointFloor = int.Parse(pr2.StringResult);

      for (int i = bottomPointFloor; i <= topPointFloor; i++) {
        PlumbingPlanBasePoint basePoint = basePoints.First(bp => bp.Floor == i);
        string message = $"Site Base Point for plan {basePoint.Plan}:{basePoint.Type}, Floor {i}";
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord curSpace = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockTableRecord block;
          //string message = "\nCreating Plumbing Base Point for Site";
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            "GMEP_PLUMBING_BASEPOINT",
            message,
            out block,
            out Point3d point
          );
          if (br == null) {
            ed.WriteMessage("\nOperation cancelled.");
            SettingObjects = false;
            return;
          }
          br.Layer = "xref";
          br.Rotation = br.Rotation + Math.PI / 4;
          curSpace.AppendEntity(br);
          tr.AddNewlyCreatedDBObject(br, true);
          DynamicBlockReferencePropertyCollection properties =
            br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in properties) {
            if (prop.PropertyName == "plan") {
              prop.Value = basePoint.Plan;
            }
            else if (prop.PropertyName == "floor") {
              prop.Value = basePoint.Floor;
            }
            else if (prop.PropertyName == "type") {
              prop.Value = basePoint.Type;
            }
            else if (prop.PropertyName == "view_id") {
              prop.Value = basePoint.ViewportId;
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
              prop.Value = basePoint.FloorHeight;
            }
            else if (prop.PropertyName == "route_height") {
              prop.Value = basePoint.RouteHeight;
            }
            else if (prop.PropertyName == "ceiling_height") {
              prop.Value = basePoint.CeilingHeight;
            }
            else if (prop.PropertyName == "is_site") {
              prop.Value = 1;
            }
          }
          tr.Commit();
        }
      }
      for (int i = bottomPointFloor; i <= topPointFloor; i++) {
        PlumbingPlanBasePoint basePoint = basePoints.First(bp => bp.Floor == i);
        string message = $"Site Base Point for plan {basePoint.Plan}:{basePoint.Type}(relative to floor {basePoint.Floor}).";
        ZoomToPoint(ed, new Point3d(basePoint.Point.X, basePoint.Point.Y, 0));
        
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
          BlockTableRecord curSpace = (BlockTableRecord)
            tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          BlockTableRecord block;
          //string message = "\nCreating Plumbing Base Point for Site";
          BlockReference br = CADObjectCommands.CreateBlockReference(
            tr,
            bt,
            "GMEP_PLUMBING_BASEPOINT",
            message,
            out block,
            out Point3d point
          );
          if (br == null) {
            ed.WriteMessage("\nOperation cancelled.");
            SettingObjects = false;
            return;
          }
          br.Layer = "xref";
          br.Rotation = br.Rotation + Math.PI / 4;
          curSpace.AppendEntity(br);
          tr.AddNewlyCreatedDBObject(br, true);
          DynamicBlockReferencePropertyCollection properties =
            br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in properties) {
            if (prop.PropertyName == "plan") {
              prop.Value = basePoint.Plan;
            }
            else if (prop.PropertyName == "floor") {
              prop.Value = basePoint.Floor;
            }
            else if (prop.PropertyName == "type") {
              prop.Value = basePoint.Type;
            }
            else if (prop.PropertyName == "view_id") {
              prop.Value = basePoint.ViewportId;
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
              prop.Value = basePoint.FloorHeight;
            }
            else if (prop.PropertyName == "route_height") {
              prop.Value = basePoint.RouteHeight;
            }
            else if (prop.PropertyName == "ceiling_height") {
              prop.Value = basePoint.CeilingHeight;
            }
            else if (prop.PropertyName == "is_site_ref") {
              prop.Value = 1;
            }
          }
          tr.Commit();
        }
      }
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
      var doc = ed.Document;
      using (doc.LockDocument()) {
        Point3d wcsPos = blockRef.Position;
        // Get the current view
        using (ViewTableRecord view = ed.GetCurrentView()) {
          Matrix3d matWcs2Dcs =
              Matrix3d.PlaneToWorld(view.ViewDirection) 
              .Inverse() *
              Matrix3d.Displacement(view.Target - Point3d.Origin) 
              .Inverse() *
              Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target);

          Point3d dcsPos = wcsPos.TransformBy(matWcs2Dcs);

          double zoomWidth = 400.0;
          double zoomHeight = 400.0;

          view.CenterPoint = new Point2d(dcsPos.X, dcsPos.Y);
          view.Width = zoomWidth;
          view.Height = zoomHeight;

          ed.SetCurrentView(view);
        }
      }
    }
    public static void ZoomToPoint(Editor ed, Point3d wcsPos) {
      var doc = ed.Document;
      using (doc.LockDocument()) {
        // Get the current view
        using (ViewTableRecord view = ed.GetCurrentView()) {
          Matrix3d matWcs2Dcs =
              Matrix3d.PlaneToWorld(view.ViewDirection)
              .Inverse() *
              Matrix3d.Displacement(view.Target - Point3d.Origin)
              .Inverse() *
              Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target);

          Point3d dcsPos = wcsPos.TransformBy(matWcs2Dcs);

          double zoomWidth = 400.0;
          double zoomHeight = 400.0;

          view.CenterPoint = new Point2d(dcsPos.X, dcsPos.Y);
          view.Width = zoomWidth;
          view.Height = zoomHeight;

          ed.SetCurrentView(view);
        }
      }
    }

    public void WriteMessage(string message) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      ed.WriteMessage(message);
    }

    private void AddArrowsToLine(ObjectId lineId, string lineGUID) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

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
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

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

    private void AttachRouteXData(ObjectId lineId, string id, string basePointId, string pipeType, double slope, string fixtureDropId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

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
          new TypedValue(1000, basePointId),
          new TypedValue(1000, pipeType),
          new TypedValue(1040, slope),
          new TypedValue(1000, fixtureDropId)
        );
        line.XData = rb;
        rb.Dispose();
        tr.Commit();
      }
    }

    private void UpdateXRecordId(Transaction tr, string newId, DateTime newCreationTime) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

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
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

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
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return DateTime.Now;

      var db = doc.Database;
      var ed = doc.Editor;

      if (doc != null && !string.IsNullOrEmpty(doc.Name)) {
        FileInfo fileInfo = new FileInfo(doc.Name);
        return fileInfo.CreationTime.ToUniversalTime();
      }
      else {
        return DateTime.UtcNow;
      }
    }

    public void MakeVerticalRouteLabel(Point3d dnPoint, string direction) {
      if (dnPoint == null || double.IsNaN(dnPoint.X) || double.IsNaN(dnPoint.Y) || double.IsNaN(dnPoint.Z)) {
        WriteMessage("\nError: Invalid point for vertical route label.");
        return;
      }
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
      if (fixture.TypeAbbreviation == "VS") {
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
      Fixture();
    }
    public void Fixture(string fixtureString = null, string catalogString = null, Point3d? placementPoint = null, double? blockRotation = null) {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);
    
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      string basePointId = CADObjectCommands.GetActiveView();

      List<PlumbingFixtureType> plumbingFixtureTypes = MariaDBService.GetPlumbingFixtureTypes();
      Dictionary<int, List<PlumbingFixtureCatalogItem>> allPlumbingFixtureCatalogItems =
        MariaDBService.GetAllPlumbingFixtureCatalogItems();

      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
      PromptResult keywordResult;


      if (fixtureString == null) {
        keywordOptions.Message = "\nSelect fixture type:";
        plumbingFixtureTypes.ForEach(t => {
          if (allPlumbingFixtureCatalogItems.ContainsKey(t.Id)) {
            List<PlumbingFixtureCatalogItem> catalogItems = allPlumbingFixtureCatalogItems[t.Id];
            if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && !catalogItems.All(item => string.IsNullOrEmpty(item.WaterBlockNames))) || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && !catalogItems.All(item => string.IsNullOrEmpty(item.GasBlockNames))) || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && !catalogItems.All(item => string.IsNullOrEmpty(item.WasteBlockNames)))) && ((CADObjectCommands.IsResidential && !catalogItems.All(item => item.Residential == false)) || (!CADObjectCommands.IsResidential && !catalogItems.All(item => item.Commercial == false)))) {
              keywordOptions.Keywords.Add(t.Abbreviation + " - " + t.Name);
            }
          }
        });
        //keywordOptions.Keywords.Default = "WC - Water Closet";
        keywordOptions.AllowNone = false;
        keywordResult = ed.GetKeywords(keywordOptions);

        if (keywordResult.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return;
        }
       fixtureString = keywordResult.StringResult;
        ed.WriteMessage("\nSelected fixture: " + fixtureString);  
      }
      PlumbingFixtureType selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t =>
        fixtureString.StartsWith(t.Abbreviation)
      );
      if (selectedFixtureType == null) {
        selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t => t.Abbreviation == "WC");
      }

      List<PlumbingFixtureCatalogItem> plumbingFixtureCatalogItems =
        MariaDBService.GetPlumbingFixtureCatalogItemsByType(selectedFixtureType.Id);

      if (catalogString == null) {
        keywordOptions = new PromptKeywordOptions("");
        keywordOptions.Message = "\nSelect catalog item:";
        plumbingFixtureCatalogItems.ForEach(i => {
          if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && i.WaterBlockNames != "") || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && i.GasBlockNames != "") || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && i.WasteBlockNames != "")) && ((CADObjectCommands.IsResidential && i.Residential == true) || (!CADObjectCommands.IsResidential && i.Commercial == true))) {
            keywordOptions.Keywords.Add(i.Id.ToString() + " - " + i.Description + " - " + i.Make + " " + i.Model);
          }
        });
        keywordResult = ed.GetKeywords(keywordOptions);

        catalogString = keywordResult.StringResult;
      }
      if (catalogString.Contains(' ')) {
        catalogString = catalogString.Split(' ')[0];
      }
      PlumbingFixtureCatalogItem selectedCatalogItem = plumbingFixtureCatalogItems.FirstOrDefault(
        i => i.Id.ToString() == catalogString
      );
      if (selectedCatalogItem == null) {
        return;
      }

      int flowTypeId = 1;
      if (selectedFixtureType.Abbreviation == "U" || selectedCatalogItem.Id == 6) {
        flowTypeId = 2;
      }
      List<string> selectedBlockNames = new List<string>();
      string viewType = GetPlumbingBasePointsFromCAD(ProjectId).Where(bp => bp.Id == basePointId).First().Type;
      if (viewType.Contains("Water")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WaterBlockNames.Split(','));
      }
      if (viewType.Contains("Gas")) {
        selectedBlockNames.AddRange(selectedCatalogItem.GasBlockNames.Split(','));
      }
      if (viewType.Contains("Sewer-Vent")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WasteBlockNames.Split(','));
      }
      selectedBlockNames = selectedBlockNames.Distinct().ToList();
      List<string> selectedBlockNames2 = new List<string>(selectedBlockNames);

      foreach (string blockName in selectedBlockNames) {
        if (blockName.Contains("%WHSIZE%")) {
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
            selectedBlockNames2[selectedBlockNames.IndexOf(blockName)] = blockName.Replace(
              "%WHSIZE%",
              whSize
            );
          }
        }
        if (blockName.Contains("%FSSIZE%")) {
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
            selectedBlockNames2[selectedBlockNames.IndexOf(blockName)] = blockName.Replace(
             "%FSSIZE%",
             fsSize
            );
          }
        }
        if (blockName.Contains("%COSTYLE%")) {
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
            selectedBlockNames2[selectedBlockNames.IndexOf(blockName)] = blockName.Replace(
              "%COSTYLE%",
              coStyle
            );
          }
        }
      }
      double routeHeight = 0;
      if (selectedFixtureType.Abbreviation != "FD" && selectedFixtureType.Abbreviation != "FS") {
        routeHeight = CADObjectCommands.GetPlumbingRouteHeight();
        if (selectedFixtureType.Abbreviation == "WH" || selectedFixtureType.Abbreviation == "IWH") {
          PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the fixture from the floor (in feet): ");
          pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();
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
        }
      }
      
      PlumbingFixture plumbingFixture = null;
      

      var routeHeightDisplay = new RouteHeightDisplay(ed);
  
      if (selectedBlockNames2.Count() != 0) {
        foreach (string blockName in selectedBlockNames2) {
          ObjectId blockId = ObjectId.Null;
          Point3d point = Point3d.Origin;
          double rotation = 0;
          int number = 0;
          string GUID = Guid.NewGuid().ToString();
          double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;
          double startHeight = CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight;
          double verticalRouteLength = startHeight - routeHeight;

          try {
            if (blockName == "GMEP CW FIXTURE POINT") {
              if (flowTypeId == 1) {
                PlumbingVerticalRoute route = VerticalRoute("ColdWater", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength, null, "Vertical Route", "Flush Tank").First().Value;

                double offsetDistance = 11.25;
                double offsetDistance2 = 2.125;
                double offsetX = offsetDistance * Math.Cos(route.Rotation + (Math.PI / 2));
                double offsetY = offsetDistance * Math.Sin(route.Rotation + (Math.PI / 2));
                Point3d newPoint = new Point3d(
                    route.Position.X + offsetX,
                    route.Position.Y + offsetY,
                    route.Position.Z
                );
                Vector3d direction = new Vector3d(newPoint.X - route.Position.X, newPoint.Y - route.Position.Y, 0);
                Vector3d offset2 = direction.GetNormal() * offsetDistance2;
         
                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight, newPoint, route.Position, route.Id);
                Point3d fixturePos = new Point3d(newPoint.X - offset2.X, newPoint.Y - offset2.Y, newPoint.Z - (route.Length * 12));
                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight - route.Length, route.Position, fixturePos, route.Id);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                  BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                  br.Rotation = route.Rotation;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;
                  tr.Commit();
                }
                
              }
              else if (flowTypeId == 2) {
                PlumbingVerticalRoute route = VerticalRoute("ColdWater", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength, null, "Vertical Route", "Flush Valve").First().Value;
                PromptKeywordOptions pko = new PromptKeywordOptions("Left or Right?");
                pko.Keywords.Add("Left");
                pko.Keywords.Add("Right");
                PromptResult res = ed.GetKeywords(pko);


                double offsetDistance = 11.25;
                double offsetDistance2 = 2.125;
                double offsetX = offsetDistance * Math.Cos(route.Rotation);
                double offsetY = offsetDistance * Math.Sin(route.Rotation);
                double rotatedOffsetX = -offsetY;
                double rotatedOffsetY = offsetX;
                if (res.StringResult == "Left") {
                  offsetX = -offsetX;
                  offsetY = -offsetY;
                  rotatedOffsetX = offsetY;
                  rotatedOffsetY = -offsetX;
                }

                Point3d StartPos = route.Position;
                Point3d newPoint = new Point3d(
                    route.Position.X + offsetX*1.2,
                    route.Position.Y + offsetY*1.2,
                    route.Position.Z
                );
                Vector3d direction = route.Position - newPoint;
                double length = direction.Length;
                if (length > 0) {
                  Vector3d offsetStart = direction.GetNormal() * 1.5;
                  StartPos = StartPos - offsetStart;
                }

                Point3d midPoint = new Point3d(
                   (StartPos.X + newPoint.X) / 2.0,
                   (StartPos.Y + newPoint.Y) / 2.0,
                   StartPos.Z
               );

                Point3d anotherNewPoint = new Point3d(
                    midPoint.X + rotatedOffsetX,
                    midPoint.Y + rotatedOffsetY,
                    midPoint.Z
                );
                Vector3d direction2 = new Vector3d(anotherNewPoint.X - midPoint.X, anotherNewPoint.Y - midPoint.Y, 0);
                Vector3d offset2 = direction2.GetNormal() * offsetDistance2;
                
               
                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight, anotherNewPoint, midPoint, route.Id);
                SpecializedHorizontalRoute( "ColdWater", route.PipeType, route.StartHeight, newPoint, StartPos, route.Id);

                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight - route.Length, StartPos, newPoint, route.Id);
                Point3d fixturePos = new Point3d(anotherNewPoint.X - offset2.X, anotherNewPoint.Y - offset2.Y, anotherNewPoint.Z - (route.Length * 12));
                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight - route.Length, midPoint, fixturePos, route.Id);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                  //Place the fixture block
                  BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                  br.Rotation = route.Rotation;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;

                  Point3d newPos = new Point3d(newPoint.X, newPoint.Y, newPoint.Z - (route.Length * 12));
                  Circle circle = new Circle(newPos, Vector3d.ZAxis, 1);
                  modelSpace.AppendEntity(circle);
                  tr.AddNewlyCreatedDBObject(circle, true);

                  Hatch hatch = new Hatch();
                  hatch.SetDatabaseDefaults();
                  hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                  hatch.Associative = false;
                  hatch.Layer = "P-DOMW-CWTR";
                  hatch.Elevation = newPos.Z;

                  modelSpace.AppendEntity(hatch);
                  tr.AddNewlyCreatedDBObject(hatch, true);

                  ObjectIdCollection ids = new ObjectIdCollection { circle.ObjectId };
                  hatch.AppendLoop(HatchLoopTypes.Default, ids);
                  hatch.EvaluateHatch(true);
                  circle.Erase();

                  //placing the line :3
                  Vector3d routeVec = newPoint - StartPos;
                  double routeLength = routeVec.Length;
                  if (routeLength == 0) return; 

                  Vector3d normal = new Vector3d(-routeVec.Y, routeVec.X, 0).GetNormal();

                  double offsetDistance3 = 4.0;
                  Point3d offsetMid = newPoint - (normal * offsetDistance3);
                  if (res.StringResult == "Right") {
                    offsetMid = newPoint + (normal * offsetDistance3);
                  }

                  Vector3d halfVec = routeVec.GetNormal() * (routeLength / 4.0);

                  Point3d newStart = offsetMid - halfVec;
                  Point3d newEnd = offsetMid + halfVec;
                  newStart = new Point3d(newStart.X, newStart.Y, newStart.Z - (route.Length * 12));
                  newEnd = new Point3d(newEnd.X, newEnd.Y, newEnd.Z - (route.Length * 12));

                  Line line = new Line(newStart, newEnd);
                  line.Layer = "P-DOMW-CWTR";
                  modelSpace.AppendEntity(line);
                  tr.AddNewlyCreatedDBObject(line, true);

                  tr.Commit();
                }
              }
            }
            else if (blockName == "GMEP HW FIXTURE POINT") {
              PlumbingVerticalRoute route = VerticalRoute("HotWater", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength, null, "Vertical Route", "Flush Tank").First().Value;
              double offsetDistance = 11.25;
              double offsetDistance2 = 2.125;
              double offsetX = offsetDistance * Math.Cos(route.Rotation + (Math.PI / 2));
              double offsetY = offsetDistance * Math.Sin(route.Rotation + (Math.PI / 2));
              Point3d newPoint = new Point3d(
                  route.Position.X + offsetX,
                  route.Position.Y + offsetY,
                  route.Position.Z
              );
              Vector3d direction = new Vector3d(newPoint.X - route.Position.X, newPoint.Y - route.Position.Y, 0);
              Vector3d offset2 = direction.GetNormal() * offsetDistance2;
          
              SpecializedHorizontalRoute( "HotWater", route.PipeType, route.StartHeight, newPoint, route.Position, route.Id);
              Point3d fixturePos = new Point3d(newPoint.X - offset2.X, newPoint.Y - offset2.Y, newPoint.Z - (route.Length * 12));
              SpecializedHorizontalRoute("HotWater", route.PipeType, route.StartHeight - route.Length, route.Position, fixturePos, route.Id);
      
              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                br.Rotation = route.Rotation;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }
              
            }
            else if (blockName == "GMEP PLUMBING GAS OUTPUT") {
              PlumbingVerticalRoute route = VerticalRoute("Gas", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength).First().Value;
              double offsetDistance = 10.25;
              double offsetDistance2 = 3.5;
              double offsetX = offsetDistance * Math.Cos(route.Rotation + (Math.PI / 2));
              double offsetY = offsetDistance * Math.Sin(route.Rotation + (Math.PI / 2));
              Point3d newPoint = new Point3d(
                  route.Position.X + offsetX,
                  route.Position.Y + offsetY,
                  route.Position.Z
              );
              Vector3d direction = new Vector3d(newPoint.X - route.Position.X, newPoint.Y - route.Position.Y, 0);
              Vector3d offset2 = direction.GetNormal() * offsetDistance2;

              SpecializedHorizontalRoute("Gas", route.PipeType, route.StartHeight, newPoint, route.Position, route.Id);
              Point3d fixturePos = new Point3d(newPoint.X - offset2.X, newPoint.Y - offset2.Y, newPoint.Z - (route.Length * 12));
              SpecializedHorizontalRoute("Gas", route.PipeType, route.StartHeight - route.Length, route.Position, fixturePos, route.Id);

              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                br.Rotation = route.Rotation + Math.PI / 2;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }
            }
            else {
              routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);
              if (blockName == "GMEP DRAIN") {
                zIndex = CADObjectCommands.ActiveFloorHeight * 12;
              }
              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr;

                BlockReference br = null;
                if (placementPoint == null) {
                  br = CADObjectCommands.CreateBlockReference(
                    tr,
                    bt,
                    blockName,
                    "Plumbing Fixture " + selectedFixtureType.Name,
                    out btr,
                    out point
                  );
                }
                else {
                  btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  br = new BlockReference((Point3d)placementPoint, btr.ObjectId);
                  point = (Point3d)placementPoint;
                }
                if (br != null) {
                  BlockTableRecord curSpace = (BlockTableRecord)
                    tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                  if (blockRotation == null) {
                    RotateJig rotateJig = new RotateJig(br);
                    PromptResult rotatePromptResult = ed.Drag(rotateJig);
                    if (rotatePromptResult.Status != PromptStatus.OK) {
                      ed.WriteMessage("\nRotation cancelled.");
                      routeHeightDisplay.Disable();
                      return;
                    }
                  }
                  else {
                    br.Rotation = blockRotation.Value;
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
                routeHeightDisplay.Disable();
                blockId = br.Id;
                tr.Commit();
              }
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
                if (prop.PropertyName == "flow_type_id") {
                  prop.Value = flowTypeId;
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
                blockName,
                flowTypeId
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
              blockName,
              flowTypeId
            );

            if (blockName == "GMEP DRAIN") {
              PromptKeywordOptions pko = new PromptKeywordOptions("How far up?");
              pko.Keywords.Add("Ceiling");
              pko.Keywords.Add("Roof");
              pko.Keywords.Add("None", "No Vent Needed", "No Vent Needed");
              pko.AllowNone = false;
              PromptResult res = ed.GetKeywords(pko);
              ed.WriteMessage("\nYou selected: " + res.StringResult);
              if (res.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Dictionary<string, PlumbingVerticalRoute> ventRoutes = null;
              if (res.StringResult == "None") {
                continue;
              }
              if (res.StringResult == "Ceiling") {
                ventRoutes = VerticalRoute("Vent", 0, CADObjectCommands.ActiveFloor, "UpToCeiling");
              }
              else if (res.StringResult == "Roof") {
                List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD(ProjectId);
                PlumbingPlanBasePoint activeBasePoint = basePoints.Where(bp => bp.Id == CADObjectCommands.ActiveBasePointId).First();
                List<PlumbingPlanBasePoint> aboveBasePoints = basePoints.Where(bp => bp.Floor >= activeBasePoint.Floor && bp.ViewportId == activeBasePoint.ViewportId).ToList();
                PlumbingPlanBasePoint highestFloorBasePoint = aboveBasePoints
                .OrderByDescending(bp => bp.Floor)
                .FirstOrDefault();

                ventRoutes = VerticalRoute("Vent", 0, highestFloorBasePoint.Floor, "UpToCeiling", null, highestFloorBasePoint.CeilingHeight - highestFloorBasePoint.FloorHeight);
                if (highestFloorBasePoint.Floor > CADObjectCommands.ActiveFloor) {
                  PlumbingVerticalRoute startRoute = ventRoutes.First().Value;
                  ZoomToPoint(ed, startRoute.Position);
                }
              }
              if (ventRoutes == null || !ventRoutes.ContainsKey(CADObjectCommands.ActiveBasePointId)) {
                ed.WriteMessage("\nError: Could not find vent route for base point.");
                return;
              }
              Point3d ventPoint = ventRoutes[CADObjectCommands.ActiveBasePointId].Position;
              ventPoint = new Point3d(ventPoint.X, ventPoint.Y, point.Z);
              double shortenBy = 1.5;
              Vector3d direction = point - ventPoint;
              double length = direction.Length;

              Point3d newEndPoint = ventPoint;
              Point3d newStartPoint = point;
              if (length > 0) {
                Vector3d offset = direction.GetNormal() * shortenBy;
                newEndPoint = ventPoint + offset;
                newStartPoint = point - offset; 
              }

              SpecializedHorizontalRoute(
                   "Waste", "", 0, newStartPoint, newEndPoint
              );
            }
          }
          catch (System.Exception ex) {
            ed.WriteMessage("FIxture Error - "+ ex.ToString());
            routeHeightDisplay.Disable();
            Console.WriteLine(ex.ToString());
          }
        }
        MakePlumbingFixtureLabel(plumbingFixture, selectedFixtureType);
      }
      routeHeightDisplay.Disable();
    }

    [CommandMethod("PLUMBINGISLANDFIXTURE")]
    public void PlumbingIslandFixture() {
      IslandFixture();
    }
    public void IslandFixture(string fixtureString = null, string catalogString = null, Point3d? placementPoint = null, double? blockRotation = null) {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);

      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      string basePointId = CADObjectCommands.GetActiveView();

      List<PlumbingFixtureType> plumbingFixtureTypes = MariaDBService.GetPlumbingFixtureTypes();
      Dictionary<int, List<PlumbingFixtureCatalogItem>> allPlumbingFixtureCatalogItems =
        MariaDBService.GetAllPlumbingFixtureCatalogItems();

      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
      PromptResult keywordResult;


      if (fixtureString == null) {
        keywordOptions.Message = "\nSelect fixture type:";
        plumbingFixtureTypes.ForEach(t => {
          if (allPlumbingFixtureCatalogItems.ContainsKey(t.Id)) {
            List<PlumbingFixtureCatalogItem> catalogItems = allPlumbingFixtureCatalogItems[t.Id];
            if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && !catalogItems.All(item => string.IsNullOrEmpty(item.WaterBlockNames))) || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && !catalogItems.All(item => string.IsNullOrEmpty(item.GasBlockNames))) || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && !catalogItems.All(item => string.IsNullOrEmpty(item.WasteBlockNames)))) && (!catalogItems.All(item => !item.Island)) && ((CADObjectCommands.IsResidential && !catalogItems.All(item => item.Residential == false)) || (!CADObjectCommands.IsResidential && !catalogItems.All(item => item.Commercial == false)))) {
              keywordOptions.Keywords.Add(t.Abbreviation + " - " + t.Name);
            }
          }
        });
        //keywordOptions.Keywords.Default = "WC - Water Closet";
        keywordOptions.AllowNone = false;
        keywordResult = ed.GetKeywords(keywordOptions);

        if (keywordResult.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return;
        }
        fixtureString = keywordResult.StringResult;
        ed.WriteMessage("\nSelected fixture: " + fixtureString);
      }
      PlumbingFixtureType selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t =>
        fixtureString.StartsWith(t.Abbreviation)
      );
      if (selectedFixtureType == null) {
        selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t => t.Abbreviation == "WC");
      }

      List<PlumbingFixtureCatalogItem> plumbingFixtureCatalogItems =
        MariaDBService.GetPlumbingFixtureCatalogItemsByType(selectedFixtureType.Id);

      if (catalogString == null) {
        keywordOptions = new PromptKeywordOptions("");
        keywordOptions.Message = "\nSelect catalog item:";
        plumbingFixtureCatalogItems.ForEach(i => {
          if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && i.WaterBlockNames != "") || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && i.GasBlockNames != "") || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && i.WasteBlockNames != "")) && ((CADObjectCommands.IsResidential && i.Residential == true) || (!CADObjectCommands.IsResidential && i.Commercial == true)) && i.Island) {
            keywordOptions.Keywords.Add(i.Id.ToString() + " - " + i.Description + " - " + i.Make + " " + i.Model);
          }
        });
        keywordResult = ed.GetKeywords(keywordOptions);

        catalogString = keywordResult.StringResult;
      }
      if (catalogString.Contains(' ')) {
        catalogString = catalogString.Split(' ')[0];
      }
      PlumbingFixtureCatalogItem selectedCatalogItem = plumbingFixtureCatalogItems.FirstOrDefault(
        i => i.Id.ToString() == catalogString
      );
      if (selectedCatalogItem == null) {
        return;
      }

      int flowTypeId = 1;
      if (selectedFixtureType.Abbreviation == "U" || selectedCatalogItem.Id == 6) {
        flowTypeId = 2;
      }
      List<string> selectedBlockNames = new List<string>();
      string viewType = GetPlumbingBasePointsFromCAD(ProjectId).Where(bp => bp.Id == basePointId).First().Type;
      if (viewType.Contains("Water")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WaterBlockNames.Split(','));
      }
      if (viewType.Contains("Gas")) {
        selectedBlockNames.AddRange(selectedCatalogItem.GasBlockNames.Split(','));
      }
      if (viewType.Contains("Sewer-Vent")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WasteBlockNames.Split(','));
      }
      selectedBlockNames = selectedBlockNames.Distinct().ToList();
      List<string> selectedBlockNames2 = new List<string>(selectedBlockNames);

      
      double routeHeight = CADObjectCommands.GetPlumbingRouteHeight();


      PlumbingFixture plumbingFixture = null;


      var routeHeightDisplay = new RouteHeightDisplay(ed);
    

      if (selectedBlockNames2.Count() != 0) {
        foreach (string blockName in selectedBlockNames2) {
          ObjectId blockId = ObjectId.Null;
          Point3d point = Point3d.Origin;
          double rotation = 0;
          int number = 0;
          string GUID = Guid.NewGuid().ToString();
          double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;
          double startHeight = CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight;
          double verticalRouteLength = startHeight - routeHeight;

          try {
            if (blockName == "GMEP CW FIXTURE POINT") {
              PlumbingVerticalRoute route2 = VerticalRoute("ColdWater", startHeight, CADObjectCommands.ActiveFloor, "Down", startHeight, null, "Vertical route down to below floor: ").First().Value;
              CircleStartPointPreviewJig circleJig = new CircleStartPointPreviewJig(route2.Position, 1.5);
              PromptResult circlePromptResult = ed.Drag(circleJig);
              if (circlePromptResult.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Point3d firstPoint = circleJig.ProjectedPoint;
              HorizontalRoute(0, route2.Type, false, "Forward", firstPoint);

            
              if (flowTypeId == 1) {
                PlumbingVerticalRoute route = VerticalRoute("ColdWater", 0, CADObjectCommands.ActiveFloor, "Up", routeHeight, null, "Vertical route back up to fixture height: ", "Flush Tank").First().Value;
                double offsetDistance = 11.25;
                double offsetDistance2 = 2.125;
                double offsetX = offsetDistance * Math.Cos(route.Rotation + (Math.PI / 2));
                double offsetY = offsetDistance * Math.Sin(route.Rotation + (Math.PI / 2));
                Point3d newPoint = new Point3d(
                    route.Position.X + offsetX,
                    route.Position.Y + offsetY,
                    route.Position.Z
                );
                Vector3d direction = new Vector3d(newPoint.X - route.Position.X, newPoint.Y - route.Position.Y, 0);
                Vector3d offset2 = direction.GetNormal() * offsetDistance2;

                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight - route.Length, newPoint, route.Position, route.Id);
                Point3d fixturePos = new Point3d(newPoint.X - offset2.X, newPoint.Y - offset2.Y, newPoint.Z);
                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight, route.Position, fixturePos, route.Id);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                  BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                  br.Rotation = route.Rotation;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;
                  tr.Commit();
                }
                
              }
              else if (flowTypeId == 2) {
                PlumbingVerticalRoute route = VerticalRoute("ColdWater", 0, CADObjectCommands.ActiveFloor, "Up", routeHeight, null, "Vertical route back up to fixture height: ", "Flush Valve").First().Value;
                PromptKeywordOptions pko = new PromptKeywordOptions("Left or Right?");
                pko.Keywords.Add("Left");
                pko.Keywords.Add("Right");
                PromptResult res = ed.GetKeywords(pko);

                double offsetDistance = 11.25;
                double offsetDistance2 = 2.125;
                double offsetX = offsetDistance * Math.Cos(route.Rotation);
                double offsetY = offsetDistance * Math.Sin(route.Rotation);
                double rotatedOffsetX = -offsetY;
                double rotatedOffsetY = offsetX;
                if (res.StringResult == "Left") {
                  offsetX = -offsetX;
                  offsetY = -offsetY;
                  rotatedOffsetX = offsetY;
                  rotatedOffsetY = -offsetX;
                }

                Point3d StartPos = route.Position;
                Point3d newPoint = new Point3d(
                    route.Position.X + offsetX * 1.2,
                    route.Position.Y + offsetY * 1.2,
                    route.Position.Z
                );
                Vector3d direction = route.Position - newPoint;
                double length = direction.Length;
                if (length > 0) {
                  Vector3d offsetStart = direction.GetNormal() * 1.5;
                  StartPos = StartPos - offsetStart;
                }

                Point3d midPoint = new Point3d(
                   (StartPos.X + newPoint.X) / 2.0,
                   (StartPos.Y + newPoint.Y) / 2.0,
                   StartPos.Z
               );

                Point3d anotherNewPoint = new Point3d(
                    midPoint.X + rotatedOffsetX,
                    midPoint.Y + rotatedOffsetY,
                    midPoint.Z
                );
                Vector3d direction2 = new Vector3d(anotherNewPoint.X - midPoint.X, anotherNewPoint.Y - midPoint.Y, 0);
                Vector3d offset2 = direction2.GetNormal() * offsetDistance2;

                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight - route.Length, anotherNewPoint, midPoint, route.Id);
                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight - route.Length, newPoint, StartPos, route.Id);

                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight, StartPos, newPoint, route.Id);
                Point3d fixturePos = new Point3d(anotherNewPoint.X - offset2.X, anotherNewPoint.Y - offset2.Y, anotherNewPoint.Z);
                SpecializedHorizontalRoute("ColdWater", route.PipeType, route.StartHeight, midPoint, fixturePos, route.Id);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                  //Place the fixture block
                  BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                  br.Rotation = route.Rotation;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;


                  Circle circle = new Circle(newPoint, Vector3d.ZAxis, 1);
                  circle.Layer = "P-DOMW-CWTR";
                  modelSpace.AppendEntity(circle);
                  tr.AddNewlyCreatedDBObject(circle, true);

                  Hatch hatch = new Hatch();
                  hatch.SetDatabaseDefaults();
                  hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                  hatch.Associative = false;
                  hatch.Layer = "P-DOMW-CWTR";
                  modelSpace.AppendEntity(hatch);
                  tr.AddNewlyCreatedDBObject(hatch, true);
                  ObjectIdCollection ids = new ObjectIdCollection { circle.ObjectId };
                  hatch.AppendLoop(HatchLoopTypes.Default, ids);
                  hatch.Elevation = newPoint.Z;

                  hatch.EvaluateHatch(true);
                  circle.Erase();

                  //placing the line :3
                  Vector3d routeVec = newPoint - StartPos;
                  double routeLength = routeVec.Length;
                  if (routeLength == 0) return;

                  Vector3d normal = new Vector3d(-routeVec.Y, routeVec.X, 0).GetNormal();

                  double offsetDistance3 = 4.0;
                  Point3d offsetMid = newPoint - (normal * offsetDistance3);
                  if (res.StringResult == "Right") {
                    offsetMid = newPoint + (normal * offsetDistance3);
                  }

                  Vector3d halfVec = routeVec.GetNormal() * (routeLength / 4.0);
                  Point3d newStart = offsetMid - halfVec;
                  Point3d newEnd = offsetMid + halfVec;

                  Line line = new Line(newStart, newEnd);
                  line.Layer = "P-DOMW-CWTR";
                  modelSpace.AppendEntity(line);
                  tr.AddNewlyCreatedDBObject(line, true);

                  tr.Commit();
                }
              }
            }
            else if (blockName == "GMEP HW FIXTURE POINT") {
              PlumbingVerticalRoute route2 = VerticalRoute("HotWater", startHeight, CADObjectCommands.ActiveFloor, "Down", startHeight, null, "Vertical route down to below floor: ").First().Value;
              CircleStartPointPreviewJig circleJig = new CircleStartPointPreviewJig(route2.Position, 1.5);
              PromptResult circlePromptResult = ed.Drag(circleJig);
              if (circlePromptResult.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Point3d firstPoint = circleJig.ProjectedPoint;
              HorizontalRoute(0, route2.Type, false, "Forward", firstPoint);
              
              PlumbingVerticalRoute route = VerticalRoute("HotWater", 0, CADObjectCommands.ActiveFloor, "Up", routeHeight, null, "Vertical route back up to fixture height: ", "Flush Tank").First().Value;
              
              double offsetDistance = 11.25;
              double offsetDistance2 = 2.125;
              double offsetX = offsetDistance * Math.Cos(route.Rotation + (Math.PI / 2));
              double offsetY = offsetDistance * Math.Sin(route.Rotation + (Math.PI / 2));
              Point3d newPoint = new Point3d(
                  route.Position.X + offsetX,
                  route.Position.Y + offsetY,
                  route.Position.Z
              );
              Vector3d direction = new Vector3d(newPoint.X - route.Position.X, newPoint.Y - route.Position.Y, 0);
              Vector3d offset2 = direction.GetNormal() * offsetDistance2;

              SpecializedHorizontalRoute("HotWater", route.PipeType, route.StartHeight - route.Length, newPoint, route.Position, route.Id);
              Point3d fixturePos = new Point3d(newPoint.X - offset2.X, newPoint.Y - offset2.Y, newPoint.Z);
              SpecializedHorizontalRoute("HotWater", route.PipeType, route.StartHeight, route.Position, fixturePos, route.Id);

              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                br.Rotation = route.Rotation;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }
            }
            else if (blockName == "GMEP PLUMBING GAS OUTPUT") {
              PlumbingVerticalRoute route2 = VerticalRoute("Gas", startHeight, CADObjectCommands.ActiveFloor, "Down", startHeight).First().Value;
              CircleStartPointPreviewJig circleJig = new CircleStartPointPreviewJig(route2.Position, 1.5);
              PromptResult circlePromptResult = ed.Drag(circleJig);
              if (circlePromptResult.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Point3d firstPoint = circleJig.ProjectedPoint;
              HorizontalRoute(0, route2.Type, false, "Forward", firstPoint);
              PlumbingVerticalRoute route = VerticalRoute("Gas", 0, CADObjectCommands.ActiveFloor, "Up", routeHeight).First().Value;
              double offsetDistance = 10.25;
              double offsetDistance2 = 3.5;
              double offsetX = offsetDistance * Math.Cos(route.Rotation + (Math.PI / 2));
              double offsetY = offsetDistance * Math.Sin(route.Rotation + (Math.PI / 2));
              Point3d newPoint = new Point3d(
                  route.Position.X + offsetX,
                  route.Position.Y + offsetY,
                  route.Position.Z
              );
              Vector3d direction = new Vector3d(newPoint.X - route.Position.X, newPoint.Y - route.Position.Y, 0);
              Vector3d offset2 = direction.GetNormal() * offsetDistance2;
    
              SpecializedHorizontalRoute("Gas", route.PipeType, route.StartHeight - route.Length, newPoint, route.Position, route.Id);
              Point3d fixturePos = new Point3d(newPoint.X - offset2.X, newPoint.Y - offset2.Y, newPoint.Z);
              SpecializedHorizontalRoute("Gas", route.PipeType, route.StartHeight, route.Position, fixturePos, route.Id);

              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(fixturePos, btr.ObjectId);
                br.Rotation = route.Rotation + Math.PI / 2;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }
            }
            else {
              routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);
              if (blockName == "GMEP DRAIN") {
                zIndex = CADObjectCommands.ActiveFloorHeight * 12;
              }
              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr;

                BlockReference br = null;
                if (placementPoint == null) {
                  br = CADObjectCommands.CreateBlockReference(
                    tr,
                    bt,
                    blockName,
                    "Plumbing Fixture " + selectedFixtureType.Name,
                    out btr,
                    out point
                  );
                }
                else {
                  btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  br = new BlockReference((Point3d)placementPoint, btr.ObjectId);
                  point = (Point3d)placementPoint;
                }
                if (br != null) {
                  BlockTableRecord curSpace = (BlockTableRecord)
                    tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                  if (blockRotation == null) {
                    RotateJig rotateJig = new RotateJig(br);
                    PromptResult rotatePromptResult = ed.Drag(rotateJig);
                    if (rotatePromptResult.Status != PromptStatus.OK) {
                      ed.WriteMessage("\nRotation cancelled.");
                      routeHeightDisplay.Disable();
                      return;
                    }
                  }
                  else {
                    br.Rotation = blockRotation.Value;
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
                routeHeightDisplay.Disable();
                blockId = br.Id;
                tr.Commit();
              }
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
                if (prop.PropertyName == "flow_type_id") {
                  prop.Value = flowTypeId;
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
                blockName,
                flowTypeId
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
              blockName,
              flowTypeId
            );

            if (blockName == "GMEP DRAIN") {
              Dictionary<string, PlumbingVerticalRoute> ventRoutes = null;
              ventRoutes = VerticalRoute("Vent", 0, CADObjectCommands.ActiveFloor, "Up", CADObjectCommands.ActiveRouteHeight);
             
              Point3d ventPoint = ventRoutes[CADObjectCommands.ActiveBasePointId].Position;
              ventPoint = new Point3d(ventPoint.X, ventPoint.Y, point.Z);
              double shortenBy = 1.5;
              Vector3d direction = point - ventPoint;
              double length = direction.Length;

              Point3d newEndPoint = ventPoint;
              Point3d newStartPoint = point;
              if (length > 0) {
                Vector3d offset = direction.GetNormal() * shortenBy;
                newEndPoint = ventPoint + offset;
                newStartPoint = point - offset;
              }

              SpecializedHorizontalRoute(
                   "Waste", "", 0, newStartPoint, newEndPoint
              );
              Dictionary<string, PlumbingVerticalRoute> ventRoutes2 = null;
              ventRoutes2 = VerticalRoute("Vent", CADObjectCommands.ActiveRouteHeight, CADObjectCommands.ActiveFloor, "Down", CADObjectCommands.ActiveRouteHeight);
              Point3d ventPoint2 = ventRoutes2[CADObjectCommands.ActiveBasePointId].Position;
              SpecializedHorizontalRoute(
                   "Vent", "", CADObjectCommands.ActiveRouteHeight, ventPoint, ventPoint2
              );
              HorizontalRoute(0, "Vent");

              PromptKeywordOptions pko = new PromptKeywordOptions("How far up?");
              pko.Keywords.Add("Ceiling");
              pko.Keywords.Add("Roof");
              pko.Keywords.Add("None", "No Vent Needed", "No Vent Needed");
              pko.AllowNone = false;
              PromptResult res = ed.GetKeywords(pko);
              ed.WriteMessage("\nYou selected: " + res.StringResult);
              if (res.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              if (res.StringResult == "None") {
               continue;
             }
             if (res.StringResult == "Ceiling") {
               ventRoutes = VerticalRoute("Vent", 0, CADObjectCommands.ActiveFloor, "UpToCeiling");
             }
             else if (res.StringResult == "Roof") {
               List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD(ProjectId);
               PlumbingPlanBasePoint activeBasePoint = basePoints.Where(bp => bp.Id == CADObjectCommands.ActiveBasePointId).First();
               List<PlumbingPlanBasePoint> aboveBasePoints = basePoints.Where(bp => bp.Floor >= activeBasePoint.Floor && bp.ViewportId == activeBasePoint.ViewportId).ToList();
               PlumbingPlanBasePoint highestFloorBasePoint = aboveBasePoints
               .OrderByDescending(bp => bp.Floor)
               .FirstOrDefault();

               ventRoutes = VerticalRoute("Vent", 0, highestFloorBasePoint.Floor, "UpToCeiling", null, highestFloorBasePoint.CeilingHeight - highestFloorBasePoint.FloorHeight);
               if (highestFloorBasePoint.Floor > CADObjectCommands.ActiveFloor) {
                 PlumbingVerticalRoute startRoute = ventRoutes.First().Value;
                 ZoomToPoint(ed, startRoute.Position);
               }
             }
             if (ventRoutes == null || !ventRoutes.ContainsKey(CADObjectCommands.ActiveBasePointId)) {
               ed.WriteMessage("\nError: Could not find vent route for base point.");
               return;
             }

            }
          }
          catch (System.Exception ex) {
            ed.WriteMessage("Fixture Error - " + ex.ToString());
            routeHeightDisplay.Disable();
            Console.WriteLine(ex.ToString());
          }
        }
        MakePlumbingFixtureLabel(plumbingFixture, selectedFixtureType);
      }
      routeHeightDisplay.Disable();
    }

    [CommandMethod("PlumbingExtendedFixture")]
    public void PlumbingExtendedFixture() {
      ExtendedFixture();
    }
    public void ExtendedFixture(string fixtureString = null, string catalogString = null, Point3d? placementPoint = null, double? blockRotation = null) {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);

      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      string basePointId = CADObjectCommands.GetActiveView();

      List<PlumbingFixtureType> plumbingFixtureTypes = MariaDBService.GetPlumbingFixtureTypes();
      Dictionary<int, List<PlumbingFixtureCatalogItem>> allPlumbingFixtureCatalogItems =
        MariaDBService.GetAllPlumbingFixtureCatalogItems();

      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
      PromptResult keywordResult;

      if (fixtureString == null) {
        keywordOptions.Message = "\nSelect fixture type:";
        plumbingFixtureTypes.ForEach(t => {
          if (allPlumbingFixtureCatalogItems.ContainsKey(t.Id)) {
            List<PlumbingFixtureCatalogItem> catalogItems = allPlumbingFixtureCatalogItems[t.Id];
            if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && catalogItems.Any(item => !string.IsNullOrEmpty(item.WaterBlockNames) && (item.WaterBlockNames.Contains("GMEP HW FIXTURE POINT") || item.WaterBlockNames.Contains("GMEP CW FIXTURE POINT")))) || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && catalogItems.Any(item => !string.IsNullOrEmpty(item.WasteBlockNames) && (item.WaterBlockNames.Contains("GMEP HW FIXTURE POINT") || item.WaterBlockNames.Contains("GMEP CW FIXTURE POINT")))) || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && catalogItems.Any(item => !string.IsNullOrEmpty(item.GasBlockNames)))) && ((CADObjectCommands.IsResidential && !catalogItems.All(item => item.Residential == false)) || (!CADObjectCommands.IsResidential && !catalogItems.All(item => item.Commercial == false)))) {
              keywordOptions.Keywords.Add(t.Abbreviation + " - " + t.Name);
            }
          }
        });
        //keywordOptions.Keywords.Default = "WC - Water Closet";
        keywordOptions.AllowNone = false;
        keywordResult = ed.GetKeywords(keywordOptions);

        if (keywordResult.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return;
        }
        fixtureString = keywordResult.StringResult;
        ed.WriteMessage("\nSelected fixture: " + fixtureString);
      }
      PlumbingFixtureType selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t =>
        fixtureString.StartsWith(t.Abbreviation)
      );
      if (selectedFixtureType == null) {
        selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t => t.Abbreviation == "WC");
      }

      List<PlumbingFixtureCatalogItem> plumbingFixtureCatalogItems =
        MariaDBService.GetPlumbingFixtureCatalogItemsByType(selectedFixtureType.Id);

      if (catalogString == null) {
        keywordOptions = new PromptKeywordOptions("");
        keywordOptions.Message = "\nSelect catalog item:";
        plumbingFixtureCatalogItems.ForEach(i => {
          if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && i.WaterBlockNames != "" && (i.WaterBlockNames.Contains("GMEP CW FIXTURE POINT") || i.WaterBlockNames.Contains("GMEP HW FIXTURE POINT"))) || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && i.WasteBlockNames != "" && (i.WaterBlockNames.Contains("GMEP CW FIXTURE POINT") || i.WaterBlockNames.Contains("GMEP HW FIXTURE POINT"))) || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && i.GasBlockNames != "")) && ((CADObjectCommands.IsResidential && i.Residential == true) || (!CADObjectCommands.IsResidential && i.Commercial == true))) {
            keywordOptions.Keywords.Add(i.Id.ToString() + " - " + i.Description + " - " + i.Make + " " + i.Model);
          }
        });
        keywordResult = ed.GetKeywords(keywordOptions);

        catalogString = keywordResult.StringResult;
      }
      if (catalogString.Contains(' ')) {
        catalogString = catalogString.Split(' ')[0];
      }
      PlumbingFixtureCatalogItem selectedCatalogItem = plumbingFixtureCatalogItems.FirstOrDefault(
        i => i.Id.ToString() == catalogString
      );
      if (selectedCatalogItem == null) {
        return;
      }

      int flowTypeId = 1;
      if (selectedFixtureType.Abbreviation == "U" || selectedCatalogItem.Id == 6) {
        flowTypeId = 2;
      }
      List<string> selectedBlockNames = new List<string>();
      string viewType = GetPlumbingBasePointsFromCAD(ProjectId).Where(bp => bp.Id == basePointId).First().Type;
      if (viewType.Contains("Water")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WaterBlockNames.Split(','));
      }
      if (viewType.Contains("Sewer-Vent")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WasteBlockNames.Split(','));
      }
      if (viewType.Contains("Gas")) {
        selectedBlockNames.AddRange(selectedCatalogItem.GasBlockNames.Split(','));
      }
      selectedBlockNames = selectedBlockNames.Distinct().ToList();
      List<string> selectedBlockNames2 = new List<string>(selectedBlockNames);

      foreach (string blockName in selectedBlockNames) {
        if (blockName.Contains("%WHSIZE%")) {
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
            selectedBlockNames2[selectedBlockNames.IndexOf(blockName)] = blockName.Replace(
              "%WHSIZE%",
              whSize
            );
          }
        }
      }

      double routeHeight = CADObjectCommands.GetPlumbingRouteHeight();
      if (selectedFixtureType.Abbreviation == "WH" || selectedFixtureType.Abbreviation == "IWH") {
        PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the fixture from the floor (in feet): ");
        pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();
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
      }

      PlumbingFixture plumbingFixture = null;

      var routeHeightDisplay = new RouteHeightDisplay(ed);

      if (selectedBlockNames2.Count() != 0) {
        foreach (string blockName in selectedBlockNames2) {
          ObjectId blockId = ObjectId.Null;
          Point3d point = Point3d.Origin;
          double rotation = 0;
          int number = 0;
          string GUID = Guid.NewGuid().ToString();
          double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;
          double startHeight = CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight;
          double verticalRouteLength = startHeight - routeHeight;

          try {
            if (blockName == "GMEP CW FIXTURE POINT") {
              if (flowTypeId == 1) {
                PlumbingVerticalRoute route = VerticalRoute("ColdWater", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength, null, "Vertical Route", "Flush Tank").First().Value;

                double offsetDistance = 2.125;

                CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
                PromptResult jigResult = ed.Drag(jig);
                if (jigResult.Status != PromptStatus.OK) {
                  ed.WriteMessage("\nCommand cancelled.");
                  return;
                }
                Point3d firstPoint = jig.ProjectedPoint;

                List<PlumbingHorizontalRoute> routes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "\nSelect Start Point For Route: ", "\nSelect a Line: ", "\nSelect End Point: ", route.Id);
                foreach (PlumbingHorizontalRoute r in routes) {
                  Point3d endPoint = r.EndPoint;
                  if (r == routes.Last()) {
                    Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                    Vector3d offset = direction.GetNormal() * offsetDistance;
                    endPoint -= offset;
                  }
                  SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
                }
                Vector3d direction2 = new Vector3d(routes.Last().StartPoint.X - routes.Last().EndPoint.X, routes.Last().StartPoint.Y - routes.Last().EndPoint.Y, 0);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                  BlockReference br = new BlockReference(routes.Last().EndPoint, btr.ObjectId);
                  br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI / 2;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;
                  tr.Commit();
                }
              }
              else if (flowTypeId == 2) {
                PlumbingVerticalRoute route = VerticalRoute("ColdWater", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength, null, "Vertical Route", "Flush Valve").First().Value;
                double offsetDistance = 2.125;

                CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
                PromptResult jigResult = ed.Drag(jig);
                if (jigResult.Status != PromptStatus.OK) {
                  ed.WriteMessage("\nCommand cancelled.");
                  return;
                }
                Point3d firstPoint = jig.ProjectedPoint;

                List<PlumbingHorizontalRoute> routes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "\nSelect start point for route: ", "\nSelect next line in route toward WHA: ", route.Id);
                foreach (PlumbingHorizontalRoute r in routes) {
                  SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, r.EndPoint, r.StartPoint, route.Id);
                }
         

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);


                  Point3d newPos = new Point3d(routes.Last().EndPoint.X, routes.Last().EndPoint.Y, routes.Last().EndPoint.Z - (route.Length * 12));
                  Circle circle = new Circle(newPos, Vector3d.ZAxis, 1);
                  modelSpace.AppendEntity(circle);
                  tr.AddNewlyCreatedDBObject(circle, true);

                  Hatch hatch = new Hatch();
                  hatch.SetDatabaseDefaults();
                  hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                  hatch.Associative = false;
                  hatch.Layer = "P-DOMW-CWTR";
                  hatch.Elevation = newPos.Z;

                  modelSpace.AppendEntity(hatch);
                  tr.AddNewlyCreatedDBObject(hatch, true);

                  ObjectIdCollection ids = new ObjectIdCollection { circle.ObjectId };
                  hatch.AppendLoop(HatchLoopTypes.Default, ids);
                  hatch.EvaluateHatch(true);
                  circle.Erase();

                  tr.Commit();
                }

                OffsetLineJig lineJig = new OffsetLineJig(routes.Last(), 4.0);
                PromptResult linePromptResult = ed.Drag(lineJig);
                if (linePromptResult.Status != PromptStatus.OK) {
                  ed.WriteMessage("\nCommand cancelled.");
                  return;
                }
                Line line = lineJig.GetOffsetLine();
                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                  
                  line.Layer = "P-DOMW-CWTR";
                  modelSpace.AppendEntity(line);
                  tr.AddNewlyCreatedDBObject(line, true);
                  tr.Commit();
                }

                List<PlumbingHorizontalRoute> fixtureRoutes = HorizontalRoute(routeHeight, route.Type, false, "Forward", null, false, "", "\nSelect line to route to fixture: ", route.Id);
                foreach (PlumbingHorizontalRoute r in fixtureRoutes) {
                  Point3d endPoint = r.EndPoint;
                  if (r == fixtureRoutes.Last()) {
                    Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                    Vector3d offset = direction.GetNormal() * offsetDistance;
                    endPoint -= offset;
                  }
                  SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
                }
                Vector3d direction2 = new Vector3d(fixtureRoutes.Last().StartPoint.X - fixtureRoutes.Last().EndPoint.X, fixtureRoutes.Last().StartPoint.Y - fixtureRoutes.Last().EndPoint.Y, 0);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                  BlockReference br = new BlockReference(fixtureRoutes.Last().EndPoint, btr.ObjectId);
                  br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI / 2;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;
                  tr.Commit();
                }
              }
            }
            else if (blockName == "GMEP HW FIXTURE POINT") {
              PlumbingVerticalRoute route = VerticalRoute("HotWater", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength, null, "Vertical Route", "Flush Tank").First().Value;
              double offsetDistance = 2.125;

              CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
              PromptResult jigResult = ed.Drag(jig);
              if (jigResult.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Point3d firstPoint = jig.ProjectedPoint;

              List<PlumbingHorizontalRoute> routes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "\nSpecify start point for route: ", "\nSelect a Line: ", "\nSelect End Point: ", route.Id);
              foreach (PlumbingHorizontalRoute r in routes) {
                Point3d endPoint = r.EndPoint;
                if (r == routes.Last()) {
                  Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                  Vector3d offset = direction.GetNormal() * offsetDistance;
                  endPoint -= offset;
                }
                SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
              }
              Vector3d direction2 = new Vector3d(routes.Last().StartPoint.X - routes.Last().EndPoint.X, routes.Last().StartPoint.Y - routes.Last().EndPoint.Y, 0);


              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(routes.Last().EndPoint, btr.ObjectId);
                br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI / 2;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }
              
            }
            else if (blockName == "GMEP PLUMBING GAS OUTPUT") {
              PlumbingVerticalRoute route = VerticalRoute("Gas", startHeight, CADObjectCommands.ActiveFloor, "Down", verticalRouteLength).First().Value;
              double offsetDistance = 3.5;

              CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
              PromptResult jigResult = ed.Drag(jig);
              if (jigResult.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Point3d firstPoint = jig.ProjectedPoint;

              List<PlumbingHorizontalRoute> routes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "\nSpecify start point for route: ", "\nSelect a Line: ", "\nSelect End Point: ", route.Id);
              foreach (PlumbingHorizontalRoute r in routes) {
                Point3d endPoint = r.EndPoint;
                if (r == routes.Last()) {
                  Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                  Vector3d offset = direction.GetNormal() * offsetDistance;
                  endPoint -= offset;
                }
                SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
              }
              Vector3d direction2 = new Vector3d(routes.Last().StartPoint.X - routes.Last().EndPoint.X, routes.Last().StartPoint.Y - routes.Last().EndPoint.Y, 0);

              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(routes.Last().EndPoint, btr.ObjectId);
                br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }
            }
            else {
              routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);
              if (blockName == "GMEP DRAIN") {
                zIndex = CADObjectCommands.ActiveFloorHeight * 12;
              }
              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr;

                BlockReference br = null;
                if (placementPoint == null) {
                  br = CADObjectCommands.CreateBlockReference(
                    tr,
                    bt,
                    blockName,
                    "Plumbing Fixture " + selectedFixtureType.Name,
                    out btr,
                    out point
                  );
                }
                else {
                  btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  br = new BlockReference((Point3d)placementPoint, btr.ObjectId);
                  point = (Point3d)placementPoint;
                }
                if (br != null) {
                  BlockTableRecord curSpace = (BlockTableRecord)
                    tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                  if (blockRotation == null) {
                    RotateJig rotateJig = new RotateJig(br);
                    PromptResult rotatePromptResult = ed.Drag(rotateJig);
                    if (rotatePromptResult.Status != PromptStatus.OK) {
                      ed.WriteMessage("\nRotation cancelled.");
                      routeHeightDisplay.Disable();
                      return;
                    }
                  }
                  else {
                    br.Rotation = blockRotation.Value;
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
                routeHeightDisplay.Disable();
                blockId = br.Id;
                tr.Commit();
              }
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
                if (prop.PropertyName == "flow_type_id") {
                  prop.Value = flowTypeId;
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
                blockName,
                flowTypeId
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
              blockName,
              flowTypeId
            );

            if (blockName == "GMEP DRAIN") {
              PromptKeywordOptions pko = new PromptKeywordOptions("How far up?");
              pko.Keywords.Add("Ceiling");
              pko.Keywords.Add("Roof");
              pko.Keywords.Add("None", "No Vent Needed", "No Vent Needed");
              pko.AllowNone = false;
              PromptResult res = ed.GetKeywords(pko);
              ed.WriteMessage("\nYou selected: " + res.StringResult);
              if (res.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Dictionary<string, PlumbingVerticalRoute> ventRoutes = null;
              if (res.StringResult == "None") {
                continue;
              }
              if (res.StringResult == "Ceiling") {
                ventRoutes = VerticalRoute("Vent", 0, CADObjectCommands.ActiveFloor, "UpToCeiling");
              }
              else if (res.StringResult == "Roof") {
                List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD(ProjectId);
                PlumbingPlanBasePoint activeBasePoint = basePoints.Where(bp => bp.Id == CADObjectCommands.ActiveBasePointId).First();
                List<PlumbingPlanBasePoint> aboveBasePoints = basePoints.Where(bp => bp.Floor >= activeBasePoint.Floor && bp.ViewportId == activeBasePoint.ViewportId).ToList();
                PlumbingPlanBasePoint highestFloorBasePoint = aboveBasePoints
                .OrderByDescending(bp => bp.Floor)
                .FirstOrDefault();

                ventRoutes = VerticalRoute("Vent", 0, highestFloorBasePoint.Floor, "UpToCeiling", null, highestFloorBasePoint.CeilingHeight - highestFloorBasePoint.FloorHeight);
                if (highestFloorBasePoint.Floor > CADObjectCommands.ActiveFloor) {
                  PlumbingVerticalRoute startRoute = ventRoutes.First().Value;
                  ZoomToPoint(ed, startRoute.Position);
                }
              }
              if (ventRoutes == null || !ventRoutes.ContainsKey(CADObjectCommands.ActiveBasePointId)) {
                ed.WriteMessage("\nError: Could not find vent route for base point.");
                return;
              }
              Point3d ventPoint = ventRoutes[CADObjectCommands.ActiveBasePointId].Position;
              ventPoint = new Point3d(ventPoint.X, ventPoint.Y, point.Z);
              double shortenBy = 1.5;
              Vector3d direction = point - ventPoint;
              double length = direction.Length;

              Point3d newEndPoint = ventPoint;
              Point3d newStartPoint = point;
              if (length > 0) {
                Vector3d offset = direction.GetNormal() * shortenBy;
                newEndPoint = ventPoint + offset;
                newStartPoint = point - offset;
              }

              SpecializedHorizontalRoute(
                   "Waste", "", 0, newStartPoint, newEndPoint
              );
            }
          }
          catch (System.Exception ex) {
            ed.WriteMessage("Fixture Error - " + ex.ToString());
            routeHeightDisplay.Disable();
            Console.WriteLine(ex.ToString());
          }
        }
        MakePlumbingFixtureLabel(plumbingFixture, selectedFixtureType);
      }
    }
    [CommandMethod("PlumbingSharedFixture")]
    public void PlumbingSharedFixture() {
      SharedFixture();
    }
    public void SharedFixture(string fixtureString = null, string catalogString = null, Point3d? placementPoint = null, double? blockRotation = null) {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);

      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      CADObjectCommands.GetActiveView();

      string basePointId = CADObjectCommands.GetActiveView();

      List<PlumbingFixtureType> plumbingFixtureTypes = MariaDBService.GetPlumbingFixtureTypes();
      Dictionary<int, List<PlumbingFixtureCatalogItem>> allPlumbingFixtureCatalogItems =
        MariaDBService.GetAllPlumbingFixtureCatalogItems();

      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
      PromptResult keywordResult;

      if (fixtureString == null) {
        keywordOptions.Message = "\nSelect fixture type:";
        plumbingFixtureTypes.ForEach(t => {
          if (allPlumbingFixtureCatalogItems.ContainsKey(t.Id)) {
            List<PlumbingFixtureCatalogItem> catalogItems = allPlumbingFixtureCatalogItems[t.Id];
            if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && catalogItems.Any(item => !string.IsNullOrEmpty(item.WaterBlockNames) && (item.WaterBlockNames.Contains("GMEP HW FIXTURE POINT") || item.WaterBlockNames.Contains("GMEP CW FIXTURE POINT")))) || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && catalogItems.Any(item => !string.IsNullOrEmpty(item.WasteBlockNames) && (item.WaterBlockNames.Contains("GMEP HW FIXTURE POINT") || item.WaterBlockNames.Contains("GMEP CW FIXTURE POINT")))) || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && catalogItems.Any(item => !string.IsNullOrEmpty(item.GasBlockNames)))) && ((CADObjectCommands.IsResidential && !catalogItems.All(item => item.Residential == false)) || (!CADObjectCommands.IsResidential && !catalogItems.All(item => item.Commercial == false)))) {
              keywordOptions.Keywords.Add(t.Abbreviation + " - " + t.Name);
            }
          }
        });
        //keywordOptions.Keywords.Default = "WC - Water Closet";
        keywordOptions.AllowNone = false;
        keywordResult = ed.GetKeywords(keywordOptions);

        if (keywordResult.Status != PromptStatus.OK) {
          ed.WriteMessage("\nCommand cancelled.");
          return;
        }
        fixtureString = keywordResult.StringResult;
        ed.WriteMessage("\nSelected fixture: " + fixtureString);
      }
      PlumbingFixtureType selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t =>
        fixtureString.StartsWith(t.Abbreviation)
      );
      if (selectedFixtureType == null) {
        selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t => t.Abbreviation == "WC");
      }

      List<PlumbingFixtureCatalogItem> plumbingFixtureCatalogItems =
        MariaDBService.GetPlumbingFixtureCatalogItemsByType(selectedFixtureType.Id);

      if (catalogString == null) {
        keywordOptions = new PromptKeywordOptions("");
        keywordOptions.Message = "\nSelect catalog item:";
        plumbingFixtureCatalogItems.ForEach(i => {
          if (((CADObjectCommands.ActiveViewTypes.Contains("Water") && i.WaterBlockNames != "" && (i.WaterBlockNames.Contains("GMEP CW FIXTURE POINT") || i.WaterBlockNames.Contains("GMEP HW FIXTURE POINT"))) || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && i.WasteBlockNames != "" && (i.WaterBlockNames.Contains("GMEP CW FIXTURE POINT") || i.WaterBlockNames.Contains("GMEP HW FIXTURE POINT"))) || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && i.GasBlockNames != "")) && ((CADObjectCommands.IsResidential && i.Residential == true) || (!CADObjectCommands.IsResidential && i.Commercial == true))) {
            keywordOptions.Keywords.Add(i.Id.ToString() + " - " + i.Description + " - " + i.Make + " " + i.Model);
          }
        });
        keywordResult = ed.GetKeywords(keywordOptions);

        catalogString = keywordResult.StringResult;
      }
      if (catalogString.Contains(' ')) {
        catalogString = catalogString.Split(' ')[0];
      }
      PlumbingFixtureCatalogItem selectedCatalogItem = plumbingFixtureCatalogItems.FirstOrDefault(
        i => i.Id.ToString() == catalogString
      );
      if (selectedCatalogItem == null) {
        return;
      }

      int flowTypeId = 1;
      if (selectedFixtureType.Abbreviation == "U" || selectedCatalogItem.Id == 6) {
        flowTypeId = 2;
      }
      List<string> selectedBlockNames = new List<string>();
      string viewType = GetPlumbingBasePointsFromCAD(ProjectId).Where(bp => bp.Id == basePointId).First().Type;
      if (viewType.Contains("Water")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WaterBlockNames.Split(','));
      }
      if (viewType.Contains("Sewer-Vent")) {
        selectedBlockNames.AddRange(selectedCatalogItem.WasteBlockNames.Split(','));
      }
      if (viewType.Contains("Gas")) {
        selectedBlockNames.AddRange(selectedCatalogItem.GasBlockNames.Split(','));
      }
      selectedBlockNames = selectedBlockNames.Distinct().ToList();
      List<string> selectedBlockNames2 = new List<string>(selectedBlockNames);


      foreach (string blockName in selectedBlockNames) {
        if (blockName.Contains("%WHSIZE%")) {
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
            selectedBlockNames2[selectedBlockNames.IndexOf(blockName)] = blockName.Replace(
              "%WHSIZE%",
              whSize
            );
          }
        }
      }

      double routeHeight = CADObjectCommands.GetPlumbingRouteHeight();
      if (selectedFixtureType.Abbreviation == "WH" || selectedFixtureType.Abbreviation == "IWH") {
        PromptDoubleOptions pdo = new PromptDoubleOptions("\nEnter the height of the fixture from the floor (in feet): ");
        pdo.DefaultValue = CADObjectCommands.GetPlumbingRouteHeight();
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
      }

      PlumbingFixture plumbingFixture = null;

      var routeHeightDisplay = new RouteHeightDisplay(ed);

      if (selectedBlockNames2.Count() != 0) {
        foreach (string blockName in selectedBlockNames2) {
          ObjectId blockId = ObjectId.Null;
          Point3d point = Point3d.Origin;
          double rotation = 0;
          int number = 0;
          string GUID = Guid.NewGuid().ToString();
          double zIndex = (routeHeight + CADObjectCommands.ActiveFloorHeight) * 12;
          double startHeight = CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight;
          double verticalRouteLength = startHeight - routeHeight;

          try {
            if (blockName == "GMEP CW FIXTURE POINT") {
              if (flowTypeId == 1) {
                PlumbingVerticalRoute route = null;
                Point3d firstPoint = Point3d.Origin;
                while (true) {
                  PromptEntityOptions peo = new PromptEntityOptions("\nSelect existing drop or vertical route to attach to: ");
                  peo.SetRejectMessage("\nMust select a drop or vertical route.");
                  peo.AddAllowedClass(typeof(BlockReference), true);
                  peo.AddAllowedClass(typeof(Line), true);
                  PromptEntityResult per = ed.GetEntity(peo);
                  if (per.Status != PromptStatus.OK) {
                    ed.WriteMessage("\nCommand cancelled.");
                    return;
                  }
                  using (Transaction tr = db.TransactionManager.StartTransaction()) {
                    DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (obj == null) {
                      continue;
                    }
                    if (obj is BlockReference blockRef && FindObjectType(blockRef) == "VerticalRoute") {
                      PlumbingVerticalRoute selectedRoute = AssembleVerticalRoute(blockRef);
                      if (selectedRoute == null || selectedRoute.FixtureType != "Flush Tank" || selectedRoute.Type != "Cold Water") {
                        ed.WriteMessage("\nMust select a Flush Tank Route, Cold Water: ");
                        continue;
                      }
                      route = selectedRoute;
                      CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
                      PromptResult jigResult = ed.Drag(jig);
                      if (jigResult.Status != PromptStatus.OK) {
                        ed.WriteMessage("\nCommand cancelled.");
                        return;
                      }
                      firstPoint = jig.ProjectedPoint;
                      break;
                    }
                    else if (obj is Line line) {
                      ResultBuffer xdata = line.GetXDataForApplication(XRecordKey);
                      if (xdata != null && xdata.AsArray().Length >= 6) {
                        string dropId = xdata.AsArray()[5].Value.ToString();
                        PlumbingVerticalRoute selectedRoute = GetVerticalRoutesFromCAD().Where(r => r.Id == dropId).FirstOrDefault();
                        if (selectedRoute == null || selectedRoute.FixtureType != "Flush Tank" || selectedRoute.Type != "Cold Water") {
                          ed.WriteMessage("\nMust select a Flush Tank Route, Cold Water: ");
                          continue;
                        }
                        route = selectedRoute;
                        LineStartPointPreviewJig jig = new LineStartPointPreviewJig(line);
                        PromptResult jigResult = ed.Drag(jig);
                        if (jigResult.Status != PromptStatus.OK) {
                          ed.WriteMessage("\nCommand cancelled.");
                          return;
                        }
                        firstPoint = jig.ProjectedPoint;
                        break;
                      }
                    }
                  }
                }

                double offsetDistance = 2.125;

                List<PlumbingHorizontalRoute> routes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "\nSpecify start point for route: ", "\nSelect a Line: ", "Select End Point: ", route.Id);
                foreach (PlumbingHorizontalRoute r in routes) {
                  Point3d endPoint = r.EndPoint;
                  if (r == routes.Last()) {
                    Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                    Vector3d offset = direction.GetNormal() * offsetDistance;
                    endPoint -= offset;
                  }
                  SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
                }
                Vector3d direction2 = new Vector3d(routes.Last().StartPoint.X - routes.Last().EndPoint.X, routes.Last().StartPoint.Y - routes.Last().EndPoint.Y, 0);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                  BlockReference br = new BlockReference(routes.Last().EndPoint, btr.ObjectId);
                  br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI / 2;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;
                  tr.Commit();
                }
              }
              else if (flowTypeId == 2) {
                PlumbingVerticalRoute route = null;
                Point3d firstPoint = Point3d.Origin;
                while (true) {
                  PromptEntityOptions peo = new PromptEntityOptions("\nSelect existing drop or vertical route to attach to: ");
                  peo.SetRejectMessage("\nMust select a drop or vertical route.");
                  peo.AddAllowedClass(typeof(BlockReference), true);
                  peo.AddAllowedClass(typeof(Line), true);
                  PromptEntityResult per = ed.GetEntity(peo);
                  if (per.Status != PromptStatus.OK) {
                    ed.WriteMessage("\nCommand cancelled.");
                    return;
                  }
                  using (Transaction tr = db.TransactionManager.StartTransaction()) {
                    DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (obj == null) {
                      continue;
                    }
                    if (obj is BlockReference blockRef && FindObjectType(blockRef) == "VerticalRoute") {
                      PlumbingVerticalRoute selectedRoute = AssembleVerticalRoute(blockRef);
                      if (selectedRoute == null || selectedRoute.FixtureType != "Flush Valve" || selectedRoute.Type != "Cold Water") {
                        ed.WriteMessage("\nMust select a Flush Valve Route, Cold Water: ");
                        continue;
                      }
                      route = selectedRoute;
                      CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
                      PromptResult jigResult = ed.Drag(jig);
                      if (jigResult.Status != PromptStatus.OK) {
                        ed.WriteMessage("\nCommand cancelled.");
                        return;
                      }
                      firstPoint = jig.ProjectedPoint;
                      break;
                    }
                    else if (obj is Line line2) {
                      ResultBuffer xdata = line2.GetXDataForApplication(XRecordKey);
                      if (xdata != null && xdata.AsArray().Length >= 6) {
                        string dropId = xdata.AsArray()[5].Value.ToString();
                        PlumbingVerticalRoute selectedRoute = GetVerticalRoutesFromCAD().Where(r => r.Id == dropId).FirstOrDefault();
                        if (selectedRoute == null || selectedRoute.FixtureType != "Flush Valve" || selectedRoute.Type != "Cold Water") {
                          ed.WriteMessage("\nMust select a Flush Valve Route, Cold Water: ");
                          continue;
                        }
                        route = selectedRoute;
                        LineStartPointPreviewJig jig = new LineStartPointPreviewJig(line2);
                        PromptResult jigResult = ed.Drag(jig);
                        if (jigResult.Status != PromptStatus.OK) {
                          ed.WriteMessage("\nCommand cancelled.");
                          return;
                        }
                        firstPoint = jig.ProjectedPoint;
                        break;
                      }
                    }
                  }
                }

                double offsetDistance = 2.125;

                List<PlumbingHorizontalRoute> fixtureRoutes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "", "\nSelect line to route to fixture: ", "\nSelect End Point: ", route.Id);
                foreach (PlumbingHorizontalRoute r in fixtureRoutes) {
                  Point3d endPoint = r.EndPoint;
                  if (r == fixtureRoutes.Last()) {
                    Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                    Vector3d offset = direction.GetNormal() * offsetDistance;
                    endPoint -= offset;
                  }
                  SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
                }
                Vector3d direction2 = new Vector3d(fixtureRoutes.Last().StartPoint.X - fixtureRoutes.Last().EndPoint.X, fixtureRoutes.Last().StartPoint.Y - fixtureRoutes.Last().EndPoint.Y, 0);

                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                  BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                  BlockReference br = new BlockReference(fixtureRoutes.Last().EndPoint, btr.ObjectId);
                  br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI / 2;
                  modelSpace.AppendEntity(br);
                  tr.AddNewlyCreatedDBObject(br, true);
                  blockId = br.Id;
                  point = br.Position;
                  rotation = br.Rotation;
                  tr.Commit();
                }
              }
            }
            else if (blockName == "GMEP HW FIXTURE POINT") {
              PlumbingVerticalRoute route = null;
              Point3d firstPoint = Point3d.Origin;
              while (true) {
                PromptEntityOptions peo = new PromptEntityOptions("\nSelect existing drop or vertical route to attach to: ");
                peo.SetRejectMessage("\nMust select a drop or vertical route.");
                peo.AddAllowedClass(typeof(BlockReference), true);
                peo.AddAllowedClass(typeof(Line), true);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) {
                  ed.WriteMessage("\nCommand cancelled.");
                  return;
                }
                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                  if (obj == null) {
                    continue;
                  }
                  if (obj is BlockReference blockRef && FindObjectType(blockRef) == "VerticalRoute") {
                    PlumbingVerticalRoute selectedRoute = AssembleVerticalRoute(blockRef);
                    if (selectedRoute == null || selectedRoute.FixtureType != "Flush Tank" || selectedRoute.Type != "Hot Water") {
                      ed.WriteMessage("\nMust select a Hot Water route: ");
                      continue;
                    }
                    route = selectedRoute;
                    CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
                    PromptResult jigResult = ed.Drag(jig);
                    if (jigResult.Status != PromptStatus.OK) {
                      ed.WriteMessage("\nCommand cancelled.");
                      return;
                    }
                    firstPoint = jig.ProjectedPoint;
                    break;
                  }
                  else if (obj is Line line) {
                    ResultBuffer xdata = line.GetXDataForApplication(XRecordKey);
                    if (xdata != null && xdata.AsArray().Length >= 6) {
                      string dropId = xdata.AsArray()[5].Value.ToString();
                      PlumbingVerticalRoute selectedRoute = GetVerticalRoutesFromCAD().Where(r => r.Id == dropId).FirstOrDefault();
                      if (selectedRoute == null || selectedRoute.FixtureType != "Flush Tank" || selectedRoute.Type != "Hot Water") {
                        ed.WriteMessage("\nMust select a Hot Water route: ");
                        continue;
                      }
                      route = selectedRoute;
                      LineStartPointPreviewJig jig = new LineStartPointPreviewJig(line);
                      PromptResult jigResult = ed.Drag(jig);
                      if (jigResult.Status != PromptStatus.OK) {
                        ed.WriteMessage("\nCommand cancelled.");
                        return;
                      }
                      firstPoint = jig.ProjectedPoint;
                      break;
                    }
                  }
                }
              }
              double offsetDistance = 2.125;

              List<PlumbingHorizontalRoute> routes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "\nSpecify start point for route: ", "\nSelect a Line: ", "\nSelect End Point: ", route.Id);
              foreach (PlumbingHorizontalRoute r in routes) {
                Point3d endPoint = r.EndPoint;
                if (r == routes.Last()) {
                  Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                  Vector3d offset = direction.GetNormal() * offsetDistance;
                  endPoint -= offset;
                }
                SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
              }
              Vector3d direction2 = new Vector3d(routes.Last().StartPoint.X - routes.Last().EndPoint.X, routes.Last().StartPoint.Y - routes.Last().EndPoint.Y, 0);

              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(routes.Last().EndPoint, btr.ObjectId);
                br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI / 2;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }
            }
            else if (blockName == "GMEP PLUMBING GAS OUTPUT") {
              PlumbingVerticalRoute route = null;
              Point3d firstPoint = Point3d.Origin;
              while (true) {
                PromptEntityOptions peo = new PromptEntityOptions("\nSelect existing drop or vertical route to attach to: ");
                peo.SetRejectMessage("\nMust select a drop or vertical route.");
                peo.AddAllowedClass(typeof(BlockReference), true);
                peo.AddAllowedClass(typeof(Line), true);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) {
                  ed.WriteMessage("\nCommand cancelled.");
                  return;
                }
                using (Transaction tr = db.TransactionManager.StartTransaction()) {
                  DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                  if (obj == null) {
                    continue;
                  }
                  if (obj is BlockReference blockRef && FindObjectType(blockRef) == "VerticalRoute") {
                    PlumbingVerticalRoute selectedRoute = AssembleVerticalRoute(blockRef);
                    if (selectedRoute == null || selectedRoute.Type != "Gas") {
                      ed.WriteMessage("\nMust select a gas route: ");
                      continue;
                    }
                    route = selectedRoute;
                    CircleStartPointPreviewJig jig = new CircleStartPointPreviewJig(route.Position, 1.5);
                    PromptResult jigResult = ed.Drag(jig);
                    if (jigResult.Status != PromptStatus.OK) {
                      ed.WriteMessage("\nCommand cancelled.");
                      return;
                    }
                    firstPoint = jig.ProjectedPoint;
                    break;
                  }
                  else if (obj is Line line) {
                    ResultBuffer xdata = line.GetXDataForApplication(XRecordKey);
                    if (xdata != null && xdata.AsArray().Length >= 6) {
                      string dropId = xdata.AsArray()[5].Value.ToString();
                      PlumbingVerticalRoute selectedRoute = GetVerticalRoutesFromCAD().Where(r => r.Id == dropId).FirstOrDefault();
                      if (selectedRoute == null || selectedRoute.Type != "Gas") {
                        ed.WriteMessage("\nMust select a gas route: ");
                        continue;
                      }
                      route = selectedRoute;
                      LineStartPointPreviewJig jig = new LineStartPointPreviewJig(line);
                      PromptResult jigResult = ed.Drag(jig);
                      if (jigResult.Status != PromptStatus.OK) {
                        ed.WriteMessage("\nCommand cancelled.");
                        return;
                      }
                      firstPoint = jig.ProjectedPoint;
                      break;
                    }
                  }
                }
              }
              double offsetDistance = 3.5;

              List<PlumbingHorizontalRoute> routes = HorizontalRoute(routeHeight, route.Type, false, "Forward", firstPoint, true, "\nSpecify start point for route: ", "\nSelect a Line: ", "\nSelect End Point: ", route.Id);
              foreach (PlumbingHorizontalRoute r in routes) {
                Point3d endPoint = r.EndPoint;
                if (r == routes.Last()) {
                  Vector3d direction = new Vector3d(r.StartPoint.X - r.EndPoint.X, r.StartPoint.Y - r.EndPoint.Y, 0);
                  Vector3d offset = direction.GetNormal() * offsetDistance;
                  endPoint -= offset;
                }
                SpecializedHorizontalRoute(route.Type, route.PipeType, CADObjectCommands.ActiveCeilingHeight - CADObjectCommands.ActiveFloorHeight, endPoint, r.StartPoint, route.Id);
              }
              Vector3d direction2 = new Vector3d(routes.Last().StartPoint.X - routes.Last().EndPoint.X, routes.Last().StartPoint.Y - routes.Last().EndPoint.Y, 0);

              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockReference br = new BlockReference(routes.Last().EndPoint, btr.ObjectId);
                br.Rotation = Math.Atan2(direction2.Y, direction2.X) + Math.PI;
                modelSpace.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                blockId = br.Id;
                point = br.Position;
                rotation = br.Rotation;
                tr.Commit();
              }

            }
            else {
              routeHeightDisplay.Enable(routeHeight, CADObjectCommands.ActiveViewName, CADObjectCommands.ActiveFloor);
              if (blockName == "GMEP DRAIN") {
                zIndex = CADObjectCommands.ActiveFloorHeight * 12;
              }
              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr;

                BlockReference br = null;
                if (placementPoint == null) {
                  br = CADObjectCommands.CreateBlockReference(
                    tr,
                    bt,
                    blockName,
                    "Plumbing Fixture " + selectedFixtureType.Name,
                    out btr,
                    out point
                  );
                }
                else {
                  btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                  br = new BlockReference((Point3d)placementPoint, btr.ObjectId);
                  point = (Point3d)placementPoint;
                }
                if (br != null) {
                  BlockTableRecord curSpace = (BlockTableRecord)
                    tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                  if (blockRotation == null) {
                    RotateJig rotateJig = new RotateJig(br);
                    PromptResult rotatePromptResult = ed.Drag(rotateJig);
                    if (rotatePromptResult.Status != PromptStatus.OK) {
                      ed.WriteMessage("\nRotation cancelled.");
                      routeHeightDisplay.Disable();
                      return;
                    }
                  }
                  else {
                    br.Rotation = blockRotation.Value;
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
                routeHeightDisplay.Disable();
                blockId = br.Id;
                tr.Commit();
              }
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
                if (prop.PropertyName == "flow_type_id") {
                  prop.Value = flowTypeId;
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
                blockName,
                flowTypeId
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
              blockName,
              flowTypeId
            );

            if (blockName == "GMEP DRAIN") {
              PromptKeywordOptions pko2 = new PromptKeywordOptions("Select Vent Option: ");
              pko2.Keywords.Add("Add", "AddVent", "Add Vent");
              pko2.Keywords.Add("Connect", "ConnectToExistingVent", "Connect To Existing Vent");
              pko2.Keywords.Default = "Add";
              pko2.AllowNone = false;
              PromptResult res2 = ed.GetKeywords(pko2);
              ed.WriteMessage("\nYou selected: " + res2.StringResult);
              if (res2.Status != PromptStatus.OK) {
                ed.WriteMessage("\nCommand cancelled.");
                return;
              }
              Point3d newEndPoint = Point3d.Origin;
              Point3d newStartPoint = Point3d.Origin;
              if (res2.StringResult == "Add") {
                PromptKeywordOptions pko = new PromptKeywordOptions("How far up?");
                pko.Keywords.Add("Ceiling");
                pko.Keywords.Add("Roof");
                pko.AllowNone = false;
                PromptResult res = ed.GetKeywords(pko);
                ed.WriteMessage("\nYou selected: " + res.StringResult);
                if (res.Status != PromptStatus.OK) {
                  ed.WriteMessage("\nCommand cancelled.");
                  return;
                }
                Dictionary<string, PlumbingVerticalRoute> ventRoutes = null;
                if (res.StringResult == "Ceiling") {
                  ventRoutes = VerticalRoute("Vent", 0, CADObjectCommands.ActiveFloor, "UpToCeiling");
                }
                else if (res.StringResult == "Roof") {
                  List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD(ProjectId);
                  PlumbingPlanBasePoint activeBasePoint = basePoints.Where(bp => bp.Id == CADObjectCommands.ActiveBasePointId).First();
                  List<PlumbingPlanBasePoint> aboveBasePoints = basePoints.Where(bp => bp.Floor >= activeBasePoint.Floor && bp.ViewportId == activeBasePoint.ViewportId).ToList();
                  PlumbingPlanBasePoint highestFloorBasePoint = aboveBasePoints
                  .OrderByDescending(bp => bp.Floor)
                  .FirstOrDefault();

                  ventRoutes = VerticalRoute("Vent", 0, highestFloorBasePoint.Floor, "UpToCeiling", null, highestFloorBasePoint.CeilingHeight - highestFloorBasePoint.FloorHeight);
                  if (highestFloorBasePoint.Floor > CADObjectCommands.ActiveFloor) {
                    PlumbingVerticalRoute startRoute = ventRoutes.First().Value;
                    ZoomToPoint(ed, startRoute.Position);
                  }
                }
                if (ventRoutes == null || !ventRoutes.ContainsKey(CADObjectCommands.ActiveBasePointId)) {
                  ed.WriteMessage("\nError: Could not find vent route for base point.");
                  return;
                }
                Point3d ventPoint = ventRoutes[CADObjectCommands.ActiveBasePointId].Position;
                ventPoint = new Point3d(ventPoint.X, ventPoint.Y, point.Z);
                double shortenBy = 1.5;
                Vector3d direction = point - ventPoint;
                double length = direction.Length;

                newEndPoint = ventPoint;
                newStartPoint = point;
                if (length > 0) {
                  Vector3d offset = direction.GetNormal() * shortenBy;
                  newEndPoint = ventPoint + offset;
                  newStartPoint = point - offset;
                }
              }
              if (res2.StringResult == "Connect") {
                while (true) {
                  PromptEntityOptions peo = new PromptEntityOptions("\nSelect existing vent drop or vertical route to attach to: ");
                  peo.SetRejectMessage("\nMust select a vent drop or vertical route.");
                  peo.AddAllowedClass(typeof(BlockReference), true);
                  PromptEntityResult per = ed.GetEntity(peo);
                  if (per.Status != PromptStatus.OK) {
                    ed.WriteMessage("\nCommand cancelled.");
                    return;
                  }
                  using (Transaction tr = db.TransactionManager.StartTransaction()) {
                    DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (obj == null) {
                      continue;
                    }
                    if (obj is BlockReference blockRef && FindObjectType(blockRef) == "VerticalRoute") {
                      PlumbingVerticalRoute selectedRoute = AssembleVerticalRoute(blockRef);
                      if (selectedRoute == null || selectedRoute.Type != "Vent") {
                        ed.WriteMessage("\nMust select a Vent Route: ");
                        continue;
                      }
                      Point3d ventPoint = selectedRoute.Position;
                      ventPoint = new Point3d(ventPoint.X, ventPoint.Y, point.Z);
                      double shortenBy = 1.5;
                      Vector3d direction = point - ventPoint;
                      double length = direction.Length;

                      newEndPoint = ventPoint;
                      newStartPoint = point;
                      if (length > 0) {
                        Vector3d offset = direction.GetNormal() * shortenBy;
                        newEndPoint = ventPoint + offset;
                        newStartPoint = point - offset;
                      }
                      break;
                    }
                  }
                }
              }

              SpecializedHorizontalRoute("Waste", "", 0, newStartPoint, newEndPoint);
            }
          }
          catch (System.Exception ex) {
            ed.WriteMessage("FIxture Error - " + ex.ToString());
            routeHeightDisplay.Disable();
            Console.WriteLine(ex.ToString());
          }
        }
        MakePlumbingFixtureLabel(plumbingFixture, selectedFixtureType);
      }
      routeHeightDisplay.Disable();
    }
    public void CreateCenterLineRoute(Point3d addPoint, double rotation, string type, string pipeType, double height) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      PromptKeywordOptions pOptions = new PromptKeywordOptions("\nForward or Backward?");
      pOptions.Keywords.Add("Forward");
      pOptions.Keywords.Add("Backward");
      pOptions.Keywords.Default = "Forward";
      PromptResult pResult = ed.GetKeywords(pOptions);

      double distance = 4.0 / 12.0;

      // Calculate the end point using rotation (angle in radians)
      double endX = addPoint.X + distance * Math.Cos(rotation);
      double endY = addPoint.Y + distance * Math.Sin(rotation);
      double endZ = addPoint.Z; // Keep Z the same
      Point3d endPoint = new Point3d(endX, endY, endZ);
      if (pResult.StringResult == "Forward") {
        SpecializedHorizontalRoute(type, pipeType, height, addPoint, endPoint);
      }
      else if (pResult.StringResult == "Backward") {
        SpecializedHorizontalRoute(type, pipeType, height, endPoint, addPoint);
      }
    }
    public PlumbingVerticalRoute AssembleVerticalRoute(BlockReference blockRef) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return null;
      var db = doc.Database;
      var ed = doc.Editor;
      PlumbingVerticalRoute route = null;
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockReference br = (BlockReference)tr.GetObject(blockRef.ObjectId, OpenMode.ForRead);
        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead);
        string name = btr.Name;
        DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
        string Id = string.Empty;
        string VerticalRouteId = string.Empty;
        string BasePointId = string.Empty;
        double routeStartHeight = 0;
        double length = 0;
        double width = 0;
        string pipeType = string.Empty;
        bool isUp = false;
        string fixtureType = string.Empty;

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
          if (prop.PropertyName == "start_height") {
            routeStartHeight = Convert.ToDouble(prop.Value);
          }
          if (prop.PropertyName == "length") {
            length = Convert.ToDouble(prop.Value);
          }
          if (prop.PropertyName == "pipe_type") {
            pipeType = prop.Value?.ToString();
          }
          if (prop.PropertyName == "is_up") {
            isUp = Convert.ToDouble(prop.Value) == 1.0;
          }
          if (prop.PropertyName == "fixture_type") {
            fixtureType = prop.Value?.ToString();
          }
        }
        if (Id != "0") {
          double rotation = br.Rotation;
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
          switch (br.Layer) {
            case "P-DOMW-CWTR":
              type = "Cold Water";
              break;
            case "P-DOMW-HOTW":
              type = "Hot Water";
              break;
            case "P-GREASE-WASTE":
              type = "Grease Waste";
              break;
            case "P-WV-W-BELOW":
              type = "Waste";
              break;
            case "P-WV-VENT":
              type = "Vent";
              break;
            case "P-GAS":
              type = "Gas";
              break;
          }
          route = new PlumbingVerticalRoute(
            Id,
            ProjectId,
            type,
            br.Position,
            VerticalRouteId,
            BasePointId,
            routeStartHeight,
            length,
            nodeTypeId,
            pipeType,
            isUp
          );
          route.Rotation = rotation;
          route.FixtureType = fixtureType;
        }
        tr.Commit();
      }
      return route;
    }
    [CommandMethod("PlumbingSource")]
    public void CreatePlumbingSource() {
      string projectNo = CADObjectCommands.GetProjectNoFromFileName();
      string projectId = MariaDBService.GetProjectIdSync(projectNo);

      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      string basePointGUID = CADObjectCommands.GetActiveView();

      List<PlumbingSourceType> plumbingSourceTypes = MariaDBService.GetPlumbingSourceTypes();
      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");

      keywordOptions.Message = "\nSelect source type:";

      plumbingSourceTypes.ForEach(t => {
        if ((CADObjectCommands.ActiveViewTypes.Contains("Water") && t.Type == "Water Meter") || (CADObjectCommands.ActiveViewTypes.Contains("Water") && t.Type == "Water Heater") || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && t.Type == "Water Heater") || (CADObjectCommands.ActiveViewTypes.Contains("Gas") && t.Type == "Gas Meter") || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && t.Type == "Waste Source") || (CADObjectCommands.ActiveViewTypes.Contains("Sewer-Vent") && t.Type == "Vent Exit")) {
          keywordOptions.Keywords.Add(t.Id.ToString() + " " + t.Type);
        }
      });
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
      if (selectedSourceType.Type == "Vent Exit") {
        ed.Command("PlumbingFixture", "VE");
        return;
      }

      double pressure = 0;
      if (selectedSourceType.Type == "Water Meter" || selectedSourceType.Type == "Water Meter") {
        PromptDoubleOptions pdo2 = new PromptDoubleOptions("\nEnter the PSI of the source");
        pdo2.DefaultValue = 60;
        pdo2.AllowNone = false;
        pdo2.AllowNegative = false;
        pdo2.AllowZero = false;
        while (true) {
          try {
            PromptDoubleResult pdr2 = ed.GetDouble(pdo2);
            if (pdr2.Status == PromptStatus.Cancel) {
              ed.WriteMessage("\nCommand cancelled.");
              return;
            }
            if (pdr2.Status != PromptStatus.OK) {
              ed.WriteMessage("\nInvalid input. Please enter a valid number.");
              continue;
            }
            pressure = pdr2.Value;
            break;
          }
          catch (System.Exception ex) {
            ed.WriteMessage($"\nError: {ex.Message}");
            continue;
          }
        }
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
            if (prop.PropertyName == "pressure") {
              prop.Value = pressure;
            }
          }
          tr.Commit();
        }
        PlumbingSource plumbingSource = new PlumbingSource(
          sourceId,
          projectId,
          point,
          selectedSourceType.Id,
          basePointGUID,
          pressure
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
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      try {
        if (
          e.Erased
          && !SettingObjects
          && !IsSaving
          && e.DBObject is BlockReference blockRef
        ) {
          if (blockRef == null || blockRef.IsDisposed)
            return;
          if (!IsVerticalRouteBlock(blockRef)) {
            return;
          }

          string cmdName = Application.GetSystemVariable("CMDNAMES") as string;
          if (!string.IsNullOrEmpty(cmdName) && cmdName.Contains("PASTECLIP")) {
            return;
          }

          ed.WriteMessage($"\nObject {e.DBObject.ObjectId} was erased.");

          string VerticalRouteId = string.Empty;
          using (Transaction tr = db.TransactionManager.StartTransaction()) {
            BlockReference br = (BlockReference)tr.GetObject(blockRef.ObjectId, OpenMode.ForRead);
            var properties = br.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty prop in properties) {
              if (prop.PropertyName == "vertical_route_id") {
                VerticalRouteId = prop.Value?.ToString();
              }
            }
            tr.Commit();
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
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      string cmdName = Application.GetSystemVariable("CMDNAMES") as string;
      if (SettingObjects || IsSaving || string.IsNullOrEmpty(cmdName)) return;

      // Only allow for MOVE, STRETCH, or other user commands
      if (!(cmdName.Contains("MOVE") || cmdName.Contains("STRETCH"))) return;

      Dictionary<string, ObjectId> basePoints = new Dictionary<string, ObjectId>();
      if (
        !SettingObjects
        && !IsSaving
        && e.DBObject is BlockReference blockRef
      ) {
        if (blockRef == null || blockRef.IsErased || blockRef.IsDisposed)
          return;
        if (!IsVerticalRouteBlock(blockRef)) {
          return;
        }
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
        using (Transaction tr = db.TransactionManager.StartTransaction()) {
          BlockReference br = (BlockReference)tr.GetObject(blockRef.ObjectId, OpenMode.ForRead);
          var properties = br.DynamicBlockReferencePropertyCollection;
          foreach (DynamicBlockReferenceProperty prop in properties) {
            if (prop.PropertyName == "vertical_route_id") {
              VerticalRouteId = prop.Value?.ToString();
            }
            if (prop.PropertyName == "base_point_id") {
              BasePointId = prop.Value?.ToString();
            }
          }
          tr.Commit();
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
      if (doc == null) return;
      var db = doc.Database;
      var ed = doc.Editor;

      if (
        !SettingObjects
        && !IsSaving
        && e.DBObject is BlockReference blockRef
      ) {
        if (blockRef == null || blockRef.IsErased || blockRef.IsDisposed)
          return;
        if (!IsPlumbingBasePointBlock(blockRef)) {
          return;
        }
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
      if (doc == null) return;

      var db = doc.Database;
      var ed = doc.Editor;

      MariaDBService mariaDBService = new MariaDBService();
      ed.WriteMessage("\nDocument saved, updating plumbing data...");

      // Always lock the document for any DB access
      using (var docLock = doc.LockDocument()) {
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

    }

    public static List<PlumbingHorizontalRoute> GetHorizontalRoutesFromCAD(string ProjectId) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return new List<PlumbingHorizontalRoute>();

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
                case "P-WV-W-BELOW":
                  type = "Waste";
                  break;
                case "P-GREASE-WASTE":
                  type = "Grease Waste";
                  break;
              }
              ResultBuffer xdata = line.GetXDataForApplication(XRecordKey);
              if (xdata != null && xdata.AsArray().Length >= 5) {
                TypedValue[] values = xdata.AsArray();

                PlumbingHorizontalRoute route = new PlumbingHorizontalRoute(values[1].Value.ToString(), ProjectId, type, line.StartPoint, line.EndPoint, values[2].Value.ToString(), values[3].Value.ToString(), (double)values[4].Value);
                if (route.Type == "Waste" || route.Type == "Vent" || route.Type == "Grease Waste") {
                  route = new PlumbingHorizontalRoute(values[1].Value.ToString(), ProjectId, type, line.EndPoint, line.StartPoint, values[2].Value.ToString(), values[3].Value.ToString(), (double)values[4].Value);
                }
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


    public static List<PlumbingVerticalRoute> GetVerticalRoutesFromCAD(string ProjectId = "") {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return new List<PlumbingVerticalRoute>();

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
                      double startHeight = 0;
                      double length = 0;
                      double width = 0;
                      string pipeType = string.Empty;
                      bool isUp = false;
                      string fixtureType = string.Empty;

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
                        if (prop.PropertyName == "start_height") {
                          startHeight = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "length") {
                          length = Convert.ToDouble(prop.Value);
                        }
                        if (prop.PropertyName == "pipe_type") {
                          pipeType = prop.Value?.ToString();
                        }
                        if (prop.PropertyName == "is_up") {
                          isUp = Convert.ToDouble(prop.Value) == 1.0;
                        }
                        if (prop.PropertyName == "fixture_type") {
                          fixtureType = prop.Value?.ToString();
                        }
                      }
                      if (Id != "0") {
                        double rotation = entity.Rotation;

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
                            type = "Grease Waste";
                            break;
                          case "P-WV-W-BELOW":
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
                          VerticalRouteId,
                          BasePointId,
                          startHeight,
                          length,
                          nodeTypeId,
                          pipeType,
                          isUp
                        );
                        route.FixtureType = fixtureType;
                        route.Rotation = rotation;
                        if (route.Type == "Waste" || route.Type == "Vent" || route.Type == "Grease Waste") {
                          route.IsUp = !route.IsUp; 
                        }
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
      ed.WriteMessage(ProjectId + " - Found " + routes.Count + " plumbing vertical routes in the drawing.");
      return routes;
    }
    [CommandMethod("ToggleFixtureVisibility")]
    public static void ToggleFixtureVisibility() {
      PromptSelectionOptions options = new PromptSelectionOptions();
      PromptSelectionResult result = Application.DocumentManager.MdiActiveDocument.Editor.GetSelection(options);

      PromptKeywordOptions keywordOptions = new PromptKeywordOptions("\nSelect visibility option: ");
      keywordOptions.Keywords.Add("Visible");
      keywordOptions.Keywords.Add("Hidden");
      PromptResult keywordResult = Application.DocumentManager.MdiActiveDocument.Editor.GetKeywords(keywordOptions);
      if (keywordResult.Status != PromptStatus.OK) {
        return;
      }
      string visibilityOption = keywordResult.StringResult;
      foreach (SelectedObject selectedObject in result.Value) {
        using (Transaction tr = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction()) {
          BlockReference blockRef = tr.GetObject(selectedObject.ObjectId, OpenMode.ForWrite) as BlockReference;
          if (blockRef.DynamicBlockReferencePropertyCollection != null) {
            foreach (DynamicBlockReferenceProperty prop in blockRef.DynamicBlockReferencePropertyCollection) {
              if (prop.PropertyName == "Visibility") {
                prop.Value = visibilityOption;
              }
            }
          }
          tr.Commit();
        }
      }
    }
  
    public static List<PlumbingPlanBasePoint> GetPlumbingBasePointsFromCAD(string ProjectId = "") {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return new List<PlumbingPlanBasePoint>();

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
                    double RouteHeight = 0;
                    bool isSite = false;
                    bool isSiteRef = false;

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
                      if (prop.PropertyName == "route_height") {
                        RouteHeight = Convert.ToDouble(prop.Value);
                      }
                      if (prop.PropertyName == "is_site") {
                        isSite = Convert.ToDouble(prop.Value) == 1.0;
                      }
                      if (prop.PropertyName == "is_site_ref") {
                        isSiteRef = Convert.ToDouble(prop.Value) == 1.0;
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
                        CeilingHeight,
                        isSite,
                        isSiteRef
                      );
                      BasePoint.RouteHeight = RouteHeight;
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

    public static List<PlumbingSource> GetPlumbingSourcesFromCAD(string ProjectId = "") {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return new List<PlumbingSource>();

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
          "GMEP PLUMBING VENT EXIT"
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
                      double pressure = 0;

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
                        if (prop.PropertyName == "pressure") {
                          pressure = Convert.ToDouble(prop.Value);
                        }
                      }
                      if (name == "GMEP WH 50" || name == "GMEP WH 80" || name == "GMEP IWH") {
                        typeId = 2;
                      }
                      if (name == "GMEP PLUMBING VENT EXIT") {
                        typeId = 5;
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
                          basePointId,
                          pressure
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

  
  public static List<PlumbingFixture> GetPlumbingFixturesFromCAD(string ProjectId = "") {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return new List<PlumbingFixture>();
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
          "GMEP PLUMBING GAS OUTPUT",
          "GMEP PLUMBING VENT START",
          "GMEP CW FIXTURE POINT",
          "GMEP HW FIXTURE POINT"
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
                      int flowTypeId = 0;

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
                        if (prop.PropertyName == "flow_type_id") {
                          flowTypeId = Convert.ToInt32(prop.Value);
                        }

                      }
                 
                      if (!string.IsNullOrEmpty(GUID) && GUID != "0") {
                        Point3d position = entity.Position;
                        if (coldWaterX != 0 && coldWaterY != 0) {
                          double rotation = entity.Rotation;
                          double rotatedX = coldWaterX * Math.Cos(rotation) - coldWaterY * Math.Sin(rotation);
                          double rotatedY = coldWaterX * Math.Sin(rotation) + coldWaterY * Math.Cos(rotation);
                          position = new Point3d(entity.Position.X + rotatedX, entity.Position.Y + rotatedY, entity.Position.Z);
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
                          name,
                          flowTypeId
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
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return 0;

      var db = doc.Database;
      var ed = doc.Editor;

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

    [CommandMethod("UP")]
    public async void Up() {
      CADObjectCommands.GetActiveView();
      VerticalRoute(null, null, CADObjectCommands.ActiveFloor, "Up", null);
    }

    [CommandMethod("DOWN")]
    public async void Down() {
      CADObjectCommands.GetActiveView();
      VerticalRoute(null, null, CADObjectCommands.ActiveFloor, "Down", null);
    }

    [CommandMethod("UPTOCEILING")]
    public async void UpToCeiling() {
      CADObjectCommands.GetActiveView();
      VerticalRoute(null, null, CADObjectCommands.ActiveFloor, "UpToCeiling");
    }

    [CommandMethod("DOWNTOFLOOR")]
    public async void DownToFloor() {
      CADObjectCommands.GetActiveView();
      VerticalRoute(null, null, CADObjectCommands.ActiveFloor, "DownToFloor");
    }
    public static void Db_ObjectAppended(object sender, ObjectEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var db = doc.Database;
      var ed = doc.Editor;

      string cmdName = Application.GetSystemVariable("CMDNAMES") as string;
      if (!cmdName.Contains("PASTECLIP")) {
        return;
      }

      try {
        if (
          e.DBObject is BlockReference blockRef
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
          if (string.IsNullOrEmpty(Id)) {
            SettingObjects = false;
            return;
          }
          
          ed.WriteMessage("\nObject appended event triggered.\n");
          ed.WriteMessage($"\nLooking for object with ID: {Id}");
          
          string type = FindObjectType(blockRef);
          if (string.IsNullOrEmpty(type)) {
            SettingObjects = false;
            return;
          }
          
          if (type != "") {
            if (type == "Fixture" || type == "Source") {
              using (Transaction tr = db.TransactionManager.StartTransaction()) {
                BlockReference blockRef2 = (BlockReference)tr.GetObject(blockRef.ObjectId, OpenMode.ForWrite);
                foreach (DynamicBlockReferenceProperty prop in blockRef2.DynamicBlockReferencePropertyCollection) {
                  if (prop.PropertyName == "id") {
                    prop.Value = Guid.NewGuid().ToString();
                  }
                }
                tr.Commit();
              }
            }
            if (type == "VerticalRoute") {
              if (!activePlacingDuplicationRoutes.Contains(Id)) {
                activePlacingDuplicationRoutes.Add(Id);
                SettingObjects = false;
                return;
              }
              StageDuplicateFullVerticalRoute(blockRef.ObjectId);
            }
          }
          else {
            ed.WriteMessage($"\nNo matching object found for ID: {Id}");
          }
          SettingObjects = false;
        }
        else if (e.DBObject is Line lineBase
          && !SettingObjects
          && !IsSaving) {
          ed.WriteMessage("\nLine object appended event triggered.\n");
          SettingObjects = true;
          using (Transaction tr = db.TransactionManager.StartTransaction()) {
            Line line = (Line)tr.GetObject(lineBase.ObjectId, OpenMode.ForWrite);
            ResultBuffer xdata = line.GetXDataForApplication(XRecordKey);
            if (xdata == null || xdata.AsArray().Length < 5) {
              SettingObjects = false;
              return;
            }
            TypedValue[] values = xdata.AsArray();
            values[1] = new TypedValue(1000, Guid.NewGuid().ToString()); 

            ResultBuffer newXdata = new ResultBuffer(values);
            line.XData = newXdata;

            tr.Commit();
          }
          SettingObjects = false;
        }

      }
      catch (System.Exception ex) {
        ed.WriteMessage($"Exception: {ex.StackTrace}\n");
        SettingObjects = false;
      }
    }
    public static string FindObjectType(BlockReference blockRef) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return null;
      var db = doc.Database;
      var ed = doc.Editor;

      List<string> verticalRouteBlockNames = new List<string>
      {
          "GMEP_PLUMBING_LINE_UP",
          "GMEP_PLUMBING_LINE_DOWN",
          "GMEP_PLUMBING_LINE_VERTICAL"
      };
      List<string> fixtureNames = new List<string> {
          "GMEP WH 80",
          "GMEP WH 50",
          "GMEP DRAIN",
          "GMEP CP",
          "GMEP FS 12",
          "GMEP FS 6",
          "GMEP FD",
          "GMEP RPBFP",
          "GMEP IWH",
          "GMEP PLUMBING GAS OUTPUT",
          "GMEP PLUMBING VENT START",
          "GMEP CW FIXTURE POINT",
          "GMEP HW FIXTURE POINT"
      };
      List<string> sourceNames = new List<string> {
          "GMEP SOURCE",
          "GMEP WH 80",
          "GMEP WH 50",
          "GMEP IWH",
          "GMEP PLUMBING VENT EXIT"
      };

      string type = "";
      string blockName = "";
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockRef.DynamicBlockTableRecord, OpenMode.ForRead);
        blockName = btr.Name;
        tr.Commit();
      }
      if (verticalRouteBlockNames.Contains(blockName)) {
        type = "VerticalRoute";
      }
      else if (fixtureNames.Contains(blockName)) {
        type = "Fixture";
      }
      else if (sourceNames.Contains(blockName)) {
        type = "Source";
      }
      return type;
    }

    public static void StageDuplicateFullVerticalRoute(ObjectId objid) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var db = doc.Database;
      var ed = doc.Editor;
      ed.WriteMessage("\nStaging full vertical route duplication...");

      List<PlumbingVerticalRoute> verticalRoutes = GetVerticalRoutesFromCAD();
      string newVerticalRouteId = Guid.NewGuid().ToString();
      string newId = Guid.NewGuid().ToString();

      string Id = "";
      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockReference blockRef2 = (BlockReference)tr.GetObject(objid, OpenMode.ForRead);
        foreach (DynamicBlockReferenceProperty prop in blockRef2.DynamicBlockReferencePropertyCollection) {
          if (prop.PropertyName == "id") {
            Id = prop.Value.ToString();
          }
        }
        tr.Commit();
      }
      PlumbingVerticalRoute route = verticalRoutes.FirstOrDefault(r => r.Id == Id);

      using (Transaction tr = db.TransactionManager.StartTransaction()) {
        BlockReference blockRef2 = (BlockReference)tr.GetObject(objid, OpenMode.ForWrite);
        foreach (DynamicBlockReferenceProperty prop in blockRef2.DynamicBlockReferencePropertyCollection) {
          if (prop.PropertyName == "id") {
            prop.Value = newId;
          }
          if (prop.PropertyName == "vertical_route_id") {
            prop.Value = newVerticalRouteId;
          }
        }
        tr.Commit();
      }

      List<PlumbingVerticalRoute> relatedRoutes = verticalRoutes
        .Where(vr => vr.VerticalRouteId == route.VerticalRouteId && vr.Id != route.Id)
        .ToList();
      if (!pendingDuplicationRoutes.ContainsKey(newId)) {
        pendingDuplicationRoutes.Add(newId, new List<string>());
      }
      foreach (var r in relatedRoutes) {
        pendingDuplicationRoutes[newId].Add(r.Id);
      }
    }

    public static void DuplicateFullVerticalRoutes() {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var db = doc.Database;
      var ed = doc.Editor;

      SettingObjects = true;

      ed.WriteMessage($"\nChecking for pending vertical route duplications... Amount:{pendingDuplicationRoutes.Count}");
      List<PlumbingVerticalRoute> routes = GetVerticalRoutesFromCAD();
      List<PlumbingPlanBasePoint> basePoints = GetPlumbingBasePointsFromCAD();

      Dictionary<string, List<string>> routeKeys = new Dictionary<string, List<string>>(pendingDuplicationRoutes);
      foreach (var entry in routeKeys) {
        PlumbingVerticalRoute baseRoute = routes.FirstOrDefault(r => r.Id == entry.Key);
        if (baseRoute == null) continue;
        PlumbingPlanBasePoint basePoint = basePoints.FirstOrDefault(bp => bp.Id == baseRoute.BasePointId);
        if (basePoint == null) continue;
        Vector3d vectorChange = baseRoute.Position - basePoint.Point;

        List<PlumbingVerticalRoute> routesToProcess = routes.Where(r => entry.Value.Contains(r.Id)).ToList();

        foreach (PlumbingVerticalRoute subRoute in routesToProcess) {
          PlumbingPlanBasePoint subBasePoint = basePoints.FirstOrDefault(bp => bp.Id == subRoute.BasePointId);
          ed.WriteMessage($"\nProcessing sub-route at position {subRoute.Position} with node type {subRoute.NodeTypeId}");
          //start adding subroutes depending on nodes.
          string blockName = "";
          switch (subRoute.NodeTypeId) {
            case 1:
              blockName = "GMEP_PLUMBING_LINE_UP";
              break;
            case 2:
              blockName = "GMEP_PLUMBING_LINE_VERTICAL";
              break;
            case 3:
              blockName = "GMEP_PLUMBING_LINE_DOWN";
              break;
          }
          string layer = "";
          switch (subRoute.Type) {
            case "Hot Water":
              layer = "P-DOMW-HOTW";
              break;
            case "Cold Water":
              layer = "P-DOMW-CWTR";
              break;
            case "Gas":
              layer = "P-GAS";
              break;
            case "Waste":
              layer = "P-WV-W-BELOW";
              break;
            case "Grease Waste":
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

          if (string.IsNullOrEmpty(blockName)) continue;
          ed.WriteMessage("Appending vertical route");
          using (Transaction tr = db.TransactionManager.StartTransaction()) {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
            if (blockDef == null) continue;
            Point3d newPosition = subBasePoint.Point + vectorChange;
            BlockReference newBlockRef = new BlockReference(newPosition, blockDef.ObjectId);
            newBlockRef.Layer = layer;
            newBlockRef.Rotation = subRoute.Rotation;
            modelSpace.AppendEntity(newBlockRef);
            tr.AddNewlyCreatedDBObject(newBlockRef, true);
            var pc = newBlockRef.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty prop in pc) {
              if (prop.PropertyName == "id") {
                prop.Value = Guid.NewGuid().ToString();
              }
              if (prop.PropertyName == "vertical_route_id") {
                prop.Value = baseRoute.VerticalRouteId;
              }
              if (prop.PropertyName == "base_point_id") {
                prop.Value = subRoute.BasePointId;
              }
              if (prop.PropertyName == "start_height") {
                prop.Value = subRoute.StartHeight;
              }
              if (prop.PropertyName == "length") {
                prop.Value = subRoute.Length;
              }
              if (prop.PropertyName == "pipe_type") {
                prop.Value = subRoute.PipeType;
              }
              if (prop.PropertyName == "is_up") {
                prop.Value = subRoute.IsUp ? 1.0 : 0.0;
              }
            }
              
            tr.Commit();
          }
        }
      }
      pendingDuplicationRoutes.Clear();
      activePlacingDuplicationRoutes.Clear();
      SettingObjects = false;
    }
    public static void Doc_CommandEnded(object sender, CommandEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var db = doc.Database;
      var ed = doc.Editor;
      ed.WriteMessage("\nCommand ended: " + e.GlobalCommandName);
      if (e.GlobalCommandName == "PASTECLIP") {
        DuplicateFullVerticalRoutes();
      }
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
      // Detach from document events
      Application.DocumentManager.DocumentCreated -= DocumentManager_DocumentCreated;
      Application.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;

      // Detach handlers from all open documents
      foreach (Document doc in Application.DocumentManager) {
        AutoCADIntegration.DetachHandlers(doc);
      }
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
      _ed = ed ?? throw new ArgumentNullException(nameof(ed));
    }

    public void Enable(double routeHeight, string viewName, int floor) {
      if (_enabled || _ed == null)
        return;

      if (string.IsNullOrWhiteSpace(viewName))
        viewName = "N/A";

      _routeHeight = routeHeight;
      _viewName = viewName;
      _floor = floor;
      _ed.PointMonitor += Ed_PointMonitor;
      _enabled = true;
    }

    public void Disable() {
      if (!_enabled) return;
      if (_ed != null)
        _ed.PointMonitor -= Ed_PointMonitor;
      _enabled = false;
      try {
        TransientManager.CurrentTransientManager.EraseTransients(TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
      }
      catch {
        Console.WriteLine("Error erasing transients");
      }
    }

    public void Update(double routeHeight, string viewName, int floor) {
      if (string.IsNullOrWhiteSpace(viewName))
        viewName = "N/A";
      _routeHeight = routeHeight;
      _viewName = viewName;
      _floor = floor;
    }

    private void Ed_PointMonitor(object sender, PointMonitorEventArgs e) {
      try {
        if (_ed == null || _ed.Document == null)
          return;

        var view = _ed.GetCurrentView();
        var db = _ed.Document.Database;

        if (view == null || db == null)
          return;

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
      catch (System.Exception ex) {
        _ed.WriteMessage($"\nError in PointMonitor: {ex.Message}");
      }
    }
  }
}
