using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;
using MySqlX.XDevAPI.Common;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace GMEPPlumbing
{
  public class DynamicLineJig : DrawJig
  {
    private Point3d startPoint;
    private Point3d endPoint;
    private double scale;
    public Line line;

    public DynamicLineJig(Point3d startPt, double scale, string equipId = "")
    {
      this.scale = scale;
      startPoint = startPt;
      endPoint = startPt;
      line = new Line(startPoint, startPoint);
      line.Layer = "D0";
      Field field = new Field(equipId);
      Field isClone = new Field("false");
      line.SetField("gmep_equip_id", field);
      line.SetField("is_clone", isClone);
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
      if (line != null)
      {
        draw.Geometry.Draw(line);
      }
      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions opts = new JigPromptPointOptions("\nSelect end point:");
      opts.BasePoint = startPoint;
      opts.UseBasePoint = true;
      opts.Cursor = CursorType.RubberBand;

      PromptPointResult res = prompts.AcquirePoint(opts);
      if (res.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (endPoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      endPoint = new Point3d(res.Value.X, startPoint.Y, startPoint.Z);
      line.EndPoint = endPoint;

      return SamplerStatus.OK;
    }
  }
  public class OffsetLineJig : DrawJig {
    private readonly Line _baseLine;
    private double _offsetDistance;
    private Point3d _mousePoint;
    private Line _previewLine;

    public OffsetLineJig(PlumbingHorizontalRoute route, double initialOffset = 4.0) {
      _baseLine = new Line(route.StartPoint, route.EndPoint);
      _offsetDistance = initialOffset;
      _mousePoint = route.StartPoint;

      Vector3d routeVec = _baseLine.EndPoint - _baseLine.StartPoint;
      Vector3d direction = routeVec.GetNormal();

      _previewLine = new Line(_baseLine.EndPoint + (direction * 3.1), _baseLine.EndPoint - (direction * 3.1));
      _previewLine.Layer = "P-DOMW-CWTR";
    }

    protected override bool WorldDraw(WorldDraw draw) {
      if (_previewLine != null)
        draw.Geometry.Draw(_previewLine);
      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts) {
      JigPromptPointOptions opts = new JigPromptPointOptions("\nSpecify offset position:");
      opts.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;
      PromptPointResult res = prompts.AcquirePoint(opts);

      if (res.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (_mousePoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      _mousePoint = res.Value;

      UpdatePreviewLine();
      return SamplerStatus.OK;
    }

    private void UpdatePreviewLine() {
      // Get the normal vector of the base line
      Vector3d routeVec = _baseLine.EndPoint - _baseLine.StartPoint;
      Vector3d normal = routeVec.CrossProduct(Vector3d.ZAxis).GetNormal();

      // Determine sign of offset based on mouse position
      Vector3d mouseVec = _mousePoint - _baseLine.StartPoint;
      double side = normal.DotProduct(mouseVec) >= 0 ? 1.0 : -1.0;

      double offsetDistance = _offsetDistance * side;

   
      Point3d offsetMid = _baseLine.EndPoint + (normal * offsetDistance);

      Vector3d halfVec = routeVec.GetNormal() * (3.1);

      Point3d newStart = offsetMid - halfVec;
      Point3d newEnd = offsetMid + halfVec;

      // Optionally adjust Z as in your example
      // newStart = new Point3d(newStart.X, newStart.Y, newStart.Z - (_baseLine.Length * 12));
      // newEnd = new Point3d(newEnd.X, newEnd.Y, newEnd.Z - (_baseLine.Length * 12));

      _previewLine.StartPoint = newStart;
      _previewLine.EndPoint = newEnd;
    }

    public Line GetOffsetLine() {
      return new Line(_previewLine.StartPoint, _previewLine.EndPoint) { Layer = _previewLine.Layer };
    }
  }
  public class HorizontalRouteJig : DrawJig {
    private Point3d startPoint;
    private Point3d endPoint;
    public Line line;
    public string message;

    public HorizontalRouteJig(Point3d startPt, string layer, string _message = "\nSelect end point:") {
      startPoint = startPt;
      endPoint = startPt;
      line = new Line(startPoint, startPoint);
      line.Layer = layer;
      message = _message;
    }

    protected override bool WorldDraw(WorldDraw draw) {
      if (line != null) {
        draw.Geometry.Draw(line);
      }
      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts) {
      JigPromptPointOptions opts = new JigPromptPointOptions(message);
      opts.BasePoint = startPoint;
      opts.UseBasePoint = true;
      opts.Cursor = CursorType.RubberBand;

      PromptPointResult res = prompts.AcquirePoint(opts);
      if (res.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (endPoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      endPoint = new Point3d(res.Value.X, res.Value.Y, startPoint.Z);
      line.EndPoint = endPoint;

      return SamplerStatus.OK;
    }
  }


  public class LineStartPointPreviewJig : DrawJig
  {
    private Line _baseLine;
    private Point3d _mousePoint;
    public Point3d ProjectedPoint { get; private set; }

    public LineStartPointPreviewJig(Line baseLine)
    {
      _baseLine = baseLine;
      _mousePoint = baseLine.StartPoint;
      ProjectedPoint = baseLine.StartPoint;
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
      Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
      ViewTableRecord view = ed.GetCurrentView();

      double unitsPerPixel = view.Width / 6000;
      double markerRadius = unitsPerPixel * 10;

      // Draw an "X" at the projected point
      Point3d p1 = ProjectedPoint + new Vector3d(-markerRadius, -markerRadius, 0);
      Point3d p2 = ProjectedPoint + new Vector3d(markerRadius, markerRadius, 0);
      Point3d p3 = ProjectedPoint + new Vector3d(-markerRadius, markerRadius, 0);
      Point3d p4 = ProjectedPoint + new Vector3d(markerRadius, -markerRadius, 0);

      Line line1 = new Line(p1, p2);
      Line line2 = new Line(p3, p4);

      draw.Geometry.Draw(line1);
      draw.Geometry.Draw(line2);

      line1.Dispose();
      line2.Dispose();

      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions opts = new JigPromptPointOptions(
        "\nMove cursor to preview start point, click to select:"
      );
      opts.UserInputControls =
        UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;
      PromptPointResult res = prompts.AcquirePoint(opts);

      if (res.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (_mousePoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      _mousePoint = res.Value;
      ProjectedPoint = ProjectPointToLineSegment(
        _baseLine.StartPoint,
        _baseLine.EndPoint,
        _mousePoint
      );
      return SamplerStatus.OK;
    }

    public static Point3d ProjectPointToLineSegment(Point3d a, Point3d b, Point3d p)
    {
      Vector3d ab = b - a;
      Vector3d ap = p - a;
      double t = ab.DotProduct(ap) / ab.LengthSqrd;
      t = Math.Max(0, Math.Min(1, t)); // Clamp to segment
      return a + ab * t;
    }
  }
  public class CircleStartPointPreviewJig : DrawJig {
    private Point3d _center;
    private double _radius;
    private Point3d _mousePoint;
    public Point3d ProjectedPoint { get; private set; }

    public CircleStartPointPreviewJig(Point3d center, double radius) {
      _center = center;
      _radius = radius;
      _mousePoint = center;
      ProjectedPoint = center + new Vector3d(radius, 0, 0); // Default to right
    }

    protected override bool WorldDraw(WorldDraw draw) {
      Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
      ViewTableRecord view = ed.GetCurrentView();

      double unitsPerPixel = view.Width / 6000;
      double markerRadius = unitsPerPixel * 10;

      // Draw an "X" at the projected point
      Point3d p1 = ProjectedPoint + new Vector3d(-markerRadius, -markerRadius, 0);
      Point3d p2 = ProjectedPoint + new Vector3d(markerRadius, markerRadius, 0);
      Point3d p3 = ProjectedPoint + new Vector3d(-markerRadius, markerRadius, 0);
      Point3d p4 = ProjectedPoint + new Vector3d(markerRadius, -markerRadius, 0);

      Line line1 = new Line(p1, p2);
      Line line2 = new Line(p3, p4);

      draw.Geometry.Draw(line1);
      draw.Geometry.Draw(line2);

      line1.Dispose();
      line2.Dispose();

      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts) {
      JigPromptPointOptions opts = new JigPromptPointOptions(
          "\nMove cursor to preview point on circle, click to select:"
      ) {
        BasePoint = _center,
        UseBasePoint = true,
        Cursor = CursorType.RubberBand,
      };
      opts.UserInputControls =
          UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;
      PromptPointResult res = prompts.AcquirePoint(opts);

      if (res.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (_mousePoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      _mousePoint = new Point3d(res.Value.X, res.Value.Y, _center.Z);
      ProjectedPoint = ProjectPointToCircle(_center, _radius, _mousePoint);
      return SamplerStatus.OK;
    }

    public static Point3d ProjectPointToCircle(Point3d center, double radius, Point3d p) {
      Vector3d dir = (p - center).GetNormal();
      if (dir.Length < Tolerance.Global.EqualPoint)
        dir = new Vector3d(1, 0, 0); // Default direction if mouse is at center
      dir = dir.GetNormal();
      return center + dir * radius;
    }
  }

  public class PolyLineJig : DrawJig
  {
    private Polyline polyline;
    public Point3d CurrentPoint;
    private List<Point3d> vertices;

    public PolyLineJig(Point3d startPoint)
    {
      polyline = new Polyline();
      vertices = new List<Point3d> { startPoint };
      polyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
      CurrentPoint = startPoint;
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
      if (polyline != null)
      {
        draw.Geometry.Draw(polyline);
      }
      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions options = new JigPromptPointOptions("\nSelect next point or [Close]:");
      options.UserInputControls =
        UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;
      options.Keywords.Add("Close");
      PromptPointResult result = prompts.AcquirePoint(options);

      if (result.Status == PromptStatus.Keyword && result.StringResult == "Close")
      {
        if (vertices.Count > 2)
        {
          polyline.Closed = true;
          return SamplerStatus.Cancel;
        }
        else
        {
          Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
            "\nA polyline must have at least 3 vertices to be closed."
          );
          return SamplerStatus.NoChange;
        }
      }
      if (result.Status == PromptStatus.Cancel || result.Status == PromptStatus.Error)
      {
        return SamplerStatus.Cancel;
      }
      options.BasePoint = CurrentPoint;
      options.UseBasePoint = true;
      options.Cursor = CursorType.RubberBand;

      if (result.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (CurrentPoint.DistanceTo(result.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      CurrentPoint = result.Value;

      return SamplerStatus.OK;
    }

    public void AddVertex(Point3d point)
    {
      vertices.Add(point);
      polyline.AddVertexAt(vertices.Count - 1, new Point2d(point.X, point.Y), 0, 0, 0);
    }

    public Polyline GetPolyline()
    {
      return polyline;
    }
  }

  public class SpoonJig : DrawJig
  {
    private Point3d firstClickPoint;
    private Point3d startPoint;
    public Point3d endPoint { get; private set; }
    private Point3d rotationPoint;
    private double scale;
    public Line line;
    public Arc arc;

    public SpoonJig(Point3d firstClick, double scale)
    {
      firstClickPoint = firstClick;
      rotationPoint = firstClickPoint;
      this.scale = scale;
      startPoint = rotationPoint + new Vector3d(-3 * (0.25 / scale), 0, 0);
      endPoint = startPoint;
      line = new Line(startPoint, startPoint);
      line.Layer = "D0";
      arc = new Arc();
      arc.Layer = "D0";
    }

    public PromptStatus DragMe()
    {
      Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
      PromptResult res;
      do
      {
        res = ed.Drag(this);
      } while (res.Status == PromptStatus.Other);
      return res.Status;
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
      if (line != null)
      {
        draw.Geometry.Draw(line);
      }
      if (arc != null && arc.StartAngle != arc.EndAngle)
      {
        draw.Geometry.Draw(arc);
      }
      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions opts = new JigPromptPointOptions("\nSelect end point:");
      opts.BasePoint = rotationPoint;
      opts.UseBasePoint = true;
      opts.Cursor = CursorType.RubberBand;

      PromptPointResult res = prompts.AcquirePoint(opts);
      if (res.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (endPoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      endPoint = res.Value;

      Vector3d direction = (endPoint - rotationPoint).GetNormal();
      startPoint = rotationPoint + direction * -3 * (0.25 / scale);

      UpdateGeometry();
      return SamplerStatus.OK;
    }

    private void UpdateGeometry()
    {
      line.StartPoint = startPoint;
      line.EndPoint = endPoint;

      Vector3d direction = (endPoint - startPoint).GetNormal();
      Vector3d perpendicular = new Vector3d(-direction.Y, direction.X, 0);
      Point3d secondPoint =
        startPoint + direction * 2 * (0.25 / scale) + perpendicular * 4 * (0.25 / scale);
      Point3d thirdPoint = startPoint + direction * 6 * (0.25 / scale);

      if ((endPoint - startPoint).Length > 6 * (0.25 / scale))
      {
        arc.SetDatabaseDefaults();
        arc.Center = Arc3PCenter(startPoint, secondPoint, thirdPoint);
        arc.Radius = (startPoint - arc.Center).Length;

        Vector3d startVector = startPoint - arc.Center;
        Vector3d endVector = thirdPoint - arc.Center;

        arc.StartAngle = Math.Atan2(startVector.Y, startVector.X);
        arc.EndAngle = Math.Atan2(endVector.Y, endVector.X);
      }
    }

    private Point3d Arc3PCenter(Point3d p1, Point3d p2, Point3d p3)
    {
      CircularArc3d tempArc = new CircularArc3d(p1, p2, p3);
      return tempArc.Center;
    }
  }

  public class ArrowJig : EntityJig
  {
    private Point3d _insertionPoint;
    private Point3d _leaderPoint;
    private Point3d _dnLocation;
    private Vector3d _direction;

    public ArrowJig(BlockReference blockRef, Point3d dnLocation)
      : base(blockRef)
    {
      _insertionPoint = Point3d.Origin;
      _dnLocation = dnLocation;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions pointOptions = new JigPromptPointOptions("\nSpecify insertion point: ");
      PromptPointResult pointResult = prompts.AcquirePoint(pointOptions);

      if (pointResult.Status == PromptStatus.OK)
      {
        if (_insertionPoint == pointResult.Value)
          return SamplerStatus.NoChange;

        _insertionPoint = pointResult.Value;
        _direction = _dnLocation - _insertionPoint;
        return SamplerStatus.OK;
      }

      return SamplerStatus.Cancel;
    }

    protected override bool Update()
    {
      //((BlockReference)Entity).Position = _insertionPoint;
      ((BlockReference)Entity).Rotation = Math.Atan2(_direction.Y, _direction.X) + Math.PI;
      double x = _dnLocation.X + (1.5 * Math.Cos(((BlockReference)Entity).Rotation));
      double y = _dnLocation.Y + (1.5 * Math.Sin(((BlockReference)Entity).Rotation));
      _leaderPoint = new Point3d(x, y, 0);
      ((BlockReference)Entity).Position = _leaderPoint;
      ((BlockReference)Entity).Rotation = ((BlockReference)Entity).Rotation + Math.PI / 2;
      return true;
    }

    public Point3d InsertionPoint => _insertionPoint;
    public Point3d LeaderPoint => _leaderPoint;
  }

  public class GeneralTextJig : EntityJig
  {
    private Point3d insertionPoint;
    private string textString;
    private double textHeight;
    private TextHorizontalMode horizontalMode;

    public Point3d InsertionPoint => insertionPoint;

    public GeneralTextJig(string textString, double textHeight, TextHorizontalMode horizontalMode)
      : base(new DBText())
    {
      this.textString = textString;
      this.textHeight = textHeight;
      this.horizontalMode = horizontalMode;

      DBText dbText = (DBText)Entity;
      dbText.TextString = textString;
      dbText.Height = textHeight;
      dbText.HorizontalMode = horizontalMode;
    }

    protected override bool Update()
    {
      DBText dbText = (DBText)Entity;
      dbText.Position = insertionPoint;

      if (horizontalMode != TextHorizontalMode.TextLeft)
      {
        dbText.AlignmentPoint = insertionPoint;
      }

      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions jigOpts = new JigPromptPointOptions("\nSpecify insertion point: ");
      PromptPointResult ppr = prompts.AcquirePoint(jigOpts);

      if (ppr.Status == PromptStatus.OK)
      {
        if (ppr.Value.IsEqualTo(insertionPoint))
        {
          return SamplerStatus.NoChange;
        }
        else
        {
          insertionPoint = ppr.Value;
          return SamplerStatus.OK;
        }
      }

      return SamplerStatus.Cancel;
    }
  }

  public class LabelJig : DrawJig
  {
    private Point3d startPoint;
    public Point3d endPoint { get; private set; }
    public Line line;

    public LabelJig(Point3d firstClick)
    {
      startPoint = firstClick;
      endPoint = startPoint;
      line = new Line(startPoint, startPoint);
      line.Layer = "D0";
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
      if (line != null)
      {
        draw.Geometry.Draw(line);
      }
      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions opts = new JigPromptPointOptions("\nSelect end point:");
      opts.BasePoint = startPoint;
      opts.UseBasePoint = true;
      opts.Cursor = CursorType.RubberBand;

      PromptPointResult res = prompts.AcquirePoint(opts);
      if (res.Status != PromptStatus.OK)
      {
        return SamplerStatus.Cancel;
      }

      if (endPoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
      {
        return SamplerStatus.NoChange;
      }

      endPoint = res.Value;

      UpdateGeometry();
      return SamplerStatus.OK;
    }

    private void UpdateGeometry()
    {
      line.StartPoint = startPoint;
      line.EndPoint = endPoint;
    }
  }

  public class BlockJig : DrawJig
  {
    public Point3d _point;

    private ObjectId _blockId = ObjectId.Null;

    private string _name = string.Empty;
    private double _scale = 1;

    public BlockJig(ObjectId blockId, string _name = "block", double _scale = 1)
    {
      this._name = _name;
      this._scale = _scale;
      this._blockId = blockId;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      if (_blockId == ObjectId.Null || !_blockId.IsValid)
        return SamplerStatus.Cancel;

      JigPromptPointOptions jigOpts = new JigPromptPointOptions();

      jigOpts.UserInputControls = (
        UserInputControls.Accept3dCoordinates
      );

      jigOpts.Message = $"Select a point for {_name}:";

      PromptPointResult jigRes = prompts.AcquirePoint(jigOpts);

      if (jigRes.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      Point3d pt = jigRes.Value;

      if (pt == _point)
        return SamplerStatus.NoChange;

      _point = pt;

      return SamplerStatus.OK;
    }

    protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
    {
      BlockReference inMemoryBlockInsert = new BlockReference(_point, _blockId);
      inMemoryBlockInsert.ScaleFactors = new Scale3d(_scale, _scale, 1);

      draw.Geometry.Draw(inMemoryBlockInsert);

      inMemoryBlockInsert.Dispose();

      return true;
    }
  }

  public class LineArrowJig : DrawJig
  {
    private readonly Line _baseLine;
    private readonly ObjectId _blockDefId;
    private Point3d _mousePoint;
    public Point3d InsertionPoint { get; private set; }
    private double _blockScale;
    private double _blockRotation;

    public LineArrowJig(
      Line baseLine,
      ObjectId blockDefId,
      double blockScale = 1.0,
      double blockRotation = 0.0
    )
    {
      _baseLine = baseLine;
      _blockDefId = blockDefId;
      _mousePoint = baseLine.StartPoint;
      InsertionPoint = baseLine.StartPoint;
      _blockScale = blockScale;
      _blockRotation = blockRotation;
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
      // Preview the block at the projected point
      BlockReference previewBlock = new BlockReference(InsertionPoint, _blockDefId)
      {
        ScaleFactors = new Scale3d(_blockScale),
        Rotation = _blockRotation,
      };
      draw.Geometry.Draw(previewBlock);
      previewBlock.Dispose();
      return true;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions opts = new JigPromptPointOptions("\nPlace Line Arrow:");
      opts.UserInputControls =
        UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;
      PromptPointResult res = prompts.AcquirePoint(opts);

      if (res.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (_mousePoint.DistanceTo(res.Value) < Tolerance.Global.EqualPoint)
        return SamplerStatus.NoChange;

      _mousePoint = res.Value;
      InsertionPoint = LineStartPointPreviewJig.ProjectPointToLineSegment(
        _baseLine.StartPoint,
        _baseLine.EndPoint,
        _mousePoint
      );
      return SamplerStatus.OK;
    }
  }

  public class RotateJig : EntityJig
  {
    private Vector3d _direction;
    private Point3d _rotationRefPoint;
    private double _baseRotation;
    private readonly BlockReference _blockRef;

    public RotateJig(BlockReference blockRef)
      : base(blockRef)
    {
      _blockRef = blockRef;
      _rotationRefPoint = blockRef.Position;
      _baseRotation = blockRef.Rotation;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      var prompt = "\nSpecify rotation reference point: ";
      var pointOptions = new JigPromptPointOptions(prompt)
      {
        BasePoint = _blockRef.Position,
        UseBasePoint = true,
        Cursor = CursorType.RubberBand,
      };
      var pointResult = prompts.AcquirePoint(pointOptions);

      if (pointResult.Status != PromptStatus.OK)
        return SamplerStatus.Cancel;

      if (_rotationRefPoint == pointResult.Value)
        return SamplerStatus.NoChange;

      _rotationRefPoint = pointResult.Value;
      _direction = _rotationRefPoint - _blockRef.Position;
      return SamplerStatus.OK;
    }

    protected override bool Update()
    {
      if (_direction.Length > Tolerance.Global.EqualPoint)
      {
        double angle = Math.Atan2(_direction.Y, _direction.X) - Math.PI / 2;
        ((BlockReference)Entity).Rotation = _baseRotation + angle;
      }
      return true;
    }
  }
}
