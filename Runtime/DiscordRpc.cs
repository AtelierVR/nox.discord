using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using Nox.CCK.Mods.Libs;

public class DiscordRpc {
	// Delegate types matching the Discord RPC API signatures
	public delegate void InitializeDelegate(string applicationId, ref EventHandlers handlers, bool autoRegister, string optionalSteamId);
	public delegate void ShutdownDelegate();
	public delegate void RunCallbacksDelegate();
	public delegate void UpdatePresenceDelegate(ref RichPresenceStruct presence);
	public delegate void ClearPresenceDelegate();
	public delegate void RespondDelegate(string userId, Reply reply);
	public delegate void UpdateHandlersDelegate(ref EventHandlers handlers);

	// Loaded delegates from the native library
	private static InitializeDelegate _initialize;
	private static ShutdownDelegate _shutdown;
	private static RunCallbacksDelegate _runCallbacks;
	private static UpdatePresenceDelegate _updatePresence;
	private static ClearPresenceDelegate _clearPresence;
	private static RespondDelegate _respond;
	private static UpdateHandlersDelegate _updateHandlers;

	// Store ILibAPI reference for unloading
	private static ILibAPI _libApi;

	[MonoPInvokeCallback(typeof(OnReadyInfo))]
	public static void ReadyCallback(ref DiscordUser connectedUser) {
		Callbacks.readyCallback(ref connectedUser);
	}

	public delegate void OnReadyInfo(ref DiscordUser connectedUser);

	[MonoPInvokeCallback(typeof(OnDisconnectedInfo))]
	public static void DisconnectedCallback(int errorCode, string message) {
		Callbacks.disconnectedCallback(errorCode, message);
	}

	public delegate void OnDisconnectedInfo(int errorCode, string message);

	[MonoPInvokeCallback(typeof(OnErrorInfo))]
	public static void ErrorCallback(int errorCode, string message) {
		Callbacks.errorCallback(errorCode, message);
	}

	public delegate void OnErrorInfo(int errorCode, string message);

	[MonoPInvokeCallback(typeof(OnJoinInfo))]
	public static void JoinCallback(string secret) {
		Callbacks.joinCallback(secret);
	}

	public delegate void OnJoinInfo(string secret);

	[MonoPInvokeCallback(typeof(OnSpectateInfo))]
	public static void SpectateCallback(string secret) {
		Callbacks.spectateCallback(secret);
	}

	public delegate void OnSpectateInfo(string secret);

	[MonoPInvokeCallback(typeof(OnRequestInfo))]
	public static void RequestCallback(ref DiscordUser request) {
		Callbacks.requestCallback(ref request);
	}

	public delegate void OnRequestInfo(ref DiscordUser request);

	static EventHandlers Callbacks { get; set; }

	public struct EventHandlers {
		public OnReadyInfo        readyCallback;
		public OnDisconnectedInfo disconnectedCallback;
		public OnErrorInfo        errorCallback;
		public OnJoinInfo         joinCallback;
		public OnSpectateInfo     spectateCallback;
		public OnRequestInfo      requestCallback;
	}

	[Serializable, StructLayout(LayoutKind.Sequential)]
	public struct RichPresenceStruct {
		public IntPtr state;   /* max 128 bytes */
		public IntPtr details; /* max 128 bytes */
		public long   startTimestamp;
		public long   endTimestamp;
		public IntPtr largeImageKey;  /* max 32 bytes */
		public IntPtr largeImageText; /* max 128 bytes */
		public IntPtr smallImageKey;  /* max 32 bytes */
		public IntPtr smallImageText; /* max 128 bytes */
		public IntPtr partyId;        /* max 128 bytes */
		public int    partySize;
		public int    partyMax;
		public int    partyPrivacy;
		public IntPtr matchSecret;    /* max 128 bytes */
		public IntPtr joinSecret;     /* max 128 bytes */
		public IntPtr spectateSecret; /* max 128 bytes */
		public bool   instance;
	}

	[Serializable]
	public struct DiscordUser {
		public string userId;
		public string username;
		public string discriminator;
		public string avatar;
	}

	public enum Reply {
		No     = 0,
		Yes    = 1,
		Ignore = 2
	}

	public enum PartyPrivacy {
		Private = 0,
		Public  = 1
	}

	/// <summary>
	/// Initializes the Discord RPC library using the provided ILibAPI.
	/// Must be called before any other DiscordRpc methods.
	/// </summary>
	public static void InitializeLib(ILibAPI libApi) {
		_libApi = libApi;
		_libApi.Load("discord-rpc");
		_initialize      = _libApi.GetDelegate<InitializeDelegate>("discord-rpc", "Discord_Initialize");
		_shutdown        = _libApi.GetDelegate<ShutdownDelegate>("discord-rpc", "Discord_Shutdown");
		_runCallbacks    = _libApi.GetDelegate<RunCallbacksDelegate>("discord-rpc", "Discord_RunCallbacks");
		_updatePresence  = _libApi.GetDelegate<UpdatePresenceDelegate>("discord-rpc", "Discord_UpdatePresence");
		_clearPresence   = _libApi.GetDelegate<ClearPresenceDelegate>("discord-rpc", "Discord_ClearPresence");
		_respond         = _libApi.GetDelegate<RespondDelegate>("discord-rpc", "Discord_Respond");
		_updateHandlers  = _libApi.GetDelegate<UpdateHandlersDelegate>("discord-rpc", "Discord_UpdateHandlers");
	}

	/// <summary>
	/// Disposes the Discord RPC library, shutting down the connection and unloading the native library.
	/// </summary>
	public static void Dispose() {
		try {
			_shutdown?.Invoke();
		} catch {
			// Ignore errors during shutdown
		}
		_libApi?.Unload("discord-rpc");
		_libApi = null;
		_initialize = null;
		_shutdown = null;
		_runCallbacks = null;
		_updatePresence = null;
		_clearPresence = null;
		_respond = null;
		_updateHandlers = null;
	}

	public static void Initialize(string applicationId, ref EventHandlers handlers, bool autoRegister, string optionalSteamId) {
		Callbacks = handlers;

		EventHandlers staticEventHandlers = new EventHandlers();
		staticEventHandlers.readyCallback        += DiscordRpc.ReadyCallback;
		staticEventHandlers.disconnectedCallback += DiscordRpc.DisconnectedCallback;
		staticEventHandlers.errorCallback        += DiscordRpc.ErrorCallback;
		staticEventHandlers.joinCallback         += DiscordRpc.JoinCallback;
		staticEventHandlers.spectateCallback     += DiscordRpc.SpectateCallback;
		staticEventHandlers.requestCallback      += DiscordRpc.RequestCallback;

		_initialize(applicationId, ref staticEventHandlers, autoRegister, optionalSteamId);
	}

	public static void Shutdown() 
		=> _shutdown();

	public static void RunCallbacks() 
		=> _runCallbacks();

	public static void ClearPresence() 
		=> _clearPresence();

	public static void Respond(string userId, Reply reply) 
		=> _respond(userId, reply);

	public static void UpdateHandlers(ref EventHandlers handlers) 
		=> _updateHandlers(ref handlers);

	public static void UpdatePresence(RichPresence presence) {
		var presencestruct = presence.GetStruct();
		_updatePresence(ref presencestruct);
		presence.FreeMem();
	}

	public class RichPresence {
		private          RichPresenceStruct _presence;
		private readonly List<IntPtr>       _buffers = new List<IntPtr>(10);

		public string       state;   /* max 128 bytes */
		public string       details; /* max 128 bytes */
		public long         startTimestamp;
		public long         endTimestamp;
		public string       largeImageKey;  /* max 32 bytes */
		public string       largeImageText; /* max 128 bytes */
		public string       smallImageKey;  /* max 32 bytes */
		public string       smallImageText; /* max 128 bytes */
		public string       partyId;        /* max 128 bytes */
		public int          partySize;
		public int          partyMax;
		public PartyPrivacy partyPrivacy;
		public string       matchSecret;    /* max 128 bytes */
		public string       joinSecret;     /* max 128 bytes */
		public string       spectateSecret; /* max 128 bytes */
		public bool         instance;

		/// <summary>
		/// Get the <see cref="RichPresenceStruct"/> reprensentation of this instance
		/// </summary>
		/// <returns><see cref="RichPresenceStruct"/> reprensentation of this instance</returns>
		internal RichPresenceStruct GetStruct() {
			if (_buffers.Count > 0) {
				FreeMem();
			}

			_presence.state          = StrToPtr(state);
			_presence.details        = StrToPtr(details);
			_presence.startTimestamp = startTimestamp;
			_presence.endTimestamp   = endTimestamp;
			_presence.largeImageKey  = StrToPtr(largeImageKey);
			_presence.largeImageText = StrToPtr(largeImageText);
			_presence.smallImageKey  = StrToPtr(smallImageKey);
			_presence.smallImageText = StrToPtr(smallImageText);
			_presence.partyId        = StrToPtr(partyId);
			_presence.partySize      = partySize;
			_presence.partyMax       = partyMax;
			_presence.partyPrivacy   = (int)partyPrivacy;
			_presence.matchSecret    = StrToPtr(matchSecret);
			_presence.joinSecret     = StrToPtr(joinSecret);
			_presence.spectateSecret = StrToPtr(spectateSecret);
			_presence.instance       = instance;

			return _presence;
		}

		/// <summary>
		/// Returns a pointer to a representation of the given string with a size of maxbytes
		/// </summary>
		/// <param name="input">String to convert</param>
		/// <returns>Pointer to the UTF-8 representation of <see cref="input"/></returns>
		private IntPtr StrToPtr(string input) {
			if (string.IsNullOrEmpty(input)) return IntPtr.Zero;
			var convbytecnt = Encoding.UTF8.GetByteCount(input);
			var buffer      = Marshal.AllocHGlobal(convbytecnt + 1);
			for (int i = 0; i < convbytecnt + 1; i++) {
				Marshal.WriteByte(buffer, i, 0);
			}

			_buffers.Add(buffer);
			Marshal.Copy(Encoding.UTF8.GetBytes(input), 0, buffer, convbytecnt);
			return buffer;
		}

		/// <summary>
		/// Convert string to UTF-8 and add null termination
		/// </summary>
		/// <param name="toconv">string to convert</param>
		/// <returns>UTF-8 representation of <see cref="toconv"/> with added null termination</returns>
		private static string StrToUtf8NullTerm(string toconv) {
			var str   = toconv.Trim();
			var bytes = Encoding.Default.GetBytes(str);
			if (bytes.Length > 0 && bytes[bytes.Length - 1] != 0) {
				str += "\0\0";
			}

			return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(str));
		}

		/// <summary>
		/// Free the allocated memory for conversion to <see cref="RichPresenceStruct"/>
		/// </summary>
		internal void FreeMem() {
			for (var i = _buffers.Count - 1; i >= 0; i--) {
				Marshal.FreeHGlobal(_buffers[i]);
				_buffers.RemoveAt(i);
			}
		}
	}
}