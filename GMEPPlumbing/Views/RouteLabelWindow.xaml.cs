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
          RouteInfoBoxes = await MariaDBService.GetPlumbingRouteInfoBoxes(BasePointId);
          DrawDiagram();
        }
        public void DrawDiagram() {

          double scale = 2.0; // Adjust as needed
          double canvasCenterX = DiagramCanvas.ActualWidth / 2.0;
          double canvasCenterY = DiagramCanvas.ActualHeight / 2.0;

          foreach (var box in RouteInfoBoxes) {
            double x1 = box.StartPosition.X * scale + canvasCenterX;
            double y1 = -box.StartPosition.Y * scale + canvasCenterY;
            double x2 = box.EndPosition.X * scale + canvasCenterX;
            double y2 = -box.EndPosition.Y * scale + canvasCenterY;

            var line = new System.Windows.Shapes.Line {
              X1 = x1,
              Y1 = y1,
              X2 = x2,
              Y2 = y2,
              Stroke = Brushes.DarkSlateBlue,
              StrokeThickness = 2
            };
          }
        }

    public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) {
          PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
