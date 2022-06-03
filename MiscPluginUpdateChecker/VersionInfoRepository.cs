using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MiscPluginUpdateChecker
{
    internal class VersionInfoRepository
    {
        internal readonly List<PluginEntry> plugins = new();
        internal readonly List<IncludeEntry> includes = new();

        internal static VersionInfoRepository Load(Stream stream)
        {
            var root = XDocument.Load(stream).Root;
            var result = new VersionInfoRepository();

            ProcessDocument(root, result);

            return result;
        }
        internal static VersionInfoRepository Parse(string xmlString)
        {
            var root = XDocument.Parse(xmlString).Root;

            var result = new VersionInfoRepository();

            ProcessDocument(root, result);

            return result;
        }

        static void ProcessDocument(XElement root, VersionInfoRepository result)
        {
            foreach (var xe in root.Elements())
            {
                if (xe.Name.LocalName == "plugin")
                {
                    var pluginEntry = new PluginEntry();
                    pluginEntry.guid = xe.AttributeWithName("guid");
                    pluginEntry.description = xe.AttributeWithName("description");

                    var xv = xe.ElementWithName("version");
                    pluginEntry.explicitVersion = xv.AttributeWithName("value");
                    pluginEntry.discoverUrl = xv.AttributeWithName("discover");

                    var dm = xv.AttributeWithName("method");
                    pluginEntry.discoverMethod = dm switch
                    {
                        "BepInPluginVersionQuote" => DiscoverMethod.BepInPluginVersionQuote,
                        "CsprojVersionTag" => DiscoverMethod.CsprojVersionTag,
                        _ => DiscoverMethod.Unknown
                    };

                    var xcl = xe.ElementWithName("changelog");
                    if (xcl != null)
                    {
                        foreach (var xce in xcl.Elements())
                        {
                            if (xce.Name.LocalName == "entry")
                            {
                                var logEntry = new ChangelogEntry();
                                logEntry.version = xce.AttributeWithName("version");
                                logEntry.title = xce.AttributeWithName("title");
                                logEntry.link = xce.AttributeWithName("link");
                                logEntry.content = xce.Value;

                                pluginEntry.changelog.Add(logEntry);
                            }
                        }
                    }

                    result.plugins.Add(pluginEntry);
                }
                else
                if (xe.Name == "include")
                {
                    var ie = new IncludeEntry();
                    ie.url = xe.AttributeWithName("url");
                    result.includes.Add(ie);
                }
            }
        }
    }

    internal class PluginEntry
    {
        internal string guid;
        internal string description;
        internal string explicitVersion;
        internal string discoverUrl;
        internal DiscoverMethod discoverMethod;
        internal string discoverVersion;
        internal readonly List<ChangelogEntry> changelog = new();
    }

    internal class ChangelogEntry
    {
        internal string version;
        internal string title;
        internal string link;
        internal string content;
    }
    
    enum DiscoverMethod
    {
        Unknown,
        BepInPluginVersionQuote,
        CsprojVersionTag
    }

    internal class IncludeEntry
    {
        internal string url;
    }

    static class XHelper
    {
        public static XElement ElementWithName(this XElement src, string name)
        {
            return src.Element(XName.Get(name));
        }
        public static string AttributeWithName(this XElement src, string name)
        {
            return src.Attribute(XName.Get(name))?.Value;
        }
    }
}
