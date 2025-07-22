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
    public List<PlumbingFixture> PlumbingFixtures { get; set; } = new List<PlumbingFixture>();
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
          List<string> types = GetSourceOutputTypes(source);
          var matchingRoutes = HorizontalRoutes.Where(route => route.StartPoint.DistanceTo(source.Position) <= 13.0 && route.BasePointId == source.BasePointId && types.Contains(route.Type)).ToList();
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
           route.Type,
           route.StartPoint,
           getPointAtLength(route.StartPoint, route.EndPoint, childRoute.Value),
           route.BasePointId
          );
          routeObjectsTemp.Add(adjustedRoute);
          TraverseHorizontalRoute(childRoute.Key, visited, fullRouteLength + childRoute.Value, routeObjectsTemp);
        }
      }
      foreach (var verticalRoute in verticalRoutes) {
        double length = fullRouteLength + route.StartPoint.DistanceTo(route.EndPoint);
        List<Object> routeObjectsTemp = new List<Object>(routeObjects);
        routeObjectsTemp.Add(route);
        SortedDictionary<int, PlumbingVerticalRoute> verticalRouteObjects = GetVerticalRoutesByIdOrdered(verticalRoute.VerticalRouteId);
        int matchingKey = verticalRouteObjects.FirstOrDefault(kvp => kvp.Value.Id == verticalRoute.Id).Key;

        //double newLength = ((verticalRoute.Length * 12) - Math.Abs(verticalRouteObjects[matchingKey].Position.Z - route.EndPoint.Z)) / 12;
        double entryPointZ = route.EndPoint.Z;
        double newLength = Math.Abs(verticalRoute.Position.Z - entryPointZ) / 12.0;
        double newLength2 = Math.Abs(verticalRoute.Length * 12 - Math.Abs(verticalRoute.Position.Z - entryPointZ)) / 12.0;

        PlumbingVerticalRoute adjustedRoute = new PlumbingVerticalRoute(
          verticalRoute.Id,
          verticalRoute.ProjectId,
          verticalRoute.Type,
          new Point3d(verticalRoute.Position.X, verticalRoute.Position.Y, entryPointZ),
          new Point3d(verticalRoute.Position.X, verticalRoute.Position.Y, entryPointZ),
          verticalRoute.VerticalRouteId,
          verticalRoute.BasePointId,
          verticalRoute.StartHeight,
          newLength2,
          verticalRoute.NodeTypeId
        );
        if (adjustedRoute.NodeTypeId == 3) {
          adjustedRoute.Position = verticalRoute.Position;
          adjustedRoute.ConnectionPosition = verticalRoute.ConnectionPosition;
          adjustedRoute.Length = newLength;
        }

        TraverseVerticalRoute(verticalRoute, entryPointZ, 1, visited, length, routeObjectsTemp);
        routeObjectsTemp.Add(adjustedRoute);
        length += adjustedRoute.Length * 12;

        for (int i = matchingKey + 1; i <= verticalRouteObjects.Count(); i++) {
          TraverseVerticalRoute(verticalRouteObjects[i], verticalRouteObjects[i].Position.Z, 2, visited, length, routeObjectsTemp);
          routeObjectsTemp.Add(verticalRouteObjects[i]);
          length += verticalRouteObjects[i].Length * 12;
        }

        //reset and adjust for going down
        length = fullRouteLength + route.StartPoint.DistanceTo(route.EndPoint);
        routeObjectsTemp = new List<Object>(routeObjects);
        routeObjectsTemp.Add(route);

        adjustedRoute = new PlumbingVerticalRoute(
          verticalRoute.Id,
          verticalRoute.ProjectId,
          verticalRoute.Type,
          new Point3d(verticalRoute.Position.X, verticalRoute.Position.Y, entryPointZ),
          new Point3d(verticalRoute.Position.X, verticalRoute.Position.Y, entryPointZ),
          verticalRoute.VerticalRouteId,
          verticalRoute.BasePointId,
          verticalRoute.StartHeight,
          newLength2,
          verticalRoute.NodeTypeId
        );
        if (adjustedRoute.NodeTypeId != 3) {
          adjustedRoute.Position = verticalRoute.Position;
          adjustedRoute.ConnectionPosition = verticalRoute.ConnectionPosition;
          adjustedRoute.Length = newLength;
        }
        routeObjectsTemp.Add(adjustedRoute);
        length += adjustedRoute.Length * 12;

        for (int i = matchingKey - 1; i > 0; i--) {
          TraverseVerticalRoute(verticalRouteObjects[i], verticalRouteObjects[i].Position.Z, 3, visited, length, routeObjectsTemp);
          routeObjectsTemp.Add(verticalRouteObjects[i]);
          length += verticalRouteObjects[i].Length * 12;
        }
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
        ed.WriteMessage($"\nFixture {fixture.Id} at {fixture.Position} with route length of {feet} feet {inches} inches.");
      }
    }
    public void TraverseVerticalRoute(PlumbingVerticalRoute route, double entryPointZ, int direction, HashSet<string> visited = null, double fullRouteLength = 0, List<Object> routeObjects = null) {
      if (visited == null)
        visited = new HashSet<string>();

      if (!visited.Add(route.Id))
        return;

      if (routeObjects == null)
        routeObjects = new List<Object>();

      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      var routePos = new Point3d(route.Position.X, route.Position.Y, 0);
      double startHeight = route.Position.Z;
      double endHeight = route.Position.Z + (route.Length * 12);
      if (route.NodeTypeId == 3) {
        startHeight = route.Position.Z - (route.Length * 12);
        endHeight = route.Position.Z;
      }
    
      ed.WriteMessage($"\nTraversing vertical route: {route.Id} at position {route.Position}");

      List<PlumbingHorizontalRoute> childRoutes = HorizontalRoutes
        .Where(r => r.Type == route.Type && r.BasePointId == route.BasePointId && (r.StartPoint.DistanceTo(route.ConnectionPosition) <= 3.0 || (routePos.DistanceTo(new Point3d(r.StartPoint.X, r.StartPoint.Y, 0)) <= 3.0 && r.StartPoint.Z >= startHeight && r.EndPoint.Z <= endHeight)))
        .ToList();

      bool isUpRoute = (route.NodeTypeId == 1 || route.NodeTypeId == 2);
      foreach (var childRoute in childRoutes) {
        double newLength = Math.Abs(childRoute.StartPoint.Z - entryPointZ) / 12.0;
        double newLength2 = ((route.Length * 12) - Math.Abs(entryPointZ - childRoute.StartPoint.Z)) / 12;

        bool isUpward = direction == 2;
        if (direction == 1) {
          isUpward = childRoute.StartPoint.Z >= entryPointZ;
          newLength2 = newLength;
        }
        Point3d connectionPosition = route.ConnectionPosition;
        
        if (route.NodeTypeId != 3) {
          new Point3d(childRoute.StartPoint.X, childRoute.StartPoint.Y, entryPointZ);
        }
        PlumbingVerticalRoute adjustedRoute = new PlumbingVerticalRoute(
          route.Id,
          route.ProjectId,
          route.Type,
          new Point3d(route.Position.X, route.Position.Y, entryPointZ),
          connectionPosition,
          route.VerticalRouteId,
          route.BasePointId,
          route.StartHeight,
          newLength,
          route.NodeTypeId
        );
        if (isUpward != isUpRoute) {
          adjustedRoute.Position = new Point3d(route.Position.X, route.Position.Y, childRoute.StartPoint.Z);
          adjustedRoute.ConnectionPosition = new Point3d(childRoute.StartPoint.X, childRoute.StartPoint.Y, childRoute.StartPoint.Z);
          adjustedRoute.Length = newLength2;
        }
        List<object> routeObjectsTemp = new List<object>(routeObjects);
        routeObjectsTemp.Add(adjustedRoute);
        TraverseHorizontalRoute(childRoute, visited, fullRouteLength + (adjustedRoute.Length*12), routeObjectsTemp);
      }
    }
    private Point3d getPointAtLength(Point3d start, Point3d end, double length) {
      var direction = end - start;
      var totalLength = direction.Length;
      if (totalLength == 0) return start; // Avoid division by zero
      var ratio = length / totalLength;
      return start + (direction * ratio);
    }
    private List<string> GetSourceOutputTypes(PlumbingSource source) {
      var types = new List<string>();
      switch (source.TypeId) {
        case 1:
          types.Add("Cold Water");
          break;
        case 2:
          types.Add("Hot Water");
          break;
        case 3:
          types.Add("Gas");
          break;
        case 4:
          types.Add("Waste");
          break;
      }
      return types;
    }

    private List<string> GetFixtureInputTypes(PlumbingFixture fixture) {
      var types = new List<string>();
      switch (fixture.TypeAbbreviation) {
        case "CP":
          types.Add("Hot Water");
          break;
        case "IWH":
        case "WH":
          types.Add("Cold Water");
          break;
        case "L":
        case "U":
        case "WC":
          types.Add("Cold Water");
          types.Add("Waste");
          break;
        case "S":
        case "HS":
        case "MS":
        case "FS":
        case "FD":
          types.Add("Cold Water");
          types.Add("Hot Water");
          types.Add("Waste");
          break;
        case "SWR":
          types.Add("Waste");
          break;
        case "VE":
          types.Add("Vent");
          break;
      }
      return types;
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
      var result = new Dictionary<PlumbingHorizontalRoute, double>();
      foreach (var route in HorizontalRoutes) {
        if (route.Id == targetRoute.Id || route.BasePointId != targetRoute.BasePointId || route.Type != targetRoute.Type)
          continue;

        // 1. Target route's trajectory: endpoint extended in its direction
        Vector3d targetDir = targetRoute.EndPoint - targetRoute.StartPoint;
        if (targetDir.Length > 0) {
          targetDir = targetDir.GetNormal();
          Point3d targetTrajectoryPoint = targetRoute.EndPoint + targetDir * 3.0;
          double segLen;
          double distToRoute = GetPointToSegmentDistance(targetTrajectoryPoint, route.StartPoint, route.EndPoint, out segLen);
          if (distToRoute <= 3.0) {
            Point3d closestPoint = GetClosestPointOnSegment(targetTrajectoryPoint, route.StartPoint, route.EndPoint);
            var adjustedRoute = new PlumbingHorizontalRoute(
                route.Id,
                route.ProjectId,
                route.Type,
                closestPoint, // new start point
                route.EndPoint,
                route.BasePointId
            );
            result[adjustedRoute] = targetRoute.StartPoint.DistanceTo(targetRoute.EndPoint);
            continue;
          }
        }

        // Candidate route's reverse trajectory: startpoint extended backward
        Vector3d routeDir = route.EndPoint - route.StartPoint;
        if (routeDir.Length > 0) {
          routeDir = routeDir.GetNormal();
          Point3d routeReverseTrajectoryPoint = route.StartPoint - routeDir * 3.0;
          double segLen;
          double distToTarget = GetPointToSegmentDistance(routeReverseTrajectoryPoint, targetRoute.StartPoint, targetRoute.EndPoint, out segLen);
          if (distToTarget <= 3.0) {
            Point3d closestPoint = GetClosestPointOnSegment(routeReverseTrajectoryPoint, route.StartPoint, route.EndPoint);
            var adjustedRoute = new PlumbingHorizontalRoute(
                route.Id,
                route.ProjectId,
                route.Type,
                closestPoint, // new start point
                route.EndPoint,
                route.BasePointId
            );
            result[adjustedRoute] = segLen;
            continue;
          }
        }
        // 3. Segments intersect
        Point3d intersectionPoint;
        if (DoSegmentsIntersect(targetRoute.StartPoint, targetRoute.EndPoint, route.StartPoint, route.EndPoint, out intersectionPoint)) {
          double segLen;
          GetPointToSegmentDistance(intersectionPoint, targetRoute.StartPoint, targetRoute.EndPoint, out segLen);
          var adjustedRoute = new PlumbingHorizontalRoute(
                route.Id,
                route.ProjectId,
                route.Type,
                intersectionPoint, // new start point
                route.EndPoint,
                route.BasePointId
            );
          result[adjustedRoute] = segLen;
        }
      }
      return result;
    }
    // Helper: Find the closest point on a segment to a given point
    private Point3d GetClosestPointOnSegment(Point3d pt, Point3d segStart, Point3d segEnd) {
      var v = segEnd - segStart;
      var w = pt - segStart;

      double c1 = v.DotProduct(w);
      if (c1 <= 0)
        return segStart;

      double c2 = v.DotProduct(v);
      if (c2 <= c1)
        return segEnd;

      double b = c1 / c2;
      return segStart + (v * b);
    }
    private bool DoSegmentsIntersect(Point3d p1, Point3d p2, Point3d q1, Point3d q2, out Point3d intersectionPoint) {
      // 2D intersection (ignoring Z)
      double x1 = p1.X, y1 = p1.Y, x2 = p2.X, y2 = p2.Y;
      double x3 = q1.X, y3 = q1.Y, x4 = q2.X, y4 = q2.Y;

      double d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
      if (Math.Abs(d) < 1e-10) {
        intersectionPoint = default(Point3d);
        return false; // Parallel or colinear
      }

      double pre = (x1 * y2 - y1 * x2), post = (x3 * y4 - y3 * x4);
      double x = (pre * (x3 - x4) - (x1 - x2) * post) / d;
      double y = (pre * (y3 - y4) - (y1 - y2) * post) / d;

      // Check if intersection is within both segments
      if (x < Math.Min(x1, x2) - 1e-10 || x > Math.Max(x1, x2) + 1e-10 ||
          x < Math.Min(x3, x4) - 1e-10 || x > Math.Max(x3, x4) + 1e-10 ||
          y < Math.Min(y1, y2) - 1e-10 || y > Math.Max(y1, y2) + 1e-10 ||
          y < Math.Min(y3, y4) - 1e-10 || y > Math.Max(y3, y4) + 1e-10) {
        intersectionPoint = default(Point3d);
        return false;
      }

      // Use Z from the first segment's start point (or set to 0 if you want 2D)
      intersectionPoint = new Point3d(x, y, p1.Z);
      return true;
    }
    public List<PlumbingVerticalRoute> FindNearbyVerticalRoutes(PlumbingHorizontalRoute targetRoute) {
      var doc = Application.DocumentManager.MdiActiveDocument;
      var db = doc.Database;
      var ed = doc.Editor;
      Point3d endPoint = new Point3d(targetRoute.EndPoint.X, targetRoute.EndPoint.Y, 0);

      return VerticalRoutes.Where(route => {
        if (route.BasePointId == targetRoute.BasePointId && (route.Type == targetRoute.Type || (route.Type == "Vent" && targetRoute.Type == "Waste"))) {
          ed.WriteMessage($"\nChecking vertical route {route.Id} for target route {targetRoute.Id}");
          if (targetRoute.EndPoint.DistanceTo(route.ConnectionPosition) < 3.0) {
            return true;
          }
          Point3d routePos = new Point3d(route.Position.X, route.Position.Y, 0);
          double startHeight = route.Position.Z;
          double endHeight = route.Position.Z + (route.Length * 12); // Convert feet to inches
          if (route.NodeTypeId == 3) {
            startHeight = route.Position.Z - (route.Length * 12); // For down routes, adjust start height
            endHeight = route.Position.Z;
          }
          if (targetRoute.EndPoint.Z >= startHeight && targetRoute.EndPoint.Z <= endHeight && endPoint.DistanceTo(routePos) < 3.0) {
            return true;
          }
        }
        return false;
      }
      ).ToList();
    }
    public List<PlumbingFixture> FindNearbyFixtures(PlumbingHorizontalRoute targetRoute) {
      return PlumbingFixtures.Select(list => list)
       .Where(fixture => targetRoute.EndPoint.DistanceTo(fixture.Position) <= 8.0 && fixture.BasePointId == targetRoute.BasePointId && GetFixtureInputTypes(fixture).Contains(targetRoute.Type))
       .GroupBy(fixture => fixture.Id)
       .Select(g => g.First())
       .ToList();
    }

    /*public PlumbingVerticalRoute FindVerticalRouteEnd(PlumbingVerticalRoute route, out double height, out SortedDictionary<int, PlumbingVerticalRoute> routes) {
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

      height = (routes.Sum(kvp => kvp.Value.Length) * 12) + 6;
      ed.WriteMessage($"\nTotal vertical route length from floor {startFloor} to floor {endFloorKey} is {height} inches.");

      return endRoute;
    }*/
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
