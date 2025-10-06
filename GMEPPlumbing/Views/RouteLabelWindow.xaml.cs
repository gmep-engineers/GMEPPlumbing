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
using GongSolutions.Wpf.DragDrop;

namespace GMEPPlumbing.Views
{
    /// <summary>
    /// Interaction logic for RouteLabelWindow.xaml
    /// </summary>
    public partial class RouteLabelWindow : Window, INotifyPropertyChanged, IDropTarget
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
                  new RouteInfoBoxGroup(kvp.Value) { Key = kvp.Key }
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
    private void GenerateLabel_Click(object sender, RoutedEventArgs e) {
      var button = sender as Button;
      var routeInfoBoxGroupBunch = button?.CommandParameter as RouteInfoBoxGroupBunch;
      if (routeInfoBoxGroupBunch != null) {
        routeInfoBoxGroupBunch.GenerateLabel(sender, e);
      }
    }
    public void DragOver(IDropInfo dropInfo) {
      if (dropInfo.Data is RouteInfoBoxGroup && dropInfo.TargetCollection != null) {
        dropInfo.Effects = DragDropEffects.Move;
        dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
      }
    }

    public void Drop(IDropInfo dropInfo) {
      var sourceGroup = dropInfo.Data as RouteInfoBoxGroup;
      var targetCollection = dropInfo.TargetCollection as ObservableCollection<RouteInfoBoxGroup>;
      var sourceCollection = dropInfo.DragInfo.SourceCollection as ObservableCollection<RouteInfoBoxGroup>;

      if (sourceGroup != null && targetCollection != null && sourceCollection != null) {
        if (targetCollection.Count == 0 || targetCollection.First().RouteType == sourceGroup.RouteType) {
          if (targetCollection.First().Type == sourceGroup.Type || (targetCollection.First().Type == "Cold Water" && sourceGroup.Type == "Hot Water") || (targetCollection.First().Type == "Hot Water" && sourceGroup.Type == "Cold Water")) {
            if (targetCollection.First().LocationDescription == sourceGroup.LocationDescription && targetCollection.First().SourceDescription == sourceGroup.SourceDescription) {
              sourceCollection.Remove(sourceGroup);
              targetCollection.Add(sourceGroup);
            }
            else {
              MessageBox.Show("You can only group vertical routes from the same location and to the same location.", "Invalid Grouping", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
          }
          else {
            MessageBox.Show("You can only group routes of the same type together.", "Invalid Grouping", MessageBoxButton.OK, MessageBoxImage.Warning);
          }
        }
        else {
          MessageBox.Show("You can only group routes of the same type together.", "Invalid Grouping", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
      }
    }
    public void DragEnter(IDropInfo dropInfo) { }
    public void DragLeave(IDropInfo dropInfo) { }
    public void DropHint(IDropHintInfo dropHintInfo) { }
    public void Place_Click(object sender, RoutedEventArgs e) {
      var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
      var button = sender as Button;
      var routeInfoBoxGroupBunch = button?.CommandParameter as RouteInfoBoxGroupBunch;
      if (routeInfoBoxGroupBunch != null) {
        using (var docLock = doc.LockDocument()) {
          routeInfoBoxGroupBunch.PlaceLabel();
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
      public string Type { get; set; }
      public string RouteType { get; set; }
      public SolidColorBrush SourceColor { get; set; } = System.Windows.Media.Brushes.Black;
      public string LocationDescription { get; set; }
      public string SourceDescription { get; set; }

    public RouteInfoBoxGroup(ObservableCollection<RouteInfoBox> boxes) {
      Value = boxes;
      DetermineInfo();
    }

    public event PropertyChangedEventHandler PropertyChanged;
      protected void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
      public void DetermineInfo() {
        Type = Value.First().Type;
        LocationDescription = Value.First().LocationDescription;
        SourceDescription = Value.First().SourceDescription;
        if (Value.First().IsVerticalRoute) {
          if (Value.First().DirectionDescription == "Up") {
            RouteType = "Vertical Up";
          }
          else {
            RouteType = "Vertical Down";
          }
        }
        else {
          RouteType = "Horizontal";
        }
        switch (Type) {
          case "Cold Water":
            SourceColor = System.Windows.Media.Brushes.Yellow;
            break;
          case "Grease Waste":
          case "Hot Water":
            SourceColor = System.Windows.Media.Brushes.Magenta;
            break;
          case "Gas":
            SourceColor = System.Windows.Media.Brushes.SteelBlue;
            break; ;
          case "Waste":
            SourceColor = System.Windows.Media.Brushes.Cyan;
            break;
          case "Vent":
            SourceColor = System.Windows.Media.Brushes.Green;
            break;
        }
      }
  }
  public class RouteInfoBoxGroupBunch: INotifyPropertyChanged {
    public ObservableCollection<RouteInfoBoxGroup> RouteInfoBoxGroups { get; set; }
    public string _locationLabelText = "";
    public string LocationLabelText {
      get => _locationLabelText;
      set {
        _locationLabelText = value;
        OnPropertyChanged(nameof(LocationLabelText));
      }
    }
    public string _additionalLabelText = "";
    public string AdditionalLabelText {
      get => _additionalLabelText;
      set {
        _additionalLabelText = value;
        OnPropertyChanged(nameof(AdditionalLabelText));
      }
    }
    public string _sourceLabelText = "";
    public string SourceLabelText {
      get => _sourceLabelText;
      set {
        _sourceLabelText = value;
        OnPropertyChanged(nameof(SourceLabelText));
      }
    }

    public void GenerateLabel(object sender, RoutedEventArgs e) {
      var selectedBoxes = RouteInfoBoxGroups
          .Where(g => g.SelectedRouteInfoBox != null)
          .Select(g => g.SelectedRouteInfoBox)
          .ToList();
      
      LocationLabelText = CreateLabelString(selectedBoxes);
      if (RouteInfoBoxGroups.First().SourceDescription != "") {
        SourceLabelText = CreateLabelString(selectedBoxes, true);
      }
      else {
        SourceLabelText = "";
      }

      //Gas Stuffs
      string additionalLabels = "";
      bool endLineFlag = false;
      foreach (var box in selectedBoxes.Where(b => b.Type == "Gas")) {
        if (endLineFlag) {
          additionalLabels += "\n";
        }
        else {
          endLineFlag = true;
        }
        additionalLabels += $"({box.CFH}CFH@~{box.LongestRunLength})";
      }
      AdditionalLabelText = additionalLabels.ToUpper();


      // Final label
      OnPropertyChanged(nameof(LocationLabelText));
      OnPropertyChanged(nameof(SourceLabelText));
      OnPropertyChanged(nameof(AdditionalLabelText));

    }
    public string CreateLabelString(List<RouteInfoBox> selectedBoxes, bool isSource = false) {
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
          switch (type) {
            case "Cold Water": type = "cw"; break;
            case "Hot Water": type = "hw"; break;
            case "Waste": type = "w"; break;
            case "Vent": type = "v"; break;
            case "Gas": type = "g"; break;
            case "Grease Waste": type = "gw"; break;
          }
          typeParts.Add(type);
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
            if (isSource) {
              locationGroups = directionGroup.Where(b => !string.IsNullOrEmpty(b.SourceDescription))
                .GroupBy(b => b.SourceDescription);
            }
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
      return string.Join("", labelParts).ToUpper();
    }
    public void PlaceLabel() {
      List<PlumbingVerticalRoute> routes = new List<PlumbingVerticalRoute>();
      if (LocationLabelText != "") {
        if (RouteInfoBoxGroups.First().RouteType != "Horizontal") {
          routes = AutoCADIntegration.GetVerticalRoutesFromCAD();
          PlumbingVerticalRoute route = routes.FirstOrDefault(r => r.Id == RouteInfoBoxGroups.First().Key);
          Point3d insertionPoint = CADObjectCommands.CreateArrowJig("D0", route.Position).Item1;

          var tempGroups = RouteInfoBoxGroups.ToList();
          tempGroups.Remove(RouteInfoBoxGroups.First());
          foreach (var group in tempGroups) {
            if (group.RouteType == "Vertical Up" || group.RouteType == "Vertical Down") {
              PlumbingVerticalRoute route2 = routes.FirstOrDefault(r => r.Id == group.Key);
              CADObjectCommands.CreateArrowJig("D0", route2.Position, false, insertionPoint);
            }
          }
        }
        CADObjectCommands.CreateTextWithJig(
          CADObjectCommands.TextLayer,
          TextHorizontalMode.TextLeft,
          LocationLabelText
        );
      }

      if (SourceLabelText != "") {
        if (RouteInfoBoxGroups.First().RouteType != "Horizontal") {
          PlumbingVerticalRoute route = routes.FirstOrDefault(r => r.Id == RouteInfoBoxGroups.First().Key);
          Point3d insertionPoint = CADObjectCommands.CreateArrowJig("D0", route.Position).Item1;

          var tempGroups = RouteInfoBoxGroups.ToList();
          tempGroups.Remove(RouteInfoBoxGroups.First());
          foreach (var group in tempGroups) {
            if (group.RouteType == "Vertical Up" || group.RouteType == "Vertical Down") {
              PlumbingVerticalRoute route2 = routes.FirstOrDefault(r => r.Id == group.Key);
              CADObjectCommands.CreateArrowJig("D0", route2.Position, false, insertionPoint);
            }
          }
        }
        CADObjectCommands.CreateTextWithJig(
          CADObjectCommands.TextLayer,
          TextHorizontalMode.TextLeft,
         SourceLabelText
        );
      }
      List<string> lines = AdditionalLabelText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
      foreach (var line in lines) {
        CADObjectCommands.CreateTextWithJig(
          CADObjectCommands.TextLayer,
          TextHorizontalMode.TextLeft,
          line
        );
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

  }
}
