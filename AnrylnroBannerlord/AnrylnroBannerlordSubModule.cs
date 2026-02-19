using AnrylnroBannerlord.Api;
using AnrylnroBannerlord.Network;
using AnrylnroBannerlord.Utils;
using BannerlordPlayerApi.Network;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ListedServer;

namespace AnrylnroBannerlord
{
    public class AnrylnroBannerlordSubModule : MBSubModuleBase
    {
        private static ConfigManager _configManager = new();
        private ChatBox _chatBox;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Console.OutputEncoding = System.Text.Encoding.UTF8;


            string customGameServerConfigFile = Module.CurrentModule.StartupInfo.CustomGameServerConfigFile;
            _configManager.LoadConfig(ModuleHelper.GetModuleFullPath("Native") + customGameServerConfigFile);


            InitialListedGameServerState.OnActivated += DedicatedCustomGameServerStateActivatedee;
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            mission.AddMissionBehavior(new PlayerTrackerBehavior());
        }

        private void DedicatedCustomGameServerStateActivatedee()
        {
            // 输出Banner横幅
            String banner = @"
     \                         |                    
    _ \    __ \    __|  |   |  |  __ \    __|  _ \  
   ___ \   |   |  |     |   |  |  |   |  |    (   | 
 _/    _\ _|  _| _|    \__, | _| _|  _| _|   \___/  
                       ____/                        
";
            ModLogger.Log(banner);

            ApiServer.Start();

            _chatBox = Game.Current.GetGameHandler<ChatBox>();
            _chatBox.OnMessageReceivedAtDedicatedServer = 
                (Action<NetworkCommunicator, string>)Delegate.Combine(_chatBox.OnMessageReceivedAtDedicatedServer, new Action<NetworkCommunicator, string>(this.OnMessageReceivedAtDedicatedServer));
        }

        private void OnMessageReceivedAtDedicatedServer(NetworkCommunicator fromPeer, string message)
        {
            ModLogger.Log($"{fromPeer.UserName}: {message}");
        }

        protected override void OnSubModuleUnloaded()
        {
            ApiServer.Stop();
            base.OnSubModuleUnloaded();
        }

    }
}
