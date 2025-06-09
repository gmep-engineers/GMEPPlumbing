using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Models
{
    class HorizontalRouteLine
    {
        private string id;
        private Point3d startPoint;
        private Point3d endPoint;
        private int typeId;
        private string projectId;
        public HorizontalRouteLine(string id, Point3d startPoint, Point3d endPoint, int typeId, string projectId)
        {
            this.id = id;
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            this.typeId = typeId;
            this.projectId = projectId;
        }
        public string Id
        {
            get { return id; }
            set { id = value; }
        }
        public Point3d StartPoint
        {
            get { return startPoint; }
            set { startPoint = value; }
        }
        public Point3d EndPoint
        {
            get { return endPoint; }
            set { endPoint = value; }
        }
        public int TypeId
        {
            get { return typeId; }
            set { typeId = value; }
        }
        public string ProjectId
        {
            get { return projectId; }
            set { projectId = value; }
        }
        public override string ToString()
        {
            return $"HorizontalRouteLine: {id}, Start: {startPoint}, End: {endPoint}, TypeId: {typeId}, ProjectId: {projectId}";
        }

    }
}
