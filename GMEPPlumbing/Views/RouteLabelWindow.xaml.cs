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
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;
using static Mysqlx.Crud.Order.Types;

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
            RouteInfoBoxGroupBunches = new ObservableCollection<RouteInfoBoxGroupBunch>(
              value.Select(kvp => new RouteInfoBoxGroupBunch 
              {
                RouteInfoBoxGroups = new ObservableCollection<RouteInfoBoxGroup>
                {
                  new RouteInfoBoxGroup { Key = kvp.Key, Value = kvp.Value }
                }
              })
            );
            OnPropertyChanged(nameof(SelectedRouteInfoBoxes));
          }
        }
        private ObservableCollection<RouteInfoBoxGroupBunch> _routeInfoBoxGroupBunches;
        public ObservableCollection<RouteInfoBoxGroupBunch> RouteInfoBoxGroupBunches {
          get => _routeInfoBoxGroupBunches;
          set {
            _routeInfoBoxGroupBunches = value;
            OnPropertyChanged(nameof(RouteInfoBoxGroupBunches));
          }
        }

        public string _labelText = "Label Text";
        public string LabelText {
          get => _labelText;
          set {
            _labelText = value;
            OnPropertyChanged(nameof(LabelText));
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) {
          PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    private void GoToLocation_Click(object sender, RoutedEventArgs e) {
      var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      var ed = doc.Editor;
      var button = sender as Button;
      var routeInfoBoxGroup = button?.CommandParameter as RouteInfoBoxGroup;
      if (routeInfoBoxGroup != null) {
        string key = routeInfoBoxGroup.Key;
        PlumbingVerticalRoute verticalRoute = AutoCADIntegration.GetVerticalRoutesFromCAD()
            .FirstOrDefault(r => r.Id == key);
        if (verticalRoute != null) {
          AutoCADIntegration.ZoomToPoint(ed, verticalRoute.Position, 10);
        }
        else {
          PlumbingHorizontalRoute horizontalRoute = AutoCADIntegration.GetHorizontalRoutesFromCAD()
            .FirstOrDefault(r => r.Id == key);
          if (horizontalRoute != null) {
            var midpoint = new Point3d(
                (horizontalRoute.StartPoint.X + horizontalRoute.EndPoint.X) / 2.0,
                (horizontalRoute.StartPoint.Y + horizontalRoute.EndPoint.Y) / 2.0,
                (horizontalRoute.StartPoint.Z + horizontalRoute.EndPoint.Z) / 2.0
            );
            AutoCADIntegration.ZoomToPoint(ed, midpoint, 10);
          }
        }
      }
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
  public class RouteInfoBoxGroupBunch: INotifyPropertyChanged {
    public ObservableCollection<RouteInfoBoxGroup> RouteInfoBoxGroups { get; set; }
    public string _labelText = "";
    public string LabelText {
      get => _labelText;
      set {
        _labelText = value;
        OnPropertyChanged(nameof(LabelText));
      }
    }
    public void GenerateLabel(object sender, RoutedEventArgs e) {
      var selectedBoxes = RouteInfoBoxGroups
          .Where(g => g.SelectedRouteInfoBox != null)
          .Select(g => g.SelectedRouteInfoBox)
          .ToList();

      var pipeSizeGroups = selectedBoxes.Where(b => !string.IsNullOrEmpty(b.PipeSize))
          .GroupBy(b => b.PipeSize);

      var labelParts = new List<string>();
      foreach (var pipeSizeGroup in pipeSizeGroups) {
        var sizeParts = new List<string>();
        var pipeSize = pipeSizeGroup.Key;
        if (pipeSize != pipeSizeGroups.First().Key) {
          sizeParts.Add("&");
        }
        sizeParts.Add(pipeSize);

        var typeGroups = pipeSizeGroup.Where(b => !string.IsNullOrEmpty(b.Type))
            .GroupBy(b => b.Type);
        foreach (var typeGroup in typeGroups) {
          var typeParts = new List<string>();
          var type = typeGroup.Key;
          if (type != typeGroups.First().Key) {
            typeParts.Add("&");
          }
          typeParts.Add(type);

          switch (type) {
            case "Cold Water": type = "cw"; break;
            case "Hot Water": type = "hw"; break;
            case "Waste": type = "w"; break;
            case "Vent": type = "v"; break;
            case "Gas": type = "g"; break;
            case "Grease Waste": type = "gw"; break;
          }
          var directionGroups = typeGroup.Where(b => !string.IsNullOrEmpty(b.DirectionDescription))
              .GroupBy(b => b.DirectionDescription);
          foreach (var directionGroup in directionGroups) {
            var directionParts = new List<string>();
            var direction = directionGroup.Key;
            if (direction != directionGroups.First().Key) {
              directionParts.Add("&");
            }
            else {
              directionParts.Add(" ");
            }
            directionParts.Add(direction);
            var locationGroups = directionGroup.Where(b => !string.IsNullOrEmpty(b.LocationDescription))
                .GroupBy(b => b.LocationDescription);
            foreach (var locationGroup in locationGroups) {
              var location = locationGroup.Key;
              if (location != locationGroups.First().Key) {
                directionParts.Add("&");
              }
              else {
                directionParts.Add(" ");
              }
              directionParts.Add(string.Join("", location));
            }
            typeParts.Add(string.Join("", directionParts));
          }
          sizeParts.Add(string.Join("", typeParts));
        }
        labelParts.Add(string.Join("", sizeParts));
      }

      // Final label
      LabelText = string.Join("", labelParts).ToUpper();
      OnPropertyChanged(nameof(LabelText));
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

  }
}
