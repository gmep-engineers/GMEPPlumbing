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

namespace GMEPPlumbing.Views
{
    /// <summary>
    /// Interaction logic for Routing.xaml
    /// </summary>
    public partial class Routing : UserControl
    {
      public Dictionary<string, List<PlumbingFullRoute>> FullRoutes { get; set; } = new Dictionary<string, List<PlumbingFullRoute>>();

      public Dictionary<string, PlumbingPlanBasePoint> BasePointLookup { get; set; } = new Dictionary<string, PlumbingPlanBasePoint>();

      public Dictionary<string, Tuple<Scene, List<Scene>>> Scenes { get; set; } = new Dictionary<string, Tuple<Scene, List<Scene>>>();
      public Routing(Dictionary<string, List<PlumbingFullRoute>> fullRoutes, Dictionary<string, PlumbingPlanBasePoint> basePointLookup)
      {
        FullRoutes = DeepCopyFullRoutes(fullRoutes);
        BasePointLookup = basePointLookup;
        NormalizeRoutes();
        GenerateScenes();
        InitializeComponent();
        DataContext = this;
      }
      public void GenerateScenes() {
        Scenes.Clear();
        foreach (var route in FullRoutes) {
          Scene fullScene = new Scene();
          List<Scene> sceneList = new List<Scene>();  
          List<Scene> sceneList2 = new List<Scene>();
          foreach (var fullRoute in route.Value) {
            var scene = new Scene(fullRoute, BasePointLookup);
            var scene2 = new Scene(fullRoute, BasePointLookup);
            sceneList.Add(scene);
            sceneList2.Add(scene2);
          }
          foreach (var scene in sceneList2) {
            foreach(var visual in scene.RouteVisuals) {
              fullScene.RouteVisuals.Add(visual);
            }
            fullScene.RemoveDuplicateRouteVisuals();
            Scenes[route.Key] = new Tuple<Scene, List<Scene>>(fullScene, sceneList);
        }
        }
      }

      public void NormalizeRoutes() {
        foreach (var fullRoute in FullRoutes.Values) {
          foreach (var route in fullRoute) {
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
                  verticalRoute.ConnectionPosition = new Point3d(
                    verticalRoute.ConnectionPosition.X - basePoint.Point.X,
                    verticalRoute.ConnectionPosition.Y - basePoint.Point.Y,
                    verticalRoute.ConnectionPosition.Z
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
              else if  (item is PlumbingFixture plumbingFixture) {
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
      }
      public static Dictionary<string, List<PlumbingFullRoute>> DeepCopyFullRoutes(Dictionary<string, List<PlumbingFullRoute>> original) {
        var result = new Dictionary<string, List<PlumbingFullRoute>>();
        foreach (var kvp in original) {
          var newList = new List<PlumbingFullRoute>();
          foreach (var fullRoute in kvp.Value) {
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
                    hr.PipeType
                );
                copy.FixtureUnits = hr.FixtureUnits;
                newFullRoute.RouteItems.Add(copy);
              }
              else if (item is PlumbingVerticalRoute vr) {
                var copy = new PlumbingVerticalRoute(
                    vr.Id,
                    vr.ProjectId,
                    vr.Type,
                    new Point3d(vr.Position.X, vr.Position.Y, vr.Position.Z),
                    new Point3d(vr.ConnectionPosition.X, vr.ConnectionPosition.Y, vr.ConnectionPosition.Z),
                    vr.VerticalRouteId,
                    vr.BasePointId,
                    vr.StartHeight,
                    vr.Length,
                    vr.NodeTypeId,
                    vr.PipeType,
                    vr.IsUp
                );
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
          result[kvp.Key] = newList;
        }
        return result;
      }
    }
    
    public class Scene {
      public List<object> RouteItems { get; set; } = new List<object>();
      public double Length { get; set; } = 0;
      public ObservableCollection<Visual3D> RouteVisuals { get; set; } = new ObservableCollection<Visual3D>();
      public Dictionary<string, PlumbingPlanBasePoint> BasePoints { get; set; } = new Dictionary<string, PlumbingPlanBasePoint>();
      public HashSet<string> BasePointIds = new HashSet<string>();
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

    public void BuildScene() {
      RouteVisuals.Clear();
      List<TextVisual3D> textVisuals = new List<TextVisual3D>();
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
          double fixtureUnits = horizontalRoute.FixtureUnits;

          double textHeight = 8;
          string textString = $"{feet}' {inches}\"\n Fixture Units: {horizontalRoute.FixtureUnits}";
          double textWidth = textHeight * textString.Length * 0.15;

          // Offset so the back of the text aligns with the end point
          var textPos = new Point3D(
              horizontalRoute.EndPoint.X - (direction.X * (textWidth / 2)),
              horizontalRoute.EndPoint.Y - (direction.Y * (textWidth / 2)),
              horizontalRoute.EndPoint.Z + 5 // +5 for vertical offset
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
          textVisuals.Add(textModel);

          fullModel.Children.Add(ballModel2);
          fullModel.Children.Add(lineModel);
          fullModel.Children.Add(ballModel);

          model = fullModel;
          BasePointIds.Add(horizontalRoute.BasePointId);
        }
        else if (item is PlumbingVerticalRoute verticalRoute) {
          ModelVisual3D fullModel = new ModelVisual3D();

          var ballModel = new SphereVisual3D {
            Center = new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z),
            Radius = 1,
            Fill = TypeToBrushColor(verticalRoute.Type)
          };
         var connectionTubeModel = new TubeVisual3D {
            Path = new Point3DCollection {
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z),
              new Point3D(verticalRoute.ConnectionPosition.X, verticalRoute.ConnectionPosition.Y, verticalRoute.ConnectionPosition.Z)
            },
            Diameter = 2,
            Fill = TypeToBrushColor(verticalRoute.Type)
         };

          double length = verticalRoute.Length * 12;

          if (verticalRoute.NodeTypeId == 3) {
            length = -length;
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
            fullModel.Children.Add(connectionTubeModel);
            fullModel.Children.Add(ballModel);
          }

          var start = new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z);
          var end = new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z + length);
          var mid = new Point3D(
              (start.X + end.X) / 2.0,
              (start.Y + end.Y) / 2.0,
              (start.Z + end.Z) / 2.0
          );

          // Offset 2 units in the positive X direction (right side)
          var textPos = new Point3D(mid.X + 4, mid.Y, mid.Z);

          // Text direction is down (negative Z)
          var textDirection = new Vector3D(0, 0, -1);

          // Up direction is positive X (right side)
          var upDirection = new Vector3D(1, 0, 0);

          // Calculate pipe length in feet/inches
          double pipeLength = start.DistanceTo(end);
          int feet = (int)(pipeLength / 12);
          int inches = (int)Math.Round(pipeLength % 12);

          var textModel = new TextVisual3D {
            Position = textPos,
            Text = $"{feet}' {inches}\"",
            Height = 8,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            TextDirection = textDirection,
            UpDirection = upDirection
          };

          textVisuals.Add(textModel);

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
}
