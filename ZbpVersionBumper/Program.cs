// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;


var workdir = Assembly.GetExecutingAssembly().Location;
var wi = workdir.LastIndexOf("ThePlanetCrafterMods");
workdir = workdir.Substring(0, wi) + "ThePlanetCrafterMods/";

Console.WriteLine("Checking projects in " + workdir);

foreach (string dir in Directory.EnumerateDirectories(workdir))
{
    foreach (var file in Directory.EnumerateFiles(dir, "*.csproj"))
    {
        var fileName = Path.GetFileName(file);

        if (fileName.StartsWith("Z") || fileName.StartsWith("Lib") || fileName.StartsWith("X"))
        {
            continue;
        }

        var text = File.ReadAllText(file);

        var version = Regex.Match(text, "<Version>(\\d+)\\.(\\d+)\\.(\\d+)\\.(\\d+)</Version>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (version.Success)
        {
            int a = int.Parse(version.Groups[1].Value);
            int b = int.Parse(version.Groups[2].Value);
            int c = int.Parse(version.Groups[3].Value);
            int d = int.Parse(version.Groups[4].Value);
            Console.WriteLine("---------------------------------------------------");
            Console.Write("Found " + fileName + ": " + a + "." + b + "." + c + "." + d + " -> ");

            if (++d == 100)
            {
                d = 0;
                c++;
            }
            if (c == 100)
            {
                c = 0;
                b++;
            }
            if (b == 100)
            {
                b = 0;
                a++;
            }

            Console.WriteLine(a + "." + b + "." + c + "." + d);

            text = Regex.Replace(text,
                @"<Version>(\d+)\.(\d+)\.(\d+)\.(\d+)</Version>",
                m => $"<Version>{a}.{b}.{c}.{d}</Version>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Console.WriteLine(text);
            File.WriteAllText(file, text);
        }
    }
}

