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
using CodeHatch.Core.Registration;
using CodeHatch.Engine.Core.Interaction.Behaviours.Networking;
using CodeHatch.Engine.Core.Interaction.Players;
using CodeHatch.Engine.Modules.SocialSystem.Objects;
using static CodeHatch.Blocks.Networking.Events.CubeEvent;

namespace Oxide.Plugins{
    [Info("AdminTools", "Pierre Anken", "1.0.0")]
    public class AdminTools : ReignOfKingsPlugin{
		
		#region Configuration Data
        private static List<Param> _Params = new List<Param>();
        private List<string> validCommands = new List<string>(new string[] { "setLang", "delBlocks"});
        #endregion

        #region classes
        public class Param
        {
            public string key;
            public string value;

            public Param(String key, String value) {
                this.key = key;
                this.value = value;
            }

            public static Param GetParam(string key) {
                if(key != "")
                {
                    foreach (Param param in _Params) {
                        if (param.key == key)
                            return param;
                    }
                }
                return null;
            }

            public static void SetParam(Param param)
            {
                if (param != null)
                {
                    Param old_param = GetParam(param.key);
                    if (old_param != null)
                    {
                        old_param.value = param.value;
                    }
                    else {
                        _Params.Add(param);
                    }
                    SaveData();
                }
            }

        }
        #endregion

        #region Config save/load	
        private void Loaded()
        {
            LoadData();
            InitParams();
            LoadDefaultMessages();
        }

        private static void LoadData()
        {
            _Params = Interface.GetMod().DataFileSystem.ReadObject<List<Param>>("AdminToolsParams");
        }

        private static void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("AdminToolsParams", _Params);
        }

        private void InitParams()
        {
            Param language = Param.GetParam("language");
            if(language != null)
                lang.SetServerLanguage(language.value);
        }

        override
        protected void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidCommand", "Commande invalide. Utiliser /at pour l'aide" },
                { "LangSet", "Langue du serveur mise à jour" },
                { "ToolsHelp", "Admin tools - commandes:#" +
                               "/at setLang xx pour changer la langue du serveur." }
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidCommand", "Invalide command. Use /at for help" },
                { "LangSet", "Server language updated" },
                { "ToolsHelp", "Admin tools - commands:#" +
                               "/at setLang xx to change server language." }
            }, this, "en");
        }

        #endregion

        #region Commands
        [ChatCommand("at")]
		private void defier(Player player, string cmd, string[] args){
            if (player.HasPermission("admin"))
            {
                if (args.Length == 0)
                {
                    displayToolsHelp(player);
                }
                else if (validCommands.Contains(args[0]))
                {
                    switch (args[0])
                    {
                        case "setLang":
                            if (checkParams(args, player, 2))
                                setLang(player, args[1]);
                            break;

                        case "delBlocks":
                            if (checkParams(args, player, 2))
                                delBlocks(player, args[1]);
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
        }

        private void displayToolsHelp(Player player)
        {
            foreach (string command in GetMessage("ToolsHelp").Split('#'))
                PrintToChat(player, command);
        }
        #endregion

        #region Hooks
        private void OnCubeTakeDamage(CubeDamageEvent e) { 


            Player player = e.Damage.DamageSource.Owner;
            if (player.HasPermission("admin"))
            {
                if (e.Damage.Amount > 0 && e.Damage.DamageSource.IsPlayer)
                {
                    Vector3 positionCube = positionToVector3(e.Position);
                    sendError(player, "Event v3: " + positionCube.x + "/" + positionCube.z + "/" + positionCube.y);
                }
            }
        }
        #endregion

        #region Functions

        private void delBlocks(Player player, string rangeS)
        {
            float range = 0;
            float.TryParse(rangeS, out range);

            if (range <= 0)
            {
                sendError(player, GetMessage("InvalidCommand"));
            }
            else{

                if (range > 50)
                    range = 50;

                Vector3 playerPosition = getPositionV3(player);
                if (playerPosition[0] != 0) {
                    
                    float delta = range / 2;
                    List<CubeInfo> cubs = new List<CubeInfo>();

                    for (float x = playerPosition.x - delta; x < playerPosition.x + delta; x++)
                    {
                        for (float y = playerPosition.y - delta; y < playerPosition.y + delta; y++)
                        {
                            for (float z = playerPosition.z - delta; z < playerPosition.z + delta; z++)
                            {
                                Vector3Int testPosition = new Vector3Int((int)x, (int)y, (int)z);
                                CubeInfo cubInfo = BlockManager.DefaultCubeGrid.GetCubeInfoAtLocal(testPosition);
                                sendError(player, "cubInfo "+ cubInfo);
                                return;
                                if((int)cubInfo.Prefab == 0 && cubInfo.MaterialID != CubeInfo.Air.MaterialID)
                                    cubs.Add(cubInfo);
                            }
                        }
                    }

                    if (cubs.Count == 0)
                    {
                        //todo translate
                        sendError(player, "No cube in range");
                    }
                    else {
                        sendError(player, cubs.Count+ " cubs found in range");
                    }
                }
            }

        }

        private void setLang(Player player, string newLang) {
            if (newLang.Length != 2)
            {
                sendError(player, GetMessage("InvalidCommand"));
            }
            else {
                lang.SetServerLanguage(newLang);
                Param language = new Param("language", newLang);
                Param.SetParam(language);
                PrintToChat(player, GetMessage("LangSet"));
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

        private void sendError(Player player, string message)
        {
            if (player != null && message != string.Empty)
            {
                PrintToChat(player, "[ff0000]" + message + "[ffffff]");
            }
        }
        #endregion

        #region Helpers
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private Vector3 getPositionV3(Player player)
        {
            Vector3 position = new Vector3(0, 0, 0);
            if (player != null && player.Entity != null)
            {
                position = new Vector3(player.Entity.Position.x, player.Entity.Position.z, player.Entity.Position.y);
            }
            return position;
        }

        private Vector3 positionToVector3(Vector3Int position)
        {
            if (position != new Vector3(0, 0, 0))
            {
                if (position.x != 0)
                {
                    return new Vector3(position.x * 1.2f, position.y * 1.2f, position.z * 1.2f);
                }
            }
            return new Vector3(0, 0, 0);
        }
        #endregion
    }
}