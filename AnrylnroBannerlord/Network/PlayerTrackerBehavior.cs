using AnrylnroBannerlord.Network;
using AnrylnroBannerlord.Utils;
using TaleWorlds.MountAndBlade;

namespace BannerlordPlayerApi.Network
{
    public class PlayerTrackerBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType
            => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            if (!PlayerDataStore.RefreshRequested)
                return;

            PlayerDataStore.RefreshRequested = false;

            var list = new List<PlayerSnapshot>();

            foreach (var peer in GameNetwork.NetworkPeers)
            {
                var mp = peer.GetComponent<MissionPeer>();
                if (mp == null)
                    continue;

                string team =
                    mp.Team == null
                        ? "None"
                        : mp.Team.Side.ToString();

                list.Add(new PlayerSnapshot
                {
                    Name = mp.Name,
                    Kill = mp.KillCount,
                    Death = mp.DeathCount,
                    Score = mp.Score,
                    Team = team
                });
            }
            ModLogger.Log($"已获取 {list.Count} 位玩家数据。");
            PlayerDataStore.Update(list);
        }
    }
}