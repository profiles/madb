namespace SharpAdbClient.Messages.Sync
{
    using System;

    public class Stat2 : Message
    {
        public UnixFileMode FileMode
        {
            get;
            set;
        }

        public int Size
        {
            get;
            set;
        }

        public DateTime Time
        {
            get;
            set;
        }

        public override void ReadFrom(IAdbSocket socket)
        {
            this.Command = socket.ReadSyncResponse();

            byte[] statResult = new byte[12];
            socket.Read(statResult);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(statResult, 0, 4);
                Array.Reverse(statResult, 4, 4);
                Array.Reverse(statResult, 8, 4);
            }

            this.FileMode = (UnixFileMode)BitConverter.ToInt32(statResult, 0);
            this.Size = BitConverter.ToInt32(statResult, 4);
            this.Time = DateTimeHelper.Epoch.AddSeconds(BitConverter.ToInt32(statResult, 8)).ToLocalTime();
        }
    }
}
