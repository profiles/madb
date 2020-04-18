namespace SharpAdbClient.Messages.Sync
{
    using System;

    public class Data : Message
    {
        public int Size { get; set; }

        public override void ReadFrom(IAdbSocket socket)
        {
            this.Command = socket.ReadSyncResponse();

            var reply = new byte[4];
            socket.Read(reply);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(reply);
            }

            // The first 4 bytes contain the length of the data packet
            this.Size = BitConverter.ToInt32(reply, 0);
        }
    }
}
