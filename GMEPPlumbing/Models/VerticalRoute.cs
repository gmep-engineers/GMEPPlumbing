using Autodesk.AutoCAD.MacroRecorder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Models
{
    class VerticalRoute
    {
        private string id;
        private string projectId;
        private int startFloor;
        private int endFloor;
        private int typeId;
        public VerticalRoute(string id, string projectId, int startFloor, int endFloor, int typeId)
        {
            this.id = id;
            this.projectId = projectId;
            this.startFloor = startFloor;
            this.endFloor = endFloor;
            this.typeId = typeId;
        }
        public string Id
        {
            get { return id; }
            set { id = value; }
        }
        public string ProjectId
        {
            get { return projectId; }
            set { projectId = value; }
        }
        public int StartFloor
        {
            get { return startFloor; }
            set { startFloor = value; }
        }
        public int EndFloor
        {
            get { return endFloor; }
            set { endFloor = value; }
        }
        public int TypeId
        {
            get { return typeId; }
            set { typeId = value; }
        }
        public override string ToString()
        {
            return $"VerticalRoute: {id}, ProjectId: {projectId}, StartFloor: {startFloor}, EndFloor: {endFloor}, TypeId: {typeId}";
        }


    }
}
