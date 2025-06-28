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

      public Dictionary<string, List<Scene>> Scenes { get; set; } = new Dictionary<string, List<Scene>>();
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
          if (!Scenes.ContainsKey(route.Key)) {
            Scenes[route.Key] = new List<Scene>();
          }
          foreach (var fullRoute in route.Value) {
            var scene = new Scene(fullRoute, BasePointLookup);
            Scenes[route.Key].Add(scene);
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
                    new Point3d(hr.StartPoint.X, hr.StartPoint.Y, hr.StartPoint.Z),
                    new Point3d(hr.EndPoint.X, hr.EndPoint.Y, hr.EndPoint.Z),
                    hr.BasePointId
                );
                newFullRoute.RouteItems.Add(copy);
              }
              else if (item is PlumbingVerticalRoute vr) {
                var copy = new PlumbingVerticalRoute(
                    vr.Id,
                    vr.ProjectId,
                    new Point3d(vr.Position.X, vr.Position.Y, vr.Position.Z),
                    new Point3d(vr.ConnectionPosition.X, vr.ConnectionPosition.Y, vr.ConnectionPosition.Z),
                    vr.VerticalRouteId,
                    vr.BasePointId,
                    vr.StartHeight,
                    vr.Length,
                    vr.NodeTypeId
                );
                newFullRoute.RouteItems.Add(copy);
              }
              else if (item is PlumbingSource plumbingSource) {
                var copy = new PlumbingSource(
                    plumbingSource.Id,
                    plumbingSource.ProjectId,
                    new Point3d(plumbingSource.Position.X, plumbingSource.Position.Y, plumbingSource.Position.Z),
                    plumbingSource.TypeId,
                    plumbingSource.BasePointId
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
                    plumbingFixture.FixtureId,
                    plumbingFixture.BlockName
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
      public SolidColorBrush RouteColor { get; set; } = new SolidColorBrush(Color.FromArgb(255, 0, 0, 255)); // Default to blue
      public Scene(PlumbingFullRoute fullRoute, Dictionary<string, PlumbingPlanBasePoint> basePoints) {
          RouteItems = fullRoute.RouteItems;
          Length = fullRoute.Length;
          BasePoints = basePoints;
          switch (fullRoute.TypeId) {
            case 1:
              RouteColor = System.Windows.Media.Brushes.Yellow;
              break;
            case 2:
              RouteColor = System.Windows.Media.Brushes.Magenta;
              break;
            case 3:
              RouteColor = System.Windows.Media.Brushes.SteelBlue;
              break;
          }
          BuildScene();
      }
      public void BuildScene() {
      RouteVisuals.Clear();
        foreach (var item in RouteItems) {
        Visual3D model = null;
         if (item is PlumbingHorizontalRoute horizontalRoute) {
          ModelVisual3D fullModel = new ModelVisual3D();
          var ballModel2 = new SphereVisual3D {
            Center = new Point3D(horizontalRoute.StartPoint.X, horizontalRoute.StartPoint.Y, horizontalRoute.StartPoint.Z),
            Radius = 1,
            Fill = RouteColor
          };
          var lineModel = new TubeVisual3D {
            Path = new Point3DCollection {
              new Point3D(horizontalRoute.StartPoint.X, horizontalRoute.StartPoint.Y, horizontalRoute.StartPoint.Z),
              new Point3D(horizontalRoute.EndPoint.X, horizontalRoute.EndPoint.Y, horizontalRoute.EndPoint.Z)
            },
            Diameter = 2,
            Fill = RouteColor
          };
          var ballModel = new SphereVisual3D {
            Center = new Point3D(horizontalRoute.EndPoint.X, horizontalRoute.EndPoint.Y, horizontalRoute.EndPoint.Z),
            Radius = 1,
            Fill = RouteColor
          };
       
          fullModel.Children.Add(ballModel2);
          fullModel.Children.Add(lineModel);
          fullModel.Children.Add(ballModel);
          
          model = fullModel;
          BasePointIds.Add(horizontalRoute.BasePointId);
        }
        else if (item is PlumbingVerticalRoute verticalRoute) {
          double length = verticalRoute.Length * 12;
          if (verticalRoute.NodeTypeId == 3) {
            length = -length;
          }
          model = new TubeVisual3D {
            Path = new Point3DCollection {
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z),
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z + length)
            },
            Diameter = 2,
            Fill = RouteColor
          };
          BasePointIds.Add(verticalRoute.BasePointId);
        }
        else if (item is PlumbingSource plumbingSource) {
          model = new SphereVisual3D {
            Center = new Point3D(plumbingSource.Position.X, plumbingSource.Position.Y, plumbingSource.Position.Z),
            Radius = 2,
            Fill = RouteColor
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

        var textModel = new TextVisual3D {
          Position = new Point3D(0, 0, BasePoints[basePoint].FloorHeight * 12 + 0.5), // Slightly above the rectangle
          Text = $"Floor {BasePoints[basePoint].Floor}",
          Height = 10, // Size of the text
          Foreground = Brushes.White,
          UpDirection = new Vector3D(0, 1, 0), // Text facing up
          Background = Brushes.Transparent // Or Brushes.White for a background
        };
        RouteVisuals.Add(textModel);
      }
    }
    }
}
