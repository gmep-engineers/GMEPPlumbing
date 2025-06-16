using Autodesk.AutoCAD.Runtime;
using GMEPPlumbing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Exception = System.Exception;
using Autodesk.AutoCAD.Geometry;

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

        foreach (var source in Sources) {
          var matchingRoutes = HorizontalRoutes
          .Where(route => route.StartPoint.DistanceTo(source.Position) <= 3.0)
          .ToList();

          foreach (var matchingRoute in matchingRoutes) {
            TraverseHorizonalRoute(matchingRoute);
          }
        }
      }
      catch (Exception ex) {
        ed.WriteMessage($"\nError: {ex.Message}");
      }
    }
    public void TraverseHorizonalRoute(PlumbingHorizontalRoute route) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      ed.WriteMessage($"\nTraversing horizontal route: {route.Id} from {route.StartPoint} to {route.EndPoint}");
      List<PlumbingHorizontalRoute> childRoutes = FindNearbyHorizontalRoutes(route);
      foreach(var childRoute in childRoutes) {
        if (childRoute.Id != route.Id) {
          TraverseHorizonalRoute(childRoute);
        }
      }
    }


    private double GetPointToSegmentDistance(Point3d pt, Point3d segStart, Point3d segEnd) {
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
    public List<PlumbingHorizontalRoute> FindNearbyHorizontalRoutes(PlumbingHorizontalRoute targetRoute) {
      return HorizontalRoutes
          .Where(route => route.Id != targetRoute.Id &&
              GetPointToSegmentDistance(
                  route.StartPoint, targetRoute.StartPoint, targetRoute.EndPoint) <= 3.0)
          .ToList();
    }
  }
}
