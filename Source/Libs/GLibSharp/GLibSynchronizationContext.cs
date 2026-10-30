//
// GLibSynchronizationContext.cs
// 
// Author:
//       Rickard Edström <ickard@gmail.com>
//
// Copyright (c) 2010 Rickard Edström
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading;

namespace GLib
{
	/// <summary>
	/// A <see cref="SynchronizationContext"/> that marshals work onto the GLib
	/// main loop, so an <c>await</c> started on the Gtk thread resumes there.
	/// </summary>
	public class GLibSynchronizationContext : SynchronizationContext
	{
		readonly int ownerThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

		/// <summary>Posts <paramref name="d"/> to the main loop as an idle source and returns at once.</summary>
		public override void Post (SendOrPostCallback d, object state)
		{
			PostAction (() => d (state));
		}

		/// <summary>
		/// Runs <paramref name="d"/> on the main loop and waits for it to finish.
		/// </summary>
		/// <remarks>
		/// When the caller is already on the thread that owns the loop -- the
		/// thread this context was created on, or whichever thread is currently
		/// dispatching the default main context -- the delegate runs inline:
		/// posting it and blocking would wait for an idle source that only the
		/// blocked thread can run, which is a deadlock. Exceptions thrown by the
		/// delegate propagate to the caller in both cases.
		/// </remarks>
		public override void Send (SendOrPostCallback d, object state)
		{
			if (d == null)
				throw new ArgumentNullException ("d");

			if (IsOnOwningThread ()) {
				d (state);
				return;
			}

			var mre = new ManualResetEvent (false);
			Exception error = null;
			
			PostAction (() =>
			{
				try {
					d (state);
				} catch (Exception ex) {
					error = ex;
				} finally {
					mre.Set ();
				}
			});
			
			mre.WaitOne ();
			
			if (error != null) {
				throw error;
			}
		}

		bool IsOnOwningThread ()
		{
			if (System.Threading.Thread.CurrentThread.ManagedThreadId == ownerThreadId)
				return true;

			return MainContext.Default.IsOwner;
		}

		void PostAction (Action action)
		{
			Idle.Add (() =>
			{
				action ();
				return false;
			});
		}
	}
}
