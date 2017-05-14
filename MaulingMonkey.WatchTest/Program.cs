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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace MaulingMonkey.WatchTest {
	class Program {
		static void Main(string[] args) {
			TestWatchNonexistantFile();
			TestWatchMutatingFile();
			TestWatchExistingFile_ThenDelete();
		}

		static void TestWatchNonexistantFile() {
			WithTempFile(path=>{
				try { File.Delete(path); } catch (IOException) { Inconclusive("Should've deleted temp file"); return; }

				try {
					Watch.FileLines(path, text => {}, error => {});
					Fail("Watch.FileLines(nonexistant, ...) should invoke only the error delegate");
				}
				catch (FileNotFoundException) {}
			});
		}

		static void TestWatchMutatingFile() {
			WithTempFile(path=>{
				var finalLines = RandomLines(PRNG);
				var lines = RandomLines(PRNG);

				var result = new TaskCompletionSource<bool>();
				File.WriteAllLines(path, lines);
				Watch.FileLines(path,
					read  => { if (EqualStringArrays(read.ToArray(), finalLines)) result.SetResult(true); },
					error => { if (!(error is IOException)) result.SetResult(false); }); // Highly contested files are likely to throw IOException s.

				// Stress
				for (var i=0; i<100; ++i) try { File.WriteAllLines(path, RandomLines(PRNG)); } catch (IOException) { }
				Thread.Sleep(10);
				for (var i=0; i<100; ++i) try { File.WriteAllLines(path, RandomLines(PRNG)); } catch (IOException) { }
				for (;;) try { File.WriteAllLines(path, finalLines); break; } catch (IOException) { }

				Assert(result.Task.Wait(1000) && result.Task.Result, "Watch.FileLines(existant, ...) should have ended up with the right final result.");
			});
		}

		static void TestWatchExistingFile_ThenDelete() {
			WithTempFile(path=>{
				var lines = RandomLines(PRNG);

				var result = new TaskCompletionSource<string[]>();
				var errResult = new TaskCompletionSource<Exception>();
				File.WriteAllLines(path, lines);
				Watch.FileLines(path,
					read  => result.SetResult(read.ToArray()),
					error => errResult.SetResult(error));
				Assert(EqualStringArrays(result.Task.Result, lines), "Watch.FileLines(existant, ...) should invoke the text delegate once");

				// Now delete
				try { File.Delete(path); } catch (IOException) { Inconclusive("Should've deleted temp file"); return; }
				var errTask = errResult.Task;
				Assert(errTask.Wait(1000) && (errTask.Result is FileNotFoundException), "Watch.FileLines(nonexistant, ...) should invoke only the error delegate");
			});
		}

		// TODO: Movement and recreation stress tests

		// I really need to start writing a MaulingMonkey.Test lib.

		static readonly Random PRNG = new Random();

		const string RandomCharacterSet = "abcdef 0123456789-_!\r\n";

		static void Inconclusive(string why) { if (Debugger.IsAttached) Debugger.Break(); }

		static void WithTempFile(Action<string> scope) {
			var path = Path.GetTempFileName();
			try { scope(path); }
			finally {
				if (File.Exists(path)) Until(()=>{
					try { File.Delete(path); return true; }
					catch (IOException) { return false; }
				}, TimeSpan.FromSeconds(5));
			}
		}

		static void Until(Func<bool> condition, TimeSpan maxWait) {
			var start = DateTime.Now;
			for(;;) {
				if (condition()) break;
				var now = DateTime.Now;
				if ((now-start) > maxWait) break;
			}
		}

		static string RandomLine(Random prng) {
			var sb = new StringBuilder(prng.Next(20,30));
			while (sb.Length < sb.Capacity-1) {
				var i = prng.Next(RandomCharacterSet.Length);
				var ch = RandomCharacterSet[i];
				if (ch == '\n' || ch == '\r') ch = '?';
				sb.Append(ch);
			}
			return sb.ToString();
		}

		static string[] RandomLines(Random prng) {
			var r = new string[prng.Next(3,6)];
			for (var i=0; i<r.Length; ++i) {
				r[i] = RandomLine(prng);
			}
			return r;
		}

		static bool EqualStringArrays(string[] lhs, string[] rhs) {
			if (lhs.Length != rhs.Length) return false;
			var n = lhs.Length;
			for (var i=0; i<n; ++i) if (lhs[i] != rhs[i]) return false;
			return true;
		}
	}
}
