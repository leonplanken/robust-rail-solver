using Microsoft.Extensions.Logging;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Tasks
{
    class ParkingTask : TrackTask
    {
        static readonly ILogger logger = Logging.GetLogger();
        public override TrackParts.Track Track
        {
            get => base.Track;
            set
            {
                if (value != null && !value.CanPark)
                {
                    logger.LogError(
                        "Creating a ParkingTask on forbidden track {TrackName}",
                        value.PrettyName
                    );
                }
                base.Track = value;
            }
        }

        public bool IsInserted { get; }

        public ParkingTask(Trains.ShuntTrain train, TrackParts.Track track, bool isinserted = false)
            : base(train, track, TrackTaskType.Parking)
        {
            this.IsInserted = isinserted;
        }
    }
}
