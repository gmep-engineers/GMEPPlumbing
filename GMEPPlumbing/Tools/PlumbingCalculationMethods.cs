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
using GMEPPlumbing.Views;
using System.Windows.Forms.Integration;
using Autodesk.AutoCAD.Windows;

namespace GMEPPlumbing {
  class PlumbingCalculationMethods {
    public MariaDBService MariaDBService { get; set; } = new MariaDBService();
    public string ProjectId { get; private set; } = string.Empty;
    public List<PlumbingPlanBasePoint> BasePoints { get; private set; } = new List<PlumbingPlanBasePoint>();
    public Dictionary<string, PlumbingPlanBasePoint> BasePointLookup { get; private set; } = new Dictionary<string, PlumbingPlanBasePoint>();
    public List<PlumbingSource> Sources { get; private set; } = new List<PlumbingSource>();
    public List<PlumbingHorizontalRoute> HorizontalRoutes { get; private set; } = new List<PlumbingHorizontalRoute>();
    public List<PlumbingVerticalRoute> VerticalRoutes { get; private set; } = new List<PlumbingVerticalRoute>();
    public Dictionary<string, List<PlumbingFixture>> PlumbingFixtures { get; set; } = new Dictionary<string, List<PlumbingFixture>>();
    public Dictionary<string, List<PlumbingFullRoute>> FullRoutes { get; set; } = new Dictionary<string, List<PlumbingFullRoute>>();
    public Routing RoutingControl { get; set; } = null;
    private PaletteSet pw;


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
        BasePointLookup = BasePoints.ToDictionary(bp => bp.Id, bp => bp);
        FullRoutes.Clear();

        foreach (var source in Sources) {
          var matchingRoutes = HorizontalRoutes.Where(route => route.StartPoint.DistanceTo(source.Position) <= 3.0 && route.BasePointId == source.BasePointId).ToList();

          foreach (var matchingRoute in matchingRoutes) {
            TraverseHorizontalRoute(matchingRoute, null, 0, new List<Object>() { source });
          }
        }
        ed.WriteMessage("\nPlumbing fixture calculation completed successfully.");
        RoutingControl = new Routing(FullRoutes, BasePointLookup);
        var host = new ElementHost();
        host.Child = RoutingControl;
        pw = new PaletteSet("GMEP Plumbing Fixture Calculations");
        pw.Style =
          PaletteSetStyles.ShowAutoHideButton
          | PaletteSetStyles.ShowCloseButton
          | PaletteSetStyles.ShowPropertiesMenu;
        pw.DockEnabled = DockSides.Left | DockSides.Right;

        pw.Size = new System.Drawing.Size(1200, 800);
        pw.MinimumSize = new System.Drawing.Size(1200, 800);
        pw.Add("MyTab", host);
        pw.Visible = true;
        pw.Dock = DockSides.Left;
        pw.RolledUp = false;
      }
      catch (Exception ex) {
        ed.WriteMessage($"\nError: {ex.Message}");
      }
    }
    public void TraverseHorizontalRoute(PlumbingHorizontalRoute route, HashSet<string> visited = null, double fullRouteLength = 0, List<Object> routeObjects = null) {
      if (visited == null)
        visited = new HashSet<string>();

      if (!visited.Add(route.Id))
        return;

      if (routeObjects == null)
        routeObjects = new List<Object>();

      //routeObjects.Add(route);

      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;

      ed.WriteMessage($"\nTraversing horizontal route: {route.Id} from {route.StartPoint} to {route.EndPoint}");
      Dictionary<PlumbingHorizontalRoute, double> childRoutes = FindNearbyHorizontalRoutes(route);
      List<PlumbingVerticalRoute> verticalRoutes = FindNearbyVerticalRoutes(route);
      List<PlumbingFixture> fixtures = FindNearbyFixtures(route);

      foreach (var childRoute in childRoutes) {
        if (childRoute.Key.Id != route.Id) {
          var routeObjectsTemp = new List<Object>(routeObjects);
          var adjustedRoute = new PlumbingHorizontalRoute(
           route.Id,
           route.ProjectId,
           route.StartPoint,
           getPointAtLength(route.StartPoint, route.EndPoint, childRoute.Value),
           route.BasePointId
          );
          routeObjectsTemp.Add(adjustedRoute);
          TraverseHorizontalRoute(childRoute.Key, visited, fullRouteLength + childRoute.Value, routeObjectsTemp);
        }
      }
      foreach (var verticalRoute in verticalRoutes) {
        double length;
        List<Object> routeObjectsTemp = new List<Object>(routeObjects);
        routeObjectsTemp.Add(route);
        SortedDictionary<int, PlumbingVerticalRoute> verticalRouteObjects = null;
        var verticalRouteEnd = FindVerticalRouteEnd(verticalRoute, out length, out verticalRouteObjects);
        foreach (var vr in verticalRouteObjects) {
          routeObjectsTemp.Add(vr.Value);
        }
        TraverseVerticalRoute(verticalRouteEnd, visited, fullRouteLength + route.StartPoint.DistanceTo(route.EndPoint) + length, routeObjectsTemp);
      }
      foreach(var fixture in fixtures) {
        List<Object> routeObjectsTemp = new List<Object>(routeObjects);
        routeObjectsTemp.Add(route);
        routeObjectsTemp.Add(fixture);

        int typeId = 0;
        if (routeObjectsTemp[0] is PlumbingSource source) {
          typeId = source.TypeId;
        }

        double lengthInInches = fullRouteLength + route.StartPoint.DistanceTo(route.EndPoint);
        PlumbingFullRoute fullRoute = new PlumbingFullRoute();
        fullRoute.Length = lengthInInches;
        fullRoute.RouteItems = routeObjectsTemp;
        fullRoute.TypeId = typeId;

        if (!FullRoutes.ContainsKey(BasePointLookup[fixture.BasePointId].ViewportId)) {
          FullRoutes[BasePointLookup[fixture.BasePointId].ViewportId] = new List<PlumbingFullRoute>();
        }

        FullRoutes[BasePointLookup[fixture.BasePointId].ViewportId].Add(fullRoute);

        int feet = (int)(lengthInInches / 12);
        int inches = (int)Math.Round(lengthInInches % 12);
        ed.WriteMessage($"\nFixture {fixture.FixtureId} at {fixture.Position} with route length of {feet} feet {inches} inches.");
      }
    }
    public void TraverseVerticalRoute(PlumbingVerticalRoute route, HashSet<string> visited = null, double fullRouteLength = 0, List<Object> routeObjects = null) {
      if (visited == null)
        visited = new HashSet<string>();

      if (!visited.Add(route.Id))
        return;

      if (routeObjects == null)
        routeObjects = new List<Object>();

      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      double routeLength = 0;
      if (route.NodeTypeId == 3) {
        routeLength = -route.Length*12;
      }
      else if (route.NodeTypeId == 1) {
        routeLength = route.Length*12;
      }
      Vector3d direction = new Vector3d(0, 0, routeLength);
      ed.WriteMessage($"\nTraversing vertical route: {route.Id} at position {route.Position}");
      List<PlumbingHorizontalRoute> childRoutes = HorizontalRoutes
        .Where(r => r.BasePointId == route.BasePointId && (r.StartPoint.DistanceTo(route.ConnectionPosition) <= 3.0 || r.StartPoint.DistanceTo(route.Position + direction) <= 3.0))
        .ToList();
      foreach (var childRoute in childRoutes) {
        TraverseHorizontalRoute(childRoute, visited, fullRouteLength, routeObjects);
      }
    }
    private Point3d getPointAtLength(Point3d start, Point3d end, double length) {
      var direction = end - start;
      var totalLength = direction.Length;
      if (totalLength == 0) return start; // Avoid division by zero
      var ratio = length / totalLength;
      return start + (direction * ratio);
    }

    private double GetPointToSegmentDistance(Point3d pt, Point3d segStart, Point3d segEnd, out double segmentLength) {
      var v = segEnd - segStart;
      var w = pt - segStart;

      double c1 = v.DotProduct(w);
      if (c1 <= 0) {
        segmentLength = 0;
        return pt.DistanceTo(segStart);
      }

      double c2 = v.DotProduct(v);
      if (c2 <= c1) {
        segmentLength = v.Length;
        return pt.DistanceTo(segEnd);
      }

      double b = c1 / c2;
      var pb = segStart + (v * b);
      segmentLength = pb.DistanceTo(segStart);
      return pt.DistanceTo(pb);
    }

    public Dictionary<PlumbingHorizontalRoute, double> FindNearbyHorizontalRoutes(PlumbingHorizontalRoute targetRoute) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      var result = new Dictionary<PlumbingHorizontalRoute, double>();
      foreach (var route in HorizontalRoutes) {
        if (route.Id == targetRoute.Id || route.BasePointId != targetRoute.BasePointId)
          continue;

        double segmentLength;
        double distance = GetPointToSegmentDistance(
            route.StartPoint, targetRoute.StartPoint, targetRoute.EndPoint, out segmentLength
        );

        if (distance <= 3.0) {
          result[route] = segmentLength;
        }
      }
      return result;
    }
    public List<PlumbingVerticalRoute> FindNearbyVerticalRoutes(PlumbingHorizontalRoute targetRoute) {
      return VerticalRoutes.Where(route => targetRoute.EndPoint.DistanceTo(route.ConnectionPosition) <= 3.0 && route.BasePointId == targetRoute.BasePointId).ToList();
    }
    public List<PlumbingFixture> FindNearbyFixtures(PlumbingHorizontalRoute targetRoute) {
      return PlumbingFixtures.Values
       .SelectMany(list => list)
       .Where(fixture => targetRoute.EndPoint.DistanceTo(fixture.Position) <= 1.0 && fixture.BasePointId == targetRoute.BasePointId)
       .GroupBy(fixture => fixture.FixtureId)
       .Select(g => g.First())
       .ToList();
    }

    public PlumbingVerticalRoute FindVerticalRouteEnd(PlumbingVerticalRoute route, out double height, out SortedDictionary<int, PlumbingVerticalRoute> routes) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      routes = GetVerticalRoutesByIdOrdered(route.VerticalRouteId);
 

      var matchingKeys = routes.FirstOrDefault(kvp => kvp.Value.Id == route.Id);
      var startFloor = matchingKeys.Key;

      // Determine end floor key and route
      int endFloorKey;
      if (routes.ElementAt(0).Key == startFloor) {
        endFloorKey = routes.ElementAt(routes.Count - 1).Key;
        ed.WriteMessage($"\nStarting vertical traversal from floor {startFloor} to floor {endFloorKey}");
      }
      else {
        endFloorKey = routes.ElementAt(0).Key;
        ed.WriteMessage($"\nStarting vertical traversal from floor {startFloor} to floor {endFloorKey}");
      }

      var endRoute = routes[endFloorKey];

      height = routes.Sum(kvp => kvp.Value.Length) * 12;
      ed.WriteMessage($"\nTotal vertical route length from floor {startFloor} to floor {endFloorKey} is {height} inches.");

      return endRoute;
    }
    public SortedDictionary<int, PlumbingVerticalRoute> GetVerticalRoutesByIdOrdered(string verticalRouteId) {
      var basePointFloorLookup = BasePoints.ToDictionary(bp => bp.Id, bp => bp.Floor);
      var dict = VerticalRoutes
       .Where(vr => vr.VerticalRouteId == verticalRouteId && basePointFloorLookup.ContainsKey(vr.BasePointId))
       .ToDictionary(
           vr => basePointFloorLookup[vr.BasePointId], // floor
           vr => vr
       );

      return new SortedDictionary<int, PlumbingVerticalRoute>(dict);
    }
  }
}
