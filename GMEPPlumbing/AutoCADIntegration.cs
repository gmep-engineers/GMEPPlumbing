using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using GMEPPlumbing.Services;
using GMEPPlumbing.Views;
using System.Windows.Forms.Integration;

[assembly: CommandClass(typeof(GMEPPlumbing.AutoCADIntegration))]

namespace GMEPPlumbing
{
  public class AutoCADIntegration
  {
    [CommandMethod("Water")]
    public void Water()
    {
      // Initialize MongoDB connection
      MongoDBService.Initialize();

      MongoDBService.AddRandomKeyValuePair();

      var myControl = new UserInterface();
      var host = new ElementHost();
      host.Child = myControl;

      var pw = new PaletteSet("GMEP Plumbing Water Calculator");
      pw.Style = PaletteSetStyles.ShowAutoHideButton |
                 PaletteSetStyles.ShowCloseButton |
                 PaletteSetStyles.ShowPropertiesMenu;

      pw.DockEnabled = DockSides.Left | DockSides.Right;

      // Set initial size (this will be used when floating)
      pw.Size = new System.Drawing.Size(1280, 800);
      pw.MinimumSize = new System.Drawing.Size(1280, 800);

      pw.Add("MyTab", host);

      // Make the PaletteSet visible
      pw.Visible = true;

      // docks on left side and unrolls
      pw.Dock = DockSides.Left;
      pw.RolledUp = false;
    }
  }
}