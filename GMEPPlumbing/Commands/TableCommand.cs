using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using System;

namespace GMEPPlumbing.Commands
{
  public class TableCommand
  {
    [CommandMethod("CreateWaterCalcTable")]
    public void CreateWaterCalculationTable()
    {
      Document doc = Application.DocumentManager.MdiActiveDocument;
      Database db = doc.Database;
      Editor ed = doc.Editor;

      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        try
        {
          BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
          Table table = new Table();

          // Set table properties
          table.TableStyle = db.Tablestyle;
          table.SetSize(13, 4);  // 13 rows, 4 columns

          PromptPointResult pr = ed.GetPoint("\nSpecify insertion point: ");
          if (pr.Status != PromptStatus.OK)
            return;
          table.Position = pr.Value;

          // Set layer to "M-TEXT"
          table.Layer = "M-TEXT";

          // Set column widths
          table.Columns[0].Width = 0.40535461;
          table.Columns[1].Width = 2.89712166;
          table.Columns[2].Width = 0.90693862;
          table.Columns[3].Width = 0.65590551;

          // Set row heights and text properties
          for (int row = 0; row < 13; row++)
          {
            for (int col = 0; col < 4; col++)
            {
              Cell cell = table.Cells[row, col];
              cell.TextHeight = 0.09375000;
              cell.TextStyleId = CreateOrGetTextStyle(db, tr, "Archquick");
            }

            if (row == 0)
            {
              table.Rows[row].Height = 0.5;  // Header
              for (int col = 0; col < 4; col++)
              {
                table.Cells[row, col].TextHeight = 0.12500000;
              }
            }
            else if (row == 3)
            {
              table.Rows[row].Height = 0.35; // Subheader
            }
            else
            {
              table.Rows[row].Height = 0.25; // Regular rows
            }
          }

          // Populate the table
          table.Cells[0, 0].TextString = "TYPICAL WATER CALCULATIONS";
          table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
          table.MergeCells(CellRange.Create(table, 0, 0, 0, 3));

          table.Cells[1, 0].TextString = "STREET PRESSURE: 65 MIN / 75 MAX";
          table.Cells[1, 0].Alignment = CellAlignment.MiddleLeft;
          table.MergeCells(CellRange.Create(table, 1, 0, 1, 3));

          table.Cells[2, 0].TextString = "METER SIZE: 5/8\" FOR 15 GPM";
          table.Cells[2, 0].Alignment = CellAlignment.MiddleLeft;
          table.MergeCells(CellRange.Create(table, 2, 0, 2, 3));

          table.MergeCells(CellRange.Create(table, 3, 0, 3, 1));
          table.Cells[3, 0].TextString = "PRESSURE CALCULATION";
          table.Cells[3, 0].Alignment = CellAlignment.MiddleLeft;
          table.Cells[3, 2].TextString = "UNIT";
          table.Cells[3, 2].Alignment = CellAlignment.MiddleCenter;
          table.Cells[3, 3].TextString = "VALUE";
          table.Cells[3, 3].Alignment = CellAlignment.MiddleCenter;

          // Define the constant values
          string[] descriptions = {
        "METER LOSS",
        "** FT STATIC LOSS",
        "MIN. PRESSURE REQUIRED",
        "TOTAL LOSSES (ITEMS 1-3)",
        "MIN. STREET PRESSURE",
        "PRESSURE AVAILABLE FOR FRICTION",
        "ACTUAL LENGTH OF SYSTEM",
        "DEVELOPED LENGTH (130% OF ITEM 7)",
        "AVERAGE PRESSURE DROP"
      };

          string[] units = {
        "PSI", "PSI", "PSI", "PSI", "PSI", "PSI", "FT", "FT", "PSI/100FT"
      };

          string[] values = {
        "4.0", "8.7", "20.0", "", "65", "", "120", "", ""
      };

          for (int i = 4; i < 13; i++)
          {
            table.Cells[i, 0].TextString = (i - 3).ToString() + ".";
            table.Cells[i, 0].Alignment = CellAlignment.MiddleLeft;
            table.Cells[i, 1].TextString = descriptions[i - 4];
            table.Cells[i, 1].Alignment = CellAlignment.MiddleLeft;
            table.Cells[i, 2].TextString = units[i - 4];
            table.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;
            table.Cells[i, 3].TextString = values[i - 4];
            table.Cells[i, 3].Alignment = CellAlignment.MiddleCenter;
          }

          currentSpace.AppendEntity(table);
          tr.AddNewlyCreatedDBObject(table, true);
          tr.Commit();
          ed.WriteMessage("\nWater calculation table created successfully.");
        }
        catch (System.Exception ex)
        {
          ed.WriteMessage($"\nError creating water calculation table: {ex.Message}");
          tr.Abort();
        }
      }
    }

    private static ObjectId CreateOrGetTextStyle(Database db, Transaction tr, string styleName)
    {
      TextStyleTable textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

      if (!textStyleTable.Has(styleName))
      {
        using (TextStyleTableRecord textStyle = new TextStyleTableRecord())
        {
          textStyle.Name = styleName;
          textStyle.Font = new FontDescriptor(styleName, false, false, 0, 0);

          textStyleTable.UpgradeOpen();
          ObjectId textStyleId = textStyleTable.Add(textStyle);
          tr.AddNewlyCreatedDBObject(textStyle, true);

          return textStyleId;
        }
      }
      else
      {
        return textStyleTable[styleName];
      }
    }
  }
}