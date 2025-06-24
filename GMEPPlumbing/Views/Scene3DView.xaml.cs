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
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace GMEPPlumbing.Views
{
  /// <summary>
  /// Interaction logic for Scene3DView.xaml
  /// </summary>
  public partial class Scene3DView : UserControl {
    public Scene3DView() {
      InitializeComponent();
      this.DataContextChanged += Scene3DView_DataContextChanged;
    }

    private void Scene3DView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      Viewport.Children.Clear();
      if (DataContext is Scene scene) {
        Viewport.Children.Add(new HelixToolkit.Wpf.DefaultLights());
        foreach (var visual in scene.RouteVisuals) {
          Viewport.Children.Add(visual);
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
