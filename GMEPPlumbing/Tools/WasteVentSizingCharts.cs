using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Tools {
  class WasteSizingCharts {
    public List<WasteEntry> entries {
      get; set;
    } = new List<WasteEntry>() {
      new WasteEntry(2, 4, 3, 10, "1-1/4\""),
      new WasteEntry(6, 12, 9, 20, "1-1/2\""),
      new WasteEntry(12, 24, 18, 30, "2\""),
      new WasteEntry(20, 40, 30, 40, "2-1/2\""),
      new WasteEntry(32, 64, 48, 50, "3\""),
      new WasteEntry(80, 160, 120, 70, "4\""),
      new WasteEntry(160, 320, 240, 90, "5\""),
      new WasteEntry(320, 640, 480, 110, "6\""),
      new WasteEntry(720, 1440, 1080, 150, "8\""),
      new WasteEntry(1280, 2560, 1920, 170, "10\""),
      new WasteEntry(2000, 4000, 3000, 200, "12\""),
    };
  }
  class VentSizingCharts {
    public List<VentEntry> Entries { get; set; } = new List<VentEntry>() {
      new VentEntry(1, 45, "1-1/4\""),
      new VentEntry(8, 60, "1-1/2\""),
      new VentEntry(24, 120, "2\""),
      new VentEntry(48, 180, "2-1/2\""),
      new VentEntry(84, 212, "3\""),
      new VentEntry(256, 300, "4\""),
      new VentEntry(600, 390, "5\""),
      new VentEntry(1380, 510, "6\""),
      new VentEntry(3600, 750, "8\""),
    };
    public VentSizingCharts() {
      // :3
    }
    public string FindSize(double dfu, double length) {
      string size = "N/A";
      foreach (var entry in Entries) {
        if (length <= entry.MaxLength && dfu <= entry.MaxDFU) {
          size = entry.PipeDiameter;
          break;
        }
      }
      return size;
    }
  }
  class VentEntry {
    public double MaxLength { get; set; }
    public double MaxDFU { get; set; }
    public string PipeDiameter { get; set; }
    public VentEntry(double maxDfu, double maxLength, string pipeDiameter) {
      MaxLength = maxLength;
      MaxDFU = maxDfu;
      PipeDiameter = pipeDiameter;
    }
  }
  class WasteEntry {
    public double VerticalDfu { get; set; }
    public double TwoPercentSlopeDfu { get; set; }
    public double OnePercentSlopeDfu { get; set; }
    public double MaxVerticalLength { get; set; }
    public string PipeDiameter { get; set; }
    public WasteEntry(double verticalDfu, double twoPercentSlopeDfu, double onePercentSlopeDfu, double maxVerticalLength, string pipeDiameter) {
      VerticalDfu = verticalDfu;
      TwoPercentSlopeDfu = twoPercentSlopeDfu;
      OnePercentSlopeDfu = onePercentSlopeDfu;
      MaxVerticalLength = maxVerticalLength;
      PipeDiameter = pipeDiameter;
    }
  }

}
  

