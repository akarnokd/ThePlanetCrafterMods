// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using MonoMod.Utils;

namespace LibCommon
{
    public static class ModVersionCheck
    {
        static readonly string[] REPOSITORIES = {
            "akarnokd/ThePlanetCrafterMods",
            "Tjatja0/ThePlanetCrafterMods",
            "mcnicki2002/PlanetCrafterMods",
        };

        static readonly Dictionary<string, (string, string)> versionInfo = [];

        static bool versionDownloaded;

        public static void Reset()
        {
            versionDownloaded = false;
            versionInfo.Clear();
        }

        public static bool Check(string localModGuid, string localModVersion, Action<object> logInfo)
        {
            if (!versionDownloaded)
            {
                List<Task<Dictionary<string, (string, string)>>> allTasks = [];
                foreach (var repo in REPOSITORIES)
                {
                    var frepo = repo;
                    allTasks.Add(Task.Run<Dictionary<string, (string, string)>>(() =>
                    {
                        try
                        {
                            return GetVersionTxt(frepo, logInfo);
                        }
                        catch (Exception ex)
                        {
                            logInfo("Repo download failed: " + frepo + "\r\n" + ex);
                        }
                        return [];
                    }));
                }

                Task.WaitAll(allTasks.ToArray());

                versionInfo.Clear();

                foreach (var t in allTasks)
                {
                    versionInfo.AddRange(t.Result);
                }
                versionDownloaded = true;
            }

            if (versionInfo.TryGetValue(localModGuid, out var version))
            {
                var localVer = new Version(localModVersion);
                var remoteVer = new Version(version.Item2);
                return remoteVer > localVer;
            }
            return false;
        }

        public static void NotifyUser(string localModGuid, string localVersion, string localDescription)
        {
            // TODO create GUI for the Intro::Start
        }

        static Dictionary<string, (string, string)> GetVersionTxt(string repository, Action<object> logInfo)
        {
            var startUrl = "https://raw.githubusercontent.com/" + repository + "/main/version_info.txt";

            if (SteamManager.Initialized && Steamworks.SteamApps.GetCurrentBetaName(out var text, 256))
            {
                logInfo("Beta name: " + text);
                var branches = GetBranches(repository, logInfo);
                branches.Sort();
                branches.Reverse();

                foreach (var b in branches)
                {
                    if (b != "main")
                    {
                        startUrl = "https://raw.githubusercontent.com/" + repository + "/" + b + "/version_info.txt";
                        break;
                    }
                }
            }

            logInfo("Download " + startUrl);

            Dictionary<string, (string, string)> plugins = [];

            var request = WebRequest.Create(MaybeRandom(startUrl)).NoCache();

            using var response = request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            string desc = "";

            logInfo("Parsing version_info.txt");
            for (; ; )
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                if (line.StartsWith("#"))
                {
                    desc = line[2..];
                    continue;
                }
                var kv = line.Split('=');
                if (kv.Length != 2)
                {
                    continue;
                }

                logInfo("  -> " + kv[0] + " @ " + kv[1]);
                plugins[kv[0]] = (desc, kv[1]);
            }

            logInfo("Version discovery done");

            return plugins;
        }

        internal static List<string> GetBranches(string repository, Action<object> logInfo)
        {
            List<string> result = [];
            logInfo("GetBranches");
            var api = "https://api.github.com/repos/" + repository + "/branches";
            var request = (HttpWebRequest)WebRequest.Create(MaybeRandom(api)).NoCache();
            request.UserAgent = "akarnokd-ThePlanetCrafterMods-ModVersionCheck";
            request.Accept = "application/json";

            using var response = request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);

            logInfo("Parsing Branches");

            var json = JArray.Parse(reader.ReadToEnd());

            foreach (var item in json)
            {
                var jo = item as JObject;
                if (jo != null && jo.ContainsKey("name"))
                {
                    var nm = jo["name"];
                    result.Add(nm.ToString());
                    // LogInfo("  " + nm);
                }
            }
            // LogInfo("Parsing Branches Done");
            return result;
        }

        static string MaybeRandom(string url)
        {
            return url + "?v=" + DateTime.UtcNow.Ticks;
        }
    }

    static class HelpersExt
    {
        internal static WebRequest NoCache(this WebRequest request)
        {
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return request;
        }
    }
}
