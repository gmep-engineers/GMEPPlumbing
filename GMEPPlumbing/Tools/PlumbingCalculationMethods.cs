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

    public Dictionary<string, double> LengthToFixtures { get; set; } = new Dictionary<string, double>();


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
          .Where(route => route.StartPoint.DistanceTo(source.Position) <= 3.0 && route.BasePointId == source.BasePointId)
          .ToList();

          foreach (var matchingRoute in matchingRoutes) {
            TraverseHorizontalRoute(matchingRoute);
          }
        }
      }
      catch (Exception ex) {
        ed.WriteMessage($"\nError: {ex.Message}");
      }
    }
    public void TraverseHorizontalRoute(PlumbingHorizontalRoute route, HashSet<string> visited = null, double fullRouteLength = 0) {
     
      fullRouteLength += route.StartPoint.DistanceTo(route.EndPoint);
      
      if (visited == null)
        visited = new HashSet<string>();

      if (!visited.Add(route.Id))
        return;

      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      ed.WriteMessage($"\nTraversing horizontal route: {route.Id} from {route.StartPoint} to {route.EndPoint}");
      List<PlumbingHorizontalRoute> childRoutes = FindNearbyHorizontalRoutes(route);
      List<PlumbingVerticalRoute> verticalRoutes = FindNearbyVerticalRoutes(route);
      List<PlumbingFixture> fixtures = FindNearbyFixtures(route);

      foreach (var childRoute in childRoutes) {
        if (childRoute.Id != route.Id) {
          TraverseHorizontalRoute(childRoute, visited);
        }
      }
      foreach (var verticalRoute in verticalRoutes) {
        var verticalRouteEnd = FindVerticalRouteEnd(verticalRoute);
        TraverseVerticalRoute(verticalRouteEnd, visited);
      }
      foreach(var fixture in fixtures) {
        if (!LengthToFixtures.ContainsKey(fixture.FixtureId)) {
          LengthToFixtures[fixture.FixtureId] = fullRouteLength;
        }
        else {
          LengthToFixtures[fixture.FixtureId] += fullRouteLength;
        }
        ed.WriteMessage($"\nFixture {fixture.FixtureId} at {fixture.Position} with length {LengthToFixtures[fixture.FixtureId]}");
      }
    }
    public void TraverseVerticalRoute(PlumbingVerticalRoute route, HashSet<string> visited = null) {
      if (visited == null)
        visited = new HashSet<string>();

      if (!visited.Add(route.Id))
        return;

      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      ed.WriteMessage($"\nTraversing vertical route: {route.Id} at position {route.Position}");
      List<PlumbingHorizontalRoute> childRoutes = HorizontalRoutes
        .Where(r => r.BasePointId == route.BasePointId && r.StartPoint.DistanceTo(route.ConnectionPosition) <= 3.0)
        .ToList();
      foreach (var childRoute in childRoutes) {
        TraverseHorizontalRoute(childRoute, visited);
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
                  route.StartPoint, targetRoute.StartPoint, targetRoute.EndPoint) <= 3.0 && route.BasePointId == targetRoute.BasePointId)
          .ToList();
    }
    public List<PlumbingVerticalRoute> FindNearbyVerticalRoutes(PlumbingHorizontalRoute targetRoute) {
      return VerticalRoutes.Where(route => targetRoute.EndPoint.DistanceTo(route.ConnectionPosition) <= 3.0 && route.BasePointId == targetRoute.BasePointId).ToList();
    }
    public List<PlumbingFixture> FindNearbyFixtures(PlumbingHorizontalRoute targetRoute) {
      return PlumbingFixtures.Values.SelectMany(list => list).Where(fixture => targetRoute.EndPoint.DistanceTo(fixture.Position) <= 3.0 && fixture.BasePointId == targetRoute.BasePointId).ToList();
    }

    public PlumbingVerticalRoute FindVerticalRouteEnd(PlumbingVerticalRoute route) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      Dictionary<int, PlumbingVerticalRoute> routes = GetVerticalRoutesByIdOrdered(route.VerticalRouteId);
      var matchingKeys = routes.FirstOrDefault(kvp => kvp.Value.Id == route.Id);
      var startFloor = matchingKeys.Key;
      

      if (routes.ElementAt(0).Key == startFloor) {
        ed.WriteMessage($"\nStarting vertical traversal from floor {startFloor} to floor {routes.ElementAt(routes.Count - 1).Key}");
        return routes.ElementAt(routes.Count - 1).Value;
      }
      ed.WriteMessage($"\nStarting vertical traversal from floor {startFloor} to floor {routes.ElementAt(0).Key}");
      return routes.ElementAt(0).Value;
    }
    public Dictionary<int, PlumbingVerticalRoute> GetVerticalRoutesByIdOrdered(string verticalRouteId) {
      var basePointFloorLookup = BasePoints.ToDictionary(bp => bp.Id, bp => bp.Floor);
      var dict = VerticalRoutes
       .Where(vr => vr.VerticalRouteId == verticalRouteId && basePointFloorLookup.ContainsKey(vr.BasePointId))
       .ToDictionary(
           vr => basePointFloorLookup[vr.BasePointId], // floor
           vr => vr
       );
      return dict;
    }
  }
}
