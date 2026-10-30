// GtkVersion.cs - runtime check of the Gtk that is actually loaded.

namespace Gtk {

	using System;

	/// <summary>
	/// The version of the Gtk library loaded at run time, as opposed to the 4.22 the
	/// bindings were generated against.
	/// </summary>
	/// <remarks>
	/// An application that supports an older Gtk (the floor is typically 4.14)
	/// guards calls to newer API with <see cref="AtLeast(uint, uint, uint)"/>.
	/// Without the guard, a function the loaded library lacks throws a
	/// <see cref="NotSupportedException"/> naming the symbol.
	/// </remarks>
	public static class GtkVersion {

		/// <summary>The major version of the loaded Gtk.</summary>
		public static uint Major {
			get { return Global.MajorVersion; }
		}

		/// <summary>The minor version of the loaded Gtk.</summary>
		public static uint Minor {
			get { return Global.MinorVersion; }
		}

		/// <summary>The micro version of the loaded Gtk.</summary>
		public static uint Micro {
			get { return Global.MicroVersion; }
		}

		/// <summary>The loaded Gtk as a <see cref="System.Version"/>.</summary>
		public static Version Current {
			get { return new Version ((int) Major, (int) Minor, (int) Micro); }
		}

		/// <summary>
		/// Whether the loaded Gtk is at least <paramref name="major"/>.<paramref name="minor"/>.<paramref name="micro"/>.
		/// </summary>
		public static bool AtLeast (uint major, uint minor, uint micro = 0)
		{
			return Compare (Major, Minor, Micro, major, minor, micro) >= 0;
		}

		static int Compare (uint major, uint minor, uint micro, uint requiredMajor, uint requiredMinor, uint requiredMicro)
		{
			if (major != requiredMajor)
				return major > requiredMajor ? 1 : -1;
			if (minor != requiredMinor)
				return minor > requiredMinor ? 1 : -1;
			if (micro != requiredMicro)
				return micro > requiredMicro ? 1 : -1;
			return 0;
		}
	}
}
