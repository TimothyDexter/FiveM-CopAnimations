You will need to create server methods to call the various arrest animations so that they are network sync'd.

[Cuff Preview](https://youtu.be/4o7rNYITP_4)
[Frisk Preview](https://youtu.be/X8ckYkStJ1s)

e.g. Client
		private static async void HandleToggleCuffsWithAnimation( string data ) {
			var model = JsonConvert.DeserializeObject<StandingArmCuffScene.SceneModel>( data );

			if( Game.PlayerPed.Weapons.Current != WeaponHash.Unarmed ) {
				Function.Call( Hash.SET_CURRENT_PED_WEAPON, Cache.PlayerHandle, WeaponHash.Unarmed, true );
				API.SetPlayerForcedAim( Cache.PlayerHandle, false );
				await BaseScript.Delay( 50 );
			}

			var arrestScene = new StandingArmCuffScene();
			await arrestScene.PlayClientPerpScene( model );

			PlayerCuffState = CuffState.Cuffed;
			Function.Call( Hash.DECOR_SET_INT, Cache.PlayerHandle, "Arrest.CuffState", (int)CuffState.Cuffed);
		}

Server
 
		public static void ToggleCuffWithAnimation( [FromSource] Player source, int targetPlayer, string data ) {
			Player target = new PlayerList()[targetPlayer];
			
			TriggerClientEvent( target, "Arrest.ToggleCuffsWithAnimation", data );
		}
