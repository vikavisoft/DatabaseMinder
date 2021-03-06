﻿using System;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Management.Smo;

namespace DatabaseMinder
{
    public static class RestoreDatabase
    {
        public static void RestoreDatabaseFromFolder(Server server, string databaseName, string backupPath)
        {
            Consoler.Write(backupPath);
            foreach (var file in Directory.EnumerateFiles(backupPath, "*.bak").OrderBy(x => x))
            {
                try
                {
                    Consoler.Write(file);
                    Execute(server,databaseName, file);
                }
                catch (Exception ex)
                {
                    Consoler.Warn("bak restore failed", backupPath);
                    Consoler.Error(ex.ToString());
                }
            }
        }

        public static void Execute(Server server, string folder, string databaseName)
        {
            var bak = Directory.EnumerateFiles(folder, "*.bak").First(x => x.Contains(databaseName));
            //If the database doesn't exist, create it so that we have something
            //to overwrite.
            if (!server.Databases.Contains(databaseName))
            {
                Consoler.Information($"Database does not exist... Creating {databaseName}");
                var database = new Database(server, databaseName);
                database.Create();
            }

            var targetDatabase = server.Databases[databaseName];
            targetDatabase.RecoveryModel = RecoveryModel.Simple;
            targetDatabase.Alter();
            Restore restore = new Restore
            {
                Database = databaseName,
                ReplaceDatabase = true
            };

            restore.Devices.AddDevice(bak, DeviceType.File);
            restore.Information += Restore_Information;


            var fileList = restore.ReadFileList(server);

            // restore to new location
            var dataFile = new RelocateFile
            {
                LogicalFileName = fileList.Rows[0][0].ToString(),
                PhysicalFileName = Path.Combine(folder, databaseName.WithoutExtension() + ".mdf")
            };

            var logFile = new RelocateFile
            {
                LogicalFileName = fileList.Rows[1][0].ToString(),
                PhysicalFileName = Path.Combine(folder, databaseName.WithoutExtension() + "_log.log")
            };

            restore.RelocateFiles.Add(dataFile);
            Consoler.Information($"dataFile {dataFile.LogicalFileName}");
            restore.RelocateFiles.Add(logFile);
            Consoler.Information($"logFile {logFile.LogicalFileName}");

            server.KillAllProcesses(databaseName);
            Consoler.Information($"KillAllProcesses");
            restore.SqlRestore(server);
            Consoler.Information($"Database restored ");
        }

        private static void Restore_Information(object sender, Microsoft.SqlServer.Management.Common.ServerMessageEventArgs e)
        {
            Consoler.Information($"restore info {e.Error}");
        }
    }
}
