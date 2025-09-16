using GMEPPlumbing.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Autodesk.AutoCAD.Geometry;
using System.Windows.Media.Media3D;
using System.Collections;
using System.Collections.ObjectModel;
using HelixToolkit.Wpf;
using GMEPPlumbing.Tools;
using System.Windows.Input;
using System.ComponentModel;

namespace GMEPPlumbing.Views
{
    /// <summary>
    /// Interaction logic for Routing.xaml
    /// </summary>
  public partial class Routing : UserControl
  {
    public List<View> Views { get; set; } = new List<View>();
    public Routing(Dictionary<string, List<PlumbingFullRoute>> fullRoutes, Dictionary<string, PlumbingPlanBasePoint> basePointLookup)
    {
      GenerateViews(fullRoutes, basePointLookup);
      InitializeComponent();
      DataContext = this;
    }
    public void GenerateViews(Dictionary<string, List<PlumbingFullRoute>> fullRoutes, Dictionary<string, PlumbingPlanBasePoint> basePointLookup) {
      Views.Clear();
      foreach (var routeScene in fullRoutes) {
        View view = new View(routeScene.Value, basePointLookup);
        Views.Add(view);
      }
    }

    private void Button_Click(object sender, RoutedEventArgs e) {
      var item = (sender as Button)?.DataContext as MenuItemViewModel;
      item?.OnClick();
      e.Handled = false; 
    }
  }

  public class Scene {
      public List<object> RouteItems { get; set; } = new List<object>();
      public double Length { get; set; } = 0;
      public ObservableCollection<Visual3D> RouteVisuals { get; set; } = new ObservableCollection<Visual3D>();
      public Dictionary<string, PlumbingPlanBasePoint> BasePoints { get; set; } = new Dictionary<string, PlumbingPlanBasePoint>();
      public HashSet<string> BasePointIds = new HashSet<string>();
      public bool InitialBuild { get; set; } = true;
      //public SolidColorBrush RouteColor { get; set; } = new SolidColorBrush(Color.FromArgb(255, 0, 0, 255)); // Default to blue
    public Scene(PlumbingFullRoute fullRoute, Dictionary<string, PlumbingPlanBasePoint> basePoints) {
          RouteItems = fullRoute.RouteItems;
          Length = fullRoute.Length;
          BasePoints = basePoints;
          /*switch (fullRoute.TypeId) {
            case 1:
              RouteColor = System.Windows.Media.Brushes.Yellow;
              break;
            case 2:
              RouteColor = System.Windows.Media.Brushes.Magenta;
              break;
            case 3:
              RouteColor = System.Windows.Media.Brushes.SteelBlue;
              break;
            case 4:
              RouteColor = System.Windows.Media.Brushes.Magenta;
              break;
          } */
          BuildScene();
      }
    public Scene() {
      
    }
    public void RebuildScene(PlumbingFullRoute fullRoute) {
      RouteItems = fullRoute.RouteItems;
      Length = fullRoute.Length;
      BuildScene();
    }

    public void BuildScene() {
      RouteVisuals.Clear();
      List<TextVisual3D> textVisuals = new List<TextVisual3D>();
      Dictionary<string, List<PlumbingVerticalRoute>> fullVerticalRoutes = new Dictionary<string, List<PlumbingVerticalRoute>>();
      foreach (var item in RouteItems) {
        Visual3D model = null;
         if (item is PlumbingHorizontalRoute horizontalRoute) {
          ModelVisual3D fullModel = new ModelVisual3D();
          var ballModel2 = new SphereVisual3D {
            Center = new Point3D(horizontalRoute.StartPoint.X, horizontalRoute.StartPoint.Y, horizontalRoute.StartPoint.Z),
            Radius = 1,
            Fill = TypeToBrushColor(horizontalRoute.Type)
          };
          var lineModel = new TubeVisual3D {
            Path = new Point3DCollection {
              new Point3D(horizontalRoute.StartPoint.X, horizontalRoute.StartPoint.Y, horizontalRoute.StartPoint.Z),
              new Point3D(horizontalRoute.EndPoint.X, horizontalRoute.EndPoint.Y, horizontalRoute.EndPoint.Z)
            },
            Diameter = 2,
            Fill = TypeToBrushColor(horizontalRoute.Type)
          };
          var ballModel = new SphereVisual3D {
            Center = new Point3D(horizontalRoute.EndPoint.X, horizontalRoute.EndPoint.Y, horizontalRoute.EndPoint.Z),
            Radius = 1,
            Fill = TypeToBrushColor(horizontalRoute.Type)
          };

          var dirX = horizontalRoute.EndPoint.X - horizontalRoute.StartPoint.X;
          var dirY = horizontalRoute.EndPoint.Y - horizontalRoute.StartPoint.Y;
          var dirZ = horizontalRoute.EndPoint.Z - horizontalRoute.StartPoint.Z;
          var direction = new Vector3D(dirX, dirY, dirZ);
          direction.Normalize();

          // Calculate length in feet/inches
          double length = horizontalRoute.StartPoint.DistanceTo(horizontalRoute.EndPoint);
          int feet = (int)(length / 12);
          int inches = (int)Math.Round(length % 12);
          horizontalRoute.GenerateGallonsPerMinute();
          string flow = (horizontalRoute.FlowTypeId == 1) ? "Flush Tank" : "Flush Valve";
          double longestRunLength = horizontalRoute.LongestRunLength;
          int longestRunFeet = (int)(longestRunLength / 12); // Convert to feet
          int longestRunInches = (int)Math.Round(longestRunLength % 12); // Remaining inches

          double textHeight = 8;
          string textString = $" {feet}' {inches}\"\n {flow} \n FU: {horizontalRoute.FixtureUnits} \n GPM: {horizontalRoute.GPM} \n ---------------------- \n {horizontalRoute.PipeSize}\n";
          if (horizontalRoute.Type == "Gas") {
            textString = $" {feet}' {inches}\"\n CFH: {horizontalRoute.FixtureUnits} \n Longest Run: {longestRunFeet}' {longestRunInches}\"\n ---------------------- \n {horizontalRoute.PipeSize}\n";
          }
          else if (horizontalRoute.Type == "Waste" || horizontalRoute.Type == "Grease Waste") {
            WasteSizingChart chart = new WasteSizingChart();
            string slope = "1%";
            if (horizontalRoute.Slope == 0.02) {
              slope = "2%";
            }
            string recommendedSize = chart.FindSize(horizontalRoute.FixtureUnits, slope);
            textString = $" {feet}' {inches}\"\n DFU: {horizontalRoute.FixtureUnits}\n Slope: {slope}\n ---------------------- \n {recommendedSize}\n";
          }
          else if (horizontalRoute.Type == "Vent") {
            VentSizingChart chart = new VentSizingChart();
            string slope = "1%";
            if (horizontalRoute.Slope == 0.02) {
              slope = "2%";
            }
            string recommendedSize = chart.FindSize(horizontalRoute.FixtureUnits, horizontalRoute.LongestRunLength);
            textString = $" {feet}' {inches}\"\n Slope: {slope}\n ---------------------- \n {recommendedSize}\n";
          }
          double textWidth = textHeight * textString.Length * 0.03;

          // Offset so the back of the text aligns with the end point 
          var textPos = new Point3D(
              horizontalRoute.EndPoint.X - (direction.X * (textWidth / 2)),
              horizontalRoute.EndPoint.Y - (direction.Y * (textWidth / 2)),
              horizontalRoute.EndPoint.Z + 6 // +5 for vertical offset
          );

          var textModel = new TextVisual3D {
            Position = textPos,
            Text = textString,
            Height = textHeight,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            UpDirection = new Vector3D(0, 0, 1),
            TextDirection = direction
          };
          TextVisual3DExtensions.SetBasePointId(textModel, horizontalRoute.BasePointId);
          TextVisual3DExtensions.SetType(textModel, horizontalRoute.Type);
          textVisuals.Add(textModel);

          fullModel.Children.Add(ballModel2);
          fullModel.Children.Add(lineModel);
          fullModel.Children.Add(ballModel);

          model = fullModel;
          BasePointIds.Add(horizontalRoute.BasePointId);
        }
        else if (item is PlumbingVerticalRoute verticalRoute) {
          if (!fullVerticalRoutes.ContainsKey(verticalRoute.VerticalRouteId)) {
            fullVerticalRoutes[verticalRoute.VerticalRouteId] = new List<PlumbingVerticalRoute>();
          }
          verticalRoute.GenerateGallonsPerMinute();
          fullVerticalRoutes[verticalRoute.VerticalRouteId].Add(verticalRoute);

          ModelVisual3D fullModel = new ModelVisual3D();
          var ballModel = new SphereVisual3D {
            Center = new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z),
            Radius = 1,
            Fill = TypeToBrushColor(verticalRoute.Type)
          };

          double length = verticalRoute.Length * 12;

          if (verticalRoute.NodeTypeId == 3) {
            length = -length;//
          }
          var tubeModel = new TubeVisual3D {
            Path = new Point3DCollection {
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z),
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z + length)
            },
            Diameter = 2,
            Fill = TypeToBrushColor(verticalRoute.Type)
          };

          fullModel.Children.Add(tubeModel);
          if (verticalRoute.NodeTypeId != 2) {
            fullModel.Children.Add(ballModel);
          }

          model = fullModel;
          BasePointIds.Add(verticalRoute.BasePointId);
        }
        else if (item is PlumbingSource plumbingSource) {
          SolidColorBrush SourceColor = Brushes.Gray; // Default color
          switch (plumbingSource.TypeId) {
            case 1:
              SourceColor = System.Windows.Media.Brushes.Yellow;
              break;
            case 2:
              SourceColor = System.Windows.Media.Brushes.Magenta;
              break;
            case 3:
              SourceColor = System.Windows.Media.Brushes.SteelBlue;
              break;
            case 4:
              SourceColor = System.Windows.Media.Brushes.Magenta;
              break;
          }
          model = new SphereVisual3D {
            Center = new Point3D(plumbingSource.Position.X, plumbingSource.Position.Y, plumbingSource.Position.Z),
            Radius = 2,
            Fill = SourceColor
          };

          BasePointIds.Add(plumbingSource.BasePointId);

        }
        else if (item is PlumbingFixture plumbingFixture) {
          model = new SphereVisual3D {
            Center = new Point3D(plumbingFixture.Position.X, plumbingFixture.Position.Y, plumbingFixture.Position.Z),
            Radius = 2,
            Fill = Brushes.Green
          };
          BasePointIds.Add(plumbingFixture.BasePointId);
        }
        if (model != null) {
          RouteVisuals.Add(model);
        }
      }
      foreach (var kvp in fullVerticalRoutes) {
        List<PlumbingVerticalRoute> verticalRoutes = kvp.Value
        .OrderBy(vr => BasePoints.TryGetValue(vr.BasePointId, out var bp) ? bp.Floor : 0)
        .ToList();

        Point3D pos = new Point3D(0, 0, 0);
        if (verticalRoutes.First().IsUp) {
          pos = new Point3D(verticalRoutes.Last().Position.X, verticalRoutes.Last().Position.Y, verticalRoutes.Last().Position.Z + verticalRoutes.Last().Length * 12);
          if (verticalRoutes.Last().NodeTypeId == 3) {
            pos = new Point3D(verticalRoutes.Last().Position.X, verticalRoutes.Last().Position.Y, verticalRoutes.Last().Position.Z);
          }
        }
        else {
          pos = new Point3D(verticalRoutes.First().Position.X, verticalRoutes.First().Position.Y, verticalRoutes.First().Position.Z);
          if (verticalRoutes.First().NodeTypeId == 3) {
            pos = new Point3D(verticalRoutes.First().Position.X, verticalRoutes.First().Position.Y, verticalRoutes.First().Position.Z - verticalRoutes.First().Length * 12);
          }
        }

        double pipeLength = 0;
        double pipeFixtureUnits = 0;
        bool isUp = false;

        foreach (var verticalRoute in verticalRoutes) {
          pipeLength += verticalRoute.Length * 12; // Convert to inches
          pipeFixtureUnits += verticalRoute.FixtureUnits;
        }
        int flowTypeId = verticalRoutes.First().FlowTypeId;
        int gpm = verticalRoutes.First().GPM;
        string pipeSize = verticalRoutes.First().PipeSize;
        double longestLength = verticalRoutes.Max(vr => vr.LongestRunLength);
        int longestLengthFeet = (int)(longestLength / 12); // Convert to feet
        int longestLengthInches = (int)Math.Round(longestLength % 12); // Remaining inches
        string routeBasePointId = verticalRoutes.First().BasePointId;
        if (verticalRoutes.First().IsUp) {
          flowTypeId = verticalRoutes.Last().FlowTypeId;
          gpm = verticalRoutes.Last().GPM;
          pipeSize = verticalRoutes.Last().PipeSize;
          routeBasePointId = verticalRoutes.Last().BasePointId;
          isUp = true;
        }
        string flow = (flowTypeId == 1) ? "Flush Tank" : "Flush Valve";

        // Calculate pipe length in feet/inches
        int feet = (int)(pipeLength / 12);
        int inches = (int)Math.Round(pipeLength % 12);
        string textString = $" {feet}' {inches}\" \n {flow} \n FU: {pipeFixtureUnits}\n GPM: {gpm} \n ---------------------- \n {pipeSize}\n";
        if (verticalRoutes.First().Type == "Gas") {
          textString = $" {feet}' {inches}\"\n CFH: {pipeFixtureUnits} \n Longest Run: {longestLengthFeet}' {longestLengthInches}\"\n ---------------------- \n {pipeSize}\n";
        }
        else if (verticalRoutes.First().Type == "Waste" || verticalRoutes.First().Type == "Grease Waste") {
          WasteSizingChart chart = new WasteSizingChart();
          string recommendedSize = chart.FindSize(pipeFixtureUnits, "Vertical", pipeLength);
          textString = $" {feet}' {inches}\"\n DFU: {pipeFixtureUnits} \n ---------------------- \n {recommendedSize}\n";
        }
        else if (verticalRoutes.First().Type == "Vent") {
          VentSizingChart chart = new VentSizingChart();
          string recommendedSize = chart.FindSize(pipeFixtureUnits, longestLength);
          textString = $" {feet}' {inches}\"\n ---------------------- \n {recommendedSize}\n";

        }
        int textHeight = 8;
        double textWidth = textHeight * textString.Length * 0.03;

        double offset = textWidth / 2;
        if (verticalRoutes[0].IsUp)
          pos = new Point3D(pos.X + 6, pos.Y, pos.Z - offset);
        else
          pos = new Point3D(pos.X + 6, pos.Y, pos.Z + offset);

        var textModel = new TextVisual3D {
          Position = pos,
          Text = textString,
          Height = textHeight,
          Foreground = Brushes.Black,
          Background = Brushes.White,
          TextDirection = new Vector3D(1, 0, 0),
          UpDirection = new Vector3D(0, 0, 1)
        };
        TextVisual3DExtensions.SetBasePointId(textModel, routeBasePointId);
        TextVisual3DExtensions.SetIsUp(textModel, isUp);
        TextVisual3DExtensions.SetType(textModel, verticalRoutes.First().Type);

        textVisuals.Add(textModel);
      }
      foreach (var basePoint in BasePointIds) {
        var basePointModel = new RectangleVisual3D {
          Origin = new Point3D(0, 0, BasePoints[basePoint].FloorHeight * 12),
          Width = 50,
          Length = 50,
          Normal = new Vector3D(0, 0, 1),
          Fill = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
        };
        RouteVisuals.Add(basePointModel);

        var basePointModel2 = new RectangleVisual3D {
          Origin = new Point3D(0, 0, BasePoints[basePoint].CeilingHeight * 12),
          Width = 50,
          Length = 50,
          Normal = new Vector3D(0, 0, 1),
          Fill = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
        };
        RouteVisuals.Add(basePointModel2);

        var textModel = new TextVisual3D {
          Position = new Point3D(0, 0, BasePoints[basePoint].FloorHeight * 12 + 12), // Slightly above the rectangle
          Text = $"Floor {BasePoints[basePoint].Floor}",
          Height = 20, // Size of the text
          Foreground = Brushes.White,
          //UpDirection = new Vector3D(0, 1, 0), // Text facing up
          Background = Brushes.Transparent // Or Brushes.White for a background
        };
        textVisuals.Add(textModel);
      }
      foreach (var text in textVisuals) {
        RouteVisuals.Add(text);
      }
    }
    public SolidColorBrush TypeToBrushColor(string type) {
      switch (type) {
        case "Cold Water":
          return System.Windows.Media.Brushes.Yellow;
        case "Hot Water":
          return System.Windows.Media.Brushes.Magenta;
        case "Gas":
          return System.Windows.Media.Brushes.SteelBlue;
        case "Waste":
          return System.Windows.Media.Brushes.Cyan;
        case "Grease Waste":
          return System.Windows.Media.Brushes.Magenta;
        case "Vent":
          return System.Windows.Media.Brushes.Green;
        default:
          return System.Windows.Media.Brushes.Gray; // Default color for unknown layers
      }
    }
    public void RemoveDuplicateRouteVisuals() {
      var unique = new HashSet<string>();
      var toRemove = new List<Visual3D>();

      foreach (var visual in RouteVisuals) {
        string key = null;
        if (visual is SphereVisual3D sphere)
          key = $"Sphere:{sphere.Center.X},{sphere.Center.Y},{sphere.Center.Z}";
        else if (visual is TubeVisual3D tube && tube.Path.Count > 1)
          key = $"Tube:{tube.Path[0].X},{tube.Path[0].Y},{tube.Path[0].Z}-{tube.Path[1].X},{tube.Path[1].Y},{tube.Path[1].Z}";
        else if (visual is RectangleVisual3D rect)
          key = $"Rect:{rect.Origin.X},{rect.Origin.Y},{rect.Origin.Z}";
        else if (visual is TextVisual3D text)
          key = $"Text:{text.Position.X},{text.Position.Y},{text.Position.Z}:{text.Text}";
        else
          key = visual.GetType().Name + visual.GetHashCode();

        if (!unique.Add(key))
          toRemove.Add(visual);
      }

      foreach (var visual in toRemove)
        RouteVisuals.Remove(visual);
    }
  }
  public class View : INotifyPropertyChanged {
    public List<PlumbingFullRoute> FullRoutes { get; set; } = new List<PlumbingFullRoute>();

    public Scene _mainScene = new Scene();
    public Scene MainScene {
      get { return _mainScene; }
      set {
        _mainScene = value;
        OnPropertyChanged(nameof(MainScene));
      }
    }

    public ObservableCollection<Scene> Scenes { get; set; } = new ObservableCollection<Scene>();


    public bool IsCalculatorEnabled { get; set; } = false;
    public Dictionary<string, WaterCalculator> WaterCalculators { get; set; } = new Dictionary<string, WaterCalculator>();
    public Dictionary<string, GasCalculator> GasCalculators { get; set; } = new Dictionary<string, GasCalculator>();
    public Dictionary<string, PlumbingPlanBasePoint> BasePointLookup { get; set; } = new Dictionary<string, PlumbingPlanBasePoint>();
    public string Name { get; set; } = "";
    public ICommand CalculateCommand { get; }
    public View(List<PlumbingFullRoute> fullRoutes, Dictionary<string, PlumbingPlanBasePoint> basePointLookup) {

      CalculateCommand = new RelayCommand(ExecuteCalculate);
      PlumbingSource source1 = fullRoutes[0].RouteItems[0] as PlumbingSource;
      if (source1 != null) {
        Name = basePointLookup[source1.BasePointId].Plan + ": " + basePointLookup[source1.BasePointId].Type;
      }

      BasePointLookup = basePointLookup;
      FullRoutes = DeepCopyFullRoutes(fullRoutes);
      NormalizeRoutes();
      GenerateWaterCalculators();
      GenerateGasCalculators();
      GenerateScenes();
    }


    public void GenerateWaterPipeSizing() {
      WaterPipeSizingChart chart = new WaterPipeSizingChart();
      foreach (var fullRoute in FullRoutes) {
        if (fullRoute.RouteItems.Count == 0) continue;
        double psi = 0;
        if (fullRoute.RouteItems[0] is PlumbingSource plumbingSource && (plumbingSource.TypeId == 1 || plumbingSource.TypeId == 2)) {
          string sourceId = plumbingSource.Id;
          psi = WaterCalculators[sourceId].AveragePressureDrop;
        }
        foreach (var item in fullRoute.RouteItems) {
          if (item is PlumbingHorizontalRoute horizontalRoute && (horizontalRoute.Type == "Cold Water" || horizontalRoute.Type == "Hot Water")) {
            bool isHot = false;
            if (horizontalRoute.Type == "Hot Water") {
              isHot = true;
            }
            horizontalRoute.GenerateGallonsPerMinute();
            horizontalRoute.PipeSize = chart.FindSize(
              horizontalRoute.PipeType,
              psi,
              isHot,
              horizontalRoute.GPM
            );
          }
          else if (item is PlumbingVerticalRoute verticalRoute && (verticalRoute.Type == "Cold Water" || verticalRoute.Type == "Hot Water")) {
            bool isHot = false;
            if (verticalRoute.Type == "Hot Water") {
              isHot = true;
            }
            verticalRoute.GenerateGallonsPerMinute();
            verticalRoute.PipeSize = chart.FindSize(
              verticalRoute.PipeType,
              psi,
              isHot,
              verticalRoute.GPM
            );
          }
        }
      }
      RegenerateScenes();
    }
    public void GenerateGasPipeSizing() {
      foreach (var fullRoute in FullRoutes) {
        if (fullRoute.RouteItems.Count == 0) continue;
        double psi = 0;
        string sourceId = "";
        if (fullRoute.RouteItems[0] is PlumbingSource plumbingSource && plumbingSource.TypeId == 3) {
          sourceId = plumbingSource.Id;
          if (GasCalculators[sourceId].ChosenChart == null) {
            return;
          }
        }
        else {
          continue;
        }
        foreach (var item in fullRoute.RouteItems) {
            if (item is PlumbingHorizontalRoute horizontalRoute && horizontalRoute.Type == "Gas") {
              string pipeSize = "";
              GasEntry entry = GasCalculators[sourceId].ChosenChart.GetData(
                horizontalRoute.LongestRunLength,
                horizontalRoute.FixtureUnits
              );
              if (entry is SemiRigidCopperGasEntry copperEntry) {
                pipeSize = "Nominal ACR: " + copperEntry.NominalACR + "\n Nominal KL: " + copperEntry.NominalKL + "\n Outside: " + copperEntry.OutsideDiameter + "\n Inside: " + copperEntry.InsideDiameter;
              }
              else if (entry is Schedule40MetalGasEntry metal40Entry) {
                pipeSize = "Actual ID: " + metal40Entry.ActualID + "\"\n Nominal Pipe Size: " + metal40Entry.NominalSize+"\" ";
              }
              else if (entry is PolyethylenePlasticGasEntry plasticEntry) {
                pipeSize = "Actual ID: " + plasticEntry.ActualID + "\n Designation: " + plasticEntry.Designation;
              }
              else if (entry is StainlessSteelGasEntry stainlessEntry) {
                pipeSize = "Flow Designation: " + stainlessEntry.FlowDesignation;
              }
              horizontalRoute.PipeSize = pipeSize;
            }
            else if (item is PlumbingVerticalRoute verticalRoute && verticalRoute.Type == "Gas") {
              GasEntry entry = GasCalculators[sourceId].ChosenChart.GetData(
                verticalRoute.LongestRunLength,
                verticalRoute.FixtureUnits
              );
              string pipeSize = "";
              if (entry is SemiRigidCopperGasEntry copperEntry) {
                pipeSize = "Nominal ACR: " + copperEntry.NominalACR + "\n Nominal KL: " + copperEntry.NominalKL + "\n Outside: " + copperEntry.OutsideDiameter + "\n Inside: " + copperEntry.InsideDiameter;
              }
              else if (entry is Schedule40MetalGasEntry metal40Entry) {
              pipeSize = "Actual ID: " + metal40Entry.ActualID + "\"\n Nominal Pipe Size: " + metal40Entry.NominalSize + "\" ";
            }
              else if (entry is PolyethylenePlasticGasEntry plasticEntry) {
                pipeSize = "Actual ID: " + plasticEntry.ActualID + "\n Designation: " + plasticEntry.Designation;
              }
              else if (entry is StainlessSteelGasEntry stainlessEntry) {
                pipeSize = "Flow Designation: " + stainlessEntry.FlowDesignation;
              }
              verticalRoute.PipeSize = pipeSize;
            }
          }
      }
      RegenerateScenes();
    }
    private void ExecuteCalculate(object parameter) {
      var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
      ed.WriteMessage("\nCalculating pipe sizes...");
      GenerateWaterPipeSizing();
      GenerateGasPipeSizing();
    }

    public void GenerateWaterCalculators() {
      WaterCalculators.Clear();
      foreach (var fullRoute in FullRoutes) {
        if (fullRoute.RouteItems[0] is PlumbingSource plumbingSource && (plumbingSource.TypeId == 1 || plumbingSource.TypeId == 2)) {
          IsCalculatorEnabled = true;
          if (!WaterCalculators.ContainsKey(plumbingSource.Id)) {
            ObservableCollection<WaterLoss> waterLosses = new ObservableCollection<WaterLoss>();
            ObservableCollection<WaterAddition> waterAdditions = new ObservableCollection<WaterAddition>();
            double maxLength = FullRoutes
              .Where(r => r.RouteItems.Count > 0
                  && r.RouteItems[0] is PlumbingSource src
                  && src.Id == plumbingSource.Id)
              .Max(r => r.Length);
            string name = "";
            switch(plumbingSource.TypeId) {
              case 1:
                name = "Water Meter";
                break;
              case 2:
                name = "Water Heater";
                break;
              case 3:
                name = "Gas Meter";
                break;
              case 4:
                name = "Waste Output";
                break;
            }

            WaterCalculators[plumbingSource.Id] = new WaterCalculator(name, plumbingSource.Pressure, 0, maxLength / 12, (maxLength * 1.3) / 12, 0, waterLosses, waterAdditions);
          }
        }
      }
    }
    public void GenerateGasCalculators() {
     GasCalculators.Clear();
      foreach (var fullRoute in FullRoutes) {
        if (fullRoute.RouteItems[0] is PlumbingSource plumbingSource && plumbingSource.TypeId == 3) {
          IsCalculatorEnabled = true;
          if (!GasCalculators.ContainsKey(plumbingSource.Id)) {
            string name = "";
            switch (plumbingSource.TypeId) {
              case 1:
                name = "Water Meter";
                break;
              case 2:
                name = "Water Heater";
                break;
              case 3:
                name = "Gas Meter";
                break;
              case 4:
                name = "Waste Output";
                break;
            }

            GasCalculators[plumbingSource.Id] = new GasCalculator(name);
          }
        }
      }
    }
    public void GenerateScenes() {
      Scenes.Clear();
      Scene fullScene = new Scene();
      foreach (var fullRoute in FullRoutes) {
        var scene = new Scene(fullRoute, BasePointLookup);
        var scene2 = new Scene(fullRoute, BasePointLookup);
        Scenes.Add(scene);
        foreach (var visual in scene2.RouteVisuals) {
          fullScene.RouteVisuals.Add(visual);
        }
      }
      fullScene.RemoveDuplicateRouteVisuals();
      MainScene = fullScene;
    }
    public void RegenerateScenes() {
      Scene fullScene = new Scene();
      int index = 0;
      foreach (var fullRoute in FullRoutes) {
        Scenes[index].RebuildScene(fullRoute);
        var scene2 = new Scene(fullRoute, BasePointLookup);
        foreach (var visual in scene2.RouteVisuals) {
          fullScene.RouteVisuals.Add(visual);
        }
        index++;
      }
      fullScene.RemoveDuplicateRouteVisuals();
      fullScene.InitialBuild = false;
      MainScene = fullScene;
    }
    public void NormalizeRoutes() {
      foreach (var route in FullRoutes) {
        foreach (var item in route.RouteItems) {
          if (item is PlumbingHorizontalRoute horizontalRoute) {
            if (BasePointLookup.TryGetValue(horizontalRoute.BasePointId, out var basePoint)) {
              horizontalRoute.StartPoint = new Point3d(
                horizontalRoute.StartPoint.X - basePoint.Point.X,
                horizontalRoute.StartPoint.Y - basePoint.Point.Y,
                horizontalRoute.StartPoint.Z
              );
              horizontalRoute.EndPoint = new Point3d(
                horizontalRoute.EndPoint.X - basePoint.Point.X,
                horizontalRoute.EndPoint.Y - basePoint.Point.Y,
                horizontalRoute.EndPoint.Z
              );
            }
          }
          else if (item is PlumbingVerticalRoute verticalRoute) {
            if (BasePointLookup.TryGetValue(verticalRoute.BasePointId, out var basePoint)) {
              verticalRoute.Position = new Point3d(
                verticalRoute.Position.X - basePoint.Point.X,
                verticalRoute.Position.Y - basePoint.Point.Y,
                verticalRoute.Position.Z
              );
            }
          }
          else if (item is PlumbingSource plumbingSource) {
            if (BasePointLookup.TryGetValue(plumbingSource.BasePointId, out var basePoint)) {
              plumbingSource.Position = new Point3d(
                plumbingSource.Position.X - basePoint.Point.X,
                plumbingSource.Position.Y - basePoint.Point.Y,
                plumbingSource.Position.Z
              );
            }
          }
          else if (item is PlumbingFixture plumbingFixture) {
            if (BasePointLookup.TryGetValue(plumbingFixture.BasePointId, out var basePoint)) {
              plumbingFixture.Position = new Point3d(
                plumbingFixture.Position.X - basePoint.Point.X,
                plumbingFixture.Position.Y - basePoint.Point.Y,
                plumbingFixture.Position.Z
              );
            }
          }
        }
      }
    }
    public static List<PlumbingFullRoute> DeepCopyFullRoutes(List<PlumbingFullRoute> original) {
      var result = new List<PlumbingFullRoute>();
      var newList = new List<PlumbingFullRoute>();
      foreach (var fullRoute in original) {
        var newFullRoute = new PlumbingFullRoute {
          Length = fullRoute.Length,
          RouteItems = new List<object>(),
          TypeId = fullRoute.TypeId,
        };
        foreach (var item in fullRoute.RouteItems) {
          if (item is PlumbingHorizontalRoute hr) {
            var copy = new PlumbingHorizontalRoute(
                hr.Id,
                hr.ProjectId,
                hr.Type,
                new Point3d(hr.StartPoint.X, hr.StartPoint.Y, hr.StartPoint.Z),
                new Point3d(hr.EndPoint.X, hr.EndPoint.Y, hr.EndPoint.Z),
                hr.BasePointId,
                hr.PipeType,
                hr.Slope
            );
            copy.FixtureUnits = hr.FixtureUnits;
            copy.FlowTypeId = hr.FlowTypeId;
            copy.LongestRunLength = hr.LongestRunLength;
            newFullRoute.RouteItems.Add(copy);
          }
          else if (item is PlumbingVerticalRoute vr) {
            var copy = new PlumbingVerticalRoute(
                vr.Id,
                vr.ProjectId,
                vr.Type,
                new Point3d(vr.Position.X, vr.Position.Y, vr.Position.Z),
                vr.VerticalRouteId,
                vr.BasePointId,
                vr.StartHeight,
                vr.Length,
                vr.NodeTypeId,
                vr.PipeType,
                vr.IsUp
            );
            copy.FixtureUnits = vr.FixtureUnits;
            copy.FlowTypeId = vr.FlowTypeId;
            copy.LongestRunLength = vr.LongestRunLength;
            newFullRoute.RouteItems.Add(copy);
          }
          else if (item is PlumbingSource plumbingSource) {
            var copy = new PlumbingSource(
                plumbingSource.Id,
                plumbingSource.ProjectId,
                new Point3d(plumbingSource.Position.X, plumbingSource.Position.Y, plumbingSource.Position.Z),
                plumbingSource.TypeId,
                plumbingSource.BasePointId,
                plumbingSource.Pressure
            );
            newFullRoute.RouteItems.Add(copy);
          }
          else if (item is PlumbingFixture plumbingFixture) {
            var copy = new PlumbingFixture(
                plumbingFixture.Id,
                plumbingFixture.ProjectId,
                new Point3d(plumbingFixture.Position.X, plumbingFixture.Position.Y, plumbingFixture.Position.Z),
                plumbingFixture.Rotation,
                plumbingFixture.CatalogId,
                plumbingFixture.TypeAbbreviation,
                plumbingFixture.Number,
                plumbingFixture.BasePointId,
                plumbingFixture.BlockName,
                plumbingFixture.FlowTypeId
            );
            newFullRoute.RouteItems.Add(copy);
          }
        }
        newList.Add(newFullRoute);
      }
      result = newList;
      return result;
    }

    public void GetWaterSizingChart(string pipeType, double psi) {

    }
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
  public class AddOneConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is int i)
        return (i + 1).ToString();
      return value;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }
  public class RelayCommand : ICommand {
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) {
      _execute = execute;
      _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
    public void Execute(object parameter) => _execute(parameter);
    public event EventHandler CanExecuteChanged {
      add { CommandManager.RequerySuggested += value; }
      remove { CommandManager.RequerySuggested -= value; }
    }
  }
  public class BoolToVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }
  public static class TextVisual3DExtensions {
    public static readonly DependencyProperty BasePointIdProperty =
        DependencyProperty.RegisterAttached(
            "BasePointId",
            typeof(object),
            typeof(TextVisual3DExtensions),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsUpProperty =
    DependencyProperty.RegisterAttached(
        "IsUp",
        typeof(object),
        typeof(TextVisual3DExtensions),
        new PropertyMetadata(null));

    public static readonly DependencyProperty TypeProperty =
    DependencyProperty.RegisterAttached(
        "Type",
        typeof(object),
        typeof(TextVisual3DExtensions),
        new PropertyMetadata(null));



    public static void SetBasePointId(DependencyObject element, object value) {
      element.SetValue(BasePointIdProperty, value);
    }

    public static object GetBasePointId(DependencyObject element) {
      return element.GetValue(BasePointIdProperty);
    }

    public static void SetIsUp(DependencyObject element, object value) {
      element.SetValue(IsUpProperty, value);
    }

    public static object GetIsUp(DependencyObject element) {
      return element.GetValue(IsUpProperty);
    }
    public static void SetType(DependencyObject element, object value) {
      element.SetValue(TypeProperty, value);
    }

    public static object GetType(DependencyObject element) {
      return element.GetValue(TypeProperty);
    }
  }


}
