using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System.Windows.Forms.Integration;

[assembly: CommandClass(typeof(GMEPPlumbing.AutoCADIntegration))]

namespace GMEPPlumbing
{
  public class AutoCADIntegration
  {
    [CommandMethod("ServiceCalculator")]
    public void ServiceCalculator()
    {
      var myControl = new UserInterface();
      var host = new ElementHost();
      host.Child = myControl;

      var pw = new PaletteSet("GMEP Plumbing Service Calculator");
      pw.Add("MyTab", host);
      pw.Visible = true;
    }
  }
}