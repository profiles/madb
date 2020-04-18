// <copyright file="SyncService.cs" company="The Android Open Source Project, Ryan Conrad, Quamotion">
// Copyright (c) The Android Open Source Project, Ryan Conrad, Quamotion. All rights reserved.
// </copyright>

namespace SharpAdbClient
{
    using Exceptions;
    using SharpAdbClient.Messages.Sync;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// <para>
    ///     Provides access to the sync service running on the Android device. Allows you to
    ///     list, download and upload files on the device.
    /// </para>
    /// </summary>
    /// <example>
    /// <para>
    ///     To send files to or receive files from your Android device, you can use the following code:
    /// </para>
    /// <code>
    /// void DownloadFile()
    /// {
    ///     var device = AdbClient.Instance.GetDevices().First();
    ///
    ///     using (SyncService service = new SyncService(new AdbSocket(), device))
    ///     using (Stream stream = File.OpenWrite(@"C:\MyFile.txt"))
    ///     {
    ///         service.Pull("/data/MyFile.txt", stream, null, CancellationToken.None);
    ///     }
    /// }
    ///
    ///     void UploadFile()
    /// {
    ///     var device = AdbClient.Instance.GetDevices().First();
    ///
    ///     using (SyncService service = new SyncService(new AdbSocket(), device))
    ///     using (Stream stream = File.OpenRead(@"C:\MyFile.txt"))
    ///     {
    ///         service.Push(stream, "/data/MyFile.txt", null, CancellationToken.None);
    ///     }
    /// }
    /// </code>
    /// </example>
    public class SyncService : ISyncService, IDisposable
    {
        /// <summary>
        /// The maximum length of a path on the remote device.
        /// </summary>
        private const int MaxPathLength = 1024;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncService"/> class.
        /// </summary>
        /// <param name="device">
        /// The device on which to interact with the files.
        /// </param>
        public SyncService(DeviceData device)
            : this(Factories.AdbSocketFactory(AdbClient.Instance.EndPoint), device)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncService"/> class.
        /// </summary>
        /// <param name="socket">
        /// A <see cref="IAdbSocket"/> that enables to connection with the
        /// adb server.
        /// </param>
        /// <param name="device">
        /// The device on which to interact with the files.
        /// </param>
        public SyncService(IAdbSocket socket, DeviceData device)
        {
            this.Socket = socket;
            this.Device = device;

            this.Open();
        }

        /// <summary>
        /// Gets or sets the maximum size of data to transfer between the device and the PC
        /// in one block.
        /// </summary>
        public int MaxBufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// Gets the device on which the file operations are being executed.
        /// </summary>
        public DeviceData Device
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the <see cref="IAdbSocket"/> that enables the connection with the
        /// adb server.
        /// </summary>
        public IAdbSocket Socket { get; private set; }

        /// <include file='.\ISyncService.xml' path='/SyncService/IsOpen/*'/>
        public bool IsOpen
        {
            get
            {
                return this.Socket != null && this.Socket.Connected;
            }
        }

        /// <include file='.\ISyncService.xml' path='/SyncService/Open/*'/>
        public void Open()
        {
            // target a specific device
            AdbClient.Instance.SetDevice(this.Socket, this.Device);

            this.Socket.SendAdbRequest("sync:");
            var resp = this.Socket.ReadAdbResponse();
        }

        /// <include file='.\ISyncService.xml' path='/SyncService/Push/*'/>
        public void Push(Stream stream, string remotePath, int permissions, DateTime timestamp, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (remotePath == null)
            {
                throw new ArgumentNullException(nameof(remotePath));
            }

            if (remotePath.Length > MaxPathLength)
            {
                throw new ArgumentOutOfRangeException(nameof(remotePath), $"The remote path {remotePath} exceeds the maximum path size {MaxPathLength}");
            }

            this.Socket.SendSyncRequest(SyncCommand.SEND, remotePath, permissions);

            // create the buffer used to read.
            // we read max SYNC_DATA_MAX.
            byte[] buffer = new byte[this.MaxBufferSize];

            // We need 4 bytes of the buffer to send the 'DATA' command,
            // and an additional X bytes to inform how much data we are
            // sending.
            byte[] dataBytes = SyncCommandConverter.GetBytes(SyncCommand.DATA);
            byte[] lengthBytes = BitConverter.GetBytes(this.MaxBufferSize);
            int headerSize = dataBytes.Length + lengthBytes.Length;
            int reservedHeaderSize = headerSize;
            int maxDataSize = this.MaxBufferSize - reservedHeaderSize;
            lengthBytes = BitConverter.GetBytes(maxDataSize);

            // Try to get the total amount of bytes to transfer. This is not always possible, for example,
            // for forward-only streams.
            long totalBytesToProcess = stream.CanSeek ? stream.Length : 0;
            long totalBytesRead = 0;

            // look while there is something to read
            while (true)
            {
                // check if we're canceled
                cancellationToken.ThrowIfCancellationRequested();

                // read up to SYNC_DATA_MAX
                int read = stream.Read(buffer, headerSize, maxDataSize);
                totalBytesRead += read;

                if (read == 0)
                {
                    // we reached the end of the file
                    break;
                }
                else if (read != maxDataSize)
                {
                    // At the end of the line, so we need to recalculate the length of the header
                    lengthBytes = BitConverter.GetBytes(read);
                    headerSize = dataBytes.Length + lengthBytes.Length;
                }

                int startPosition = reservedHeaderSize - headerSize;

                Buffer.BlockCopy(dataBytes, 0, buffer, startPosition, dataBytes.Length);
                Buffer.BlockCopy(lengthBytes, 0, buffer, startPosition + dataBytes.Length, lengthBytes.Length);

                // now send the data to the device
                this.Socket.Send(buffer, startPosition, read + dataBytes.Length + lengthBytes.Length);

                // Let the caller know about our progress, if requested
                if (progress != null && totalBytesToProcess != 0)
                {
                    progress.Report((int)(100.0 * totalBytesRead / totalBytesToProcess));
                }
            }

            // create the DONE message
            int time = (int)timestamp.ToUnixEpoch();
            this.Socket.SendSyncRequest(SyncCommand.DONE, time);

            // read the result, in a byte array containing 2 ints
            // (id, size)
            var status = new Status();
            status.ReadFrom(this.Socket);

            if (status.Command == SyncCommand.FAIL)
            {
                var message = this.Socket.ReadSyncString();

                throw new AdbException(message);
            }
            else if (status.Command != SyncCommand.OKAY)
            {
                throw new AdbException($"The server sent an invali repsonse {status.Command}");
            }
        }

        /// <include file='.\ISyncService.xml' path='/SyncService/PullFile2/*'/>
        public void Pull(string remoteFilepath, Stream stream, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (remoteFilepath == null)
            {
                throw new ArgumentNullException(nameof(remoteFilepath));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // Get file information, including the file size, used to calculate the total amount of bytes to receive.
            var stat = this.Stat(remoteFilepath);
            long totalBytesToProcess = stat.Size;
            long totalBytesRead = 0;

            byte[] buffer = new byte[this.MaxBufferSize];

            this.Socket.SendSyncRequest(SyncCommand.RECV, remoteFilepath);

            while (true)
            {
                var data = new Data();
                data.ReadFrom(this.Socket);
                cancellationToken.ThrowIfCancellationRequested();

                if (data.Command == SyncCommand.DONE)
                {
                    break;
                }
                else if (data.Command == SyncCommand.FAIL)
                {
                    var message = this.Socket.ReadSyncString();
                    throw new AdbException($"Failed to pull '{remoteFilepath}'. {message}");
                }
                else if (data.Command != SyncCommand.DATA)
                {
                    throw new AdbException($"The server sent an invalid response {data.Command}");
                }

                if (data.Size > this.MaxBufferSize)
                {
                    throw new AdbException($"The adb server is sending {data.Size} bytes of data, which exceeds the maximum chunk size {this.MaxBufferSize}");
                }

                // now read the length we received
                this.Socket.Read(buffer, data.Size);
                stream.Write(buffer, 0, data.Size);
                totalBytesRead += data.Size;

                // Let the caller know about our progress, if requested
                if (progress != null && totalBytesToProcess != 0)
                {
                    progress.Report((int)(100.0 * totalBytesRead / totalBytesToProcess));
                }
            }
        }

        /// <include file='.\ISyncService.xml' path='/SyncService/Stat/*'/>
        public FileStatistics Stat(string remotePath)
        {
            // create the stat request message.
            this.Socket.SendSyncRequest(SyncCommand.STAT, remotePath);

            // read the result, in a byte array containing 3 ints
            // (mode, size, time)
            var statv2 = new Stat2();
            statv2.ReadFrom(this.Socket);
            if (statv2.Command != SyncCommand.STAT)
            {
                throw new AdbException($"The server returned an invalid sync response.");
            }
            
            FileStatistics value = FileStatistics.ReadFromStat2(remotePath, statv2);
            return value;
        }

        /// <include file='.\ISyncService.xml' path='/SyncService/GetDirectoryListing/*'/>
        public IEnumerable<FileStatistics> GetDirectoryListing(string remotePath)
        {
            Collection<FileStatistics> value = new Collection<FileStatistics>();

            // create the stat request message.
            this.Socket.SendSyncRequest(SyncCommand.LIST, remotePath);

            while (true)
            {
                var dent = new Dent();
                dent.ReadFrom(this.Socket);

                if (dent.Command == SyncCommand.DONE)
                {
                    break;
                }
                else if (dent.Command != SyncCommand.DENT)
                {
                    throw new AdbException($"The server returned an invalid sync response.");
                }

                FileStatistics entry = FileStatistics.ReadFromDent(dent);
                value.Add(entry);
            }

            return value;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.Socket != null)
            {
                this.Socket.Dispose();
                this.Socket = null;
            }
        }
    }
}
