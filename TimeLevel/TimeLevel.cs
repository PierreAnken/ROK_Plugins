using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

using Oxide.Core;
using Oxide.Core.Plugins;

using CodeHatch;
using CodeHatch.Blocks;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Blocks.Inventory;
using CodeHatch.Common;
using CodeHatch.Core.Registration;
using CodeHatch.Damaging;
using CodeHatch.Engine.Behaviours;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Core.Commands;
using CodeHatch.Engine.Core.Interaction.Behaviours.Networking;
using CodeHatch.Engine.Core.Interaction.Players;
using CodeHatch.Engine.Entities.Definitions;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem.Objects;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Serialization;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Gaming;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Thrones;
using CodeHatch.Thrones.AncientThrone;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Thrones.Weapons.Events;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.UserInterface.General;

using static CodeHatch.Blocks.Networking.Events.CubeEvent;

namespace Oxide.Plugins
{
    [Info("TimeLevel", "Pierre Anken", "1.0.0")]
    public class TimeLevel : ReignOfKingsPlugin
    {

        /*=============================================
         * Last update: 14.12.2019
         * Contact: pierre.anken@gmail.com
         * https://github.com/PierreAnken/ROK_Plugins
         ==============================================*/

        #region Configuration Data
        private static Collection<PlayerLevel> _PlayerLevelData = new Collection<PlayerLevel>();
        private List<string> validCommands = new List<string>(new string[] {"give","giveTo"});
        const int FIRSTXPSTEP = 30;
        const float STEPFACTOR = 0.07f;
        const int STEPBASEINCREMENT = 45;
        const int XPMINUTE = 1;
        #endregion

        #region classes
        private class PlayerLevel
        {
            public ulong playerId;
            public int xp;
            public int currentLevel;
            public string playerName;
            public float[] lastPosition = null;

            public PlayerLevel(ulong playerId,string playerName, int xp = 0) {
                this.playerId = playerId;
                this.playerName = playerName;
                this.xp = xp;
            }

            override
            public string ToString()
            {
                return string.Format("name: {0} - xp: {1} - lvl: {2}", playerName, xp, currentLevel);
            }
        }

        #endregion

        #region Config save/load	
        private void Loaded()
        {
            LoadDefaultMessages();
            LoadData();
            setUpTimerGiveXP();
            updateAllPlayer();
        }

        private void LoadData()
        {
            _PlayerLevelData = Interface.GetMod().DataFileSystem.ReadObject<Collection<PlayerLevel>>("PlayerLevel");
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("PlayerLevel", _PlayerLevelData);
        }

        override
        protected void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidCommand", "Commande invalide. Utiliser /zm pour l'aide" },
                { "WrongGiveAmount", "Invalide valeur d'xp." }
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidCommand", "Invalide command. Use /zm for help" },
                { "WrongGiveAmount", "Invalid xp value." }
            }, this, "en");
        }
        #endregion

        #region Commands

        [ChatCommand("tl")]
        private void commands(Player player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                //todo display help
            }
            else if (validCommands.Contains(args[0]))
            {
                switch (args[0])
                {
                    case "give":
                        if(checkParams(args, player, 2))
                            giveXP(player, args[1]);
                        break;
                    case "giveTo":
                        if (checkParams(args, player, 3))
                            giveXP(player, args[2], args[1]);
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

        private List<PlayerLevel> GetPlayerLevelFromName(string name) {
            List<PlayerLevel> playerLevels = new List<PlayerLevel>();
            foreach (PlayerLevel playerLevel in _PlayerLevelData) {

                if (playerLevel.playerName.ToLower().Contains(name.ToLower()))
                {
                    playerLevels.Add(playerLevel);
                }

                // If exact match return it
                if (playerLevel.playerName.ToLower() == name.ToLower())
                {
                    playerLevels.RemoveRange(0, playerLevels.Count - 1);
                    break;
                }
            }
            Log("GetPlayerLevelFromName: " + name+" - matchs: "+ playerLevels.Count);
            return playerLevels;
        }

        private void giveXP(Player sender, string amountS, string receiver = "")
        {
            if (sender.HasPermission("admin"))
            {
                int amount = 0;
                Int32.TryParse(amountS, out amount);
                if (amount != 0) {
                    if (receiver != "")
                    {
                        List<PlayerLevel> matches = GetPlayerLevelFromName(receiver);
                        if(matches.Count == 1){
                            //TODO translate

                            PrintToChat(sender, amount+" xp sent to "+ matches[0].playerName);
                            giveXpToPlayer(matches[0], amount, false, "give");
                        }
                        else
                        {
                            //TODO translate
                            PrintToChat(sender, receiver+" matched " + matches.Count+" players, no xp given.");
                        }
                    }
                    else {
                        giveXpToPlayer(sender, amount, false, "give");
                    }
                }
                else
                {
                    PrintToChat(sender, GetMessage("WrongGiveAmount"));
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
        #endregion

        #region Hooks
        private void OnCubeTakeDamage(CubeDamageEvent e)
        {
            #region Checks
            if (e == null) return;
            if (e.Cancelled) return;
            if (e.Damage == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            #endregion

            try
            {
                Player attacker = e.Damage.DamageSource.Owner;
                PlayerLevel attackerLevel = getPlayerFromLevelData(attacker);

                if (attackerLevel.currentLevel < 6)
                {
                    sendError(attacker, "Pas de dégats aux cubes avant le niveau 5");

                    var centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(e.Position);
                    if (centralPrefabAtLocal != null)
                    {
                        var component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                        if (component != null) component.info.NotSalvageable = true;
                    }

                    e.Damage.Amount = 0f;
                    e.Cancel();
                    return;
                }
            }
            catch {
            }
            
        }

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            try
            {
                Player attacker = e.Damage.DamageSource.Owner;
                Player victim = e.Entity.Owner;
                if (attacker == null || victim == null || attacker == victim) return;

                PlayerLevel attackerLevel = getPlayerFromLevelData(attacker);
                PlayerLevel victimLevel = getPlayerFromLevelData(victim);
                
                if (e.Entity.name.Contains("Crest") && attackerLevel.currentLevel < 6)
                {
                    sendError(attacker, "Pas de dégats au crest avant le niveau 5");
                    e.Damage.Amount = 0f;
                    return;
                }
                bool isVictimPlayer = e.Entity.IsPlayer;

                if (attacker == null || victim == null || attacker == victim || !isVictimPlayer) return;
                if (attackerLevel.currentLevel < 6 || victimLevel.currentLevel < 6)
                {
                    //TODO translate
                    sendError(attacker, "Pas de dégats aux joueurs avant votre/leur niveau 5");
                    e.Damage.Amount = 0f;
                    e.Cancel();
                    return;
                }
            }
            catch {
            }
        }


        private void OnThroneCapture(AncientThroneCaptureEvent captureEvent)
        {
            #region Checks
            if (captureEvent == null) return;
            if (captureEvent.Cancelled) return;
            if (captureEvent.Player == null) return;
            #endregion

            try
            {
                var player = captureEvent.Player;
                captureEvent.Cancel();
                sendError(player,"Il n'est pour l'instant pas possible de capturer le trone.");
            }
            catch (Exception e)
            {
                Log($"Broke. \n\r{e}");
            }
        }

        private void OnPlayerRespawn(PlayerRespawnEvent respawnEvent)
        {
            Player player = respawnEvent.Player;
            Log("Respawn from: " + player);
            if (player == null) return;
            updatePlayerName(player);
        }

        private void OnPlayerConnected(Player player)
        {
            updatePlayerName(player);
        }
        #endregion

        #region Functions

        private void updateAllPlayer()
        {
            foreach (Player player in getPlayersOnline()) {
                updatePlayerName(player);
                updatePlayerLevel(player);
            }
        }

        private string getPlayerFormat(PlayerLevel playerLevel)
        {
            playerLevel = updatePlayerLevel(playerLevel);
            Player player = getPlayerFromPlayerLevel(playerLevel);
            if (player == null) return null;

            Log("Set player format for: " + player.Name + " playerLevel: " + playerLevel);
            string format = "";
            if (playerLevel != null)
            {
                format = string.Format("{0} ([00cc00]{1}[ffffff])", player.Name, playerLevel.currentLevel);
            }
            else
            {
                format = string.Format("{0} ([00cc00]0[ffffff])", player.Name);
            }

            if (player.HasPermission("admin"))
            {
                format = string.Format("[4da6ff]Admin[ffffff] | {0}", format);
            }
            Log("getPlayerFormat for: playerLevel: " + playerLevel + " - result: " + format);
            return format;
        }

        private string getPlayerFormat(Player player)
        {
            PlayerLevel playerLevel = getPlayerFromLevelData(player);
            return getPlayerFormat(playerLevel);
        }

        private PlayerLevel getPlayerFromLevelData(Player player){

            if (player == null) return null;

            //return existing player
            foreach (PlayerLevel playerLevel in _PlayerLevelData)
            {
                if (playerLevel.playerId == player.Id) {
                    Log("getPlayerFromLevel: " + player.Id + " - result: "+ playerLevel);
                    return playerLevel;
                }
            }

            // Create new players in level system
            PlayerLevel newPlayerLevel = new PlayerLevel(player.Id, player.Name);
            Log("getPlayerFromLevel: " + player.Id + " - result: new player created: " + newPlayerLevel);
            _PlayerLevelData.Add(newPlayerLevel);
            SaveData();
            return newPlayerLevel;
        }

        private PlayerLevel updatePlayerLevel(Player player)
        {
            PlayerLevel playerLevel = getPlayerFromLevelData(player);
            return updatePlayerLevel(playerLevel);
        }

        private PlayerLevel updatePlayerName(PlayerLevel playerLevel)
        {
            Player player = getPlayerFromPlayerLevel(playerLevel);
            return updatePlayerName(player);
        }

        private PlayerLevel updatePlayerName(Player player)
        {
            if (player.Id == 9999999999) return null;

            string format = getPlayerFormat(player);
            player.DisplayNameFormat = format;
            Log("updatePlayerName: " + player.Name + " - format: " + format);
            return getPlayerFromLevelData(player);
        }

        private Player getPlayerFromPlayerLevel(PlayerLevel playerLevel) { 
            if(playerLevel == null) return null;
            foreach (Player player in getPlayersOnline()) {
                if (player.Id == playerLevel.playerId) {
                    return player;
                }
            }
            return null;
        }

        private PlayerLevel updatePlayerLevel(PlayerLevel playerLevel)
        {
            if (playerLevel == null) return null;

            int newLevel = computeLevel(playerLevel);
            if (playerLevel.currentLevel != newLevel) {
                if (newLevel > 0 && newLevel > playerLevel.currentLevel)
                {
                    // TODO translate
                    PrintToChat(playerLevel.playerName + " a atteint le niveau [ffd700]" + newLevel + "[ffffff]");
                }
                playerLevel.currentLevel = newLevel;
                updatePlayerName(playerLevel);
                SaveData();
            }

            return playerLevel;
        }

        private int computeLevel(PlayerLevel playerLevel)
        {
            int level = 0;
            float nextLevelXp = FIRSTXPSTEP;

            if (playerLevel != null)
            {

                while (playerLevel.xp >= nextLevelXp && level < 50) {
                    level++;
                    nextLevelXp += STEPBASEINCREMENT + nextLevelXp * STEPFACTOR;
                }
                
            }
            Log("computeLevel for: playerLevel: " + playerLevel + " - result: " + level);
            return level;
        }

        private int computeLevel(Player player) {
            PlayerLevel playerLevel = getPlayerFromLevelData(player);     
            return computeLevel(playerLevel);
        }

        private void setUpTimerGiveXP()
        {
            timer.Repeat(300f, 0, () =>
            {
                giveXpToPlayers();
            });
        }

        private void giveXpToPlayers()
        {
            Log("giveXpToPlayers > start");
            foreach (Player player in getPlayersOnline())
            {
                giveXpToPlayer(player, XPMINUTE * 5, true);
            }
        }

        private void giveXpToPlayer(PlayerLevel playerLevel, int amount, bool skipAFK = false, string reason = "présence")
        {
            bool skipDueToAfk = false;
            Player player = getPlayerFromPlayerLevel(playerLevel);

            if (skipAFK)
            {
                float[] currentPosition = getPosition(player);
                float distance = DistancePointAPointB(currentPosition, playerLevel.lastPosition);
                skipDueToAfk = distance != -1 && distance < 5;
            }


            if (!skipDueToAfk) {
                Log("giveXpToPlayers > Give " + amount + " xp to : " + playerLevel);
                playerLevel.xp += amount;
                if (playerLevel.xp < 0)
                {
                    playerLevel.xp = 0;
                }
                if(player != null)
                {
                    PrintToChat(player, "[ffd700]" + "+" + amount + " xp (présence)" + "[ffffff]");
                }
                updatePlayerLevel(playerLevel);
            }
            else
            {
                Log("giveXpToPlayer > no xp due to afk : " + playerLevel);
                if (player != null)
                {
                    PrintToChat(player, "[ff0000]" + "xp de présence désactivée car AFK" + "[ffffff]");
                }
            }

            playerLevel.lastPosition = getPosition(player);

            SaveData();
        }

        private void giveXpToPlayer(Player player, int amount, bool skipAFK = false, string reason = "présence")
        {
            PlayerLevel playerLevel = getPlayerFromLevelData(player);
            giveXpToPlayer(playerLevel, amount, skipAFK, reason);
        }

        private void sendError(Player player, string message)
        {
            if (player != null && message != string.Empty)
            {
                PrintToChat(player, "[ff0000]" + message + "[ffffff]");
            }
        }

        private bool isOnline(ulong playerId)
        {
            Player playerExist = Server.GetPlayerById(playerId);
            if (playerExist != null)
            {
                return true;
            }
            return false;
        }

        #endregion

        #region Helpers
        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private Vector3 convertPosCubeInCoordinates(Vector3Int positionCube)
        {
            if (positionCube != new Vector3(0, 0, 0))
            {
                if (positionCube.x != 0)
                {
                    return new Vector3(positionCube.x * 1.2f, positionCube.y * 1.2f, positionCube.z * 1.2f);
                }
            }
            return new Vector3(0, 0, 0);
        }

        private int getTimestamp()
        {
            return (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        private List<Player> getPlayersOnline()
        {
            List<Player> listPlayersOnline = new List<Player>();
            foreach (Player player in Server.AllPlayers)
            {
                if (player.Id != 9999999999)
                {
                    listPlayersOnline.Add(player);
                }
            }
            return listPlayersOnline;
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private float DistancePointAPointB(float[] pointA, float[] pointB)
        {
            float distance = -1;
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
                position = new float[] { joueur.Entity.Position.x, joueur.Entity.Position.z, joueur.Entity.Position.y };
            }
            return position;
        }

        private void Log(string msg) => LogFileUtil.LogTextToFile($"..\\oxide\\logs\\TimeLevel_{DateTime.Now:yyyy-MM-dd}.txt", $"[{DateTime.Now:h:mm:ss tt}] {msg}\r\n");

        #endregion
    }
}