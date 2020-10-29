/*
 * 
 * Standing Uncuff Animation
 * Author: Timothy Dexter
 * Release: 0.0.1
 * Date: 03/21/19
 * 
 * Credits Sam (LSPDFR) 
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
 * Revision 0.0.1 2019/03/21 20:49:02 EDT TimothyDexter 
 * - Initial release
 * 
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Roleplay.Client.Classes.Jobs.Police;
using Roleplay.Client.Classes.Player;
using Roleplay.Client.Helpers;
using Roleplay.SharedClasses;

namespace Roleplay.Client.Classes.Actions.CopAnimations
{
	internal class StandingUncuffScene
	{
		private const float SceneStartTime = 0.45f;
		private const int SceneDuration = 1400;
		private const string AnimDict = "mp_arresting";
		private const string Anim = "a_uncuff";
		private const string HandCuffPropName = "p_cs_cuffs_02_s";


		private const string UncuffingSound = "handcuffsTakenOff";
		private const float SoundVolume = 0.25f;
		private const float SoundDistanceThreshold = 0.2f;

		/// <summary>
		///     Plays the scene.
		/// </summary>
		/// <param name="perpHandle">The perp handle.</param>
		/// <returns></returns>
		public async Task PlayClientScene( int perpHandle ) {
			var perp = Entity.FromHandle( perpHandle );
			if( perp == null || !perp.Exists() ) return;

			bool isPerpCuffed = API.IsEntityPlayingAnim( perpHandle, "mp_arresting", "idle", 3 );
			if( !isPerpCuffed ) return;

			var offsetPos = perp.GetOffsetPosition( new Vector3( -0.1f, -0.575f, 0f ) );

			float distanceFromPos = CurrentPlayer.Ped.Position.DistanceToSquared2D( offsetPos );
			if( distanceFromPos > 3f ) {
				Log.Info( $"Player too far ({distanceFromPos}) to start uncuffing." );
				return;
			}

			API.TaskGoStraightToCoord( Cache.PlayerHandle, offsetPos.X, offsetPos.Y, offsetPos.Z, 1f, 5000,
				perp.Heading, 4f );
			await BaseScript.Delay( 750 );

			var rot = CurrentPlayer.Ped.Rotation;
			API.TaskPlayAnimAdvanced( Cache.PlayerHandle, AnimDict, Anim, offsetPos.X, offsetPos.Y, offsetPos.Z, rot.X,
				rot.Y, rot.Z, 8f, -8f, SceneDuration, 262152, SceneStartTime, 2, 0 );
			BaseScript.TriggerServerEvent( "InteractSound_SV:PlayWithinDistance", SoundDistanceThreshold,
				UncuffingSound, SoundVolume );

			await BaseScript.Delay( SceneDuration );
		}

		/// <summary>
		///     Plays the demo scene.
		/// </summary>
		/// <returns></returns>
		public async Task PlayDemoScene() {
			try {
				var nearestPed = PedInteraction.GetClosestStreetPedWithUsualExclusions();
				if( nearestPed == null ) return;

				await PlayClientScene( nearestPed.Handle );
				nearestPed.Task.ClearAll();
			}
			catch( Exception ex ) {
				Log.Error( ex );
			}
		}

		/// <summary>
		/// Deletes the handcuff props.
		/// </summary>
		public void DeleteHandcuffProps() {
			try {

				var prop = Entity.FromHandle( Arrest.HandcuffProp );

				if( prop == null || !prop.Exists() ) {
					if( API.IsEntityAttachedToAnyObject( Cache.PlayerHandle ) ) {
						prop = Entity.FromHandle( API.GetEntityAttachedTo( Cache.PlayerHandle ) );
					}
				}

				if( prop == null || !prop.Exists() ) {
					prop = Entity.FromHandle( Props.FindProps3D( HandCuffPropName, CurrentPlayer.Ped.Position, 4f ).FirstOrDefault() );
				}

				if( prop.Exists() ) {
					prop.Detach();
					prop.Position = new Vector3( -1705.096f, -5812.861f, 0f );
					prop.IsPersistent = false;
					prop.MarkAsNoLongerNeeded();
					prop.Delete();
					Arrest.HandcuffProp = -1;
				}
			}
			catch( Exception ex ) {
				Log.Error( ex );
			}
		}
	}
}