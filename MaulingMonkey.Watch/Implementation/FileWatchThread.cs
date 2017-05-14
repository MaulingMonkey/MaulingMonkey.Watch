// Copyright 2017 MaulingMonkey
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MaulingMonkey.Implementation {
	static class FileWatchThreads {
		public class Entry {
			public Entry(string path, bool watch, Action<IEnumerable<string>> onChanged, Action<Exception> onError) {
				Pending   = false;
				LastMeta  = FileMetaSnapshot.From(path);

				Path      = path;
				Watch     = watch;
				OnChanged = onChanged;
				OnError   = onError;
			}
			public bool                                 Pending;
			public FileMetaSnapshot                     LastMeta;

			public readonly string                      Path;
			public readonly bool                        Watch;
			public readonly Action<IEnumerable<string>> OnChanged;
			public readonly Action<Exception>           OnError;

			// Only used to protect mutable data such as Pending and LastMeta.
			public object                               Mutex { get { return this; } }
		}

		public static void Add(Entry e) {
			lock (All) All.Add(e);
			
			var dir  = Path.GetDirectoryName(e.Path);
			var name = Path.GetFileName(e.Path);
			if (!Directory.Exists(dir   )) throw new DirectoryNotFoundException("Watch.FileLines: Directory not found: "+dir);
			if (!File     .Exists(e.Path)) throw new FileNotFoundException     ("Watch.FileLines: File not found: "+e.Path, e.Path);

			if (e.Watch) {
				var fsw = new FileSystemWatcher(dir, name) {
					IncludeSubdirectories = false,
					NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.Security | NotifyFilters.FileName,
				};
				fsw.Created += delegate { lock (e.Mutex) { e.LastMeta = FileMetaSnapshot.From(e.Path); Refresh(e); } };
				fsw.Changed += delegate { lock (e.Mutex) { e.LastMeta = FileMetaSnapshot.From(e.Path); Refresh(e); } };
				fsw.Error   += delegate { lock (e.Mutex) { e.LastMeta = FileMetaSnapshot.From(e.Path); Refresh(e); } };
				fsw.Deleted += delegate { lock (e.Mutex) { e.LastMeta = FileMetaSnapshot.From(e.Path); Refresh(e); } };
				fsw.Renamed += delegate { lock (e.Mutex) { e.LastMeta = FileMetaSnapshot.From(e.Path); Refresh(e); } };
				fsw.EnableRaisingEvents = true;
			}

			Refresh(e);
		}

		public static void Refresh(Entry we) {
			lock (we.Mutex) {
				if (we.Pending) return; // Already refreshing...

				we.Pending = true;
				ThreadPool.QueueUserWorkItem(delegate{DoWork(we);});
			}
		}

		static readonly List<Entry>  All         = new List<Entry>();
		static readonly List<Entry>  AllTemp     = new List<Entry>();
		static readonly Timer        PollTimer   = new Timer(delegate{PollFileMetadatasChanged();}, null, 30000, 30000);

		static void PollFileMetadatasChanged() {
			lock (AllTemp) {
				AllTemp.Clear();
				lock (All) AllTemp.AddRange(All);
				foreach (var entry in AllTemp) {
					try {
						lock (entry.Mutex) if (PollFileMetaChanged(entry)) Refresh(entry);
					}
					catch (IOException ex) when (entry.OnError != null) { entry.OnError(ex); }
				}
			}
		}

		static bool PollFileMetaChanged(Entry e) { // FIXME: Rename to better indicate side effects?!?
			var current = FileMetaSnapshot.From(e.Path);
			bool changes = false;
			changes = e.LastMeta != current;
			e.LastMeta = current;
			return changes;
		}

		static void DoWork(Entry entry) {
			var commit = false;
			try {
				IEnumerable<string> lines;
				lock (entry.Mutex) {
					lines = File.ReadLines(entry.Path);
					commit = true;
					entry.Pending = false;
				}
				entry.OnChanged(lines);
			}
			catch (Exception ex) when (entry.OnError != null) { entry.OnError(ex); }
			finally {
				if (!commit) Task.Delay(200).ContinueWith(t => {
					lock (entry.Mutex) {
						entry.Pending = false;
						Refresh(entry);
					}
				});
			}
		}
	}
}
