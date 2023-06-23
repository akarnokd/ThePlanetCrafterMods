using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiscPluginUpdateChecker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace XTestPlugins
{
    [TestClass]
    public class MiscPluginUpdateChecker_Test
    {
        [TestMethod]
        public void Version_Extractor_BepInPlugin_Version_Quote()
        {
            string str = "    [BepInPlugin(\"akarnokd.theplanetcraftermods.miscpluginupdatechecker\", \"(Misc) Plugin Update Checker\", \"1.0.0.0\")]";

            Assert.AreEqual("1.0.0.0", Helpers.GetBepInPluginVersionQuoteFrom(str));
        }
        [TestMethod]
        public void Version_Extractor_BepInPlugin_Version_Quote_Missing()
        {
            string str = "    [BepInPlugin(\"akarnokd.theplanetcraftermods.miscpluginupdatechecker\", \"(Misc) Plugin Update Checker\", SomeVersionString)]";

            Assert.AreEqual(null, Helpers.GetBepInPluginVersionQuoteFrom(str));
        }

        [TestMethod]
        public void Version_Extractor_Csproj_Version_Tag()
        {
            string str = "    <Version>1.0.0.0</Version>";
            Assert.AreEqual("1.0.0.0", Helpers.GetCsprojVersionTag(str));
        }

        [TestMethod]
        public void Version_Extractor_Csproj_Version_Tag_Missing()
        {
            string str = "    <Version2>1.0.0.0</Version2>";
            Assert.AreEqual(null, Helpers.GetCsprojVersionTag(str));
        }

        [TestMethod]
        public void VersionInfoRepositoryParse_1()
        {
            string str = @"<?xml version='1.0' encoding='utf-8'?>
               <version_info_repository>
                   <plugin 
                       guid='id1' description='desc1'
                       discover='file' 
                       method='BepInPluginVersionQuote' 
                       link='link_a'>
                       <changelog version='1.1' title='title11' link='link11'>
                                Content11
                       </changelog>
                       <changelog version='1.0' title='title10' link='link10'>
                                Content10
                       </changelog>
                   </plugin>
                   <plugin guid='id2' description='desc2'
                           version='1.2' 
                           link='link_b'/>
               </version_info_repository>
            ";

            var vir = VersionInfoRepository.Parse(str);

            Assert.AreEqual(2, vir.plugins.Count);

            Assert.AreEqual("id1", vir.plugins[0].guid);
            Assert.AreEqual("desc1", vir.plugins[0].description);
            Assert.IsNull(vir.plugins[0].explicitVersion);
            Assert.AreEqual("file", vir.plugins[0].discoverUrl);
            Assert.AreEqual("link_a", vir.plugins[0].link);
            Assert.AreEqual(DiscoverMethod.BepInPluginVersionQuote, vir.plugins[0].discoverMethod);
            Assert.AreEqual(2, vir.plugins[0].changelog.Count);

            Assert.AreEqual(Version.Parse("1.1"), vir.plugins[0].changelog[0].version);
            Assert.AreEqual("title11", vir.plugins[0].changelog[0].title);
            Assert.AreEqual("link11", vir.plugins[0].changelog[0].link);
            Assert.AreEqual("Content11", vir.plugins[0].changelog[0].content.Trim());

            Assert.AreEqual(Version.Parse("1.0"), vir.plugins[0].changelog[1].version);
            Assert.AreEqual("title10", vir.plugins[0].changelog[1].title);
            Assert.AreEqual("link10", vir.plugins[0].changelog[1].link);
            Assert.AreEqual("Content10", vir.plugins[0].changelog[1].content.Trim());

            // --------------------------------------------------

            Assert.AreEqual("id2", vir.plugins[1].guid);
            Assert.AreEqual("desc2", vir.plugins[1].description);
            Assert.AreEqual(Version.Parse("1.2"), vir.plugins[1].explicitVersion);
            Assert.AreEqual("link_b", vir.plugins[1].link);
            Assert.IsNull(vir.plugins[1].discoverUrl);
            Assert.AreEqual(DiscoverMethod.Unknown, vir.plugins[1].discoverMethod);
            Assert.AreEqual(0, vir.plugins[1].changelog.Count);
        }
        [TestMethod]
        public void VersionInfoRepositoryParse_2()
        {
            var vir = VersionInfoRepository.Parse(File.ReadAllText("..\\..\\..\\version_info_repository.xml"));
            Assert.IsTrue(vir.plugins.Count > 0);
        }

        [TestMethod]
        public void DownloadPluginInfos_Web()
        {
            var result = Helpers.DownloadPluginInfos(
                Helpers.defaultVersionInfoRepository,
                o => Console.WriteLine("INFO : " + o),
                o => Console.WriteLine("WARN : " + o),
                o => Console.WriteLine("ERROR: " + o),
                true
            );

            Assert.IsTrue(result.Count > 0);
        }

        [TestMethod]
        public void DiscoverVersions_Local()
        {
            var vir = VersionInfoRepository.Parse(File.ReadAllText("..\\..\\..\\version_info_repository.xml"));

            Dictionary<string, PluginEntry> plugins = new Dictionary<string, PluginEntry>();
            foreach (var pi in vir.plugins)
            {
                plugins[pi.guid] = pi;
            }

            Helpers.DiscoverVersions(plugins,
                o => Console.WriteLine("INFO : " + o),
                o => Console.WriteLine("WARN : " + o),
                o => Console.WriteLine("ERROR: " + o),
                true);
        }

        [TestMethod]
        public void GenerateDefaultPluginInfos()
        {
            StringBuilder sb = new StringBuilder();
            string pattern = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,\\s*\"(.*?)\"\\)";
            string pattern2 = "BepInPlugin\\(\"(.*?)\"\\s*,\\s*\"(.*?)\"\\s*,";
            string patternConst = "BepInPlugin\\((.*?)\\s*,\\s*\"(.*?)\"\\s*,";
            var regex = new Regex(pattern);
            var regex2 = new Regex(pattern2);
            foreach (string dir in Directory.EnumerateDirectories("..\\..\\.."))
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
                    sb.Append("    <plugin guid=\"").Append(guid).Append("\"").AppendLine();
                    sb.Append("            description=\"").Append(desc).Append("\"").AppendLine();
                    sb.Append("            discover=\"https://raw.githubusercontent.com/akarnokd/ThePlanetCrafterMods/main/").Append(d).Append("/").Append(d).Append(".csproj\"").AppendLine();
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
