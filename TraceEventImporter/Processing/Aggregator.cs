using System;
using System.Collections.Generic;
using System.Linq;

namespace TraceEventImporter.Processing
{
    /// <summary>
    /// Builds tblTimeIntervals and computes per-(HashID, TimeInterval, DBID, AppNameID, LoginNameID)
    /// aggregates for tblBatchPartialAggs and tblStmtPartialAggs.
    /// </summary>
    public class Aggregator
    {
        private readonly int _intervalSeconds;

        public List<TimeIntervalRow> TimeIntervals { get; } = new List<TimeIntervalRow>();
        public List<BatchPartialAggRow> BatchAggs { get; } = new List<BatchPartialAggRow>();
        public List<StmtPartialAggRow> StmtAggs { get; } = new List<StmtPartialAggRow>();

        public Aggregator(int intervalSeconds = 60)
        {
            _intervalSeconds = intervalSeconds > 0 ? intervalSeconds : 60;
        }

        public void Compute(List<BatchRow> batches, List<StatementRow> statements)
        {
            // Determine overall time range
            DateTime minTime = DateTime.MaxValue;
            DateTime maxTime = DateTime.MinValue;

            foreach (var b in batches)
            {
                if (b.StartTime.HasValue && b.StartTime.Value < minTime) minTime = b.StartTime.Value;
                if (b.EndTime.HasValue && b.EndTime.Value > maxTime) maxTime = b.EndTime.Value;
            }
            foreach (var s in statements)
            {
                if (s.StartTime.HasValue && s.StartTime.Value < minTime) minTime = s.StartTime.Value;
                if (s.EndTime.HasValue && s.EndTime.Value > maxTime) maxTime = s.EndTime.Value;
            }

            if (minTime >= maxTime)
                return;

            // Build time intervals
            BuildTimeIntervals(minTime, maxTime);

            // Build batch aggregations
            ComputeBatchAggs(batches);

            // Build statement aggregations
            ComputeStmtAggs(statements);
        }

        private void BuildTimeIntervals(DateTime minTime, DateTime maxTime)
        {
            DateTime current = minTime;
            int intervalId = 1;
            while (current < maxTime)
            {
                DateTime end = current.AddSeconds(_intervalSeconds);
                if (end > maxTime) end = maxTime;

                TimeIntervals.Add(new TimeIntervalRow
                {
                    TimeInterval = intervalId++,
                    StartTime = current,
                    EndTime = end
                });

                current = end;
            }
        }

        private void ComputeBatchAggs(List<BatchRow> batches)
        {
            // Group by HashID, TimeInterval, DBID, AppNameID, LoginNameID
            foreach (var batch in batches)
            {
                int timeInterval = FindTimeInterval(batch.EndTime ?? batch.StartTime);
                if (timeInterval <= 0) continue;

                var key = new AggKey(batch.HashID, timeInterval, batch.DBID, batch.AppNameID, batch.LoginNameID);

                if (!_batchAggDict.TryGetValue(key, out var agg))
                {
                    agg = new BatchPartialAggRow
                    {
                        HashID = batch.HashID,
                        TimeInterval = timeInterval,
                        DBID = batch.DBID,
                        AppNameID = batch.AppNameID,
                        LoginNameID = batch.LoginNameID
                    };
                    _batchAggDict[key] = agg;
                    BatchAggs.Add(agg);
                }

                // Count events
                if (batch.StartTime.HasValue)
                    agg.StartingEvents++;
                if (batch.EndTime.HasValue)
                    agg.CompletedEvents++;
                if (batch.AttnSeq.HasValue)
                    agg.AttentionEvents++;

                // Aggregate metrics (from completed events only)
                if (batch.Duration.HasValue)
                {
                    agg.TotalDuration = (agg.TotalDuration ?? 0) + batch.Duration.Value;
                    agg.MinDuration = agg.MinDuration.HasValue ? Math.Min(agg.MinDuration.Value, batch.Duration.Value) : batch.Duration.Value;
                    agg.MaxDuration = agg.MaxDuration.HasValue ? Math.Max(agg.MaxDuration.Value, batch.Duration.Value) : batch.Duration.Value;
                }
                if (batch.Reads.HasValue)
                {
                    agg.TotalReads = (agg.TotalReads ?? 0) + batch.Reads.Value;
                    agg.MinReads = agg.MinReads.HasValue ? Math.Min(agg.MinReads.Value, batch.Reads.Value) : batch.Reads.Value;
                    agg.MaxReads = agg.MaxReads.HasValue ? Math.Max(agg.MaxReads.Value, batch.Reads.Value) : batch.Reads.Value;
                }
                if (batch.Writes.HasValue)
                {
                    agg.TotalWrites = (agg.TotalWrites ?? 0) + batch.Writes.Value;
                    agg.MinWrites = agg.MinWrites.HasValue ? Math.Min(agg.MinWrites.Value, batch.Writes.Value) : batch.Writes.Value;
                    agg.MaxWrites = agg.MaxWrites.HasValue ? Math.Max(agg.MaxWrites.Value, batch.Writes.Value) : batch.Writes.Value;
                }
                if (batch.CPU.HasValue)
                {
                    agg.TotalCPU = (agg.TotalCPU ?? 0) + batch.CPU.Value;
                    agg.MinCPU = agg.MinCPU.HasValue ? Math.Min(agg.MinCPU.Value, batch.CPU.Value) : batch.CPU.Value;
                    agg.MaxCPU = agg.MaxCPU.HasValue ? Math.Max(agg.MaxCPU.Value, batch.CPU.Value) : batch.CPU.Value;
                }
            }
        }

        private void ComputeStmtAggs(List<StatementRow> statements)
        {
            foreach (var stmt in statements)
            {
                int timeInterval = FindTimeInterval(stmt.EndTime ?? stmt.StartTime);
                if (timeInterval <= 0) continue;

                var key = new AggKey(stmt.HashID, timeInterval, stmt.DBID, stmt.AppNameID, stmt.LoginNameID);

                if (!_stmtAggDict.TryGetValue(key, out var agg))
                {
                    agg = new StmtPartialAggRow
                    {
                        HashID = stmt.HashID,
                        TimeInterval = timeInterval,
                        ObjectID = stmt.ObjectID,
                        DBID = stmt.DBID,
                        AppNameID = stmt.AppNameID,
                        LoginNameID = stmt.LoginNameID
                    };
                    _stmtAggDict[key] = agg;
                    StmtAggs.Add(agg);
                }

                if (stmt.StartTime.HasValue) agg.StartingEvents++;
                if (stmt.EndTime.HasValue) agg.CompletedEvents++;
                if (stmt.AttnSeq.HasValue) agg.AttentionEvents++;

                if (stmt.Duration.HasValue)
                {
                    agg.TotalDuration = (agg.TotalDuration ?? 0) + stmt.Duration.Value;
                    agg.MinDuration = agg.MinDuration.HasValue ? Math.Min(agg.MinDuration.Value, stmt.Duration.Value) : stmt.Duration.Value;
                    agg.MaxDuration = agg.MaxDuration.HasValue ? Math.Max(agg.MaxDuration.Value, stmt.Duration.Value) : stmt.Duration.Value;
                }
                if (stmt.Reads.HasValue)
                {
                    agg.TotalReads = (agg.TotalReads ?? 0) + stmt.Reads.Value;
                    agg.MinReads = agg.MinReads.HasValue ? Math.Min(agg.MinReads.Value, stmt.Reads.Value) : stmt.Reads.Value;
                    agg.MaxReads = agg.MaxReads.HasValue ? Math.Max(agg.MaxReads.Value, stmt.Reads.Value) : stmt.Reads.Value;
                }
                if (stmt.Writes.HasValue)
                {
                    agg.TotalWrites = (agg.TotalWrites ?? 0) + stmt.Writes.Value;
                    agg.MinWrites = agg.MinWrites.HasValue ? Math.Min(agg.MinWrites.Value, stmt.Writes.Value) : stmt.Writes.Value;
                    agg.MaxWrites = agg.MaxWrites.HasValue ? Math.Max(agg.MaxWrites.Value, stmt.Writes.Value) : stmt.Writes.Value;
                }
                if (stmt.CPU.HasValue)
                {
                    agg.TotalCPU = (agg.TotalCPU ?? 0) + stmt.CPU.Value;
                    agg.MinCPU = agg.MinCPU.HasValue ? Math.Min(agg.MinCPU.Value, stmt.CPU.Value) : stmt.CPU.Value;
                    agg.MaxCPU = agg.MaxCPU.HasValue ? Math.Max(agg.MaxCPU.Value, stmt.CPU.Value) : stmt.CPU.Value;
                }
            }
        }

        private int FindTimeInterval(DateTime? time)
        {
            if (!time.HasValue || TimeIntervals.Count == 0) return 0;
            DateTime t = time.Value;
            foreach (var ti in TimeIntervals)
            {
                if (t >= ti.StartTime && t < ti.EndTime)
                    return ti.TimeInterval;
            }
            // If after last interval, use the last one
            return TimeIntervals[TimeIntervals.Count - 1].TimeInterval;
        }

        private readonly Dictionary<AggKey, BatchPartialAggRow> _batchAggDict = new Dictionary<AggKey, BatchPartialAggRow>();
        private readonly Dictionary<AggKey, StmtPartialAggRow> _stmtAggDict = new Dictionary<AggKey, StmtPartialAggRow>();

        private struct AggKey : IEquatable<AggKey>
        {
            public readonly long HashID;
            public readonly int TimeInterval;
            public readonly int DBID;
            public readonly int AppNameID;
            public readonly int LoginNameID;

            public AggKey(long hashId, int ti, int dbid, int appId, int loginId)
            {
                HashID = hashId; TimeInterval = ti; DBID = dbid; AppNameID = appId; LoginNameID = loginId;
            }

            public bool Equals(AggKey o) => HashID == o.HashID && TimeInterval == o.TimeInterval && DBID == o.DBID && AppNameID == o.AppNameID && LoginNameID == o.LoginNameID;
            public override bool Equals(object obj) => obj is AggKey k && Equals(k);
            public override int GetHashCode() => HashID.GetHashCode() ^ (TimeInterval * 397) ^ (DBID * 31) ^ AppNameID ^ LoginNameID;
        }
    }

    #region Aggregation Row Types

    public class TimeIntervalRow
    {
        public int TimeInterval;
        public DateTime StartTime;
        public DateTime EndTime;
    }

    public class BatchPartialAggRow
    {
        public long HashID;
        public int TimeInterval;
        public int StartingEvents;
        public int CompletedEvents;
        public int AttentionEvents;
        public long? MinDuration, MaxDuration, TotalDuration;
        public long? MinReads, MaxReads, TotalReads;
        public long? MinWrites, MaxWrites, TotalWrites;
        public long? MinCPU, MaxCPU, TotalCPU;
        public int AppNameID;
        public int LoginNameID;
        public int DBID;
    }

    public class StmtPartialAggRow
    {
        public long HashID;
        public int TimeInterval;
        public int? ObjectID;
        public int? DBID;
        public int AppNameID;
        public int LoginNameID;
        public int StartingEvents;
        public int CompletedEvents;
        public int AttentionEvents;
        public long? MinDuration, MaxDuration, TotalDuration;
        public long? MinReads, MaxReads, TotalReads;
        public long? MinWrites, MaxWrites, TotalWrites;
        public long? MinCPU, MaxCPU, TotalCPU;
    }

    #endregion
}
