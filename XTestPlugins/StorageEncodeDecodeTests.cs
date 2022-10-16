using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeatMultiplayer;

namespace XTestPlugins
{
    [TestClass]
    public class StorageEncodeDecodeTests
    {
        [TestMethod]
        public void Encode()
        {
            Assert.AreEqual("abc", StorageHelper.StorageEncodeString("abc"));
            Assert.AreEqual("abc\\p", StorageHelper.StorageEncodeString("abc|"));
            Assert.AreEqual("abc\\p\\a\\n\\r\\b\\c\\e", StorageHelper.StorageEncodeString("abc|@\n\r\\,="));
        }

        [TestMethod]
        public void Decode()
        {
            Assert.AreEqual("abc", StorageHelper.StorageDecodeString("abc"));
            Assert.AreEqual("abc|", StorageHelper.StorageDecodeString("abc\\p"));
            Assert.AreEqual("abc|@\n\r\\,=", StorageHelper.StorageDecodeString("abc\\p\\a\\n\\r\\b\\c\\e"));
        }
    }
}
