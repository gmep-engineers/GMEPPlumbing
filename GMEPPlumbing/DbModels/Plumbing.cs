using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GMEPPlumbing {
  public class PlumbingFixture {
    public string Id;
    public string ProjectId;
    public Point3d Position;
    public double Rotation;
    public int CatalogId;
    public string TypeAbbreviation;
    public int Number;
    public string BasePointId;
    public string FixtureId;
    public string BlockName;
    public PlumbingFixture(
      string id,
      string projectId,
      Point3d position,
      double rotation,
      int catalogId,
      string typeAbbreviation,
      int number,
      string basePointId,
      string fixtureId,
      string blockName
    ) {
      Id = id;
      ProjectId = projectId;
      Position = position;
      Rotation = rotation;
      CatalogId = catalogId;
      TypeAbbreviation = typeAbbreviation;
      Number = number;
      BasePointId = basePointId;
      FixtureId = fixtureId;
      BlockName = blockName;
    }
  }
  
  public class PlumbingFixtureType {
    public int Id;
    public string Name;
    public string Abbreviation;
    public string WaterGasBlockName;
    public string WasteVentBlockName;

    public PlumbingFixtureType(
      int id,
      string name,
      string abbreviation,
      string waterGasBlockName,
      string wasteVentBlockName
    ) {
      Id = id;
      Name = name;
      Abbreviation = abbreviation;
      WaterGasBlockName = waterGasBlockName;
      WasteVentBlockName = wasteVentBlockName;
    }
  }

  public class PlumbingFixtureCatalogItem {
    public int Id;
    public int TypeId;
    public string Description;
    public string Make;
    public string Model;
    public decimal Trap;
    public decimal Waste;
    public decimal Vent;
    public decimal ColdWater;
    public decimal HotWater;
    public string Remarks;
    public decimal FixtureDemand;
    public decimal HotDemand;
    public int Dfu;
    public string WaterGasBlockName;
    public string WasteVentBlockName;

    public PlumbingFixtureCatalogItem(
      int id,
      int typeId,
      string description,
      string make,
      string model,
      decimal trap,
      decimal waste,
      decimal vent,
      decimal coldWater,
      decimal hotWater,
      string remarks,
      decimal fixtureDemand,
      decimal hotDemand,
      int dfu,
      string waterGasBlockName,
      string wasteVentBlockName
    ) {
      Id = id;
      TypeId = typeId;
      Description = description;
      Make = make;
      Model = model;
      Trap = trap;
      Waste = waste;
      Vent = vent;
      ColdWater = coldWater;
      HotWater = hotWater;
      Remarks = remarks;
      FixtureDemand = fixtureDemand;
      HotDemand = hotDemand;
      Dfu = dfu;
      WaterGasBlockName = waterGasBlockName;
      WasteVentBlockName = wasteVentBlockName;
    }
  }

  public class PlumbingSource {
    public string Id;
    public string ProjectId;
    public Point3d Position;
    public int TypeId;
    public string BasePointId;

    public PlumbingSource(
      string id,
      string projectId,
      Point3d position,
      int typeId,
      string basePointId
    ) {
      Id = id;
      ProjectId = projectId;
      Position = position;
      TypeId = typeId;
      BasePointId = basePointId;
    }
  }

  public class PlumbingSourceType {
    public int Id;
    public string Type;

    public PlumbingSourceType(int id, string type) {
      Id = id;
      Type = type;
    }
  }

  public class PlumbingPlanBasePoint {
    public string Id;
    public string ProjectId;
    public string ViewportId;
    public int Floor;
    public string Plan;
    public string Type;
    public Point3d Point;

    public PlumbingPlanBasePoint(string id, string projectId, Point3d point, string plan, string type, string viewportId, int floor) {
      Id = id;
      ProjectId = projectId;
      ViewportId = viewportId;
      Floor = floor;
      Type = type;
      Plan = plan;
      Point = point;
    }
  }
  public class PlumbingHorizontalRoute {
    public string Id;
    public string ProjectId;
    public Point3d StartPoint;
    public Point3d EndPoint;
    public string BasePointId;
    public PlumbingHorizontalRoute(
      string id,
      string projectId,
      Point3d startPoint,
      Point3d endPoint,
      string basePointId
    ) {
      Id = id;
      ProjectId = projectId;
      StartPoint = startPoint;
      EndPoint = endPoint;
      BasePointId = basePointId;
    }
  }
  public class PlumbingVerticalRoute {
    public string Id;
    public string ProjectId;
    public Point3d Position;
    public Point3d ConnectionPosition;
    public string VerticalRouteId;
    public string BasePointId;

    public PlumbingVerticalRoute(
      string id,
      string projectId,
      Point3d position,
      Point3d connectionPosition,
      string verticalRouteId,
      string basePointId
    ) {
      Id = id;
      ProjectId = projectId;
      Position = position;
      ConnectionPosition = connectionPosition;
      VerticalRouteId = verticalRouteId;
      BasePointId = basePointId;
    }
  }
}
