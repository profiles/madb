namespace SharpAdbClient.Messages.Sync
{
    using System;

    public class Status : Message
    {
        public override void ReadFrom(IAdbSocket socket)
        {
            this.Command = socket.ReadSyncResponse();
        }
    }
}
