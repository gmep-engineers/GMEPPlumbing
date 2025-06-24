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

namespace GMEPPlumbing.Views
{
    /// <summary>
    /// Interaction logic for Routing.xaml
    /// </summary>
    public partial class Routing : UserControl
    {
      public Dictionary<string, List<PlumbingFullRoute>> FullRoutes { get; set; } = new Dictionary<string, List<PlumbingFullRoute>>();

      public Dictionary<string, PlumbingPlanBasePoint> BasePointLookup { get; set; } = new Dictionary<string, PlumbingPlanBasePoint>();
      public Routing(Dictionary<string, List<PlumbingFullRoute>> fullRoutes, Dictionary<string, PlumbingPlanBasePoint> basePointLookup)
      {
        FullRoutes = fullRoutes;
        BasePointLookup = basePointLookup;
        NormalizeRoutes();
        InitializeComponent();
        DataContext = this;
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
    public class InchesToFeetInchesConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is double inches) {
          int feet = (int)(inches / 12);
          int remainingInches = (int)Math.Round(inches % 12);
          return $"{feet} Feet, {remainingInches} Inches";
        }
        return value?.ToString() ?? string.Empty;
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
      }
    }

}
