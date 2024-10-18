using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using GMEPPlumbing.ViewModels;
using System;
using System.Collections.Generic;

namespace GMEPPlumbing.Commands
{
  public class TableCommand
  {
    public static void CreateWaterCalculationTableResidentialBasic(WaterSystemData data)
    {
      Document doc = Application.DocumentManager.MdiActiveDocument;
      Database db = doc.Database;
      Editor ed = doc.Editor;

      using (DocumentLock docLock = doc.LockDocument())
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          try
          {
            BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            Table table = new Table();

            // Calculate the number of rows dynamically
            int rowCount = 13;
            rowCount += (data.PrvPressureLoss != 0.0) ? 1 : 0;
            rowCount += (data.BackflowPressureLoss != 0.0) ? 1 : 0;
            rowCount += data.AdditionalLosses.Count;

            table.TableStyle = db.Tablestyle;
            table.SetSize(rowCount, 4);

            PromptPointResult pr = ed.GetPoint("\nSpecify insertion point: ");
            if (pr.Status != PromptStatus.OK)
              return;
            table.Position = pr.Value;

            // Set layer to "M-TEXT"
            table.Layer = "M-TEXT";

            // Calculate table width based on section header length
            int maxHeaderLength;
            if (data.PressureRequired2 != 0 && !string.IsNullOrEmpty(data.MeterSize2) &&
                data.FixtureCalculation2 != 0 && data.SystemLength2 != 0)
            {
              maxHeaderLength = Math.Max(data.SectionHeader1.Length, data.SectionHeader2.Length);
            }
            else
            {
              maxHeaderLength = data.SectionHeader1.Length;
            }
            double additionalWidth = Math.Max(maxHeaderLength - 40, 0) * 0.125;

            // Set column widths
            table.Columns[0].Width = 0.40535461;
            table.Columns[1].Width = 2.89712166 + additionalWidth;
            table.Columns[2].Width = 0.90693862;
            table.Columns[3].Width = 0.65590551;

            // Set row heights and text properties
            for (int row = 0; row < rowCount; row++)
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

            string formattedMeterSize = "";
            switch (data.MeterSize)
            {
              case "0.625": formattedMeterSize = "5/8"; break;
              case "0.75": formattedMeterSize = "3/4"; break;
              case "1.5": formattedMeterSize = "1-1/2"; break;
              default: formattedMeterSize = data.MeterSize; break;
            }

            // Populate the table
            table.Cells[0, 0].TextString = $"{data.SectionHeader1.ToUpper()}";
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
            table.MergeCells(CellRange.Create(table, 0, 0, 0, 3));

            table.Cells[1, 0].TextString = $"STREET PRESSURE: {data.StreetLowPressure} MIN / {data.StreetHighPressure} MAX";
            table.Cells[1, 0].Alignment = CellAlignment.MiddleLeft;
            table.MergeCells(CellRange.Create(table, 1, 0, 1, 3));

            table.Cells[2, 0].TextString = $"METER SIZE: {formattedMeterSize}\" FOR {data.FixtureCalculation} GPM";
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
            List<(string Description, string Unit, string Value)> rows = new List<(string, string, string)>
            {
                ($"{formattedMeterSize}\" METER LOSS", "PSI", $"{data.MeterLoss:F1}"),
                ($"{data.Elevation}FT STATIC LOSS", "PSI", $"{data.StaticLoss:F1}")
            };
            int actualLengthItemNum = 7;
            // Add PRV pressure loss if not zero
            if (data.PrvPressureLoss != 0.0)
            {
              rows.Add(("PRV PRESSURE LOSS", "PSI", $"{data.PrvPressureLoss:F1}"));
              actualLengthItemNum++;
            }

            // Add backflow pressure loss if not zero
            if (data.BackflowPressureLoss != 0.0)
            {
              rows.Add(("BACKFLOW PRESSURE LOSS", "PSI", $"{data.BackflowPressureLoss:F1}"));
              actualLengthItemNum++;
            }

            // Add additional losses
            foreach (var loss in data.AdditionalLosses)
            {
              rows.Add((loss.Title, "PSI", loss.Amount));
              actualLengthItemNum++;
            }

            // Add remaining rows
            rows.AddRange(new List<(string, string, string)>
            {
                ("MIN. PRESSURE REQUIRED", "PSI", $"{data.PressureRequiredOrAtUnit:F1}"),
                ($"TOTAL LOSSES (ITEMS 1-{3 + rowCount - 13})", "PSI", $"{data.TotalLoss:F1}"),
                ("MIN. STREET PRESSURE", "PSI", $"{data.StreetLowPressure:F1}"),
                ("PRESSURE AVAILABLE FOR FRICTION", "PSI", $"{data.PressureAvailable:F1}"),
                ("ACTUAL LENGTH OF SYSTEM", "FT", $"{data.SystemLength:F1}"),
                ($"DEVELOPED LENGTH ({data.DevelopedLengthPercentage}% OF ITEM {actualLengthItemNum})", "FT", $"{data.DevelopedLength:F1}"),
                ("AVERAGE PRESSURE DROP", "PSI/100FT", $"{data.AveragePressureDrop:F1}")
            });

            // Populate the table with the rows
            for (int i = 0; i < rows.Count; i++)
            {
              int rowIndex = i + 4;
              table.Cells[rowIndex, 0].TextString = $"{i + 1}.";
              table.Cells[rowIndex, 0].Alignment = CellAlignment.MiddleLeft;
              table.Cells[rowIndex, 1].TextString = rows[i].Description;
              table.Cells[rowIndex, 1].Alignment = CellAlignment.MiddleLeft;
              table.Cells[rowIndex, 2].TextString = rows[i].Unit;
              table.Cells[rowIndex, 2].Alignment = CellAlignment.MiddleCenter;
              table.Cells[rowIndex, 3].TextString = rows[i].Value;
              table.Cells[rowIndex, 3].Alignment = CellAlignment.MiddleCenter;
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
    }

    public static void CreateWaterCalculationTableCommercialBasic(WaterSystemData data)
    {
      Document doc = Application.DocumentManager.MdiActiveDocument;
      Database db = doc.Database;
      Editor ed = doc.Editor;

      using (DocumentLock docLock = doc.LockDocument())
      {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
          try
          {
            BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            Table table = new Table();

            // Calculate the number of rows dynamically
            int rowCount = 14 + data.AdditionalLosses.Count;
            // Add rows for backflow preventer and PRV if they have non-zero values
            if (data.BackflowPressureLoss > 0) rowCount++;
            if (data.PrvPressureLoss > 0) rowCount++;

            table.TableStyle = db.Tablestyle;
            table.SetSize(rowCount, 2);

            PromptPointResult pr = ed.GetPoint("\nSpecify insertion point: ");
            if (pr.Status != PromptStatus.OK)
              return;
            table.Position = pr.Value;

            // Set layer to "M-TEXT"
            table.Layer = "M-TEXT";

            // Calculate table width based on section header length
            int maxHeaderLength;
            if (data.PressureRequired2 != 0 && !string.IsNullOrEmpty(data.MeterSize2) &&
                data.FixtureCalculation2 != 0 && data.SystemLength2 != 0)
            {
              maxHeaderLength = Math.Max(data.SectionHeader1.Length, data.SectionHeader2.Length);
            }
            else
            {
              maxHeaderLength = data.SectionHeader1.Length;
            }
            double additionalWidth = Math.Max(maxHeaderLength - 57, 0) * 0.175;

            // Set table width and column widths
            table.Width = 11.8206 + additionalWidth;
            table.Columns[0].Width = 10.0467 + additionalWidth;
            table.Columns[1].Width = 1.7739;

            // Set row heights and text properties
            for (int row = 0; row < rowCount; row++)
            {
              for (int col = 0; col < 2; col++)
              {
                Cell cell = table.Cells[row, col];
                cell.TextHeight = 0.09375000;
                cell.TextStyleId = CreateOrGetTextStyle(db, tr, "gmep");
              }

              if (row == 0)
              {
                table.Rows[row].Height = 0.6413;  // Header
                table.Cells[row, 0].TextHeight = 0.1875;
              }
              else if (row >= 1 && row <= 4)
              {
                table.Rows[row].Height = 0.2500;
              }
              else
              {
                table.Rows[row].Height = 0.4955;
              }
            }

            // Populate the table
            table.Cells[0, 0].TextString = $"{data.SectionHeader1.ToUpper()}";
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
            table.Cells[1, 1].TextString = "COLD WATER";
            table.Cells[1, 1].Alignment = CellAlignment.MiddleCenter;

            table.MergeCells(CellRange.Create(table, 1, 1, 4, 1));

            table.Cells[1, 0].TextString = $"STREET PRESSURE: {data.StreetLowPressure}PSI*";
            table.Cells[1, 0].Alignment = CellAlignment.MiddleLeft;

            table.Cells[2, 0].TextString = $"METER SIZE: {data.MeterSize} {(data.ExistingMeter ? "\" EXISTING METER" : "NEW METER")}";
            table.Cells[2, 0].Alignment = CellAlignment.MiddleLeft;

            table.Cells[3, 0].TextString = $"PIPE MATERIAL: {data.PipeMaterial.ToUpper()}";
            table.Cells[3, 0].Alignment = CellAlignment.MiddleLeft;

            table.Cells[4, 0].TextString = $"COLD WATER MAX. VEL.= {data.ColdWaterMaxVelocity} FPS, HOT WATER MAX. VEL.={data.HotWaterMaxVelocity}FPS";
            table.Cells[4, 0].Alignment = CellAlignment.MiddleLeft;

            // Define the dynamic rows
            List<(string Description, string Value)> rows = new List<(string, string)>
        {
            ($"1. {data.MeterSize}\" METER LOSS, PSI", $"{data.MeterLoss:F1}"),
            ($"2. {data.Elevation}FT STATIC LOSS, PSI", $"{data.StaticLoss:F1}")
        };

            // Add backflow preventer loss if non-zero
            if (data.BackflowPressureLoss > 0)
            {
              rows.Add(($"3. BACKFLOW PREVENTER LOSS, PSI", $"{data.BackflowPressureLoss:F1}"));
            }

            // Add PRV loss if non-zero
            if (data.PrvPressureLoss > 0)
            {
              rows.Add(($"{rows.Count + 1}. PRV LOSS, PSI", $"{data.PrvPressureLoss:F1}"));
            }

            // Add additional losses
            int itemNumber = rows.Count + 1;
            foreach (var loss in data.AdditionalLosses)
            {
              rows.Add(($"{itemNumber}. {loss.Title}, PSI", loss.Amount));
              itemNumber++;
            }

            rows.Add(($"{itemNumber}. MINIMUM PRESSURE REQUIRED, PSI", $"{data.PressureRequiredOrAtUnit:F1}"));
            itemNumber++;

            // Add remaining rows
            rows.AddRange(new List<(string, string)>
            {
                ($"{itemNumber}. TOTAL LOSSES, PSI (1 THRU {itemNumber - 1})", $"{data.TotalLoss:F1}"),
                ($"{itemNumber + 1}. WATER PRESSURE (MIN), PSI", $"{data.StreetLowPressure:F1}"),
                ($"{itemNumber + 2}. PRESSURE AVAILABLE FOR FRICTION, PSI", $"{data.PressureAvailable:F1}"),
                ($"{itemNumber + 3}. ACTUAL LENGTH OF SYSTEM, FT", $"{data.SystemLength:F1}"),
                ($"{itemNumber + 4}. DEVELOPED LENGTH (130% OF ITEM {itemNumber + 3})", $"{data.DevelopedLength:F1}"),
                ($"{itemNumber + 5}. AVERAGE PRESSURE DROP, PSI/100FT", $"{data.AveragePressureDrop:F1}")
            });

            //Populate the table with the rows
            for (int i = 0; i < rows.Count; i++)
            {
              int rowIndex = i + 5;
              table.Cells[rowIndex, 0].TextString = rows[i].Description;
              table.Cells[rowIndex, 0].Alignment = CellAlignment.MiddleLeft;
              table.Cells[rowIndex, 1].TextString = rows[i].Value;
              table.Cells[rowIndex, 1].Alignment = CellAlignment.MiddleCenter;
            }

            // Add note at the bottom
            table.InsertRows(rowCount, 0.5257, 1);
            table.Cells[rowCount, 0].TextString = "NOTE: *IF STREET PRESSURE EXCEEDS 80 PSI, A PRESSURE REDUCING VALVE IS TO BE INSTALLED TO REDUCE THE PRESSURE TO 80PSI.";
            table.Cells[rowCount, 0].Alignment = CellAlignment.MiddleLeft;
            table.MergeCells(CellRange.Create(table, rowCount, 0, rowCount, 1));
            table.Cells[rowCount, 0].TextStyleId = CreateOrGetTextStyle(db, tr, "A2");
            table.Cells[rowCount, 0].TextHeight = 0.09375000;

            currentSpace.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            tr.Commit();
            ed.WriteMessage("\nCommercial water calculation table created successfully.");
          }
          catch (System.Exception ex)
          {
            ed.WriteMessage($"\nError creating commercial water calculation table: {ex.Message}");
            tr.Abort();
          }
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