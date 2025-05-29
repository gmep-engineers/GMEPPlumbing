using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using MySqlX.XDevAPI.Common;
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
      line.Layer = "E-TXT1";
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
      line.Layer = "E-TXT1";
      arc = new Arc();
      arc.Layer = "E-TXT1";
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
    private Point3d _panelLocation;
    private Vector3d _direction;

    public ArrowJig(BlockReference blockRef, Point3d panelLocation)
      : base(blockRef)
    {
      _insertionPoint = Point3d.Origin;
      _panelLocation = panelLocation;
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
        _direction = _panelLocation - _insertionPoint;
        return SamplerStatus.OK;
      }

      return SamplerStatus.Cancel;
    }

    protected override bool Update()
    {
      ((BlockReference)Entity).Position = _insertionPoint;
      ((BlockReference)Entity).Rotation = Math.Atan2(_direction.Y, _direction.X) - Math.PI / 2;
      return true;
    }

    public Point3d InsertionPoint => _insertionPoint;
  }

  public class LabelJig : DrawJig
  {
    private Point3d startPoint;
    public Point3d endPoint { get; private set; }
    public Line line;

    public LabelJig(Point3d firstClick, string equipId = "")
    {
      startPoint = firstClick;
      endPoint = startPoint;
      line = new Line(startPoint, startPoint);
      Field field = new Field(equipId);
      Field isClone = new Field("false");
      line.SetField("gmep_equip_id", field);
      line.SetField("is_clone", isClone);
      line.Layer = "E-TXT1";
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

    public BlockJig(string _name = "block", double _scale = 1)
    {
      this._name = _name;
      this._scale = _scale;
    }

    public PromptResult DragMe(ObjectId i_blockId, out Point3d o_pnt)
    {
      _blockId = i_blockId;

      Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

      PromptResult jigRes = ed.Drag(this);

      o_pnt = _point;

      return jigRes;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      JigPromptPointOptions jigOpts = new JigPromptPointOptions();

      jigOpts.UserInputControls = (
        UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted
      );

      jigOpts.Message = $"Select a point for {_name}:";

      PromptPointResult jigRes = prompts.AcquirePoint(jigOpts);

      Point3d pt = jigRes.Value;

      if (pt == _point)
        return SamplerStatus.NoChange;

      _point = pt;

      if (jigRes.Status == PromptStatus.OK)
        return SamplerStatus.OK;

      return SamplerStatus.Cancel;
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

  public class RotateJig : EntityJig
  {
    private Point3d _insertionPoint;
    private Point3d _rotationPoint;
    private Vector3d _direction;
    private bool _inserted;

    public RotateJig(BlockReference blockRef)
      : base(blockRef)
    {
      _insertionPoint = Point3d.Origin;
      _rotationPoint = Point3d.Origin;
      _inserted = false;
    }

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
      string prompt;
      if (!_inserted)
      {
        prompt = "\nSpecify insertion point: ";
      }
      else
      {
        prompt = "\nSpecify rotation: ";
      }
      JigPromptPointOptions pointOptions = new JigPromptPointOptions(prompt);
      PromptPointResult pointResult = prompts.AcquirePoint(pointOptions);
      if (pointResult.Status == PromptStatus.OK)
      {
        if (!_inserted)
        {
          if (pointResult.Status == PromptStatus.OK)
          {
            if (_insertionPoint == pointResult.Value)
            {
              return SamplerStatus.NoChange;
            }
            _insertionPoint = pointResult.Value;
            _inserted = true;
            return SamplerStatus.OK;
          }
        }
        else
        {
          if (pointResult.Status == PromptStatus.OK)
          {
            if (_rotationPoint == pointResult.Value)
            {
              return SamplerStatus.NoChange;
            }
            _direction = _insertionPoint - pointResult.Value;
            _rotationPoint = pointResult.Value;
            return SamplerStatus.OK;
          }
        }
      }
      return SamplerStatus.Cancel;
    }

    protected override bool Update()
    {
      ((BlockReference)Entity).Position = _insertionPoint;
      double rotation = 0;
      if (
        (_insertionPoint - _rotationPoint).Length > 12
        && (_insertionPoint - _rotationPoint).Length < 72
      )
      {
        rotation = Math.Atan2(_direction.Y, _direction.X) - Math.PI / 2;
        for (int i = -12; i < 12; i += 2)
        {
          if (rotation >= 0.3926991 * (i - 1) && rotation < 0.3926991 * (i + 1))
          {
            rotation = ((0.3926991 * i) + 0.3926991 * (i + 2)) / 2 - 0.3926991;
            break;
          }
        }
      }
      if ((_insertionPoint - _rotationPoint).Length >= 96)
      {
        rotation = Math.Atan2(_direction.Y, _direction.X) - Math.PI / 2;
      }

      ((BlockReference)Entity).Rotation = rotation;
      return true;
    }

    public Point3d InsertionPoint => _insertionPoint;
  }
}
