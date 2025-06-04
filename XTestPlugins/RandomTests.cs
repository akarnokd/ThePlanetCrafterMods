// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

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
