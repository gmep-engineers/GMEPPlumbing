using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Data;
using CsvHelper;
using System.Globalization;

namespace GMEPPlumbing.Tools {
  public class GasPipeSizingChart {
    public string FilePath { get; set; } = string.Empty;
    public string PipeType { get; set; } = string.Empty;
    public string GasType { get; set; } = string.Empty;
    public int ChartIndex { get; set; } = 0;
    public string Name { get; set; }
    public List<string> DisplayHeaders { get; private set; } = new List<string>();
    public DataTable CsvTable { get; set; } = new DataTable();
    public GasPipeSizingChart(string gasType, string pipeType, int chartIndex, string name) {
      SetChartPath(gasType, pipeType, chartIndex);
      Name = name;
    }
    public void SetChartPath(string gasType, string pipeType, int chartIndex) {
      string directory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

      string filePath = System.IO.Path.Combine(directory, "Charts", "Gas");
      if (gasType == "Natural Gas") {
        filePath = System.IO.Path.Combine(filePath, "NaturalGas");
      }
      else if (gasType == "Propane") {
        filePath = System.IO.Path.Combine(filePath, "Propane");
      }
      if (pipeType == "Semi-Rigid Copper Tubing") {
        filePath = System.IO.Path.Combine(filePath, "Semi-RigidCopper");
      }
      else if (pipeType == "Schedule 40 Metallic Pipe") {
        filePath = System.IO.Path.Combine(filePath, "Schedule40Metal");
      }
      else if (pipeType == "Corrugated Stainless Steel Tubing") {
        filePath = System.IO.Path.Combine(filePath, "CorrugatedStainlessSteel");
      }
      else if (pipeType == "Polyethylene Plastic Pipe") {
        filePath = System.IO.Path.Combine(filePath, "PolyethylenePlastic");
      }
      string chart = "Chart" + chartIndex.ToString() + ".csv";
      filePath = System.IO.Path.Combine(filePath, chart);
      LoadCsv(filePath);

      FilePath = filePath;
      PipeType = pipeType;
      GasType = gasType;
      ChartIndex = chartIndex;
    }
    public GasEntry GetData(double length, double cfh) {
      GasEntry gasEntry = null;
      int startRow = 0;
      if (PipeType == "Semi-Rigid Copper Tubing") {
        startRow = 5;
      }
      else if (PipeType == "Schedule 40 Metallic Pipe") {
        startRow = 3;
      }
      else if (PipeType == "Corrugated Stainless Steel Tubing") {
        startRow = 2;
      }
      else if (PipeType == "Polyethylene Plastic Pipe") {
        startRow = 4;
      }

      var rows = new List<string[]>();
      using (var reader = new StreamReader(FilePath, Encoding.UTF8)) {
        string line;
        int currentRow = 0;
        List<string> headerLines = new List<string>();
        while ((line = reader.ReadLine()) != null) {
          if (currentRow++ < startRow) {
            headerLines.Add(line);
            continue; // Skip header or irrelevant rows
          }

          var values = line.Split(',');
          int lengthVal;
          if (int.TryParse(values[0], out lengthVal)) {
            if (length <= lengthVal) {
              for (int i = 1; i < values.Length; i++) {
                int cfhVal;
                if (int.TryParse(values[i], out cfhVal)) {
                  if (cfh <= cfhVal) {
                    if (PipeType == "Semi-Rigid Copper Tubing") {
                      SemiRigidCopperGasEntry entry = new SemiRigidCopperGasEntry() {
                        NominalKL = headerLines[0].Split(',')[i],
                        NominalACR = headerLines[1].Split(',')[i],
                        OutsideDiameter = headerLines[2].Split(',')[i],
                        InsideDiameter = headerLines[3].Split(',')[i]
                      };
                      gasEntry = entry;
                    }
                    else if (PipeType == "Schedule 40 Metallic Pipe") {
                      Schedule40MetalGasEntry entry = new Schedule40MetalGasEntry() {
                        NominalSize = headerLines[0].Split(',')[i],
                        ActualID = headerLines[1].Split(',')[i]
                      };
                      gasEntry = entry;
                    }
                    else if (PipeType == "Corrugated Stainless Steel Tubing") {
                      StainlessSteelGasEntry entry = new StainlessSteelGasEntry() {
                        FlowDesignation = headerLines[0].Split(',')[i]
                      };
                      gasEntry = entry;
                    }
                    else if (PipeType == "Polyethylene Plastic Pipe") {
                      PolyethylenePlasticGasEntry entry = new PolyethylenePlasticGasEntry() {
                        NominalOD = headerLines[0].Split(',')[i],
                        Designation = headerLines[1].Split(',')[i],
                        ActualID = headerLines[2].Split(',')[i]
                      };
                      gasEntry = entry;
                    }
                  }
                }
              }
            }
          }
        }
      }
      return gasEntry;
    }
    public void LoadCsv(string filePath) {
      CsvTable.Clear();
      CsvTable.Columns.Clear();
      DisplayHeaders.Clear();

      var lines = File.ReadAllLines(filePath);
      if (lines.Length < 4) return;

      var header1 = lines[0].Split(',');
      var header2 = lines[1].Split(',');
      var header3 = lines[2].Split(',');

      int colCount = Math.Max(header1.Length, Math.Max(header2.Length, header3.Length));

      for (int i = 0; i < colCount; i++) {
        string h1 = i < header1.Length ? header1[i] : "";
        string h2 = i < header2.Length ? header2[i] : "";
        string h3 = i < header3.Length ? header3[i] : "";
        string displayHeader = $"{h1}\n{h2}\n{h3}".Trim('\n');
        DisplayHeaders.Add(displayHeader);

        // Safe column name for binding
        string safeName = $"Col{i}";
        CsvTable.Columns.Add(safeName);
      }

      for (int i = 3; i < lines.Length; i++) {
        var row = lines[i].Split(',');
        if (row.Length < colCount)
          row = row.Concat(Enumerable.Repeat("", colCount - row.Length)).ToArray();
        CsvTable.Rows.Add(row);
      }
    }
  }
    public class GasEntry {
    public string Name { get; set; }
  }
  public class StainlessSteelGasEntry : GasEntry {
    public StainlessSteelGasEntry() {
      Name = "Corrugated Stainless Steel Tubing";
    }
    public string FlowDesignation { get; set; }
  }
  public class PolyethylenePlasticGasEntry : GasEntry {
    
    public PolyethylenePlasticGasEntry() {
      Name = "Polyethylene Plastic Pipe";
    }
    public string NominalOD { get; set; }

    public string Designation { get; set; }

    public string ActualID { get; set; }
  }
  public class Schedule40MetalGasEntry : GasEntry {
    public Schedule40MetalGasEntry() {
      Name = "Schedule 40 Metallic Pipe";
    }
    public string NominalSize { get; set; }
    public string ActualID { get; set; }

  }
  public class SemiRigidCopperGasEntry : GasEntry {
    public SemiRigidCopperGasEntry() {
      Name = "Semi-Rigid Copper Tubing";
    }
    public string NominalKL { get; set; }
    public string NominalACR { get; set; }
    public string OutsideDiameter { get; set; }
    public string InsideDiameter { get; set; }
  }
}
