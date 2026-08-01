PalWorld Server Manager v0.3 - Backup Manager Refactor

FILES TO REPLACE
1. Replace MainWindow.xaml with the included MainWindow.xaml.
2. Replace MainWindow.xaml.cs with the included MainWindow.xaml.cs.

FILES TO ADD
Create these folders inside the PalWorldServerManager project:
- Themes
- Models
- Services

Then add:
- Themes/DarkTheme.xaml
- Models/BackupItem.cs
- Services/BackupService.cs

In Visual Studio:
1. Right-click the project.
2. Select Add > New Folder for each folder.
3. Right-click each folder and select Add > Existing Item.
4. Select the matching file from this package.
5. Save all.
6. Build with Ctrl+Shift+B.
7. Run with F5.

TEST
1. Load a copied PalWorldSettings.ini.
2. Save once to create a backup.
3. Open Backups in the sidebar.
4. Select a backup and click Restore Selected.
5. Confirm the editor and dashboard reload.

Suggested commit:
Add backup browser and restore functionality
