using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace AdminExtension
{
    [ApiVersion(2, 1)]
    public class AdminExtension : TerrariaPlugin
    {
        public override string Name
        {
            get
            {
                return "AdminExtensionX";
            }
        }
        
        public override string Author
        {
            get
            {
                return "Professor X + Ghasty";
            }
        }
        
        public override string Description
        {
            get
            {
                return "Adds some useful commands.";
            }
        }
        
        public override Version Version
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }
        
        public AdminExtension(Main game) : base(game) { }

        public bool[] PvPForced = new bool[255];
        public bool[] ghostMode = new bool[255];
        public List<TSPlayer> autoKilledPlayers = new List<TSPlayer>();
        public List<TSPlayer> autoHealedPlayers = new List<TSPlayer>();
        private Timer updater;

        public override void Initialize()
        {
            updater = new Timer(1000);
            updater.Elapsed += OnUpdate;
            updater.Start();
            ServerApi.Hooks.GameInitialize.Register(this, new HookHandler<EventArgs>(OnInitialize));
        }

        private void OnUpdate(object sender, ElapsedEventArgs e)
        {
            foreach (TSPlayer tsplayer in TShock.Players)
            {
                if (tsplayer != null)
                {
                    if (autoKilledPlayers.Contains(tsplayer))
                    {
                        tsplayer.DamagePlayer(15000);
                    }
                    if (autoHealedPlayers.Contains(tsplayer))
                    {
                        tsplayer.Heal(600);
                    }
                    if (PvPForced[tsplayer.Index] && !tsplayer.TPlayer.hostile)
                    {
                        tsplayer.TPlayer.hostile = true;
                        NetMessage.SendData(30, -1, -1, null, tsplayer.Index, 0f, 0f, 0f, 0, 0, 0);
                    }
                    if (ghostMode[tsplayer.Index])
                    {
                        tsplayer.TPlayer.position.X = 0f;
                        tsplayer.TPlayer.position.Y = 0f;
                        tsplayer.TPlayer.team = 0;
                        NetMessage.SendData(13, -1, -1, null, tsplayer.Index, 0f, 0f, 0f, 0, 0, 0);
                        NetMessage.SendData(45, -1, -1, null, tsplayer.Index, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, new HookHandler<EventArgs>(OnInitialize));
                updater.Stop();
                updater.Dispose();
            }
            base.Dispose(disposing);
        }

        public void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("ae.killall", KillAll, "killall"));
            Commands.ChatCommands.Add(new Command("ae.autokill", AutoKill, "autokill", "akill"));
            Commands.ChatCommands.Add(new Command("ae.healall", HealAll, "healall"));
            Commands.ChatCommands.Add(new Command("ae.autoheal", AutoHeal, "autoheal"));
            Commands.ChatCommands.Add(new Command("ae.forcepvp", ForcePvP, "forcepvp"));
            Commands.ChatCommands.Add(new Command("ae.ghost", Ghost, "ghost"));
            Commands.ChatCommands.Add(new Command("ae.butcher.npc", ButcherNPC, "butchernpc", "bnpc"));
            Commands.ChatCommands.Add(new Command("ae.butcher.friendly", ButcherFriendly, "butcherfriendly", "bfriendly", "butcherf"));
            Commands.ChatCommands.Add(new Command("ae.findpermission", FindPermission, "findpermission", "findperm"));
        }

        private void FindPermission(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Usage: /findpermission <commandname>");
            }
            var commandname = args.Parameters[0].ToLowerInvariant();
            if (commandname.StartsWith(TShock.Config.CommandSpecifier)) commandname = commandname.Substring(1);
            for (int i = 0; i < Commands.ChatCommands.Count; i++)
            {
                if (!Commands.ChatCommands[i].Names.Contains(commandname)) continue;
                if (Commands.ChatCommands[i].Permissions.Count == 1)
                {
                    args.Player.SendInfoMessage("The permission needed for {2}{1} is {0}", Commands.ChatCommands[i].Permissions[0], Commands.ChatCommands[i].Name, TShock.Config.CommandSpecifier);
                }
                else
                {
                    args.Player.SendInfoMessage("The permissions needed for {0}{1} are:", TShock.Config.CommandSpecifier, Commands.ChatCommands[i].Name);
                    for (int j = 0; j < Commands.ChatCommands[i].Permissions.Count; j++)
                    {
                        args.Player.SendInfoMessage(Commands.ChatCommands[i].Permissions[j]);
                    }
                }
                break;
            }
        }

        public void KillAll(CommandArgs args)
        {
            foreach (TSPlayer tsplayer in TShock.Players)
            {
                if (tsplayer != null && !tsplayer.Group.HasPermission("ae.killall.bypass") && tsplayer != args.Player && TShock.Utils.ActivePlayers() > 1)
                {
                    tsplayer.DamagePlayer(15000);
                }
            }
            args.Player.SendSuccessMessage("You killed everyone!");
            if (!args.Silent)
            {
                TSPlayer.All.SendErrorMessage("{0} killed you (along with everyone else).", args.Player.Name);
            }
        }
        
        public void AutoKill(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}autokill <player name/list>", new object[]
                {
                    TShock.Config.CommandSpecifier
                });
            }
            else if (args.Parameters[0].ToLower() == "list")
            {
                IEnumerable<string> enumerable = from p in autoKilledPlayers
                                                 orderby p.Name
                                                 select p.Name;
                if (enumerable.Count() == 0)
                {
                    args.Player.SendErrorMessage("There are no players being auto-killed.");
                }
                else
                {
                    args.Player.SendSuccessMessage("Auto-killed players: {0}", string.Join(", ", enumerable));
                }
            }
            else
            {
                List<TSPlayer> list = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (list.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player.");
                }
                else if (list.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, from p in list
                                                                     select p.Name);
                }
                else if (list[0].Group.HasPermission("ae.autokill.bypass"))
                {
                    args.Player.SendErrorMessage("You cannot auto-kill this player.");
                }
                else if (!autoKilledPlayers.Contains(list[0]))
                {
                    autoKilledPlayers.Add(list[0]);
                    args.Player.SendSuccessMessage("{0} is now being auto-killed.", new object[]
                    {
                        list[0].Name
                    });
                    list[0].SendInfoMessage("You are now being auto-killed.");
                }
                else
                {
                    autoKilledPlayers.Remove(list[0]);
                    args.Player.SendSuccessMessage("{0} is no longer being auto-killed.", new object[]
                    {
                        list[0].Name
                    });
                    list[0].SendInfoMessage("You are no longer being auto-killed.");
                }
            }
        }
        
        public void HealAll(CommandArgs args)
        {
            foreach (TSPlayer tsplayer in TShock.Players)
            {
                if (tsplayer != null)
                {
                    tsplayer.Heal(600);
                }
            }
            args.Player.SendSuccessMessage("You healed everyone!");
            if (!args.Silent)
            {
                TSPlayer.All.SendInfoMessage("{0} healed everyone.", args.Player.Name);
            }
        }
        
        public void AutoHeal(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}autoheal <player name/list>", TShock.Config.CommandSpecifier);
            }
            else if (args.Parameters[0].ToLower() == "list")
            {
                IEnumerable<string> enumerable = from p in autoHealedPlayers
                                                 orderby p.Name
                                                 select p.Name;
                if (enumerable.Count() == 0)
                {
                    args.Player.SendErrorMessage("There are no players being auto-healed.");
                }
                else
                {
                    args.Player.SendSuccessMessage("Auto-healed players: {0}", string.Join(", ", enumerable));
                }
            }
            else
            {
                List<TSPlayer> list = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (list.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player.");
                }
                else if (list.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, from p in list
                                                                     select p.Name);
                }
                else if (!autoHealedPlayers.Contains(list[0]))
                {
                    autoHealedPlayers.Add(list[0]);
                    args.Player.SendSuccessMessage("{0} is now being auto-healed.", list[0].Name);
                    list[0].SendInfoMessage("You are now being auto-healed.");
                }
                else
                {
                    autoHealedPlayers.Remove(list[0]);
                    args.Player.SendSuccessMessage("{0} is no longer being auto-healed.", list[0].Name);
                    list[0].SendInfoMessage("You are no longer being auto-healed.");
                }
            }
        }
        
        public void ForcePvP(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}forcepvp <player name>", TShock.Config.CommandSpecifier);
            }
            else
            {
                List<TSPlayer> list = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (list.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player.");
                }
                else if (list.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, from p in list
                                                                     select p.Name);
                }
                else if (list[0].Group.HasPermission("ae.forcepvp.bypass"))
                {
                    args.Player.SendErrorMessage("You cannot force this player's PvP.");
                }
                else
                {
                    PvPForced[list[0].Index] = !PvPForced[list[0].Index];
                    args.Player.SendSuccessMessage("{0}'s PvP is {1} forced.", 
                        list[0].Name,
                        PvPForced[list[0].Index] ? "now" : "no longer"
                    );
                    list[0].SendInfoMessage("Your PvP is {0} forced.", 
                        PvPForced[list[0].Index] ? "now" : "no longer"
                    );
                }
            }
        }
        
        public void Ghost(CommandArgs args)
        {
            if (args.Parameters.Count == 1)
            {
                List<TSPlayer> list = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (list.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player.");
                }
                else if (list.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, from p in list
                                                                     select p.Name);
                }
                else
                {
                    ghostMode[list[0].Index] = !ghostMode[list[0].Index];
                    args.Player.SendSuccessMessage("{0}abled ghost mode for {1}.", 
                        ghostMode[list[0].Index] ? "En" : "Dis",
                        list[0].Name
                    );
                    list[0].SendInfoMessage("{0} {1}abled ghost mode for you.", 
                        args.Player.Name,
                        ghostMode[list[0].Index] ? "en" : "dis"
                    );
                    TSPlayer.All.SendInfoMessage("{0} has {1}.", 
                        list[0].Name,
                        ghostMode[list[0].Index] ? "left" : "joined"
                    );
                }
            }
            else if (args.Parameters.Count == 0)
            {
                ghostMode[args.Player.Index] = !ghostMode[args.Player.Index];
                args.Player.SendSuccessMessage("{0}abled ghost mode.", 
                    ghostMode[args.Player.Index] ? "En" : "Dis"
                );
                TSPlayer.All.SendInfoMessage("{0} has {1}.", 
                    args.Player.Name,
                    ghostMode[args.Player.Index] ? "left" : "joined"
                );
            }
        }
        
        public void ButcherNPC(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}butchernpc <npc ID/name>", 
                    TShock.Config.CommandSpecifier
                );
            }
            else
            {
                int num = 0;
                List<NPC> npcbyIdOrName = TShock.Utils.GetNPCByIdOrName(args.Parameters[0]);
                if (npcbyIdOrName.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid NPC.");
                }
                else
                {
                    for (int i = 0; i < Main.npc.Length; i++)
                    {
                        if (Main.npc[i].friendly && Main.npc[i].type == npcbyIdOrName[0].type)
                        {
                            num++;
                            TSPlayer.Server.StrikeNPC(i, 9999, 0f, 0);
                        }
                    }
                    TSPlayer.All.SendSuccessMessage("{0} killed {1} {2}{3}.", 
                        args.Player.Name,
                        num.ToString(),
                        npcbyIdOrName[0].GivenOrTypeName,
                        (num > 1) ? "s" : ""
                    );
                }
            }
        }
        
        public void ButcherFriendly(CommandArgs args)
        {
            int num = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                if (Main.npc[i].active && Main.npc[i].townNPC)
                {
                    num++;
                    TSPlayer.Server.StrikeNPC(i, 9999, 0f, 0);
                }
            }
            TSPlayer.All.SendInfoMessage("{0} butchered {1} friendly NPC{2}.", 
                args.Player.Name,
                num.ToString(),
                (num > 1) ? "s" : ""
            );
        }
    }
}
