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
using Autodesk.AutoCAD.GraphicsSystem;
using static System.Net.Mime.MediaTypeNames;
using Autodesk.AutoCAD.MacroRecorder;
using MySqlX.XDevAPI.Common;

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
        View view = new View(routeScene.Key, routeScene.Value, basePointLookup);
        Views.Add(view);
      }
    }

    private void Button_Click(object sender, RoutedEventArgs e) {
      var item = (sender as Button)?.DataContext as MenuItemViewModel;
      item?.OnClick();
      e.Handled = false; 
    }

    private void InnerControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
      if (IsMouseOverComboBoxPopup())
        return;

      var scrollViewer = FindParent<ScrollViewer>(sender as DependencyObject);
      if (scrollViewer != null) {
        if (e.Delta != 0) {
          scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
          e.Handled = true;
        }
      }
    }
    private bool IsMouseOverComboBoxPopup() {
      foreach (var obj in FindVisualChildren<ComboBox>(this)) {
        if (obj.IsDropDownOpen) {
          var popup = obj.Template.FindName("PART_Popup", obj) as System.Windows.Controls.Primitives.Popup;
          if (popup != null && popup.Child != null && popup.Child.IsMouseOver)
            return true;
        }
      }
      return false;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject {
      if (depObj != null) {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
          DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
          if (child != null && child is T t) {
            yield return t;
          }

          foreach (T childOfChild in FindVisualChildren<T>(child)) {
            yield return childOfChild;
          }
        }
      }
    }
    

    public static T FindParent<T>(DependencyObject child) where T : DependencyObject {
      DependencyObject parentObject = VisualTreeHelper.GetParent(child);
      if (parentObject == null) return null;
      T parent = parentObject as T;
      if (parent != null)
        return parent;
      else
        return FindParent<T>(parentObject);
    }

    private void CalcExpander_Collapsed(object sender, RoutedEventArgs e) {
      if (sender is Expander expander && expander.Name == "CalcExpander") {
        if (expander == null) return;
        var parent = VisualTreeHelper.GetParent(expander);
        while (parent != null && !(parent is Grid))
          parent = VisualTreeHelper.GetParent(parent);

        var grid = parent as Grid;
        if (grid == null || grid.RowDefinitions.Count < 4) return;

        grid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        grid.RowDefinitions[3].Height = new GridLength(20, GridUnitType.Star);
      }
      e.Handled = true;
    }
    private void CalcExpander_Expanded(object sender, RoutedEventArgs e) {
      if (sender is Expander expander && expander.Name == "CalcExpander") {
        if (expander == null) return;
        var parent = VisualTreeHelper.GetParent(expander);
        while (parent != null && !(parent is Grid))
          parent = VisualTreeHelper.GetParent(parent);

        var grid = parent as Grid;
        if (grid == null || grid.RowDefinitions.Count < 4) return;

        grid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        grid.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);
      }
      e.Handled = true;
    }
  }

  public class Scene : INotifyPropertyChanged {
      public List<object> RouteItems { get; set; } = new List<object>();
      public double Length { get; set; } = 0;
      public ObservableCollection<Visual3D> RouteVisuals { get; set; } = new ObservableCollection<Visual3D>();
      public Dictionary<string, PlumbingPlanBasePoint> BasePoints { get; set; } = new Dictionary<string, PlumbingPlanBasePoint>();

      public HashSet<string> BasePointIds = new HashSet<string>();

      public List<RouteInfoBox> RouteInfoBoxes { get; set; } = new List<RouteInfoBox>();

      public Dictionary<Brush, MeshBuilder> meshBuilders = new Dictionary<Brush, MeshBuilder>();

    public string ViewportId { get; set; } = "";

      public bool _changeFlag = true;
      public bool ChangeFlag {
        get { return _changeFlag; }
        set {
          _changeFlag = value;
          OnPropertyChanged(nameof(ChangeFlag));
        }
      }

      public bool InitialBuild { get; set; } = true;

      public event PropertyChangedEventHandler PropertyChanged;
      protected void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }

      public Scene(string viewportId, PlumbingFullRoute fullRoute, Dictionary<string, PlumbingPlanBasePoint> basePoints) {
        ViewportId = viewportId;
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
      ChangeFlag = !ChangeFlag;
    }

    public async void BuildScene() {
      RouteInfoBoxes.Clear();
      RouteVisuals.Clear();
      meshBuilders.Clear();
      List<TextVisual3D> textVisuals = new List<TextVisual3D>();
      Dictionary<string, List<PlumbingVerticalRoute>> fullVerticalRoutes = new Dictionary<string, List<PlumbingVerticalRoute>>();

      foreach (var item in RouteItems) {
        //Visual3D model = null;
         if (item is PlumbingHorizontalRoute horizontalRoute) {
          var color = TypeToBrushColor(horizontalRoute.Type);
          if (!meshBuilders.ContainsKey(color)) {
            meshBuilders[color] = new MeshBuilder(false, false);
          }

          meshBuilders[color].AddTube(
            new[] {new Point3D(horizontalRoute.StartPoint.X, horizontalRoute.StartPoint.Y, horizontalRoute.StartPoint.Z),
                    new Point3D(horizontalRoute.EndPoint.X, horizontalRoute.EndPoint.Y, horizontalRoute.EndPoint.Z)
                  }, 2, 8, false);

          meshBuilders[color].AddSphere(new Point3D(horizontalRoute.StartPoint.X, horizontalRoute.StartPoint.Y, horizontalRoute.StartPoint.Z), 1, 8, 8);
          meshBuilders[color].AddSphere(new Point3D(horizontalRoute.EndPoint.X, horizontalRoute.EndPoint.Y, horizontalRoute.EndPoint.Z), 1, 8, 8);

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
          int longestRunFeet = (int)Math.Ceiling(longestRunLength / 12); // Convert to feet
          //int longestRunInches = (int)Math.Round(longestRunLength % 12); // Remaining inches
          string recommendedSize = horizontalRoute.PipeSize;
          string units = horizontalRoute.FixtureUnits.ToString();
          string longestRun = "";

          double textHeight = 8;
          string textString = $" {feet}' {inches}\"\n {flow} \n FU: {horizontalRoute.FixtureUnits} \n GPM: {horizontalRoute.GPM} \n ---------------------- \n {recommendedSize}\n";
          if (horizontalRoute.Type == "Gas") {
            longestRun = $"{longestRunFeet}'";
            textString = $" {feet}' {inches}\"\n CFH: {horizontalRoute.FixtureUnits} \n Longest Run: {longestRun}\n ---------------------- \n {recommendedSize}\n";
          }
          else if (horizontalRoute.Type == "Waste" || horizontalRoute.Type == "Grease Waste") {
            WasteSizingChart chart = new WasteSizingChart();
            string slope = "1%";
            if (horizontalRoute.Slope == 0.02) {
              slope = "2%";
            }
            recommendedSize = chart.FindSize(horizontalRoute.FixtureUnits, slope);
            textString = $" {feet}' {inches}\"\n DFU: {horizontalRoute.FixtureUnits}\n Slope: {slope}\n ---------------------- \n {recommendedSize}\n";
          }
          else if (horizontalRoute.Type == "Vent") {
            VentSizingChart chart = new VentSizingChart();
            string slope = "1%";
            if (horizontalRoute.Slope == 0.02) {
              slope = "2%";
            }
            recommendedSize = chart.FindSize(horizontalRoute.FixtureUnits, horizontalRoute.LongestRunLength);
            textString = $" {feet}' {inches}\"\n Slope: {slope}\n ---------------------- \n {recommendedSize}\n";
          }
          double textWidth = textHeight * textString.Length * 0.03;

          // Offset so the back of the text aligns with the end point 
          var textPos = new Point3D(
              horizontalRoute.EndPoint.X - (direction.X * (textWidth / 2)),
              horizontalRoute.EndPoint.Y - (direction.Y * (textWidth / 2)),
              horizontalRoute.EndPoint.Z + 6 // +5 for vertical offset
          );

          //upload the route data
          string cleanedSize = recommendedSize;
          int idx = cleanedSize.IndexOf("Nominal Pipe Size: ");
          if (idx >= 0)
            cleanedSize = cleanedSize.Substring(idx + "Nominal Pipe Size: ".Length);
          else {
            idx = cleanedSize.IndexOf("Pipe Size: ");
            if (idx >= 0)
              cleanedSize = cleanedSize.Substring(idx + "Pipe Size: ".Length);
          }
          cleanedSize = cleanedSize.Replace("\n", "").Replace("\r", "");

          //Uploading Route Info
          double segmentLength = horizontalRoute.EndPoint.DistanceTo(horizontalRoute.StartPoint);
          string segmentLengthString = ToFeetInchesString(segmentLength);
          string locationDescription = "";
          if (BasePoints.ContainsKey(horizontalRoute.BasePointId)) {
            PlumbingPlanBasePoint point = BasePoints[horizontalRoute.BasePointId];
            if (horizontalRoute.StartPoint.Z == point.CeilingHeight * 12) {
              locationDescription = "ABV. CLG";
            }
            else if (horizontalRoute.StartPoint.Z == point.FloorHeight * 12) {
              locationDescription = "BLW. FLR";
            }
          }
          RouteInfoBoxes.Add(new RouteInfoBox(
            horizontalRoute.ProjectId,
            ViewportId,
            horizontalRoute.Id,
            horizontalRoute.BasePointId,
            cleanedSize,
            horizontalRoute.Type,
            locationDescription.Replace("\n", "").Replace("\r", ""),
            "",
            units,
            longestRun,
            "",
            false,
            segmentLengthString
          ));
          // Create and configure the TextVisual3D
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

          BasePointIds.Add(horizontalRoute.BasePointId);
        }
        else if (item is PlumbingVerticalRoute verticalRoute) {
          if (!fullVerticalRoutes.ContainsKey(verticalRoute.VerticalRouteId)) {
            fullVerticalRoutes[verticalRoute.VerticalRouteId] = new List<PlumbingVerticalRoute>();
          }
          verticalRoute.GenerateGallonsPerMinute();
          fullVerticalRoutes[verticalRoute.VerticalRouteId].Add(verticalRoute);

          double length = verticalRoute.Length * 12;

          if (verticalRoute.NodeTypeId == 3) {
            length = -length;
          }
          Brush color = TypeToBrushColor(verticalRoute.Type);
          if (!meshBuilders.ContainsKey(color)) {
            meshBuilders[color] = new MeshBuilder(false, false);
          }
          meshBuilders[color].AddTube(
            new[] {
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z),
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z + length)
            }, 2, 8, false);

          meshBuilders[color].AddSphere(
            new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z + length), 1, 8, 8);
          meshBuilders[color].AddSphere(
            new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z), 1, 8, 8);


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
          if (!meshBuilders.ContainsKey(SourceColor)) {
            meshBuilders[SourceColor] = new MeshBuilder(false, false);
          }

          meshBuilders[SourceColor].AddSphere(new Point3D(plumbingSource.Position.X, plumbingSource.Position.Y, plumbingSource.Position.Z), 2, 8, 8);

          BasePointIds.Add(plumbingSource.BasePointId);

        }
        else if (item is PlumbingFixture plumbingFixture) {
          if (!meshBuilders.ContainsKey(Brushes.Green)) {
            meshBuilders[Brushes.Green] = new MeshBuilder(false, false);
          }
          meshBuilders[Brushes.Green].AddSphere(new Point3D(plumbingFixture.Position.X, plumbingFixture.Position.Y, plumbingFixture.Position.Z), 2, 8, 8);
          BasePointIds.Add(plumbingFixture.BasePointId);
        }
        else if (item is PlumbingAccessory plumbingAccessory) {
          if (plumbingAccessory.TypeId == 1) {
            CreateGroundCleanoutMesh(new Point3D(plumbingAccessory.Position.X, plumbingAccessory.Position.Y, plumbingAccessory.Position.Z), 4, 1, TypeToBrushColor(plumbingAccessory.Type));
          }
          else if (plumbingAccessory.TypeId == 2) {
          }
          else if (plumbingAccessory.TypeId == 3) {
          }
          else if (plumbingAccessory.TypeId == 4) {
            double pyramidHeight = 1.5;
            double pyramidBaseSize = 2;
            CreateValveMesh(new Point3D(plumbingAccessory.Position.X, plumbingAccessory.Position.Y, plumbingAccessory.Position.Z), pyramidBaseSize, pyramidHeight, TypeToBrushColor(plumbingAccessory.Type), plumbingAccessory.Rotation);
          }
          else if (plumbingAccessory.TypeId == 5) {
            double pyramidHeight = 2.5;
            double pyramidBaseSize = 2;
            CreateValveMesh(new Point3D(plumbingAccessory.Position.X, plumbingAccessory.Position.Y, plumbingAccessory.Position.Z), pyramidBaseSize, pyramidHeight, TypeToBrushColor(plumbingAccessory.Type), plumbingAccessory.Rotation);
          }
          BasePointIds.Add(plumbingAccessory.BasePointId);
        }
        foreach (var kvp in meshBuilders) {
          var color = kvp.Key;
          var meshBuilder = kvp.Value;
          var mesh = meshBuilder.ToMesh();
          var material = MaterialHelper.CreateMaterial(color);
          mesh.Freeze();
          material.Freeze();

          var model = new GeometryModel3D {
            Geometry = mesh,
            Material = material
          };
          model.Freeze();

          var visual = new ModelVisual3D {
            Content = model
          };

          RouteVisuals.Add(visual);
        }
      }
      foreach (var kvp in fullVerticalRoutes) {
        List<PlumbingVerticalRoute> verticalRoutes = kvp.Value
        .OrderBy(vr => BasePoints.TryGetValue(vr.BasePointId, out var bp) ? bp.Floor : 0)
        .ToList();
        /*foreach (var vr in verticalRoutes) {
          if (vr.Type == "Waste" || vr.Type == "Grease Waste" || vr.Type == "Vent") {
            vr.IsUp = !vr.IsUp;
          }
        }*/

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
        int longestLengthFeet = (int)Math.Ceiling(longestLength / 12); // Convert to feet
        //int longestLengthInches = (int)Math.Round(longestLength % 12); // Remaining inches
        string routeBasePointId = verticalRoutes.First().BasePointId;
        string units = pipeFixtureUnits.ToString();
        string longestRun = ""; 
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
          longestRun = $"{longestLengthFeet}'";
          textString = $" {feet}' {inches}\"\n CFH: {units} \n Longest Run: {longestRun}\n ---------------------- \n {pipeSize}\n";
        }
        else if (verticalRoutes.First().Type == "Waste" || verticalRoutes.First().Type == "Grease Waste") {
          WasteSizingChart chart = new WasteSizingChart();
          pipeSize = chart.FindSize(pipeFixtureUnits, "Vertical", pipeLength);
          textString = $" {feet}' {inches}\"\n DFU: {pipeFixtureUnits} \n ---------------------- \n {pipeSize}\n";
        }
        else if (verticalRoutes.First().Type == "Vent") {
          VentSizingChart chart = new VentSizingChart();
          pipeSize = chart.FindSize(pipeFixtureUnits, longestLength);
          textString = $" {feet}' {inches}\"\n ---------------------- \n {pipeSize}\n";

        }
        int textHeight = 8;
        double textWidth = textHeight * textString.Length * 0.03;

        double offset = textWidth / 2;
        if (isUp)
          pos = new Point3D(pos.X + 6, pos.Y, pos.Z - offset);
        else
          pos = new Point3D(pos.X + 6, pos.Y, pos.Z + offset);

        if (verticalRoutes.First().Type == "Waste" || verticalRoutes.First().Type == "Grease Waste" || verticalRoutes.First().Type == "Vent") {
          isUp = !isUp;
        }
        //upload the route data
        string cleanedSize = pipeSize;
        int idx = cleanedSize.IndexOf("Nominal Pipe Size: ");
        if (idx >= 0)
          cleanedSize = cleanedSize.Substring(idx + "Nominal Pipe Size: ".Length);
        else {
          idx = cleanedSize.IndexOf("Pipe Size: ");
          if (idx >= 0)
            cleanedSize = cleanedSize.Substring(idx + "Pipe Size: ".Length);
        }
        cleanedSize = cleanedSize.Replace("\n", "").Replace("\r", "");

        string pipeLengthString = ToFeetInchesString(pipeLength);
        bool isRoof = verticalRoutes.Any(verticalRoute=> {
          if (BasePoints.ContainsKey(verticalRoute.BasePointId)) {
            var point = BasePoints[verticalRoute.BasePointId];
            return point.IsRoof;
          }
          return false;
        });

        foreach (var verticalRoute in verticalRoutes) {
          string locationDescription = "";
          string sourceDescription = "";
          if (BasePoints.ContainsKey(verticalRoute.BasePointId)) {
            int floor = BasePoints[verticalRoute.BasePointId].Floor;
            if (isUp) {
              PlumbingVerticalRoute belowRoute = verticalRoutes.FirstOrDefault(vr => vr.VerticalRouteId == verticalRoute.VerticalRouteId && BasePoints[vr.BasePointId].Floor == floor - 1);
              if (belowRoute != null) {
                sourceDescription = $"from {floor - 1}{GetSuffix(floor - 1)} floor";
              }
              PlumbingVerticalRoute aboveRoute = verticalRoutes.FirstOrDefault(vr => vr.VerticalRouteId == verticalRoute.VerticalRouteId && BasePoints[vr.BasePointId].Floor == floor + 1);
              if (aboveRoute != null) {
                locationDescription = $"to {floor + 1}{GetSuffix(floor + 1)} floor\n";
               
              }
              else {
                PlumbingPlanBasePoint point = BasePoints[verticalRoute.BasePointId];
                if (verticalRoute.Position.Z == point.CeilingHeight * 12) {
                  locationDescription = "ABV. CLG";
                }
              }
            }
            else {
              PlumbingVerticalRoute aboveRoute = verticalRoutes.FirstOrDefault(vr => vr.VerticalRouteId == verticalRoute.VerticalRouteId && BasePoints[vr.BasePointId].Floor == floor + 1);
              if (aboveRoute != null) {
                sourceDescription = $"from {floor + 1}{GetSuffix(floor + 1)} floor";
              }
              PlumbingVerticalRoute belowRoute = verticalRoutes.FirstOrDefault(vr => vr.VerticalRouteId == verticalRoute.VerticalRouteId && BasePoints[vr.BasePointId].Floor == floor - 1);
              if (belowRoute != null) {
                locationDescription = $"to {floor - 1}{GetSuffix(floor - 1)} floor";
              }
              else {
                PlumbingPlanBasePoint point = BasePoints[verticalRoute.BasePointId];
                if (verticalRoute.NodeTypeId == 1) {
                  if (verticalRoute.Position.Z == point.FloorHeight * 12) {
                    locationDescription = "BLW. FLR";
                  }
                }
                else if (verticalRoute.NodeTypeId == 3) {
                  if (verticalRoute.Position.Z - (verticalRoute.Length * 12) == point.FloorHeight * 12) {
                    locationDescription = "BLW. FLR";
                  }
                }
              }
            }
          }
          if (isRoof) {
            locationDescription = "To Roof";
            sourceDescription = "";
          }

          RouteInfoBoxes.Add(new RouteInfoBox(
            verticalRoute.ProjectId,
            ViewportId,
            verticalRoute.Id,
            verticalRoute.BasePointId,
            cleanedSize,
            verticalRoutes.First().Type,
            locationDescription.Replace("\n", "").Replace("\r", ""),
            sourceDescription.Replace("\n", "").Replace("\r", ""),
            units,
            longestRun,
            isUp ? "Up" : "Down",
            true,
            pipeLengthString
          ));
        }

        // Create and configure the TextVisual3D
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
        string floor = $"Floor {BasePoints[basePoint].Floor}";
        if (BasePoints[basePoint].IsRoof) {
          floor = "Roof";
        }
        var textModel = new TextVisual3D {
          Position = new Point3D(0, 0, BasePoints[basePoint].FloorHeight * 12 + 12), // Slightly above the rectangle
          Text = floor,
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
      var seen = new HashSet<string>();
      var toRemove = new List<Visual3D>();

      // Traverse from end to start to keep the last occurrence
      for (int i = RouteVisuals.Count - 1; i >= 0; i--) {
        var visual = RouteVisuals[i];
        string key = null;
        if (visual is RectangleVisual3D rect)
          key = $"Rect:{rect.Origin.X},{rect.Origin.Y},{rect.Origin.Z}";
        else if (visual is TextVisual3D text)
          key = $"Text:{text.Position.X},{text.Position.Y},{text.Position.Z}:{text.Text}";
        else
          key = visual.GetType().Name + visual.GetHashCode();

        if (seen.Contains(key)) {
          toRemove.Add(visual);
        }
        else {
          seen.Add(key);
        }
      }

      // Remove all earlier duplicates
      foreach (var visual in toRemove)
        RouteVisuals.Remove(visual);
    }
    public static string ToFeetInchesString(double lengthInInches) {
      int feet = (int)(lengthInInches / 12);
      int inches = (int)Math.Round(lengthInInches % 12);
      return $"{feet}' {inches}\"";
    }
    public string GetSuffix(int number) {
      if (number % 100 >= 11 && number % 100 <= 13) {
        return "th";
      }
      switch (number % 10) {
        case 1:
          return "st";
        case 2:
          return "nd";
        case 3:
          return "rd";
        default:
          return "th";
      }
    }
    public void CreateValveMesh(Point3D center, double baseSize, double height, Brush color, double rotation) {
      var direction = new Vector3D(Math.Cos(rotation), Math.Sin(rotation), 0);
      // Helper to create a pyramid mesh
      MeshGeometry3D CreatePyramid(Point3D apex, Vector3D dir, double baseSz, double h) {
        var baseCenter = apex + dir * h;

        // Find two perpendicular vectors for the base
        Vector3D up = new Vector3D(0, 0, 1);
        if (Vector3D.CrossProduct(dir, up).Length == 0)
          up = new Vector3D(0, 1, 0);

        var right = Vector3D.CrossProduct(dir, up);
        right.Normalize();
        up = Vector3D.CrossProduct(right, dir);
        up.Normalize();

        double half = baseSz / 2;
        var p1 = baseCenter + right * half + up * half;
        var p2 = baseCenter - right * half + up * half;
        var p3 = baseCenter - right * half - up * half;
        var p4 = baseCenter + right * half - up * half;

        var meshBuilder = new MeshBuilder(false, false);
        meshBuilder.AddPolygon(new[] { p1, p2, p3, p4 });
        meshBuilder.AddPolygon(new[] { p4, p3, p2, p1 });
        meshBuilder.AddTriangle(apex, p1, p2);
        meshBuilder.AddTriangle(apex, p2, p3);
        meshBuilder.AddTriangle(apex, p3, p4);
        meshBuilder.AddTriangle(apex, p4, p1);

        return meshBuilder.ToMesh();
      }
      var mesh1 = CreatePyramid(center, direction, baseSize, height);
      var mesh2 = CreatePyramid(center, -direction, baseSize, height);

      // Two pyramids, apexes at center, bases offset in +X and -X
      if (!meshBuilders.ContainsKey(color))
        meshBuilders[color] = new MeshBuilder(false, false);

      meshBuilders[color].Append(mesh1);
      meshBuilders[color].Append(mesh2);
    }
    public void CreateGroundCleanoutMesh(Point3D center, double diameter = 4, double height = 1, Brush color = null) {
      Brush actualColor = color ?? Brushes.Silver;
      if (!meshBuilders.ContainsKey(actualColor))
        meshBuilders[actualColor] = new MeshBuilder(false, false);

      var meshBuilder = new MeshBuilder(false, false);

      meshBuilder.AddCylinder(center, new Point3D(center.X, center.Y, center.Z + height), diameter / 2, 32, true, true);

      meshBuilder.AddCylinder(new Point3D(center.X, center.Y, center.Z + height), new Point3D(center.X, center.Y, center.Z + height + 0.2), diameter / 3, 32, true, true);

      var mesh = meshBuilder.ToMesh();
      meshBuilders[actualColor].Append(mesh);
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
    public string ViewportId { get; set; } = "";

    public ObservableCollection<Scene> Scenes { get; set; } = new ObservableCollection<Scene>();

    public bool IsCalculatorEnabled { get; set; } = false;
    public Dictionary<string, WaterCalculator> WaterCalculators { get; set; } = new Dictionary<string, WaterCalculator>();
    public Dictionary<string, GasCalculator> GasCalculators { get; set; } = new Dictionary<string, GasCalculator>();
    public Dictionary<string, PlumbingPlanBasePoint> BasePointLookup { get; set; } = new Dictionary<string, PlumbingPlanBasePoint>();
    public string Name { get; set; } = "";
    public ICommand CalculateCommand { get; }
    public ICommand SaveCommand { get; }

    public bool isLoading = false;
    public bool IsLoading {
      get { return isLoading; }
      set {
        isLoading = value;
        OnPropertyChanged(nameof(IsLoading));
      }
    }

    public View(string viewportId, List<PlumbingFullRoute> fullRoutes, Dictionary<string, PlumbingPlanBasePoint> basePointLookup) {
      ViewportId = viewportId;
      CalculateCommand = new RelayCommand(ExecuteCalculate);
      SaveCommand = new RelayCommand(ExecuteSave);
      PlumbingSource source1 = fullRoutes[0].RouteItems[0] as PlumbingSource;
      if (source1 != null) {
        Name = basePointLookup[source1.BasePointId].Plan + ": " + basePointLookup[source1.BasePointId].Type;
      }
      BasePointLookup = basePointLookup;
      FullRoutes = DeepCopyFullRoutes(fullRoutes);
      InitializeView();
    }
    public async void InitializeView() {
      NormalizeRoutes();
      await GenerateWaterCalculators();
      await GenerateGasCalculators();
      GenerateWaterPipeSizing();
      GenerateGasPipeSizing();
      GenerateScenes();
    }

    public void GenerateWaterPipeSizing() {
     // WaterPipeSizingChart chart = new WaterPipeSizingChart();
      foreach (var fullRoute in FullRoutes) {
        if (fullRoute.RouteItems.Count == 0) continue;
        WaterCalculator waterCalculator = null;
        if (fullRoute.RouteItems[0] is PlumbingSource plumbingSource && plumbingSource.TypeId == 1) {
          string sourceId = plumbingSource.Id;
          waterCalculator = WaterCalculators[sourceId];
        }
        if (waterCalculator == null) {
          continue;
        }
        foreach (var item in fullRoute.RouteItems) {
          if (item is PlumbingHorizontalRoute horizontalRoute && (horizontalRoute.Type == "Cold Water" || horizontalRoute.Type == "Hot Water")) {
            bool isHot = false;
            if (horizontalRoute.Type == "Hot Water") {
              isHot = true;
            }
            horizontalRoute.GenerateGallonsPerMinute();
            horizontalRoute.PipeSize = waterCalculator.Chart.FindSize(
              horizontalRoute.PipeType,
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
            verticalRoute.PipeSize = waterCalculator.Chart.FindSize(
              verticalRoute.PipeType,
              isHot,
              verticalRoute.GPM
            );
          }
        }
      }
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
    }
    private async void ExecuteCalculate(object parameter) {
      var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
      ed.WriteMessage("\nCalculating pipe sizes...");
      IsLoading = true;
      GenerateWaterPipeSizing();
      GenerateGasPipeSizing();
      await UploadCalculators();
      RegenerateScenes();
      IsLoading = false;
    }
    private async void ExecuteSave(object parameter) {
      var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
      ed.WriteMessage("\nSaving route info...");
      IsLoading = true;
      await UploadCalculators();
      IsLoading = false;
    }
    public async Task UploadCalculators() {
      var service = ServiceLocator.MariaDBService;
      foreach (var calculator in WaterCalculators.Values) {
        await service.UpdatePlumbingWaterCalculations(calculator);
      }
      foreach (var calculator in GasCalculators.Values) {
        await service.UpdatePlumbingGasCalculations(calculator);
      }
    }
    public async Task GenerateWaterCalculators() {
      WaterCalculators.Clear();
      foreach (var fullRoute in FullRoutes) {

        if (fullRoute.RouteItems[0] is PlumbingSource plumbingSource && plumbingSource.TypeId == 1) {
          IsCalculatorEnabled = true;
          if (!WaterCalculators.ContainsKey(plumbingSource.Id)) {
            ObservableCollection<WaterLoss> waterLosses = new ObservableCollection<WaterLoss>();
            ObservableCollection<WaterAddition> waterAdditions = new ObservableCollection<WaterAddition>();
            double maxLength = FullRoutes
              .Where(r => r.RouteItems.Count > 0
                  && r.RouteItems[0] is PlumbingSource src
                  && src.Id == plumbingSource.Id && r.RouteItems.Last() is PlumbingFixture fixture && IsColdWaterFixture(fixture.BlockName))
              .Max(r => r.Length);
            string name = "Water Meter";
            if (plumbingSource.BlockName == "GMEP PLUMBING POINT OF CONNECTION") {
              name = "Cold Water - Point of Connection";
            }
           
            double minSourcePressure =  plumbingSource.Pressure;
            Tuple<string, double, ObservableCollection<WaterLoss>, ObservableCollection<WaterAddition>, Tuple<int, int, int, int>> info  = await ServiceLocator.MariaDBService.GetPlumbingWaterCalculations(plumbingSource.Id);
            if (info != null) {
              name = info.Item1;
              minSourcePressure = info.Item2;
              waterLosses = info.Item3;
              waterAdditions = info.Item4;
            }
            WaterCalculators[plumbingSource.Id] = new WaterCalculator(plumbingSource.Id, name, minSourcePressure, 0, maxLength / 12, (maxLength * 1.3) / 12, 0, waterLosses, waterAdditions);
            if (info != null) {
              WaterCalculators[plumbingSource.Id].PickChartParameters(info.Item5);
            }
            foreach (var item in fullRoute.RouteItems) {
              string pipeType = "";
              if (item is PlumbingHorizontalRoute hr && !string.IsNullOrEmpty(hr.PipeType))
                pipeType = hr.PipeType;
              else if (item is PlumbingVerticalRoute vr && !string.IsNullOrEmpty(vr.PipeType))
                pipeType = vr.PipeType;
              if (!string.IsNullOrEmpty(pipeType)) {
                switch (pipeType) {
                  case "Copper":
                    WaterCalculators[plumbingSource.Id].EnableCopper = true;
                    break;
                  case "PEX":
                    WaterCalculators[plumbingSource.Id].EnablePex = true;
                    break;
                  case "CPVCSCH80":
                    WaterCalculators[plumbingSource.Id].EnableCPVCSCH80 = true;
                    break;
                  case "CPVCSDRII":
                    WaterCalculators[plumbingSource.Id].EnableCPVCSDRII = true;
                    break;
                }
              }
            }
          }
        }
      }
    }
    public bool IsColdWaterFixture(string blockName) {
      var coldWater = new List<string> {
          "GMEP WH 80",
          "GMEP WH 50",
          "GMEP IWH",
          "GMEP CW FIXTURE POINT",
          "GMEP CP"
      };
      return coldWater.Contains(blockName.ToUpper());
    }
    public async Task GenerateGasCalculators() {
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
            Tuple<string, string, int, string> info = await ServiceLocator.MariaDBService.GetPlumbingGasCalculations(plumbingSource.Id);
            if (info != null) {
              GasPipeSizingChart gasChart = new GasPipeSizingChart(info.Item2, info.Item4, info.Item3);
              GasCalculators[plumbingSource.Id] = new GasCalculator(plumbingSource.Id, info.Item1, gasChart);
            }
            else {
              GasCalculators[plumbingSource.Id] = new GasCalculator(plumbingSource.Id, name);
            }
          }
        }
      }
    }
    public async void GenerateScenes() {
      var service = ServiceLocator.MariaDBService;
      await service.ClearPlumbingRouteInfoBoxes(ViewportId);
      List<RouteInfoBox> allInfoBoxes = new List<RouteInfoBox>();

      Scenes.Clear();
      Scene fullScene = new Scene();
      foreach (var fullRoute in FullRoutes) {
        var scene = new Scene(ViewportId, fullRoute, BasePointLookup);
        allInfoBoxes.AddRange(scene.RouteInfoBoxes);
        Scenes.Add(scene);
        foreach (var visual in scene.RouteVisuals.OrderBy(v => v is ModelVisual3D ? 0 : v is RectangleVisual3D ? 1 : v is TextVisual3D ? 2 : 3)) {
          if (visual is RectangleVisual3D rect) {
            RectangleVisual3D rect2 = DeepCopyRectangleVisual3D(rect);
            fullScene.RouteVisuals.Add(rect2);
          }
          else if (visual is TextVisual3D text) {
            TextVisual3D text2 = DeepCopyTextVisual3D(text);
            fullScene.RouteVisuals.Add(text2);
          }
          else if (visual is ModelVisual3D model) {
            ModelVisual3D model2 = DeepCopyModelVisual3D(model);
            fullScene.RouteVisuals.Add(model2);
          }
        }
      }
      fullScene.RemoveDuplicateRouteVisuals();
      MainScene = fullScene;
      if (ViewportId != "") {
        allInfoBoxes = RemoveDuplicateInfoBoxes(allInfoBoxes);
        await service.InsertPlumbingRouteInfoBoxes(allInfoBoxes, ViewportId);
      }
    }
    public async void RegenerateScenes() {
      var service = ServiceLocator.MariaDBService;
      await service.ClearPlumbingRouteInfoBoxes(ViewportId);
      List<RouteInfoBox> allInfoBoxes = new List<RouteInfoBox>();

      Scene fullScene = new Scene();
      int index = 0;
      foreach (var fullRoute in FullRoutes) {
        Scenes[index].RebuildScene(fullRoute);
        allInfoBoxes.AddRange(Scenes[index].RouteInfoBoxes);

        foreach (var visual in Scenes[index].RouteVisuals.OrderBy(v => v is ModelVisual3D ? 0 : v is RectangleVisual3D ? 1 : v is TextVisual3D ? 2 : 3)) {
          if (visual is RectangleVisual3D rect) {
            RectangleVisual3D rect2 = DeepCopyRectangleVisual3D(rect);
            fullScene.RouteVisuals.Add(rect2);
          }
          else if (visual is TextVisual3D text) {
            TextVisual3D text2 = DeepCopyTextVisual3D(text);
            fullScene.RouteVisuals.Add(text2);
          }
          else if (visual is ModelVisual3D model) {
            ModelVisual3D model2 = DeepCopyModelVisual3D(model);
            fullScene.RouteVisuals.Add(model2);
          }
        }
        index++;
      }
      fullScene.RemoveDuplicateRouteVisuals();
      fullScene.InitialBuild = false;
      MainScene = fullScene;
      if (ViewportId != "") {
        allInfoBoxes = RemoveDuplicateInfoBoxes(allInfoBoxes);
        await service.InsertPlumbingRouteInfoBoxes(allInfoBoxes, ViewportId);
      }
    }
    public List<RouteInfoBox> RemoveDuplicateInfoBoxes(List<RouteInfoBox> infoBoxes) {
      List<RouteInfoBox> boxes = infoBoxes
      .GroupBy(b => new {
        b.ComponentId,
        b.BasePointId,
        b.PipeSize,
        b.Type,
        b.LocationDescription,
        b.SegmentLength,
        b.Units,
        b.LongestRunLength,
        b.DirectionDescription,
        b.IsVerticalRoute
      })
      .Select(g => g.First())
      .ToList();
      return boxes;
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
          else if (item is PlumbingAccessory plumbingAccessory) {
            if (BasePointLookup.TryGetValue(plumbingAccessory.BasePointId, out var basePoint)) {
              plumbingAccessory.Position = new Point3d(
                plumbingAccessory.Position.X - basePoint.Point.X,
                plumbingAccessory.Position.Y - basePoint.Point.Y,
                plumbingAccessory.Position.Z
              );
            }
          }
        }
      }
    }
    public static PlumbingFullRoute DeepCopyFullRoute(PlumbingFullRoute fullRoute) {
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
              plumbingSource.Pressure,
              plumbingSource.BlockName
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
        else if (item is PlumbingAccessory plumbingAccessory) {
          var copy = new PlumbingAccessory(
              plumbingAccessory.Id,
              plumbingAccessory.ProjectId,
              plumbingAccessory.BasePointId,
              new Point3d(plumbingAccessory.Position.X, plumbingAccessory.Position.Y, plumbingAccessory.Position.Z),
              plumbingAccessory.Rotation,
              plumbingAccessory.TypeId,
              plumbingAccessory.Type
          );
          newFullRoute.RouteItems.Add(copy);
        }
      }
      return newFullRoute;
    }
    public static List<PlumbingFullRoute> DeepCopyFullRoutes(List<PlumbingFullRoute> original) {
      var result = new List<PlumbingFullRoute>();
      var newList = new List<PlumbingFullRoute>();
      foreach (var fullRoute in original) {
        PlumbingFullRoute newFullRoute = DeepCopyFullRoute(fullRoute);
        newList.Add(newFullRoute);
      }
      result = newList;
      return result;
    }

    public ModelVisual3D DeepCopyModelVisual3D(ModelVisual3D original) {
      var copy = new ModelVisual3D();

      if (original.Content is Model3D model) {
        copy.Content = model.Clone();
      }

      if (original.Transform != null) {
        copy.Transform = original.Transform.Clone();
      }

      foreach (var child in original.Children) {
        if (child is ModelVisual3D childVisual) {
          copy.Children.Add(DeepCopyModelVisual3D(childVisual));
        }
      }

      return copy;
    }
    public  TextVisual3D DeepCopyTextVisual3D(TextVisual3D original) {
      var copy = new TextVisual3D {
        Position = original.Position,
        Text = original.Text,
        Height = original.Height,
        Foreground = original.Foreground,
        Background = original.Background,
        UpDirection = original.UpDirection,
        TextDirection = original.TextDirection,
        FontFamily = original.FontFamily,
        FontSize = original.FontSize,
        FontWeight = original.FontWeight,
        IsDoubleSided = original.IsDoubleSided,
      };

      // Copy attached properties if used
      var basePointId = TextVisual3DExtensions.GetBasePointId(original);
      if (basePointId != null)
        TextVisual3DExtensions.SetBasePointId(copy, basePointId);

      var isUp = TextVisual3DExtensions.GetIsUp(original);
      if (isUp != null)
        TextVisual3DExtensions.SetIsUp(copy, isUp);

      var type = TextVisual3DExtensions.GetType(original);
      if (type != null)
        TextVisual3DExtensions.SetType(copy, type);

      return copy;
    }
    public  RectangleVisual3D DeepCopyRectangleVisual3D(RectangleVisual3D original) {
      var copy = new RectangleVisual3D {
        Origin = original.Origin,
        Width = original.Width,
        Length = original.Length,
        Normal = original.Normal,
        Fill = original.Fill,
        Material = original.Material,
        BackMaterial = original.BackMaterial,
        Transform = original.Transform != null ? original.Transform.Clone() : null,
      };

      return copy;
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
  public class GPMToFUConverter : IValueConverter {
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
        {0, 21},{6, 23}, {7, 24}, {8, 25}, {9, 26}, {10, 27}, {11, 28}, {12, 29}, {13, 30}, {14, 31},
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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      int flowTypeId = 0;
      if (parameter is string s && int.TryParse(s, out int parsed))
        flowTypeId = parsed;
      else
        return 0;
      var lookup = flowTypeId == 1 ? flushTankDict : flushValveDict;
     
      if (flowTypeId != 1 && flowTypeId != 2) {
        return 0;
      }

      int result = 0;
      bool found = false;
      foreach (var kvp in lookup.Reverse()) {
        if (value is double num && num <= kvp.Value) {
          result = kvp.Key;
          found = true;
        }
      }
      if (!found) {
        result = lookup.Last().Key;
      }
      if (result == 0) {
        return "-";
      }

      return result;
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
  public class ServiceLocator {
    public static MariaDBService MariaDBService { get; } = new MariaDBService();
  }

}
