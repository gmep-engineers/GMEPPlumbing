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
        }
        public void SelectPoint_Click(object sender, RoutedEventArgs e) {
          // Get the active AutoCAD document and editor
          var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
          var ed = doc.Editor;
          
          this.Hide();
         // Prompt the user to select a point
          PlumbingPlanBasePoint activeBasePoint = AutoCADIntegration.GetPlumbingBasePointsFromCAD()
            .FirstOrDefault(bp => bp.Id == BasePointId);
          if (activeBasePoint == null) {
            MessageBox.Show("Active base point not found.");
            this.Show();
            return;
          }
          var promptPointOptions = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nSelect a point:");
          var promptPointResult = ed.GetPoint(promptPointOptions);

          if (promptPointResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK) {
            var selectedPoint = promptPointResult.Value;
            Vector3d basePointInsertion = selectedPoint - activeBasePoint.Point;
            var adjustedPoint = new Point3d(
              0 + basePointInsertion.X,
              0 + basePointInsertion.Y,
              0
             );

            MessageBox.Show($"Selected Point: X={selectedPoint.X}, Y={selectedPoint.Y}, Z={selectedPoint.Z}");
            // You can now use selectedPoint as needed
            /*var boxes = RouteInfoBoxes
            .Where(r =>
                DistancePointToSegment(adjustedPoint, r.StartPosition, r.EndPosition) <= 3.0
            )
            .ToList();
            if (boxes.Count == 0) {
              MessageBox.Show("No route found near the selected point.");
              this.Show();
              return;
            }
            SelectedRouteInfoBoxes.Clear();
            foreach (var box in boxes) {
              ed.WriteMessage($"\nFound Route with Pipe Size: {box.PipeSize}");
              SelectedRouteInfoBoxes.Add(box);
            }*/
          }
          else {
            MessageBox.Show("Point selection cancelled or failed.");
          }
          this.Show();
        }
        public static double DistancePointToSegment(Point3d pt, Point3d segStart, Point3d segEnd) {
          var v = segEnd - segStart;
          var w = pt - segStart;

          double c1 = v.DotProduct(w);
          if (c1 <= 0)
            return pt.DistanceTo(segStart);

          double c2 = v.DotProduct(v);
          if (c2 <= c1)
            return pt.DistanceTo(segEnd);

          double b = c1 / c2;
          var pb = segStart + (v * b);
          return pt.DistanceTo(pb);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) {
          PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
