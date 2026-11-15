//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General 
// Public License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.

namespace Gtk {

	using System;
	using System.Runtime.InteropServices;

	public partial class MessageDialog {
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_gtk_message_dialog_new(IntPtr parent_window, DialogFlags flags, MessageType type, ButtonsType bt, IntPtr msg);
		static d_gtk_message_dialog_new gtk_message_dialog_new = FuncLoader.LoadFunction<d_gtk_message_dialog_new>(FuncLoader.GetProcAddress(GLibrary.Load(Library.Gtk), "gtk_message_dialog_new"));
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_gtk_message_dialog_new_with_markup(IntPtr parent_window, DialogFlags flags, MessageType type, ButtonsType bt, IntPtr msg);
		static d_gtk_message_dialog_new_with_markup gtk_message_dialog_new_with_markup = FuncLoader.LoadFunction<d_gtk_message_dialog_new_with_markup>(FuncLoader.GetProcAddress(GLibrary.Load(Library.Gtk), "gtk_message_dialog_new_with_markup"));

		public MessageDialog (Gtk.Window parent_window, DialogFlags flags, MessageType type, ButtonsType bt, bool use_markup, string format, params object[] args) : base (IntPtr.Zero)
		{
			IntPtr p = (parent_window != null) ? parent_window.Handle : IntPtr.Zero;

			if (format == null) {
				Raw = gtk_message_dialog_new (p, flags, type, bt, IntPtr.Zero);
				return;
			}

			// Pass the composed text as the message_format with every per cent
			// sign doubled and no variadic argument. The "%s"-with-the-text-behind
			// it form is correct on x64 but reads garbage on Apple arm64, where the
			// variadic argument is passed on the stack, not the register a
			// fixed-arity delegate puts it in -- the dialog showed mojibake.
			// Doubling the per cent signs lets Gtk's printf render them back to
			// one, for plain text and markup alike, with no va_list ever read.
			var message = args != null && args.Length > 0 ? String.Format (format, args) : format;
			IntPtr nmsg = GLib.Marshaller.StringToPtrGStrdup (message.Replace ("%", "%%"));

			if (use_markup)
				Raw = gtk_message_dialog_new_with_markup (p, flags, type, bt, nmsg);
			else
				Raw = gtk_message_dialog_new (p, flags, type, bt, nmsg);

			GLib.Marshaller.Free (nmsg);
		}

		public MessageDialog (Gtk.Window parent_window, DialogFlags flags, MessageType type, ButtonsType bt, string format, params object[] args) : this (parent_window, flags, type, bt, true, format, args) {}

	}
}

