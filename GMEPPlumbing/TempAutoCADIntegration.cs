using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Media.Media3D;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using GMEPPlumbing.Commands;
using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using GMEPPlumbing.Views;
using MongoDB.Bson.Serialization.Conventions;
using Trace = System.Diagnostics.Trace;

[assembly: CommandClass(typeof(GMEPPlumbing.TempAutoCADIntegration))]
[assembly: CommandClass(typeof(GMEPPlumbing.Commands.TableCommand))]

namespace GMEPPlumbing
{
    public class PlumbingFixtureType
    {
        public string Name;
        public string Abbreviation;
        public string WaterGasBlockName;
        public string WasteVentBlockName;

        public PlumbingFixtureType(
            string name,
            string abbreviation,
            string waterGasBlockName,
            string wasteVentBlockName
        )
        {
            Name = name;
            Abbreviation = abbreviation;
            WaterGasBlockName = waterGasBlockName;
            WasteVentBlockName = wasteVentBlockName;
        }
    }

    internal class TempAutoCADIntegration
    {
        private const string XRecordKey = "GMEPPlumbingID";
        private PaletteSet pw;
        private UserInterface myControl;
        private string currentDrawingId;
        private WaterSystemViewModel viewModel;
        private bool needsXRecordUpdate = false;
        private string newDrawingId;
        private DateTime newCreationTime;
        public MariaDBService MariaDBService { get; set; } = new MariaDBService();

        public Document doc { get; private set; }
        public Database db { get; private set; }
        public Editor ed { get; private set; }
        public string ProjectId { get; private set; } = string.Empty;

        [CommandMethod("PlumbingFixture")]
        public async void PlumbingFixture()
        {
            doc = Application.DocumentManager.MdiActiveDocument;
            db = doc.Database;
            ed = doc.Editor;

            List<PlumbingFixtureType> plumbingFixtureTypes =
                MariaDBService.GetPlumbingFixtureTypes();
            // prompt for fixture type
            PromptKeywordOptions keywordOptions = new PromptKeywordOptions("");
            keywordOptions.Message = "\nSelect fixture type:";

            plumbingFixtureTypes.ForEach(t =>
            {
                keywordOptions.Keywords.Add(t.Abbreviation + " - " + t.Name);
            });
            keywordOptions.Keywords.Default = "WC - Water Closet";
            keywordOptions.AllowNone = false;
            PromptResult keywordResult = ed.GetKeywords(keywordOptions);
            string keywordResultString = keywordResult.StringResult;

            PlumbingFixtureType selectedFixtureType = plumbingFixtureTypes.FirstOrDefault(t =>
                t.Abbreviation == keywordResultString
            );

            if (selectedFixtureType.WaterGasBlockName.Contains("%SIZE%"))
            {
                if (selectedFixtureType.Abbreviation == "WH")
                {
                    keywordOptions = new PromptKeywordOptions("");
                    keywordOptions.Message = "\nSelect WH size";
                    keywordOptions.Keywords.Add("50 gal.");
                    keywordOptions.Keywords.Add("80 gal.");
                    keywordOptions.Keywords.Default = "50 gal.";
                    keywordOptions.AllowNone = false;
                    keywordResult = ed.GetKeywords(keywordOptions);
                    string whSize = keywordResult.StringResult;
                    selectedFixtureType.WaterGasBlockName =
                        selectedFixtureType.WaterGasBlockName.Replace("%SIZE%", whSize);
                }
            }
            if (selectedFixtureType.WasteVentBlockName.Contains(

            /*
            string blockName = "";
            Point3d point;
            // run block & rotate jigs
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
              BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
              BlockTableRecord btr;
      
              BlockReference br = CADObjectCommands.CreateBlockReference(
                tr,
                bt,
                blockName,
                out btr,
                out point
              );
            }
            */

            // save data in database
        }
    }
}
