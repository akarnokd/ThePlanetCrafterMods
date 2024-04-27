// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System.Text.RegularExpressions;

string pattern = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,\\s*\"(.*?)\"\\)";
string pattern2 = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,";
string patternConst = "BepInPlugin\\((.*?)\\s*,\\s*\"(.*?)\"\\s*,";
var regex = new Regex(pattern);
var regex2 = new Regex(pattern2);

string versionTag = "<Version>(.*?)</Version>";
var versionRegex = new Regex(versionTag);

var workdir = Path.GetFullPath(args[0]);
Console.WriteLine("Checking projects in " + workdir);

List<string> lines = [];

foreach (string dir in Directory.EnumerateDirectories(workdir))
{
    string d = Path.GetFileName(dir);
    string f = Path.Combine(dir, "Plugin.cs");
    if (d != "XTestPlugins" && d != "ZipRest" && File.Exists(f))
    {
        var text = File.ReadAllText(f);
        var m = regex.Match(text);
        string guid = "";
        string desc = "";

        if (m.Groups[1].Success)
        {
            guid = m.Groups[1].Value;
            desc = m.Groups[2].Value;
        }
        else
        {
            m = regex2.Match(text);
            if (m.Groups[1].Success)
            {
                guid = m.Groups[1].Value;
                desc = m.Groups[2].Value;
            }
            else
            {
                m = new Regex(patternConst).Match(text);
                if (m.Success)
                {
                    desc = m.Groups[2].Value;
                    string constDecl = "const\\s+string\\s+" + m.Groups[1].Value + "\\s*=\\s*\"(.*?)\"\\s*;";
                    m = new Regex(constDecl).Match(text);
                    guid = m.Groups[1].Value;
                }
            }
        }
        if (guid != "") {
            string csprojFile = Path.Combine(dir, Path.GetFileName(dir) + ".csproj");

            if (File.Exists(csprojFile))
            {
                var csproj = File.ReadAllText(csprojFile);

                var m3 = versionRegex.Match(csproj);
                if (m3.Success)
                {
                    lines.Add("# " + desc);
                    lines.Add(guid + "=" + m3.Groups[1].Value);

                    Console.WriteLine("  " + csprojFile);
                    Console.WriteLine("  :: " + guid + "=" + m3.Groups[1].Value + " ## " + desc);
                }
            }
        }
    }
}

if (lines.Count != 0)
{
    File.WriteAllLines(workdir + "version_info.txt", lines);
}
