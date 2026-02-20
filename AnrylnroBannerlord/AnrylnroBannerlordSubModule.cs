using AnrylnroBannerlord.Api;
using AnrylnroBannerlord.Network;
using AnrylnroBannerlord.Utils;
using BannerlordPlayerApi.Network;
using NetworkMessages.FromServer;
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


            InitialListedGameServerState.OnActivated += DedicatedCustomGameServerStateActivated;
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            mission.AddMissionBehavior(new PlayerTrackerBehavior());
        }

        private void DedicatedCustomGameServerStateActivated()
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
            _chatBox.OnMessageReceivedAtDedicatedServer += OnMessageReceivedAtDedicatedServer;
        }

        private void OnMessageReceivedAtDedicatedServer(NetworkCommunicator fromPeer, string message)
        {
            if ("/register".Equals(message))
            {
                _ = ApiServer.Instance.RegisterLifecycleAsync(ApiServer.RegisterUrl);
                GameNetwork.BeginModuleEventAsServer(fromPeer);
                GameNetwork.WriteMessage(new ServerMessage("Send register message to master server.", false));
                GameNetwork.EndModuleEventAsServer();
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            ApiServer.Stop();
            _chatBox.OnMessageReceivedAtDedicatedServer -= OnMessageReceivedAtDedicatedServer;
            InitialListedGameServerState.OnActivated -= DedicatedCustomGameServerStateActivated;
            
            base.OnSubModuleUnloaded();
        }

    }
}
