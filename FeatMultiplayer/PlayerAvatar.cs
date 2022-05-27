using UnityEngine;

namespace FeatMultiplayer
{
    internal class PlayerAvatar
    {
        internal GameObject avatar;

        internal void Destroy()
        {
            UnityEngine.Object.Destroy(avatar);
        }

        internal void SetPosition(Vector3 position, Quaternion rotation)
        {
            if (avatar != null)
            {
                avatar.transform.position = new Vector3(position.x, position.y + 1, position.z);
                avatar.transform.rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            }
        }

        static readonly Color avatarDefaultColor = new Color(1f, 0.75f, 0, 1f);

        internal static PlayerAvatar CreateAvatar()
        {
            PlayerAvatar result = new PlayerAvatar();

            result.avatar = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // TODO this doesn't work
            result.avatar.GetComponent<Renderer>().material.SetColor("_Color", avatarDefaultColor);

            result.avatar.transform.localScale = new Vector3(0.75f, 2f, 0.25f);

            return result;
        }
    }
}
