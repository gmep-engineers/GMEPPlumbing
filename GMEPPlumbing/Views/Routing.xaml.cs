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
        FullRoutes = fullRoutes;
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
            var scene = new Scene(fullRoute);
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
            }
          }
        }
      }
    }
    
    public class Scene {
      public List<object> RouteItems { get; set; } = new List<object>();
      public double Length { get; set; } = 0;
      public ObservableCollection<Visual3D> RouteVisuals { get; set; } = new ObservableCollection<Visual3D>();
      public Scene(PlumbingFullRoute fullRoute) {
          RouteItems = fullRoute.RouteItems;
          Length = fullRoute.Length;
          BuildScene();
      }
      public void BuildScene() {
        RouteVisuals.Clear();
        foreach (var item in RouteItems) {
        Visual3D model = null;
         if (item is PlumbingHorizontalRoute horizontalRoute) {
          model = new TubeVisual3D {
            Path = new Point3DCollection {
              new Point3D(horizontalRoute.StartPoint.X, horizontalRoute.StartPoint.Y, horizontalRoute.StartPoint.Z),
              new Point3D(horizontalRoute.EndPoint.X, horizontalRoute.EndPoint.Y, horizontalRoute.EndPoint.Z)
            },
            Diameter = 2,
            Fill = System.Windows.Media.Brushes.SteelBlue
          };
        }
        else if (item is PlumbingVerticalRoute verticalRoute) {
          model = new TubeVisual3D {
            Path = new Point3DCollection {
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z),
              new Point3D(verticalRoute.Position.X, verticalRoute.Position.Y, verticalRoute.Position.Z + (verticalRoute.Length*12))
            },
            Diameter = 2,
            Fill = System.Windows.Media.Brushes.SteelBlue
          };
        }
        if (model != null) {
          RouteVisuals.Add(model);
        }
      }
      }
    }
}
