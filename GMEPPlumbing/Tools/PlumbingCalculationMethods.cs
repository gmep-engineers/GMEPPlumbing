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
        ed.WriteMessage($"\nStack Trace: {ex.StackTrace}");
        if (ex.InnerException != null) {
          ed.WriteMessage($"\nInner Exception: {ex.InnerException.Message}");
          ed.WriteMessage($"\nInner Stack Trace: {ex.InnerException.StackTrace}");
        }
      }
    }
    public Tuple<double, int, double> TraverseHorizontalRoute(PlumbingHorizontalRoute route, HashSet<string> visited = null, double fullRouteLength = 0, List<Object> routeObjects = null) {
      if (visited == null)
        visited = new HashSet<string>();

      if (!visited.Add(route.Id))
        return new Tuple<double, int, double>(0, 1, 0);

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

      double fixtureUnits = 0;
      int flowTypeId = 1;
      double longestRunLength = 0;


      //Fixtures
      foreach (var fixture in fixtures) {
        PlumbingFixtureCatalogItem catalogItem = MariaDBService.GetPlumbingFixtureCatalogItemById(fixture.CatalogId);
        ed.WriteMessage($"\nFixture {fixture.Id} has a demand of {catalogItem.FixtureDemand} fixture units.");
        double units = (double)catalogItem.FixtureDemand;
        if (GetFixtureInputTypes(fixture).Contains("Gas")) {
          units = catalogItem.Cfh;
        }
        if (GetFixtureInputTypes(fixture).Contains("Waste")) {
          units = catalogItem.Dfu;
        }
        fixtureUnits += units;
        flowTypeId = (flowTypeId == 2) ? 2 : fixture.FlowTypeId;

        List<Object> routeObjectsTemp = new List<Object>(routeObjects);
        //will change later to include all fixture units for fixtures on the horizontal route(although is that possible?). For now, just single fixture.
        route.FixtureUnits = units;
        route.FlowTypeId = flowTypeId;
        routeObjectsTemp.Add(route);
        routeObjectsTemp.Add(fixture);

        int typeId = 0;
        if (routeObjectsTemp[0] is PlumbingSource source) {
          typeId = source.TypeId;
        }

        double lengthInInches = fullRouteLength + route.StartPoint.DistanceTo(route.EndPoint);
        longestRunLength = Math.Max(longestRunLength, lengthInInches);
        route.LongestRunLength = longestRunLength;

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

    
      //Vertical Routes
      foreach (var verticalRoute in verticalRoutes) {
        double length = fullRouteLength + route.StartPoint.DistanceTo(route.EndPoint);
        List<Object> routeObjectsTemp = new List<Object>(routeObjects);
        routeObjectsTemp.Add(route);
        SortedDictionary<int, PlumbingVerticalRoute> verticalRouteObjects = GetVerticalRoutesByIdOrdered(verticalRoute.VerticalRouteId, verticalRoute.IsUp, verticalRoute.BasePointId);
        ed.WriteMessage($"\nTraversing vertical route: {verticalRoute.Id} with {verticalRouteObjects.Count} segments.");
        int matchingKey = verticalRouteObjects.FirstOrDefault(kvp => kvp.Value.Id == verticalRoute.Id).Key;

        double entryPointZ = route.EndPoint.Z;

        double endVerticalRoute = 0;
        if (verticalRoute.IsUp) {
          if (verticalRoute.NodeTypeId != 3) {
            endVerticalRoute = verticalRoute.Position.Z + (verticalRoute.Length * 12);
          }
          else {
            endVerticalRoute = verticalRoute.Position.Z;
          }
        }
        else {
          if (verticalRoute.NodeTypeId == 3) {
            endVerticalRoute = verticalRoute.Position.Z - (verticalRoute.Length * 12);
          }
          else {
            endVerticalRoute = verticalRoute.Position.Z;
          }
        }
        double newLength = Math.Abs(endVerticalRoute - entryPointZ) / 12.0;

        PlumbingVerticalRoute adjustedRoute = new PlumbingVerticalRoute(
         verticalRoute.Id,
         verticalRoute.ProjectId,
         verticalRoute.Type,
         verticalRoute.Position,
         verticalRoute.VerticalRouteId,
         verticalRoute.BasePointId,
         verticalRoute.StartHeight,
         newLength,
         verticalRoute.NodeTypeId,
         verticalRoute.PipeType,
         verticalRoute.IsUp
       );
        bool isUpRoute = (verticalRoute.NodeTypeId == 1 || verticalRoute.NodeTypeId == 2);
        if (verticalRoute.IsUp == isUpRoute) {
          adjustedRoute.Position = new Point3d(verticalRoute.Position.X, verticalRoute.Position.Y, entryPointZ);
        }
        routeObjectsTemp.Add(adjustedRoute);
        length += adjustedRoute.Length * 12;
        //adding all entries to be taken away from
        foreach (var kvp in verticalRouteObjects.Reverse()) {
          routeObjectsTemp.Add(kvp.Value);
          length += kvp.Value.Length * 12;
      }
      foreach (var kvp in verticalRouteObjects) {
          var verticalRoute2 = kvp.Value;
          if (adjustedRoute.IsUp) {
            if (verticalRoute2.NodeTypeId == 3) {
              entryPointZ = verticalRoute2.Position.Z - (verticalRoute2.Length * 12);
            }
            else {
              entryPointZ = verticalRoute2.Position.Z;
            }
          }
          else {
            if (verticalRoute2.NodeTypeId == 3) {
              entryPointZ = verticalRoute2.Position.Z;
            }
            else {
              entryPointZ = verticalRoute2.Position.Z + (verticalRoute2.Length * 12);
            }
          }
          routeObjectsTemp.Remove(routeObjectsTemp.Last());
          length -= kvp.Value.Length * 12;
          Tuple<double, int, double> verticalRoute2Result = TraverseVerticalRoute(verticalRoute2, entryPointZ, fixtureUnits, flowTypeId, longestRunLength, visited, length, routeObjectsTemp);
          fixtureUnits += verticalRoute2Result.Item1;
          flowTypeId = (flowTypeId == 2) ? 2 : verticalRoute2Result.Item2;
          longestRunLength = Math.Max(longestRunLength, verticalRoute2Result.Item3);
        }
        entryPointZ = route.EndPoint.Z;
        routeObjectsTemp.Remove(routeObjectsTemp.Last());
        length -= adjustedRoute.Length * 12;
        Tuple<double, int, double> verticalRouteResult = TraverseVerticalRoute(adjustedRoute, entryPointZ, fixtureUnits, flowTypeId, longestRunLength, visited, length, routeObjectsTemp);
        fixtureUnits += verticalRouteResult.Item1;
        flowTypeId = (flowTypeId == 2) ? 2 : verticalRouteResult.Item2;
        longestRunLength = Math.Max(longestRunLength, verticalRouteResult.Item3);
        route.FixtureUnits = fixtureUnits;
        route.FlowTypeId = flowTypeId;
        route.LongestRunLength = longestRunLength;
      }

      //Horizontal Routes
      foreach (var childRoute in childRoutes) {
        if (childRoute.Key.Id != route.Id) {
          var routeObjectsTemp = new List<Object>(routeObjects);
          var adjustedRoute = new PlumbingHorizontalRoute(
           route.Id,
           route.ProjectId,
           route.Type,
           route.StartPoint,
           getPointAtLength(route.StartPoint, route.EndPoint, childRoute.Value),
           route.BasePointId,
           route.PipeType
          );
          routeObjectsTemp.Add(adjustedRoute);
          Tuple<double, int, double> childRouteResult = TraverseHorizontalRoute(childRoute.Key, visited, fullRouteLength + childRoute.Value, routeObjectsTemp);
          // Add to the total for this route
          fixtureUnits += childRouteResult.Item1;
          flowTypeId = (flowTypeId == 2) ? 2 : childRouteResult.Item2;
          longestRunLength = Math.Max(longestRunLength, childRouteResult.Item3);
          adjustedRoute.FixtureUnits = fixtureUnits;
          adjustedRoute.FlowTypeId = flowTypeId;
          adjustedRoute.LongestRunLength = longestRunLength;
        }
      }



      return new Tuple<double, int, double>(fixtureUnits, flowTypeId, longestRunLength);
     
    }
    public Tuple<double, int, double> TraverseVerticalRoute(PlumbingVerticalRoute route, double entryPointZ, double fixtureUnits, int flowTypeId, double longestRunLength, HashSet<string> visited = null, double fullRouteLength = 0, List<Object> routeObjects = null) {
      if (visited == null)
        visited = new HashSet<string>();

      if (!visited.Add(route.Id))
        return new Tuple<double, int, double>(0, 1, 0);

      if (routeObjects == null)
        routeObjects = new List<Object>();

      double fixtureUnitsSoFar = 0;
      int flowTypeIdTemp = flowTypeId;

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

      double startZ = entryPointZ;
      double endZ = 0;
      if (route.IsUp) {
        endZ = endHeight;
      }
      else {
        endZ = startHeight;
      }

      ed.WriteMessage($"\nTraversing vertical route: {route.Id} at position {route.Position}");

      List<PlumbingHorizontalRoute> childRoutes = HorizontalRoutes
        .Where(r => (r.Type == route.Type || (r.Type == "Waste" && route.Type == "Vent")) && r.BasePointId == route.BasePointId && routePos.DistanceTo(new Point3d(r.StartPoint.X, r.StartPoint.Y, 0)) <= 3.0 && r.StartPoint.Z >= Math.Min(startZ, endZ) && r.StartPoint.Z <= Math.Max(startZ, endZ))
        .OrderByDescending(r => Math.Abs(r.StartPoint.Z - startZ))
        .ToList();

      bool isUpRoute = (route.NodeTypeId == 1 || route.NodeTypeId == 2);
      foreach (var childRoute in childRoutes) {
        double newLength = Math.Abs(startZ - childRoute.StartPoint.Z) / 12.0;
  
        PlumbingVerticalRoute adjustedRoute = new PlumbingVerticalRoute(
          route.Id,
          route.ProjectId,
          route.Type,
          route.Position,
          route.VerticalRouteId,
          route.BasePointId,
          route.StartHeight,
          newLength,
          route.NodeTypeId,
          route.PipeType,
          route.IsUp
        );
        if (route.IsUp != isUpRoute) {
          adjustedRoute.Position = new Point3d(route.Position.X, route.Position.Y, childRoute.StartPoint.Z);
        }

        List<object> routeObjectsTemp = new List<object>(routeObjects);
        routeObjectsTemp.Add(adjustedRoute);

        Tuple<double, int, double> childRouteResult = TraverseHorizontalRoute(childRoute, visited, fullRouteLength + (adjustedRoute.Length * 12), routeObjectsTemp);
        fixtureUnitsSoFar += childRouteResult.Item1;
        flowTypeIdTemp = (flowTypeIdTemp == 2) ? 2 : childRouteResult.Item2;
        longestRunLength = Math.Max(longestRunLength, childRouteResult.Item3);
        adjustedRoute.FixtureUnits = fixtureUnits + fixtureUnitsSoFar;
        adjustedRoute.FlowTypeId = flowTypeIdTemp;
        adjustedRoute.LongestRunLength = longestRunLength;
      }
      return new Tuple<double, int, double>(fixtureUnitsSoFar, flowTypeIdTemp, longestRunLength);
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
        case 5:
          types.Add("Vent");
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
          if (fixture.BlockName == "GMEP PLUMBING GAS OUTPUT") {
            types.Add("Gas");
          }
          else {
            types.Add("Cold Water");
          }
          break;
        case "L":
        case "U":
        case "DF":
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
        case "VS":
        case "VE":
          types.Add("Vent");
          break;
        case "FRY":
        case "GRD":
        case "GSLM":
        case "CHR":
        case "6BRN":
          types.Add("Gas");
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
                route.BasePointId,
                route.PipeType
            );
            result[adjustedRoute] = targetRoute.StartPoint.DistanceTo(targetRoute.EndPoint);
            continue;
          }
        }

        // 2. Candidate route's reverse trajectory: startpoint extended backward
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
                route.BasePointId,
                route.PipeType
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
                route.BasePointId,
                route.PipeType
            );
          result[adjustedRoute] = segLen;
        }
      }
      result.OrderByDescending(kvp => kvp.Value); // Sort by distance
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
        if (route.BasePointId == targetRoute.BasePointId && route.Type == targetRoute.Type ) {
          ed.WriteMessage($"\nChecking vertical route {route.Id} for target route {targetRoute.Id}");
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
    public SortedDictionary<int, PlumbingVerticalRoute> GetVerticalRoutesByIdOrdered(string verticalRouteId, bool isUp, string basePointId) {
      var basePointFloorLookup = BasePoints.ToDictionary(bp => bp.Id, bp => bp.Floor);


      // Get all routes for this verticalRouteId and with a valid base point
      var routes = VerticalRoutes
          .Where(vr => vr.VerticalRouteId == verticalRouteId && basePointFloorLookup.ContainsKey(vr.BasePointId))
          .ToList();

      if (routes.Count == 0)
        return new SortedDictionary<int, PlumbingVerticalRoute>();

      // Find the base point floor (use the first route as reference)
      int basePointFloor = basePointFloorLookup[basePointId];

      // Filter by direction
      var filtered = routes
          .Where(vr =>
              (isUp && basePointFloorLookup[vr.BasePointId] > basePointFloor) ||
              (!isUp && basePointFloorLookup[vr.BasePointId] < basePointFloor)
          )
          .ToDictionary(
              vr => basePointFloorLookup[vr.BasePointId],
              vr => vr
          );

      // Sort accordingly
      var sorted = isUp
          ? new SortedDictionary<int, PlumbingVerticalRoute>(filtered, Comparer<int>.Create((a, b) => b.CompareTo(a))) 
          : new SortedDictionary<int, PlumbingVerticalRoute>(filtered);

      return sorted;
    }
  }
}
