using UnityEngine;

namespace FeatMultiplayer
{
    internal class PlayerAvatar
    {
        internal GameObject avatar;
        internal GameObject avatarFront;
        internal GameObject avatarBack;

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
                avatar.transform.position = new Vector3(position.x, position.y + 1.5f, position.z);
                avatar.transform.rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);

            }
        }

        //static readonly Color avatarDefaultColor = new Color(1f, 0.75f, 0, 1f);

        internal static PlayerAvatar CreateAvatar()
        {
            PlayerAvatar result = new PlayerAvatar();

            SpriteRenderer sr;

            result.avatar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.avatar.transform.localScale = new Vector3(0.5f, 0.5f, 0.2f);

            float scaling = 2.5f;

            // ----------

            result.avatarFront = new GameObject();
            result.avatarFront.transform.SetParent(result.avatar.transform);
            result.avatarFront.transform.localScale = new Vector3(scaling, scaling, scaling);
            result.avatarFront.transform.localPosition = new Vector3(0, 0, 0.51f);

            sr = result.avatarFront.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Plugin.astronautFront, new Rect(0, 0, Plugin.astronautFront.width, Plugin.astronautFront.height), new Vector2(0.5f, 0.5f));

            // ----------

            result.avatarBack = new GameObject();
            sr = result.avatarBack.AddComponent<SpriteRenderer>();
            result.avatarBack.transform.SetParent(result.avatar.transform);
            result.avatarBack.transform.localScale = new Vector3(scaling, scaling, scaling);
            result.avatarBack.transform.localPosition = new Vector3(0, 0, -0.51f);

            sr.sprite = Sprite.Create(Plugin.astronautBack, new Rect(0, 0, Plugin.astronautFront.width, Plugin.astronautFront.height), new Vector2(0.5f, 0.5f));


            return result;
        }
    }
}
