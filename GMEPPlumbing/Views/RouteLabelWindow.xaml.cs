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
using Autodesk.AutoCAD.Geometry;
using System.Collections.ObjectModel;
using System.ComponentModel;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using System.Windows.Markup;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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
        private Dictionary<string, ObservableCollection<RouteInfoBox>> _selectedRouteInfoBoxes = new Dictionary<string, ObservableCollection<RouteInfoBox>>();
        public Dictionary<string, ObservableCollection<RouteInfoBox>> SelectedRouteInfoBoxes {
          get => _selectedRouteInfoBoxes;
          set {
            _selectedRouteInfoBoxes = value;
            RouteInfoBoxGroups = new ObservableCollection<RouteInfoBoxGroup>(
                value.Select(kvp => new RouteInfoBoxGroup { Key = kvp.Key, Value = kvp.Value })
            );
            OnPropertyChanged(nameof(SelectedRouteInfoBoxes));
          }
        }
        private ObservableCollection<RouteInfoBoxGroup> _routeInfoBoxGroups;
        public ObservableCollection<RouteInfoBoxGroup> RouteInfoBoxGroups {
          get => _routeInfoBoxGroups;
          set {
            _routeInfoBoxGroups = value;
            OnPropertyChanged(nameof(RouteInfoBoxGroups));
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
        public void Select_Click(object sender, RoutedEventArgs e) {
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
          var promptSelectionOptions = new Autodesk.AutoCAD.EditorInput.PromptSelectionOptions();
          var promptSelectionResult = ed.GetSelection(promptSelectionOptions);

          if (promptSelectionResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK) {
            this.Show();
            var selectedIds = promptSelectionResult.Value.GetObjectIds();
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction()) {
              var selectedRouteInfoBoxes = new Dictionary<string, ObservableCollection<RouteInfoBox>>();
              foreach (var objId in selectedIds) {
                var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent is Line line) {
                  ResultBuffer xdata = line.GetXDataForApplication("GMEPPlumbingID");
                  if (xdata != null && xdata.AsArray().Length >= 5) {
                    List<RouteInfoBox> boxes = RouteInfoBoxes.Where(r => r.ComponentId == xdata.AsArray()[1].Value.ToString()).ToList();
                    if (boxes.Count > 0) {
                      if (!selectedRouteInfoBoxes.ContainsKey(xdata.AsArray()[1].Value.ToString())) {
                        selectedRouteInfoBoxes[xdata.AsArray()[1].Value.ToString()] = new ObservableCollection<RouteInfoBox>();
                      }
                      foreach (var box in boxes) {
                        selectedRouteInfoBoxes[xdata.AsArray()[1].Value.ToString()].Add(box);
                      }
                    }
                  }
                }
                if (ent is BlockReference blockReference) {
                  DynamicBlockReferencePropertyCollection pc = blockReference.DynamicBlockReferencePropertyCollection;
                  foreach (DynamicBlockReferenceProperty prop in pc) {
                    if (prop.PropertyName == "id") {
                      List<RouteInfoBox> boxes = RouteInfoBoxes.Where(r => r.ComponentId == prop.Value.ToString()).ToList();
                      if (boxes.Count > 0) {
                        if (!selectedRouteInfoBoxes.ContainsKey(prop.Value.ToString())) {
                          selectedRouteInfoBoxes[prop.Value.ToString()] = new ObservableCollection<RouteInfoBox>();
                        }
                        foreach (var box in boxes) {
                          selectedRouteInfoBoxes[prop.Value.ToString()].Add(box);
                        }
                      }
                    }
                  }
                }
              }
              SelectedRouteInfoBoxes = selectedRouteInfoBoxes;
            }
          }
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
    public class RouteInfoBoxGroup : INotifyPropertyChanged {
      public string Key { get; set; }
      public ObservableCollection<RouteInfoBox> Value { get; set; }

      private RouteInfoBox _selectedRouteInfoBox;
      public RouteInfoBox SelectedRouteInfoBox {
        get => _selectedRouteInfoBox;
        set {
          _selectedRouteInfoBox = value;
          OnPropertyChanged(nameof(SelectedRouteInfoBox));
        }
      }

      public event PropertyChangedEventHandler PropertyChanged;
      protected void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
    }
}
