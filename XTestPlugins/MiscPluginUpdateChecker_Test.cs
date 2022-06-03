using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiscPluginUpdateChecker;
using System;

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
                   <plugin guid='id1' description='desc1'>
                       <version discover='file' method='BepInPluginVersionQuote'/>
                       <changelog>
                           <entry version='1.1' title='title11' link='link11'>
                                Content11
                           </entry>
                           <entry version='1.0' title='title10' link='link10'>
                                Content10
                           </entry>
                       </changelog>
                   </plugin>
                   <plugin guid='id2' description='desc2'>
                       <version value='1.2'/>
                       <changelog/>
                   </plugin>
               </version_info_repository>
            ";

            var vir = VersionInfoRepository.Parse(str);

            Assert.AreEqual(2, vir.plugins.Count);

            Assert.AreEqual("id1", vir.plugins[0].guid);
            Assert.AreEqual("desc1", vir.plugins[0].description);
            Assert.IsNull(vir.plugins[0].explicitVersion);
            Assert.AreEqual("file", vir.plugins[0].discoverUrl);
            Assert.AreEqual(DiscoverMethod.BepInPluginVersionQuote, vir.plugins[0].discoverMethod);
            Assert.AreEqual(2, vir.plugins[0].changelog.Count);

            Assert.AreEqual("1.1", vir.plugins[0].changelog[0].version);
            Assert.AreEqual("title11", vir.plugins[0].changelog[0].title);
            Assert.AreEqual("link11", vir.plugins[0].changelog[0].link);
            Assert.AreEqual("Content11", vir.plugins[0].changelog[0].content.Trim());

            Assert.AreEqual("1.0", vir.plugins[0].changelog[1].version);
            Assert.AreEqual("title10", vir.plugins[0].changelog[1].title);
            Assert.AreEqual("link10", vir.plugins[0].changelog[1].link);
            Assert.AreEqual("Content10", vir.plugins[0].changelog[1].content.Trim());

            // --------------------------------------------------

            Assert.AreEqual("id2", vir.plugins[1].guid);
            Assert.AreEqual("desc2", vir.plugins[1].description);
            Assert.AreEqual("1.2", vir.plugins[1].explicitVersion);
            Assert.IsNull(vir.plugins[1].discoverUrl);
            Assert.AreEqual(DiscoverMethod.Unknown, vir.plugins[1].discoverMethod);
            Assert.AreEqual(0, vir.plugins[1].changelog.Count);
        }

        [TestMethod]
        public void DownloadPluginInfos_Web()
        {
            var result = Helpers.DownloadPluginInfos(
                Helpers.defaultVersionInfoRepository,
                o => Console.WriteLine("INFO : " + o),
                o => Console.WriteLine("WARN : " + o),
                o => Console.WriteLine("ERROR: " + o)
            );

            Assert.IsTrue(result.Count > 0);
        }
    }
}
