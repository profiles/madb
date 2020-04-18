// <copyright file="FileStatistics.cs" company="The Android Open Source Project, Ryan Conrad, Quamotion">
// Copyright (c) The Android Open Source Project, Ryan Conrad, Quamotion. All rights reserved.
// </copyright>

namespace SharpAdbClient
{
    using System;
    using SharpAdbClient.Messages.Sync;

    /// <summary>
    /// Contains information about a file on the remote device.
    /// </summary>
    public class FileStatistics
    {
        /// <summary>
        /// Gets or sets the path of the file.
        /// </summary>
        public string Path
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <see cref="UnixFileMode"/> attributes of the file.
        /// </summary>
        public UnixFileMode FileMode
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the total file size, in bytes.
        /// </summary>
        public int Size
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the time of last modification.
        /// </summary>
        public DateTime Time
        {
            get;
            set;
        }

        /// <summary>
        /// Gets a <see cref="string"/> that represents the current <see cref="FileStatistics"/> object.
        /// </summary>
        /// <returns>
        /// The <see cref="Path"/> of the current <see cref="FileStatistics"/> object.
        /// </returns>
        public override string ToString()
        {
            return this.Path;
        }

        public static FileStatistics ReadFromStat2(string path, Stat2 stat2)
        {
            return new FileStatistics()
            {
                FileMode = stat2.FileMode,
                Size = stat2.Size,
                Time = stat2.Time,
                Path = path,
            };
        }

        public static FileStatistics ReadFromDent(Dent dent)
        {
            return new FileStatistics()
            {
                FileMode = dent.FileMode,
                Size = dent.Size,
                Time = dent.Time,
                Path = dent.Path,
            };
        }
    }
}
