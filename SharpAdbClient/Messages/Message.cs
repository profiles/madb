using System;

namespace SharpAdbClient.Messages
{
    public abstract class Message
    {
        public SyncCommand Command { get; set; }

        public abstract void ReadFrom(IAdbSocket socket);
    }
}
