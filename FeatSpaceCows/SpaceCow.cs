// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using SpaceCraft;
using UnityEngine;

namespace FeatSpaceCows
{
    internal class SpaceCow
    {
        internal WorldObject parent;
        internal Inventory inventory;
        internal GameObject body;
        GameObject side1;
        GameObject side2;

        internal Color color;

        /// <summary>
        /// What the other side told us about their position.
        /// </summary>
        internal Vector3 rawPosition;

        internal Quaternion rawRotation;

        internal static SpaceCow CreateCow(Texture2D sideTexture, Color color)
        {
            var result = new SpaceCow
            {
                color = color,

                body = GameObject.CreatePrimitive(PrimitiveType.Cube)
            };

            result.body.name = "SpaceCow";
            result.body.transform.localScale = new Vector3(2f, 1f, 0.5f);

            float scalingX = 1 * 0.7f;
            float scalingY = 2 * 0.7f;
            int layer = LayerMask.NameToLayer(GameConfig.layerIgnoreRaycast);

            // ----------

            result.side1 = new GameObject("SpaceCow-LeftSide");
            result.side1.transform.SetParent(result.body.transform);
            result.side1.transform.localScale = new Vector3(scalingX, scalingY, 1);
            result.side1.transform.localPosition = new Vector3(0, 0, 0.51f);

            SpriteRenderer sr = result.side1.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(sideTexture, new Rect(0, 0, sideTexture.width, sideTexture.height), new Vector2(0.5f, 0.5f));
            sr.color = color;

            result.side1.layer = layer;

            // ----------

            result.side2 = new GameObject("SpaceCow-RightSide");
            sr = result.side2.AddComponent<SpriteRenderer>();
            result.side2.transform.SetParent(result.body.transform);
            result.side2.transform.localScale = new Vector3(scalingX, scalingY, 1);
            result.side2.transform.localPosition = new Vector3(0, 0, -0.51f);

            sr.sprite = Sprite.Create(sideTexture, new Rect(0, 0, sideTexture.width, sideTexture.height), new Vector2(0.5f, 0.5f));
            sr.color = color;

            result.side2.layer = layer;

            // ------------


            return result;
        }

        internal void Destroy()
        {
            UnityEngine.Object.Destroy(body);
            UnityEngine.Object.Destroy(side1);
            UnityEngine.Object.Destroy(side2);
            inventory = null;
            parent = null;
        }

        internal void SetPosition(Vector3 position, Quaternion rotation)
        {
            if (body != null)
            {
                rawPosition = position;
                rawRotation = rotation;

                body.transform.position = new Vector3(position.x, position.y + 1.5f, position.z);
                var yrot = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
                body.transform.rotation = yrot;
            }
        }

        internal void SetColor(Color color)
        {
            this.color = color;
            side1.GetComponent<SpriteRenderer>().color = color;
            side2.GetComponent<SpriteRenderer>().color = color;
        }
    }
}
