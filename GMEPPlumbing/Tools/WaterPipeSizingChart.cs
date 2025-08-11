using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Tools
{
  
  public class WaterPipeSizingChart {
    public List<WaterPipeSizingEntry> Entries { get; set; } = new List<WaterPipeSizingEntry>() {
      //Pex PSI 1
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1, HotGPM = 1.9, ColdGPM = 1.7, PipeSize = "3/4\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1, HotGPM = 3.8, ColdGPM = 3.3, PipeSize = "1\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1, HotGPM = 6.5, ColdGPM = 5.7, PipeSize = "1-1/4\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1, HotGPM = 10.2, ColdGPM = 9.1, PipeSize = "1-1/2\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1, HotGPM = 20.8, ColdGPM = 18.8, PipeSize = "2\"" },

       //Pex PSI 2
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1.5, HotGPM = 0.9, ColdGPM = 0.8, PipeSize = "1/2\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1.5, HotGPM = 2.4, ColdGPM = 2.1, PipeSize = "3/4\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1.5, HotGPM = 4.7, ColdGPM = 4.2, PipeSize = "1\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1.5, HotGPM = 6.5, ColdGPM = 5.7, PipeSize = "1-1/4\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1.5, HotGPM = 10.2, ColdGPM = 9.1, PipeSize = "1-1/2\"" },
      new WaterPipeSizingEntry { PipeType = "PEX", PressureLossPer100Ft = 1.5, HotGPM = 20.8, ColdGPM = 18.8, PipeSize = "2\"" },
      // Add more entries as needed
    };

    public string FindSize(string pipeType, double psi, bool isHot, double gpm) {
      // Filter entries by pipeType and psi (pressure loss per 100ft)
      var filtered = Entries
          .Where(e => e.PipeType.Equals(pipeType, StringComparison.OrdinalIgnoreCase)
                   && Math.Abs(e.PressureLossPer100Ft - psi) < 0.01);

      // Find the smallest pipe size that meets the GPM requirement
      WaterPipeSizingEntry match = null;
      if (isHot) {
        match = filtered
            .Where(e => e.HotGPM >= gpm)
            .OrderBy(e => e.HotGPM)
            .FirstOrDefault();
      }
      else {
        match = filtered
            .Where(e => e.ColdGPM >= gpm)
            .OrderBy(e => e.ColdGPM)
            .FirstOrDefault();
      }

      return match?.PipeSize ?? "No suitable size found";
    }
  }
  public class WaterPipeSizingEntry {
    public string PipeType { get; set; } // e.g., "PEX"
    public double PressureLossPer100Ft { get; set; } // e.g., 20.0
    public double HotGPM { get; set; }
    public double ColdGPM { get; set; }
    public string PipeSize { get; set; } // e.g., "1/2\"", "3/4\""
  }
}
