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
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) {
          PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
