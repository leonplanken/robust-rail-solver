namespace ServiceSiteScheduling.TrackParts
{
    class GateWay : Connection
    {
        public GateWay(ulong id, string name)
            : base(id, name) { }

        public Infrastructure EndPoint { get; private set; }

        public GateWay(ulong id, string name, Infrastructure infrastructure)
            : this(id, name)
        {
            this.Connect(infrastructure);
            this.EndPoint = infrastructure;
        }

        public void Connect(Infrastructure infrastructure)
        {
            this.Connections[infrastructure] = Array.Empty<Infrastructure>();
            this.EndPoint = infrastructure;
        }

        public override IList<TrackSwitchContainer> GetTracksConnectedTo(
            Infrastructure infrastructure,
            int switches,
            List<Infrastructure> path,
            bool ignoreInactive = true
        )
        {
            return [];
        }
    }
}
