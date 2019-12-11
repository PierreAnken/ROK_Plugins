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
    [Info("TimeLevel", "Pierre Anken", "1.0.1")]
    public class TimeLevel : ReignOfKingsPlugin
    {

        /*=============================================
         * Last update: 11.12.2019
         * Contact: pierre.anken@gmail.com
         * https://github.com/PierreAnken/ROK_Plugins
         ==============================================*/

        #region Configuration Data
        private static Collection<PlayerLevel> _PlayerLevel = new Collection<PlayerLevel>();
        private List<string> validCommands = new List<string>(new string[] { }); 
        private List<int> xpLevel = new List<int>(new int[] {5,15,60,126,180,264,345,556,725,1166,1682,2100,2450,2800,3400,3900,4600,5346,6000,9000,9999999});

        const int XPMINUTE = 1;
        #endregion

        #region classes
        private class PlayerLevel
        {
            public ulong playerId;
            public int xp;
            public int currentLevel;

            public PlayerLevel(ulong playerId, int xp = 0) {
                this.playerId = playerId;
                this.xp = xp;
            }
        }

        #endregion

        #region Config save/load	
        private void Loaded()
        {
            LoadDefaultMessages();
            LoadData();
            setUpTimerGiveXP();
        }

        private void LoadData()
        {
            _PlayerLevel = Interface.GetMod().DataFileSystem.ReadObject<Collection<PlayerLevel>>("PlayerLevel");
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("PlayerLevel", _PlayerLevel);
        }

        override
        protected void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidCommand", "Commande invalide. Utiliser /zm pour l'aide" }
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidCommand", "Invalide command. Use /zm for help" }
            }, this, "en");
        }
        #endregion

        #region Commands

        [ChatCommand("pl")]
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


        #endregion

        #region Functions


        private PlayerLevel getPlayerFromLevel(Player player)
        {
            foreach (PlayerLevel playerLevel in _PlayerLevel)
            {
                if (playerLevel.playerId == player.Id)
                    return playerLevel;
            }
            return null;
        }

        private int computeLevel(Player player) {
            int level = 0;
            PlayerLevel playerLevel = getPlayerFromLevel(player);
            if (playerLevel != null) {
                int xpPlayer = playerLevel.xp;
                while(xpPlayer - xpLevel[level] > 0)
                {
                    xpPlayer -= xpLevel[level];
                    level++;
                }
            }
            return level;
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
            Log("giveXpToPlayers : start");

            foreach (Player player in getPlayersOnline())
            {
                PlayerLevel playerLevel = getPlayerFromLevel(player);
                if (playerLevel == null)
                {
                    Log("giveXpToPlayers: new player added in level " + player.Name);
                    _PlayerLevel.Add(new PlayerLevel(player.Id));
                }
                else
                {
                    playerLevel.xp += XPMINUTE*5;
                    PrintToChat(player, "[ffd700]" + "+"+(XPMINUTE*5)+" xp (présence)" + "[ffffff]");
                }
            }
            Log("giveXpToPlayers: Save Data ");
            SaveData();
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
                position = new float[] { joueur.Entity.Position.x, joueur.Entity.Position.z, joueur.Entity.Position.y };
            }
            return position;
        }

        private void Log(string msg) => LogFileUtil.LogTextToFile($"..\\oxide\\logs\\TimeLevel_{DateTime.Now:yyyy-MM-dd}.txt", $"[{DateTime.Now:h:mm:ss tt}] {msg}\r\n");

        #endregion
    }
}