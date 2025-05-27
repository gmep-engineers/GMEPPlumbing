using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Accord.Math;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;



namespace GMEPPlumbing
{
    public class CADObjectCommands
    {
        public static string GetProjectNoFromFileName()
        {
            Document doc = Autodesk
              .AutoCAD
              .ApplicationServices
              .Core
              .Application
              .DocumentManager
              .MdiActiveDocument;
            string fileName = Path.GetFileName(doc.Name);
            return Regex.Match(fileName, @"[0-9]{2}-[0-9]{3}").Value;
        }

    }
}
