// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using UnityEngine;

namespace FeatTechniciansExile
{
    internal class TechnicianAvatar
    {
        internal GameObject avatar;
        internal GameObject avatarFront;
        internal GameObject avatarBack;

        /// <summary>
        /// What the other side told us about their position.
        /// </summary>
        internal Vector3 rawPosition;

        internal void Destroy()
        {
            UnityEngine.Object.Destroy(avatar);
            UnityEngine.Object.Destroy(avatarFront);
            UnityEngine.Object.Destroy(avatarBack);
        }

        internal void SetPosition(Vector3 position, Quaternion rotation)
        {
            if (avatar != null)
            {
                rawPosition = position;

                avatar.transform.position = new Vector3(position.x, position.y + 1.5f, position.z);
                //var yrot = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
                avatar.transform.rotation = rotation;
            }
        }

        internal void SetVisible(bool visible)
        {
            avatar?.SetActive(visible);
        }

        internal void SetColor(Color color)
        {
            avatarFront.GetComponent<SpriteRenderer>().color = color;
            avatarBack.GetComponent<SpriteRenderer>().color = color;
        }

        internal static TechnicianAvatar CreateAvatar(Color color)
        {
            var result = new TechnicianAvatar();

            SpriteRenderer sr;

            result.avatar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.avatar.name = "TechnicianAvatar";
            result.avatar.transform.localScale = new Vector3(0.5f, 0.5f, 0.2f);

            float scaling = 2.5f;

            // ----------

            result.avatarFront = new GameObject("TechnicianAvatarFront");
            result.avatarFront.transform.SetParent(result.avatar.transform);
            result.avatarFront.transform.localScale = new Vector3(scaling, scaling, scaling);
            result.avatarFront.transform.localPosition = new Vector3(0, 0, 0.51f);

            sr = result.avatarFront.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Plugin.technicianFront, new Rect(0, 0, Plugin.technicianFront.width, Plugin.technicianFront.height), new Vector2(0.5f, 0.5f));
            sr.color = color;

            // ----------

            result.avatarBack = new GameObject("AvatarBack");
            sr = result.avatarBack.AddComponent<SpriteRenderer>();
            result.avatarBack.transform.SetParent(result.avatar.transform);
            result.avatarBack.transform.localScale = new Vector3(scaling, scaling, scaling);
            result.avatarBack.transform.localPosition = new Vector3(0, 0, -0.51f);

            sr.sprite = Sprite.Create(Plugin.technicianBack, new Rect(0, 0, Plugin.technicianFront.width, Plugin.technicianFront.height), new Vector2(0.5f, 0.5f));
            sr.color = color;

            return result;
        }
    }
}
