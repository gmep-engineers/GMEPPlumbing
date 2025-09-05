using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Tools {
  class WasteSizingCharts {
    public List<WasteEntry> Entries { get; set; } = new List<WasteEntry>() {
      new WasteEntry(1, 1, 0, 45, "1-1/4\""),
      new WasteEntry(2, 1, 0, 65, "1-1/2\""),
      new WasteEntry(16, 8, 0, 85, "2\""),
      new WasteEntry(32, 14, 0, 148, "2-1/2\""),
      new WasteEntry(48, 35, 0, 212, "3\""),
      new WasteEntry(256, 216, 172, 300, "4\""),
      new WasteEntry(600, 428, 542, 390, "5\""),
      new WasteEntry(1380, 720, 576, 510, "6\""),
      new WasteEntry(3600, 2640, 2112, 750, "8\""),
    };
    public WasteSizingCharts() {
      // :3
    }
    public string FindSize(double dfu, string type, double length = 0) {
      string size = "N/A";
      WasteEntry entry = null;
      if (type == "1%") {
        entry = Entries.Where(e => e.OnePercentSlopeDfu >= dfu).OrderBy(e => e.OnePercentSlopeDfu).FirstOrDefault();
      }
      else if (type == "2%") {
        entry = Entries.Where(e => e.TwoPercentSlopeDfu >= dfu).OrderBy(e => e.TwoPercentSlopeDfu).FirstOrDefault();
      }
      else if (type == "Vertical") {
        entry = Entries.Where(e => e.VerticalDfu >= dfu && e.MaxVerticalLength >= length).OrderBy(e => e.VerticalDfu).FirstOrDefault();
      }
     
      if (entry == null) return size;
      return entry.PipeDiameter;
    }
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
      VentEntry entry = Entries.Where(e => e.MaxDFU >= dfu && e.MaxLength >= length).OrderBy(e => e.MaxDFU).FirstOrDefault();
      if (entry == null) return size;
      return entry.PipeDiameter;
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
  

