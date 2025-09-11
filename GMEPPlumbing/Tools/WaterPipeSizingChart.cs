using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Tools
{

  public class WaterPipeSizingChart {
    public List<WaterPipeSizingEntry> PEXChart { get; set; } = new List<WaterPipeSizingEntry>() {
      //Pex PSI 1
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, HotGPM = 1.9, ColdGPM = 1.7, PipeSize = "3/4\"" },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, HotGPM = 3.8, ColdGPM = 3.3, PipeSize = "1\"" },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, HotGPM = 6.5, ColdGPM = 5.7, PipeSize = "1-1/4\"" },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, HotGPM = 10.2, ColdGPM = 9.1, PipeSize = "1-1/2\"" },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, HotGPM = 20.8, ColdGPM = 18.8, PipeSize = "2\"" },

       //Pex PSI 1.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1/2\"", ColdGPM=0.8, HotGPM=0.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "3/4\"", ColdGPM=2.1, HotGPM=2.4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1\"", ColdGPM=4.2, HotGPM=4.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1-1/4\"", ColdGPM=7.3, HotGPM=8.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1-1/2\"", ColdGPM=11.4, HotGPM=12.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "2\"", ColdGPM=24, HotGPM=26 },

       //Pex PSI 2
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1/2\"", ColdGPM=0.9, HotGPM=1.1},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "3/4\"", ColdGPM=2.5, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1\"", ColdGPM=5.1, HotGPM=5.4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1-1/4\"", ColdGPM=8.7, HotGPM=9.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1-1/2\"", ColdGPM=13.6, HotGPM=14.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "2\"", ColdGPM=27.9, HotGPM=30.5 },

      //Pex PSI 2.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1/2\"", ColdGPM=1.1, HotGPM=1.2},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "3/4\"", ColdGPM=2.8, HotGPM=3.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1\"", ColdGPM=5.6, HotGPM=6.3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1-1/4\"", ColdGPM=9.7, HotGPM=10.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1-1/2\"", ColdGPM=15.5, HotGPM=17.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "2\"", ColdGPM=31.8, HotGPM=32.4 },

      //Pex PSI 3
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1/2\"", ColdGPM=1.2, HotGPM=1.1},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "3/4\"", ColdGPM=3.2, HotGPM=2.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1\"", ColdGPM=6.3, HotGPM=3.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1-1/4\"", ColdGPM=10.8, HotGPM=5.4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1-1/2\"", ColdGPM=17.0, HotGPM=7.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "2\"", ColdGPM=35.7, HotGPM=12.9 },

      //Pex PSI 3.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1/2\"", ColdGPM=1.3, HotGPM=1.4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "3/4\"", ColdGPM=3.5, HotGPM=3.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1\"", ColdGPM=6.9, HotGPM=7.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1-1/4\"", ColdGPM=11.9, HotGPM=13 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1-1/2\"", ColdGPM=18.5, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "2\"", ColdGPM=38.9, HotGPM=32.4 },

      //Pex PSI 4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1/2\"", ColdGPM=1.4, HotGPM=1.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "3/4\"", ColdGPM=3.7, HotGPM=4.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1\"", ColdGPM=7.4, HotGPM=8.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1-1/4\"", ColdGPM=12.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1-1/2\"", ColdGPM=20.0, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "2\"", ColdGPM=41.5, HotGPM=32.4},

      //Pex PSI 4.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1/2\"", ColdGPM=1.6, HotGPM=1.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "3/4\"", ColdGPM=4.0, HotGPM=4.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1\"", ColdGPM=8.0, HotGPM=8.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1-1/4\"", ColdGPM=13.8, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1-1/2\"", ColdGPM=21.6, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "2\"", ColdGPM=44.8, HotGPM=32.4},

      //Pex PSI 5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1/2\"", ColdGPM=1.6, HotGPM=1.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "3/4\"", ColdGPM=4.3, HotGPM=4.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1\"", ColdGPM=8.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1-1/4\"", ColdGPM=14.6, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1-1/2\"", ColdGPM=22.7, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "2\"", ColdGPM=47.4, HotGPM=32.4},

      //Pex PSI 5.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1/2\"", ColdGPM=1.7, HotGPM=1.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "3/4\"", ColdGPM=4.5, HotGPM=5.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1\"", ColdGPM=8.9, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1-1/4\"", ColdGPM=15.5, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1-1/2\"", ColdGPM=24.2, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "2\"", ColdGPM=50, HotGPM=32.4},

      //Pex PSI 6
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1/2\"", ColdGPM=1.8, HotGPM=2.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "3/4\"", ColdGPM=4.7, HotGPM=5.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1\"", ColdGPM=9.4, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1-1/4\"", ColdGPM=16.3, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1-1/2\"", ColdGPM=25.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 6.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1/2\"", ColdGPM=1.9, HotGPM=2.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "3/4\"", ColdGPM=4.9, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1\"", ColdGPM=9.8, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1-1/4\"", ColdGPM=17.1, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1-1/2\"", ColdGPM=26.5, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 7
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1/2\"", ColdGPM=2, HotGPM=2.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "3/4\"", ColdGPM=5.1, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1\"", ColdGPM=10.3, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1-1/4\"", ColdGPM=17.6, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1-1/2\"", ColdGPM=27.6, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 7.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1/2\"", ColdGPM=2.1, HotGPM=2.3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "3/4\"", ColdGPM=5.4, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1\"", ColdGPM=10.7, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1-1/4\"", ColdGPM=18.4, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1-1/2\"", ColdGPM=28.7, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 8
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1/2\"", ColdGPM=2.21, HotGPM=2.4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "3/4\"", ColdGPM=5.6, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1\"", ColdGPM=11.1, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1-1/4\"", ColdGPM=19, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 8.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1/2\"", ColdGPM=2.2, HotGPM=2.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "3/4\"", ColdGPM=5.8, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1\"", ColdGPM=11.4, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1-1/4\"", ColdGPM=19.8, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 9
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1/2\"", ColdGPM=2.3, HotGPM=2.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "3/4\"", ColdGPM=6, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1\"", ColdGPM=11.8, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1-1/4\"", ColdGPM=20.4, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 9.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1/2\"", ColdGPM=2.4, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "3/4\"", ColdGPM=6.2, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1\"", ColdGPM=12.1, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1-1/4\"", ColdGPM=20.9, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 10
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1/2\"", ColdGPM=2.4, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "3/4\"", ColdGPM=6.3, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1\"", ColdGPM=12.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 11
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1/2\"", ColdGPM=2.6, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "3/4\"", ColdGPM=6.7, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1\"", ColdGPM=13.2, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 12
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1/2\"", ColdGPM=2.7, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "3/4\"", ColdGPM=7.1, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1\"", ColdGPM=14.0, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 13
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1/2\"", ColdGPM=2.9, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "3/4\"", ColdGPM=7.5, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1\"", ColdGPM=14.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 14
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1/2\"", ColdGPM=3.0, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "3/4\"", ColdGPM=7.8, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1\"", ColdGPM=14.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 15
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1/2\"", ColdGPM=3.1, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "3/4\"", ColdGPM=8.0, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1\"", ColdGPM=14.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 16
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1/2\"", ColdGPM=3.3, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "3/4\"", ColdGPM=8.3, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1\"", ColdGPM=14.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 17
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1/2\"", ColdGPM=3.4, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "3/4\"", ColdGPM=8.8, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1\"", ColdGPM=14.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 18
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1/2\"", ColdGPM=3.5, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "3/4\"", ColdGPM=8.8, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1\"", ColdGPM=14.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 19
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1/2\"", ColdGPM=3.65, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "3/4\"", ColdGPM=8.82, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1\"", ColdGPM=14.55, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1-1/4\"", ColdGPM=21.76, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},

      //Pex PSI 20
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1/2\"", ColdGPM=3.7, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "3/4\"", ColdGPM=8.8, HotGPM=5.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1\"", ColdGPM=14.5, HotGPM=9.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1-1/4\"", ColdGPM=21.7, HotGPM=13.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1-1/2\"", ColdGPM=30.3, HotGPM=18.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "2\"", ColdGPM=51.9, HotGPM=32.4},
    };
    public List<WaterPipeSizingEntry> CPVCSDRIIChart { get; set; } = new List<WaterPipeSizingEntry>() {

      //CPVC SDR II PSI 1
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=0.9, HotGPM=0.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=2.1, HotGPM=2.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=4.2, HotGPM=4.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=7.1, HotGPM=7.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=11.1, HotGPM=11.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=22.7, HotGPM=22.7},

      //CPVC SDR II PSI 1.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=1.1, HotGPM=1.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=2.6, HotGPM=2.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=5.2, HotGPM=5.2},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=8.9, HotGPM=8.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=13.9, HotGPM=13.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=28.3, HotGPM=28.3},

      //CPVC SDR II PSI 2.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=1.3, HotGPM=1.3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=3.1, HotGPM=3.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=6.1, HotGPM=6.1},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=10.4, HotGPM=10.4},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=16.2, HotGPM=16.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=33.0, HotGPM=33.0},

      //CPVC SDR II PSI 2.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=1.4, HotGPM=1.4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=3.5, HotGPM=3.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=6.9, HotGPM=6.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=11.7, HotGPM=11.7},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=18.3, HotGPM=18.3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=37.2, HotGPM=36.0},

      //CPVC SDR II PSI 3.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=1.6, HotGPM=1.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=3.8, HotGPM=3.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=7.6, HotGPM=7.6},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=12.9, HotGPM=12.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=20.2, HotGPM=20.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=41.1, HotGPM=36.0},

      
      //CPVC SDR II PSI 3.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=1.7, HotGPM=1.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=4.2, HotGPM=4.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=8.2, HotGPM=8.2},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=14.0, HotGPM=14.0},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=21.9, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=44.7, HotGPM=36.0},

      //CPVC SDR II PSI 4.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=1.8, HotGPM=1.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=4.5, HotGPM=4.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=8.8, HotGPM=8.8},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=15.1, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=23.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=48.0, HotGPM=36.0},

      //CPVC SDR II PSI 4.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.0, HotGPM=2.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=4.8, HotGPM=4.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=9.4, HotGPM=9.4},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=16.1, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=25.1, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=51.1, HotGPM=36.0},

      //CPVC SDR II PSI 5.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.1, HotGPM=1.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=5, HotGPM=3.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=10, HotGPM=6.3},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=17, HotGPM=10.8},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=26.6, HotGPM=17.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=54.1, HotGPM=32.4},

      //CPVC SDR II PSI 5.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.2, HotGPM=2.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=5.3, HotGPM=5.3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=10.5, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=17.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=28.0, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.0, HotGPM=36.0},

      //CPVC SDR II PSI 6.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.3, HotGPM=2.3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=5.6, HotGPM=5.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=11.0, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=18.8, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=29.3, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 6.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.4, HotGPM=2.4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=5.8, HotGPM=5.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=11.5, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=19.6, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=30.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 7
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.5, HotGPM=2.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=6.0, HotGPM=5.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=11.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=20.4, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=31.9, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      
      //CPVC SDR II PSI 7.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.6, HotGPM=2.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=6.3, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=12.4, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=21.2, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.1, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 8
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.7, HotGPM=2.7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=6.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=12.8, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=22.0, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 8.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.8, HotGPM=2.8 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=6.7, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=13.3, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=22.7, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 9.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.9, HotGPM=2.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=6.9, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=13.7, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.4, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 9.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=2.9, HotGPM=2.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=7.1, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=14.1, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 10.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.0, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=7.3, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=14.5, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 10.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.1, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=7.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=14.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 11.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.2, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=7.7, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.3, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 11.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.3, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=7.9, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.6, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 11.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 12.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.3, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=8.1, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.96, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 12.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.4, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=8.3, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 12.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 13.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.5, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=8.4, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 13.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.6, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=8.6, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 13.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 14.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.6, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=8.8, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 14.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.7, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.0, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 14.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 15.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.8, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.1, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 15.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.8, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.3, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 15.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 16.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=3.9, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.4, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},
      
      //CPVC SDR II PSI 16.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.0, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 16.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 17.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.0, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

       //CPVC SDR II PSI 17.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.1, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 17.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 18.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.1, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},
        
      //CPVC SDR II PSI 18.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.2, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 18.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 19.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.3, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 19.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19.5, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.3, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19.5, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19.5, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19.5, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19.5, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 19.5, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},

      //CPVC SDR II PSI 19.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1/2\"", InnerDiameter="0.496\"", ColdGPM=4.4, HotGPM=3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "3/4\"", InnerDiameter="0.695\"", ColdGPM=9.5, HotGPM=5.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1\"", InnerDiameter="0.901\"", ColdGPM=15.9, HotGPM=9.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1-1/4\"", InnerDiameter="1.105\"", ColdGPM=23.9, HotGPM=14.9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "1-1/2\"", InnerDiameter="1.309\"", ColdGPM=33.6, HotGPM=21.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 20, PipeSize = "2\"", InnerDiameter="1.716\"", ColdGPM=57.7, HotGPM=36.0},
    };
    public List<WaterPipeSizingEntry> CPVCSCH80Chart { get; set; } = new List<WaterPipeSizingEntry>() {
      //CPVC SCH80 PSI 1
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "2-1/2\"", InnerDiameter="2.323\"", ColdGPM=50.3, HotGPM=50.3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "3\"", InnerDiameter="2.9\"", ColdGPM=90.1, HotGPM=90.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "4\"", InnerDiameter="3.826\"", ColdGPM=186.6, HotGPM=179.2},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "5\"", InnerDiameter="4.813\"", ColdGPM=341.0, HotGPM=283.5},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "6\"", InnerDiameter="5.761\"", ColdGPM=546.9, HotGPM=406.2 },

      //CPVC SCH80 PSI 1.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "2-1/2\"", InnerDiameter="2.323\"", ColdGPM=62.6, HotGPM=62.6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "3\"", InnerDiameter="2.9\"", ColdGPM=112.2, HotGPM=102.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "4\"", InnerDiameter="3.826\"", ColdGPM=232.3, HotGPM=179.2},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "5\"", InnerDiameter="4.813\"", ColdGPM=424.5, HotGPM=283.5},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.5, PipeSize = "6\"", InnerDiameter="5.761\"", ColdGPM=650.0, HotGPM=406.2 },

      //CPVC SCH80 PSI 2.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "2-1/2\"", InnerDiameter="2.323\"", ColdGPM=73.1, HotGPM=66.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "3\"", InnerDiameter="2.9\"", ColdGPM=131.0, HotGPM=102.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "4\"", InnerDiameter="3.826\"", ColdGPM=271.3, HotGPM=179.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "5\"", InnerDiameter="4.813\"", ColdGPM=453.7, HotGPM=283.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "6\"", InnerDiameter="5.761\"", ColdGPM=650.0, HotGPM=406.2 },

      //CPVC SCH80 PSI 2.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "2-1/2\"", InnerDiameter="2.323\"", ColdGPM=82.5, HotGPM=66.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "3\"", InnerDiameter="2.9\"", ColdGPM=147.8, HotGPM=102.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "4\"", InnerDiameter="3.826\"", ColdGPM=286.7, HotGPM=179.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "5\"", InnerDiameter="4.813\"", ColdGPM=453.7, HotGPM=283.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.5, PipeSize = "6\"", InnerDiameter="5.761\"", ColdGPM=650.0, HotGPM=406.2 },
      
      //CPVC SCH80 PSI 3.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "2-1/2\"", InnerDiameter="2.323\"", ColdGPM=91.0, HotGPM=66.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "3\"", InnerDiameter="2.9\"", ColdGPM=163.1, HotGPM=102.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "4\"", InnerDiameter="3.826\"", ColdGPM=286.7, HotGPM=179.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "5\"", InnerDiameter="4.813\"", ColdGPM=453.7, HotGPM=283.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "6\"", InnerDiameter="5.761\"", ColdGPM=650.0, HotGPM=406.2 },

      
      //CPVC SCH80 PSI 3.5
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "2-1/2\"", InnerDiameter="2.323\"", ColdGPM=99.0, HotGPM=66.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "3\"", InnerDiameter="2.9\"", ColdGPM=164.1, HotGPM=102.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "4\"", InnerDiameter="3.826\"", ColdGPM=286.7, HotGPM=179.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "5\"", InnerDiameter="4.813\"", ColdGPM=453.7, HotGPM=283.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.5, PipeSize = "6\"", InnerDiameter="5.761\"", ColdGPM=650.0, HotGPM=406.2 },

      
      //CPVC SCH80 PSI 4.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "2-1/2\"", InnerDiameter="2.323\"", ColdGPM=105.7, HotGPM=66.1 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "3\"", InnerDiameter="2.9\"", ColdGPM=164.7, HotGPM=102.9 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "4\"", InnerDiameter="3.826\"", ColdGPM=286.7, HotGPM=179.2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "5\"", InnerDiameter="4.813\"", ColdGPM=453.7, HotGPM=283.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "6\"", InnerDiameter="5.761\"", ColdGPM=650.0, HotGPM=406.2 },
    };
    public List<WaterPipeSizingEntry> CopperTypeLChart { get; set; } = new List<WaterPipeSizingEntry>() { 
      //Copper Type L PSI 1
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "3/4\"", ColdGPM = 2.5, HotGPM = 2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "1\"", ColdGPM = 5, HotGPM = 5},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "1-1/4\"", ColdGPM = 9, HotGPM = 9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "1-1/2\"", ColdGPM = 15, HotGPM = 15 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "2\"", ColdGPM = 31, HotGPM = 31 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "2-1/2\"", ColdGPM = 55, HotGPM = 55 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "3\"", ColdGPM = 88, HotGPM = 88 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "3-1/2\"", ColdGPM = 130, HotGPM = 130 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1, PipeSize = "4\"", ColdGPM = 185, HotGPM = 185 },

      //Copper Type L PSI 1.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "3/4\"", ColdGPM = 3, HotGPM = 3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "1\"", ColdGPM = 6, HotGPM = 6},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "1-1/4\"", ColdGPM = 11, HotGPM = 11},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "1-1/2\"", ColdGPM = 18, HotGPM = 18 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "2\"", ColdGPM = 37, HotGPM = 37 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "2-1/2\"", ColdGPM = 66, HotGPM = 66 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "3\"", ColdGPM = 105, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "3-1/2\"", ColdGPM = 155, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 1.4, PipeSize = "4\"", ColdGPM = 220, HotGPM = 185 },

      //Copper Type L PSI 2.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "3/4\"", ColdGPM = 3, HotGPM = 3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1\"", ColdGPM = 7, HotGPM = 7},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1-1/4\"", ColdGPM = 13, HotGPM = 13},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "1-1/2\"", ColdGPM = 21, HotGPM = 21 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "2\"", ColdGPM = 45, HotGPM = 45 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "2-1/2\"", ColdGPM = 80, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "3\"", ColdGPM = 125, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "3-1/2\"", ColdGPM = 190, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2, PipeSize = "4\"", ColdGPM = 260, HotGPM = 185 },

      //Copper Type L PSI 2.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "3/4\"", ColdGPM = 4, HotGPM = 4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "1\"", ColdGPM = 8, HotGPM = 13},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "1-1/4\"", ColdGPM = 15, HotGPM = 15},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "1-1/2\"", ColdGPM = 24, HotGPM = 24 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "2\"", ColdGPM = 50, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "2-1/2\"", ColdGPM = 88, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "3\"", ColdGPM = 140, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "3-1/2\"", ColdGPM = 210, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 2.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

       //Copper Type L PSI 3.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "3/4\"", ColdGPM = 4, HotGPM = 4 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1\"", ColdGPM = 9, HotGPM = 9},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1-1/4\"", ColdGPM = 17, HotGPM = 17},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "1-1/2\"", ColdGPM = 27, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "2\"", ColdGPM = 56, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "2-1/2\"", ColdGPM = 100, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "3\"", ColdGPM = 155, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

       //Copper Type L PSI 3.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "1/2\"", ColdGPM = 2, HotGPM = 2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "3/4\"", ColdGPM = 5, HotGPM = 5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "1\"", ColdGPM = 10, HotGPM = 10},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "1-1/4\"", ColdGPM = 18, HotGPM = 18},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "1-1/2\"", ColdGPM = 29, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "2\"", ColdGPM = 60, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "2-1/2\"", ColdGPM = 105, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 3.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      
       //Copper Type L PSI 4.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1/2\"", ColdGPM = 2, HotGPM = 2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "3/4\"", ColdGPM = 5, HotGPM = 5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1\"", ColdGPM = 11, HotGPM = 11},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1-1/4\"", ColdGPM = 20, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "1-1/2\"", ColdGPM = 31, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "2\"", ColdGPM = 66, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      
      //Copper Type L PSI 4.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "1/2\"", ColdGPM = 2, HotGPM = 2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "3/4\"", ColdGPM = 6, HotGPM = 6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "1\"", ColdGPM = 12, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "1-1/4\"", ColdGPM = 21, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "1-1/2\"", ColdGPM = 33, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "2\"", ColdGPM = 68, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 4.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 5.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1/2\"", ColdGPM = 2, HotGPM = 2 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "3/4\"", ColdGPM = 6, HotGPM = 6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1\"", ColdGPM = 13, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1-1/4\"", ColdGPM = 22, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "1-1/2\"", ColdGPM = 35, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "2\"", ColdGPM = 74, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 5.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "1/2\"", ColdGPM = 2.5, HotGPM = 2.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "3/4\"", ColdGPM = 6, HotGPM = 6 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "1\"", ColdGPM = 13, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "1-1/4\"", ColdGPM = 23, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "1-1/2\"", ColdGPM = 37, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 5.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 6.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1/2\"", ColdGPM = 2.5, HotGPM = 2.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "3/4\"", ColdGPM = 7, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1\"", ColdGPM = 14, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1-1/4\"", ColdGPM = 25, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "1-1/2\"", ColdGPM = 39, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 6.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "1/2\"", ColdGPM = 2.5, HotGPM = 2.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "3/4\"", ColdGPM = 7, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "1\"", ColdGPM = 14, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "1-1/4\"", ColdGPM = 26, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "1-1/2\"", ColdGPM = 41, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 6.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 7.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1/2\"", ColdGPM = 2.5, HotGPM = 2.5 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "3/4\"", ColdGPM = 7, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1\"", ColdGPM = 15, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1-1/4\"", ColdGPM = 27, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "1-1/2\"", ColdGPM = 43, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 7.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "1/2\"", ColdGPM = 3.0, HotGPM = 3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "3/4\"", ColdGPM = 8, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "1\"", ColdGPM = 16, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "1-1/4\"", ColdGPM = 28, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "1-1/2\"", ColdGPM = 44, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 7.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 8.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1/2\"", ColdGPM = 3.0, HotGPM = 3.0 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "3/4\"", ColdGPM = 8.0, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1\"", ColdGPM = 16, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1-1/4\"", ColdGPM = 29, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "1-1/2\"", ColdGPM = 44, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 8.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "1/2\"", ColdGPM = 3, HotGPM = 3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "3/4\"", ColdGPM = 8, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "1\"", ColdGPM = 17, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "1-1/4\"", ColdGPM = 30, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "1-1/2\"", ColdGPM = 44, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 8.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 9.0
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1/2\"", ColdGPM = 3, HotGPM = 3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "3/4\"", ColdGPM = 8, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1\"", ColdGPM = 17, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1-1/4\"", ColdGPM = 31, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "1-1/2\"", ColdGPM = 44, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 9.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "1/2\"", ColdGPM = 3, HotGPM = 3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "3/4\"", ColdGPM = 9, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "1\"", ColdGPM = 18, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "1-1/4\"", ColdGPM = 31, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "1-1/2\"", ColdGPM = 44, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 9.4, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },

      //Copper Type L PSI 9.4
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1/2\"", ColdGPM = 3, HotGPM = 3 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "3/4\"", ColdGPM = 9, HotGPM = 7 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1\"", ColdGPM = 19, HotGPM = 12},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1-1/4\"", ColdGPM = 31, HotGPM = 19},
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "1-1/2\"", ColdGPM = 44, HotGPM = 27 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "2\"", ColdGPM = 76, HotGPM = 48 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "2-1/2\"", ColdGPM = 115, HotGPM = 74 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "3\"", ColdGPM = 165, HotGPM = 105 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "3-1/2\"", ColdGPM = 220, HotGPM = 140 },
      new WaterPipeSizingEntry { PressureLossPer100Ft = 10, PipeSize = "4\"", ColdGPM = 290, HotGPM = 185 },


    };

    public string FindSize(string pipeType, double psi, bool isHot, double gpm) {
      List<WaterPipeSizingEntry> searchChart = null;
      if (pipeType == "Copper Type L") {
        searchChart = CopperTypeLChart;
      }
      else if (pipeType == "PEX") {
        searchChart = PEXChart;
      }
      else if (pipeType == "CPVC SCH80") {
        searchChart = CPVCSCH80Chart;
      }
      else if (pipeType == "CPVC SDR II") {
        searchChart = CPVCSDRIIChart;
      }
      double selectedPSI = searchChart.Where(e => e.PressureLossPer100Ft <= psi).OrderByDescending(e => e.PressureLossPer100Ft).Select(e => e.PressureLossPer100Ft).FirstOrDefault();

      WaterPipeSizingEntry result = null;
      if (isHot) {
        result = searchChart
            .Where(e => e.HotGPM >= gpm && e.PressureLossPer100Ft == selectedPSI)
            .OrderBy(e => e.PressureLossPer100Ft)
            .FirstOrDefault();
      }
      else {
        result = searchChart
            .Where(e => e.ColdGPM >= gpm && e.PressureLossPer100Ft == selectedPSI)
            .OrderBy(e => e.PressureLossPer100Ft)
            .FirstOrDefault();
      }
      string pipeSize = "Pipe Size: " + (result?.PipeSize ?? "Not Found") + "\n";
      string innerDiameter = "";
      if (result != null && !string.IsNullOrEmpty(result.InnerDiameter)) {
        innerDiameter = "Inner Diameter: " + result.InnerDiameter + "\n";
      }

      return pipeSize + innerDiameter;
    }
  }
  public class WaterPipeSizingEntry {
    public double PressureLossPer100Ft { get; set; } // e.g., 20.0
    public double HotGPM { get; set; }
    public double ColdGPM { get; set; }
    public string PipeSize { get; set; } // e.g., "1/2\"", "3/4\""
    public string InnerDiameter { get; set; } = "";
  }
}
