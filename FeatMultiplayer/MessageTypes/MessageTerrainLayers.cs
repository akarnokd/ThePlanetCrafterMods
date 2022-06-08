using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    internal class MessageTerrainLayers : MessageStringProvider
    {
        internal readonly List<MessageTerrainLayer> layers = new();

        internal static bool TryParse(string str, out MessageTerrainLayers mtl)
        {
            if (MessageHelper.TryParseMessage("TerrainLayers|", str, out var parameters))
            {
                try
                {
                    mtl = new();
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        if (parameters[i].Length != 0)
                        {
                            string[] layerStr = parameters[i].Split(';');

                            var layer = new MessageTerrainLayer();
                            layer.layerId = layerStr[0];
                            layer.colorBase = MessageHelper.StringToColor(layerStr[1]);
                            layer.colorCustom = MessageHelper.StringToColor(layerStr[2]);
                            layer.colorBaseLerp = int.Parse(layerStr[3]);
                            layer.colorCustomLerp = int.Parse(layerStr[4]);

                            mtl.layers.Add(layer);
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mtl = null;
            return false;
        }

        public string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("TerrainLayers");
            if (layers.Count == 0)
            {
                sb.Append('|');
            }
            foreach (var layer in layers)
            {
                sb.Append('|');
                sb.Append(layer.layerId).Append(';');
                sb.Append(MessageHelper.ColorToString(layer.colorBase)).Append(';');
                sb.Append(MessageHelper.ColorToString(layer.colorCustom)).Append(';');
                sb.Append(layer.colorBaseLerp).Append(';');
                sb.Append(layer.colorCustomLerp);
            }
            sb.Append('\n');

            return sb.ToString();
        }
    }

    internal class MessageTerrainLayer
    {
        internal string layerId;
        internal Color colorBase;
        internal Color colorCustom;
        internal int colorBaseLerp;
        internal int colorCustomLerp;
    }
}
