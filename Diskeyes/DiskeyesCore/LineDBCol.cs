using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiskeyesCore
{
    abstract class LineDBCol
    {
        public abstract Task<bool> AppendHot();
        public abstract Task<bool> Search(string[] values, bool[] desiredPresence, CancellationToken token, IProgress<SearchBatch> progress, int identifier);
        public abstract Task<bool> Initialize();
    }
    class LineDBCol<T> : LineDBCol
    {
        #region Properties
        public int BulkInRAM { get { return Math.Abs(bulkInRAM); } }
        public const uint FileAppendTriggerSize = 50000;
        public const int AvailableSlotsCap = 150000;
        public const string DBdir = "";
        public const string Format = ".LineDB";
        public const string FormatChanges = ".CHANGES";
        public const string TempMarker = ".TEMP";
        public int EntriesCount { get => entriesCount; }
        public int AvailableCount { get => AvailableSlots.Count; }
        public int ChangesCount { get => changesCount; }
        public string Name { get => name; }
        public int NextID { get => Math.Max(lastID, Utilities.MaxKey(ref HotChanges)) + 1; }
        protected int bulkInRAM;
        protected int entriesCount;
        protected int changesCount;
        protected int lastID = -1;
        protected string name;
        protected Dictionary<int, string> SavedChanges;
        protected Dictionary<int, string> HotChanges;
        protected CollectionCap<HashSet<int>, int> AvailableSlots;
        protected ReadOnlyCollection<(int, T, string)> DataPeek;
        protected Func<string, T> DeserializerLambda;
        protected Func<T, string> SerializerLambda;
        protected Encoding FileEncoding;
        protected string AvailabilityMarker;
        #endregion
        public LineDBCol(string name, Encoding encoding, Func<T, string> serializer, Func<string, T> deserializer, int inRAMsize = 300, string availabilityMarker = "")
        {
            this.name = name;
            FileEncoding = encoding;
            SerializerLambda = serializer;
            DeserializerLambda = deserializer;
            bulkInRAM = inRAMsize;
            AvailabilityMarker = availabilityMarker;
            AvailableSlots = new CollectionCap<HashSet<int>, int>(AvailableSlotsCap, new HashSet<int>());
            SavedChanges = new Dictionary<int, string>();
            HotChanges = new Dictionary<int, string>();
        }

        public override async Task<bool> Initialize()
        {
            try
            {
                await Task.Run(() =>
                {
                    ReadMeta();
                    ReadChangesMeta();
                    Load();
                });
                return true;
            }
            catch (AggregateException)
            {
                return false;
            }


        }
        /// <summary>
        /// Sets the expected number of entries from the main data file.
        /// Must not be called when the main data file might be written to.
        /// </summary>
        protected virtual void ReadMeta()
        {
            try
            {
                using (var fp = new StreamReader(File.Open(DBdir + name + Format, FileMode.Open, FileAccess.Read), FileEncoding))
                {
                    var info = MetaLine(fp.ReadLine());
                    entriesCount = int.Parse(info["entries"]);
                }
            }
            catch
            {
                entriesCount = 0;
                using (var fp = new StreamWriter(File.Open(DBdir + name + Format, FileMode.Create, FileAccess.Write)))
                {
                    fp.WriteLine(String.Format("{0},", EntriesCount));
                }
            }
        }
        /// <summary>
        /// Sets the expected number of entries from CHANGES file.
        /// Must not be called when the CHANGES file might be written to.
        /// </summary>
        protected virtual void ReadChangesMeta()
        {
            try
            {
                using (var fp = new StreamReader(File.Open(DBdir + name + FormatChanges, FileMode.Open, FileAccess.Read), FileEncoding))
                {
                    var info = MetaLine(fp.ReadLine());
                    changesCount = int.Parse(info["entries"]);
                }
            }
            catch
            {
                changesCount = 0;
                File.Create(DBdir + name + FormatChanges, 4096, FileOptions.SequentialScan).Close();
                using (var fp = new StreamWriter(File.Open(DBdir + name + FormatChanges, FileMode.Create, FileAccess.Write)))
                {
                    fp.WriteLine(String.Format("{0},", ChangesCount));
                }
            }
        }
        /// <summary>
        /// Skips the metadata line and returns data line by line together with the line index.
        /// </summary>
        protected IEnumerable<(int, string)> Read()
        {
            const short toSkip = 1; // skipping meta line
            int counter = 0;
            foreach (string line in File.ReadLines(DBdir + name + Format).Skip(toSkip))
            {
                //yield return (counter + toSkip, line); // deprecated to keep indexing simple and ignore metadata line
                yield return (counter, line);
                counter += 1;
            }
        }
        /// <summary>
        /// Skips the metadata line and returns the changes file line by line,
        /// together with the index of the changed line.
        /// </summary>
        protected IEnumerable<(int, string)> ReadChanges()
        {
            const short toSkip = 1; // skipping meta line
            foreach (string line in File.ReadLines(DBdir + name + FormatChanges).Skip(toSkip))
            {
                string[] data = line.Split(",", 2);
                yield return (int.Parse(data[0]), data[1]);
            }
        }

        /// <summary>
        /// Collects indices of available slots and stores changes.
        /// </summary>
        protected virtual void Scan()
        {
            entriesCount = 0;
            changesCount = 0;
            AvailableSlots.Clear();
            SavedChanges.Clear();

            SavedChanges.EnsureCapacity(ChangesCount);

            foreach (var (index, data) in Read())
            {
                // Find empty slots in saved data file
                lastID = index; // simple assignment is possible, because retrieved indices are always in ascending order here
                if (data == AvailabilityMarker) AvailableSlots.Add(index);
                entriesCount += 1;
            }
            foreach (var (index, data) in ReadChanges())
            {
                // Add / remove empty slots based on changes file
                if (index > lastID) lastID = index; // here, only indices for changed/added lines are present
                SavedChanges[index] = data;
                if (data == AvailabilityMarker) AvailableSlots.Add(index);
                else if (AvailableSlots.Contains(index)) AvailableSlots.Remove(index);
                changesCount += 1;
            }
            foreach (KeyValuePair<int, string> change in HotChanges)
            {
                // Add / remove empty slots based on hot changes in RAM
                if (change.Value == AvailabilityMarker) AvailableSlots.Add(change.Key);
                else if (AvailableSlots.Contains(change.Key)) AvailableSlots.Remove(change.Key);
            }
        }

        /// <summary>
        /// Skip meta data, optionally skip some lines, read file in batches, parsing lines with a lambda convertor.
        /// </summary>
        /// <typeparam name="T">Data type of converted line data.</typeparam>
        /// <param name="toSkip">Lines to be skipped after the meta data line.</param>
        /// <returns>A lazy-loaded list of indices paired with converted data.</returns>
        protected IEnumerable<List<(int, T, string)>> IndexedBatches(int toSkip = 0)
        {
            int batchSize = (int)BulkInRAM;
            var batch = new List<(int, T, string)>(batchSize);
            foreach (var (index, data) in Read().Skip(toSkip))
            {
                batch.Add((index, DeserializerLambda(data), data));
                if (batch.Count >= BulkInRAM)
                {
                    yield return batch;
                    batch = new List<(int, T, string)>(batchSize);
                }
            }
            batch.TrimExcess();
            if (batch.Count > 0) yield return batch;
        }

        /// <summary>
        /// Skip meta data, optionally skip some lines, read file in batches, parsing lines with a lambda convertor.
        /// </summary>
        /// <typeparam name="T">Data type of converted line data.</typeparam>
        /// <param name="toSkip">Lines to be skipped after the meta data line.</param>
        /// <returns>A lazy-loaded batch (list) of converted data.</returns>
        protected IEnumerable<List<T>> Batches(int toSkip = 0)
        {
            int batchSize = BulkInRAM;
            var batch = new List<T>(batchSize);
            foreach (var (index, data) in Read().Skip(toSkip))
            {
                batch.Add(DeserializerLambda(data));
                if (batch.Count >= BulkInRAM)
                {
                    yield return batch;
                    batch = new List<T>(batchSize);
                }
            }
            batch.TrimExcess();
            if (batch.Count > 0) yield return batch;
        }

        protected IEnumerable<List<(int, string)>> ReadBatches()
        {
            int batchSize = BulkInRAM;
            var batch = new List<(int, string)>(batchSize);
            var mergedChanges = MergedChanges();

            if (DataPeek is object)
                yield return DataPeek.Select(x => (x.Item1, x.Item3)).ToList();

            foreach (var (index, data) in Read().Skip(batchSize))
            {
                string content;
                if (!mergedChanges.TryGetValue(index, out content))
                {
                    content = data;
                }

                batch.Add((index, content));
                if (batch.Count >= BulkInRAM)
                {
                    yield return batch;
                    batch = new List<(int, string)>(batchSize);
                }
            }
            foreach (var (index, data) in mergedChanges.Where(x => x.Key >= entriesCount))
            {
                batch.Add((index, data));
                if (batch.Count >= BulkInRAM)
                {
                    yield return batch;
                    batch = new List<(int, string)>(batchSize);
                }
            }
            batch.TrimExcess();
            if (batch.Count > 0) yield return batch;
        }
        public IEnumerable<List<(int, string[])>> ReadCSVBatches()
        {
            foreach (var batch in ReadBatches())
            {
                List<(int, string[])> output = new List<(int, string[])>(batch.Count);
                foreach (var (index, data) in batch)
                {
                    output.Add((index, data.Split(",")));
                }
                yield return output;
            }
        }
        public IEnumerable<List<(int, T)>> Access()
        {
            foreach (var batch in ReadBatches())
            {
                List<(int, T)> output = new List<(int, T)>(batch.Count);
                foreach (var (index, data) in batch)
                {
                    output.Add((index, DeserializerLambda(data)));
                }
                yield return output;
            }
        }

        protected Dictionary<int, string> MergedChanges()
        {
            return Utilities.MergeDictionaries<int, string>(SavedChanges, HotChanges);
        }
        /// <summary>
        /// Searches for the presence or absence of the provided values asynchronously and passes matches to the Progress Reporter.
        /// The final boolean vector reflects NEITHER the presence NOR the absence of respective values in the entry,
        /// but rather whether the specific value met the criteria for desired presence.
        /// A missing value will return true if the value is not wanted in the query, false if its presence is desired.
        /// </summary>
        /// <param name="values">Values to search for</param>
        /// <param name="desiredPresence">Boolean vector of desired presence (true = desired presence, false = desired absence).</param>
        /// <param name="token"></param>
        /// <param name="progress"></param>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public override async Task<bool> Search(string[] values, bool[] desiredPresence, CancellationToken token, IProgress<SearchBatch> progress, int identifier = 0)
        {
            await Task.Run(() =>
            {
                foreach (var batch in ReadCSVBatches())
                {
                    var matches = new List<(int, bool[])>();
                    foreach (var (index, entry) in batch)
                    {
                        // initialize the vector with opposite values
                        // seeked -> unfound, not seeked -> found
                        var vector = desiredPresence.Select(x=>!x).ToArray();
                        bool match = false;
                        for (int i = 0; i < values.Length; i++)
                        {
                            if (entry.Contains(values[i]))
                            {
                                vector[i] = desiredPresence[i];
                                match = true;
                            }
                        }
                        if (match)
                        {
                            matches.Add((index, vector));
                        }
                    }
                    if (matches.Count > 0)
                    {
                        progress.Report(new SearchBatch(matches.AsReadOnly(), identifier));
                    }
                    if (token.IsCancellationRequested) return;
                }
            });
            return true;
        }

        public virtual void Change(int index, string value)
        {
            if (value == AvailabilityMarker)
            {
                AvailableSlots.Add(index);
                HotChanges[index] = value;
            }
            else
            {
                try
                {
                    AvailableSlots.Remove(index);
                }
                finally
                {
                    HotChanges[index] = value;
                }
            }
        }

        public void Change(int index, T value)
        {
            Change(index, SerializerLambda(value));
        }

        public int Add(string value)
        {
            int id;
            try
            {
                id = AvailableSlots.Storage.First();
                AvailableSlots.Remove(id);
            }
            catch
            {
                id = NextID;
            }
            Change(id, value);
            return id;
        }

        public int Add(T value)
        {
            return Add(SerializerLambda(value));
        }

        public void Remove(int index)
        {
            Change(index, AvailabilityMarker);
        }

        protected virtual void Load()
        {
            Scan();
            var firstBatch = IndexedBatches().FirstOrDefault();
            DataPeek = firstBatch is null ? null : firstBatch.AsReadOnly();
        }
        /// <summary>
        /// Parses metadata from the first line of the file.
        /// </summary>
        protected virtual Dictionary<string, string> MetaLine(string line)
        {
            string[] values = line.Split(",")
                                  .Select(value => value.Trim())
                                  .ToArray();

            return new Dictionary<string, string>() {
                                                     { "entries", values[0] },
                                                    };
        }

        public IEnumerable<string> OldOrChange(IEnumerable<(int, string)> savedData, Dictionary<int, string> mergedChanges)
        {
            foreach (var (index, value) in savedData)
            {
                if (mergedChanges.ContainsKey(index))
                {
                    yield return mergedChanges[index];
                }
                else
                {
                    yield return value;
                }
            }
        }

        public async Task<bool> ApplyChanges()
        {
            var allChanges = MergedChanges();
            var changesSnapshot = new Dictionary<int, string>(HotChanges); // a deep copy
            var appendedContent = allChanges.Where(x => x.Key > lastID).Select(x => x.Value);
            IEnumerable<string> allContent = OldOrChange(Read(), allChanges).Concat(appendedContent);
            int newCount = entriesCount + appendedContent.Count();
            string finalFile = DBdir + name + Format;
            string tempFile = finalFile + TempMarker;
            string changesFile = DBdir + name + FormatChanges;

            string metaLine = String.Format("{0},", newCount);
            File.WriteAllLines(tempFile, new string[] { metaLine });

            bool success = false;
            Task writingTask = File.AppendAllLinesAsync(tempFile, allContent)
                                   .ContinueWith((previousTask) =>
            {
                Console.WriteLine("Finished writing");
                if (previousTask.IsFaulted == false)
                {
                    // rename the temporary file as the original
                    File.Move(tempFile, finalFile, true);
                    File.Delete(changesFile);
                    ClearHotChanges(changesSnapshot);
                    SavedChanges.Clear();
                    SavedChanges.TrimExcess();
                    HotChanges.TrimExcess();
                    success = true;
                }
            });
            await Task.Run(() => writingTask);
            return success;
        }
        public void WriteChanges()
        {
            long changesFileSize = new System.IO.FileInfo(DBdir + name + Format).Length;
            if (changesFileSize > FileAppendTriggerSize || HotChanges.Count < 100)
            {
                AppendHot().RunSynchronously();
            }
            else
            {
                RewriteChanges().RunSynchronously();
            }
        }

        public async Task<bool> RewriteChanges()
        {
            // temporary file
            string finalFile = DBdir + name + FormatChanges;
            string tempFile = finalFile + TempMarker;

            var mergedChanges = MergedChanges();
            string metaLine = String.Format("{0},", mergedChanges.Count);
            await File.WriteAllLinesAsync(tempFile, new string[] { metaLine });

            var data = mergedChanges.Select(x => String.Format("{0},{1}", x.Key, x.Value));
            var changesSnapshot = new Dictionary<int, string>(HotChanges);
            await File.AppendAllLinesAsync(tempFile, data, FileEncoding);
            await Task.Run(() => ClearHotChanges(changesSnapshot));
            File.Move(tempFile, finalFile, true);
            return true;
        }

        public override async Task<bool> AppendHot()
        {
            string filePath = DBdir + name + FormatChanges;
            try
            {
                // check if metaline exists in a valid form
                string firstLine = File.ReadLines(filePath, FileEncoding).First();
                var metaData = MetaLine(firstLine);
            }
            catch
            {
                int changesCount = MergedChanges().Count;
                string metaLine = String.Format("{0},", changesCount);
                File.WriteAllLines(filePath, new string[] { metaLine });
            }
            finally
            {
                var data = HotChanges.Select(x => String.Format("{0},{1}", x.Key, x.Value));
                var changesSnapshot = new Dictionary<int, string>(HotChanges);
                Console.WriteLine("Started writing");
                await File.AppendAllLinesAsync(filePath, data, FileEncoding);
                Console.WriteLine("Clearing hot changes");
                await Task.Run(() => ClearHotChanges(changesSnapshot));
            }
            return true;
        }
        protected void ClearHotChanges(Dictionary<int, string> changes)
        {
            // Although changes and HotChanges should be identical,
            // check if values have not changed during the async file writing
            Console.WriteLine("moving changes");
            foreach (var change in changes)
            {
                if (HotChanges[change.Key] == change.Value)
                {
                    SavedChanges[change.Key] = change.Value;
                    HotChanges.Remove(change.Key);
                }
            }
            Console.WriteLine("Finished moving changes");
        }
        public ReadOnlyCollection<(int, T, string)> Peek()
        {
            return DataPeek;
        }
        ~LineDBCol()
        {
            AppendHot().RunSynchronously();
        }
    }
}
