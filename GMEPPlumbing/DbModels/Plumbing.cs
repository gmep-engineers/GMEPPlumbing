using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GMEPPlumbing.Tools;

namespace GMEPPlumbing {
  public class PlumbingFixture {
    public string Id;
    public string ProjectId;
    public Point3d Position;
    public double Rotation;
    public int CatalogId;
    public string TypeAbbreviation;
    public int Number;
    public string BasePointId;
    public string BlockName;
    public int FlowTypeId;
    public PlumbingFixture(
      string id,
      string projectId,
      Point3d position,
      double rotation,
      int catalogId,
      string typeAbbreviation,
      int number,
      string basePointId,
      string blockName,
      int flowTypeId
    ) {
      Id = id;
      ProjectId = projectId;
      Position = position;
      Rotation = rotation;
      CatalogId = catalogId;
      TypeAbbreviation = typeAbbreviation;
      Number = number;
      BasePointId = basePointId;
      BlockName = blockName;
      FlowTypeId = flowTypeId;
    }
  }
  
  public class PlumbingFixtureType {
    public int Id;
    public string Name;
    public string Abbreviation;
    public string WaterBlockNames;
    public string WasteBlockNames;


    public PlumbingFixtureType(
      int id,
      string name,
      string abbreviation,
      string waterBlockNames,
      string wasteBlockNames
    ) {
      Id = id;
      Name = name;
      Abbreviation = abbreviation;
      WaterBlockNames = waterBlockNames;
      WasteBlockNames = wasteBlockNames;
    }
  }
  public class PlumbingFullRoute {
    public double Length { get; set; }
    public List<Object> RouteItems { get; set; }
    public int TypeId { get; set; }
  }
  public class PlumbingFixtureCatalogItem {
    public int Id;
    public int TypeId;
    public string Description;
    public string Make;
    public string Model;
    public decimal Trap;
    public decimal Waste;
    public decimal Vent;
    public decimal ColdWater;
    public decimal HotWater;
    public string Remarks;
    public decimal FixtureDemand;
    public decimal HotDemand;
    public int Dfu;
    public string WaterGasBlockName;
    public string WasteVentBlockName;
    public int Cfh;

    public PlumbingFixtureCatalogItem(
      int id,
      int typeId,
      string description,
      string make,
      string model,
      decimal trap,
      decimal waste,
      decimal vent,
      decimal coldWater,
      decimal hotWater,
      string remarks,
      decimal fixtureDemand,
      decimal hotDemand,
      int dfu,
      string waterGasBlockName,
      string wasteVentBlockName,
      int cfh
    ) {
      Id = id;
      TypeId = typeId;
      Description = description;
      Make = make;
      Model = model;
      Trap = trap;
      Waste = waste;
      Vent = vent;
      ColdWater = coldWater;
      HotWater = hotWater;
      Remarks = remarks;
      FixtureDemand = fixtureDemand;
      HotDemand = hotDemand;
      Dfu = dfu;
      WaterGasBlockName = waterGasBlockName;
      WasteVentBlockName = wasteVentBlockName;
      Cfh = cfh;
    }
  }

  public class PlumbingSource {
    public string Id;
    public string ProjectId;
    public Point3d Position;
    public int TypeId;
    public string BasePointId;
    public double Pressure;

    public PlumbingSource(
      string id,
      string projectId,
      Point3d position,
      int typeId,
      string basePointId,
      double pressure
    ) {
      Id = id;
      ProjectId = projectId;
      Position = position;
      TypeId = typeId;
      BasePointId = basePointId;
      Pressure = pressure;
    }
  }

  public class PlumbingSourceType {
    public int Id;
    public string Type;

    public PlumbingSourceType(int id, string type) {
      Id = id;
      Type = type;
    }
  }

  public class PlumbingPlanBasePoint {
    public string Id;
    public string ProjectId;
    public string ViewportId;
    public int Floor;
    public string Plan;
    public string Type;
    public Point3d Point;
    public double FloorHeight;
    public double CeilingHeight;

    public PlumbingPlanBasePoint(string id, string projectId, Point3d point, string plan, string type, string viewportId, int floor, double floorHeight, double ceilingHeight) {
      Id = id;
      ProjectId = projectId;
      ViewportId = viewportId;
      Floor = floor;
      Type = type;
      Plan = plan;
      Point = point;
      FloorHeight = floorHeight;
      CeilingHeight = ceilingHeight;
    }
  }
  public class PlumbingHorizontalRoute {
    public string Id;
    public string ProjectId;
    public string Type;
    public Point3d StartPoint;
    public Point3d EndPoint;
    public string BasePointId;
    public string PipeType;
    public double FixtureUnits { get; set; } = 0;
    public int FlowTypeId { get; set; } = 1;
    public int GPM { get; set; } = 0;
    public double LongestRunLength { get; set; } = 0;
    public string PipeSize { get; set; } = "";
    public PlumbingHorizontalRoute(
      string id,
      string projectId,
      string type,
      Point3d startPoint,
      Point3d endPoint,
      string basePointId,
      string pipeType
    ) {
      Id = id;
      Type = type;
      ProjectId = projectId;
      StartPoint = startPoint;
      EndPoint = endPoint;
      BasePointId = basePointId;
      PipeType = pipeType;
    }
    public void GenerateGallonsPerMinute() {
      // Key: fixture units, Value: gpm
      // All values from the provided charts
      SortedDictionary<int, int> flushTankDict = new SortedDictionary<int, int>
      {
        // Data from Image 1
        {0, 1}, {1, 2}, {3, 3}, {4, 4}, {6, 5}, {7, 6}, {8, 7}, {10, 8}, {12, 9}, {13, 10},
        {15, 11}, {16, 12}, {18, 13}, {20, 14}, {21, 15}, {23, 16}, {24, 17}, {26, 18}, {28, 19},
        {30, 20}, {32, 21}, {34, 22}, {36, 23}, {39, 24}, {42, 25}, {44, 26}, {46, 27}, {49, 28},
        {51, 29}, {54, 30}, {56, 31}, {58, 32}, {60, 33}, {63, 34}, {66, 35}, {69, 36}, {74, 37},
        {78, 38}, {83, 39}, {86, 40}, {90, 41}, {95, 42}, {99, 43}, {103, 44}, {107, 45}, {111, 46},
        {115, 47}, {119, 48}, {123, 49}, {127, 50}, {130, 51}, {135, 52}, {141, 53}, {146, 54},
        {151, 55}, {155, 56}, {160, 57}, {165, 58}, {170, 59}, {175, 60}, {185, 62}, {195, 64},
        {205, 66},

        // Data from Image 2
        {215, 68}, {225, 70}, {236, 72}, {245, 74}, {254, 76}, {264, 78}, {284, 82}, {294, 84},
        {305, 86}, {315, 88}, {326, 90}, {337, 92}, {348, 94}, {359, 96}, {370, 98}, {380, 100},
        {406, 105}, {431, 110}, {455, 115}, {479, 120}, {506, 125}, {533, 130}, {559, 135},
        {585, 140}, {611, 145}, {638, 150}, {665, 155}, {692, 160}, {719, 165}, {748, 170},
        {778, 175}, {809, 180}, {840, 185}, {874, 190}, {945, 200}, {1018, 210}, {1091, 220},
        {1173, 230}, {1254, 240}, {1335, 250}, {1418, 260}, {1500, 270}, {1583, 280}, {1668, 290},
        {1755, 300}, {1845, 310}, {1926, 320}, {2018, 330}, {2110, 340}, {2204, 350}, {2298, 360},
        {2388, 370}, {2480, 380}, {2575, 390}, {2670, 400}, {2765, 410}, {2862, 420}, {2960, 430},
        {3060, 440}, {3150, 450}, {3620, 500}, {4070, 550}, {4480, 600}, {5380, 700}, {6280, 800},
        {7280, 900},

        // Data from Image 3
        {8300, 1000}, {9320, 1100}, {10340, 1200}, {11360, 1300}, {12380, 1400}, {13400, 1500},
        {14420, 1600}, {15440, 1700}, {16460, 1800}, {17480, 1900}, {18500, 2000}, {19520, 2100},
        {20540, 2200}, {21560, 2300}, {22580, 2400}, {23600, 2500}, {24620, 2600}, {25640, 2700}
      };

      SortedDictionary<int, int> flushValveDict = new SortedDictionary<int, int>
      {
        // Data from Image 1
        {6, 23}, {7, 24}, {8, 25}, {9, 26}, {10, 27}, {11, 28}, {12, 29}, {13, 30}, {14, 31},
        {15, 32}, {16, 33}, {18, 34}, {20, 35}, {21, 36}, {23, 37}, {25, 38}, {26, 39}, {28, 40},
        {30, 41}, {31, 42}, {33, 43}, {35, 44}, {37, 45}, {39, 46}, {42, 47}, {44, 48}, {46, 49},
        {48, 50}, {50, 51}, {52, 52}, {54, 53}, {57, 54}, {60, 55}, {63, 56}, {66, 57}, {69, 58},
        {73, 59}, {76, 60}, {82, 62}, {88, 64}, {95, 66},

        // Data from Image 2
        {102, 68}, {108, 70}, {116, 72}, {124, 74}, {132, 76}, {140, 78}, {158, 82}, {168, 84},
        {176, 86}, {186, 88}, {195, 90}, {205, 92}, {214, 94}, {223, 96}, {234, 98}, {245, 100},
        {270, 105}, {295, 110}, {329, 115}, {365, 120}, {396, 125}, {430, 130}, {460, 135},
        {490, 140}, {521, 145}, {559, 150}, {596, 155}, {631, 160}, {666, 165}, {700, 170},
        {739, 175}, {775, 180}, {811, 185}, {850, 190}, {931, 200}, {1009, 210}, {1091, 220},
        {1173, 230}, {1254, 240}, {1335, 250}, {1418, 260}, {1500, 270}, {1583, 280}, {1668, 290},
        {1755, 300}, {1845, 310}, {1926, 320}, {2018, 330}, {2110, 340}, {2204, 350}, {2298, 360},
        {2388, 370}, {2480, 380}, {2575, 390}, {2670, 400}, {2765, 410}, {2862, 420}, {2960, 430},
        {3060, 440}, {3150, 450}, {3620, 500}, {4070, 550}, {4480, 600}, {5380, 700}, {6280, 800},
        {7280, 900},

        // Data from Image 3
        {8300, 1000}, {9320, 1100}, {10340, 1200}, {11360, 1300}, {12380, 1400}, {13400, 1500},
        {14420, 1600}, {15440, 1700}, {16460, 1800}, {17480, 1900}, {18500, 2000}, {19520, 2100},
        {20540, 2200}, {21560, 2300}, {22580, 2400}, {23600, 2500}, {24620, 2600}, {25640, 2700}
      };

      var lookup = FlowTypeId == 1 ? flushTankDict : flushValveDict;

      if (FlowTypeId != 1 && FlowTypeId != 2) { 
        GPM = 0;
        return;
      }

      // Find the minimum gpm for which fixtureUnits <= key
      foreach (var kvp in lookup) {
        if (FixtureUnits <= kvp.Key) {
          GPM = kvp.Value;
          return;
        }
      }
      GPM = lookup.Last().Value;
      return;
    }
  }
  public class PlumbingVerticalRoute {
    public string Id;
    public string ProjectId;
    public string Type;
    public Point3d Position;
    public string VerticalRouteId;
    public string BasePointId;
    public double StartHeight;
    public double Length;
    public int NodeTypeId;
    public string PipeType;
    public bool IsUp;
    public double FixtureUnits { get; set; } = 0;
    public int FlowTypeId { get; set; } = 1;
    public int GPM { get; set; } = 0;
    public double LongestRunLength { get; set; } = 0;
    public string PipeSize { get; set; } = "";

    public PlumbingVerticalRoute(
      string id,
      string projectId,
      string type,
      Point3d position,
      string verticalRouteId,
      string basePointId,
      double startHeight,
      double length,
      int nodeTypeId,
      string pipeType,
      bool isUp
    ) {
      Id = id;
      ProjectId = projectId;
      Position = position;
      VerticalRouteId = verticalRouteId;
      BasePointId = basePointId;
      StartHeight = startHeight;
      Length = length;
      NodeTypeId = nodeTypeId;
      Type = type;
      PipeType = pipeType;
      IsUp = isUp;
    }
    public void GenerateGallonsPerMinute() {
      // Key: fixture units, Value: gpm
      // All values from the provided charts
      SortedDictionary<int, int> flushTankDict = new SortedDictionary<int, int>
      {
        // Data from Image 1
        {0, 1}, {1, 2}, {3, 3}, {4, 4}, {6, 5}, {7, 6}, {8, 7}, {10, 8}, {12, 9}, {13, 10},
        {15, 11}, {16, 12}, {18, 13}, {20, 14}, {21, 15}, {23, 16}, {24, 17}, {26, 18}, {28, 19},
        {30, 20}, {32, 21}, {34, 22}, {36, 23}, {39, 24}, {42, 25}, {44, 26}, {46, 27}, {49, 28},
        {51, 29}, {54, 30}, {56, 31}, {58, 32}, {60, 33}, {63, 34}, {66, 35}, {69, 36}, {74, 37},
        {78, 38}, {83, 39}, {86, 40}, {90, 41}, {95, 42}, {99, 43}, {103, 44}, {107, 45}, {111, 46},
        {115, 47}, {119, 48}, {123, 49}, {127, 50}, {130, 51}, {135, 52}, {141, 53}, {146, 54},
        {151, 55}, {155, 56}, {160, 57}, {165, 58}, {170, 59}, {175, 60}, {185, 62}, {195, 64},
        {205, 66},

        // Data from Image 2
        {215, 68}, {225, 70}, {236, 72}, {245, 74}, {254, 76}, {264, 78}, {284, 82}, {294, 84},
        {305, 86}, {315, 88}, {326, 90}, {337, 92}, {348, 94}, {359, 96}, {370, 98}, {380, 100},
        {406, 105}, {431, 110}, {455, 115}, {479, 120}, {506, 125}, {533, 130}, {559, 135},
        {585, 140}, {611, 145}, {638, 150}, {665, 155}, {692, 160}, {719, 165}, {748, 170},
        {778, 175}, {809, 180}, {840, 185}, {874, 190}, {945, 200}, {1018, 210}, {1091, 220},
        {1173, 230}, {1254, 240}, {1335, 250}, {1418, 260}, {1500, 270}, {1583, 280}, {1668, 290},
        {1755, 300}, {1845, 310}, {1926, 320}, {2018, 330}, {2110, 340}, {2204, 350}, {2298, 360},
        {2388, 370}, {2480, 380}, {2575, 390}, {2670, 400}, {2765, 410}, {2862, 420}, {2960, 430},
        {3060, 440}, {3150, 450}, {3620, 500}, {4070, 550}, {4480, 600}, {5380, 700}, {6280, 800},
        {7280, 900},

        // Data from Image 3
        {8300, 1000}, {9320, 1100}, {10340, 1200}, {11360, 1300}, {12380, 1400}, {13400, 1500},
        {14420, 1600}, {15440, 1700}, {16460, 1800}, {17480, 1900}, {18500, 2000}, {19520, 2100},
        {20540, 2200}, {21560, 2300}, {22580, 2400}, {23600, 2500}, {24620, 2600}, {25640, 2700}
      };

      SortedDictionary<int, int> flushValveDict = new SortedDictionary<int, int>
      {
        // Data from Image 1
        {6, 23}, {7, 24}, {8, 25}, {9, 26}, {10, 27}, {11, 28}, {12, 29}, {13, 30}, {14, 31},
        {15, 32}, {16, 33}, {18, 34}, {20, 35}, {21, 36}, {23, 37}, {25, 38}, {26, 39}, {28, 40},
        {30, 41}, {31, 42}, {33, 43}, {35, 44}, {37, 45}, {39, 46}, {42, 47}, {44, 48}, {46, 49},
        {48, 50}, {50, 51}, {52, 52}, {54, 53}, {57, 54}, {60, 55}, {63, 56}, {66, 57}, {69, 58},
        {73, 59}, {76, 60}, {82, 62}, {88, 64}, {95, 66},

        // Data from Image 2
        {102, 68}, {108, 70}, {116, 72}, {124, 74}, {132, 76}, {140, 78}, {158, 82}, {168, 84},
        {176, 86}, {186, 88}, {195, 90}, {205, 92}, {214, 94}, {223, 96}, {234, 98}, {245, 100},
        {270, 105}, {295, 110}, {329, 115}, {365, 120}, {396, 125}, {430, 130}, {460, 135},
        {490, 140}, {521, 145}, {559, 150}, {596, 155}, {631, 160}, {666, 165}, {700, 170},
        {739, 175}, {775, 180}, {811, 185}, {850, 190}, {931, 200}, {1009, 210}, {1091, 220},
        {1173, 230}, {1254, 240}, {1335, 250}, {1418, 260}, {1500, 270}, {1583, 280}, {1668, 290},
        {1755, 300}, {1845, 310}, {1926, 320}, {2018, 330}, {2110, 340}, {2204, 350}, {2298, 360},
        {2388, 370}, {2480, 380}, {2575, 390}, {2670, 400}, {2765, 410}, {2862, 420}, {2960, 430},
        {3060, 440}, {3150, 450}, {3620, 500}, {4070, 550}, {4480, 600}, {5380, 700}, {6280, 800},
        {7280, 900},

        // Data from Image 3
        {8300, 1000}, {9320, 1100}, {10340, 1200}, {11360, 1300}, {12380, 1400}, {13400, 1500},
        {14420, 1600}, {15440, 1700}, {16460, 1800}, {17480, 1900}, {18500, 2000}, {19520, 2100},
        {20540, 2200}, {21560, 2300}, {22580, 2400}, {23600, 2500}, {24620, 2600}, {25640, 2700}
      };

      var lookup = FlowTypeId == 1 ? flushTankDict : flushValveDict;

      if (FlowTypeId != 1 && FlowTypeId != 2) {
        GPM = 0;
        return;
      }

      // Find the minimum gpm for which fixtureUnits <= key
      foreach (var kvp in lookup) {
        if (FixtureUnits <= kvp.Key) {
          GPM = kvp.Value;
          return;
        }
      }
      GPM = lookup.Last().Value;
      return;
    }
  }
  public class GasCalculator: INotifyPropertyChanged {
    public GasPipeSizingChart _chosenChart;

    public ObservableCollection<MenuItemViewModel> MenuItems { get; set; }
      
    string _description;
    public GasPipeSizingChart ChosenChart {
      get => _chosenChart;
      set { if (_chosenChart != value) { _chosenChart = value; OnPropertyChanged(); } }
    }
    public string Description {
      get => _description;
      set { if (_description != value) { _description = value; OnPropertyChanged(); } }
    }
    public GasCalculator(string description, GasPipeSizingChart chosenChart = null) {
      MenuItems = new ObservableCollection<MenuItemViewModel> {
        new MenuItemViewModel {
          Name = "Natural Gas",
          Children = new ObservableCollection<MenuItemViewModel> {
            new MenuItemViewModel { Name = "Corrugated Stainless Steel Tubing", Children = new ObservableCollection<MenuItemViewModel>()
            {
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Corrugated Stainless Steel Tubing", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Corrugated Stainless Steel Tubing", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Corrugated Stainless Steel Tubing", 3)},
              new MenuItemViewModel { Name = "Chart 4", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Corrugated Stainless Steel Tubing", 4)},
              new MenuItemViewModel { Name = "Chart 5", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Corrugated Stainless Steel Tubing", 5)},
            }
            },
            new MenuItemViewModel { Name = "Semi-Rigid Copper Tubing", Children = new ObservableCollection<MenuItemViewModel>(){
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 3)},
              new MenuItemViewModel { Name = "Chart 4", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 4)},
              new MenuItemViewModel { Name = "Chart 5", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 5)},
              new MenuItemViewModel { Name = "Chart 6", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 6)},
              new MenuItemViewModel { Name = "Chart 7", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Semi-Rigid Copper Tubing", 7)},
            } 
            },
            new MenuItemViewModel { Name = "Polyethylene Plastic Pipe", Children = new ObservableCollection<MenuItemViewModel>(){
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Polyethylene Plastic Pipe", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Polyethylene Plastic Pipe", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Polyethylene Plastic Pipe", 3)},
              new MenuItemViewModel { Name = "Chart 4", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Polyethylene Plastic Pipe", 4)},
              new MenuItemViewModel { Name = "Chart 5", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Polyethylene Plastic Pipe", 5)},
            } 
            },
            new MenuItemViewModel { Name = "Schedule 40 Metallic Pipe", Children = new ObservableCollection<MenuItemViewModel>(){
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Schedule 40 Metallic Pipe", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Schedule 40 Metallic Pipe", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Schedule 40 Metallic Pipe", 3)},
              new MenuItemViewModel { Name = "Chart 4", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Schedule 40 Metallic Pipe", 4)},
              new MenuItemViewModel { Name = "Chart 5", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Schedule 40 Metallic Pipe", 5)},
              new MenuItemViewModel { Name = "Chart 6", Clicked = () => ChosenChart = new GasPipeSizingChart("Natural Gas", "Schedule 40 Metallic Pipe", 6)},
            }
            },
          }
        },
        new MenuItemViewModel {
          Name = "Propane",
          Children = new ObservableCollection<MenuItemViewModel> {
            new MenuItemViewModel { Name = "Corrugated Stainless Steel Tubing", Children = new ObservableCollection<MenuItemViewModel>()
            {
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Corrugated Stainless Steel Tubing", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Corrugated Stainless Steel Tubing", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Corrugated Stainless Steel Tubing", 3)},
            }
            },
            new MenuItemViewModel { Name = "Semi-Rigid Copper Tubing", Children = new ObservableCollection<MenuItemViewModel>(){
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Semi-Rigid Copper Tubing", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Semi-Rigid Copper Tubing", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Semi-Rigid Copper Tubing", 3)},
            }
            },
            new MenuItemViewModel { Name = "Polyethylene Plastic Pipe", Children = new ObservableCollection<MenuItemViewModel>(){
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Polyethylene Plastic Pipe", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Polyethylene Plastic Pipe", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Polyethylene Plastic Pipe", 3)},
            }
            },
            new MenuItemViewModel { Name = "Schedule 40 Metallic Pipe", Children = new ObservableCollection<MenuItemViewModel>(){
              new MenuItemViewModel { Name = "Chart 1", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Schedule 40 Metallic Pipe", 1)},
              new MenuItemViewModel { Name = "Chart 2", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Schedule 40 Metallic Pipe", 2)},
              new MenuItemViewModel { Name = "Chart 3", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Schedule 40 Metallic Pipe", 3)},
              new MenuItemViewModel { Name = "Chart 4", Clicked = () => ChosenChart = new GasPipeSizingChart("Propane", "Schedule 40 Metallic Pipe", 4)},
            }
            },
          }
        }
      };
      Description = description;
      ChosenChart = chosenChart;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
  public class MenuItemViewModel {
    public string Name { get; set; }
    public ObservableCollection<MenuItemViewModel> Children { get; set; }
    public bool HasClicked => Clicked != null;
    public Action Clicked { get; set; } // Add this line

    // Optionally, a method to invoke the click
    public void OnClick() {
      Clicked?.Invoke();
    }
  }
  public class WaterCalculator : INotifyPropertyChanged {
    private string _description;
    private double _minSourcePressure;
    private double _availableFrictionPressure;
    private double _systemLength;
    private double _developedSystemLength;
    private double _averagePressureDrop;

    public string Description {
      get => _description;
      set { if (_description != value) { _description = value; OnPropertyChanged(); } }
    }
    public double MinSourcePressure {
      get => _minSourcePressure;
      set { if (_minSourcePressure != value) { _minSourcePressure = value; OnPropertyChanged(); DeterminePressure(); } }
    }
    public double AvailableFrictionPressure {
      get => _availableFrictionPressure;
      set { if (_availableFrictionPressure != value) { _availableFrictionPressure = value; OnPropertyChanged(); } }
    }
    public double SystemLength {
      get => _systemLength;
      set { if (_systemLength != value) { _systemLength = value; OnPropertyChanged(); } }
    }
    public double DevelopedSystemLength {
      get => _developedSystemLength;
      set { if (_developedSystemLength != value) { _developedSystemLength = value; OnPropertyChanged(); } }
    }
    public double AveragePressureDrop {
      get => _averagePressureDrop;
      set { if (_averagePressureDrop != value) { _averagePressureDrop = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<WaterLoss> Losses { get; } = new ObservableCollection<WaterLoss>();
    public ObservableCollection<WaterAddition> Additions { get; } = new ObservableCollection<WaterAddition>();


    public WaterCalculator(string description,
        double minSourcePressure,
        double availableFrictionPressure,
        double systemLength,
        double developedSystemLength,
        double averagePressureDrop,
        ObservableCollection<WaterLoss> losses,
        ObservableCollection<WaterAddition> additions) {
      Description = description;
      MinSourcePressure = minSourcePressure;
      AvailableFrictionPressure = availableFrictionPressure;
      SystemLength = systemLength;
      DevelopedSystemLength = developedSystemLength;
      AveragePressureDrop = averagePressureDrop;
      if (losses != null)
        foreach (var loss in losses) Losses.Add(loss);
      if (additions != null)
        foreach (var addition in additions) Additions.Add(addition);
      Losses.CollectionChanged += Losses_CollectionChanged;
      Additions.CollectionChanged += Additions_CollectionChanged;
      DeterminePressure();
    }
    public void DeterminePressure() {
      // Calculate the total pressure loss
      double totalLoss = Losses.Sum(l => l.Value);
      double totalAddition = Additions.Sum(a => a.Value);
      double totalPressure = MinSourcePressure + totalAddition - totalLoss;
      AvailableFrictionPressure = totalPressure;
      // Update the average pressure drop
      AveragePressureDrop = (totalPressure / DevelopedSystemLength) * 100;
    }

    private void Losses_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
      if (e.NewItems != null) {
        foreach (WaterLoss item in e.NewItems)
          item.PropertyChanged += LossItem_PropertyChanged;
      }
      if (e.OldItems != null) {
        foreach (WaterLoss item in e.OldItems)
          item.PropertyChanged -= LossItem_PropertyChanged;
        DeterminePressure();
      }
      // Optionally, handle Reset (e.Action == Reset) if you clear the collection
    }

    private void Additions_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
      if (e.NewItems != null) {
        foreach (WaterAddition item in e.NewItems)
          item.PropertyChanged += AdditionItem_PropertyChanged;
      }
      if (e.OldItems != null) {
        foreach (WaterAddition item in e.OldItems)
          item.PropertyChanged -= AdditionItem_PropertyChanged;
        DeterminePressure();
      }
      // Optionally, handle Reset (e.Action == Reset) if you clear the collection
    }
    private void LossItem_PropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof(WaterLoss.Value)) {
        DeterminePressure();
      }
    }

    private void AdditionItem_PropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof(WaterAddition.Value)) {
        DeterminePressure();
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
  public class WaterLoss : INotifyPropertyChanged {
    private string _description = "";
    private double _value = 0.0;

    public string Description {
      get => _description;
      set { if (_description != value) { _description = value; OnPropertyChanged(); } }
    }
    public double Value {
      get => _value;
      set { if (_value != value) { _value = value; OnPropertyChanged(); } }
    }

    public WaterLoss(string description, double value) {
      Description = description;
      Value = value;
    }
    public WaterLoss() { }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }

  public class WaterAddition : INotifyPropertyChanged {
    private string _description = "";
    private double _value = 0.0;

    public string Description {
      get => _description;
      set { if (_description != value) { _description = value; OnPropertyChanged(); } }
    }
    public double Value {
      get => _value;
      set { if (_value != value) { _value = value; OnPropertyChanged(); } }
    }

    public WaterAddition(string description, double value) {
      Description = description;
      Value = value;
    }
    public WaterAddition() { }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
