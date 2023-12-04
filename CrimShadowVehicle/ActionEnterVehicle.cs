using HarmonyLib;
using SpaceCraft;
using UnityEngine;

namespace CrimShadowVehicle
{
// Token: 0x02000004 RID: 4
	public class ActionEnterVehicle : Actionnable
	{
		// Token: 0x06000007 RID: 7 RVA: 0x000022E4 File Offset: 0x000004E4
		private new void Start()
		{
			bool flag = ActionEnterVehicle.activePlayerController == null || ActionEnterVehicle.playerMultitool == null || ActionEnterVehicle.playerLookable == null || ActionEnterVehicle.playerCameraShake == null;
			if (flag)
			{
				ActionEnterVehicle.activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
				ActionEnterVehicle.playerMultitool = ActionEnterVehicle.activePlayerController.GetMultitool();
				ActionEnterVehicle.playerLookable = ActionEnterVehicle.activePlayerController.GetPlayerLookable();
				ActionEnterVehicle.playerCameraShake = ActionEnterVehicle.activePlayerController.GetPlayerCameraShake();
			}
			this.rootObject = base.gameObject.transform.parent.gameObject;
			this.vehiclePlayerExclusionCollider = this.rootObject.GetComponent<Collider>();
			this.openInventoryCollider = this.rootObject.transform.Find("TriggerOpenInventory").GetComponent<Collider>();
			GameObject gameObject = this.rootObject.transform.Find("TriggerExit").gameObject;
			this.exitVehicleCollider = gameObject.GetComponent<Collider>();
			gameObject.AddComponent<ActionExitVehicle>().enterVehicleAction = this;
			this.vehicleLocationTransform = this.rootObject.transform;
			this.playerHeadLocationTransform = this.rootObject.transform.Find("PlayerHeadLocation");
			this.playerExitLocationTransform = this.rootObject.transform.Find("PlayerExitLocation");
			this.worldObject = this.rootObject.GetComponent<WorldObjectAssociated>().GetWorldObject();
			GameObject gameObject2 = GameObject.Find("Player");
			this.playerArmor = gameObject2.GetComponentInChildren<PlayerTEMPFIXANIM>().playerArmor;
			this.playerFootsteps = gameObject2.transform.Find("Audio/Footsteps").GetComponent<AudioSource>();
		}

		// Token: 0x06000008 RID: 8 RVA: 0x0000247C File Offset: 0x0000067C
		private void Update()
		{
			bool flag = this.inVehicle;
			if (flag)
			{
				this.worldObject.SetPositionAndRotation(this.vehicleLocationTransform.position, this.vehicleLocationTransform.rotation);
			}
		}

		// Token: 0x06000009 RID: 9 RVA: 0x000024B8 File Offset: 0x000006B8
		public override void OnAction()
		{
			bool flag = ActionEnterVehicle.playerMultitool.GetState() == DataConfig.MultiToolState.Deconstruct;
			if (!flag)
			{
				this.inVehicle = true;
				this.vehiclePlayerExclusionCollider.enabled = false;
				this.openInventoryCollider.enabled = false;
				Vector3 vector = new Vector3(this.vehicleLocationTransform.position.x + this.playerHeadLocationTransform.localPosition.x, this.vehicleLocationTransform.position.y, this.vehicleLocationTransform.position.z + this.playerHeadLocationTransform.localPosition.z);
				ActionEnterVehicle.activePlayerController.SetPlayerPlacement(vector, ActionEnterVehicle.activePlayerController.transform.rotation);
				Vector3 localPosition = ActionEnterVehicle.playerLookable.m_Camera.localPosition;
				this.previousPlayerCameraHeight = localPosition.y;
				float y = this.playerHeadLocationTransform.localPosition.y;
				ActionEnterVehicle.playerLookable.m_Camera.localPosition = new Vector3(localPosition.x, y, localPosition.z);
				this.UpdateCameraShakerOriginalLocalCameraPosition(ActionEnterVehicle.playerLookable.m_Camera.localPosition);
				float y2 = this.rootObject.transform.localEulerAngles.y;
				this.rootObject.transform.localEulerAngles = new Vector3(0f, y2, 0f);
				this.previousParentTransform = this.rootObject.transform.parent;
				this.rootObject.transform.SetParent(ActionEnterVehicle.activePlayerController.transform, true);
				CharacterController component = ActionEnterVehicle.activePlayerController.GetComponent<CharacterController>();
				this.previousPlayerColliderCenter = component.center;
				this.previousPlayerColliderHeight = component.height;
				this.previousPlayerColliderRadius = component.radius;
				component.height = y + this.colliderRadius;
				component.radius = this.colliderRadius;
				component.center = new Vector3(0f, (y + this.colliderRadius) / 2f, 0f);
				this.playerArmor.SetActive(false);
				this.previousFootstepsVolume = this.playerFootsteps.volume;
				this.playerFootsteps.volume = 0f;
				ActionEnterVehicle.activePlayerController.GetPlayerMovable().EnableJump = false;
				this.previousRunSpeed = ActionEnterVehicle.activePlayerController.GetPlayerMovable().RunSpeed;
				ActionEnterVehicle.activePlayerController.GetPlayerMovable().RunSpeed *= 2f;
				this.exitVehicleCollider.enabled = true;
			}
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002734 File Offset: 0x00000934
		private void UpdateCameraShakerOriginalLocalCameraPosition(Vector3 newPosition)
		{
			AccessTools.FieldRefAccess<PlayerCameraShake, Vector3>(ActionEnterVehicle.playerCameraShake, "originalCamPosition") = newPosition;
		}

		// Token: 0x0600000B RID: 11 RVA: 0x0000274C File Offset: 0x0000094C
		public void ExitVehicle()
		{
			bool flag = this.inVehicle;
			if (flag)
			{
				this.inVehicle = false;
				this.exitVehicleCollider.enabled = false;
				ActionEnterVehicle.activePlayerController.GetPlayerMovable().RunSpeed = this.previousRunSpeed;
				ActionEnterVehicle.activePlayerController.GetPlayerMovable().EnableJump = true;
				this.playerFootsteps.volume = this.previousFootstepsVolume;
				this.playerArmor.SetActive(true);
				CharacterController component = ActionEnterVehicle.activePlayerController.GetComponent<CharacterController>();
				component.height = this.previousPlayerColliderHeight;
				component.radius = this.previousPlayerColliderRadius;
				component.center = this.previousPlayerColliderCenter;
				this.rootObject.transform.SetParent(this.previousParentTransform, true);
				this.rootObject.transform.position = ActionEnterVehicle.activePlayerController.transform.position;
				Vector3 localPosition = ActionEnterVehicle.playerLookable.m_Camera.localPosition;
				ActionEnterVehicle.playerLookable.m_Camera.localPosition = new Vector3(localPosition.x, this.previousPlayerCameraHeight, localPosition.z);
				this.UpdateCameraShakerOriginalLocalCameraPosition(ActionEnterVehicle.playerLookable.m_Camera.localPosition);
				ActionEnterVehicle.activePlayerController.SetPlayerPlacement(this.playerExitLocationTransform.position, ActionEnterVehicle.activePlayerController.transform.rotation);
				this.openInventoryCollider.enabled = true;
				this.vehiclePlayerExclusionCollider.enabled = true;
			}
		}

		// Token: 0x0600000C RID: 12 RVA: 0x000028B7 File Offset: 0x00000AB7
		public override void OnHover()
		{
			Actionnable.hudHandler.DisplayCursorText("Enter", 0f, "Enter SpaceCraft");
			base.OnHover();
		}

		// Token: 0x0600000D RID: 13 RVA: 0x000028DB File Offset: 0x00000ADB
		public override void OnHoverOut()
		{
			Actionnable.hudHandler.CleanCursorTextIfCode("Enter");
			base.OnHoverOut();
		}

		// Token: 0x0600000E RID: 14 RVA: 0x000028F5 File Offset: 0x00000AF5
		private void OnDestroy()
		{
			Actionnable.hudHandler.CleanCursorTextIfCode("Enter");
		}

		// Token: 0x0400000B RID: 11
		public float colliderRadius = 2.5f;

		// Token: 0x0400000C RID: 12
		private static PlayerMainController activePlayerController;

		// Token: 0x0400000D RID: 13
		private static PlayerMultitool playerMultitool;

		// Token: 0x0400000E RID: 14
		private static PlayerLookable playerLookable;

		// Token: 0x0400000F RID: 15
		private static PlayerCameraShake playerCameraShake;

		// Token: 0x04000010 RID: 16
		private GameObject rootObject;

		// Token: 0x04000011 RID: 17
		private Collider vehiclePlayerExclusionCollider;

		// Token: 0x04000012 RID: 18
		private Collider openInventoryCollider;

		// Token: 0x04000013 RID: 19
		private Collider exitVehicleCollider;

		// Token: 0x04000014 RID: 20
		private Transform vehicleLocationTransform;

		// Token: 0x04000015 RID: 21
		private Transform playerHeadLocationTransform;

		// Token: 0x04000016 RID: 22
		private Transform playerExitLocationTransform;

		// Token: 0x04000017 RID: 23
		private WorldObject worldObject;

		// Token: 0x04000018 RID: 24
		private GameObject playerArmor;

		// Token: 0x04000019 RID: 25
		private AudioSource playerFootsteps;

		// Token: 0x0400001A RID: 26
		private bool inVehicle = false;

		// Token: 0x0400001B RID: 27
		private Transform previousParentTransform;

		// Token: 0x0400001C RID: 28
		private float previousPlayerCameraHeight = 0f;

		// Token: 0x0400001D RID: 29
		private Vector3 previousPlayerColliderCenter = Vector3.zero;

		// Token: 0x0400001E RID: 30
		private float previousPlayerColliderHeight = 0f;

		// Token: 0x0400001F RID: 31
		private float previousPlayerColliderRadius = 0f;

		// Token: 0x04000020 RID: 32
		private float previousFootstepsVolume = 0f;

		// Token: 0x04000021 RID: 33
		private float previousRunSpeed = 0f;
	}
}