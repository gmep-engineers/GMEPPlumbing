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
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HelixToolkit.Wpf;
using static Mysqlx.Crud.Order.Types;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.EditorInput;

namespace GMEPPlumbing.Views
{
  /// <summary>
  /// Interaction logic for Scene3DView.xaml
  /// </summary>
  public partial class Scene3DView : UserControl {
    private TextVisual3D _highlightedText;
    private Brush _originalBrush;
    public Scene3DView() {
      InitializeComponent();
      this.DataContextChanged += Scene3DView_DataContextChanged;
      this.Viewport.MouseLeftButtonDown += Viewport_MouseLeftButtonDown;
      this.Viewport.MouseMove += Viewport_MouseMove;
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
    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      var mousePos = e.GetPosition(Viewport);
      var windowPos = Viewport.PointToScreen(mousePos);
      var hitResult = Viewport.Viewport.FindHits(mousePos).FirstOrDefault();

      if (hitResult != null && hitResult.Visual is TextVisual3D textVisual && textVisual.Background == Brushes.LightGray) {
        PopupText.Text = $"{textVisual.Text}";
        InfoPopup.PlacementTarget = Viewport;
        InfoPopup.HorizontalOffset = windowPos.X;
        InfoPopup.VerticalOffset = windowPos.Y;
        InfoPopup.IsOpen = true;
        e.Handled = true;
        if (PopupText.Text.Contains("Pipe Size") || PopupText.Text.Contains("Nominal Size")) {
          PlaceOnCad.IsEnabled = true;
          PlaceOnCad.Content = "Place Pipe Size";
          PlaceOnCad.Foreground = Brushes.Black;
        }
        else {
          PlaceOnCad.IsEnabled = false;
          PlaceOnCad.Content = "Pipe Size Required";
          PlaceOnCad.Foreground = Brushes.Crimson;
        }
      }
      else {
        InfoPopup.IsOpen = false;
      }
    }
    private void Viewport_MouseMove(object sender, MouseEventArgs e) {
      var mousePos = e.GetPosition(Viewport);
      var hitResult = Viewport.Viewport.FindHits(mousePos).FirstOrDefault();

      if (_highlightedText != null) {
        _highlightedText.Background = _originalBrush;
        _highlightedText = null;
        _originalBrush = null;
      }

      if (hitResult != null && hitResult.Visual is TextVisual3D textVisual && textVisual.Background == Brushes.White) {
        _highlightedText = textVisual;
        _originalBrush = textVisual.Background;
        textVisual.Background = Brushes.LightGray;
      }
    }

    private void PlaceOnCad_Click(object sender, RoutedEventArgs e) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var db = doc.Database;
      var ed = doc.Editor;
      InfoPopup.IsOpen = false;

      List<PlumbingPlanBasePoint> basePoints =  AutoCADIntegration.GetPlumbingBasePointsFromCAD();
      object basePointIds = TextVisual3DExtensions.GetBasePointId(_highlightedText);
      object isUp = TextVisual3DExtensions.GetIsUp(_highlightedText);
      object type = TextVisual3DExtensions.GetType(_highlightedText);
      if (isUp != null && isUp is bool isUpBool) {
        if (basePointIds != null && basePointIds is string basePointId && type != null && type is string typeText) {
          PlumbingPlanBasePoint basePoint = basePoints.FirstOrDefault(bp => bp.Id == basePointId);
          if (basePoint != null) {
            string pipeSize = "";
            string text = _highlightedText.Text;
            const string marker = "Pipe Size: ";
            int index = text.IndexOf(marker);
            if (index >= 0) {
              pipeSize = text.Substring(index + marker.Length).Trim();
              int newlineIndex = pipeSize.IndexOf('\n');
              if (newlineIndex >= 0)
                pipeSize = pipeSize.Substring(0, newlineIndex).Trim();
            }
            string gasInfo = "";
            if (typeText == "Gas") {
              string cfhMarker = "CFH: ";
              string runMarker = "Longest Run: ";
              int cfhIndex = text.IndexOf(cfhMarker);
              int runIndex = text.IndexOf(runMarker);
              string cfh = "";
              if (cfhIndex >= 0) {
                cfh = text.Substring(cfhIndex + cfhMarker.Length).Trim();
                int newlineIndex = cfh.IndexOf('\n');
                if (newlineIndex >= 0)
                  cfh = cfh.Substring(0, newlineIndex).Trim();
              }
              string run = "";
              if (runIndex >= 0) {
                run = text.Substring(runIndex + runMarker.Length).Trim();
                int newlineIndex = run.IndexOf('\'');
                if (newlineIndex >= 0)
                  run = run.Substring(0, newlineIndex).Trim();
              }
              gasInfo = $"({cfh}CFH@~{run}')";
            }
            ed.WriteMessage(_highlightedText.Position.ToString());
            Point3d placementPoint = basePoint.Point + new Vector3d(_highlightedText.Position.X, _highlightedText.Position.Y, 0);
            AutoCADIntegration.ZoomToPoint(ed, placementPoint);
            string fullText = pipeSize + TypeToAbbreviation(typeText);
            using (var docLock = doc.LockDocument()) {
              CADObjectCommands.CreateTextWithJig(
                CADObjectCommands.TextLayer,
                TextHorizontalMode.TextLeft,
                fullText
              );
              if (typeText == "Gas") {
                CADObjectCommands.CreateTextWithJig(
                  CADObjectCommands.TextLayer,
                  TextHorizontalMode.TextLeft,
                  gasInfo
                );
              }
            }
          }
        }
      }
      else {
        if (basePointIds != null && basePointIds is string basePointId && type != null && type is string typeText) {
          PlumbingPlanBasePoint basePoint = basePoints.FirstOrDefault(bp => bp.Id == basePointId);
          if (basePoint != null) {
            string pipeSize = "";
            string text = _highlightedText.Text;
            const string marker = "Pipe Size: ";
            int index = text.IndexOf(marker);
            if (index >= 0) {
              pipeSize = text.Substring(index + marker.Length).Trim();
              int newlineIndex = pipeSize.IndexOf('\n');
              if (newlineIndex >= 0)
                pipeSize = pipeSize.Substring(0, newlineIndex).Trim();
            }
            string gasInfo = "";
            if (typeText == "Gas") {
              string cfhMarker = "CFH: ";
              string runMarker = "Longest Run: ";
              int cfhIndex = text.IndexOf(cfhMarker);
              int runIndex = text.IndexOf(runMarker);
              string cfh = "";
              if (cfhIndex >= 0) {
                cfh = text.Substring(cfhIndex + cfhMarker.Length).Trim();
                int newlineIndex = cfh.IndexOf('\n');
                if (newlineIndex >= 0)
                  cfh = cfh.Substring(0, newlineIndex).Trim();
              }
              string run = "";
              if (runIndex >= 0) {
                run = text.Substring(runIndex + runMarker.Length).Trim();
                int newlineIndex = run.IndexOf('\'');
                if (newlineIndex >= 0)
                  run = run.Substring(0, newlineIndex).Trim();
              }
              gasInfo = $"({cfh}CFH@~{run}')";
            }
            ed.WriteMessage(_highlightedText.Position.ToString());
            Point3d placementPoint = basePoint.Point + new Vector3d(_highlightedText.Position.X, _highlightedText.Position.Y, 0);
            AutoCADIntegration.ZoomToPoint(ed, placementPoint);
            string fullText = pipeSize + TypeToAbbreviation(typeText); 
            using (var docLock = doc.LockDocument()) {
              CADObjectCommands.CreateTextWithJig(
                CADObjectCommands.TextLayer,
                TextHorizontalMode.TextLeft,
                fullText
              );
              if (typeText == "Gas") {
                CADObjectCommands.CreateTextWithJig(
                  CADObjectCommands.TextLayer,
                  TextHorizontalMode.TextLeft,
                  gasInfo
                );
              }
            }
          }
        }
      }
    }
    public string TypeToAbbreviation(string type) {
    switch(type) {
        case "Cold Water":
          return "CW";
        case "Hot Water":
          return "HW";
        case "Waste":
          return "W";
        case "Grease Waste":
          return "GW";
        case "Vent":
          return "V";
        case "Gas":
          return "G";
        default:
          return "";
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
