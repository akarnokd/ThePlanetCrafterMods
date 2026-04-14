// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

string pattern = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,\\s*\"(.*?)\"\\)";
string pattern2 = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,";
string patternConst = "BepInPlugin\\((.*?)\\s*,\\s*\"(.*?)\"\\s*,";
var regex = new Regex(pattern);
var regex2 = new Regex(pattern2);

string versionTag = "<Version>(.+?)<\\/Version>";
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
                    Console.WriteLine(csprojFile);
                    var assName = Regex.Match(csproj,
                        @"<AssemblyName\s*>(.*?)</AssemblyName>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;

                    var dirName = Regex.Match(csproj,
                        @"<Description\s*>(.*?)</Description>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;

                    string dllFileName = Path.Combine(dir, "bin/Debug/netstandard2.1/"  + assName + ".dll");
                    var data = File.ReadAllBytes(dllFileName);
                    using var sha2 = SHA256.Create();
                    var hash = sha2.ComputeHash(data);
                    var hashCopy = Convert.ToBase64String(hash);

                    lines.Add("# " + desc);
                    lines.Add(guid + "=" + m3.Groups[1].Value + "|" + hashCopy + "|" + dirName);

                    Console.WriteLine("  " + csprojFile);
                    Console.WriteLine("  :: " + guid + "=" + m3.Groups[1].Value + " ## " + desc + " |" + hashCopy + "|" + dirName);
                }
            }
        }
    }
}

if (lines.Count != 0)
{
    File.WriteAllLines(workdir + "version_info.txt", lines);
}

List<string> hashes = [];
using var sha1 = SHA256.Create();

foreach (var file in Directory.EnumerateFiles(workdir, "akarnokd-*.zip"))
{
    if (file.Contains("akarnokd-all"))
    {
        continue;
    }

    using var zf = ZipFile.OpenRead(file);
    foreach (var entry in zf.Entries)
    {
        if (entry.FullName.EndsWith(".dll"))
        {
            var fname = entry.Name;
            using var stream = entry.Open();
            var h = sha1.ComputeHash(stream);
            var hash = Convert.ToBase64String(h);
            hashes.Add(entry.Name.PadRight(64) + " " + hash);
        }
    }
}

if (hashes.Count != 0)
{
    hashes.Sort();
    File.WriteAllLines(workdir + "hashes.txt", hashes);
}
/*
Console.WriteLine("---");

string gamePath = @"c:\Program Files (x86)\Steam\steamapps\common\The Planet Crafter\Planet Crafter_Data\Managed";

string[] files = ["Assembly-CSharp.dll", "Assembly-Csharp-firstpass.dll"];

foreach (string file in files)
{
    Console.WriteLine("Disassembly check " + file);
    var main = Path.Combine(gamePath, file);
    var backup = Path.Combine(gamePath, file + ".bak");

    var generate = true;

    if (File.Exists(backup))
    {
        Console.WriteLine("  Backup found. Comparing.");
        var a1 = File.ReadAllBytes(backup);
        var a2 = File.ReadAllBytes(main);

        if (Enumerable.SequenceEqual<byte>(a1, a2))
        {
            generate = false;
            Console.WriteLine("    Main == Backup");
        }
    }
    else
    {
        Console.WriteLine("  Backup not found.");
    }

    if (generate)
    {
        Console.WriteLine("  Creating a fresh backup.");
        File.Copy(main, backup);

        {
            Console.WriteLine("  Installing ilspycmd.");
            var ps = new ProcessStartInfo("dotnet.exe",
                ["tool", "install", "--global", "ilspycmd", "--version", "9.1.0.7988"])!;
            ps.RedirectStandardOutput = true;

            var p = Process.Start(ps)!;
            Console.WriteLine(p.StandardOutput.ReadToEnd());
            p.WaitForExit();
            Console.WriteLine("    Done.");
        }

        {
            Console.WriteLine("  Decompiling.");
            var ps = new ProcessStartInfo("ilspycmd", ["-genpdb", main]);
            ps.RedirectStandardOutput = true;

            var p = Process.Start(ps)!;
            Console.WriteLine(p.StandardOutput.ReadToEnd());
            p.WaitForExit();
            Console.WriteLine("    Done.");
        }
    }
}
*/