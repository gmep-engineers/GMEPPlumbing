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

namespace GMEPPlumbing
{
  public class CADObjectCommands
  {
    public static double Scale { get; set; } = -1.0;

    public static string TextLayer = "P-HC-PPLM-TEXT";

    public static string ActiveBasePointId { get; set; }

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
    [CommandMethod("SetActiveBasePoint")]{


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

      BlockJig blockJig = new BlockJig();
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
        BlockTableRecord acBlkTblRec =
          acTrans.GetObject(acBlkTbl[$"ar{Scale}"], OpenMode.ForRead) as BlockTableRecord;
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
