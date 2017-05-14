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
using System.IO;

namespace MaulingMonkey.Implementation {
	struct FileMetaSnapshot {
		public DateTime LastMod;
		public long     Size;

		public static bool operator==(FileMetaSnapshot left, FileMetaSnapshot right) {
			return (left.LastMod == right.LastMod)
				&& (left.Size    == right.Size);
		}
		public static bool operator!=(FileMetaSnapshot left, FileMetaSnapshot right) { return !(left == right); }
		public override bool Equals(object obj) { return base.Equals(obj); }
		public override int GetHashCode() { return base.GetHashCode(); }

		public static FileMetaSnapshot From(string path) {
			var fi = new FileInfo(path);
			var snapshot = new FileMetaSnapshot() {
				LastMod = fi.Exists ? fi.LastWriteTimeUtc : new DateTime(1971,1,1),
				Size    = fi.Exists ? fi.Length : 0,
			};
			return snapshot;
		}
	}
}
