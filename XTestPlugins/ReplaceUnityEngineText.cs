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
    public class ReplaceUnityEngineText
    {
        [TestMethod]
        public void Do()
        {
            ScanDir("c:\\Users\\akarnokd\\git\\ThePlanetCrafterSources\\0.8.010c\\");
        }

        void ScanDir(string dir)
        {
            foreach (var file in Directory.GetFileSystemEntries(dir))
            {
                if (Directory.Exists(file))
                {
                    ScanDir(file);
                }
                else if (Path.GetExtension(file) == ".cs")
                {
                    var lines = File.ReadAllLines(file, Encoding.UTF8);
                    bool changed = false;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string? line = lines[i];
                        if (line.Contains("UnityEngine.") && !line.Contains("using UnityEngine."))
                        {
                            lines[i] = line.Replace("UnityEngine.", "");
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        Console.WriteLine("Updating " + file);
                        File.WriteAllLines(file, lines, new UTF8Encoding(true));
                    }
                }
            }
        }
    }
}
