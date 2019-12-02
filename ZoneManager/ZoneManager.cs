using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

using Oxide.Core;
using Oxide.Core.Plugins;

using CodeHatch.Blocks;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Damaging;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Networking;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Thrones.AncientThrone;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch;
using CodeHatch.Blocks.Inventory;
using CodeHatch.Core.Registration;
using CodeHatch.Engine.Core.Interaction.Behaviours.Networking;
using CodeHatch.Engine.Core.Interaction.Players;
using CodeHatch.Engine.Modules.SocialSystem.Objects;
using static CodeHatch.Blocks.Networking.Events.CubeEvent;

namespace Oxide.Plugins{
    [Info("ZoneManager", "PierreA", "1.0.0")]
    public class ZoneManager : ReignOfKingsPlugin {

        /*=============================================
         * Last update: 29.11.2019
         * Contact: pierre.anken@gmail.com
         * https://github.com/PierreAnken/ROK_Plugins
         ==============================================*/

        #region Configuration Data
        private Collection<Zone> _Zones = new Collection<Zone>();
        private List<string> validCommands = new List<string>(new string[]{"add", "list", "setA", "setB","build","cubeDmg"});
        #endregion

        #region classes
        private class Zone
        {
            public bool active;
			public string name;
			public int damagePerSeconde;
            public bool objectDamage = true;
            public bool cubeDamage = true;
            public bool playerDamage = true;
            public bool canBuild = false;
            public Coordinate pointA;
            public Coordinate pointB; 
            public bool round = false; //square: A+B are opposite corners - round: A : center + B radius ends
            public Collection<ulong> playersInZone = new Collection<ulong>();
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
            setUpTimerUpdatePlayersZone();
        }
		
		private void LoadData(){
            _Zones = Interface.GetMod().DataFileSystem.ReadObject<Collection<Zone>>("Zones");
		}
		
		private void SaveData(){
			Interface.GetMod().DataFileSystem.WriteObject("Zones", _Zones);
		}
		
        override
		protected void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{ "EnteringZone", "Entrée {0}" },
                { "ZoneAdded", "Zone ajoutée" },
                { "InvalidCommand", "Commande invalide. Utiliser /zm pour l'aide" },
                { "ZoneHelp", "Zone manager - commandes:#" +
                              "/zm add <NomZone> pour créer une nouvelle zone#" +
                              "/zm list pour voir toutes les zones" },
                { "InvalidZoneName", "Nom de zone invalide '{0}', min. 3 caractères, max. 20" },
                { "LeavingZone", "Sortie {0}" },
                { "ZoneListHeader", "ID - Nom - Active - Dgts - DgtsObj - DgtsJoueurs  -Constr." }
            }, this,"fr");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
                { "EnteringZone", "Entering {0}" },
                { "ZoneAdded", "Zone added" },
                { "InvalidCommand", "Invalide command. Use /zm for help" },
                { "ZoneHelp", "Zone manager - commands:#" +
                              "/zm add <ZoneName> : create new zone#" +
                              "/zm list : to see all zones" },
                { "InvalidZoneName", "Zone name is invalide '{0}', min. 3 chars, max 20" },
                { "LeavingZone", "Leaving {0}" },
                { "ZoneListHeader", "ID - Name - Active - Dmg - ObjDmg - PlayerDmg - Build" }
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
                        if(checkParams(args, player, 2))
                            newZone(player, args[1]);
                        break;
                    case "list":
                        if (checkParams(args, player))
                            displayZoneList(player);
                        break;
                    case "setA":
                    case "setB":
                        if (checkParams(args, player, 2))
                            setPointForZone(args, player);
                        break;
                    case "build":
                        if (checkParams(args, player))
                            toggleBuild(player);
                        break;
                    case "cubeDmg":
                        if (checkParams(args, player))
                            toggleCubeDmg(player);
                        break;
                    default:
                        sendError(player, GetMessage("InvalidCommand"));
                        break;
                }
            }
            else
            {
                sendError(player, GetMessage("InvalidCommand"));
            }
		}

        
        private void toggleBuild(Player player)
        {
            Zone zone = getPlayerZone(player);
            if (zone != null){

                bool canBuild = zone.canBuild;
                zone.canBuild = !canBuild;
                //TODO: Translate
                PrintToChat(player, "Construction activée ? " + !canBuild);
                SaveData();
            }
            else {
                sendError(player, "Vous n'êtes pas dans une zone.");
            }
        }

        private void toggleCubeDmg(Player player)
        {
            Zone zone = getPlayerZone(player);
            if (zone != null)
            {
                bool cubeDamage = zone.cubeDamage;
                zone.cubeDamage = !cubeDamage;
                //TODO: Translate
                PrintToChat(player, "Dégats aux cubes activés ? " + !cubeDamage);
                SaveData();
            }
            else
            {
                sendError(player, "Vous n'êtes pas dans une zone.");
            }
        }

        private void setPointForZone(string[] parameters, Player player) {
            
            int zoneId = getZoneId(parameters[1], player);
            if(zoneId > -1)
            {
                //TODO: Check if point in another zone
                Coordinate point = coordinateFromPosition(getPosition(player));
                if(parameters[0] == "setA")
                {
                    _Zones[zoneId].pointA = point;
                    //TODO Translation
                    PrintToChat(player, "Point A set for zone "+zoneId);
                }
                else {
                    _Zones[zoneId].pointB = point;
                    //TODO Translation
                    PrintToChat(player, "Point B set for zone " + zoneId);
                }
                SaveData();
            }
        }

        private void newZone(Player player, string name)
        {
            if (player.HasPermission("admin"))
            {
                if (name.Length < 3 || name.Length > 20)
                {
                    string message = string.Format(GetMessage("InvalidZoneName"), name);
                    sendError(player, message);
                }
                else
                {
                    Zone newZone = new Zone()
                    {
                        name = name
                    };

                    _Zones.Add(newZone);
                    SaveData();
                    PrintToChat(player, GetMessage("ZoneAdded"));
                }
            }
        }

        private bool checkParams(string[] parameters, Player player, int numberParams = 1)
        {
            if (parameters.Length != numberParams)
            {
                sendError(player, GetMessage("InvalidCommand"));
                return false;
            }
            else
            {
                foreach (string param in parameters)
                {
                    if (param.Replace(" ", string.Empty) == "")
                    {
                        sendError(player, GetMessage("InvalidCommand"));
                        return false;
                    }
                }
            }
            return true;
        }

        private void displayZoneHelp(Player player)
        {
            foreach (string command in GetMessage("ZoneHelp").Split('#'))
                PrintToChat(player, command);
        }

        private void displayZoneList(Player player)
        {
            PrintToChat(player, GetMessage("ZoneListHeader"));
            int i = 0;
            foreach (Zone zone in _Zones)
            {
                String message = string.Format("{0} - {1} - {2} - {3} - {4} - {5} - {6}", i, zone.name, zone.active, zone.damagePerSeconde, zone.objectDamage, zone.playerDamage, zone.canBuild);
                PrintToChat(player, message);
                i++;
            }
        }

        #endregion

        #region Hooks
        private void OnCubePlacement(CubePlaceEvent placeEvent)
        {
            
            #region Check
            if (placeEvent == null) return;
            if (placeEvent.Cancelled) return;
            if (placeEvent.Material == 0) return;
            if (placeEvent.Entity == null) return;
            if (placeEvent.Entity.Owner == null) return;
            #endregion

            Player player = placeEvent.Entity.Owner;

            if (!player.HasPermission("admin"))
            {
                Zone currentZone = getPlayerZone(player);
                if (currentZone != null)
                {
                    if (!currentZone.canBuild)
                    {
                        //TODO: To translate
                        sendError(player, "Pas de construction dans cette zone.");
                        placeEvent.Cancel();
                    }
                }
            }
        }

        private void OnCubeTakeDamage(CubeDamageEvent damageEvent)
        {
            #region Checks
            if (damageEvent == null) return;
            if (damageEvent.Cancelled) return;
            if (damageEvent.Damage == null) return;
            if (!damageEvent.Damage.DamageSource.IsPlayer) return;
            if (damageEvent.Damage.DamageSource.Owner == null) return;
            if (damageEvent.Damage.Amount <= 0) return;
            #endregion

            Player player = damageEvent.Damage.DamageSource.Owner;
            if (!player.HasPermission("admin"))
            {
                Zone currentZone = getPlayerZone(player);
                if (currentZone != null)
                {
                    if (!currentZone.cubeDamage)
                    {
                        //TODO: To translate
                        sendError(player, "Pas de dégats aux cubes dans la zone.");
                        
                        var centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(damageEvent.Position);
                        if (centralPrefabAtLocal != null)
                        {
                            var component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                            if (component != null) component.info.NotSalvageable = true;
                        }

                        damageEvent.Damage.Amount = 0f;
                        damageEvent.Cancel();
                        return;
                    }
                }
            }
        }


        #endregion


        #region Functions

        private void sendError(Player player, string message) {
            if (player != null && message != string.Empty) {
                PrintToChat(player, "[ff0000]"+ message + "[ffffff]");
            }
        }

        private int getZoneId(string zoneIdS, Player player)
        {
            int zoneId = -1;
            Int32.TryParse(zoneIdS, out zoneId);
            if (zoneId == -1)
            {
                sendError(player, GetMessage("InvalidCommand"));
            }
            else
            {
                bool valid = zoneId + 1 <= _Zones.Count && zoneId >= 0;
                if (!valid)
                {
                    sendError(player, "Zone " + zoneId + " dosen't exists");
                }
            }
            return zoneId;
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
                foreach(Zone zone in _Zones) {
                    bool playerInZoneList = zone.playersInZone.Contains(player.Id);
                    bool playerInZone = isPositionInZone(getPosition(player), zone);

                    if (playerInZone)
                    {
                       
                        if (!playerInZoneList)
                        {
                            //todo translate
                            PrintToChat(player, string.Format("[46A0BC] {0} [FFFFFF] - entrée", zone.name));
                            zone.playersInZone.Add(player.Id);
                        }

                        break;
                    }
                    else if(playerInZoneList)
                    {
                        //todo translate
                        PrintToChat(player, string.Format("[46A0BC] {0} [FFFFFF] - sortie", zone.name));
                        zone.playersInZone.Remove(player.Id);
                    }
                }
				
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

        private float DistanceCoordinateACoordinateB(Coordinate pointA, Coordinate pointB)
        {
            float distance = 0;
            if (pointA != null && pointB != null)
            {
                float[] positionA = new float[] { pointA.x, pointA.y, 0 };
                float[] positionB = new float[] { pointB.x, pointB.y, 0 };
                distance = DistancePointAPointB(positionA, positionB);
            }
            return distance;
        }

        private float DistanceCoordinatePoint(Coordinate pointA, float[] pointB) {
            float distance = 0;
            if (pointA != null) {
                float[] positionA = new float[] { pointA.x, pointA.y, 0};
                pointB[2] = 0;
                distance = DistancePointAPointB(positionA, pointB);
            }
            return distance;
        }

        private float DistancePointAPointB(float[] pointA, float[] pointB)
        {
            float distance = 0;
            if (pointA != null && pointB != null)
            {
                if (pointA.Length == 3 && pointB.Length == 3)
                {
                    Vector3 vector3 = new Vector3(pointA[0] - pointB[0], pointA[1] - pointB[1], pointA[2] - pointB[2]);
                    distance = Mathf.Sqrt((float)((double)vector3.x * (double)vector3.x + (double)vector3.y * (double)vector3.y + (double)vector3.z * (double)vector3.z));
                }
            }
            return distance;
        }

        private float[] getPosition(Player joueur)
        {
            float[] position = null;
            if (joueur != null && joueur.Entity != null)
            {
                position = new float[] { joueur.Entity.Position.x, joueur.Entity.Position.z,  joueur.Entity.Position.y };
            }
            return position;
        }

        private Zone getPlayerZone(Player player) {
            if(player != null) {
                float[] position = getPosition(player);
                foreach (Zone zone in _Zones){
                    if(isPositionInZone(position, zone)){
                        return zone;
                    }
                };
            }
            return null;
        }

        private bool isPositionInZone(float[] position, Zone zone) {
            try
            {
                if (zone != null || position != null)
                {
                    if (position.Length > 1 && zone.pointA != null && zone.pointB != null)
                    {
                        if (zone.round)
                        {
                            float radiusZone = DistanceCoordinateACoordinateB(zone.pointA, zone.pointB);
                            return DistanceCoordinatePoint(zone.pointA, position) > radiusZone;
                        }
                        else
                        {

                            float posX = position[0];
                            float posY = position[1];

                            float zoneXmin = zone.pointA.x > zone.pointB.x ? zone.pointB.x : zone.pointA.x;
                            float zoneXmax = zone.pointA.x > zone.pointB.x ? zone.pointA.x : zone.pointB.x;

                            float zoneYmin = zone.pointA.y > zone.pointB.y ? zone.pointB.y : zone.pointA.y;
                            float zoneYmax = zone.pointA.y > zone.pointB.y ? zone.pointA.y : zone.pointB.y;

                            if ((zoneXmin <= posX && posX <= zoneXmax) && (zoneYmin <= posY && posY <= zoneYmax))
                            {
                                return true;
                            }
                        }

                    }
                }
            }
            catch(Exception e){
                Puts("Error isPositionInZone: " + e.Message);
            }
            return false;
        }

        private Coordinate coordinateFromPosition(float[] position) {
            Coordinate newCoordinate = null;
            if (position != null && position.Length > 1) {
                newCoordinate = new Coordinate()
                {
                    x = position[0],
                    y = position[1]
                };

            }
            return newCoordinate;
        }

        #endregion
    }
}