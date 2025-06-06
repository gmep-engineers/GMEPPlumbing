﻿using Autodesk.AutoCAD.ApplicationServices;
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
using System.Windows.Documents;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;

using System.ComponentModel;
using Autodesk.AutoCAD.MacroRecorder;
using MongoDB.Driver.Core.Connections;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using Autodesk.Windows;
using System.Windows.Markup;
using System.Windows.Shapes;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using System.Xml.Linq;
using SharpCompress.Common;
using Google.Protobuf;
using Org.BouncyCastle.Ocsp;
using static System.Windows.Forms.LinkLabel;

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
    public bool SettingObjects { get; set; }
    public MariaDBService MariaDBService { get; set; } = new MariaDBService();

    public Document doc { get; private set; }
    public Database db { get; private set; }
    public Editor ed { get; private set; }
    public string ProjectId { get; private set; } = string.Empty;



    public AutoCADIntegration()
    {
        doc = Application.DocumentManager.MdiActiveDocument;
        db = doc.Database;
        ed = doc.Editor;
        SettingObjects = false;

        //db.ObjectErased += DB_LineErased; 
        db.ObjectErased += Db_VerticalRouteErased;

        // Initialize the MongoDB service
    }


    [CommandMethod("PlumbingHorizontalRoute")]
    public async void PlumbingHorizontalRoute()
    {
        while (true)
        {
            //Select a starting point/object
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect a route or source to start from ");
            peo.SetRejectMessage("\nSelect a route or source to start from ");
            peo.AddAllowedClass(typeof(BlockReference), true);
            peo.AddAllowedClass(typeof(Line), true);
            PromptEntityResult per = ed.GetEntity(peo);
            
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nCommand cancelled.");
                return;
            }
            ObjectId basePointId = per.ObjectId;
            int pointX = 0;
            int pointY = 0;
        
            Point3d startPointLocation = Point3d.Origin;
            ObjectId addedLineId = ObjectId.Null;
            
            string FedFromId = "";
            string layer = "";
            string LineGUID = Guid.NewGuid().ToString();

            // Check if the selected object is a BlockReference or Line
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity basePoint = (Entity)tr.GetObject(basePointId, OpenMode.ForRead);

                if (basePoint.Layer == "Defpoints")
                {
                    ed.WriteMessage("\nMust be connected to a source.");
                    return;
                }

                //get blockreference choice
                if (basePoint is BlockReference basePointRef)
                {
                    if (basePointRef != null)
                    {
                        DynamicBlockReferencePropertyCollection properties = basePointRef.DynamicBlockReferencePropertyCollection;
                        foreach (DynamicBlockReferenceProperty prop in properties)
                        {
                            if (prop.PropertyName == "Connection X")
                            {
                                pointX = Convert.ToInt32(prop.Value);
                            }
                            if (prop.PropertyName == "Connection Y")
                            {
                                pointY = Convert.ToInt32(prop.Value);
                            }
                            if (prop.PropertyName == "id")
                            {
                                FedFromId = prop.Value.ToString();
                            }
                        }
                        layer = basePointRef.Layer;
                    }
                    if (pointX != 0 || pointY != 0)
                    {
                        double rotation = basePointRef.Rotation;
                        double rotatedX = pointX * Math.Cos(rotation) - pointY * Math.Sin(rotation);
                        double rotatedY = pointX * Math.Sin(rotation) + pointY * Math.Cos(rotation);
                        startPointLocation = new Point3d(basePointRef.Position.X + rotatedX, basePointRef.Position.Y + rotatedY, 0);
                    }
                }

                //get line choice
                if (basePoint is Line basePointLine)
                {
                    //retrieving the lines xdata
                    ResultBuffer xData = basePointLine.GetXDataForApplication(XRecordKey);
                    if (xData == null || xData.AsArray().Length < 3)
                    {
                        ed.WriteMessage("\nSelected line does not have the required XData.");
                        return;
                    }
                    TypedValue[] values = xData.AsArray();
                    FedFromId = values[1].Value as string;

                    //Placing Line
                    LineStartPointPreviewJig jig = new LineStartPointPreviewJig(basePointLine);
                    PromptResult jigResult = ed.Drag(jig);
                    startPointLocation = jig.ProjectedPoint;
                    layer = basePointLine.Layer;
                }


                //Choosing end object or point
                PromptEntityOptions peo1 = new PromptEntityOptions("\nSelect Source, Vertical Route, Fixture or [Point]: ");
                peo1.Keywords.Add("Point"); 
                peo1.AllowNone = false; // Allow clicking in empty space
                peo1.SetRejectMessage("\nSelect a valid object or pick a point.");
                peo1.AddAllowedClass(typeof(BlockReference), true);
                PromptEntityResult per1 = ed.GetEntity(peo1);

                
                if (per1.Status == PromptStatus.Keyword && per1.StringResult == "Point")
                {
                    PromptPointOptions ppo = new PromptPointOptions("\nSpecify next point for route: ");
                    ppo.BasePoint = startPointLocation;
                    ppo.UseBasePoint = true;

                    PromptPointResult ppr = ed.GetPoint(ppo);
                    if (ppr.Status != PromptStatus.OK)
                        return;

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    Line line = new Line();
                    line.StartPoint =  startPointLocation;
                    line.EndPoint = new Point3d(ppr.Value.X, ppr.Value.Y, 0);
                    line.Layer = layer;
                    btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    addedLineId =line.ObjectId;
                }
                else if (per1.Status == PromptStatus.OK)
                {
                    ObjectId endObjectId = per1.ObjectId;
                    Entity endEntity = (Entity)tr.GetObject(endObjectId, OpenMode.ForRead);
                    Point3d endPointLocation = Point3d.Origin;
                    if (endEntity is BlockReference endBlockRef)
                    {
                        string verticalRouteId = "";
                        DynamicBlockReferencePropertyCollection properties = endBlockRef.DynamicBlockReferencePropertyCollection;
                        foreach (DynamicBlockReferenceProperty prop in properties)
                        {
                            if (prop.PropertyName == "Connection X")
                            {
                                pointX = Convert.ToInt32(prop.Value);
                            }
                            if (prop.PropertyName == "Connection Y")
                            {
                                pointY = Convert.ToInt32(prop.Value);
                            }
                            if (prop.PropertyName == "vertical_route_id")
                            {
                                verticalRouteId = prop.Value.ToString();
                            }
                        }
                        if (pointX != 0 || pointY != 0)
                        {
                            double rotation = endBlockRef.Rotation;
                            double rotatedX = pointX * Math.Cos(rotation) - pointY * Math.Sin(rotation);
                            double rotatedY = pointX * Math.Sin(rotation) + pointY * Math.Cos(rotation);
                            endPointLocation = new Point3d(endBlockRef.Position.X + rotatedX, endBlockRef.Position.Y + rotatedY, 0);
                        }
                        ChangeVerticalRouteInfo(tr, verticalRouteId, layer, LineGUID);
                     }

                    //adding line to the drawing
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    Line line = new Line();
                    line.StartPoint = startPointLocation;
                    line.EndPoint = endPointLocation;
                    line.Layer = layer;
                    btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    addedLineId = line.ObjectId;
                }

                //PropagateUpRouteInfo(tr, layer, LineGUID);

                tr.Commit();
            }
           
            AttachRouteXData(addedLineId, LineGUID, FedFromId);
            AddArrowsToLine(addedLineId, LineGUID);
        }
    }

    [CommandMethod("PlumbingVerticalRoute")]
    public async void PlumbingVerticalRoute()
    {

        List<ObjectId> basePointIds = new List<ObjectId>();
        int startFloor = 0;
        Point3d StartBasePointLocation = new Point3d(0, 0, 0);
        Point3d StartUpLocation = new Point3d(0, 0, 0);
        ObjectId startPipeId = ObjectId.Null;
        string verticalRouteId = Guid.NewGuid().ToString();

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt["GMEP_PLUMBING_BASEPOINT"], OpenMode.ForRead);
            Dictionary<string, List<ObjectId>> basePoints = new Dictionary<string, List<ObjectId>>();
            foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
            {
               if (id.IsValid)
               {
                    using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord)
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
                        int count = keywords.Count(x => x == keyword || (x.StartsWith(keyword + "(") && x.EndsWith(")")));
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
            for (int i = 1; i <= basePointIds.Count; i++) {
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
                    br.Layer = "Defpoints";
                    BlockTableRecord curSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
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
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                //delete previous start pipe
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockReference startPipe = tr.GetObject(startPipeId, OpenMode.ForWrite) as BlockReference;
                startPipe.Erase(true);

                //start pipe
                Point3d newUpPointLocation2 = BasePointRefs[startFloor].Position + upVector;
                BlockTableRecord blockDef2 = tr.GetObject(bt["GMEP_PLUMBING_LINE_UP"], OpenMode.ForRead) as BlockTableRecord;
                BlockTableRecord curSpace2 = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                BlockReference upBlockRef2 = new BlockReference(newUpPointLocation2, blockDef2.ObjectId);
                RotateJig rotateJig = new RotateJig(upBlockRef2);
                PromptResult rotatePromptResult = ed.Drag(rotateJig);
                
                if (rotatePromptResult.Status != PromptStatus.OK)
                {
                    return;
                }
                upBlockRef2.Layer = "Defpoints";
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

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                //Continue Pipe
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                for (int i = startFloor + 1; i < endFloor; i++)
                {
                    Point3d newUpPointLocation = BasePointRefs[i].Position + upVector;
                    BlockTableRecord blockDef = tr.GetObject(bt["GMEP_PLUMBING_LINE_VERTICAL"], OpenMode.ForRead) as BlockTableRecord;
                    BlockTableRecord curSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    // Create the BlockReference at the desired location
                    BlockReference upBlockRef = new BlockReference(newUpPointLocation, blockDef.ObjectId);
                    upBlockRef.Layer = "Defpoints";
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
                BlockTableRecord blockDef3 = tr.GetObject(bt["GMEP_PLUMBING_LINE_DOWN"], OpenMode.ForRead) as BlockTableRecord;
                BlockTableRecord curSpace3 = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
                RotateJig rotateJig2 = new RotateJig(upBlockRef3);
                PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
                if (rotatePromptResult2.Status != PromptStatus.OK)
                {
                    return;
                }
                upBlockRef3.Layer = "Defpoints";
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
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                //delete previous start pipe
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockReference startPipe = tr.GetObject(startPipeId, OpenMode.ForWrite) as BlockReference;
                startPipe.Erase(true);

                //start pipe
                Point3d newUpPointLocation2 = BasePointRefs[startFloor].Position + upVector;
                BlockTableRecord blockDef2 = tr.GetObject(bt["GMEP_PLUMBING_LINE_DOWN"], OpenMode.ForRead) as BlockTableRecord;
                BlockTableRecord curSpace2 = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                BlockReference upBlockRef2 = new BlockReference(newUpPointLocation2, blockDef2.ObjectId);
                RotateJig rotateJig = new RotateJig(upBlockRef2);
                PromptResult rotatePromptResult = ed.Drag(rotateJig);
                if (rotatePromptResult.Status != PromptStatus.OK)
                {
                    return;
                }
                upBlockRef2.Layer = "Defpoints";
                curSpace2.AppendEntity(upBlockRef2);
                tr.AddNewlyCreatedDBObject(upBlockRef2, true);
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

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                //Continue Pipe
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                for (int i = startFloor - 1; i > endFloor; i--)
                {
                    Point3d newUpPointLocation = BasePointRefs[i].Position + upVector;
                    BlockTableRecord blockDef = tr.GetObject(bt["GMEP_PLUMBING_LINE_VERTICAL"], OpenMode.ForRead) as BlockTableRecord;
                    BlockTableRecord curSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    // Create the BlockReference at the desired location
                    BlockReference upBlockRef = new BlockReference(newUpPointLocation, blockDef.ObjectId);
                    upBlockRef.Layer = "Defpoints";
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
                BlockTableRecord blockDef3 = tr.GetObject(bt["GMEP_PLUMBING_LINE_UP"], OpenMode.ForRead) as BlockTableRecord;
                BlockTableRecord curSpace3 = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                BlockReference upBlockRef3 = new BlockReference(newUpPointLocation3, blockDef3.ObjectId);
                RotateJig rotateJig2 = new RotateJig(upBlockRef3);
                PromptResult rotatePromptResult2 = ed.Drag(rotateJig2);
                if (rotatePromptResult2.Status != PromptStatus.OK)
                {
                    return;
                }
                upBlockRef3.Layer = "Defpoints";
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
    }



    [CommandMethod("SETPLUMBINGBASEPOINT")]
    public async void SetPlumbingBasePoint()
    {


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
        if (water) viewport += "Water";
        if (viewport != "" && gas) viewport += "-";
        if (gas) viewport += "Gas";
        if (viewport != "" && sewerVent) viewport += "-";
        if (sewerVent) viewport += "Sewer-Vent";
        if (viewport != "" && storm) viewport += "-";
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
                        else if (prop.PropertyName == "View_Id")
                        {
                            prop.Value = ViewId;
                        }
                        else if (prop.PropertyName == "Id")
                        {
                            prop.Value = Guid.NewGuid().ToString();
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
            view.Height = ext.MaxPoint.Y*50 - ext.MinPoint.Y*50;
            view.Width = ext.MaxPoint.X*50 - ext.MinPoint.X*50;
            ed.SetCurrentView(view);
        }
    }
    public void ChangeVerticalRouteInfo(Transaction tr, string VerticalRouteId, string layer, string RefId)
    {
        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
        BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        List<string> blockNames = new List<string>
        {
            "GMEP_PLUMBING_LINE_UP",
            "GMEP_PLUMBING_LINE_DOWN",
            "GMEP_PLUMBING_LINE_VERTICAL"
        };
        foreach (var name in blockNames)
        {
            BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForWrite);

            foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
            {
                if (id.IsValid)
                {
                    using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord)
                    {
                        if (anonymousBtr != null)
                        {
                            foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                            {
                                var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;
                                var pc = entity.DynamicBlockReferencePropertyCollection;
                                
                                bool match = false;

                                foreach (DynamicBlockReferenceProperty prop in pc)
                                {
                                    if (prop.PropertyName == "vertical_route_id")
                                    {
                                        if (prop.Value.ToString() == VerticalRouteId)
                                        {
                                                match = true;
                                                entity.Layer = layer; 
                                         }
                                    }
                                }
                                if (match)
                                {
                                    foreach (DynamicBlockReferenceProperty prop in pc)
                                    {
                                        if (prop.PropertyName == "fed_from_id")
                                        {
                                            prop.Value = RefId; 
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
    public void Db_VerticalRouteErased(object sender, ObjectErasedEventArgs e)
    {
        try
        {
            if (e.Erased && !SettingObjects)
            {
                ed.WriteMessage($"\nObject {e.DBObject.ObjectId} was erased.");
                Entity obj = e.DBObject as Entity;
                if (obj is BlockReference blockRef)
                {
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
                            Dictionary<string, List<ObjectId>> fedFromRefs = new Dictionary<string, List<ObjectId>>();
                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                            
                            //getting lines
                            BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                            foreach (ObjectId entId in modelSpace)
                            {
                                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                if (ent is Line line)
                                {
                                    if (line.XData != null)
                                    {
                                        TypedValue[] values = line.XData.AsArray();
                                        string FedFromId = values[2].Value as string;
                                        if (fedFromRefs.ContainsKey(FedFromId))
                                        {
                                            fedFromRefs[FedFromId].Add(line.ObjectId);
                                        }
                                        else
                                        {
                                            fedFromRefs.Add(FedFromId, new List<ObjectId> { line.ObjectId });
                                        }
                                    }
                                }
                            }
                            List<string> blockNames = new List<string>
                            {
                                "GMEP_PLUMBING_LINE_UP",
                                "GMEP_PLUMBING_LINE_DOWN",
                                "GMEP_PLUMBING_LINE_VERTICAL"
                            };
                            foreach (var name in blockNames)
                            {
                                BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForWrite);
                                foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
                                {
                                    if (id.IsValid)
                                    {
                                        using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord)
                                        {
                                            if (anonymousBtr != null)
                                            {
                                                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                                                {
                                                    if (objId.IsValid)
                                                    {
                                                        var entity = tr.GetObject(objId, OpenMode.ForWrite) as BlockReference;
                                                    
                                                        var pc = entity.DynamicBlockReferencePropertyCollection;

                                                        string GUID = "";
                                                        bool match = false;
                                                        
                                                        foreach (DynamicBlockReferenceProperty prop in pc)
                                                        {
                                                            if (prop.PropertyName == "vertical_route_id" &&
                                                                prop.Value?.ToString() == VerticalRouteId)
                                                            {

                                                                match = true;

                                                            }
                                                            if (prop.PropertyName == "id")
                                                            {
                                                                GUID = prop.Value?.ToString();
                                                            }
                                                        }
                                                        if (match)
                                                        {
                                                            entity.Erase();
                                                            if (fedFromRefs.ContainsKey(GUID))
                                                            { 
                                                                foreach (ObjectId lineId in fedFromRefs[GUID])
                                                                {
                                                                    if (lineId.IsValid)
                                                                    {
                                                                        Entity lineEntity = tr.GetObject(lineId, OpenMode.ForWrite) as Entity;
                                                                        if (lineEntity != null)
                                                                        {
                                                                            lineEntity.Erase();
                                                                            deleteLines(tr, fedFromRefs, GUID);
                                                                        }
                                                                    }
                                                                }
                                                                fedFromRefs.Remove(GUID);
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
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nError in Db_ObjectErased: {ex.Message}");
        }
    }
    public void deleteLines(Transaction tr, Dictionary<string, List<ObjectId>> fedFromRefs, string RefId)
    {
        if (fedFromRefs.ContainsKey(RefId))
        {
            foreach (ObjectId lineId in fedFromRefs[RefId])
            {
                if (lineId.IsValid)
                {
                    Entity lineEntity = tr.GetObject(lineId, OpenMode.ForWrite) as Entity;
                    string Id = "";
                    if (lineEntity.XData != null)
                    {
                        TypedValue[] values = lineEntity.XData.AsArray();
                        Id = values[1].Value as string;
                    }

                    if (Id != "")
                    {
                        lineEntity.Erase();
                        deleteLines(tr, fedFromRefs, Id);
                    }
                }
            }
        }
    }
    public void DB_LineErased(object sender, ObjectErasedEventArgs e)
    {
        if (e.Erased)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                List<string> blockNames = new List<string>
                {
                    "GMEP_PLUMBING_LINE_UP",
                    "GMEP_PLUMBING_LINE_DOWN",
                    "GMEP_PLUMBING_LINE_VERTICAL"
                };
                Dictionary<string, List<ObjectId>> fedFromRefs = new Dictionary<string, List<ObjectId>>();
                foreach (var name in blockNames)
                {
                    BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);

                    foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
                    {
                        if (id.IsValid)
                        {
                            using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord)
                            {
                                if (anonymousBtr != null)
                                {
                                    foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                                    {
                                        var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                                        var pc = entity.DynamicBlockReferencePropertyCollection;
                                        bool match = false;
                                        string newId = "";
                                        foreach (DynamicBlockReferenceProperty prop in pc)
                                        {
                                            if (prop.PropertyName == "fed_from_id")
                                            {
                                                if (fedFromRefs.ContainsKey(prop.Value.ToString()))
                                                {
                                                    fedFromRefs[prop.Value.ToString()].Add(entity.ObjectId);
                                                }
                                                else
                                                {
                                                    fedFromRefs.Add(prop.Value.ToString(), new List<ObjectId> { entity.ObjectId });
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                foreach (ObjectId entId in modelSpace)
                {
                    Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent is Line line)
                    {
                        if (line.XData != null)
                        {
                            TypedValue[] values = line.XData.AsArray();
                            string FedFromId = values[2].Value as string;
                            if (fedFromRefs.ContainsKey(FedFromId))
                            {
                                fedFromRefs[FedFromId].Add(line.ObjectId);
                            }
                            else
                            {
                                fedFromRefs.Add(FedFromId, new List<ObjectId> { line.ObjectId });
                            }
                        }
                    }
                }
                if (e.DBObject is Entity objEntity)
                {
                    if (objEntity is Line line)
                    {
                        if (line.XData != null)
                        {
                            TypedValue[] values = line.XData.AsArray();
                            string id = values[1].Value as string;
                            SetRouteInfo(tr, fedFromRefs, "Defpoints", id);
                        }
                    }
                }

            }
         }
    }



        /*public void PropagateUpRouteInfo(Transaction tr, string layer, string RefId)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            List<string> blockNames = new List<string>
            {
                "GMEP_PLUMBING_LINE_UP",
                "GMEP_PLUMBING_LINE_DOWN",
                "GMEP_PLUMBING_LINE_VERTICAL"
            };
            Dictionary<string, List<ObjectId>> fedFromRefs = new Dictionary<string, List<ObjectId>>();
            foreach (var name in blockNames)
            {
                BlockTableRecord basePointBlock = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);

                foreach (ObjectId id in basePointBlock.GetAnonymousBlockIds())
                {
                    if (id.IsValid)
                    {
                        using (BlockTableRecord anonymousBtr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord)
                        {
                            if (anonymousBtr != null)
                            {
                                foreach (ObjectId objId in anonymousBtr.GetBlockReferenceIds(true, false))
                                {
                                    var entity = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                                    var pc = entity.DynamicBlockReferencePropertyCollection;
                                    bool match = false;
                                    string newId = "";
                                    foreach (DynamicBlockReferenceProperty prop in pc)
                                    {
                                        if (prop.PropertyName == "fed_from_id")
                                        {
                                            if (fedFromRefs.ContainsKey(prop.Value.ToString()))
                                            {
                                                fedFromRefs[prop.Value.ToString()].Add(entity.ObjectId);
                                            }
                                            else
                                            {
                                                fedFromRefs.Add(prop.Value.ToString(), new List<ObjectId> { entity.ObjectId });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (ObjectId entId in modelSpace)
            {
                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent is Line line)
                {
                    if (line.XData != null)
                    {
                        TypedValue[] values = line.XData.AsArray();
                        string FedFromId = values[2].Value as string;
                        if (fedFromRefs.ContainsKey(FedFromId))
                        {
                            fedFromRefs[FedFromId].Add(line.ObjectId);
                        }
                        else
                        {
                            fedFromRefs.Add(FedFromId, new List<ObjectId> { line.ObjectId });
                        }
                    }
                }
            }
            SetRouteInfo(tr, fedFromRefs, layer, RefId);

        }*/
        public void SetRouteInfo(Transaction tr, Dictionary<string, List<ObjectId>> FedFromRefs, string layer, string refId)
        {
            if (FedFromRefs.ContainsKey(refId))
            {
                    foreach (ObjectId objId in FedFromRefs[refId])
                    {
                        Entity entity = (Entity)tr.GetObject(objId, OpenMode.ForWrite);
                        entity.Layer = layer;
                        if (entity is BlockReference blockRef)
                        {
                            DynamicBlockReferencePropertyCollection properties = blockRef.DynamicBlockReferencePropertyCollection;
                            foreach (DynamicBlockReferenceProperty prop in properties)
                            {
                                if (prop.PropertyName == "id")
                                {
                                    SetRouteInfo(tr, FedFromRefs, layer, prop.Value.ToString());
                                }
                            }
                        }
                        else if (entity is Line line)
                        {
                            TypedValue[] values = line.XData.AsArray();
                            string LineId = values[1].Value as string;
                            SetRouteInfo(tr, FedFromRefs, layer, LineId);
                        }
                    }
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
                    Layer = line.Layer
                };
                btr.AppendEntity(arrowRef);
                tr.AddNewlyCreatedDBObject(arrowRef, true);
                DynamicBlockReferencePropertyCollection properties = arrowRef.DynamicBlockReferencePropertyCollection;
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
    private void AttachRouteXData(ObjectId lineId, string id, string FedFromId)
    {
        ed.WriteMessage("Id: " + id + " FedFromId: " + FedFromId);
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            Line line = (Line)tr.GetObject(lineId, OpenMode.ForWrite);
            if (line == null || string.IsNullOrEmpty(FedFromId))
                return;

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
            ResultBuffer rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, XRecordKey),
                new TypedValue(1000, id),
                new TypedValue(1000, FedFromId)
            );
            line.XData = rb;
            rb.Dispose();
            tr.Commit();
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