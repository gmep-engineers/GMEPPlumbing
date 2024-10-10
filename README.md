# GMEPPlumbing
GMEP plumbing software to automate calculations &amp; table generation

## Setup
1. Configure `appsettings.json` to connect to the remote Mongo database (or stay with a local instance if one is running).
1. Right-click `create_load_dll.ps1` and click *Run with PowerShell*.
1. Ensure `GMEPPlumbing.csproj` is configured to look up the currently installed version of AutoCAD.
1. Open blank file in AutoCAD and run the `APPLOAD` command.
1. Under *Startup Suite*, click *Contents*.
1. Click *Add*.
1. Navigate to the same `GMEPPlumbing` folder above. Select `load_dll.lsp`. Click *Open*.
1. Click *Close* to close the Startup Suite window.
1. Click *Close* to close the Load/Unload Applications window.
1. Close and reopen AutoCAD. Click *Always Load* at the security prompts.