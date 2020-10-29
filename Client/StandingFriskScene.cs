/*
 * 
 * Frisk Animation
 * Author: Timothy Dexter
 * Release: 0.0.1
 * Date: 03/18/19
 * 
 * Credits Beijoljo (Stop The Ped)
 * 
 * Known Issues
 * 1) Cop "floats" to perp if they're too far.  This is a cleaner option than
 * teleporting.  The walking native is inconsistent and not a viable option.
 * 
 * Please send any edits/improvements/bugs to this script back to the author. 
 * 
 * Usage 
 * - Call PlayClientScene with the handle of the player being frisked
 * 
 * History:
 * Revision 0.0.1 2019/03/18 20:02:52 EDT TimothyDexter 
 * - Initial release
 * 
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Roleplay.Client.Classes.Player;
using Roleplay.Client.Helpers;
using Roleplay.SharedClasses;

namespace Roleplay.Client.Classes.Actions.CopAnimations
{
	internal class StandingFriskScene
	{
		private static int _startingHealth;

		private static readonly List<SceneAnimation> SceneAnimations = new List<SceneAnimation> {
			new SceneAnimation( "anim@heists@load_box", "idle", 850 ),
			new SceneAnimation( "anim@heists@box_carry@", "idle", 600 ),
			new SceneAnimation( "missfam5_yoga", "start_pose", 750 ),
			new SceneAnimation( "missbigscore2aig_7@driver", "boot_r_loop", 1000 ),
			new SceneAnimation( "mini@yoga", "outro_2", 1500 ),
			new SceneAnimation( "missbigscore2aig_7@driver", "boot_l_loop", 1000 ),
			new SceneAnimation( "mini@yoga", "outro_2", 1500 )
		};

		/// <summary>
		/// Plays the scene.
		/// </summary>
		/// <param name="perpHandle">The perp handle.</param>
		/// <returns></returns>
		public async Task PlayClientScene( int perpHandle ) {
			var perp = Entity.FromHandle( perpHandle );
			if( perp == null || !perp.Exists() ) return;

			var isPerpDead = API.IsPedRagdoll(perpHandle) || perp.IsDead || API.DecorGetBool( perpHandle, "Ped.IsIncapacitated" );
			if( isPerpDead ) return;

			var pos = StandingArmCuffScene.GetArrestPosition( perp.Position, perp.Heading );

			var headingOffset = pos == StandingArmCuffScene.ArrestPositionEnum.Back ? 0 : 180f;
			float forwardVector = -0.5f;
			forwardVector = pos == StandingArmCuffScene.ArrestPositionEnum.Back ? forwardVector : forwardVector * -1;

			var offsetPos = perp.GetOffsetPosition( new Vector3( 0f, forwardVector, 0f ) );

			float distanceFromPos = CurrentPlayer.Ped.Position.DistanceToSquared2D( offsetPos );
			if( distanceFromPos > 3f ) {
				Log.Info( $"Player too far ({distanceFromPos}) to start frisk." );
				return;
			}

			_startingHealth = Cache.PlayerHealth;

			API.TaskGoStraightToCoord( Cache.PlayerHandle, offsetPos.X, offsetPos.Y, offsetPos.Z, 1f, 5000,
				perp.Heading + headingOffset, 4f );
			await BaseScript.Delay( 750 );

			foreach( var sceneAnim in SceneAnimations ) {
				if( sceneAnim.Anim == "boot_r_loop" ) {
					CurrentPlayer.Ped.Heading = CurrentPlayer.Ped.Heading - 20f;
				}
				else if( sceneAnim.Anim == "boot_l_loop" ) {
					CurrentPlayer.Ped.Task.ClearAll();
					CurrentPlayer.Ped.Heading = CurrentPlayer.Ped.Heading + 40f;
				}

				PlaySceneAnimation( sceneAnim.Dict, sceneAnim.Anim );
				if( await SceneWaitAndCheckContinue( sceneAnim.AnimWaitTime ) ) continue;
				//Exit animation
				CurrentPlayer.Ped.Task.ClearAll();
				return;
			}
			CurrentPlayer.Ped.Task.ClearAll();
			CurrentPlayer.Ped.Heading = CurrentPlayer.Ped.Heading - 20f;
		}

		/// <summary>
		/// Plays the demo scene.
		/// </summary>
		/// <returns></returns>
		public async Task PlayDemoScene() {
			try {
				var nearestPed = PedInteraction.GetClosestStreetPedWithUsualExclusions();
				if( nearestPed == null ) return;

				await PlayClientScene( nearestPed.Handle );
			}
			catch( Exception ex ) {
				Log.Error( ex );
			}
		}

		/// <summary>
		/// Plays the scene animation.
		/// </summary>
		/// <param name="dict">The dictionary.</param>
		/// <param name="anim">The anim.</param>
		private static void PlaySceneAnimation( string dict, string anim ) {
			CurrentPlayer.Ped.Task.PlayAnimation( dict, anim, 1.5f, -1, AnimationFlags.None );
		}

		/// <summary>
		/// Scenes the wait and check continue.
		/// </summary>
		/// <param name="timeMs">The time ms.</param>
		/// <returns></returns>
		private async Task<bool> SceneWaitAndCheckContinue( int timeMs ) {
			var endTime = DateTime.Now.AddMilliseconds( timeMs );
			while( DateTime.Now.CompareTo( endTime ) < 0 ) {
				int currentHealth = Cache.PlayerHealth;
				if( currentHealth > _startingHealth ) _startingHealth = currentHealth;

				if( ControlHelper.IsControlPressed( Control.MoveDownOnly, false ) ||
					ControlHelper.IsControlPressed( Control.MoveUpOnly, false ) ||
					ControlHelper.IsControlPressed( Control.MoveLeftOnly, false ) ||
					ControlHelper.IsControlPressed( Control.MoveRightOnly, false ) ||
					currentHealth < _startingHealth ||
					CurrentPlayer.Ped.IsRagdoll )
					return false;

				await BaseScript.Delay( 10 );
			}

			return true;
		}

		/// <summary>
		/// Object for scene animations
		/// </summary>
		private class SceneAnimation
		{
			public readonly string Anim;
			public readonly int AnimWaitTime;
			public readonly string Dict;

			public SceneAnimation( string dict, string anim, int waitTime ) {
				Dict = dict;
				Anim = anim;
				AnimWaitTime = waitTime;
			}
		}
	}
}