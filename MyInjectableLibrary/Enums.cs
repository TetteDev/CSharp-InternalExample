using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MyInjectableLibrary
{
	public class Enums
	{
		public enum GameState
		{
			MENU = 0,
			GAME = 6
		}

		public enum WeaponID
		{
			WEAPON_DEAGLE = 1,
			WEAPON_ELITE,
			WEAPON_FIVESEVEN,
			WEAPON_GLOCK,
			WEAPON_AK47 = 7,
			WEAPON_AUG,
			WEAPON_AWP,
			WEAPON_FAMAS,
			WEAPON_G3SG1,
			WEAPON_GALIL = 13,
			WEAPON_M249,
			WEAPON_M4A4 = 16,
			WEAPON_MAC10,
			WEAPON_P90 = 19,
			WEAPON_UMP45 = 24,
			WEAPON_XM1014,
			WEAPON_BIZON,
			WEAPON_MAG7,
			WEAPON_NEGEV,
			WEAPON_SAWEDOFF,
			WEAPON_TEC9,
			WEAPON_TASER,
			WEAPON_HKP2000,
			WEAPON_MP7,
			WEAPON_MP9,
			WEAPON_NOVA,
			WEAPON_P250,
			WEAPON_SCAR20 = 38,
			WEAPON_SG556,
			WEAPON_SSG08,
			WEAPON_KNIFE = 42,
			WEAPON_FLASHBANG,
			WEAPON_HEGRENADE,
			WEAPON_SMOKEGRENADE,
			WEAPON_MOLOTOV,
			WEAPON_DECOY,
			WEAPON_INCGRENADE,
			WEAPON_C4,
			WEAPON_KNIFE_T = 59,
			WEAPON_M4A1_SILENCER = 60,
			WEAPON_USP_SILENCER = 61,
			WEAPON_CZ75A = 63,
			WEAPON_REVOLVER = 64,
			WEAPON_KNIFE_BAYONET = 500,
			WEAPON_KNIFE_FLIP = 505,
			WEAPON_KNIFE_GUT = 506,
			WEAPON_KNIFE_KARAMBIT = 507,
			WEAPON_KNIFE_M9_BAYONET = 508,
			WEAPON_KNIFE_TACTICAL = 509,
			WEAPON_KNIFE_FALCHION = 512,
			WEAPON_KNIFE_SURVIVAL_BOWIE = 514,
			WEAPON_KNIFE_BUTTERFLY = 515,
			WEAPON_KNIFE_PUSH = 516,
			WEAPON_ERROR = 9999,
		}
		public enum MoveType : int
		{
			MOVETYPE_NONE = 0,            /**< never moves */
			MOVETYPE_ISOMETRIC,            /**< For players */
			MOVETYPE_WALK,                /**< Player only - moving on the ground */
			MOVETYPE_STEP,                /**< gravity, special edge handling -- monsters use this */
			MOVETYPE_FLY,                /**< No gravity, but still collides with stuff */
			MOVETYPE_FLYGRAVITY,        /**< flies through the air + is affected by gravity */
			MOVETYPE_VPHYSICS,            /**< uses VPHYSICS for simulation */
			MOVETYPE_PUSH,                /**< no clip to world, push and crush */
			MOVETYPE_NOCLIP,            /**< No gravity, no collisions, still do velocity/avelocity */
			MOVETYPE_LADDER,            /**< Used by players only when going onto a ladder */
			MOVETYPE_OBSERVER,            /**< Observer movement, depends on player's observer mode */
			MOVETYPE_CUSTOM,            /**< Allows the entity to describe its own physics */
		}

		public enum LifeState
		{
			Alive = 0,
			KillCam = 1, // playing death animation or still falling off of a ledge waiting to hit ground
			Dead = 2
		}

		public enum Team
		{
			None = 0,
			Spectator = 1,
			Terrorists = 2,
			CounterTerrorists = 3
		}

		[Flags]
		public enum PlayerFlags : int
		{
			FL_ONGROUND = (1 << 0),
			FL_DUCKING = (1 << 1),
			FL_WATERJUMP = (1 << 2),
			FL_ONTRAIN = (1 << 3),
			FL_INRAIN = 1 << 4,
			FL_FROZEN = 1 << 5,
			FL_ATCONTROLS = 1 << 6,
			FL_CLIENT = 1 << 7,
			FL_FAKECLIENT = 1 << 8,
			FL_INWATER = 1 << 9,
			FL_PARTIALGROUND = 1 << 18,
		}
	}
}
