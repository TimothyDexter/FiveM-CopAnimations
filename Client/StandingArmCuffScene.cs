/*
 * 
 * Standing Arm Cuff Animation
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
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Roleplay.Client.Classes.Jobs.Police;
using Roleplay.Client.Classes.Player;
using Roleplay.Client.Enums;
using Roleplay.SharedClasses;

namespace Roleplay.Client.Classes.Actions.CopAnimations
{
	public class StandingArmCuffScene
	{
		public enum ArrestPositionEnum
		{
			Back,
			Front
		}

		public enum RoleEnum
		{
			Perp,
			Cop
		}
		private const string AnimDictionary = "rcmpaparazzo_3";
		private const string CopAnim = "poppy_arrest_cop";
		private const string PerpAnim = "poppy_arrest_popm";

		private const string HandCuffPropName = "p_cs_cuffs_02_s";

		private const string HandcuffingSound = "handcuffsPutOn";
		private const float SoundVolume = 0.25f;
		private const float SoundDistanceThreshold = 0.2f;

		private const float BackArrestStartSceneTime = 0.6f;
		private const float FrontArrestStartSceneTime = 0.56f;
		private const float SceneSoundTime = 0.635f;
		private const float SceneStopTime = 0.64f;

		private readonly Vector3 _sceneStartOffset = new Vector3( -6.6365965f, -3.0495602f, -0.96f );

		private bool _isDictionaryLoaded;

		/// <summary>
		/// Model used to send event information to client of arrested player
		/// </summary>
		public class SceneModel
		{
			public Vector3 PerpPos { get; set; }
			public Vector3 PerpRot { get; set; }
			public int ScenePos { get; set; }

			public SceneModel() {

			}

			public SceneModel( Vector3 perpPos, Vector3 perpRot, int arrestPos ) {
				PerpPos = perpPos;
				PerpRot = perpRot;
				ScenePos = arrestPos;
			}
		}

		public string Sound { get; }
		public string PropName { get; }

		public StandingArmCuffScene( string sound = "", string propName = "" ) {
			Sound = string.IsNullOrEmpty( sound ) ? HandcuffingSound : sound;
			PropName = string.IsNullOrEmpty( propName ) ? HandCuffPropName : propName;
		}

		/// <summary>
		/// Executes the scene animations according to role and position.
		/// </summary>
		/// <param name="perpPos">The perp position.</param>
		/// <param name="perpRot">The perp rot.</param>
		/// <param name="arrestPosition">The arrest position: front or back</param>
		/// <param name="role">The client role in the arrest: cop or perp</param>
		/// <returns></returns>
		public async Task PlayClientScene( Vector3 perpPos, Vector3 perpRot, ArrestPositionEnum arrestPosition, RoleEnum role ) {
			try {
				await LoadAnimDictionary();

				if( !IsStartPositionInRange( perpPos ) ) return;

				if( role == RoleEnum.Perp ) {
					CurrentPlayer.Ped.Task.ClearAllImmediately();
				}

				if( arrestPosition == ArrestPositionEnum.Front ) {
					if( role == RoleEnum.Cop ) {
						PlayCopAnimationFromFront( Cache.PlayerHandle, perpPos, perpRot.Z );
					}
					else {
						PlayPerpAnimationFromFront( Cache.PlayerHandle, perpPos, perpRot );
					}
				}
				else {
					if( role == RoleEnum.Cop ) {
						PlayCopAnimationFromBack( Cache.PlayerHandle, perpPos, perpRot.Z );
					}
					else {
						PlayPerpAnimationFromBack( Cache.PlayerHandle, perpPos, perpRot );
					}
				}

				await WaitForSceneCompletion( Cache.PlayerHandle, role, arrestPosition );

				CurrentPlayer.Ped.Task.ClearAll();
			}
			catch( Exception ex ) {
				CurrentPlayer.EnableWeaponWheel( true );
				Log.Error( ex );
			}
		}

		/// <summary>
		/// Plays the client perp scene.
		/// </summary>
		/// <param name="model">The model.</param>
		/// <returns></returns>
		public async Task PlayClientPerpScene( SceneModel model ) {
			if( model == null ) return;
			await PlayClientScene( model.PerpPos, model.PerpRot, (ArrestPositionEnum) model.ScenePos, RoleEnum.Perp );
		}

		/// <summary>
		///     Plays the scene.
		/// </summary>
		/// <param name="pedToArrest">The ped to arrest.</param>
		/// <param name="arrestPosition">The arrest position.</param>
		/// <returns></returns>
		public async Task DemoScene( int pedToArrest ) {
			try {
				await LoadAnimDictionary();

				var perp = new Ped( pedToArrest );

				if( !perp.Exists() ) return;

				var perpCurrPos = perp.Position;
				var perpCurrRot = perp.Rotation;

				if( !IsStartPositionInRange( perpCurrPos ) ) return;

				var arrestPosition = GetArrestPosition( perpCurrPos, perpCurrRot.Z );

				if( arrestPosition == ArrestPositionEnum.Front ) {
					PlayPerpAnimationFromFront( perp.Handle, perpCurrPos, perpCurrRot );
					PlayCopAnimationFromFront( Cache.PlayerHandle, perpCurrPos, perpCurrRot.Z );
				}
				else {
					PlayPerpAnimationFromBack( perp.Handle, perpCurrPos, perpCurrRot );
					PlayCopAnimationFromBack( Cache.PlayerHandle, perpCurrPos, perpCurrRot.Z );
				}

				await WaitForSceneCompletion( Cache.PlayerHandle, RoleEnum.Cop, arrestPosition, perp.Handle );

				CurrentPlayer.Ped.Task.ClearAll();
				perp.Task.ClearAll();

				perp.Task.PlayAnimation( "mp_arresting", "idle", 8f, -8f, -1, (AnimationFlags)49, 0 );
			}
			catch( Exception ex ) {
				Log.Error( ex );
			}
		}

		/// <summary>
		/// Gets the cop relative heading.
		/// </summary>
		/// <param name="perpPosition">The perp position.</param>
		/// <param name="copPosition">The cop position.</param>
		/// <returns></returns>
		public static double GetCopRelativeHeading( Vector3 perpPosition, Vector3 copPosition ) {
			var heading = Math.Atan2( perpPosition.Y - copPosition.Y,
							  perpPosition.X - copPosition.X ) * (180f / Math.PI);
			return heading;
		}

		public static int GetClientPositionToPerp( Vector3 perpPosition, float heading ) {
			var clientPos = CurrentPlayer.Ped.Position;

			var backPos = API.GetObjectOffsetFromCoords( perpPosition.X, perpPosition.Y, perpPosition.Z, heading, 0,
				-0.5f, 0 );
			var frontPos = API.GetObjectOffsetFromCoords( perpPosition.X, perpPosition.Y, perpPosition.Z, heading, 0,
				0.5f, 0 );

			var backDist = clientPos.DistanceToSquared2D( backPos );
			var frontDist = clientPos.DistanceToSquared2D( frontPos );

			if( backDist - 0.1f <= frontDist ) {
				return -1;
			}

			return 1;
		}


		/// <summary>
		/// Gets the arrest position.
		/// </summary>
		/// <param name="perpPosition">The perp position.</param>
		/// <returns></returns>
		public static ArrestPositionEnum GetArrestPosition( Vector3 perpPosition, float heading ) {
			if( GetClientPositionToPerp( perpPosition, heading ) > 0 ) {
				return ArrestPositionEnum.Front;
			}

			return ArrestPositionEnum.Back;
		}

		/// <summary>
		/// Shoulds the client perform animation.
		/// </summary>
		/// <param name="role">The role.</param>
		/// <returns></returns>
		public bool ShouldClientPerformAnimation( RoleEnum role ) {
			if( Cache.IsPlayerInVehicle || CurrentPlayer.Ped.IsRagdoll || DeathHandler.IsPlayerDead ) return false;

			if( role == RoleEnum.Perp ) {
				if( Arrest.PlayerCuffState != CuffState.Cuffed ||
					API.IsEntityPlayingAnim( Cache.PlayerHandle, "mp_arresting", "idle", 3 ) ) {
					return false;
				}
			}

			return true;
		}

		/// <summary>
		///     Loads the anim dictionary.
		/// </summary>
		/// <returns></returns>
		private async Task LoadAnimDictionary() {
			if( _isDictionaryLoaded ) return;

			while( !API.HasAnimDictLoaded( AnimDictionary ) ) {
				API.RequestAnimDict( AnimDictionary );
				await BaseScript.Delay( 100 );
			}

			_isDictionaryLoaded = API.HasAnimDictLoaded( AnimDictionary );
		}

		/// <summary>
		///     Waits for scene completion.
		/// </summary>
		/// <param name="handle">The handle.</param>
		/// <param name="role">The role.</param>
		/// <param name="arrestPosition">The arrest position.</param>
		/// <param name="demoHandle">Only used for DemoScene()</param>
		/// <returns></returns>
		private async Task WaitForSceneCompletion( int handle, RoleEnum role, ArrestPositionEnum arrestPosition,
			int demoHandle = -1 ) {
			float sceneStartTime = arrestPosition == ArrestPositionEnum.Front
				? FrontArrestStartSceneTime
				: BackArrestStartSceneTime;
			string anim = role == RoleEnum.Cop ? CopAnim : PerpAnim;
			float currentSceneTime = sceneStartTime;
			bool hasCuffEventOccurred = false;

			try {
				while( currentSceneTime < SceneStopTime ) {
					currentSceneTime = API.GetEntityAnimCurrentTime( handle, AnimDictionary, anim );
					if( !hasCuffEventOccurred && currentSceneTime >= SceneSoundTime ) {
						if( demoHandle > 0 ) {
							HandcuffPed( demoHandle );
							BaseScript.TriggerServerEvent( "InteractSound_SV:PlayWithinDistance", SoundDistanceThreshold,
								Sound, SoundVolume );
						}
						else if( role == RoleEnum.Cop ) {
							BaseScript.TriggerServerEvent( "InteractSound_SV:PlayWithinDistance", SoundDistanceThreshold,
								Sound, SoundVolume );
						}
						else {
							HandcuffPed( handle );
						}

						hasCuffEventOccurred = true;
					}

					await BaseScript.Delay( 10 );
				}
			}
			catch( Exception ex ) {
				Log.Error( ex );
			}
		}

		/// <summary>
		///     Handcuffs the ped.
		/// </summary>
		/// <param name="handle">The handle.</param>
		private async void HandcuffPed( int handle ) {
			if( Arrest.HandcuffProp < 0 || !Entity.FromHandle( Arrest.HandcuffProp ).Exists() ) {
				Arrest.HandcuffProp = await CreateHandCuffs( handle, PropName );
				AttachHandCuffsToPerp( Arrest.HandcuffProp, handle );
			}
		}

		/// <summary>
		///     Plays the perp animation from front.
		/// </summary>
		/// <param name="perpHandle">The perp handle.</param>
		/// <param name="perpPosition">The perp position.</param>
		/// <param name="perpRotation">The perp rotation.</param>
		private static void PlayPerpAnimationFromFront( int perpHandle, Vector3 perpPosition, Vector3 perpRotation ) {
			float offsetToRetainPerpStartHeadingUponAnimFinish = -65.567f;
			PlaySceneAnimation( perpHandle, perpPosition,
				new Vector3( perpRotation.X, perpRotation.Y,
					perpRotation.Z + offsetToRetainPerpStartHeadingUponAnimFinish ), RoleEnum.Perp,
				ArrestPositionEnum.Front );
		}

		/// <summary>
		///     Plays the perp animation from back.
		/// </summary>
		/// <param name="perpHandle">The perp handle.</param>
		/// <param name="perpPosition">The perp position.</param>
		/// <param name="perpRotation">The perp rotation.</param>
		private static void PlayPerpAnimationFromBack( int perpHandle, Vector3 perpPosition, Vector3 perpRotation ) {
			float offsetToRetainPerpStartHeadingUponAnimFinish = -38.8f;
			PlaySceneAnimation( perpHandle, perpPosition,
				new Vector3( perpRotation.X, perpRotation.Y,
					perpRotation.Z + offsetToRetainPerpStartHeadingUponAnimFinish ), RoleEnum.Perp,
				ArrestPositionEnum.Back );
		}

		/// <summary>
		///     Plays the cop animation from front.
		/// </summary>
		/// <param name="copHandle">The cop handle.</param>
		/// <param name="perpPosition">The perp position.</param>
		/// <param name="perpHeading">The perp heading.</param>
		private static void PlayCopAnimationFromFront( int copHandle, Vector3 perpPosition, float perpHeading ) {
			var cop = new Ped( copHandle );
			if( !cop.Exists() ) return;

			var copAnimPos = GetCopFromFrontAnimStartPosition( perpPosition, perpHeading );
			var copAnimRot = GetCopFromFrontAnimStartRotation( cop.Rotation, perpHeading );

			PlaySceneAnimation( copHandle, copAnimPos,
				copAnimRot, RoleEnum.Cop, ArrestPositionEnum.Front );
		}

		/// <summary>
		///     Gets the cop from front start position.
		/// </summary>
		/// <param name="perpPosition">The perp position.</param>
		/// <param name="perpHeading">The perp heading.</param>
		/// <returns></returns>
		private static Vector3 GetCopFromFrontAnimStartPosition( Vector3 perpPosition, float perpHeading ) {
			float magicRotationOffset = 8.569f;
			float vectorHeading = perpHeading + magicRotationOffset;

			double cosx = Math.Cos( vectorHeading * (Math.PI / 180f) );
			double siny = Math.Sin( vectorHeading * (Math.PI / 180f) );

			float vectorMagnitude = 0.9405f;
			float deltax = (float)(vectorMagnitude * cosx);
			float deltay = (float)(vectorMagnitude * siny);

			return perpPosition + new Vector3( deltax, deltay, 0 );
		}

		/// <summary>
		///     Gets the cop from front anim start rotation.
		/// </summary>
		/// <param name="copRotation">The cop rotation.</param>
		/// <param name="perpHeading">The perp heading.</param>
		/// <returns></returns>
		private static Vector3 GetCopFromFrontAnimStartRotation( Vector3 copRotation, float perpHeading ) {
			float offsetCopHeadingFromPed = 167f;
			return new Vector3( copRotation.X, copRotation.Y, perpHeading + offsetCopHeadingFromPed );
		}

		/// <summary>
		///     Plays the cop animation from back.
		/// </summary>
		/// <param name="copHandle">The cop handle.</param>
		/// <param name="perpPosition">The perp position.</param>
		/// <param name="perpHeading">The perp heading.</param>
		private static void PlayCopAnimationFromBack( int copHandle, Vector3 perpPosition, float perpHeading ) {
			var cop = new Ped( copHandle );
			if( !cop.Exists() ) return;

			var copAnimPos = GetCopFromBackAnimStartPosition( perpPosition, perpHeading );
			var copAnimRot = GetCopFromBackAnimStartRotation( cop.Rotation, perpHeading );

			PlaySceneAnimation( copHandle, copAnimPos, copAnimRot, RoleEnum.Cop, ArrestPositionEnum.Back );
		}

		/// <summary>
		///     Gets the cop from back anim start position.
		/// </summary>
		/// <param name="perpPosition">The perp position.</param>
		/// <param name="perpHeading">The perp heading.</param>
		/// <returns></returns>
		private static Vector3 GetCopFromBackAnimStartPosition( Vector3 perpPosition, float perpHeading ) {
			float magicRotationOffset = 68.20297f;
			float vectorHeading = perpHeading + magicRotationOffset;

			double sinx = Math.Sin( vectorHeading * (Math.PI / 180f) );
			magicRotationOffset = -21.802f;
			vectorHeading = perpHeading + magicRotationOffset;
			double siny = Math.Sin( vectorHeading * (Math.PI / 180f) );

			float vectorMagnitude = 0.5385f;
			float deltax = (float)(vectorMagnitude * sinx);
			float deltay = (float)(vectorMagnitude * siny);

			return perpPosition + new Vector3( deltax, deltay, 0 );
		}

		/// <summary>
		///     Gets the cop from back anim start rotation.
		/// </summary>
		/// <param name="copRotation">The cop rotation.</param>
		/// <param name="perpHeading">The perp heading.</param>
		/// <returns></returns>
		private static Vector3 GetCopFromBackAnimStartRotation( Vector3 copRotation, float perpHeading ) {
			float offsetCopHeadingFromPed = 53f;
			return new Vector3( copRotation.X, copRotation.Y, perpHeading + offsetCopHeadingFromPed );
		}

		/// <summary>
		///     Plays the scene animation.
		/// </summary>
		/// <param name="handle">The handle.</param>
		/// <param name="position">The position.</param>
		/// <param name="rotation">The rotation.</param>
		/// <param name="role">The role.</param>
		/// <param name="arrestPosition">The arrest position.</param>
		private static async void PlaySceneAnimation( int handle, Vector3 position, Vector3 rotation, RoleEnum role,
			ArrestPositionEnum arrestPosition ) {
			string anim = role == RoleEnum.Cop ? CopAnim : PerpAnim;
			float animationLength = arrestPosition == ArrestPositionEnum.Front
				? FrontArrestStartSceneTime
				: BackArrestStartSceneTime;

			API.TaskGoStraightToCoord( Cache.PlayerHandle, position.X, position.Y, position.Z, 1f, 5000,
				rotation.Z, 6f );

			await BaseScript.Delay( 600 );

			API.TaskPlayAnimAdvanced( handle, AnimDictionary, anim,
				position.X, position.Y, position.Z,
				rotation.X, rotation.Y, rotation.Z, 8f, -8f, 5250,
				262152, animationLength, 2, 0 );
		}

		/// <summary>
		///     Creates the hand cuffs.
		/// </summary>
		/// <param name="playerHandle">The player handle.</param>
		/// <returns></returns>
		private static async Task<int> CreateHandCuffs( int playerHandle, string propName ) {
			try {
				var model = new Model( propName );
				await model.Request( 250 );

				if( !model.IsInCdImage || !model.IsValid ) return -1;

				while( !model.IsLoaded ) await BaseScript.Delay( 10 );

				var ped = new Ped( playerHandle );

				if( !ped.Exists() ) return -1;

				var offsetPosition = ped.GetOffsetPosition( Vector3.One );
				var attachPosition = API.GetPedBoneCoords( playerHandle, (int)Bone.SKEL_R_Hand, offsetPosition.X,
					offsetPosition.Y,
					offsetPosition.Z );

				var prop = await World.CreateProp( model, attachPosition, new Vector3( 0, 0, 0 ),
					false, false );
				model.MarkAsNoLongerNeeded();
				return prop.Handle;
			}
			catch( Exception ex ) {
				Log.Error( ex );
				return -1;
			}
		}

		/// <summary>
		///     Attaches the hand cuffs.
		/// </summary>
		/// <param name="cuffsHandler">The cuffs handler.</param>
		/// <param name="playerHandle">The player handle.</param>
		private static void AttachHandCuffsToPerp( int cuffsHandler, int perpHandle ) {
			float xPos = 0.01f;
			float yPos = 0.075f;
			float zPos = 0;
			float xRot = 10f;
			float yRot = 45f;
			float zRot = 80f;

			API.AttachEntityToEntity( cuffsHandler, perpHandle,
				API.GetPedBoneIndex( perpHandle, (int)Bone.SKEL_R_Hand ), xPos,
				yPos, zPos, xRot,
				yRot, zRot,
				true,
				true, false, true, 1, true );
		}

		/// <summary>
		///     Determines whether [is start position in range] [the specified start position].
		/// </summary>
		/// <param name="startPos">The start position.</param>
		/// <returns>
		///     <c>true</c> if [is start position in range] [the specified start position]; otherwise, <c>false</c>.
		/// </returns>
		private bool IsStartPositionInRange( Vector3 startPos ) {
			float distanceToStartPos = CurrentPlayer.Ped.Position.DistanceToSquared2D( startPos );

			if( distanceToStartPos < 10f )
				return true;

			// Fail gracefully
			Log.Info( $"Destination for scene unusually far: {distanceToStartPos}" );
			return false;
		}
	}
}