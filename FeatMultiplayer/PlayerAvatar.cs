using SpaceCraft;
using UnityEngine;

namespace FeatMultiplayer
{
    internal class PlayerAvatar
    {
        internal GameObject avatar;
        internal GameObject avatarFront;
        internal GameObject avatarBack;
        internal GameObject light1;
        internal GameObject light2;

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

        internal void SetPosition(Vector3 position, Quaternion rotation, int lightMode)
        {
            if (avatar != null)
            {
                rawPosition = position;

                avatar.transform.position = new Vector3(position.x, position.y + 1.5f, position.z);
                var yrot = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
                avatar.transform.rotation = yrot;

                light1.transform.localRotation = Quaternion.Euler(rotation.eulerAngles.x, 0, 0);
                light1.SetActive(lightMode == 1);
                light2.transform.localRotation = Quaternion.Euler(rotation.eulerAngles.x, 0, 0);
                light2.SetActive(lightMode == 2);
            }
        }

        //static readonly Color avatarDefaultColor = new Color(1f, 0.75f, 0, 1f);

        internal static PlayerAvatar CreateAvatar(Color color, bool host, PlayerMainController player)
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
            sr.sprite = Sprite.Create(host ? Plugin.astronautFrontHost : Plugin.astronautFront, new Rect(0, 0, Plugin.astronautFront.width, Plugin.astronautFront.height), new Vector2(0.5f, 0.5f));
            sr.color = color;

            // ----------

            result.avatarBack = new GameObject();
            sr = result.avatarBack.AddComponent<SpriteRenderer>();
            result.avatarBack.transform.SetParent(result.avatar.transform);
            result.avatarBack.transform.localScale = new Vector3(scaling, scaling, scaling);
            result.avatarBack.transform.localPosition = new Vector3(0, 0, -0.51f);

            sr.sprite = Sprite.Create(host ? Plugin.astronautBackHost : Plugin.astronautBack, new Rect(0, 0, Plugin.astronautFront.width, Plugin.astronautFront.height), new Vector2(0.5f, 0.5f));
            sr.color = color;

            // ------------

            var lights = player.GetComponentInChildren<MultiToolLight>();

            result.light1 = UnityEngine.Object.Instantiate<GameObject>(lights.toolLightT1);
            result.light1.transform.SetParent(result.avatar.transform);
            result.light1.transform.localPosition = new Vector3(0, 0, 0.52f);
            result.light1.SetActive(true);

            result.light2 = UnityEngine.Object.Instantiate<GameObject>(lights.toolLightT2);
            result.light2.transform.SetParent(result.avatar.transform);
            result.light2.transform.localPosition = new Vector3(0, 0, 0.52f);
            result.light2.SetActive(false);

            return result;
        }
    }
}
