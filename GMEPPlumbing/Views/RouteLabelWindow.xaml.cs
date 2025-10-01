using GMEPPlumbing.Services;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using Autodesk.AutoCAD.Geometry;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace GMEPPlumbing.Views
{
    /// <summary>
    /// Interaction logic for RouteLabelWindow.xaml
    /// </summary>
    public partial class RouteLabelWindow : Window, INotifyPropertyChanged
    {
        public MariaDBService MariaDBService { get; } = new MariaDBService();
        public List<RouteInfoBox> RouteInfoBoxes{ get; set; }
        public string BasePointId { get; set; }
        public PlumbingPlanBasePoint ActiveBasePoint { get; set; }

        private ObservableCollection<RouteInfoBox> _selectedRouteInfoBoxes = new ObservableCollection<RouteInfoBox>();
        public ObservableCollection<RouteInfoBox> SelectedRouteInfoBoxes {
          get => _selectedRouteInfoBoxes;
          set {
            _selectedRouteInfoBoxes = value;
            OnPropertyChanged(nameof(SelectedRouteInfoBoxes));
          }
        }
        public RouteLabelWindow(string basePointId)
        {
          BasePointId = basePointId;
          InitializeComponent();
          DataContext = this;
          Startup();
        }
        public async void Startup() {
          ActiveBasePoint = AutoCADIntegration.GetPlumbingBasePointsFromCAD().FirstOrDefault(bp => bp.Id == BasePointId);
          RouteInfoBoxes = await MariaDBService.GetPlumbingRouteInfoBoxes(BasePointId);
          DrawDiagram();
        }
        public void DrawDiagram() {
          DiagramCanvas.Children.Clear();

          double canvasCenterX = DiagramCanvas.ActualWidth / 2.0;
          double canvasCenterY = DiagramCanvas.ActualHeight / 2.0;

          foreach (var box in RouteInfoBoxes) {
            // Convert CAD coordinates to canvas coordinates (simple scaling for demo)
            double scale = 2.0; // Adjust as needed for your data
            double x1 = box.StartPosition.X * scale + canvasCenterX;
            double y1 = box.StartPosition.Y * scale + canvasCenterY;
            double x2 = box.EndPosition.X * scale + canvasCenterX;
            double y2 = box.EndPosition.Y * scale + canvasCenterY;

            // Draw line
            var line = new System.Windows.Shapes.Line {
              X1 = x1,
              Y1 = y1,
              X2 = x2,
              Y2 = y2,
              Stroke = Brushes.DarkSlateBlue,
              StrokeThickness = 2
            };
            DiagramCanvas.Children.Add(line);

            // Draw start point
           /* var startEllipse = new System.Windows.Shapes.Ellipse {
              Width = 8,
              Height = 8,
              Fill = Brushes.Green
            };
            Canvas.SetLeft(startEllipse, x1 - 4);
            Canvas.SetTop(startEllipse, y1 - 4);
            DiagramCanvas.Children.Add(startEllipse);

            // Draw end point
            var endEllipse = new System.Windows.Shapes.Ellipse {
              Width = 8,
              Height = 8,
              Fill = Brushes.Red
            };
            Canvas.SetLeft(endEllipse, x2 - 4);
            Canvas.SetTop(endEllipse, y2 - 4);
            DiagramCanvas.Children.Add(endEllipse);

            // Add label at end point
            var label = new TextBlock {
              Text = box.Type,
              Foreground = Brushes.Black,
              FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, x2 + 5);
            Canvas.SetTop(label, y2 - 10);
            DiagramCanvas.Children.Add(label);*/
          }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) {
          PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
