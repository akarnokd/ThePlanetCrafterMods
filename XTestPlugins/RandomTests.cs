using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTestPlugins
{
    [TestClass]
    public class RandomTests
    {
        [TestMethod]
        public void IsModded()
        {
            Assert.IsTrue(Directory.GetFileSystemEntries("E:\\Steam\\steamapps\\common\\The Planet Crafter\\BepInEx\\plugins\\").Length != 0);
        }
    }
}
