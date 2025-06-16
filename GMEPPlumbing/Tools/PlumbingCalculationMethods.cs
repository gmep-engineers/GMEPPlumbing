using Autodesk.AutoCAD.Runtime;
using GMEPPlumbing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Exception = System.Exception;

namespace GMEPPlumbing {
  class PlumbingCalculationMethods {
    public MariaDBService MariaDBService { get; set; } = new MariaDBService();
    public string ProjectId { get; private set; } = string.Empty;

    public List<PlumbingPlanBasePoint> BasePoints { get; private set; } = new List<PlumbingPlanBasePoint>();
    public List<PlumbingSource> Sources { get; private set; } = new List<PlumbingSource>();
    public List<PlumbingHorizontalRoute> HorizontalRoutes { get; private set; } = new List<PlumbingHorizontalRoute>();
    public List<PlumbingVerticalRoute> VerticalRoutes { get; private set; } = new List<PlumbingVerticalRoute>();
    public Dictionary<string, List<PlumbingFixture>> PlumbingFixtures { get; set; } = new Dictionary<string, List<PlumbingFixture>>();


    [CommandMethod("PlumbingFixtureCalc")]
    public async void PlumbingFixtureCalc() {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;


      try {
        string projectNo = CADObjectCommands.GetProjectNoFromFileName();
        ProjectId = await MariaDBService.GetProjectId(projectNo);
        Sources = await MariaDBService.GetPlumbingSources(ProjectId);
        HorizontalRoutes = await MariaDBService.GetPlumbingHorizontalRoutes(ProjectId);
        VerticalRoutes = await MariaDBService.GetPlumbingVerticalRoutes(ProjectId);
        PlumbingFixtures = await MariaDBService.GetPlumbingFixtures(ProjectId);
        BasePoints = await MariaDBService.GetPlumbingPlanBasePoints(ProjectId);

        /*foreach (var source in Sources) {
          var matchingRoute = HorizontalRoutes
          .FirstOrDefault(route => route.StartPoint.DistanceTo(source.Position) <= 3.0);
          if (matchingRoute != null) {
            ed.WriteMessage($"\nSource {source.Id} is connected to horizontal route {matchingRoute.Id}.");
          }
        }*/
      }
      catch (Exception ex) {
        ed.WriteMessage($"\nError: {ex.Message}");
      }
    }

  }
}
