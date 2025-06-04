// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using System;
using System.IO.Compression;
using System.Text;

namespace SaveAutoBackup
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveautobackup", "(Save) Auto Backup", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.perfsavereducesize", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static ConfigEntry<string> outputPath;
        static ConfigEntry<bool> gzip;
        static ConfigEntry<int> keepCount;
        static ConfigEntry<int> keepAge;
        static ConfigEntry<bool> doAsync;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            outputPath = Config.Bind("General", "OutputPath", "", "The path where the backups will be placed if not empty. Make sure this path exists!");
            gzip = Config.Bind("General", "GZIP", true, "Compress the backups with GZIP?");
            keepCount = Config.Bind("General", "KeepCount", 0, "If zero, all previous backups are retained. If positive, only that number of backups per world is kept and the old ones will be deleted");
            keepAge = Config.Bind("General", "KeepAge", 0, "If zero, all previous backups are retained. If positive, backups older than this number of days will be deleted. Age is determined from the file name's timestamp part");
            doAsync = Config.Bind("General", "Async", true, "If true, the backup handling is done asynchronously so the game doesn't hang during the process.");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JSONExport), "SaveStringsInFile")]
        static bool JSONExport_SaveStringsInFile(
            string _saveFileName, 
            List<string> _saveStrings,
            char ___listDelimiter,
            char ___chunckDelimiter
            )
        {
            int keepCnt = keepCount.Value;
            int keepDays = keepAge.Value;
            bool gz = gzip.Value;
            string outputPathStr = outputPath.Value.Trim();
            while (outputPathStr.EndsWith("\\"))
            {
                outputPathStr = outputPathStr[..^1];
            }
            while (outputPathStr.EndsWith("/"))
            {
                outputPathStr = outputPathStr[..^1];
            }

            if (doAsync.Value)
            {
                var sh = ThreadingHelper.Instance;
                sh.StartAsyncInvoke(() =>
                {
                    DoBackup(_saveFileName, outputPathStr, _saveStrings, ___listDelimiter, ___chunckDelimiter, 
                        s =>
                        {
                            sh.StartSyncInvoke(() =>
                            {
                                logger.LogInfo(s);
                            });
                        },
                        s =>
                        {
                            sh.StartSyncInvoke(() =>
                            {
                                logger.LogError(s);
                            });
                        }
                    , keepCnt, keepDays, gz);
                    return null;
                });
            } 
            else
            {
                DoBackup(_saveFileName, outputPathStr, _saveStrings, ___listDelimiter, ___chunckDelimiter, logger.LogInfo, logger.LogError, keepCnt, keepDays, gz);
            }
            return true;
        }

        static void DoBackup(string _saveFileName,
            string outputPathStr,
            List<string> _saveStrings,
            char ___listDelimiter,
            char ___chunkDelimiter,
            Action<string> logInfo,
            Action<string> logError,
            int keepCnt, int keepDays, bool gzip)
        {
            try
            {
                var now = DateTime.Now;
                /*
                var tz = TimeZoneInfo.Local;
                logInfo("TimeZone.Now " + now.ToString());
                logInfo("TimeZone.NowInv " + now.ToString(CultureInfo.InvariantCulture));
                logInfo("TimeZone " + tz.ToString());
                logInfo("TimeZone.Name " + tz.StandardName);
                logInfo("TimeZone.DaylightSaving " + tz.IsDaylightSavingTime(now));
                */

                string dateFormatStr = "yyyyMMdd_HHmmss_fff";

                if (!string.IsNullOrEmpty(outputPathStr) && Directory.Exists(outputPathStr))
                {
                    // Remove old entries
                    if (keepCnt > 0 || keepDays > 0)
                    {
                        string backupNaming = _saveFileName + "_backup_";
                        List<string> existingBackups = [];
                        foreach (string file in Directory.EnumerateFiles(outputPathStr))
                        {
                            string nameOnly = Path.GetFileName(file);
                            if (nameOnly.StartsWith(backupNaming))
                            {
                                existingBackups.Add(file);
                            }
                        }

                        existingBackups.Sort((a, b) =>
                        {
                            return b.CompareTo(a);
                        });

                        if (keepCnt > 0)
                        {
                            for (int i = keepCnt - 1; i < existingBackups.Count; i++)
                            {
                                string toDelete = existingBackups[i];
                                logInfo("Deleting backup #" + i + ": " + toDelete);
                                File.Delete(toDelete);
                            }
                        }
                        if (keepDays > 0)
                        {
                            foreach (string file in existingBackups)
                            {

                                string nameOnly = Path.GetFileName(file);
                                int idx = nameOnly.IndexOf("_backup_");
                                if (idx >= 0)
                                {
                                    int jdx = nameOnly.IndexOf(".", idx);
                                    if (jdx > 0)
                                    {
                                        string dtStr = nameOnly.Substring(idx + 8, jdx - idx - 8);
                                        var dt = DateTime.ParseExact(dtStr, dateFormatStr, CultureInfo.InvariantCulture);
                                        if ((now - dt).TotalDays > keepDays)
                                        {
                                            logInfo("Deleting old backup: " + file);
                                            File.Delete(file);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Save the file, possibly compressed
                    string outputFileName = outputPathStr + "\\" + _saveFileName + "_backup_" + now.ToString(dateFormatStr) + ".json" + (gzip ? ".gz" : "");

                    
                    Stream stream = null;
                    GZipStream gz = null;

                    
                    var fs = new FileStream(outputFileName, FileMode.Create);

                    try
                    {
                        if (gzip)
                        {
                            gz = new GZipStream(fs, CompressionMode.Compress);
                            stream = gz;
                        }
                        else
                        {
                            stream = fs;
                        }
                        var writer = new StreamWriter(stream, Encoding.UTF8);

                        string delimPlusNewLine = ___listDelimiter.ToString() + "\n";
                        string delim = ___listDelimiter.ToString();
                        foreach (string line in _saveStrings)
                        {
                            writer.Write('\r');
                            writer.Write(line.Replace(delim, delimPlusNewLine));
                            writer.Write('\r');
                            writer.Write(___chunkDelimiter);
                        }

                        writer.Flush();
                    } 
                    finally
                    {
                        if (gz != null)
                        {
                            gz.Flush();
                            gz.Close();
                        }
                        fs.Close();
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(outputPathStr))
                    {
                        logError("OutputPath does not exist: " + outputPathStr);
                    }
                }
            }
            catch (Exception ex)
            {
                logError(ex.ToString() + "\r\n" + ex.StackTrace);
            }

        }
    }
}
