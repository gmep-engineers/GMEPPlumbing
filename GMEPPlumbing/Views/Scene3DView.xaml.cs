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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Autodesk.AutoCAD.ApplicationServices;
using HelixToolkit.Wpf;
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
      //this.Viewport.CameraChanged += Viewport_CameraChanged;
    }

    private void Scene3DView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      Viewport.Children.Clear();
      if (DataContext is Scene scene) {
        Viewport.Children.Add(new HelixToolkit.Wpf.SunLight());
        foreach (var visual in scene.RouteVisuals) {
          Viewport.Children.Add(visual);
        }
        Dispatcher.BeginInvoke(new Action(() => Viewport.ZoomExtents()), System.Windows.Threading.DispatcherPriority.Loaded);
      }
    }
    private void Viewport_CameraChanged(object sender, RoutedEventArgs e) {
      // Find all TextVisual3D in the viewport
      foreach (var visual in Viewport.Children) {
        if (visual is TextVisual3D text) {
          FaceTextToCamera(text);
        }
      }
    }

    private void FaceTextToCamera(TextVisual3D text) {
      var camera = Viewport.Camera as ProjectionCamera;
      if (camera == null) return;

      var lookDirection = camera.LookDirection;
      var up = camera.UpDirection;
      var right = Vector3D.CrossProduct(lookDirection, up);
      right.Normalize();

      text.TextDirection = right;
      text.UpDirection = up;
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
