using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Screeny
{
	/// <summary>Voce del menu della tray. Text null = separatore.</summary>
	public sealed record TrayMenuItem(string? Text, Action? Action = null, bool Enabled = true, bool IsDefault = false)
	{
		public static readonly TrayMenuItem Separator = new((string?)null);
	}

	/// <summary>
	/// Icona nell'area di notifica via Shell_NotifyIcon, con menu contestuale Win32 (le voci disattivate sono grigie).
	/// I messaggi dell'icona arrivano alla finestra principale tramite subclassing, anche quando è nascosta.
	/// ponytail: P/Invoke diretto invece di H.NotifyIcon, una dipendenza in meno per ~150 righe
	/// </summary>
	public sealed class TrayIcon : IDisposable
	{
		private const uint WM_NULL = 0x0000;
		private const uint WM_LBUTTONUP = 0x0202;
		private const uint WM_RBUTTONUP = 0x0205;
		private const uint WM_TRAYICON = 0x8000 + 1; // WM_APP + 1
		private const uint NIM_ADD = 0, NIM_DELETE = 2;
		private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4;
		private const uint MF_STRING = 0x0, MF_GRAYED = 0x1, MF_SEPARATOR = 0x800;
		private const uint TPM_RIGHTBUTTON = 0x2, TPM_NONOTIFY = 0x80, TPM_RETURNCMD = 0x100;
		private const int SM_CXSMICON = 49, SM_CYSMICON = 50;

		private readonly IntPtr _hwnd;
		private readonly IntPtr _icon;
		private readonly string _tooltip;
		private readonly uint _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
		private readonly SubclassProc _proc; // referenziato qui: il GC non deve raccogliere il delegate passato a Win32
		private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
		private bool _disposed;

		public TrayIcon(IntPtr hwnd, string iconPath, string tooltip)
		{
			_hwnd = hwnd;
			_tooltip = tooltip;
			_icon = LoadImageW(IntPtr.Zero, iconPath, 1 /* IMAGE_ICON */, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), 0x10 /* LR_LOADFROMFILE */);
			_proc = WndProc;
			SetWindowSubclass(_hwnd, _proc, 1, 0);
			Add();
		}

		/// <summary>Clic sinistro sull'icona.</summary>
		public event Action? Invoked;

		/// <summary>Voci del menu, ricalcolate a ogni apertura.</summary>
		public Func<IReadOnlyList<TrayMenuItem>>? MenuProvider { get; set; }

		/// <summary>Tema del menu Win32 (i menu nativi non seguono da soli il tema scelto in Screeny).</summary>
		public ElementTheme Theme { get; set; }

		private void Add()
		{
			var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
			Shell_NotifyIconW(NIM_ADD, ref data);
		}

		private NotifyIconData NewData(uint flags) => new()
		{
			cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
			hWnd = _hwnd,
			uID = 1,
			uFlags = flags,
			uCallbackMessage = WM_TRAYICON,
			hIcon = _icon,
			szTip = _tooltip,
		};

		private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr refData)
		{
			if (msg == WM_TRAYICON)
			{
				switch ((uint)lParam & 0xFFFF)
				{
					case WM_LBUTTONUP:
						_dispatcher.TryEnqueue(() => Invoked?.Invoke());
						break;
					case WM_RBUTTONUP:
						ShowMenu();
						break;
				}
				return IntPtr.Zero;
			}
			if (msg == _taskbarCreated)
				Add(); // Explorer riavviato: l'icona va registrata di nuovo
			return DefSubclassProc(hwnd, msg, wParam, lParam);
		}

		private void ShowMenu()
		{
			var items = MenuProvider?.Invoke() ?? [];
			var menu = CreatePopupMenu();
			for (var i = 0; i < items.Count; i++)
			{
				var item = items[i];
				if (item.Text is null)
				{
					AppendMenuW(menu, MF_SEPARATOR, 0, null);
					continue;
				}
				// id = indice + 1 (0 è "nessuna scelta"); '&' raddoppiata per non diventare un acceleratore
				AppendMenuW(menu, MF_STRING | (item.Enabled ? 0 : MF_GRAYED), (UIntPtr)(i + 1), item.Text.Replace("&", "&&"));
				if (item.IsDefault)
					SetMenuDefaultItem(menu, (uint)(i + 1), 0);
			}

			ApplyMenuTheme();
			GetCursorPos(out var point);
			SetForegroundWindow(_hwnd); // senza, il menu non si chiude cliccando altrove
			var chosen = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, point.X, point.Y, _hwnd, IntPtr.Zero);
			PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
			DestroyMenu(menu);

			if (chosen > 0 && items[chosen - 1].Action is { } action)
				_dispatcher.TryEnqueue(() => action());
		}

		// ponytail: SetPreferredAppMode/FlushMenuThemes sono export per ordinale non documentati di uxtheme
		// (gli stessi usati da Notepad++ e altri); se spariscono il menu resta semplicemente chiaro.
		private void ApplyMenuTheme()
		{
			try
			{
				SetPreferredAppMode(Theme switch
				{
					ElementTheme.Dark => 2, // ForceDark
					ElementTheme.Light => 3, // ForceLight
					_ => 1, // AllowDark: segue Windows
				});
				FlushMenuThemes();
			}
			catch (EntryPointNotFoundException)
			{
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;
			_disposed = true;
			var data = NewData(0);
			Shell_NotifyIconW(NIM_DELETE, ref data);
			RemoveWindowSubclass(_hwnd, _proc, 1);
			if (_icon != IntPtr.Zero)
				DestroyIcon(_icon);
		}

		// ── Win32 ───────────────────────────────────────────────────────────

		private delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr refData);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct NotifyIconData
		{
			public uint cbSize;
			public IntPtr hWnd;
			public uint uID;
			public uint uFlags;
			public uint uCallbackMessage;
			public IntPtr hIcon;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string szTip;
			public uint dwState;
			public uint dwStateMask;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public string szInfo;
			public uint uVersion;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
			public string szInfoTitle;
			public uint dwInfoFlags;
			public Guid guidItem;
			public IntPtr hBalloonIcon;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct Point
		{
			public int X;
			public int Y;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

		[DllImport("comctl32.dll")]
		private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc proc, nuint id, nuint refData);

		[DllImport("comctl32.dll")]
		private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc proc, nuint id);

		[DllImport("comctl32.dll")]
		private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern uint RegisterWindowMessageW(string name);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr LoadImageW(IntPtr instance, string name, uint type, int cx, int cy, uint load);

		[DllImport("user32.dll")]
		private static extern bool DestroyIcon(IntPtr icon);

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int index);

		[DllImport("user32.dll")]
		private static extern IntPtr CreatePopupMenu();

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern bool AppendMenuW(IntPtr menu, uint flags, nuint id, string? text);

		[DllImport("user32.dll")]
		private static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);

		[DllImport("user32.dll")]
		private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);

		[DllImport("user32.dll")]
		private static extern bool DestroyMenu(IntPtr menu);

		[DllImport("user32.dll")]
		private static extern bool GetCursorPos(out Point point);

		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hwnd);

		[DllImport("user32.dll")]
		private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("uxtheme.dll", EntryPoint = "#135")]
		private static extern int SetPreferredAppMode(int mode);

		[DllImport("uxtheme.dll", EntryPoint = "#136")]
		private static extern void FlushMenuThemes();
	}
}
