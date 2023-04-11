using SpaceCraft;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace FeatMultiplayer
{
    internal class PlayerAvatar
    {
        internal GameObject avatar;
        internal GameObject avatarFront;
        internal GameObject avatarBack;
        internal GameObject light1;
        internal GameObject light2;
        internal GameObject nameBar;
        internal GameObject emote;

        /// <summary>
        /// What the other side told us about their position.
        /// </summary>
        internal Vector3 rawPosition;

        internal void Destroy()
        {
            UnityEngine.Object.Destroy(avatar);
            UnityEngine.Object.Destroy(avatarFront);
            UnityEngine.Object.Destroy(avatarBack);
            UnityEngine.Object.Destroy(nameBar);
            UnityEngine.Object.Destroy(emote);
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

        internal void SetColor(Color color)
        {
            avatarFront.GetComponent<SpriteRenderer>().color = color;
            avatarBack.GetComponent<SpriteRenderer>().color = color;
        }

        internal void SetName(string name)
        {
            if (Plugin.resourcesPath.Contains(".v0."))
            {
                HelloThereDecompiler(ref name);
            }
            nameBar.GetComponent<TextMesh>().text = name;
        }

        void HelloThereDecompiler(ref string name)
        {
            DoNotComeToMeForTechSupport(ref name);
        }

        void DoNotComeToMeForTechSupport(ref string name)
        {
            YouAreDisrespectingMyTime(ref name);
        }

        void YouAreDisrespectingMyTime(ref string name)
        {
            SoTakeThisFunnyFaceForFree(ref name);
        }

        void SoTakeThisFunnyFaceForFree(ref string name)
        {
            name += " (^ u ^)";
        }

        internal void Emote(List<Sprite> sprites, float delayBetweenFrames, int loopCount)
        {
            emote.GetComponent<EmoteAnimator>().Emote(sprites, delayBetweenFrames, loopCount);
        }

        //static readonly Color avatarDefaultColor = new Color(1f, 0.75f, 0, 1f);

        internal static PlayerAvatar CreateAvatar(Color color, bool host, PlayerMainController player)
        {
            PlayerAvatar result = new PlayerAvatar();

            SpriteRenderer sr;

            result.avatar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.avatar.name = "Avatar";
            result.avatar.transform.localScale = new Vector3(0.5f, 0.5f, 0.2f);

            float scaling = 2.5f;

            // ----------

            result.avatarFront = new GameObject("AvatarFront");
            result.avatarFront.transform.SetParent(result.avatar.transform);
            result.avatarFront.transform.localScale = new Vector3(scaling, scaling, scaling);
            result.avatarFront.transform.localPosition = new Vector3(0, 0, 0.51f);

            sr = result.avatarFront.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(host ? Plugin.astronautFrontHost : Plugin.astronautFront, new Rect(0, 0, Plugin.astronautFront.width, Plugin.astronautFront.height), new Vector2(0.5f, 0.5f));
            sr.color = color;

            // ----------

            result.avatarBack = new GameObject("AvatarBack");
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

            // -------------

            result.nameBar = new GameObject("AvatarNameBar");

            var txt = result.nameBar.AddComponent<TextMesh>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = "";
            txt.color = new Color(1f, 1f, 1f, 1f);
            txt.fontSize = (int)Plugin.playerNameFontSize.Value;
            txt.anchor = TextAnchor.MiddleCenter;

            result.nameBar.transform.SetParent(result.avatar.transform);
            result.nameBar.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            result.nameBar.transform.localPosition = new Vector3(0, 2.75f, 0.52f);
            result.nameBar.transform.Rotate(new Vector3(0, 1, 0), 180);


            // -------------

            result.emote = new GameObject("AvatarEmote");
            result.emote.AddComponent<SpriteRenderer>();
            result.emote.transform.SetParent(result.avatar.transform);
            result.emote.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            result.emote.transform.localPosition = new Vector3(0, 4f, 0);

            result.emote.AddComponent<EmoteAnimator>();
            return result;
        }

        // Just to have access to coroutines
        class EmoteAnimator : MonoBehaviour
        {
            Coroutine currentAnimation;

            internal void Emote(List<Sprite> sprites, float delayBetweenFrames, int loopCount)
            {
                if (currentAnimation != null)
                {
                    StopCoroutine(currentAnimation);
                    gameObject.GetComponent<SpriteRenderer>().sprite = null;
                }

                currentAnimation = StartCoroutine(EmoteAnimate(sprites, delayBetweenFrames, loopCount));
            }

            internal IEnumerator EmoteAnimate(List<Sprite> sprites, float delayBetweenFrames, int loopCount)
            {
                while (loopCount-- > 0)
                {
                    foreach (var sp in sprites)
                    {
                        gameObject.GetComponent<SpriteRenderer>().sprite = sp;
                        yield return new WaitForSeconds(delayBetweenFrames);
                    }
                }
                gameObject.GetComponent<SpriteRenderer>().sprite = null;
                currentAnimation = null;
            }
        }
    }
}
