using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TraceEventImporter.Models;

namespace TraceEventImporter.Readers
{
    /// <summary>
    /// Reads SQL Server Profiler trace (.trc) files in binary format.
    /// Implements a simplified binary parser based on the Yukon+ .trc file format
    /// documented in READ80TRACE/trccommon.h and 80TraceIO.cpp.
    /// No SMO or SQL Server dependency required.
    /// </summary>
    public class TrcFileReader : ITraceEventReader
    {
        // TRACE_SPECIAL_COLUMNS
        private const ushort TRACE_BEGIN_RECORD = 0xFFF6;
        private const ushort TRACE_FLUSH = 0xFFF2;
        private const ushort SQL_TRACE_VERSION = 0xFFFE;
        private const ushort TRACED_OPTIONS = 0xFFFD;
        private const ushort TRACED_EVENTS = 0xFFFC;
        private const ushort TRACED_FILTERS = 0xFFFB;
        private const ushort TRACE_START = 0xFFFA;
        private const ushort TRACE_STOP = 0xFFF9;
        private const ushort TRACE_TEXT_FILTERED = 0xFFF5;

        // Column IDs (from trccommon.h TRACE_DATA_COLUMNS)
        private const int COL_TEXT = 1;
        private const int COL_BINARYDATA = 2;
        private const int COL_DBID = 3;
        private const int COL_LINENO = 5;
        private const int COL_NTUSERNAME = 6;
        private const int COL_NTDOMAINNAME = 7;
        private const int COL_HOSTNAME = 8;
        private const int COL_APPNAME = 10;
        private const int COL_LOGINNAME = 11;
        private const int COL_SESSION = 12;
        private const int COL_DURATION = 13;
        private const int COL_STARTTIME = 14;
        private const int COL_ENDTIME = 15;
        private const int COL_READS = 16;
        private const int COL_WRITES = 17;
        private const int COL_CPU = 18;
        private const int COL_SEVERITY = 20;
        private const int COL_SUBCLASS = 21;
        private const int COL_OBJID = 22;
        private const int COL_INTDATA = 25;
        private const int COL_CLASS = 27;
        private const int COL_NESTLEVEL = 29;
        private const int COL_STATE = 30;
        private const int COL_ERROR = 31;
        private const int COL_OBJNAME = 34;
        private const int COL_ROWCOUNTS = 48;
        private const int COL_REQUESTID = 49;
        private const int COL_EVENTSEQ = 51;
        private const int COL_OFFSET = 61;

        // Max columns in trace format
        private const int TOTAL_COLUMNS = 76;

        // Fixed-size column sizes (bytes). Columns not listed are variable-length.
        private static readonly Dictionary<int, int> FixedColumnSizes = new Dictionary<int, int>
        {
            { COL_DBID, 4 }, { 4, 4 }, { COL_LINENO, 4 }, { 9, 4 },
            { COL_SESSION, 4 }, { COL_DURATION, 8 }, { COL_STARTTIME, 8 }, { COL_ENDTIME, 8 },
            { COL_READS, 8 }, { COL_WRITES, 8 }, { COL_CPU, 8 }, { 19, 4 },
            { COL_SEVERITY, 4 }, { COL_SUBCLASS, 4 }, { COL_OBJID, 4 }, { 23, 4 },
            { 24, 4 }, { COL_INTDATA, 4 }, { COL_CLASS, 2 }, { 28, 4 },
            { COL_NESTLEVEL, 4 }, { COL_STATE, 4 }, { COL_ERROR, 4 }, { 32, 4 },
            { 33, 4 }, { COL_ROWCOUNTS, 8 }, { COL_REQUESTID, 4 }, { 50, 8 },
            { COL_EVENTSEQ, 8 }, { 52, 8 }, { 53, 8 }, { 54, 16 },
            { 55, 4 }, { 56, 4 }, { 57, 4 }, { 58, 4 },
            { 60, 4 }, { COL_OFFSET, 4 }, { 62, 4 }, { 66, 4 },
            { 68, 16 },
        };

        private long _globalSeq;

        public TrcFileReader(long startingSeq = 0)
        {
            _globalSeq = startingSeq;
        }

        public string[] SupportedExtensions => new[] { ".trc" };

        public IEnumerable<TraceEvent> ReadEvents(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            using (var reader = new BinaryReader(stream, Encoding.Unicode))
            {
                // Read file header
                var header = ReadFileHeader(reader);
                if (header == null)
                    yield break;

                bool isYukon = header.MajorVersion >= 9;

                // Read events
                while (stream.Position < stream.Length - 2)
                {
                    TraceEvent evt = null;
                    try
                    {
                        evt = ReadNextEvent(reader, stream, isYukon);
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Skip malformed events
                        continue;
                    }

                    if (evt != null)
                    {
                        if (evt.Seq == 0)
                            evt.Seq = ++_globalSeq;
                        yield return evt;
                    }
                }
            }
        }

        private TrcHeader ReadFileHeader(BinaryReader reader)
        {
            try
            {
                var header = new TrcHeader();
                header.UnicodeTag = reader.ReadUInt16();
                if (header.UnicodeTag != 0xFEFF)
                    return null;

                header.HeaderSize = reader.ReadUInt16();
                header.TraceVersion = reader.ReadUInt16();

                // Provider name: WCHAR[128] = 256 bytes
                byte[] providerBytes = reader.ReadBytes(256);
                header.ProviderName = Encoding.Unicode.GetString(providerBytes).TrimEnd('\0');

                // Definition type: WCHAR[64] = 128 bytes
                reader.ReadBytes(128);

                header.MajorVersion = reader.ReadByte();
                header.MinorVersion = reader.ReadByte();
                header.BuildNumber = reader.ReadUInt16();
                header.HeaderOptions = reader.ReadUInt32();

                // Server name: WCHAR[128] = 256 bytes
                byte[] serverBytes = reader.ReadBytes(256);
                header.ServerName = Encoding.Unicode.GetString(serverBytes).TrimEnd('\0');

                // RepeatBase
                header.RepeatBase = reader.ReadUInt16();

                return header;
            }
            catch
            {
                return null;
            }
        }

        private TraceEvent ReadNextEvent(BinaryReader reader, Stream stream, bool isYukon)
        {
            // Find TRACE_BEGIN_RECORD marker
            while (true)
            {
                if (stream.Position >= stream.Length - 2)
                    return null;

                ushort marker = reader.ReadUInt16();

                if (marker == 0) // EOF
                    return null;

                // Handle special columns (skip them)
                if (marker >= TRACE_FLUSH && marker <= SQL_TRACE_VERSION && marker != TRACE_BEGIN_RECORD)
                {
                    SkipSpecialColumn(reader, marker);
                    continue;
                }

                if (marker == TRACE_BEGIN_RECORD)
                    break;

                // Unexpected data — try to resync
                if (marker < 1 || marker > TOTAL_COLUMNS)
                    continue;

                // It's a column ID outside of a record context — skip
                SkipColumnData(reader, marker);
            }

            // Read TRACE_BEGIN_RECORD data
            ushort eventId;
            int recordLength;

            if (isYukon)
            {
                // TrcBeginRecordData: USHORT eventId + LONG recordLength
                eventId = reader.ReadUInt16();
                recordLength = reader.ReadInt32();
            }
            else
            {
                // Shiloh: SHORT eventClass + SHORT colCount packed in 4 bytes
                int packed = reader.ReadInt32();
                eventId = (ushort)(packed & 0xFFFF);
                recordLength = -1; // Read until next TRACE_BEGIN_RECORD
            }

            var evt = new TraceEvent();
            evt.EventId = eventId;
            evt.EventType = MapEventClass(eventId);

            // Read columns within the record
            long recordStart = stream.Position;
            long recordEnd = recordLength > 0 ? recordStart + recordLength - 6 : stream.Length; // -6 for begin record header

            while (stream.Position < recordEnd - 1)
            {
                if (stream.Position >= stream.Length - 2)
                    break;

                ushort colId = reader.ReadUInt16();

                if (colId == TRACE_BEGIN_RECORD || colId == 0)
                {
                    // We've hit the next event or EOF — back up and return
                    stream.Position -= 2;
                    break;
                }

                // Skip special markers within events
                if (colId >= TRACE_FLUSH && colId <= SQL_TRACE_VERSION)
                {
                    break;
                }

                if (colId == TRACE_TEXT_FILTERED)
                    colId = COL_TEXT;

                try
                {
                    ReadAndApplyColumn(reader, evt, colId);
                }
                catch
                {
                    break;
                }
            }

            return evt;
        }

        private void ReadAndApplyColumn(BinaryReader reader, TraceEvent evt, int colId)
        {
            if (FixedColumnSizes.TryGetValue(colId, out int fixedSize))
            {
                byte[] data = reader.ReadBytes(fixedSize);
                if (data.Length < fixedSize) return;
                ApplyFixedColumn(evt, colId, data);
            }
            else
            {
                // Variable-length: read length prefix
                byte firstByte = reader.ReadByte();
                int dataLen;
                if (firstByte == 0xFF)
                    dataLen = reader.ReadInt32();
                else
                    dataLen = firstByte;

                if (dataLen <= 0 || dataLen > 10 * 1024 * 1024) // Sanity: max 10MB per column
                    return;

                byte[] data = reader.ReadBytes(dataLen);
                if (data.Length < dataLen) return;
                ApplyVariableColumn(evt, colId, data);
            }
        }

        private void ApplyFixedColumn(TraceEvent evt, int colId, byte[] data)
        {
            switch (colId)
            {
                case COL_DBID:
                    evt.DatabaseId = BitConverter.ToInt32(data, 0);
                    break;
                case COL_SESSION:
                    evt.SessionId = BitConverter.ToInt32(data, 0);
                    break;
                case COL_DURATION:
                    evt.Duration = BitConverter.ToInt64(data, 0);
                    break;
                case COL_STARTTIME:
                    evt.StartTime = ReadSqlDateTime(data, 0);
                    break;
                case COL_ENDTIME:
                    evt.EndTime = ReadSqlDateTime(data, 0);
                    break;
                case COL_READS:
                    evt.Reads = BitConverter.ToInt64(data, 0);
                    break;
                case COL_WRITES:
                    evt.Writes = BitConverter.ToInt64(data, 0);
                    break;
                case COL_CPU:
                    evt.CPU = BitConverter.ToInt64(data, 0);
                    break;
                case COL_OBJID:
                    evt.ObjectId = BitConverter.ToInt32(data, 0);
                    break;
                case COL_NESTLEVEL:
                    evt.NestLevel = BitConverter.ToInt32(data, 0);
                    break;
                case COL_ERROR:
                    evt.Error = BitConverter.ToInt32(data, 0);
                    break;
                case COL_SEVERITY:
                    evt.Severity = BitConverter.ToInt32(data, 0);
                    break;
                case COL_STATE:
                    evt.State = BitConverter.ToInt32(data, 0);
                    break;
                case COL_SUBCLASS:
                    evt.EventSubclass = BitConverter.ToInt32(data, 0);
                    break;
                case COL_INTDATA:
                    evt.IntegerData = BitConverter.ToInt32(data, 0);
                    break;
                case COL_ROWCOUNTS:
                    evt.RowCount = BitConverter.ToInt64(data, 0);
                    break;
                case COL_REQUESTID:
                    evt.RequestId = BitConverter.ToInt32(data, 0);
                    break;
                case COL_EVENTSEQ:
                    evt.Seq = BitConverter.ToInt64(data, 0);
                    break;
                case COL_OFFSET:
                    evt.Offset = BitConverter.ToInt32(data, 0);
                    break;
                case COL_LINENO:
                    evt.LineNumber = BitConverter.ToInt32(data, 0);
                    break;
            }
        }

        private void ApplyVariableColumn(TraceEvent evt, int colId, byte[] data)
        {
            switch (colId)
            {
                case COL_TEXT:
                    evt.TextData = Encoding.Unicode.GetString(data).TrimEnd('\0');
                    break;
                case COL_LOGINNAME:
                    evt.LoginName = Encoding.Unicode.GetString(data).TrimEnd('\0');
                    break;
                case COL_APPNAME:
                    evt.ApplicationName = Encoding.Unicode.GetString(data).TrimEnd('\0');
                    break;
                case COL_HOSTNAME:
                    evt.HostName = Encoding.Unicode.GetString(data).TrimEnd('\0');
                    break;
                case COL_NTUSERNAME:
                    evt.NTUserName = Encoding.Unicode.GetString(data).TrimEnd('\0');
                    break;
                case COL_NTDOMAINNAME:
                    evt.NTDomainName = Encoding.Unicode.GetString(data).TrimEnd('\0');
                    break;
                case COL_OBJNAME:
                    evt.ObjectName = Encoding.Unicode.GetString(data).TrimEnd('\0');
                    break;
            }
        }

        private static DateTime? ReadSqlDateTime(byte[] data, int offset)
        {
            if (data.Length < offset + 8) return null;
            try
            {
                // SQL Server datetime: 4-byte days since 1900-01-01 + 4-byte 300ths of second
                int days = BitConverter.ToInt32(data, offset);
                uint ticks = BitConverter.ToUInt32(data, offset + 4);
                DateTime baseDate = new DateTime(1900, 1, 1);
                return baseDate.AddDays(days).AddMilliseconds(ticks * 10.0 / 3.0);
            }
            catch
            {
                return null;
            }
        }

        private void SkipSpecialColumn(BinaryReader reader, ushort marker)
        {
            // Special columns have variable data — read and discard
            try
            {
                switch (marker)
                {
                    case SQL_TRACE_VERSION:
                    case TRACED_OPTIONS:
                    case TRACE_START:
                    case TRACE_STOP:
                        // These have a 4-byte length followed by data
                        int len = reader.ReadInt32();
                        if (len > 0 && len < 1024 * 1024)
                            reader.ReadBytes(len);
                        break;
                    case TRACED_EVENTS:
                    case TRACED_FILTERS:
                        // Variable-length: read length prefix
                        byte fb = reader.ReadByte();
                        int dlen = fb == 0xFF ? reader.ReadInt32() : fb;
                        if (dlen > 0 && dlen < 1024 * 1024)
                            reader.ReadBytes(dlen);
                        break;
                    case TRACE_FLUSH:
                        // No data
                        break;
                }
            }
            catch { }
        }

        private void SkipColumnData(BinaryReader reader, int colId)
        {
            try
            {
                if (FixedColumnSizes.TryGetValue(colId, out int size))
                {
                    reader.ReadBytes(size);
                }
                else
                {
                    byte fb = reader.ReadByte();
                    int len = fb == 0xFF ? reader.ReadInt32() : fb;
                    if (len > 0 && len < 10 * 1024 * 1024)
                        reader.ReadBytes(len);
                }
            }
            catch { }
        }

        private static TraceEventType MapEventClass(int eventId)
        {
            if (Enum.IsDefined(typeof(TraceEventType), eventId))
                return (TraceEventType)eventId;
            return TraceEventType.Unknown;
        }

        private class TrcHeader
        {
            public ushort UnicodeTag;
            public ushort HeaderSize;
            public ushort TraceVersion;
            public string ProviderName;
            public byte MajorVersion;
            public byte MinorVersion;
            public ushort BuildNumber;
            public uint HeaderOptions;
            public string ServerName;
            public ushort RepeatBase;
        }
    }
}
