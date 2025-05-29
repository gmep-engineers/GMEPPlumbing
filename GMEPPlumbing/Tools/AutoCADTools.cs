using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        public static BlockReference CreateBlockReference(Transaction tr, BlockTable bt, string blockName, out BlockTableRecord block, out Point3d point)
        {
            if (!bt.Has(blockName))
            {
                Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
                  $"Block '{blockName}' not found in the BlockTable."
                );
                block = null;
                point = Point3d.Origin;
                return null;
            }
            block = (BlockTableRecord)
                    tr.GetObject(bt[blockName], OpenMode.ForRead);

            BlockJig blockJig = new BlockJig();
            PromptResult res = blockJig.DragMe(block.ObjectId, out point);
            if (res.Status != PromptStatus.OK)
            {
                return null;
            }
            BlockReference br = new BlockReference(point, block.ObjectId);
            return br;
        }


    }
}
