using SpaceCraft;

namespace CrimShadowVehicle
{
// Token: 0x02000005 RID: 5
	public class ActionExitVehicle : Actionnable
	{
		// Token: 0x06000010 RID: 16 RVA: 0x00002970 File Offset: 0x00000B70
		public override void OnAction()
		{
			this.enterVehicleAction.ExitVehicle();
		}

		// Token: 0x06000011 RID: 17 RVA: 0x0000297F File Offset: 0x00000B7F
		public override void OnHover()
		{
			Actionnable.hudHandler.DisplayCursorText("Exit", 0f, "Exit SpaceCraft");
			base.OnHover();
		}

		// Token: 0x06000012 RID: 18 RVA: 0x000029A3 File Offset: 0x00000BA3
		public override void OnHoverOut()
		{
			Actionnable.hudHandler.CleanCursorTextIfCode("Exit");
			base.OnHoverOut();
		}

		// Token: 0x06000013 RID: 19 RVA: 0x000029BD File Offset: 0x00000BBD
		private void OnDestroy()
		{
			Actionnable.hudHandler.CleanCursorTextIfCode("Exit");
		}

		// Token: 0x04000022 RID: 34
		public ActionEnterVehicle enterVehicleAction;
	}
}