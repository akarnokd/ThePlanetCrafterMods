// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System.Text;
using System.Text.RegularExpressions;

namespace XTestPlugins
{
    [TestClass]
    public class VersionInfoRepository_Test
    {
        
        [TestMethod]
        public void GenerateDefaultPluginInfos()
        {
            var sb = new StringBuilder();
            string pattern = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,\\s*\"(.*?)\"\\)";
            string pattern2 = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,";
            string patternConst = "BepInPlugin\\((.*?)\\s*,\\s*\"(.*?)\"\\s*,";
            var regex = new Regex(pattern);
            var regex2 = new Regex(pattern2);
            Console.WriteLine(Directory.GetCurrentDirectory());
            foreach (string dir in Directory.EnumerateDirectories("..\\..\\..\\.."))
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
                    sb.Append("    <plugin guid=\"").Append(guid).Append('"').AppendLine();
                    sb.Append("            description=\"").Append(desc).Append('"').AppendLine();
                    sb.Append("            discover=\"https://raw.githubusercontent.com/akarnokd/ThePlanetCrafterMods/main/").Append(d).Append('/').Append(d).Append(".csproj\"").AppendLine();
                    sb.Append("            method=\"CsprojVersionTag\"").AppendLine();
                    sb.Append("            link=\"https://github.com/akarnokd/ThePlanetCrafterMods/releases/latest\"").AppendLine();
                    sb.Append("            />").AppendLine();
                }
            }
            Console.Write(sb.ToString());
            Console.WriteLine();
        }
    }
}