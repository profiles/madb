namespace SharpAdbClient.Messages.Sync
{
    public class Dent : Stat2
    {
        public string Path
        {
            get;
            set;
        }

        public override void ReadFrom(IAdbSocket socket)
        {
            base.ReadFrom(socket);
            this.Path = socket.ReadSyncString();
        }
    }
}
