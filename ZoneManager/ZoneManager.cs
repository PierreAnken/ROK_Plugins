using UnityEngine;
using System;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Networking;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Damaging;
using Oxide.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Oxide.Plugins{
    [Info("ZoneManager", "PierreA", "1.0.0")]
    public class RaidOffDefender : ReignOfKingsPlugin{

        /*=============================================
         * Last update: 29.11.2019
         * Contact: pierre.anken@gmail.com
         * https://github.com/PierreAnken/ROK_Plugins
         ==============================================*/

        #region Configuration Data
		private CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
		private Collection<Zone> _Zones = new Collection<Zone>();
        private Zone currentEdit;
        private const List<string> validCommands = { "add" };
        #endregion

        #region classes
        private class Zone
        {
			public int id;
            public bool active = false;
			public string name;
			public int damagePerSeconde;
            public bool objectDamage = true;
            public bool playerDamage = true;
            public Coordinate pointA;
            public Coordinate pointB;
            private Collection<ulong> playersInZone;
        }

        private class Coordinate
        {
            public float x;
            public float y;
        }

        #endregion

        #region Config save/load	
        private void Loaded()
        {
            LoadDefaultMessages();
			LoadData();
			setUpTimerUpdatePlayersData();
        }
		
		private void LoadData(){
            _Zones = Interface.GetMod().DataFileSystem.ReadObject<Collection<Zone>>("Zones");
		}
		
		private void SaveData(){
			Interface.GetMod().DataFileSystem.WriteObject("Zones", _Zones);
		}
		
		private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{ "EnteringZone", "Entrée {0}" },
                { "InvalidCommand", "Format de commande invalide. Utiliser /zm pour l'aide" },
                { "ZoneHelp", "/zm add <NomZone> pour créer une nouvelle zone" },
                { "InvalidZoneName", "Nom de zone invalide '{0}', min. 3 caractères" },
                { "EnteringZone", "Sortie {0}" }
            }, this,"fr");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
                { "EnteringZone", "Entering {0}" },
                { "InvalidCommand", "Invalide command format. Use /zm for help" },
                { "ZoneHelp", "/zm add <ZoneName> to create new zone" },
                { "InvalidZoneName", "Zone name is invalide '{0}', min. 3 chars" },
                { "EnteringZone", "Leaving {0}" }
            }, this,"en");
        }	
		#endregion
		
		#region Commands

		[ChatCommand("zm")]
		private void commands(Player player,string cmd, string[] args){
			if(args.Length == 0){
                displayZoneHelp(player);
			}
            else if(validCommands.Contains(args[0])) {
                switch(args[0]) {
                    case "add":
                        newZone(player, args[1]);
                        break;
                    
                }
            }
            else
            {
                //todo invalid command
            }
		}

        #endregion

        #region Hooks

        #endregion

        #region Functions

        private bool checkParams(string[] parameters, string numberParams) {
            bool valid = false;
            if (parameters.Length == numberParams) {
                
            
            }
            return valid;
        }

        private void displayZoneHelp(Player player){
            PrintToChat(player, GetMessage("ZoneHelp"));
        }

        private void newZone(Player player, string name)
        {
            if (player.HasPermission("admin"))
            {
                if(name.length < 3)
                {
                    string msg = string.Format(GetMessage("InvalidZoneName"), name);
                    PrintToChat(player, message);
                }
                else
                {
                    //todo create zone
                }
            }
        }

        private bool isOnline(ulong playerId){
			Player playerExist = Server.GetPlayerById(playerId);
			if (playerExist != null){
				return true;
			}
			return false;
		}
		
		private void setUpTimerUpdatePlayersZone(){
			timer.Repeat(1f, 0, () =>
			{			
				updatePlayersZone();
			});
		}
		
		private void updatePlayersZone(){
			foreach(Player player in getPlayersOnline()){
				//new player
				PlayerInfos playerInfos = getDatasFromPlayer(player);
				if(playerInfos == null){
					_PlayersInfos.Add(
						new PlayerInfos{
							playerId=player.Id,
							playerName=player.Name,
							guildName=PlayerExtensions.GetGuild(player).Name,
						}
					);
				}
				else{
					//update player	
					playerInfos.playerId=player.Id;
					playerInfos.playerName=player.Name;
					playerInfos.guildName=PlayerExtensions.GetGuild(player).Name;
				}
				SaveData();
			}
		}
		#endregion
		
		#region Helpers
		private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
		
		private Vector3 convertPosCubeInCoordinates(Vector3Int positionCube){
			if(positionCube != new Vector3(0,0,0)){
				if(positionCube.x != 0){
					return new Vector3(positionCube.x*1.2f,positionCube.y*1.2f,positionCube.z*1.2f);
				}
			}
			return new Vector3(0,0,0);
		}
		
		private int getTimestamp(){
			return (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;		
		}
		
		private PlayerInfos getDatasFromPlayer(Player player){
			foreach(var playerData in _PlayersInfos){
				if(player.Id == playerData.playerId)
					return playerData;
			}
			return (PlayerInfos)null;
		}
		
		private PlayerInfos getDatasFromPlayer(ulong playerId){
			foreach(var playerData in _PlayersInfos){
				if(playerId == playerData.playerId)
					return playerData;
			}
			return (PlayerInfos)null;
		}
		
		private List<Player> getPlayersOnline(){
			List<Player> listPlayersOnline = new List<Player>();
			foreach(Player player in Server.AllPlayers){
				if(player.Id != 9999999999){
					listPlayersOnline.Add(player);
				}
			}
			return listPlayersOnline;
		}
		
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
		#endregion
	}
}